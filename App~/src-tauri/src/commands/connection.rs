use tauri::{AppHandle, State};

use crate::node_supervisor::NodeSupervisor;
use crate::types::{AppError, ConnectionStatus, NodeSdkStatus};
use crate::unity_client::UnityClient;

/// Reads the current Unity TCP connection status from `UnityClient`. Sync.
/// Polled every 2s by App.tsx; events from `unity-status-changed` provide
/// the fast-path updates.
#[tauri::command]
pub fn get_unity_status(client: State<'_, UnityClient>) -> ConnectionStatus {
    client.current_status()
}

/// Reads the live state machine maintained by the supervisor. Sync —
/// microsecond cheap. Polled every 2s by App.tsx; events from
/// `node-sdk-status-changed` provide the fast-path updates.
#[tauri::command]
pub fn get_node_sdk_status(supervisor: State<'_, NodeSupervisor>) -> NodeSdkStatus {
    supervisor.current_status()
}

#[tauri::command]
pub fn reconnect_unity() -> Result<(), AppError> {
    // The connection loop already retries on backoff; a manual nudge would
    // require interrupting the current sleep. Out of scope for 4.1; revisit
    // if there's a UX need.
    Ok(())
}

/// Restarts the Node SDK child. Implemented as `supervisor.spawn(app)` —
/// idempotent, kills any prior child first.
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