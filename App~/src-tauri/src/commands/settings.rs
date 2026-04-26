use crate::types::{AppError, AppSettings, AppSettingsPatch, Theme};

#[tauri::command]
pub fn get_settings() -> AppSettings {
    AppSettings {
        theme: Theme::Dark,
        unity_project_path: None,
    }
}

#[tauri::command]
#[allow(unused_variables)]
pub fn update_settings(patch: AppSettingsPatch) -> Result<(), AppError> {
    Ok(())
}