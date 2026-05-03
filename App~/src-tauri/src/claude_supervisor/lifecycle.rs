//! Supervisor lifecycle helpers — startup health check today; future
//! shutdown / restart hooks will land here too.
//!
//! The health check verifies that the spawned `claude` subprocess can
//! actually answer a query — `sdk-entry.js`'s `{type:"ready"}` only
//! confirms the JS module loaded, which is necessary but not
//! sufficient. We trigger it from `spawn::read_stdout` after the JS
//! ready event arrives, then react to `{type:"health-ok"}` /
//! `{type:"health-failed"}` envelopes the same way.

use std::time::Duration;

use tokio::sync::mpsc;

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

// endregion