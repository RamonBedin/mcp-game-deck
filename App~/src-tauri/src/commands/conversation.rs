use serde_json::json;
use tauri::State;

use crate::node_supervisor::NodeSupervisor;
use crate::types::{AppError, Message, MessageId, PermissionMode};

/// Forwards a chat message to the Node SDK as the `conversation/send`
/// JSON-RPC method. The actual assistant reply arrives asynchronously via a
/// `message/received` notification (handled in jsonrpc.rs and emitted as the
/// `message-received` Tauri event).
///
/// Returns the message id assigned by the Node SDK (or `"ack"` if the stub
/// hasn't implemented the full echo response shape yet — task 5.2 wires that).
#[tauri::command]
pub async fn send_message(
    text: String,
    agent: Option<String>,
    supervisor: State<'_, NodeSupervisor>,
) -> Result<MessageId, AppError> {
    let params = json!({
        "text": text,
        "agent": agent,
        "session_id": null,
    });
    let result = supervisor
        .request("conversation/send", Some(params))
        .await
        .map_err(|e| AppError::NodeSdkUnavailable(e.to_string()))?;

    let id = result
        .get("message_id")
        .and_then(|v| v.as_str())
        .unwrap_or("ack")
        .to_string();
    Ok(id)
}

#[tauri::command]
#[allow(unused_variables)]
pub fn get_conversation_history(session_id: String, limit: usize) -> Vec<Message> {
    Vec::new()
}

#[tauri::command]
#[allow(unused_variables)]
pub fn clear_conversation(session_id: String) -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
#[allow(unused_variables)]
pub fn set_permission_mode(mode: PermissionMode) -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
pub fn get_permission_mode() -> PermissionMode {
    PermissionMode::Ask
}