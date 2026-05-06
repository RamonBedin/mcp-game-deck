//! Supervisor lifecycle helpers — startup health check, child exit
//! monitor; future shutdown / restart hooks will land here too.
//!
//! The health check verifies that the spawned `claude` subprocess can
//! actually answer a query — `sdk-entry.js`'s `{type:"ready"}` only
//! confirms the JS module loaded, which is necessary but not
//! sufficient. We trigger it from `spawn::read_stdout` after the JS
//! ready event arrives, then react to `{type:"health-ok"}` /
//! `{type:"health-failed"}` envelopes the same way.
//!
//! `monitor_child_exit` owns the spawned `Child` for the rest of its
//! lifetime: it races a `kill_rx` oneshot (intentional shutdown) against
//! `child.wait()` (unexpected exit), kills + reaps in the first case
//! and transitions the supervisor to `Crashed` in the second.

use std::sync::{Arc, Mutex as StdMutex};
use std::time::Duration;

use tauri::AppHandle;
use tokio::process::{Child, Command};
use tokio::sync::{mpsc, oneshot};

use crate::events::emit_supervisor_status_changed;
use crate::types::{SupervisorStatus, SupervisorStatusChangedPayload};

// region: Constants

pub const HEALTH_CHECK_DELAY: Duration = Duration::from_millis(1500);

const TREE_KILL_CAP: Duration = Duration::from_millis(1500);

// endregion

// region: Public surface

/// Sleeps `HEALTH_CHECK_DELAY`, then pushes a `{type:"healthCheck"}`
/// JSON line onto the supervisor's stdin writer channel. The JS side
/// runs the actual query, with its own internal 5s timeout, and emits
/// `health-ok` / `health-failed` envelopes back over stdout — the
/// `read_stdout` task translates those to status transitions.
///
/// Soft-success on encoding / send failure: a closed channel means
/// the supervisor is already shutting down, in which case the timer
/// was racing teardown and there's nothing useful to report.
pub async fn schedule_health_check_trigger(stdin_tx: mpsc::UnboundedSender<String>,) {
    tokio::time::sleep(HEALTH_CHECK_DELAY).await;
    let line = match serde_json::to_string(&serde_json::json!({
        "type": "healthCheck",
    })) {
        Ok(s) => s,
        Err(e) => {
            eprintln!("[claude-supervisor] health check serialize failed: {e}");
            return;
        }
    };
    if stdin_tx.send(line).is_err() {
        eprintln!(
            "[claude-supervisor] health check trigger skipped — stdin writer closed (likely shutting down)"
        );
    }
}

/// Owns the freshly spawned `Child` and races two outcomes:
///
/// * `kill_rx` fires (intentional shutdown requested by `shutdown_inner`)
///   → tree-kill the OS process tree first so the `claude` grandchild
///   spawned by the SDK dies alongside Node; then `start_kill()` +
///   `wait()` reap any zombie remaining. Status emit is owned by the
///   caller, so this branch stays silent.
/// * `child.wait()` resolves first (unexpected exit — segfault, OOM,
///   manual `Task Manager` kill, parent process death cascading down)
///   → transition to `Crashed` and emit `supervisor-status-changed`,
///   but ONLY when current status is `Starting | Ready`. Other states
///   (`Idle`, `Crashed`, `Failed`) mean someone already moved past
///   this child (shutdown completed; `read_stdout` flipped to
///   `Crashed` on `health-failed`; spawn never reached `Starting`),
///   so we skip the emit to avoid double-firing or stale events.
///
/// The status-gate also covers a defensive ordering: `shutdown_inner`
/// sets status to `Idle` before sending on `kill_rx`, so even if the
/// `wait` branch were to win the select race (it shouldn't, but in
/// theory could on heavy load), the gate suppresses the spurious emit.
///
/// `pid` is the OS process id of the Node child, captured by the
/// caller before `child` was moved into this task. `None` (or `0`)
/// disables the tree-kill step — `start_kill` + `kill_on_drop` then
/// remain the only teardown path, which leaks the `claude` grandchild
/// on Windows.
pub async fn monitor_child_exit(
    mut child: Child,
    pid: Option<u32>,
    mut kill_rx: oneshot::Receiver<()>,
    app: AppHandle,
    status: Arc<StdMutex<SupervisorStatus>>,
) {
    tokio::select! {
        _ = &mut kill_rx => {
            kill_process_tree(pid, TREE_KILL_CAP).await;
            let _ = child.start_kill();
            let _ = child.wait().await;
        }
        result = child.wait() => {
            let exit_label = match result {
                Ok(s) => format!("{s}"),
                Err(e) => format!("wait error: {e}"),
            };
            eprintln!(
                "[claude-supervisor] node child exited unexpectedly ({exit_label})"
            );
            let should_emit = {
                let mut s = status
                    .lock()
                    .expect("supervisor status mutex poisoned");
                match *s {
                    SupervisorStatus::Starting | SupervisorStatus::Ready => {
                        *s = SupervisorStatus::Crashed;
                        true
                    }
                    SupervisorStatus::Idle
                    | SupervisorStatus::Crashed
                    | SupervisorStatus::Failed => false,
                }
            };
            if should_emit {
                let _ = emit_supervisor_status_changed(
                    &app,
                    SupervisorStatusChangedPayload {
                        status: SupervisorStatus::Crashed,
                        pid: None,
                    },
                );
            }
        }
    }
}

// endregion

// region: Tree kill

/// Kills the entire OS process tree rooted at `pid`. The
/// `@anthropic-ai/claude-agent-sdk` spawns the `claude` binary as a
/// grandchild of Tauri (Tauri → Node `sdk-entry.js` → `claude`), and
/// `child.start_kill()` only signals the immediate Node child. On
/// Windows the grandchild then survives Node's death and shows up as
/// an orphan in Task Manager
///
/// Behavior per platform:
///
/// * **Windows** — spawns `taskkill /T /F /PID <pid>`. `/T` walks the
///   descendant tree, `/F` forces termination (no WM_CLOSE round-trip).
///   The taskkill child is awaited with `cap` as an upper bound; on
///   timeout we move on and let `start_kill()` + `kill_on_drop` finish
///   what they can.
/// * **Non-Windows** — no-op for now. `kill_on_drop(true)` set in
///   `spawn::spawn_node_child` covers the immediate Node child via
///   SIGKILL on `Child` drop. A robust process-group teardown
///   (`process_group(0)` at spawn + `kill(-pgid, SIGTERM/SIGKILL)`
///   here) is parked until we have a Unix smoke environment to
///   validate it; tracked in tasks.
async fn kill_process_tree(pid: Option<u32>, cap: Duration) {
    let Some(pid) = pid else { return };
    if pid == 0 {
        return;
    }

    #[cfg(windows)]
    {
        let mut cmd = Command::new("taskkill");
        cmd.arg("/T")
            .arg("/F")
            .arg("/PID")
            .arg(pid.to_string())
            .stdin(std::process::Stdio::null())
            .stdout(std::process::Stdio::null())
            .stderr(std::process::Stdio::null())
            .kill_on_drop(true);
        match cmd.spawn() {
            Ok(mut tk) => match tokio::time::timeout(cap, tk.wait()).await {
                Ok(Ok(status)) => {
                    if !status.success() {
                        eprintln!(
                            "[claude-supervisor] taskkill /T /F /PID {pid} exited with {status}"
                        );
                    }
                }
                Ok(Err(e)) => {
                    eprintln!("[claude-supervisor] taskkill wait error: {e}");
                }
                Err(_) => {
                    eprintln!(
                        "[claude-supervisor] taskkill /T /F /PID {pid} did not finish within {cap:?}; continuing teardown"
                    );
                }
            },
            Err(e) => {
                eprintln!(
                    "[claude-supervisor] failed to spawn taskkill for PID {pid}: {e}"
                );
            }
        }
    }

    #[cfg(not(windows))]
    {
        let _ = (pid, cap);
    }
}

// endregion