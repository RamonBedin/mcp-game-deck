//! Conversation Tauri commands.
//!
//! Forward chat traffic between the React frontend and the Claude
//! Code supervisor (`claude_supervisor::ClaudeSupervisor`):
//! `send_message`, `set_permission_mode`, `set_model`,
//! `cancel_current_turn`.
//!
//! History + clear commands were dropped in task 4.1: Claude Code's
//! own session storage is the source of truth (Decision #6 — wired up
//! in task 4.4) and `/clear` is the in-chat reset path.

use serde_json::json;
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

/// Updates the supervisor's permission mode and pushes a control
/// message to `sdk-entry.js`'s stdin so the next `query()` round-trip
/// applies it. Tolerates a non-running supervisor — the mode is
/// stored and re-pushed on the next `spawn`.
///
/// # Arguments
///
/// * `mode` - New permission policy.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when the stdin writer task is closed
/// or the JSON encoding fails.
#[tauri::command]
pub async fn set_permission_mode(
    mode: PermissionMode,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .set_permission_mode(mode)
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}

// endregion

// region: Model

/// Updates the supervisor's model selection and pushes a control
/// message to `sdk-entry.js`'s stdin so the next `query()` round-trip
/// uses it. `None` resets to the CLI default. Tolerates a non-running
/// supervisor — the choice is stored and re-pushed on the next `spawn`.
///
/// # Arguments
///
/// * `model` - SDK model id (e.g. `"claude-sonnet-4-6"`) or `None`.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when the stdin writer task is closed
/// or the JSON encoding fails.
#[tauri::command]
pub async fn set_model(
    model: Option<String>,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    supervisor
        .set_model(model)
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}

// endregion

// region: Cancel current turn (B.02)

/// Sends an interrupt to the in-flight `query()` round-trip inside
/// `sdk-entry.js`. The JS side calls `.return()` on the live query
/// AsyncGenerator, breaking its `for await` loop; an
/// `assistant-turn-complete` envelope is emitted so the React
/// WorkingStrip hides immediately without waiting for the SDK to
/// settle.
///
/// Powers the Cancel button in the v2.0 chat WorkingStrip (B.02 ask).
/// A no-op when no turn is in flight — the JS side debug-logs and
/// drops the message, so the Promise resolves clean.
///
/// # Errors
///
/// `AppError::Internal` when the supervisor isn't running or the
/// stdin writer task is closed.
#[tauri::command]
pub async fn cancel_current_turn(
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    let payload = json!({ "type": "cancel-current-turn" });
    supervisor
        .write_stdin_line(&payload.to_string())
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}

// endregion