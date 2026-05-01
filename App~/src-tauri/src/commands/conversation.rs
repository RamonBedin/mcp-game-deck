//! Conversation Tauri commands.
//!
//! Forward chat traffic between the React frontend and the Claude
//! Code supervisor (`claude_supervisor::ClaudeSupervisor`). Permission
//! mode stubs (`set_permission_mode` / `get_permission_mode`) are
//! filled in by tasks 4.2-4.3.
//!
//! History + clear commands were dropped in task 4.1: Claude Code's
//! own session storage is the source of truth (Decision #6 — wired up
//! in task 4.4) and `/clear` is the in-chat reset path.

use tauri::State;

use crate::claude_supervisor::ClaudeSupervisor;
use crate::types::{AppError, PermissionMode};

// region: Send

/// Forwards a user message to `sdk-entry.js` over the supervisor's
/// stdin channel. The assistant reply arrives asynchronously via the
/// `agent-message` Tauri event (dispatched by
/// `claude_supervisor::spawn::read_stdout` for every envelope the SDK
/// emits — `text-delta`, `tool-use`, `tool-result`,
/// `assistant-turn-complete`, `error`).
///
/// # Arguments
///
/// * `text` - User's message text.
/// * `attachment_paths` - Absolute paths the user attached alongside
///   the prompt. Always empty today; UI wiring lands in Group 5.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when the supervisor isn't running,
/// the stdin writer task is closed, or the JSON encoding fails.
#[tauri::command]
pub async fn send_message(
    text: String,
    attachment_paths: Vec<String>,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .send_input(&text, &attachment_paths)
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
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