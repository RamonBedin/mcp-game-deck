//! Dev / diagnostic Tauri commands. The frontend hides these behind
//! `import.meta.env.DEV` so production users never reach them, though the
//! commands themselves are always registered (cfg-gating individual commands
//! out of `tauri::generate_handler!` would force duplicating the entire
//! handler list).

use serde_json::{Map, Value};
use tauri::{AppHandle, State};

use crate::node_supervisor::NodeSupervisor;
use crate::unity_client::UnityClient;

/// Emits a one-shot `unity-status-changed` event with `disconnected`. Used by
/// task 2.6's verification flow. The polling loop in App.tsx reverts the
/// status within ~2s.
#[tauri::command]
#[allow(unused_variables)]
pub fn dev_emit_test_event(app: AppHandle) -> Result<(), String> {
    #[cfg(debug_assertions)]
    {
        use crate::events::emit_unity_status_changed;
        use crate::types::{ConnectionStatus, UnityStatusChangedPayload};
        emit_unity_status_changed(
            &app,
            UnityStatusChangedPayload {
                status: ConnectionStatus::Disconnected,
                reason: Some("dev test trigger".to_string()),
            },
        )
        .map_err(|e| e.to_string())
    }
    #[cfg(not(debug_assertions))]
    {
        Err("dev_emit_test_event is disabled in release builds".to_string())
    }
}

/// Pings the Node SDK child via JSON-RPC. Returns the `pong` boolean.
/// Round-trip latency on localhost is typically <50ms.
#[tauri::command]
pub async fn node_ping(supervisor: State<'_, NodeSupervisor>) -> Result<bool, String> {
    supervisor.ping().await.map_err(|e| e.to_string())
}

/// Forwards a `tools/call` to Unity's MCP server. Generic over tool name +
/// arguments — used by the verification button in SettingsRoute and as the
/// transport that Feature 02's orchestrator will eventually invoke.
#[tauri::command]
pub async fn dev_call_unity_tool(
    name: String,
    arguments: Option<Value>,
    client: State<'_, UnityClient>,
) -> Result<Value, String> {
    let args = arguments.unwrap_or_else(|| Value::Object(Map::new()));
    client
        .call_tool(&name, args)
        .await
        .map_err(|e| e.to_string())
}