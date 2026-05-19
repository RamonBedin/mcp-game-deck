//! Plans Tauri commands.
//!
//! Real `list_plans`, `read_plan`, `write_plan`, and `delete_plan`.

use std::fs;
use std::io::ErrorKind;
use std::path::PathBuf;

use crate::markdown_doc::{
    atomic_write, ensure_dir, next_available_suffix, parse_frontmatter, read_metadata_ms,
    validate_kebab_name,
};
use crate::project_root::try_resolve_project_root;
use crate::types::{AppError, Plan, PlanMeta};

const PLANS_SUBDIR: &str = "ProjectSettings/GameDeck/plans";

// region: Internal — path resolution

/// Returns the absolute path to the plans directory for the current
/// project, or `None` when no project root resolves.
pub(crate) fn plans_dir() -> Option<PathBuf> {
    try_resolve_project_root().map(|root| root.join(PLANS_SUBDIR))
}

// endregion

// region: Internal — name collision

/// Returns the next free `<base>.md` slot under the plans dir, falling
/// back through `<base>-2.md`, `<base>-3.md`, ..., `<base>-99.md`.
///
/// Returns `None` when no Unity project resolves OR when all 99 slots
/// are occupied. Caller decides which case to surface.
///
/// the helper to land alongside the atomic write logic so the React
/// "+ New plan" path in task 2.4 has it ready. The `dead_code` attribute
/// will come off in the PR that consumes it.
#[allow(dead_code)]
fn find_available_name(base: &str) -> Option<String> {
    let dir = plans_dir()?;
    next_available_suffix(base, |candidate| {
        dir.join(format!("{candidate}.md")).exists()
    })
}

// endregion

// region: Listing

/// Lists every plan saved for the currently-pinned Unity project.
///
/// Reads `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/*.md`
/// (one level, no recursion), parses YAML frontmatter for the optional
/// `description`, derives `last_modified` from filesystem mtime (in
/// milliseconds), and returns the list sorted by mtime descending.
/// Creates the plans directory on first call (idempotent).
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

    if let Err(e) = ensure_dir(&dir) {
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
            let last_modified = read_metadata_ms(&metadata);
            let raw = fs::read_to_string(&path).unwrap_or_default();
            let (frontmatter, _body) = parse_frontmatter(&raw);
            let description = frontmatter
                .get("description")
                .and_then(|v| v.as_str())
                .map(str::trim)
                .filter(|s| !s.is_empty())
                .map(str::to_string);
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

// region: Read

/// Reads a plan by name from the pinned Unity project's plans dir.
///
/// Validates the name format, then reads
/// `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/<name>.md` and
/// splits the optional YAML frontmatter from the body. Malformed YAML
/// is tolerated: `frontmatter` falls back to an empty map and the body
/// still surfaces so the Plans tab can edit and resave.
///
/// # Arguments
///
/// * `name` - Plan filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
///
/// # Returns
///
/// The plan's `name`, mtime (`last_modified`, in milliseconds),
/// `content` (body without the `---` delimiters), and `frontmatter`
/// map.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned, or when the file
///   does not exist on disk.
/// - `PermissionDenied` when the OS rejects the read.
/// - `Internal` for any other IO error or filesystem stat failure.
#[tauri::command]
pub fn read_plan(name: String) -> Result<Plan, AppError> {
    validate_kebab_name(&name)?;

    let dir = plans_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Plan '{name}' not found: no Unity project pinned"
        ))
    })?;

    let path = dir.join(format!("{name}.md"));

    let metadata = fs::metadata(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Plan '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot stat plan '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to stat plan '{name}': {e}")),
    })?;

    let last_modified = read_metadata_ms(&metadata);

    let raw = fs::read_to_string(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Plan '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot read plan '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to read plan '{name}': {e}")),
    })?;

    let (frontmatter, content) = parse_frontmatter(&raw);

    Ok(Plan {
        name,
        last_modified,
        content,
        frontmatter,
    })
}

// endregion

// region: Write

/// Writes a plan to disk atomically.
///
/// Validates the name format, ensures the plans directory exists, then
/// delegates to `markdown_doc::atomic_write` (tmp-then-rename). The
/// two-step survives partial-write crashes: a crashed write leaves a
/// `.md.tmp` zombie that `list_plans` ignores (filter is `extension ==
/// "md"` exact, not `.md.*`).
///
/// **Overwrite semantics:** the existing `<name>.md` is replaced
/// without backup. Auto-suffix is the `/save-plan` skill's job, not
/// this command's — the React Plans tab Save button writes back to the
/// same name on purpose.
///
/// # Arguments
///
/// * `name` - Plan filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
/// * `content` - Full markdown body (frontmatter included if any).
///   Written verbatim.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned (caller can't save
///   without a destination root). Mirrors `read_plan`'s symmetry so
///   the React side handles both with one error path.
/// - `PermissionDenied` when the OS rejects the write or rename.
/// - `Internal` when ensuring the plans dir fails, or any other IO
///   error during write/rename.
#[tauri::command]
pub fn write_plan(name: String, content: String) -> Result<(), AppError> {
    validate_kebab_name(&name)?;

    let dir = plans_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot save plan '{name}': no Unity project pinned"
        ))
    })?;

    ensure_dir(&dir).map_err(|e| {
        AppError::Internal(format!(
            "Failed to ensure plans dir at {}: {e}",
            dir.display()
        ))
    })?;

    let final_path = dir.join(format!("{name}.md"));

    atomic_write(&final_path, content.as_bytes()).map_err(|e| match e.kind() {
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot write plan '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to write plan '{name}': {e}")),
    })?;

    Ok(())
}

// endregion

// region: Delete

/// Deletes a plan by name from the pinned Unity project's plans dir.
///
/// Validates the name format, resolves the plans directory, and calls
/// `fs::remove_file`. Does not create the plans dir on the way in: if
/// the dir is missing, the underlying `NotFound` surfaces naturally
/// (creating an empty dir just to delete a file inside it would be
/// nonsense).
///
/// # Arguments
///
/// * `name` - Plan filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned, or when the file
///   does not exist on disk.
/// - `PermissionDenied` when the OS rejects the unlink.
/// - `Internal` for any other IO error.
#[tauri::command]
pub fn delete_plan(name: String) -> Result<(), AppError> {
    validate_kebab_name(&name)?;

    let dir = plans_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot delete plan '{name}': no Unity project pinned"
        ))
    })?;

    let path = dir.join(format!("{name}.md"));

    fs::remove_file(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Plan '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot delete plan '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to delete plan '{name}': {e}")),
    })?;

    Ok(())
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn read_plan_rejects_invalid_name() {
        let err = read_plan("Has Spaces".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }

    #[test]
    fn delete_plan_rejects_invalid_name() {
        let err = delete_plan("Has Spaces".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }
}