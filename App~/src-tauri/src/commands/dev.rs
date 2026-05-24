//! Dev / diagnostic Tauri commands.
//!
//! The frontend hides these behind `import.meta.env.DEV` so production
//! users never reach them, though the commands themselves are always
//! registered (cfg-gating individual commands out of `tauri::generate_handler!`
//! would force duplicating the entire handler list).

use serde_json::{Map, Value};
use tauri::{AppHandle, State};

use crate::unity_client::UnityClient;

// region: Event triggers

/// Emits a one-shot `unity-status-changed` event with `disconnected`.
///
/// reverts the status within ~2s.
///
/// # Arguments
///
/// * `app` - Application handle used to emit the event.
///
/// # Returns
///
/// `Ok(())` when the event is emitted in debug builds.
///
/// # Errors
///
/// Returns the underlying emitter error stringified, or a "disabled in
/// release builds" message when invoked from a non-debug build.
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

// endregion

// region: Probes

/// Forwards a `tools/call` to Unity's MCP server.
///
/// Generic over tool name + arguments — used by the verification button in
/// eventually invoke.
///
/// # Arguments
///
/// * `name` - MCP tool name to invoke.
/// * `arguments` - Optional JSON arguments object; defaults to `{}` when omitted.
/// * `client` - Tauri-managed `UnityClient` state.
///
/// # Returns
///
/// The unwrapped JSON-RPC `result` value on success.
///
/// # Errors
///
/// Returns the underlying `ToolCallError` stringified (auth missing,
/// transport failure, non-200, JSON parse, RPC error, malformed response).
#[tauri::command]
#[allow(unused_variables)]
pub async fn dev_call_unity_tool(
    name: String,
    arguments: Option<Value>,
    client: State<'_, UnityClient>,
) -> Result<Value, String> {
    #[cfg(debug_assertions)]
    {
        let args = arguments.unwrap_or_else(|| Value::Object(Map::new()));
        client
            .call_tool(&name, args)
            .await
            .map_err(|e| e.to_string())
    }
    #[cfg(not(debug_assertions))]
    {
        Err("dev_call_unity_tool is disabled in release builds".to_string())
    }
}

// endregion