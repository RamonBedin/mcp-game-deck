//! Compiles enabled rule files into a single bundle markdown file
//! that the Claude Agent SDK injects as `appendSystemPromptFile` on
//! every `query()`.
//!
//! The bundle file lives at
//! `<UNITY_PROJECT_PATH>/Library/MCPGameDeck/rules-bundle.md` — a
//! derived artifact under Unity's gitignored `Library/`. Source of
//! truth remains the individual rule files under
//! `ProjectSettings/GameDeck/rules/` (which the user versions).
//!
//! `recompose` is invoked from three places (F08 task 3.3):
//! - Tauri app startup (lib.rs setup hook).
//! - The rules watcher loop, on every debounced event.
//! - `restart_supervisor`, after a project switch.
//!
//! When zero rules are enabled, the bundle file is **deleted** (not
//! written empty) so the JS-side existence check naturally omits
//! `appendSystemPromptFile` and the SDK never sees a zero-byte file.

use std::fs;
use std::io::ErrorKind;
use std::path::{Path, PathBuf};

use crate::markdown_doc::{atomic_write, ensure_dir, parse_frontmatter};

// region: Constants

const RULES_SUBDIR: &str = "ProjectSettings/GameDeck/rules";
const BUNDLE_SUBDIR: &str = "Library/MCPGameDeck";
const BUNDLE_FILENAME: &str = "rules-bundle.md";

const BUNDLE_HEADING: &str = "## Project Rules";
const BUNDLE_INTRO: &str = "The user has configured the following rules for this Unity project. Apply them consistently throughout the conversation:";

// endregion

// region: Public API

/// Returns the absolute path to the rules bundle file for the given
/// project root: `<root>/Library/MCPGameDeck/rules-bundle.md`. The
/// file may or may not exist; callers check at use time.
///
/// Used by `claude_supervisor::spawn` to set the
/// `MCP_GAME_DECK_RULES_BUNDLE_PATH` env var (task 3.3).
pub fn bundle_path(project_root: &Path) -> PathBuf {
    project_root.join(BUNDLE_SUBDIR).join(BUNDLE_FILENAME)
}

/// Recomposes the rules bundle from the current state of the rules
/// directory. Reads every `.md` under
/// `<project_root>/ProjectSettings/GameDeck/rules/`, filters by
/// `enabled: true` in frontmatter, sorts the survivors alphabetically
/// by file stem (lowercase compare), and either writes the composed
/// bundle or deletes the previous bundle file when zero rules are
/// enabled.
///
/// **Single source of truth** for the bundle on disk: called from
/// startup, from every watcher event, and from `restart_supervisor`.
///
/// # Errors
///
/// Surfaces IO errors from the bundle write (atomic tmp-then-rename)
/// or from the stale-bundle delete on the zero-enabled path. Read
/// failures on the rules dir or individual rule files are tolerated
/// — a missing rules dir is treated as zero enabled, and an
/// unreadable/unparseable rule file simply doesn't enter the bundle.
pub fn recompose(project_root: &Path) -> std::io::Result<()> {
    let enabled_rules = collect_enabled_rules(&project_root.join(RULES_SUBDIR));
    let bundle = bundle_path(project_root);

    if enabled_rules.is_empty() {
        match fs::remove_file(&bundle) {
            Ok(()) => Ok(()),
            Err(e) if e.kind() == ErrorKind::NotFound => Ok(()),
            Err(e) => Err(e),
        }
    } else {
        let composed = compose(&enabled_rules);
        ensure_dir(bundle.parent().expect("bundle_path always has a parent"))?;
        atomic_write(&bundle, composed.as_bytes())
    }
}

// endregion

// region: Internal — collection

struct EnabledRule {
    name: String,
    body: String,
}

/// Scans `dir` for `*.md`, parses frontmatter, returns the rules
/// where `enabled: true`, sorted alphabetically by stem (lowercase
/// compare). Unreadable files and parse failures are silently
/// dropped (defensive: a broken rule cannot accidentally activate).
fn collect_enabled_rules(dir: &Path) -> Vec<EnabledRule> {
    let Ok(entries) = fs::read_dir(dir) else {
        return Vec::new();
    };

    let mut rules: Vec<EnabledRule> = entries
        .filter_map(|res| res.ok())
        .filter_map(|entry| {
            let path = entry.path();
            if path.extension().and_then(|e| e.to_str()) != Some("md") {
                return None;
            }
            let name = path
                .file_stem()
                .and_then(|s| s.to_str())
                .map(str::to_string)?;
            let raw = fs::read_to_string(&path).ok()?;
            let (frontmatter, body) = parse_frontmatter(&raw);
            let enabled = frontmatter
                .get("enabled")
                .and_then(|v| v.as_bool())
                .unwrap_or(false);
            if !enabled {
                return None;
            }
            Some(EnabledRule { name, body })
        })
        .collect();

    rules.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
    rules
}

// endregion

// region: Internal — composition

/// Composes the bundle markdown string from a non-empty list of
/// enabled rules. Format:
///
/// ```text
/// ## Project Rules
///
/// <intro paragraph>
///
/// ### <name1>
///
/// <body1>
///
/// ---
///
/// ### <name2>
///
/// <body2>
/// ```
///
/// No trailing `---` after the last rule. Bodies are trimmed of
/// leading/trailing whitespace to avoid double-blank-line artifacts
/// when frontmatter is followed by a blank line in the source file.
/// File ends with a single trailing newline.
fn compose(rules: &[EnabledRule]) -> String {
    let parts: Vec<String> = rules
        .iter()
        .map(|r| format!("### {}\n\n{}", r.name, r.body.trim()))
        .collect();
    let joined = parts.join("\n\n---\n\n");
    format!("{BUNDLE_HEADING}\n\n{BUNDLE_INTRO}\n\n{joined}\n")
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;

    fn temp_project() -> PathBuf {
        let dir = std::env::temp_dir().join(format!(
            "mcp-game-deck-bundle-test-{}",
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_nanos()
        ));
        fs::create_dir_all(dir.join(RULES_SUBDIR)).unwrap();
        dir
    }

    fn write_rule_file(project: &Path, name: &str, enabled: bool, body: &str) {
        let path = project.join(RULES_SUBDIR).join(format!("{name}.md"));
        let content = format!("---\nenabled: {enabled}\n---\n\n{body}");
        fs::write(path, content).unwrap();
    }

    #[test]
    fn recompose_empty_dir_writes_no_bundle_and_returns_ok() {
        let project = temp_project();
        recompose(&project).unwrap();
        assert!(!bundle_path(&project).exists());
        fs::remove_dir_all(&project).ok();
    }

    #[test]
    fn recompose_single_enabled_rule_writes_bundle_with_no_trailing_separator() {
        let project = temp_project();
        write_rule_file(&project, "alpha", true, "Body of alpha rule.");
        recompose(&project).unwrap();

        let bundle = fs::read_to_string(bundle_path(&project)).unwrap();
        assert!(bundle.starts_with("## Project Rules\n\n"));
        assert!(bundle.contains("### alpha"));
        assert!(bundle.contains("Body of alpha rule."));
        assert!(
            !bundle.contains("\n---\n"),
            "no separator should appear with a single rule"
        );

        fs::remove_dir_all(&project).ok();
    }

    #[test]
    fn recompose_multiple_rules_sorted_alphabetically_with_separators() {
        let project = temp_project();
        write_rule_file(&project, "charlie", true, "C body");
        write_rule_file(&project, "alpha", true, "A body");
        write_rule_file(&project, "bravo", true, "B body");
        recompose(&project).unwrap();

        let bundle = fs::read_to_string(bundle_path(&project)).unwrap();
        let alpha_pos = bundle.find("### alpha").unwrap();
        let bravo_pos = bundle.find("### bravo").unwrap();
        let charlie_pos = bundle.find("### charlie").unwrap();
        assert!(alpha_pos < bravo_pos);
        assert!(bravo_pos < charlie_pos);
        assert_eq!(bundle.matches("\n---\n").count(), 2);
        assert!(!bundle.trim_end().ends_with("---"));

        fs::remove_dir_all(&project).ok();
    }

    #[test]
    fn recompose_ignores_disabled_rules() {
        let project = temp_project();
        write_rule_file(&project, "on", true, "Enabled body");
        write_rule_file(&project, "off", false, "Disabled body");
        recompose(&project).unwrap();

        let bundle = fs::read_to_string(bundle_path(&project)).unwrap();
        assert!(bundle.contains("Enabled body"));
        assert!(!bundle.contains("Disabled body"));
        assert!(!bundle.contains("### off"));

        fs::remove_dir_all(&project).ok();
    }

    #[test]
    fn recompose_treats_malformed_frontmatter_as_disabled() {
        let project = temp_project();
        let path = project.join(RULES_SUBDIR).join("bad.md");
        fs::write(&path, "---\n\t: : :\n---\nbad body\n").unwrap();
        recompose(&project).unwrap();

        assert!(!bundle_path(&project).exists());

        fs::remove_dir_all(&project).ok();
    }

    #[test]
    fn recompose_deletes_stale_bundle_when_zero_enabled() {
        let project = temp_project();
        write_rule_file(&project, "rule", true, "body");
        recompose(&project).unwrap();
        assert!(bundle_path(&project).exists());

        write_rule_file(&project, "rule", false, "body");
        recompose(&project).unwrap();

        assert!(!bundle_path(&project).exists());

        fs::remove_dir_all(&project).ok();
    }
}