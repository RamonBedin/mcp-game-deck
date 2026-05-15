//! Rules Tauri commands.
//!
//! Real `list_rules`, `read_rule`, `write_rule`, `delete_rule`, and
//! `toggle_rule`.

use std::fs;
use std::io::ErrorKind;
use std::path::{Path, PathBuf};

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

// region: Internal — cap enforcement

const ENABLED_CAP: usize = 10;

/// Counts how many rules in `dir` currently have `enabled: true` in
/// their YAML frontmatter. Returns `0` on any IO failure (best-effort
/// — used only for the soft cap; a missing dir means zero enabled).
fn count_enabled_rules(dir: &Path) -> usize {
    let Ok(entries) = fs::read_dir(dir) else {
        return 0;
    };
    entries
        .filter_map(|res| res.ok())
        .filter(|entry| {
            let path = entry.path();
            if path.extension().and_then(|e| e.to_str()) != Some("md") {
                return false;
            }
            let Ok(raw) = fs::read_to_string(&path) else {
                return false;
            };
            let (frontmatter, _body) = parse_frontmatter(&raw);
            frontmatter
                .get("enabled")
                .and_then(|v| v.as_bool())
                .unwrap_or(false)
        })
        .count()
}

// endregion

// region: Internal — surgical toggle

/// Computes the new file content after flipping the `enabled` key in
/// the rule's YAML frontmatter, and reports the previous `enabled`
/// state so the caller can short-circuit the cap check on no-ops.
///
/// Three input shapes are handled:
///
/// 1. **Valid YAML mapping between `---` delimiters:** parses via
///    `serde_yaml::Value::Mapping` (preserves insertion order),
///    inserts or updates the `enabled` key in place, re-serializes,
///    splices back. Existing key ordering is preserved across the
///    round-trip; unknown keys round-trip verbatim.
/// 2. **No frontmatter (or unclosed opening delimiter):** injects a
///    fresh `---\nenabled: <bool>\n---\n` block at the top; body
///    preserved byte-for-byte. Effectively materializes the
///    frontmatter on first toggle.
/// 3. **Non-mapping YAML (`null`, sequence, scalar):** returns
///    `AppError::Internal` — refusing to silently rewrite user
///    intent. The user is expected to fix the file by hand.
///
/// Returns `(new_content, was_enabled)`. `was_enabled` is `false`
/// when the file had no frontmatter or had no `enabled` key.
fn compose_toggled_file(raw: &str, enabled: bool) -> Result<(String, bool), AppError> {
    let stripped = raw.strip_prefix('\u{FEFF}').unwrap_or(raw);

    let after_open = stripped
        .strip_prefix("---\n")
        .or_else(|| stripped.strip_prefix("---\r\n"));

    let split = after_open.and_then(|rest| {
        if let Some(end) = rest.find("\n---\n") {
            Some((&rest[..end], &rest[end + "\n---\n".len()..]))
        } else if let Some(end) = rest.find("\n---\r\n") {
            Some((&rest[..end], &rest[end + "\n---\r\n".len()..]))
        } else {
            None
        }
    });

    let (yaml_text, body) = match split {
        Some(pair) => pair,
        None => {
            let injected = format!("---\nenabled: {enabled}\n---\n{stripped}");
            return Ok((injected, false));
        }
    };

    let value: serde_yaml::Value = serde_yaml::from_str(yaml_text)
        .map_err(|e| AppError::Internal(format!("Failed to parse rule frontmatter: {e}")))?;

    let serde_yaml::Value::Mapping(mut mapping) = value else {
        return Err(AppError::Internal(
            "Cannot toggle a rule with non-mapping frontmatter; edit the file directly."
                .into(),
        ));
    };

    let key = serde_yaml::Value::String("enabled".into());
    let was_enabled = mapping
        .get(&key)
        .and_then(|v| v.as_bool())
        .unwrap_or(false);
    mapping.insert(key, serde_yaml::Value::Bool(enabled));

    let new_yaml = serde_yaml::to_string(&serde_yaml::Value::Mapping(mapping))
        .map_err(|e| AppError::Internal(format!("Failed to serialize rule frontmatter: {e}")))?;

    let new_content = format!("---\n{new_yaml}---\n{body}");
    Ok((new_content, was_enabled))
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

/// Deletes a rule by name from the pinned Unity project's rules dir.
///
/// Validates the name format, resolves the rules directory, and
/// calls `fs::remove_file`. Does not create the rules dir on the way
/// in: if the dir is missing, the underlying `NotFound` surfaces
/// naturally (creating an empty dir just to delete a file inside it
/// would be nonsense).
///
/// # Arguments
///
/// * `name` - Rule filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule.
/// - `FileNotFound` when no Unity project is pinned, or when the
///   file does not exist on disk.
/// - `PermissionDenied` when the OS rejects the unlink.
/// - `Internal` for any other IO error.
#[tauri::command]
pub fn delete_rule(name: String) -> Result<(), AppError> {
    validate_kebab_name(&name)?;

    let dir = rules_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot delete rule '{name}': no Unity project pinned"
        ))
    })?;

    let path = dir.join(format!("{name}.md"));

    fs::remove_file(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Rule '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot delete rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to delete rule '{name}': {e}")),
    })?;

    Ok(())
}

// endregion

// region: Toggle

/// Enables or disables a rule by surgically rewriting its YAML
/// frontmatter's `enabled` key. Body and unknown frontmatter fields
/// round-trip verbatim.
///
/// **Cap enforcement (defense in depth):** when `enabled == true`,
/// counts currently-enabled rules in the dir; if already at
/// [`ENABLED_CAP`] (10) AND the target rule wasn't already enabled,
/// returns `InvalidInput("Rule cap reached (10 enabled). Disable
/// one first.")`. Idempotent enable on an already-enabled rule
/// bypasses the cap (no count growth). Disabling is always allowed.
///
/// **Non-mapping frontmatter:** if the file's frontmatter parses as
/// anything other than a YAML mapping (e.g. `null`, a sequence, a
/// scalar), the command returns `Internal` rather than silently
/// rewriting — user fixes the file manually.
///
/// **No frontmatter:** the file gets a fresh `---\nenabled:
/// <bool>\n---\n` block prepended; body is preserved byte-for-byte.
///
/// # Arguments
///
/// * `name` - Rule filename without extension; must match the
///   kebab-case rule enforced by `markdown_doc::validate_kebab_name`.
/// * `enabled` - Desired activation flag.
///
/// # Errors
///
/// - `InvalidInput` when `name` violates the kebab-case rule or
///   when the cap would be exceeded.
/// - `FileNotFound` when no Unity project is pinned, or when the
///   rule file is missing.
/// - `PermissionDenied` when the OS rejects the read or write.
/// - `Internal` for non-mapping frontmatter, YAML parse/serialize
///   failures, or other IO errors.
#[tauri::command]
pub fn toggle_rule(name: String, enabled: bool) -> Result<(), AppError> {
    validate_kebab_name(&name)?;

    let dir = rules_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot toggle rule '{name}': no Unity project pinned"
        ))
    })?;

    let path = dir.join(format!("{name}.md"));

    let raw = fs::read_to_string(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Rule '{name}' not found")),
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot read rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to read rule '{name}': {e}")),
    })?;

    let (new_content, was_enabled) = compose_toggled_file(&raw, enabled)?;

    if enabled && !was_enabled && count_enabled_rules(&dir) >= ENABLED_CAP {
        return Err(AppError::InvalidInput(
            "Rule cap reached (10 enabled). Disable one first.".into(),
        ));
    }

    atomic_write(&path, new_content.as_bytes()).map_err(|e| match e.kind() {
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot write rule '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to write rule '{name}': {e}")),
    })?;

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

    #[test]
    fn delete_rule_rejects_invalid_name() {
        let err = delete_rule("Has Spaces".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }

    #[test]
    fn toggle_rule_rejects_invalid_name() {
        let err = toggle_rule("Has Spaces".to_string(), true).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }

    #[test]
    fn compose_toggled_file_flips_enabled_in_existing_mapping() {
        let raw = "---\nenabled: false\ndescription: foo\n---\n# Body\n";
        let (out, was) = compose_toggled_file(raw, true).unwrap();
        assert!(!was);
        assert!(out.contains("enabled: true"));
        assert!(out.ends_with("# Body\n"));
        assert!(!out.contains("enabled: false"));
    }

    #[test]
    fn compose_toggled_file_preserves_key_order() {
        let raw =
            "---\ndescription: d\nenabled: false\napplies-to:\n- ui\ncustom: keep\n---\nbody";
        let (out, _) = compose_toggled_file(raw, true).unwrap();
        let desc_pos = out.find("description").unwrap();
        let enabled_pos = out.find("enabled").unwrap();
        let applies_pos = out.find("applies-to").unwrap();
        let custom_pos = out.find("custom").unwrap();
        assert!(desc_pos < enabled_pos);
        assert!(enabled_pos < applies_pos);
        assert!(applies_pos < custom_pos);
    }

    #[test]
    fn compose_toggled_file_preserves_body_bytes() {
        let raw = "---\nenabled: false\n---\n# Heading\n\n- item\n- item2\n";
        let (out, _) = compose_toggled_file(raw, true).unwrap();
        assert!(out.ends_with("# Heading\n\n- item\n- item2\n"));
    }

    #[test]
    fn compose_toggled_file_injects_block_when_no_frontmatter() {
        let raw = "# Just a body\n\nno frontmatter at all\n";
        let (out, was) = compose_toggled_file(raw, true).unwrap();
        assert!(!was);
        assert!(out.starts_with("---\nenabled: true\n---\n"));
        assert!(out.ends_with("# Just a body\n\nno frontmatter at all\n"));
    }

    #[test]
    fn compose_toggled_file_rejects_non_mapping_frontmatter() {
        let raw = "---\n- foo\n- bar\n---\nbody\n";
        let err = compose_toggled_file(raw, true).unwrap_err();
        assert!(matches!(err, AppError::Internal(_)));
    }

    #[test]
    fn compose_toggled_file_reports_was_enabled_when_already_true() {
        let raw = "---\nenabled: true\n---\nbody";
        let (_out, was) = compose_toggled_file(raw, true).unwrap();
        assert!(was);
    }

    #[test]
    fn compose_toggled_file_inserts_enabled_when_key_missing() {
        let raw = "---\ndescription: foo\n---\nbody";
        let (out, was) = compose_toggled_file(raw, true).unwrap();
        assert!(!was);
        assert!(out.contains("enabled: true"));
        assert!(out.contains("description: foo"));
    }
}