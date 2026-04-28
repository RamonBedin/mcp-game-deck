//! Plans Tauri commands.
//!
//! so the frontend can wire its calls today; implementations land later.

use crate::types::{AppError, Plan, PlanFrontmatter, PlanMeta};

// region: Listing

/// Stub: lists known plan files.
///
/// Always returns an empty list today. Real implementation lands in Feature 06.
///
/// # Returns
///
/// An empty `Vec<PlanMeta>`.
#[tauri::command]
pub fn list_plans() -> Vec<PlanMeta> {
    Vec::new()
}

// endregion

// region: Read / write

/// Stub: reads a plan by name.
///
/// Returns a fixed placeholder `Plan` today.
///
/// # Arguments
///
/// * `name` - Plan filename without extension.
///
/// # Returns
///
/// A placeholder `Plan` whose body explains that real CRUD lands in Feature 06.
///
/// # Errors
///
/// Reserved for future implementations.
#[tauri::command]
pub fn read_plan(name: String) -> Result<Plan, AppError> {
    Ok(Plan {
        name,
        last_modified: 0,
        content: "# Stub plan\n\nReal plans CRUD lands in Feature 06.".to_string(),
        frontmatter: PlanFrontmatter::new(),
    })
}

/// Stub: writes a plan to disk.
///
/// No-op today. Real implementation lands in Feature 06.
///
/// # Arguments
///
/// * `name` - Plan filename without extension (currently ignored).
/// * `content` - Markdown body to persist (currently ignored).
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
pub fn write_plan(name: String, content: String) -> Result<(), AppError> {
    Ok(())
}

/// Stub: deletes a plan.
///
/// No-op today. Real implementation lands in Feature 06.
///
/// # Arguments
///
/// * `name` - Plan filename without extension (currently ignored).
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
pub fn delete_plan(name: String) -> Result<(), AppError> {
    Ok(())
}

// endregion