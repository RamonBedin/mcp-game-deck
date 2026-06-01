//! Settings Tauri commands.
//!
//! Theme persistence still stubbed; `unity_project_path` resolves
//! live from the env-then-saved-settings fallback so the HUD can
//! show which Unity project is bound.

use crate::types::{AppError, AppSettings, AppSettingsPatch, Theme};

// region: Read / write

/// Reads the current application settings.
///
/// Theme is hard-coded to `Dark` until the persistence layer lands.
/// `unity_project_path` resolves via
/// [`try_resolve_project_root`](crate::project_root::try_resolve_project_root)
/// — env var (`UNITY_PROJECT_PATH`) first, then any saved override —
/// so the frontend HUD reflects the live project binding without
/// duplicating the resolver.
///
/// # Returns
///
/// An `AppSettings` value with `unity_project_path` resolved from
/// the live environment, or `None` when no Unity project is bound.
#[tauri::command]
pub fn get_settings() -> AppSettings {
    AppSettings {
        theme: Theme::Dark,
        unity_project_path: crate::project_root::try_resolve_project_root()
            .map(|p| p.to_string_lossy().into_owned()),
    }
}

/// Stub: applies a partial settings update.
///
/// No-op today. Real implementation lands alongside the settings storage.
///
/// # Arguments
///
/// * `patch` - Fields to update; `None` fields are left unchanged (currently ignored).
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
pub fn update_settings(patch: AppSettingsPatch) -> Result<(), AppError> {
    Ok(())
}

// endregion