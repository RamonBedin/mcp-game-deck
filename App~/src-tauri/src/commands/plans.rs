//! Plans Tauri commands.
//!
//! Real `list_plans`

use std::fs;
use std::io::ErrorKind;
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

use serde_yaml::Value as YamlValue;

use crate::commands::settings::get_settings;
use crate::types::{AppError, Plan, PlanFrontmatter, PlanMeta};

const PLANS_SUBDIR: &str = "ProjectSettings/GameDeck/plans";

// region: Internal — path resolution

/// Resolves the Unity project root, or `None` if no source provides
/// one.
///
/// Tries `UNITY_PROJECT_PATH` env first (the F07 launch contract;
/// matches `claude_supervisor::spawn`'s resolution), then falls back to
/// the persisted `AppSettings.unity_project_path`. Returning `Option`
/// (rather than `Result`) lets every plans command coalesce "no
/// project pinned" into the same empty/not-found path it already
/// handles for "project pinned, no plans dir yet".
pub(super) fn try_resolve_project_root() -> Option<PathBuf> {
    if let Some(path) = std::env::var("UNITY_PROJECT_PATH")
        .ok()
        .filter(|s| !s.is_empty())
    {
        return Some(PathBuf::from(path));
    }

    get_settings().unity_project_path.map(PathBuf::from)
}

/// Returns the absolute path to the plans directory for the current
/// project, or `None` when no project root resolves.
pub(super) fn plans_dir() -> Option<PathBuf> {
    try_resolve_project_root().map(|root| root.join(PLANS_SUBDIR))
}

/// Creates the plans directory if missing. Idempotent: suppresses
/// `AlreadyExists`; surfaces other IO errors to the caller.
fn ensure_plans_dir(dir: &Path) -> std::io::Result<()> {
    match fs::create_dir_all(dir) {
        Ok(()) => Ok(()),
        Err(e) if e.kind() == ErrorKind::AlreadyExists => Ok(()),
        Err(e) => Err(e),
    }
}

// endregion

// region: Internal — frontmatter (mini-extractor for 1.1)

/// Extracts the optional `description` field from a markdown file's
/// YAML frontmatter.
///
/// Frontmatter must open at byte 0 with `---\n` (or `---\r\n`) and
/// close on a line containing only `---`. Anything malformed (missing
/// delimiters, invalid YAML, missing or non-string `description`,
/// blank `description`) returns `None` without erroring — list-view
/// convenience, not a contract.
fn extract_description(raw: &str) -> Option<String> {
    let raw = raw.strip_prefix('\u{FEFF}').unwrap_or(raw);
    let body = raw
        .strip_prefix("---\n")
        .or_else(|| raw.strip_prefix("---\r\n"))?;
    let end = body
        .find("\n---\n")
        .or_else(|| body.find("\n---\r\n"))?;
    let yaml = &body[..end];
    let parsed: YamlValue = serde_yaml::from_str(yaml).ok()?;
    match parsed.get("description")? {
        YamlValue::String(s) => {
            let trimmed = s.trim();
            if trimmed.is_empty() {
                None
            } else {
                Some(trimmed.to_string())
            }
        }
        _ => None,
    }
}

// endregion

// region: Listing

/// Lists every plan saved for the currently-pinned Unity project.
///
/// Reads `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/*.md`
/// (one level, no recursion), parses YAML frontmatter for the optional
/// `description`, derives `last_modified` from filesystem mtime, and
/// returns the list sorted by mtime descending. Creates the plans
/// directory on first call (idempotent).
///
/// # Returns
///
/// All plan metadata in the project's plans dir. Returns an empty
/// vector when:
/// - no Unity project root resolves (no env var, no persisted setting)
/// - the plans directory cannot be created
/// - the directory exists but cannot be read
///
/// Empty-list cases are intentionally not surfaced as errors: the
/// React tab uses other signals (settings store, file picker) to
/// distinguish "no project pinned" from "project pinned, no plans yet".
#[tauri::command]
pub fn list_plans() -> Vec<PlanMeta> {
    let Some(dir) = plans_dir() else {
        return Vec::new();
    };

    if let Err(e) = ensure_plans_dir(&dir) {
        eprintln!(
            "[plans] failed to ensure plans dir at {}: {e}",
            dir.display()
        );
        return Vec::new();
    }

    let entries = match fs::read_dir(&dir) {
        Ok(e) => e,
        Err(e) => {
            eprintln!("[plans] read_dir failed at {}: {e}", dir.display());
            return Vec::new();
        }
    };

    let mut metas: Vec<PlanMeta> = entries
        .filter_map(|res| res.ok())
        .filter_map(|entry| {
            let file_type = entry.file_type().ok()?;
            if !file_type.is_file() {
                return None;
            }
            let path = entry.path();
            if path.extension().and_then(|e| e.to_str()) != Some("md") {
                return None;
            }
            let name = path.file_stem()?.to_string_lossy().into_owned();
            let metadata = entry.metadata().ok()?;
            let last_modified = metadata
                .modified()
                .ok()
                .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
                .map(|d| d.as_secs() as i64)
                .unwrap_or(0);
            let raw = fs::read_to_string(&path).unwrap_or_default();
            let description = extract_description(&raw);
            Some(PlanMeta {
                name,
                last_modified,
                description,
            })
        })
        .collect();

    metas.sort_by(|a, b| b.last_modified.cmp(&a.last_modified));
    metas
}

// endregion

// region: Read / write — STUBS (real impls land in tasks 1.2 / 1.3 / 1.4)

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