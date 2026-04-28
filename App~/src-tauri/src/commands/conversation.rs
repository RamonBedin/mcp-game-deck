//! Conversation Tauri commands.
//!
//! Forward chat traffic between the React frontend and the Node Agent SDK.
//! (conversation persistence) — the typed surface is in place so the
//! frontend can wire its calls today.

use serde_json::json;
use tauri::State;

use crate::node_supervisor::NodeSupervisor;
use crate::types::{AppError, Message, MessageId, PermissionMode};

// region: Send / history

/// Forwards a chat message to the Node SDK as the `conversation/send`
/// JSON-RPC method.
///
/// The actual assistant reply arrives asynchronously via a `message/received`
/// notification (handled in `jsonrpc.rs` and re-emitted as the
/// `message-received` Tauri event).
///
/// # Arguments
///
/// * `text` - User's message text.
/// * `agent` - Optional sub-agent name to route the message through.
/// * `supervisor` - Tauri-managed `NodeSupervisor` state.
///
/// # Returns
///
/// The message id assigned by the Node SDK, or `"ack"` if the stub hasn't
/// implemented the full echo response shape yet (task 5.2 wires that).
///
/// # Errors
///
/// Returns `AppError::NodeSdkUnavailable` when the JSON-RPC request fails
/// (child dead, timeout, serde error, or a JSON-RPC error reply).
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

/// Stub: returns the recent conversation history for a session.
///
/// Always returns an empty list today. Real implementation lands in
/// Feature 05 (conversation persistence).
///
/// # Arguments
///
/// * `session_id` - Session whose history to retrieve (currently ignored).
/// * `limit` - Maximum number of messages to return (currently ignored).
///
/// # Returns
///
/// An empty `Vec<Message>`.
#[tauri::command]
#[allow(unused_variables)]
pub fn get_conversation_history(session_id: String, limit: usize) -> Vec<Message> {
    Vec::new()
}

/// Stub: clears the message history for a session.
///
/// No-op today. Real implementation lands in Feature 05.
///
/// # Arguments
///
/// * `session_id` - Session to clear (currently ignored).
///
/// # Returns
///
/// `Ok(())` unconditionally.
///
/// # Errors
///
/// Reserved for future implementations.
#[tauri::command]
#[allow(unused_variables)]
pub fn clear_conversation(session_id: String) -> Result<(), AppError> {
    Ok(())
}

// endregion

// region: Permission mode

/// Stub: persists the agent's permission mode.
///
/// No-op today. Real implementation lands alongside Feature 02's
/// permission flow.
///
/// # Arguments
///
/// * `mode` - Desired permission policy (currently ignored).
///
/// # Returns
///
/// `Ok(())` unconditionally.
///
/// # Errors
///
/// Reserved for future implementations.
#[tauri::command]
#[allow(unused_variables)]
pub fn set_permission_mode(mode: PermissionMode) -> Result<(), AppError> {
    Ok(())
}

/// Stub: reads the agent's permission mode.
///
/// Always returns `PermissionMode::Ask` today.
///
/// # Returns
///
/// `PermissionMode::Ask` (the safe default).
#[tauri::command]
pub fn get_permission_mode() -> PermissionMode {
    PermissionMode::Ask
}

// endregion