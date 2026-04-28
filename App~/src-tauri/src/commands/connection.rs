//! Connection-status Tauri commands.
//!
//! Surface the live state of the Unity TCP client and the Node Agent SDK
//! supervisor to the React frontend, plus manual reconnect / restart hooks.

use tauri::{AppHandle, State};

use crate::node_supervisor::NodeSupervisor;
use crate::types::{AppError, ConnectionStatus, NodeSdkStatus};
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

/// Reads the live state machine maintained by the Node SDK supervisor.
///
/// Sync — microsecond cheap. Polled every 2s by `App.tsx`; the
/// `node-sdk-status-changed` event provides the fast-path updates between polls.
///
/// # Arguments
///
/// * `supervisor` - Tauri-managed `NodeSupervisor` state.
///
/// # Returns
///
/// The latest `NodeSdkStatus` (Starting / Running / Crashed).
#[tauri::command]
pub fn get_node_sdk_status(supervisor: State<'_, NodeSupervisor>) -> NodeSdkStatus {
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

/// Restarts the Node SDK child.
///
/// Implemented as `supervisor.spawn(app)` — idempotent, kills any prior
/// child first.
///
/// # Arguments
///
/// * `app` - Application handle forwarded to `spawn` for event emission.
/// * `supervisor` - Tauri-managed `NodeSupervisor` state.
///
/// # Returns
///
/// `Ok(())` once the child has been respawned (the new PID is logged but
/// not returned to the caller).
///
/// # Errors
///
/// Returns `AppError::NodeSdkUnavailable` when the spawn fails (e.g. Node
/// not on PATH, stdio pipes missing).
#[tauri::command]
pub async fn restart_node_sdk(
    app: AppHandle,
    supervisor: State<'_, NodeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .spawn(app)
        .await
        .map(|_pid| ())
        .map_err(|e| AppError::NodeSdkUnavailable(e.to_string()))
}

// endregion