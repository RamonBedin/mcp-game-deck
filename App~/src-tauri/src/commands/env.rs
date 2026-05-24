//! Read-only access to host process environment variables.
//!
//! Used by [`get_env_var`] so the React side can read `MCP_GAME_DECK_*`
//! variables that the Unity pin sets at process spawn — see
//! `Editor/Pin/PinLauncher.cs` for the producer side and
//! `src/components/UpdateBanner.tsx` for the consumer.


const ALLOWED_PREFIXES: &[&str] = &["MCP_GAME_DECK_"];

/// Reads a single environment variable from the Tauri host process.
///
/// # Arguments
///
/// * `name` - Environment variable name to read.
///
/// # Returns
///
/// `Some(value)` when the name matches one of [`ALLOWED_PREFIXES`] AND
/// the variable is set with valid UTF-8; `None` otherwise. Names outside
/// the allowlist collapse silently to `None` (same shape as
/// "variable not set" — the React side already handles that case).
/// An explicitly empty value is returned as `Some("")` — callers should
/// treat empty strings as absent at the application layer.
#[tauri::command]
pub fn get_env_var(name: String) -> Option<String> {
    if !ALLOWED_PREFIXES.iter().any(|p| name.starts_with(p)) {
        return None;
    }
    std::env::var(name).ok()
}

/// Reads the OS user's login name from `USERNAME` (Windows) or `USER`
/// (Unix). Used by the React side to derive avatar initials for user
/// messages instead of the hardcoded `"RB"` placeholder.
///
/// # Returns
///
/// `Some(name)` when the platform-appropriate env var is set and
/// non-empty; `None` otherwise. The frontend treats `None` and `""` the
/// same — both fall back to a generic `"??"` avatar.
#[tauri::command]
pub fn get_os_username() -> Option<String> {
    #[cfg(target_os = "windows")]
    let var = "USERNAME";
    #[cfg(not(target_os = "windows"))]
    let var = "USER";

    std::env::var(var).ok().filter(|s| !s.is_empty())
}
