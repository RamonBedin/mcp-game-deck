//! Read-only access to host process environment variables.
//!
//! Used by [`get_env_var`] so the React side can read `MCP_GAME_DECK_*`
//! variables that the Unity pin sets at process spawn — see
//! `Editor/Pin/PinLauncher.cs` for the producer side and
//! `src/components/UpdateBanner.tsx` for the consumer.

/// Reads a single environment variable from the Tauri host process.
///
/// # Arguments
///
/// * `name` - Environment variable name to read.
///
/// # Returns
///
/// `Some(value)` when the variable is set and contains valid UTF-8;
/// `None` when it is unset or contains invalid UTF-8. An explicitly empty
/// value is returned as `Some("")` — callers should treat empty strings
/// as absent at the application layer.
#[tauri::command]
pub fn get_env_var(name: String) -> Option<String> {
    std::env::var(name).ok()
}
