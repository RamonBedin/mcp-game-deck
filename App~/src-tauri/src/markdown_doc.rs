//! Shared markdown-document helpers for on-disk YAML-frontmatter
//! markdown files (plans, rules, and future v2.x peers).
//!
//! here is filesystem-light, dependency-free, and shared by every
//! caller that owns a `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/
//! <subdir>/<name>.md`-shaped store.
//!
//! **What lives here:**
//! - YAML frontmatter splitting (`parse_frontmatter`).
//! - Kebab-case name validation (`validate_kebab_name`).
//! - Idempotent directory creation (`ensure_dir`).
//! - Tmp-then-rename atomic writes (`atomic_write`).
//! - Millisecond mtime extraction (`read_metadata_ms`).
//! - Pure suffix-ladder collision resolution (`next_available_suffix`).
//!
//! **What does NOT live here:** anything domain-specific (paths,
//! command names, frontmatter schema). Callers compose these helpers
//! with their own `<X>_dir()` resolver and their own typed structs.

use std::fs;
use std::io::ErrorKind;
use std::path::Path;
use std::time::UNIX_EPOCH;

use crate::types::AppError;

// region: Types

/// Generic frontmatter map, parsed from YAML into JSON-shaped values.
///
/// YAML is effectively a superset of JSON, so any standard frontmatter
/// round-trips through `serde_json::Value`. Edge YAML features that
/// don't map to JSON (non-string keys, timestamps, etc.) cause the
/// parse to fall back to an empty map — see [`parse_frontmatter`].
pub type FrontmatterMap = serde_json::Map<String, serde_json::Value>;

// endregion

// region: Frontmatter parsing

/// Splits a markdown file's optional YAML frontmatter from its body.
///
/// Returns `(frontmatter_map, body)`. Three cases:
///
/// 1. **No opening delimiter** (after optional BOM strip): returns
///    `({}, raw_without_BOM)`; the whole input is the body.
/// 2. **Opening delimiter present but YAML malformed or non-mapping**:
///    returns `({}, body_after_close)` — graceful so the UI can still
///    surface the body for editing.
///    **Opening delimiter that never closes**: returns `({}, raw)` —
///    treated as if there is no frontmatter, so a half-typed file in
///    the editor doesn't lose its content.
/// 3. **Valid YAML mapping between delimiters**: returns `(map, body)`.
///
/// Frontmatter parses into `serde_json::Map<String, Value>` (the
/// `FrontmatterMap` alias) by deserializing YAML directly into
/// `serde_json::Value`.
pub fn parse_frontmatter(raw: &str) -> (FrontmatterMap, String) {
    let raw = raw.strip_prefix('\u{FEFF}').unwrap_or(raw);

    let after_open = match raw
        .strip_prefix("---\n")
        .or_else(|| raw.strip_prefix("---\r\n"))
    {
        Some(rest) => rest,
        None => return (FrontmatterMap::new(), raw.to_string()),
    };

    let (yaml, body) = if let Some(end) = after_open.find("\n---\n") {
        (&after_open[..end], &after_open[end + "\n---\n".len()..])
    } else if let Some(end) = after_open.find("\n---\r\n") {
        (&after_open[..end], &after_open[end + "\n---\r\n".len()..])
    } else {
        return (FrontmatterMap::new(), raw.to_string());
    };

    let frontmatter: FrontmatterMap = match serde_yaml::from_str::<serde_json::Value>(yaml) {
        Ok(serde_json::Value::Object(map)) => map,
        _ => FrontmatterMap::new(),
    };

    (frontmatter, body.to_string())
}

// endregion

// region: Name validation

/// Validates a name against the kebab-case rule shared by every
/// markdown-doc store (plans, rules, ...): lowercase ASCII letters,
/// digits, and hyphens only; must start with a letter or digit; 1-64
/// characters.
///
/// Hand-coded char-by-char (no `regex` dep) — the rule is trivial and
/// `regex` would balloon the dep tree for an anchored ASCII match.
///
/// # Errors
///
/// - `InvalidInput` when the name is empty, longer than 64 chars,
///   starts with a non-alphanumeric character, or contains any
///   character outside `[a-z0-9-]`.
pub fn validate_kebab_name(name: &str) -> Result<(), AppError> {
    if name.is_empty() || name.len() > 64 {
        return Err(AppError::InvalidInput(
            "Name must be 1-64 characters.".into(),
        ));
    }
    let mut chars = name.chars();
    let first = chars.next().expect("non-empty checked above");
    if !(first.is_ascii_lowercase() || first.is_ascii_digit()) {
        return Err(AppError::InvalidInput(
            "Name must start with a lowercase letter or digit.".into(),
        ));
    }
    for c in chars {
        if !(c.is_ascii_lowercase() || c.is_ascii_digit() || c == '-') {
            return Err(AppError::InvalidInput(
                "Name may only contain lowercase letters, digits, and hyphens.".into(),
            ));
        }
    }
    Ok(())
}

// endregion

// region: Directory helpers

/// Creates `dir` (and any missing parents) if it doesn't already
/// exist. Idempotent: suppresses `AlreadyExists`; surfaces other IO
/// errors to the caller.
pub fn ensure_dir(dir: &Path) -> std::io::Result<()> {
    match fs::create_dir_all(dir) {
        Ok(()) => Ok(()),
        Err(e) if e.kind() == ErrorKind::AlreadyExists => Ok(()),
        Err(e) => Err(e),
    }
}

// endregion

// region: Atomic write

/// Writes `content` atomically to `path` via tmp-then-rename.
///
/// Computes `<path>.tmp` as the staging file, writes `content` there,
/// then renames it over `path`. The two-step survives partial-write
/// crashes: a crashed write leaves a `<path>.tmp` zombie rather than
/// truncating the canonical file. Callers that list a directory
/// should filter on the canonical extension (e.g. `extension == "md"`
/// exact, not `.md.*`) so a stray `.md.tmp` is invisible.
///
/// **Cleanup on rename failure:** the `.tmp` file is best-effort
/// removed before the error surfaces, so a transient rename failure
/// doesn't leave a permanent zombie.
///
/// The parent directory of `path` must already exist; this helper
/// does not call [`ensure_dir`] on the caller's behalf.
pub fn atomic_write(path: &Path, content: &[u8]) -> std::io::Result<()> {
    let mut tmp = path.as_os_str().to_owned();
    tmp.push(".tmp");
    let tmp_path = std::path::PathBuf::from(tmp);

    fs::write(&tmp_path, content)?;

    if let Err(e) = fs::rename(&tmp_path, path) {
        let _ = fs::remove_file(&tmp_path);
        return Err(e);
    }

    Ok(())
}

// endregion

// region: Metadata helpers

/// Returns a filesystem `Metadata`'s mtime expressed in **milliseconds
/// since the Unix epoch**, or `0` when the platform doesn't expose
/// mtime or the timestamp predates the epoch.
///
/// Standardizing on milliseconds keeps every wire type symmetric with
/// `SessionSummary.last_modified` and the React side's
/// `Date.now()`-based math — no compensating `* 1000` in the UI.
pub fn read_metadata_ms(metadata: &fs::Metadata) -> i64 {
    metadata
        .modified()
        .ok()
        .and_then(|t| t.duration_since(UNIX_EPOCH).ok())
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

// endregion

// region: Suffix ladder

/// Pure suffix-ladder logic: tries `base`, then `base-2`, ...,
/// `base-99`, returning the first name for which `exists` returns
/// `false`. Returns `None` if all 99 candidates are taken.
///
/// Filesystem-free by design — `exists` is injected so the ladder can
/// be unit-tested without a real directory. The dash separator is
/// part of the contract: matches `/save-plan`'s skill-side suffix
/// scheme so chat-path and UI-path collisions surface identically
/// named files.
pub fn next_available_suffix(base: &str, exists: impl Fn(&str) -> bool) -> Option<String> {
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

#[cfg(test)]
mod tests {
    use super::*;

    // region: parse_frontmatter

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

    // endregion

    // region: validate_kebab_name

    #[test]
    fn validate_kebab_name_accepts_kebab_case() {
        assert!(validate_kebab_name("setup-2d-roguelike").is_ok());
        assert!(validate_kebab_name("a").is_ok());
        assert!(validate_kebab_name("123").is_ok());
        assert!(validate_kebab_name("9-lives").is_ok());
    }

    #[test]
    fn validate_kebab_name_rejects_empty() {
        assert!(matches!(
            validate_kebab_name(""),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_kebab_name_rejects_too_long() {
        let long: String = "a".repeat(65);
        assert!(matches!(
            validate_kebab_name(&long),
            Err(AppError::InvalidInput(_))
        ));
        let exact: String = "a".repeat(64);
        assert!(validate_kebab_name(&exact).is_ok());
    }

    #[test]
    fn validate_kebab_name_rejects_uppercase() {
        assert!(matches!(
            validate_kebab_name("MyPlan"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_kebab_name_rejects_spaces() {
        assert!(matches!(
            validate_kebab_name("my plan"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_kebab_name_rejects_special_chars() {
        assert!(matches!(
            validate_kebab_name("my-plan!"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_kebab_name_rejects_leading_hyphen() {
        assert!(matches!(
            validate_kebab_name("-leading"),
            Err(AppError::InvalidInput(_))
        ));
    }

    #[test]
    fn validate_kebab_name_rejects_non_ascii() {
        assert!(matches!(
            validate_kebab_name("plà"),
            Err(AppError::InvalidInput(_))
        ));
    }

    // endregion

    // region: next_available_suffix

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

    // endregion

    // region: atomic_write

    #[test]
    fn atomic_write_creates_file_and_cleans_up_tmp() {
        let dir = std::env::temp_dir().join(format!(
            "mcp-game-deck-md-doc-test-{}",
            std::time::SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        fs::create_dir_all(&dir).unwrap();

        let target = dir.join("x.md");
        atomic_write(&target, b"hello").unwrap();

        assert_eq!(fs::read_to_string(&target).unwrap(), "hello");
        let tmp = dir.join("x.md.tmp");
        assert!(!tmp.exists(), "tmp file should have been renamed away");

        // Overwrite path: same target, new content, no leftover tmp.
        atomic_write(&target, b"world").unwrap();
        assert_eq!(fs::read_to_string(&target).unwrap(), "world");
        assert!(!tmp.exists());

        fs::remove_dir_all(&dir).ok();
    }

    // endregion

    // region: ensure_dir

    #[test]
    fn ensure_dir_is_idempotent() {
        let dir = std::env::temp_dir().join(format!(
            "mcp-game-deck-md-doc-ensure-{}",
            std::time::SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        ensure_dir(&dir).unwrap();
        assert!(dir.is_dir());
        // Second call must not error.
        ensure_dir(&dir).unwrap();
        fs::remove_dir_all(&dir).ok();
    }

    // endregion

    // region: read_metadata_ms

    #[test]
    fn read_metadata_ms_returns_recent_timestamp() {
        let dir = std::env::temp_dir().join(format!(
            "mcp-game-deck-md-doc-mtime-{}",
            std::time::SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        fs::create_dir_all(&dir).unwrap();
        let path = dir.join("a.md");
        fs::write(&path, b"x").unwrap();

        let now_ms = std::time::SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_millis() as i64;
        let meta = fs::metadata(&path).unwrap();
        let mtime = read_metadata_ms(&meta);

        assert!(mtime > 1_000_000_000_000, "expected ms timestamp, got {mtime}");
        assert!((now_ms - mtime).abs() < 60_000, "mtime drifted too far from now");

        fs::remove_dir_all(&dir).ok();
    }

    // endregion
}