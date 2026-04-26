use crate::types::{AppError, Rule, RuleMeta};

#[tauri::command]
pub fn list_rules() -> Vec<RuleMeta> {
    Vec::new()
}

#[tauri::command]
pub fn read_rule(name: String) -> Result<Rule, AppError> {
    Ok(Rule {
        name,
        enabled: false,
        content: "# Stub rule\n\nReal rules UI lands in Feature 08.".to_string(),
    })
}

#[tauri::command]
#[allow(unused_variables)]
pub fn write_rule(name: String, content: String) -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
#[allow(unused_variables)]
pub fn delete_rule(name: String) -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
#[allow(unused_variables)]
pub fn toggle_rule(name: String, enabled: bool) -> Result<(), AppError> {
    Ok(())
}