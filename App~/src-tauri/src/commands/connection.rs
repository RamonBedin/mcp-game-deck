use crate::types::{AppError, ConnectionStatus};

#[tauri::command]
pub fn get_unity_status() -> ConnectionStatus {
    ConnectionStatus::Connected
}

#[tauri::command]
pub fn get_node_sdk_status() -> ConnectionStatus {
    ConnectionStatus::Connected
}

#[tauri::command]
pub fn reconnect_unity() -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
pub fn restart_node_sdk() -> Result<(), AppError> {
    Ok(())
}