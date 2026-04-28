//! Settings Tauri commands.
//!
//! Stubs awaiting persistent settings storage. Today returns hard-coded
//! defaults; the typed surface is in place so the frontend can wire its
//! calls without changing once the storage backing lands.

use crate::types::{AppError, AppSettings, AppSettingsPatch, Theme};

// region: Read / write

/// Stub: reads the persisted application settings.
///
/// Returns a hard-coded default today (`Theme::Dark`, no Unity project pin).
///
/// # Returns
///
/// An `AppSettings` value populated with default values.
#[tauri::command]
pub fn get_settings() -> AppSettings {
    AppSettings {
        theme: Theme::Dark,
        unity_project_path: None,
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