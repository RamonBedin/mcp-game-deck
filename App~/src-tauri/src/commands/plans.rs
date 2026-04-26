use crate::types::{AppError, Plan, PlanFrontmatter, PlanMeta};

#[tauri::command]
pub fn list_plans() -> Vec<PlanMeta> {
    Vec::new()
}

#[tauri::command]
pub fn read_plan(name: String) -> Result<Plan, AppError> {
    Ok(Plan {
        name,
        last_modified: 0,
        content: "# Stub plan\n\nReal plans CRUD lands in Feature 06.".to_string(),
        frontmatter: PlanFrontmatter::new(),
    })
}

#[tauri::command]
#[allow(unused_variables)]
pub fn write_plan(name: String, content: String) -> Result<(), AppError> {
    Ok(())
}

#[tauri::command]
#[allow(unused_variables)]
pub fn delete_plan(name: String) -> Result<(), AppError> {
    Ok(())
}