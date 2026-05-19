//! Filesystem watcher for the active Unity project root.
//!
//! Watches `<UNITY_PROJECT_PATH>` recursively, debounced at 250ms.
//! Every batch invalidates the `FilesIndex` cache (so the next call
//! to `list_project_files` rebuilds) and emits a single
//! `project-files-changed` event so React's `useProjectFiles` can
//! refetch.
//!
//! **Why no per-file filtering at the watcher level:** notify watches
//! the entire tree (including `Library/`, `Temp/`, etc), but the
//! consumer cares only about "something changed somewhere." Filtering
//! happens during the walker rebuild in `commands::files` — pruning
//! noisy subtrees there is cheap and avoids reconfiguring the
//! watcher when the exclusion list evolves. The trade-off is that
//! frequent Library/Cache churn fires through the debouncer and into
//! one cache-invalidate + emit per 250ms window even when no
//! user-visible file changed.
//!
//! **Lifecycle:** identical to [`crate::plans_watcher::PlansWatcher`]
//! — start in `lib.rs::setup`, stop on `CloseRequested`, restart from
//! `commands::connection::restart_supervisor`. Polls every 5s while
//! no project root resolves and rebinds with backoff on watcher
//! errors.

use std::path::PathBuf;
use std::sync::Arc;
use std::time::Duration;

use notify::RecursiveMode;
use notify_debouncer_mini::{new_debouncer, DebounceEventResult};
use tauri::{AppHandle, Manager};
use tokio::sync::{mpsc, oneshot, Mutex};
use tokio::task::JoinHandle;

use crate::commands::files::FilesIndex;
use crate::events::emit_project_files_changed;
use crate::project_root::try_resolve_project_root;
use crate::types::ProjectFilesChangedPayload;

// region: Constants

const POLL_INTERVAL: Duration = Duration::from_secs(5);
const REBIND_BACKOFF: Duration = Duration::from_secs(1);
const DEBOUNCE_WINDOW: Duration = Duration::from_millis(250);

// endregion

// region: FilesWatcher

/// Tauri-managed handle for the project-root file watcher.
///
/// Mirrors the shape of [`crate::plans_watcher::PlansWatcher`]: an
/// optional running task with its stop channel, both reset together
/// on every `start` / `stop`.
pub struct FilesWatcher {
    state: Arc<Mutex<Option<WatcherHandles>>>,
}

struct WatcherHandles {
    stop_tx: oneshot::Sender<()>,
    join: JoinHandle<()>,
}

impl FilesWatcher {
    /// Builds a fresh watcher in the stopped state. No background
    /// task is spawned until `start` is called.
    pub fn new() -> Self {
        Self {
            state: Arc::new(Mutex::new(None)),
        }
    }

    /// Starts the watcher background task. If a previous task is
    /// alive, stops it first so callers don't have to sequence the
    /// teardown.
    pub async fn start(&self, app: AppHandle) {
        self.stop().await;
        let (stop_tx, stop_rx) = oneshot::channel();
        let app_for_task = app.clone();
        let join = tokio::spawn(async move {
            run_watcher_loop(app_for_task, stop_rx).await;
        });
        let mut s = self.state.lock().await;
        *s = Some(WatcherHandles { stop_tx, join });
    }

    /// Signals the watcher to stop and awaits its task. Idempotent —
    /// a no-op when no task is running.
    pub async fn stop(&self) {
        let handles = {
            let mut s = self.state.lock().await;
            s.take()
        };
        if let Some(h) = handles {
            let _ = h.stop_tx.send(());
            let _ = h.join.await;
        }
    }
}

impl Default for FilesWatcher {
    fn default() -> Self {
        Self::new()
    }
}

// endregion

// region: Run loop

/// Outer loop: resolves the project root, attaches the debouncer,
/// drains events, and re-attaches after backoff on error or channel
/// close. Exits on `stop_rx`.
async fn run_watcher_loop(app: AppHandle, mut stop_rx: oneshot::Receiver<()>) {
    loop {
        let root: PathBuf = loop {
            if let Some(r) = try_resolve_project_root() {
                if r.exists() {
                    break r;
                }
                eprintln!(
                    "[files-watcher] project root {} does not exist yet; polling",
                    r.display()
                );
            }
            tokio::select! {
                _ = &mut stop_rx => return,
                _ = tokio::time::sleep(POLL_INTERVAL) => {}
            }
        };

        let (tx, mut rx) = mpsc::unbounded_channel::<DebounceEventResult>();
        let mut debouncer = match new_debouncer(DEBOUNCE_WINDOW, move |res| {
            let _ = tx.send(res);
        }) {
            Ok(d) => d,
            Err(e) => {
                eprintln!("[files-watcher] failed to create debouncer: {e}; backing off");
                tokio::select! {
                    _ = &mut stop_rx => return,
                    _ = tokio::time::sleep(REBIND_BACKOFF) => {}
                }
                continue;
            }
        };
        if let Err(e) = debouncer
            .watcher()
            .watch(&root, RecursiveMode::Recursive)
        {
            eprintln!(
                "[files-watcher] watch({}) failed: {e}; backing off",
                root.display()
            );
            drop(debouncer);
            tokio::select! {
                _ = &mut stop_rx => return,
                _ = tokio::time::sleep(REBIND_BACKOFF) => {}
            }
            continue;
        }

        let outcome = drain_events(&app, &mut rx, &mut stop_rx).await;
        drop(debouncer);

        match outcome {
            DrainOutcome::Stop => return,
            DrainOutcome::Rebind => {
                tokio::select! {
                    _ = &mut stop_rx => return,
                    _ = tokio::time::sleep(REBIND_BACKOFF) => {}
                }
            }
        }
    }
}

enum DrainOutcome {
    Stop,
    Rebind,
}

/// Inner loop: pulls debounce batches, invalidates the file index
/// cache, and emits a single `project-files-changed` event per
/// batch. `debounced` is `true` when the batch collapsed more than
/// one underlying FS event.
async fn drain_events(
    app: &AppHandle,
    rx: &mut mpsc::UnboundedReceiver<DebounceEventResult>,
    stop_rx: &mut oneshot::Receiver<()>,
) -> DrainOutcome {
    loop {
        tokio::select! {
            _ = &mut *stop_rx => return DrainOutcome::Stop,
            msg = rx.recv() => match msg {
                None => return DrainOutcome::Rebind,
                Some(Err(e)) => {
                    eprintln!("[files-watcher] notify error: {e}");
                    return DrainOutcome::Rebind;
                }
                Some(Ok(events)) => {
                    if events.is_empty() {
                        continue;
                    }
                    if let Some(index) = app.try_state::<FilesIndex>() {
                        index.invalidate();
                    }
                    let payload = ProjectFilesChangedPayload {
                        debounced: events.len() > 1,
                    };
                    if let Err(e) = emit_project_files_changed(app, payload) {
                        eprintln!("[files-watcher] emit failed: {e}");
                    }
                }
            }
        }
    }
}

// endregion