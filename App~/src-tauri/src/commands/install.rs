//! Install-detection Tauri commands.
//!
//! Surface the local environment's readiness to run Claude Code to the
//! React frontend. The first-run UX consumes
//! `check_claude_install_status` to pick which onboarding step to show;
//! tasks 1.3 and 4.4 will add more install-related commands here.

use tauri::AppHandle;

use crate::claude_supervisor::{install_check, sdk_install};
use crate::events::emit_sdk_install_completed;
use crate::types::{AppError, ClaudeInstallStatus};

// region: Status getter

/// Runs the four detection probes (claude binary, version, auth, SDK
/// package) and returns the bundled status.
///
/// Internally spawns up to three subprocesses in parallel; the slow path
/// is the auth probe (~500ms-1s on a warm machine, capped at the 5s
/// internal timeout). Polled by `FirstRunPanel` every 5s while mounted —
/// safe to call repeatedly because each call runs fresh (no cache today)
///
/// # Returns
///
/// A `ClaudeInstallStatus` with all four fields populated. Detection
/// failures (e.g. `where.exe` missing, `claude` hangs) yield `false` or
/// `None` for the affected field rather than propagating an error — the
/// React panel cannot meaningfully distinguish "missing" from "probe
/// broken" anyway.
#[tauri::command]
pub async fn check_claude_install_status() -> ClaudeInstallStatus {
    install_check::check_install_status().await
}

// endregion

// region: SDK install

/// Kicks off `npm install @anthropic-ai/claude-agent-sdk` into
/// `App~/runtime/` if the SDK is missing and no install is already
/// running. Idempotent — re-invocations during install are no-ops;
/// re-invocations when the SDK is already present emit a fresh
/// `sdk-install-completed` and return.
///
/// Returns immediately; progress streams via `sdk-install-progress`,
/// completion via `sdk-install-completed`, failure via
/// `sdk-install-failed`.
///
/// # Errors
///
/// `Result` reserved for future synchronous failures; today the body
/// always succeeds in starting the background task.
#[tauri::command]
pub async fn start_sdk_install(app: AppHandle) -> Result<(), AppError> {
    if sdk_install::is_sdk_installed().await {
        let _ = emit_sdk_install_completed(&app);
        return Ok(());
    }

    if !sdk_install::try_claim_slot() {
        // Already running — silent no-op; the in-flight install will
        // emit its own completed/failed event when done.
        return Ok(());
    }

    tauri::async_runtime::spawn(async move {
        sdk_install::install_sdk_async(app).await;
        sdk_install::release_slot();
    });

    Ok(())
}

// endregion