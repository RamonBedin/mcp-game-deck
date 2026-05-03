//! Centralized event emission to the frontend.
//!
//! Event names are kebab-case strings matching the literals in
//! `src/ipc/events.ts`. Each helper takes an `AppHandle` (never a borrowed
//! `Window` — events broadcast app-wide).

use tauri::{AppHandle, Emitter};

use crate::types::{
    AgentMessagePayload, AskUserRequestedPayload, Message, MessageStreamChunkPayload,
    MessageStreamCompletePayload, NodeSdkStatusChangedPayload, PermissionModeChangedPayload,
    PermissionRequestedPayload, RouteRequestedPayload, SdkInstallFailedPayload,
    SdkInstallProgressPayload, SupervisorStatusChangedPayload, UnityStatusChangedPayload,
};

// region: Event names

/// Event name for `UnityStatusChangedPayload`.
pub const EVT_UNITY_STATUS_CHANGED: &str = "unity-status-changed";

/// kept while `node_supervisor/jsonrpc.rs` still emits it internally.
/// Removed alongside `node_supervisor/`
pub const EVT_NODE_SDK_STATUS_CHANGED: &str = "node-sdk-status-changed";

/// Event name for `SupervisorStatusChangedPayload` — replaces
/// `node-sdk-status-changed` from F01.
pub const EVT_SUPERVISOR_STATUS_CHANGED: &str = "supervisor-status-changed";

/// Event name for `Message` delivery (full, non-streamed messages).
pub const EVT_MESSAGE_RECEIVED: &str = "message-received";

/// Event name for `MessageStreamChunkPayload` (incremental streaming).
pub const EVT_MESSAGE_STREAM_CHUNK: &str = "message-stream-chunk";

/// Event name for `MessageStreamCompletePayload` (end-of-stream marker).
pub const EVT_MESSAGE_STREAM_COMPLETE: &str = "message-stream-complete";

/// Event name for `AskUserRequestedPayload`.
pub const EVT_ASK_USER_REQUESTED: &str = "ask-user-requested";

/// Event name for `PermissionRequestedPayload`.
pub const EVT_PERMISSION_REQUESTED: &str = "permission-requested";

/// Event name for `RouteRequestedPayload`.
pub const EVT_ROUTE_REQUESTED: &str = "route-requested";

/// Event name for `SdkInstallProgressPayload`.
pub const EVT_SDK_INSTALL_PROGRESS: &str = "sdk-install-progress";

/// Event name for `sdk-install-completed` (no payload — emit `()`).
pub const EVT_SDK_INSTALL_COMPLETED: &str = "sdk-install-completed";

/// Event name for `SdkInstallFailedPayload`.
pub const EVT_SDK_INSTALL_FAILED: &str = "sdk-install-failed";

/// Event name for `AgentMessagePayload` — every line `sdk-entry.js`
pub const EVT_AGENT_MESSAGE: &str = "agent-message";

/// Event name for `PermissionModeChangedPayload` — emitted when the
/// supervisor confirms the JS side has applied a new permission mode.
pub const EVT_PERMISSION_MODE_CHANGED: &str = "permission-mode-changed";

// endregion

// region: Emitters

/// Broadcasts a Unity connection state change to the frontend.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - New status plus an optional reason string.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails (e.g. window destroyed).
pub fn emit_unity_status_changed(
    app: &AppHandle,
    payload: UnityStatusChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_UNITY_STATUS_CHANGED, payload)
}

/// Broadcasts a Node.js Agent SDK lifecycle change to the frontend.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - New SDK status plus the OS process id when known.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_node_sdk_status_changed(
    app: &AppHandle,
    payload: NodeSdkStatusChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_NODE_SDK_STATUS_CHANGED, payload)
}

/// Broadcasts a Claude Code supervisor lifecycle change to the frontend.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - New supervisor status plus the OS process id when known.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_supervisor_status_changed(
    app: &AppHandle,
    payload: SupervisorStatusChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_SUPERVISOR_STATUS_CHANGED, payload)
}

/// Delivers a complete (non-streamed) message to the frontend.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `message` - The fully formed message, including id, role and content.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_message_received(app: &AppHandle, message: Message) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_RECEIVED, message)
}

/// Pushes an incremental chunk for an in-flight streamed message.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Identifier of the message being streamed and the text fragment to append.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_message_stream_chunk(
    app: &AppHandle,
    payload: MessageStreamChunkPayload,
) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_STREAM_CHUNK, payload)
}

/// Signals that streaming has finished for a given message.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Identifier of the message that finished streaming.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_message_stream_complete(
    app: &AppHandle,
    payload: MessageStreamCompletePayload,
) -> tauri::Result<()> {
    app.emit(EVT_MESSAGE_STREAM_COMPLETE, payload)
}

/// Asks the frontend to prompt the user for input on behalf of the agent.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Question id, prompt text, optional options and answer shape.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_ask_user_requested(
    app: &AppHandle,
    payload: AskUserRequestedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_ASK_USER_REQUESTED, payload)
}

/// Asks the frontend to confirm a sensitive tool invocation with the user.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Request id plus the tool name and the parameters the agent intends to pass.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
#[allow(dead_code)]
pub fn emit_permission_requested(
    app: &AppHandle,
    payload: PermissionRequestedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_PERMISSION_REQUESTED, payload)
}

/// Asks the running window to navigate to the route carried by a re-launch.
///
/// Emitted from the single-instance callback when a second invocation arrives
/// with a `--route=/path` CLI argument.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - The route the running window should navigate to.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_route_requested(
    app: &AppHandle,
    payload: RouteRequestedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_ROUTE_REQUESTED, payload)
}

/// Streams a single stdout line from the running `npm install`.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Indeterminate percent + the latest stdout line.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_sdk_install_progress(
    app: &AppHandle,
    payload: SdkInstallProgressPayload,
) -> tauri::Result<()> {
    app.emit(EVT_SDK_INSTALL_PROGRESS, payload)
}

/// Signals successful `npm install` completion. No payload.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_sdk_install_completed(app: &AppHandle) -> tauri::Result<()> {
    app.emit(EVT_SDK_INSTALL_COMPLETED, ())
}

/// Signals a failed `npm install`, with the trailing stderr lines
/// and exit code.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - Failure message (last few stderr lines) plus
///   optional exit code.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_sdk_install_failed(
    app: &AppHandle,
    payload: SdkInstallFailedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_SDK_INSTALL_FAILED, payload)
}

/// Re-emits a typed `AgentMessage` to the React side for DevTools
/// debugging. The same message also drives status transitions and
/// `message-received` emits when applicable — see
/// `claude_supervisor::spawn::read_stdout`.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - The wrapped `AgentMessage` envelope.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_agent_message(
    app: &AppHandle,
    payload: AgentMessagePayload,
) -> tauri::Result<()> {
    app.emit(EVT_AGENT_MESSAGE, payload)
}

/// Broadcasts a permission-mode change to the frontend. Driven by
/// `sdk-entry.js`'s echo after applying a `setPermissionMode` control
/// message — see `claude_supervisor::spawn::read_stdout` for the
/// `AgentMessage::PermissionModeChanged` translation.
///
/// # Arguments
///
/// * `app` - Tauri application handle used to emit the event.
/// * `payload` - The new permission mode.
///
/// # Errors
///
/// Returns `tauri::Error` when the underlying emitter fails.
pub fn emit_permission_mode_changed(
    app: &AppHandle,
    payload: PermissionModeChangedPayload,
) -> tauri::Result<()> {
    app.emit(EVT_PERMISSION_MODE_CHANGED, payload)
}

// endregion