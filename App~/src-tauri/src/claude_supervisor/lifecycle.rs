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
use tokio::process::Child;
use tokio::sync::{mpsc, oneshot};

use crate::events::emit_supervisor_status_changed;
use crate::types::{SupervisorStatus, SupervisorStatusChangedPayload};

// region: Constants

pub const HEALTH_CHECK_DELAY: Duration = Duration::from_millis(1500);

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
///   → `start_kill()` + `wait()` to reap; status emit is owned by the
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
pub async fn monitor_child_exit(
    mut child: Child,
    mut kill_rx: oneshot::Receiver<()>,
    app: AppHandle,
    status: Arc<StdMutex<SupervisorStatus>>,
) {
    tokio::select! {
        _ = &mut kill_rx => {
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