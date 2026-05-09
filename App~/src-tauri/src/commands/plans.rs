//! Plans Tauri commands.
//!
//! Real `list_plans`, `read_plan`, and `write_plan`; `delete_plan` is
//! still a stub (real impl lands in task 1.4).

use std::fs;
use std::io::ErrorKind;
use std::path::{Path, PathBuf};
use std::time::UNIX_EPOCH;

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

// region: Internal — name validation

/// Validates a plan name against the kebab-case rule shared by the
/// chat skills and the React tab: lowercase ASCII letters, digits, and
/// hyphens only; must start with a letter or digit; 1-64 characters.
///
/// Hand-coded char-by-char (no `regex` dep) — the rule is trivial and
/// `regex` would balloon the dep tree for an anchored ASCII match.
fn validate_plan_name(name: &str) -> Result<(), AppError> {
    if name.is_empty() || name.len() > 64 {
        return Err(AppError::InvalidInput(
            "Plan name must be 1-64 characters.".into(),
        ));
    }
    let mut chars = name.chars();
    let first = chars.next().expect("non-empty checked above");
    if !(first.is_ascii_lowercase() || first.is_ascii_digit()) {
        return Err(AppError::InvalidInput(
            "Plan name must start with a lowercase letter or digit.".into(),
        ));
    }
    for c in chars {
        if !(c.is_ascii_lowercase() || c.is_ascii_digit() || c == '-') {
            return Err(AppError::InvalidInput(
                "Plan name may only contain lowercase letters, digits, and hyphens.".into(),
            ));
        }
    }
    Ok(())
}

// endregion

// region: Internal — frontmatter

/// Splits a markdown file's optional YAML frontmatter from its body.
///
/// Returns `(frontmatter_map, body)`. Three cases:
///
/// 1. **No opening delimiter** (after optional BOM strip): returns
///    `({}, raw_without_BOM)`; the whole input is the body.
/// 2. **Opening delimiter present but YAML malformed or non-mapping**:
///    returns `({}, body_after_close)` — graceful so the Plans tab can
///    still surface the body for editing.
///    **Opening delimiter that never closes**: returns `({}, raw)` —
///    treated as if there is no frontmatter, so a half-typed file in
///    the editor doesn't lose its content.
/// 3. **Valid YAML mapping between delimiters**: returns `(map, body)`.
///
/// Frontmatter parses into `serde_json::Map<String, Value>` (the
/// `PlanFrontmatter` alias) by deserializing YAML directly into
/// `serde_json::Value` — YAML is effectively a superset of JSON, so
/// any standard frontmatter round-trips. Edge YAML features that don't
/// map to JSON (non-string keys, timestamps, etc.) fall back to `{}`.
fn parse_frontmatter(raw: &str) -> (PlanFrontmatter, String) {
    let raw = raw.strip_prefix('\u{FEFF}').unwrap_or(raw);

    let after_open = match raw
        .strip_prefix("---\n")
        .or_else(|| raw.strip_prefix("---\r\n"))
    {
        Some(rest) => rest,
        None => return (PlanFrontmatter::new(), raw.to_string()),
    };

    let (yaml, body) = if let Some(end) = after_open.find("\n---\n") {
        (&after_open[..end], &after_open[end + "\n---\n".len()..])
    } else if let Some(end) = after_open.find("\n---\r\n") {
        (&after_open[..end], &after_open[end + "\n---\r\n".len()..])
    } else {
        return (PlanFrontmatter::new(), raw.to_string());
    };

    let frontmatter: PlanFrontmatter = match serde_yaml::from_str::<serde_json::Value>(yaml) {
        Ok(serde_json::Value::Object(map)) => map,
        _ => PlanFrontmatter::new(),
    };

    (frontmatter, body.to_string())
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

/// Pure suffix-ladder logic: tries `base`, then `base-2`, ..., `base-99`,
/// returning the first name for which `exists` returns `false`. Returns
/// `None` if all 99 candidates are taken.
///
/// Filesystem-free by design — `exists` is injected so the ladder can be
/// unit-tested without a real plans dir. The dash separator is part of
/// the contract: matches `/save-plan`'s skill-side suffix scheme so
/// chat-path and UI-path collisions surface identically named files.
fn next_available_suffix(base: &str, exists: impl Fn(&str) -> bool) -> Option<String> {
    if !exists(base) {
        return Some(base.to_string());
    }
    for n in 2..=99 {
        let candidate = format!("{base}-{n}");
        if !exists(&candidate) {
            return Some(candidate);
        }
    }
    None
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
///   kebab-case rule enforced by `validate_plan_name`.
///
/// # Returns
///
/// The plan's `name`, mtime (`last_modified`), `content` (body without
/// the `---` delimiters), and `frontmatter` map.
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
    validate_plan_name(&name)?;

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

    let last_modified = metadata
        .modified()
        .ok()
        .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0);

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
/// writes `content` to `<name>.md.tmp` and renames it over `<name>.md`.
/// The tmp-then-rename two-step survives partial-write crashes: a
/// crashed write leaves a `.md.tmp` zombie that `list_plans` ignores
/// (filter is `extension == "md"` exact, not `.md.*`).
///
/// **Overwrite semantics:** the existing `<name>.md` is replaced
/// without backup. Auto-suffix is the `/save-plan` skill's job, not
/// this command's — the React Plans tab Save button writes back to the
/// same name on purpose.
///
/// # Arguments
///
/// * `name` - Plan filename without extension; must match the
///   kebab-case rule enforced by `validate_plan_name`.
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
///   error during write/rename. Best-effort cleanup of the `.md.tmp`
///   leftover runs on rename failure.
#[tauri::command]
pub fn write_plan(name: String, content: String) -> Result<(), AppError> {
    validate_plan_name(&name)?;

    let dir = plans_dir().ok_or_else(|| {
        AppError::FileNotFound(format!(
            "Cannot save plan '{name}': no Unity project pinned"
        ))
    })?;

    ensure_plans_dir(&dir).map_err(|e| {
        AppError::Internal(format!(
            "Failed to ensure plans dir at {}: {e}",
            dir.display()
        ))
    })?;

    let final_path = dir.join(format!("{name}.md"));
    let tmp_path = dir.join(format!("{name}.md.tmp"));

    fs::write(&tmp_path, &content).map_err(|e| match e.kind() {
        ErrorKind::PermissionDenied => {
            AppError::PermissionDenied(format!("Cannot write plan '{name}'"))
        }
        _ => AppError::Internal(format!("Failed to write plan '{name}': {e}")),
    })?;

    fs::rename(&tmp_path, &final_path).map_err(|e| {
        let _ = fs::remove_file(&tmp_path);
        match e.kind() {
            ErrorKind::PermissionDenied => {
                AppError::PermissionDenied(format!("Cannot write plan '{name}'"))
            }
            _ => AppError::Internal(format!("Failed to write plan '{name}': {e}")),
        }
    })?;

    Ok(())
}

// endregion

// region: Delete — STUB (real impl lands in task 1.4)

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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parse_frontmatter_no_delimiters() {
        let (fm, body) = parse_frontmatter("# Just a heading\n\nbody");
        assert!(fm.is_empty());
        assert_eq!(body, "# Just a heading\n\nbody");
    }

    #[test]
    fn parse_frontmatter_empty_input() {
        let (fm, body) = parse_frontmatter("");
        assert!(fm.is_empty());
        assert_eq!(body, "");
    }

    #[test]
    fn parse_frontmatter_valid_yaml_single_key() {
        let raw = "---\ndescription: hello world\n---\n# Body";
        let (fm, body) = parse_frontmatter(raw);
        assert_eq!(
            fm.get("description").and_then(|v| v.as_str()),
            Some("hello world")
        );
        assert_eq!(body, "# Body");
    }

    #[test]
    fn parse_frontmatter_valid_yaml_multiple_keys() {
        let raw = "---\ndescription: foo\ntags:\n  - a\n  - b\n---\nbody";
        let (fm, body) = parse_frontmatter(raw);
        assert_eq!(fm.get("description").and_then(|v| v.as_str()), Some("foo"));
        assert!(fm.get("tags").and_then(|v| v.as_array()).is_some());
        assert_eq!(body, "body");
    }

    #[test]
    fn parse_frontmatter_malformed_yaml_keeps_body() {
        let raw = "---\n\t: : :\n---\nbody after malformed";
        let (fm, body) = parse_frontmatter(raw);
        assert!(fm.is_empty());
        assert_eq!(body, "body after malformed");
    }

    #[test]
    fn parse_frontmatter_non_mapping_yaml_falls_back() {
        let raw = "---\n- foo\n- bar\n---\nbody";
        let (fm, body) = parse_frontmatter(raw);
        assert!(fm.is_empty());
        assert_eq!(body, "body");
    }

    #[test]
    fn parse_frontmatter_unclosed_delimiter_keeps_full_raw_as_body() {
        let raw = "---\ndescription: foo\nno closing delimiter here";
        let (fm, body) = parse_frontmatter(raw);
        assert!(fm.is_empty());
        assert_eq!(body, raw);
    }

    #[test]
    fn parse_frontmatter_strips_bom() {
        let raw = "\u{FEFF}---\ndescription: foo\n---\nbody";
        let (fm, body) = parse_frontmatter(raw);
        assert_eq!(fm.get("description").and_then(|v| v.as_str()), Some("foo"));
        assert_eq!(body, "body");
    }

    #[test]
    fn parse_frontmatter_crlf_line_endings() {
        let raw = "---\r\ndescription: foo\r\n---\r\nbody";
        let (fm, body) = parse_frontmatter(raw);
        assert_eq!(fm.get("description").and_then(|v| v.as_str()), Some("foo"));
        assert_eq!(body, "body");
    }

    #[test]
    fn validate_plan_name_accepts_kebab_case() {
        assert!(validate_plan_name("setup-2d-roguelike").is_ok());
        assert!(validate_plan_name("a").is_ok());
        assert!(validate_plan_name("123").is_ok());
        assert!(validate_plan_name("9-lives").is_ok());
    }

    #[test]
    fn validate_plan_name_rejects_empty() {
        assert!(matches!(
            validate_plan_name(""),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_plan_name_rejects_too_long() {
        let long: String = "a".repeat(65);
        assert!(matches!(
            validate_plan_name(&long),
            Err(AppError::InvalidInput(_))
        ));
        let exact: String = "a".repeat(64);
        assert!(validate_plan_name(&exact).is_ok());
    }

    #[test]
    fn validate_plan_name_rejects_uppercase() {
        assert!(matches!(
            validate_plan_name("MyPlan"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_plan_name_rejects_spaces() {
        assert!(matches!(
            validate_plan_name("my plan"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_plan_name_rejects_special_chars() {
        assert!(matches!(
            validate_plan_name("my-plan!"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_plan_name_rejects_leading_hyphen() {
        assert!(matches!(
            validate_plan_name("-leading"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_plan_name_rejects_non_ascii() {
        assert!(matches!(
            validate_plan_name("plà"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn read_plan_rejects_invalid_name() {
        let err = read_plan("Has Spaces".to_string()).unwrap_err();
        assert!(matches!(err, AppError::InvalidInput(_)));
    }

    #[test]
    fn next_available_suffix_returns_base_when_free() {
        let result = next_available_suffix("foo", |_| false);
        assert_eq!(result, Some("foo".to_string()));
    }

    #[test]
    fn next_available_suffix_skips_taken_base() {
        let result = next_available_suffix("foo", |name| name == "foo");
        assert_eq!(result, Some("foo-2".to_string()));
    }

    #[test]
    fn next_available_suffix_skips_multiple_taken() {
        let taken = ["foo", "foo-2", "foo-3"];
        let result = next_available_suffix("foo", |name| taken.contains(&name));
        assert_eq!(result, Some("foo-4".to_string()));
    }

    #[test]
    fn next_available_suffix_returns_none_when_all_99_taken() {
        let result = next_available_suffix("foo", |_| true);
        assert_eq!(result, None);
    }

    #[test]
    fn next_available_suffix_uses_dash_separator_not_underscore() {
        let result = next_available_suffix("foo", |name| name == "foo");
        let candidate = result.expect("base taken, suffix expected");
        assert!(
            candidate.contains('-') && !candidate.contains('_'),
            "expected dash separator, got: {candidate}"
        );
    }

    #[test]
    fn next_available_suffix_returns_99_when_only_99_free() {
        let result = next_available_suffix("foo", |name| name != "foo-99");
        assert_eq!(result, Some("foo-99".to_string()));
    }
}