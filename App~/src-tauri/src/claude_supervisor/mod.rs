//! Claude Code supervisor module — owns the Claude Code subprocess that
//! powers the chat experience.
//!
//! wires the real spawn: launches a Node child running
//! `App~/runtime/sdk-entry.js`, captures stdout for the AgentMessage
//! protocol, and pushes user input via stdin. Health check formal

pub mod install_check;
pub mod lifecycle;
pub mod paths;
pub mod runtime_setup;
pub mod sdk_install;
pub mod spawn;

use std::sync::Arc;
use std::sync::Mutex as StdMutex;

use tauri::AppHandle;
use tokio::io::AsyncWriteExt;
use tokio::sync::Mutex;
use tokio::sync::{mpsc, oneshot};
use tokio::task::JoinHandle;

use crate::events::emit_supervisor_status_changed;
use crate::types::{PermissionMode, SupervisorStatus, SupervisorStatusChangedPayload};

// region: SpawnError

/// Failure modes for `ClaudeSupervisor::spawn`. `MissingProjectPath`
/// is the soft case the others are hard failures.
#[derive(Debug)]
pub enum SpawnError {
    MissingProjectPath,
    EntryScriptWrite(std::io::Error),
    NodeSpawn(std::io::Error),
}

impl std::fmt::Display for SpawnError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SpawnError::MissingProjectPath => write!(
                f,
                "UNITY_PROJECT_PATH env var not set (F07 launch contract)"
            ),
            SpawnError::EntryScriptWrite(e) => {
                write!(f, "failed to write sdk-entry.js: {e}")
            }
            SpawnError::NodeSpawn(e) => write!(f, "failed to spawn Node child: {e}"),
        }
    }
}

impl std::error::Error for SpawnError {}

// endregion

// region: SendError

/// Failure modes for `ClaudeSupervisor::send_input`.
#[derive(Debug)]
pub enum SendError {
    NotRunning,
    WriterClosed,
    Serde(serde_json::Error),
}

impl std::fmt::Display for SendError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SendError::NotRunning => write!(f, "supervisor not running"),
            SendError::WriterClosed => write!(f, "stdin writer task closed"),
            SendError::Serde(e) => write!(f, "serde error: {e}"),
        }
    }
}

impl std::error::Error for SendError {}

// endregion

// region: ClaudeSupervisor

/// Tauri-managed supervisor for the Claude Code subprocess.
pub struct ClaudeSupervisor {
    status: Arc<StdMutex<SupervisorStatus>>,
    permission_mode: Arc<StdMutex<PermissionMode>>,
    resume_session_id: Arc<StdMutex<Option<String>>>,
    state: Arc<Mutex<State>>,
}

/// Runtime handles owned by a single live supervisor session. The
/// fields reset together on `spawn` and `shutdown` — never out of
/// sync with the `status` field.
///
/// The `Child` itself is no longer kept here: it's owned by the
/// `monitor_child_exit` task spawned in `spawn`. To request shutdown,
/// `shutdown_inner` fires `kill_tx` and awaits `monitor_handle` so the
/// monitor task kills + reaps the child before the next `spawn` runs.
struct State {
    stdin_tx: Option<mpsc::UnboundedSender<String>>,
    kill_tx: Option<oneshot::Sender<()>>,
    monitor_handle: Option<JoinHandle<()>>,
}

impl ClaudeSupervisor {
    /// Builds a fresh supervisor in the `Idle` state. No child is
    /// spawned until `spawn` is called.
    pub fn new() -> Self {
        Self {
            status: Arc::new(StdMutex::new(SupervisorStatus::Idle)),
            permission_mode: Arc::new(StdMutex::new(PermissionMode::Default)),
            resume_session_id: Arc::new(StdMutex::new(None)),
            state: Arc::new(Mutex::new(State {
                stdin_tx: None,
                kill_tx: None,
                monitor_handle: None,
            })),
        }
    }

    /// Returns the current supervisor status. Microsecond-cheap —
    /// the lock is never held across an await.
    pub fn current_status(&self) -> SupervisorStatus {
        *self.status.lock().expect("supervisor status mutex poisoned")
    }

    /// Returns the supervisor's currently-selected permission mode.
    /// Mirrors what `sdk-entry.js` will apply on the next `query()`
    /// round-trip — kept in sync via control messages on stdin.
    pub fn current_permission_mode(&self) -> PermissionMode {
        *self
            .permission_mode
            .lock()
            .expect("supervisor permission_mode mutex poisoned")
    }

    /// Updates the supervisor's permission mode. Always writes the new
    /// mode to local state so React's "current mode" read stays
    /// accurate; additionally pushes a `setPermissionMode` control
    /// message to `sdk-entry.js`'s stdin when the supervisor is
    /// running so the next prompt picks up the new mode.
    ///
    /// Returns `Ok(())` even when the supervisor isn't running — the
    /// mode is stored anyway and re-pushed on the next `spawn`.
    ///
    /// # Errors
    ///
    /// `SendError::WriterClosed` when the stdin writer task has
    /// exited. `SendError::Serde` on encoding failure.
    pub async fn set_permission_mode(
        &self,
        mode: PermissionMode,
    ) -> Result<(), SendError> {
        {
            let mut current = self
                .permission_mode
                .lock()
                .expect("supervisor permission_mode mutex poisoned");
            *current = mode;
        }
        self.push_permission_mode_line(mode).await
    }

    /// Internal — serializes a `setPermissionMode` JSON line and
    /// pushes it onto the stdin writer's mpsc channel. Soft-success
    /// when the supervisor isn't running (no child to talk to).
    async fn push_permission_mode_line(
        &self,
        mode: PermissionMode,
    ) -> Result<(), SendError> {
        let line = serde_json::to_string(&serde_json::json!({
            "type": "setPermissionMode",
            "mode": mode,
        }))
        .map_err(SendError::Serde)?;
        let s = self.state.lock().await;
        let tx = match s.stdin_tx.as_ref() {
            Some(tx) => tx,
            None => return Ok(()),
        };
        tx.send(line).map_err(|_| SendError::WriterClosed)?;
        Ok(())
    }

    /// Returns the session id the supervisor is configured to resume
    /// on the next `query()` round-trip, or `None` for a fresh
    /// session.
    pub fn current_resume_session_id(&self) -> Option<String> {
        self.resume_session_id
            .lock()
            .expect("supervisor resume_session_id mutex poisoned")
            .clone()
    }

    /// Pins a session id for the supervisor to resume — every
    /// subsequent prompt is appended to that session's JSONL until
    /// `clear_resume_session` runs (or the user picks another).
    /// Pushes a `setResumeSession` control message to the JS side
    /// when running; soft-success when not.
    pub async fn set_resume_session(&self, id: String) -> Result<(), SendError> {
        {
            let mut current = self
                .resume_session_id
                .lock()
                .expect("supervisor resume_session_id mutex poisoned");
            *current = Some(id.clone());
        }
        self.push_resume_session_line(Some(id)).await
    }

    /// Resets the supervisor back to a fresh session — the next
    /// prompt starts a new JSONL file. Pushes a `clearResumeSession`
    /// control message when running.
    pub async fn clear_resume_session(&self) -> Result<(), SendError> {
        {
            let mut current = self
                .resume_session_id
                .lock()
                .expect("supervisor resume_session_id mutex poisoned");
            *current = None;
        }
        self.push_resume_session_line(None).await
    }

    /// Internal — pushes either `{type:"setResumeSession",sessionId}`
    /// or `{type:"clearResumeSession"}` to the JS stdin. Soft-success
    /// when the supervisor isn't running.
    async fn push_resume_session_line(
        &self,
        id: Option<String>,
    ) -> Result<(), SendError> {
        let payload = match id {
            Some(session_id) => serde_json::json!({
                "type": "setResumeSession",
                "sessionId": session_id,
            }),
            None => serde_json::json!({
                "type": "clearResumeSession",
            }),
        };
        let line = serde_json::to_string(&payload).map_err(SendError::Serde)?;
        let s = self.state.lock().await;
        let tx = match s.stdin_tx.as_ref() {
            Some(tx) => tx,
            None => return Ok(()),
        };
        tx.send(line).map_err(|_| SendError::WriterClosed)?;
        Ok(())
    }

    /// Launches `sdk-entry.js` as a Node child, wires stdin/stdout/
    /// stderr.
    ///
    /// Status flow: Idle -> Starting -> (JS emits ready) -> health
    /// check trigger scheduled (1.5s delay) -> health check runs in
    /// JS with 5s race timeout -> health-ok -> Ready, OR health-failed
    /// -> Crashed. The 5s ready-signal legacy timeout was removed in
    /// 6.1 — the health check exercises the full pipeline and is the
    /// single source of truth for liveness now.
    ///
    /// # Arguments
    ///
    /// * `app` - Application handle used to emit status-changed +
    ///   agent-message events from the reader task.
    ///
    /// # Returns
    ///
    /// The OS process id of the freshly spawned Node child (`0` if
    /// unknown).
    ///
    /// # Errors
    ///
    /// `SpawnError::MissingProjectPath` when the F07 env contract
    /// is broken. `SpawnError::EntryScriptWrite` /
    /// `SpawnError::NodeSpawn` for filesystem / process failures.
    pub async fn spawn(&self, app: AppHandle) -> Result<u32, SpawnError> {
        self.shutdown_inner(false).await;

        let project_path = std::env::var("UNITY_PROJECT_PATH")
            .ok()
            .filter(|s| !s.is_empty())
            .unwrap_or_else(|| {
                let fallback = paths::runtime_dir().to_string_lossy().to_string();
                eprintln!(
                    "[claude-supervisor] UNITY_PROJECT_PATH not set; using dev fallback cwd={fallback}"
                );
                fallback
            });

        if let Err(e) = runtime_setup::ensure_entry_script().await {
            self.set_status(&app, SupervisorStatus::Failed, None);
            return Err(SpawnError::EntryScriptWrite(e));
        }

        self.set_status(&app, SupervisorStatus::Starting, None);

        let mut child = match spawn::spawn_node_child(&app, &project_path) {
            Ok(c) => c,
            Err(e) => {
                self.set_status(&app, SupervisorStatus::Failed, None);
                return Err(SpawnError::NodeSpawn(e));
            }
        };
        let pid = child.id().unwrap_or(0);
        let stdin = child.stdin.take().expect("stdin piped");
        let stdout = child.stdout.take().expect("stdout piped");
        let stderr = child.stderr.take().expect("stderr piped");

        let (tx, mut rx) = mpsc::unbounded_channel::<String>();
        tokio::spawn(async move {
            let mut writer = stdin;
            while let Some(line) = rx.recv().await {
                if writer.write_all(line.as_bytes()).await.is_err() {
                    break;
                }
                if writer.write_all(b"\n").await.is_err() {
                    break;
                }
                if writer.flush().await.is_err() {
                    break;
                }
            }
        });

        let app_for_stdout = app.clone();
        let status_for_stdout = self.status.clone();
        let permission_mode_for_stdout = self.permission_mode.clone();
        let stdin_tx_for_stdout = tx.clone();
        tokio::spawn(async move {
            spawn::read_stdout(
                stdout,
                app_for_stdout,
                status_for_stdout,
                permission_mode_for_stdout,
                stdin_tx_for_stdout,
            )
            .await;
        });

        tokio::spawn(async move {
            spawn::read_stderr(stderr).await;
        });

        let (kill_tx, kill_rx) = oneshot::channel::<()>();
        let app_for_monitor = app.clone();
        let status_for_monitor = self.status.clone();
        let monitor_handle = tokio::spawn(async move {
            lifecycle::monitor_child_exit(
                child,
                kill_rx,
                app_for_monitor,
                status_for_monitor,
            )
            .await;
        });

        {
            let mut s = self.state.lock().await;
            s.stdin_tx = Some(tx);
            s.kill_tx = Some(kill_tx);
            s.monitor_handle = Some(monitor_handle);
        }

        let stored_mode = self.current_permission_mode();
        if stored_mode != PermissionMode::Default {
            if let Err(e) = self.push_permission_mode_line(stored_mode).await {
                eprintln!(
                    "[claude-supervisor] failed to re-sync permission mode after spawn: {e}"
                );
            }
        }

        if let Some(stored_session) = self.current_resume_session_id() {
            if let Err(e) =
                self.push_resume_session_line(Some(stored_session)).await
            {
                eprintln!(
                    "[claude-supervisor] failed to re-sync resume session after spawn: {e}"
                );
            }
        }


        Ok(pid)
    }

    /// Sends a user input to `sdk-entry.js` as the JSON line
    /// `{"type":"input","text":"...","attachments":[...]}`.
    ///
    /// `attachments` carries absolute paths the user attached alongside
    /// the prompt. Real handling lands in Group 5; today the field is
    /// always an empty slice from `commands::conversation::send_message`
    /// and `sdk-entry.js` only logs it for visibility.
    ///
    /// # Errors
    ///
    /// `SendError::NotRunning` when the supervisor has no child.
    /// `SendError::WriterClosed` when the stdin writer task has
    /// exited (typically because the child died). `SendError::Serde`
    /// on encoding failure (extremely unlikely for a plain string).
    pub async fn send_input(
        &self,
        text: &str,
        attachments: &[String],
    ) -> Result<(), SendError> {
        let line = serde_json::to_string(&serde_json::json!({
            "type": "input",
            "text": text,
            "attachments": attachments,
        }))
        .map_err(SendError::Serde)?;
        let s = self.state.lock().await;
        let tx = s.stdin_tx.as_ref().ok_or(SendError::NotRunning)?;
        tx.send(line).map_err(|_| SendError::WriterClosed)?;
        Ok(())
    }

    /// Tears down the running Claude Code subprocess and resets the
    /// status to `Idle`. Idempotent.
    pub async fn shutdown(&self) {
        self.shutdown_inner(true).await;
    }

    /// Internal teardown shared by `spawn` (pre-spawn cleanup, no
    /// status emit) and the public `shutdown` (post-shutdown status
    /// emit). Drops the stdin writer, sets status to `Idle` BEFORE
    /// firing the kill signal — so any race with `monitor_child_exit`'s
    /// wait branch sees the new status and skips its emit — then asks
    /// the monitor task to kill + reap the child and awaits the
    /// monitor's completion.
    ///
    /// `emit_status` is reserved for 6.3: the explicit shutdown path
    /// (window close) will surface `Idle` to React via this flag so
    /// the UI reflects the teardown before the window goes away.
    async fn shutdown_inner(&self, emit_status: bool) {
        let (kill_tx, monitor_handle) = {
            let mut s = self.state.lock().await;
            s.stdin_tx = None;
            (s.kill_tx.take(), s.monitor_handle.take())
        };
        *self.status.lock().expect("supervisor status mutex poisoned") =
            SupervisorStatus::Idle;
        if let Some(tx) = kill_tx {
            let _ = tx.send(());
        }
        if let Some(h) = monitor_handle {
            let _ = h.await;
        }
        if emit_status {
        }
    }

    /// Internal — sets status and emits the changed event.
    fn set_status(&self, app: &AppHandle, status: SupervisorStatus, pid: Option<u32>) {
        *self.status.lock().expect("supervisor status mutex poisoned") = status;
        let _ = emit_supervisor_status_changed(
            app,
            SupervisorStatusChangedPayload { status, pid },
        );
    }
}

impl Default for ClaudeSupervisor {
    fn default() -> Self {
        Self::new()
    }
}

// endregion