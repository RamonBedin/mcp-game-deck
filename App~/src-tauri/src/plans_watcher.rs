//! Filesystem watcher for the active project's plans directory.
//!
//! Emits the `plans-changed` Tauri event whenever a `.md` file under
//! `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/` is created,
//! modified, or deleted.
//!
//! **Kind synthesis caveat:** `notify-debouncer-mini` collapses every
//! native event into `DebouncedEventKind::Any` — the lighter debouncer
//! in the `notify` family does not preserve create/modify/delete
//! distinctions. To populate `PlansChangedKind`, this module tracks a
//! `HashSet<String>` of currently-known plan names and synthesizes the
//! kind by checking presence-before + `path.exists()`-now at event
//! delivery time (see [`classify_event`]). The consumer (`plansStore`
//! ) refetches `list_plans` on every event, so the
//! synthesized kind is informational — a delete-then-create within the
//! 250ms debounce window will collapse into a single `Modified` event
//! rather than `Deleted` + `Created`, and that's accepted as a
//! cosmetic limitation.
//!
//! **`.md.tmp` filter:** `write_plan` writes through a `<name>.md.tmp`
//! file then renames; that sequence produces four native FS events
//! (create tmp, modify tmp, delete tmp, create md). The 250ms debounce
//! collapses them, but we ALSO filter on `extension == "md"` before
//! classifying so the tmp path never surfaces as an event (its stem
//! would be `<name>.md`, which is invalid for the consumer).
//!
//! **Lifecycle:**
//! - Started from `lib.rs::setup` (Tauri-managed state).
//! - Stopped from the `CloseRequested` handler before `app.exit(0)`.
//! - Restarted by `commands::connection::restart_supervisor` so a
//!   `UNITY_PROJECT_PATH` change picks up the new project's plans dir.
//! - If no project root resolves at startup, the background task polls
//!   every 5s until one appears.

use std::collections::HashSet;
use std::path::Path;
use std::sync::Arc;
use std::time::Duration;

use notify::RecursiveMode;
use notify_debouncer_mini::{new_debouncer, DebounceEventResult};
use tauri::AppHandle;
use tokio::sync::{mpsc, oneshot, Mutex};
use tokio::task::JoinHandle;

use crate::commands::plans::{ensure_plans_dir, plans_dir};
use crate::events::emit_plans_changed;
use crate::types::{PlansChangedKind, PlansChangedPayload};

// region: Constants

const POLL_INTERVAL: Duration = Duration::from_secs(5);
const REBIND_BACKOFF: Duration = Duration::from_secs(1);
const DEBOUNCE_WINDOW: Duration = Duration::from_millis(250);

// endregion

// region: PlansWatcher

/// Tauri-managed handle for the plans-directory watcher.
///
/// Owns the optional running task plus its stop channel; both reset
/// together on every `start` / `stop` so the handle never reports a
/// stale "running" state when the task has already terminated.
pub struct PlansWatcher {
    state: Arc<Mutex<Option<WatcherHandles>>>,
}

struct WatcherHandles {
    stop_tx: oneshot::Sender<()>,
    join: JoinHandle<()>,
}

impl PlansWatcher {
    /// Builds a fresh watcher in the stopped state. No background task
    /// is spawned until `start` is called.
    pub fn new() -> Self {
        Self {
            state: Arc::new(Mutex::new(None)),
        }
    }

    /// Starts the watcher background task. If a previous task is alive,
    /// stops it first so callers don't have to sequence the teardown.
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

impl Default for PlansWatcher {
    fn default() -> Self {
        Self::new()
    }
}

// endregion

// region: Event classification

/// Synthesizes a [`PlansChangedKind`] from before/after presence.
///
/// `notify-debouncer-mini` collapses native event kinds into
/// `DebouncedEventKind::Any`, so the create/modify/delete distinction
/// is reconstructed here by comparing the in-memory set of
/// previously-known names against the path's current `exists()`. The
/// fourth quadrant — `!was_known && !exists_now` — is theoretically
/// unreachable (the path appeared in an FS event but doesn't exist
/// and wasn't tracked) and falls back to `Modified` as the
/// least-destructive default: the React consumer refetches anyway,
/// and "Modified" avoids incorrectly suggesting a delete that never
/// happened.
pub(crate) fn classify_event(was_known: bool, exists_now: bool) -> PlansChangedKind {
    match (was_known, exists_now) {
        (false, true) => PlansChangedKind::Created,
        (true, true) => PlansChangedKind::Modified,
        (true, false) => PlansChangedKind::Deleted,
        (false, false) => PlansChangedKind::Modified,
    }
}

// endregion

// region: Snapshot

/// Reads the current `.md` filenames (stems, no extension) under
/// `dir`. Returns an empty set on read errors — the watcher loop will
/// re-attempt on the next rebind.
fn read_known_names(dir: &Path) -> HashSet<String> {
    let Ok(entries) = std::fs::read_dir(dir) else {
        return HashSet::new();
    };
    entries
        .filter_map(|e| e.ok())
        .filter_map(|e| {
            let path = e.path();
            if path.extension().and_then(|s| s.to_str()) != Some("md") {
                return None;
            }
            path.file_stem()
                .and_then(|s| s.to_str())
                .map(str::to_string)
        })
        .collect()
}

// endregion

// region: Run loop

/// Outer loop: resolves the plans dir, attaches the debouncer, drains
/// events, and re-attaches after backoff on error or channel close.
/// Exits on `stop_rx`.
async fn run_watcher_loop(app: AppHandle, mut stop_rx: oneshot::Receiver<()>) {
    loop {
        let dir = loop {
            if let Some(d) = plans_dir() {
                match ensure_plans_dir(&d) {
                    Ok(()) => break d,
                    Err(e) => {
                        eprintln!(
                            "[plans-watcher] ensure_plans_dir failed at {}: {e}; retrying",
                            d.display()
                        );
                    }
                }
            }
            tokio::select! {
                _ = &mut stop_rx => return,
                _ = tokio::time::sleep(POLL_INTERVAL) => {}
            }
        };

        let mut known = read_known_names(&dir);

        let (tx, mut rx) = mpsc::unbounded_channel::<DebounceEventResult>();
        let mut debouncer = match new_debouncer(DEBOUNCE_WINDOW, move |res| {
            let _ = tx.send(res);
        }) {
            Ok(d) => d,
            Err(e) => {
                eprintln!("[plans-watcher] failed to create debouncer: {e}; backing off");
                tokio::select! {
                    _ = &mut stop_rx => return,
                    _ = tokio::time::sleep(REBIND_BACKOFF) => {}
                }
                continue;
            }
        };
        if let Err(e) = debouncer
            .watcher()
            .watch(&dir, RecursiveMode::NonRecursive)
        {
            eprintln!(
                "[plans-watcher] watch({}) failed: {e}; backing off",
                dir.display()
            );
            drop(debouncer);
            tokio::select! {
                _ = &mut stop_rx => return,
                _ = tokio::time::sleep(REBIND_BACKOFF) => {}
            }
            continue;
        }

        let needs_rebind = drain_events(&app, &mut rx, &mut stop_rx, &mut known).await;
        drop(debouncer);

        match needs_rebind {
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

/// Inner loop: pulls from the debouncer's channel, filters non-`.md`
/// paths, classifies each remaining path, and emits. Returns `Stop`
/// when the caller asked us to stop, `Rebind` when the channel closed
/// or the underlying watcher reported an error.
async fn drain_events(
    app: &AppHandle,
    rx: &mut mpsc::UnboundedReceiver<DebounceEventResult>,
    stop_rx: &mut oneshot::Receiver<()>,
    known: &mut HashSet<String>,
) -> DrainOutcome {
    loop {
        tokio::select! {
            _ = &mut *stop_rx => return DrainOutcome::Stop,
            msg = rx.recv() => match msg {
                None => return DrainOutcome::Rebind,
                Some(Err(e)) => {
                    eprintln!("[plans-watcher] notify error: {e}");
                    return DrainOutcome::Rebind;
                }
                Some(Ok(events)) => {
                    for event in events {
                        if event.path.extension().and_then(|s| s.to_str()) != Some("md") {
                            continue;
                        }
                        let Some(name) = event
                            .path
                            .file_stem()
                            .and_then(|s| s.to_str())
                            .map(str::to_string)
                        else {
                            continue;
                        };
                        let exists_now = event.path.exists();
                        let was_known = known.contains(&name);
                        let kind = classify_event(was_known, exists_now);
                        if exists_now {
                            known.insert(name.clone());
                        } else {
                            known.remove(&name);
                        }
                        if let Err(e) = emit_plans_changed(
                            app,
                            PlansChangedPayload {
                                kind,
                                name: Some(name),
                            },
                        ) {
                            eprintln!("[plans-watcher] emit failed: {e}");
                        }
                    }
                }
            }
        }
    }
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn classify_event_unknown_then_exists_is_created() {
        assert_eq!(classify_event(false, true), PlansChangedKind::Created);
    }

    #[test]
    fn classify_event_known_then_exists_is_modified() {
        assert_eq!(classify_event(true, true), PlansChangedKind::Modified);
    }

    #[test]
    fn classify_event_known_then_missing_is_deleted() {
        assert_eq!(classify_event(true, false), PlansChangedKind::Deleted);
    }

    #[test]
    fn classify_event_unknown_then_missing_defaults_to_modified() {
        assert_eq!(classify_event(false, false), PlansChangedKind::Modified);
    }
}