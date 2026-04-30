//! Install-detection Tauri commands.
//!
//! Surface the local environment's readiness to run Claude Code to the
//! React frontend. The first-run UX consumes
//! `check_claude_install_status` to pick which onboarding step to show;
//! tasks 1.3 and 4.4 will add more install-related commands here.

use crate::claude_supervisor::install_check;
use crate::types::ClaudeInstallStatus;

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
