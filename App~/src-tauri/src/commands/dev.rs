//! Dev-only Tauri commands. The function bodies are gated by
//! `#[cfg(debug_assertions)]`; in release builds the command exists but
//! returns an error. The frontend hides these behind `import.meta.env.DEV`,
//! so production callers never reach them.
//!
//! Function bodies cannot be cfg-gated out of `tauri::generate_handler!`
//! cleanly without duplicating the entire handler list, so we keep the
//! signature stable and gate the body instead.

use tauri::AppHandle;

#[tauri::command]
#[allow(unused_variables)]
pub fn dev_emit_test_event(app: AppHandle) -> Result<(), String> {
    #[cfg(debug_assertions)]
    {
        use crate::events::emit_unity_status_changed;
        use crate::types::{ConnectionStatus, UnityStatusChangedPayload};
        emit_unity_status_changed(
            &app,
            UnityStatusChangedPayload {
                status: ConnectionStatus::Disconnected,
                reason: Some("dev test trigger".to_string()),
            },
        )
        .map_err(|e| e.to_string())
    }
    #[cfg(not(debug_assertions))]
    {
        Err("dev_emit_test_event is disabled in release builds".to_string())
    }
}