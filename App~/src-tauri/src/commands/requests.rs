//! `respond_to_request` Tauri command.
//!
//! Routes the user's response to a permission card or a question card
//! down through the supervisor's stdin so `sdk-entry.js`'s
//! `respond-to-request` branch (task 1.3) resolves the awaited
//! `canUseTool` promise. The conversation then continues from where
//! it stalled.
//!
//! Wire format mirrors the JSON line that lands on Node stdin:
//!
//! ```json
//! { "type": "respond-to-request",
//!   "requestId": "<toolUseID>",
//!   "decision": { "kind": "permission", "outcome": "allow" } }
//! ```
//!
//! `decision` is a `DecisionPayload` (`kind: "permission" | "question"`).
//! Unknown `requestId`s are tolerated by `sdk-entry.js`'s defensive
//! check — debug-logged and dropped — so a stale Tauri click after a
//! supervisor restart doesn't bubble an error to React.

use serde_json::json;
use tauri::State;

use crate::claude_supervisor::ClaudeSupervisor;
use crate::types::{AppError, DecisionPayload};

/// Forwards the user's permission / question card response to the
/// supervisor. Builds a `respond-to-request` JSON line and writes it
/// to `sdk-entry.js`'s stdin via the shared `write_stdin_line` helper.
///
/// # Arguments
///
/// * `request_id` - The SDK's `toolUseID` originally surfaced as
///   `permission-requested.requestId` / `ask-user-requested.requestId`.
/// * `decision` - Tagged payload — `Permission { outcome }` or
///   `Question { answer }`.
/// * `supervisor` - Tauri-managed `ClaudeSupervisor` state.
///
/// # Errors
///
/// Returns `AppError::Internal` when the supervisor isn't running,
/// the stdin writer task is closed, or the JSON encoding fails.
#[tauri::command]
pub async fn respond_to_request(
    request_id: String,
    decision: DecisionPayload,
    supervisor: State<'_, ClaudeSupervisor>,
) -> Result<(), AppError> {
    let payload = json!({
        "type": "respond-to-request",
        "requestId": request_id,
        "decision": decision,
    });
    supervisor
        .write_stdin_line(&payload.to_string())
        .await
        .map_err(|e| AppError::Internal(e.to_string()))
}