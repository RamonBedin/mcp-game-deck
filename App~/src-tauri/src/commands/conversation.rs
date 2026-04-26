use std::time::{SystemTime, UNIX_EPOCH};

use crate::types::{AppError, Message, MessageId, PermissionMode};

#[tauri::command]
#[allow(unused_variables)]
pub fn send_message(text: String, agent: Option<String>) -> Result<MessageId, AppError> {
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis())
        .unwrap_or(0);
    Ok(format!("stub-{now}"))
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