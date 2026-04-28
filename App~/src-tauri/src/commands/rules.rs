//! Rules Tauri commands.
//!
//! so the frontend can wire its calls today; implementations land later.

use crate::types::{AppError, Rule, RuleMeta};

// region: Listing

/// Stub: lists known rule files.
///
/// # Returns
///
/// An empty `Vec<RuleMeta>`.
#[tauri::command]
pub fn list_rules() -> Vec<RuleMeta> {
    Vec::new()
}

// endregion

// region: Read / write

/// Stub: reads a rule by name.
///
/// Returns a fixed placeholder `Rule` today.
///
/// # Arguments
///
/// * `name` - Rule filename without extension.
///
/// # Returns
///
/// A placeholder `Rule` whose body explains that real UI lands in Feature 08.
///
/// # Errors
///
/// Reserved for future implementations.
#[tauri::command]
pub fn read_rule(name: String) -> Result<Rule, AppError> {
    Ok(Rule {
        name,
        enabled: false,
        content: "# Stub rule\n\nReal rules UI lands in Feature 08.".to_string(),
    })
}

/// Stub: writes a rule to disk.
///
/// No-op today. Real implementation lands in Feature 08.
///
/// # Arguments
///
/// * `name` - Rule filename without extension (currently ignored).
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
pub fn write_rule(name: String, content: String) -> Result<(), AppError> {
    Ok(())
}

/// Stub: deletes a rule.
///
/// # Arguments
///
/// * `name` - Rule filename without extension (currently ignored).
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
pub fn delete_rule(name: String) -> Result<(), AppError> {
    Ok(())
}

// endregion

// region: Toggle

/// Stub: enables or disables a rule.
///
/// # Arguments
///
/// * `name` - Rule filename without extension (currently ignored).
/// * `enabled` - Desired activation flag (currently ignored).
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
pub fn toggle_rule(name: String, enabled: bool) -> Result<(), AppError> {
    Ok(())
}

// endregion