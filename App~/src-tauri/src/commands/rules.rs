//! Rules Tauri commands.

use std::fs;
use std::io::ErrorKind;
use std::path::PathBuf;

use crate::markdown_doc::{
    atomic_write, ensure_dir, parse_frontmatter, read_metadata_ms, validate_kebab_name,
};
use crate::project_root::try_resolve_project_root;
use crate::types::{AppError, Rule, RuleMeta};

const RULES_SUBDIR: &str = "ProjectSettings/GameDeck/rules";

// region: Internal — path resolution

/// Returns the absolute path to the rules directory for the current
/// project, or `None` when no project root resolves.
pub(crate) fn rules_dir() -> Option<PathBuf> {
    try_resolve_project_root().map(|root| root.join(RULES_SUBDIR))
}

// endregion

// region: Internal — token estimate

/// Rounded-up `chars / 4` heuristic. Counts the FULL file content
/// (frontmatter delimiters + YAML + body) — that's what the bundle
/// compiler will eventually inject via `appendSystemPromptFile`, so
/// the estimate is directionally honest about cost.
///
/// The React side mirrors this formula in `tokenEstimate.ts` for the
/// live editor count; Rust is authoritative for `list_rules` and the
/// header summary.
fn estimate_tokens(raw: &str) -> u32 {
    ((raw.chars().count() as u32) + 3) / 4
}

// endregion

// region: Listing

/// Lists every rule saved for the currently-pinned Unity project.
///
/// Reads `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/rules/*.md`
/// (one level, no recursion), parses YAML frontmatter for
/// `enabled` / `description` / `applies-to`, computes
/// `estimated_tokens` from the full file content, derives
/// `last_modified` from filesystem mtime (in milliseconds), and
/// returns the list sorted by mtime descending. Creates the rules
/// directory on first call (idempotent).
///
/// **Conservative defaults:** missing or malformed `enabled` falls
/// to `false` — a broken rule can never accidentally activate.
/// `applies-to` falls back to `[]` when absent, non-array, or
/// contains non-string entries (informational-only field in v2.0).
/// `description` becomes `None` when absent, blank, or non-string.
///
/// # Returns
///
/// All rule metadata in the project's rules dir. Returns an empty
/// vector when no Unity project resolves, the dir cannot be
/// created, or read fails — same shape as `list_plans`.
#[tauri::command]
pub fn list_rules() -> Vec<RuleMeta> {
    let Some(dir) = rules_dir() else {
        return Vec::new();
    };

    if let Err(e) = ensure_dir(&dir) {
        eprintln!(
            "[rules] failed to ensure rules dir at {}: {e}",
            dir.display()
        );
        return Vec::new();
    }

    let entries = match fs::read_dir(&dir) {
        Ok(e) => e,
        Err(e) => {
            eprintln!("[rules] read_dir failed at {}: {e}", dir.display());
            return Vec::new();
        }
    };

    let mut metas: Vec<RuleMeta> = entries
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
            let estimated_tokens = estimate_tokens(&raw);
            let (frontmatter, _body) = parse_frontmatter(&raw);

            let enabled = frontmatter
                .get("enabled")
                .and_then(|v| v.as_bool())
                .unwrap_or(false);

            let description = frontmatter
                .get("description")
                .and_then(|v| v.as_str())
                .map(str::trim)
                .filter(|s| !s.is_empty())
                .map(str::to_string);

            let applies_to = frontmatter
                .get("applies-to")
                .and_then(|v| v.as_array())
                .map(|arr| {
                    arr.iter()
                        .filter_map(|v| v.as_str().map(str::to_string))
                        .collect()
                })
                .unwrap_or_default();

            Some(RuleMeta {
                name,
                last_modified,
                enabled,
                description,
                applies_to,
                estimated_tokens,
            })
        })
        .collect();

    metas.sort_by(|a, b| b.last_modified.cmp(&a.last_modified));
    metas
}

// endregion

// region: Read / write

/// Reads a rule by name from the pinned Unity project's rules dir.
///
/// Validates the name format, then reads
/// `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/rules/<name>.md` and
/// splits the optional YAML frontmatter from the body. Malformed YAML
/// is tolerated: `frontmatter` falls back to an empty map and the
/// body still surfaces so the Rules pane can edit and resave.
///
/// # Arguments
///
/// * `name` - Rule filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
///
/// # Returns
///
/// The rule's `name`, mtime (`last_modified`, in milliseconds),
/// `content` (body without the `---` delimiters), the full
/// `frontmatter` map, and `estimated_tokens` (chars/4 round-up over
/// the full raw file).
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned, or when the
///   file does not exist on disk.
/// - `PermissionDenied` when the OS rejects the read.
/// - `Internal` for any other IO error or filesystem stat failure.
#[tauri::command]
pub fn read_rule(name: String) -> Result<Rule, AppError> {
    validate_kebab_name(&name)?;

    let dir = rules_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Rule '{name}' not found: no Unity project pinned"
        ))
    })?;

    let path = dir.join(format!("{name}.md"));

    let metadata = fs::metadata(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Rule '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot stat rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to stat rule '{name}': {e}")),
    })?;

    let last_modified = read_metadata_ms(&metadata);

    let raw = fs::read_to_string(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Rule '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot read rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to read rule '{name}': {e}")),
    })?;

    let estimated_tokens = estimate_tokens(&raw);
    let (frontmatter, content) = parse_frontmatter(&raw);

    Ok(Rule {
        name,
        last_modified,
        content,
        frontmatter,
        estimated_tokens,
    })
}

/// Writes a rule to disk atomically.
///
/// Validates the name format, ensures the rules directory exists,
/// then delegates to `markdown_doc::atomic_write` (tmp-then-rename).
/// The two-step survives partial-write crashes: a crashed write
/// leaves a `.md.tmp` zombie that `list_rules` ignores (filter is
/// `extension == "md"` exact, not `.md.*`).
///
/// **Overwrite semantics:** the existing `<name>.md` is replaced
/// without backup. Collision detection is the UI's job (the React
/// "+ New rule" flow checks the cached list before invoking).
/// `toggle_rule` also writes back through the same
/// underlying helper after splicing the frontmatter.
///
/// # Arguments
///
/// * `name` - Rule filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
/// * `content` - Full markdown body (frontmatter included if any).
///   Written verbatim.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned.
/// - `PermissionDenied` when the OS rejects the write or rename.
/// - `Internal` when ensuring the rules dir fails, or any other IO
///   error during write/rename.
#[tauri::command]
pub fn write_rule(name: String, content: String) -> Result<(), AppError> {
    validate_kebab_name(&name)?;

    let dir = rules_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot save rule '{name}': no Unity project pinned"
        ))
    })?;

    ensure_dir(&dir).map_err(|e| {
        AppError::Internal(format!(
            "Failed to ensure rules dir at {}: {e}",
            dir.display()
        ))
    })?;

    let final_path = dir.join(format!("{name}.md"));

    atomic_write(&final_path, content.as_bytes()).map_err(|e| match e.kind() {
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot write rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to write rule '{name}': {e}")),
    })?;

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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn estimate_tokens_rounds_up() {
        assert_eq!(estimate_tokens(""), 0);
        assert_eq!(estimate_tokens("a"), 1);
        assert_eq!(estimate_tokens("abcd"), 1);
        assert_eq!(estimate_tokens("abcde"), 2);
        assert_eq!(estimate_tokens(&"x".repeat(2000)), 500);
    }

    #[test]
    fn estimate_tokens_counts_unicode_chars_not_bytes() {
        assert_eq!(estimate_tokens("éééé"), 1);
    }

    #[test]
    fn read_rule_rejects_invalid_name() {
        let err = read_rule("Has Spaces".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }

    #[test]
    fn write_rule_rejects_invalid_name() {
        let err = write_rule("Has Spaces".to_string(), "x".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }
}