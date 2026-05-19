//! Shared resolver for the active Unity project root.
//!
//! Plans, file index, rules — every feature that scopes work to "the
//! currently-pinned Unity project" reads this single helper instead
//! of duplicating the env-then-settings fallback. Originally lived in
//! `commands::plans`, hoisted to crate root once `commands::files`
//! became a second consumer and is set to
//! become the third.
//!
//! Resolution order:
//!
//! 1. `UNITY_PROJECT_PATH` environment variable
//! 2. Persisted `AppSettings.unity_project_path` (set via the
//!    in-app settings UI as a fallback when the app was launched
//!    outside the Editor pin).
//! 3. `None` — no project pinned. Callers should treat this as
//!    "nothing to list" rather than as an error.

use std::path::PathBuf;

use crate::commands::settings::get_settings;

/// Returns the absolute path to the active Unity project, or `None`
/// when no source provides one. See module docblock for the
/// resolution order.
pub fn try_resolve_project_root() -> Option<PathBuf> {
    if let Some(path) = std::env::var("UNITY_PROJECT_PATH")
        .ok()
        .filter(|s| !s.is_empty())
    {
        return Some(PathBuf::from(path));
    }

    get_settings().unity_project_path.map(PathBuf::from)
}