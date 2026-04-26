//! Centralized event emission to the frontend.
//!
//! Event names are kebab-case strings matching the literals in
//! src/ipc/events.ts. Each helper takes an AppHandle (never a borrowed
//! Window — events broadcast app-wide).

use tauri::{AppHandle, Emitter};

use crate::types::{
    AskUserRequestedPayload, Message, MessageStreamChunkPayload, MessageStreamCompletePayload,
    NodeSdkStatusChangedPayload, PermissionRequestedPayload, UnityStatusChangedPayload,
};

pub const EVT_UNITY_STATUS_CHANGED: &str = "unity-status-changed";
pub const EVT_NODE_SDK_STATUS_CHANGED: &str = "node-sdk-status-changed";
pub const EVT_MESSAGE_RECEIVED: &str = "message-received";
pub const EVT_MESSAGE_STREAM_CHUNK: &str = "message-stream-chunk";
pub const EVT_MESSAGE_STREAM_COMPLETE: &str = "message-stream-complete";
pub const EVT_ASK_USER_REQUESTED: &str = "ask-user-requested";
pub const EVT_PERMISSION_REQUESTED: &str = "permission-requested";

pub fn emit_unity_status_changed(
    app: &AppHandle,
    payload: UnityStatusChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_UNITY_STATUS_CHANGED, payload)
}

#[allow(dead_code)]
pub fn emit_node_sdk_status_changed(
    app: &AppHandle,
    payload: NodeSdkStatusChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_NODE_SDK_STATUS_CHANGED, payload)
}

#[allow(dead_code)]
pub fn emit_message_received(app: &AppHandle, message: Message) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_RECEIVED, message)
}

#[allow(dead_code)]
pub fn emit_message_stream_chunk(
    app: &AppHandle,
    payload: MessageStreamChunkPayload,
) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_STREAM_CHUNK, payload)
}

#[allow(dead_code)]
pub fn emit_message_stream_complete(
    app: &AppHandle,
    payload: MessageStreamCompletePayload,
) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_STREAM_COMPLETE, payload)
}

#[allow(dead_code)]
pub fn emit_ask_user_requested(
    app: &AppHandle,
    payload: AskUserRequestedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_ASK_USER_REQUESTED, payload)
}

#[allow(dead_code)]
pub fn emit_permission_requested(
    app: &AppHandle,
    payload: PermissionRequestedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_PERMISSION_REQUESTED, payload)
}