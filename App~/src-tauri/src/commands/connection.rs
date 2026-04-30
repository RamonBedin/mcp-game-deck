//! Connection-status Tauri commands.
//!
//! Surface the live state of the Unity TCP client and the Node Agent SDK
//! supervisor to the React frontend, plus manual reconnect / restart hooks.

use tauri::{AppHandle, State};

use crate::claude_supervisor::ClaudeSupervisor;
use crate::types::{AppError, ConnectionStatus, SupervisorStatus};
use crate::unity_client::UnityClient;

// region: Status getters

/// Reads the current Unity TCP connection status from `UnityClient`.
///
/// Sync — microsecond cheap. Polled every 2s by `App.tsx`; the
/// `unity-status-changed` event provides the fast-path updates between polls.
///
/// # Arguments
///
/// * `client` - Tauri-managed `UnityClient` state.
///
/// # Returns
///
/// The latest `ConnectionStatus` observed by the run loop.
#[tauri::command]
pub fn get_unity_status(client: State<'_, UnityClient>) -> ConnectionStatus {
    client.current_status()
}

/// Reads the live state machine maintained by the Claude Code supervisor.
///
/// Sync — microsecond cheap. Polled every 2s by `App.tsx`; the
/// `supervisor-status-changed` event provides the fast-path updates
/// between polls.
///
/// # Arguments
///
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Returns
///
/// The latest `SupervisorStatus` (Idle / Starting / Ready / Crashed /
/// Failed).
#[tauri::command]
pub fn get_supervisor_status(supervisor: State<'_, ClaudeSupervisor>) -> SupervisorStatus {
    supervisor.current_status()
}

// endregion

// region: Manual triggers

/// Manual reconnect hook for the Unity client.
///
/// No-op today: the connection loop already retries on backoff, and a
/// manual nudge would require interrupting the current sleep. Out of scope
///
/// # Returns
///
/// `Ok(())` unconditionally.
///
/// # Errors
///
/// Reserved for future implementations.
#[tauri::command]
pub fn reconnect_unity() -> Result<(), AppError> {
    // The connection loop already retries on backoff; a manual nudge would
    // require interrupting the current sleep. Out of scope for 4.1; revisit
    // if there's a UX need.
    Ok(())
}

/// Restarts the Claude Code supervisor.
///
/// Skeleton today: returns `AppError::Internal` with the message
/// . Real spawn lands in
/// and replaces the body without changing the signature.
///
/// # Arguments
///
/// * `app` - Application handle forwarded to `spawn`.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal`
#[tauri::command]
pub async fn restart_supervisor(
    app: AppHandle,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .spawn(app)
        .await
        .map(|_pid| ())
        .map_err(|e| AppError::Internal(e.to_string()))
}

// endregion