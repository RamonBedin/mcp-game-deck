//! Conversation Tauri commands.
//!
//! Forward chat traffic between the React frontend and the Claude
//! Code supervisor (`claude_supervisor::ClaudeSupervisor`). Stubs for
//! `get_conversation_history` / `clear_conversation` /
//! `set_permission_mode` / `get_permission_mode`

use tauri::State;

use crate::claude_supervisor::ClaudeSupervisor;
use crate::types::{AppError, Message, PermissionMode};

// region: Send / history

/// Forwards a user message to `sdk-entry.js` over the supervisor's
/// stdin channel. The assistant reply arrives asynchronously via the
/// `message-received` Tauri event (dispatched by
/// `claude_supervisor::spawn::read_stdout` when the SDK emits an
/// `AssistantText` or `Error` AgentMessage).
///
/// # Arguments
///
/// * `text` - User's message text.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when the supervisor isn't running,
/// the stdin writer task is closed, or the JSON encoding fails.
#[tauri::command]
pub async fn send_message(
    text: String,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .send_input(&text)
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
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