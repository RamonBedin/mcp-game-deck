//! Knowledge docs Tauri commands
//!
//! Exposes the 16 markdown documents bundled at `Plugin~/knowledge/` —
//! the same files agents and skills already reference via
//! `${CLAUDE_PLUGIN_ROOT}/knowledge/<file>.md`. The React Library route
//! reads them through `list_knowledge_docs()` (metadata only) and
//! `read_knowledge_doc(id)` (body).
//!
//! Doc id == filename stem (e.g. `01-unity-project-architecture`).
//! `title` is derived from the first H1 heading when present; falls
//! back to the stem with leading numeric prefix stripped and dashes
//! turned into spaces.

use std::fs;
use std::io::ErrorKind;
use std::path::PathBuf;

use serde::Serialize;

use crate::claude_supervisor::paths::plugin_dir;
use crate::types::AppError;

const KNOWLEDGE_SUBDIR: &str = "knowledge";

// region: Types

/// Metadata describing a single knowledge doc. Mirrors the React
/// `KnowledgeDocMeta` interface; `id` is the stable filename stem.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct KnowledgeDocMeta {
    pub id: String,
    pub num: String,
    pub title: String,
    pub word_count: u32,
}

/// Full doc — metadata plus body. `body` is the file's raw markdown
/// without modification (no YAML stripping; knowledge docs don't have
/// frontmatter today).
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct KnowledgeDoc {
    pub id: String,
    pub num: String,
    pub title: String,
    pub word_count: u32,
    pub body: String,
}

// endregion

// region: Helpers

/// Returns the absolute path to the knowledge directory under the plugin's
/// install root.
///
/// Composes [`plugin_dir`] with [`KNOWLEDGE_SUBDIR`] so all knowledge-related
/// I/O routes through a single canonical location.
///
/// # Returns
///
/// The resolved knowledge directory path.
fn knowledge_dir() -> PathBuf {
    plugin_dir().join(KNOWLEDGE_SUBDIR)
}

/// Extracts the numeric prefix from a stem like `01-foo-bar`.
/// Returns the prefix (digits only) and the remainder. When the stem
/// doesn't start with `\d+-`, returns ("", full stem).
fn split_num_prefix(stem: &str) -> (String, &str) {
    let mut chars = stem.char_indices();
    let mut last_digit_end = 0;

    for (idx, c) in chars.by_ref() {
        if c.is_ascii_digit() {
            last_digit_end = idx + c.len_utf8();
        } else {
            break;
        }
    }

    if last_digit_end == 0 {
        return (String::new(), stem);
    }

    if stem.as_bytes().get(last_digit_end) == Some(&b'-') {
        let num = stem[..last_digit_end].to_string();
        let rest = &stem[last_digit_end + 1..];
        (num, rest)
    } else {
        (String::new(), stem)
    }
}

/// First H1 heading text from the body, when present. Tolerates
/// leading whitespace and any spacing after `#`.
fn extract_first_h1(body: &str) -> Option<String> {
    for line in body.lines() {
        let trimmed = line.trim_start();

        if let Some(rest) = trimmed.strip_prefix("# ") {
            let title = rest.trim();

            if !title.is_empty() {
                return Some(title.to_string());
            }
        }
    }

    None
}

/// Synthesizes a title from a stem when the body has no H1. Turns
/// `unity-project-architecture` into `Unity Project Architecture`.
fn title_from_stem(stem_without_num: &str) -> String {
    stem_without_num
        .split('-')
        .map(|word| {
            let mut chars = word.chars();

            match chars.next() {
                Some(c) => c.to_uppercase().chain(chars).collect::<String>(),
                None => String::new(),
            }
        })
        .collect::<Vec<_>>()
        .join(" ")
}

/// Counts the whitespace-separated words in `text`.
///
/// Uses [`str::split_whitespace`], so consecutive whitespace collapses and
/// leading/trailing whitespace is ignored. The result is cast to `u32`, which
/// is sufficient for any reasonable knowledge-doc body.
///
/// # Arguments
///
/// * `text` - The text to scan.
///
/// # Returns
///
/// The number of whitespace-separated words found in `text`.
fn word_count(text: &str) -> u32 {
    text.split_whitespace().count() as u32
}

/// Builds a [`KnowledgeDocMeta`] entry from a knowledge file's path and body.
///
/// Derives the document id from the file stem, splits any leading numeric
/// prefix into a separate ordering field, and resolves the title from the
/// body's first H1 — falling back to a humanised version of the stem when
/// no H1 is present.
///
/// # Arguments
///
/// * `path` - Filesystem path to the knowledge document.
/// * `body` - Full text content of the document.
///
/// # Returns
///
/// `Some(meta)` when the path has a valid UTF-8 file stem, or `None` when the
/// stem is missing or not representable as UTF-8.
fn meta_from_file(path: &std::path::Path, body: &str) -> Option<KnowledgeDocMeta> {
    let stem = path.file_stem()?.to_str()?.to_string();
    let (num, rest) = split_num_prefix(&stem);
    let title = extract_first_h1(body).unwrap_or_else(|| title_from_stem(rest));

    Some(KnowledgeDocMeta {
        id: stem,
        num,
        title,
        word_count: word_count(body),
    })
}

// endregion

// region: Commands

/// Lists every knowledge doc currently bundled at `Plugin~/knowledge/`.
/// Returns an empty vec when the directory doesn't exist (e.g. ran
/// outside the source repo without `Plugin~/` shipped).
///
/// Sorted by `id` (filename stem) ascending, so the numeric `NN-`
/// prefix produces a natural reading order.
///
/// # Errors
///
/// `AppError::Internal` only on hard IO failures other than
/// "directory missing" (which is treated as zero docs).
#[tauri::command]
pub fn list_knowledge_docs() -> Result<Vec<KnowledgeDocMeta>, AppError> {
    let dir = knowledge_dir();

    let entries = match fs::read_dir(&dir) {
        Ok(it) => it,
        Err(e) if e.kind() == ErrorKind::NotFound => return Ok(Vec::new()),
        Err(e) => {
            return Err(AppError::Internal(format!(
                "Failed to list '{}': {}",
                dir.display(),
                e
            )))
        }
    };

    let mut docs: Vec<KnowledgeDocMeta> = entries
        .filter_map(|res| res.ok())
        .filter_map(|entry| {
            let path = entry.path();

            if path.extension().and_then(|e| e.to_str()) != Some("md") {
                return None;
            }

            let body = fs::read_to_string(&path).ok()?;
            meta_from_file(&path, &body)
        })
        .collect();

    docs.sort_by(|a, b| a.id.cmp(&b.id));

    Ok(docs)
}

/// Reads one knowledge doc by id (the filename stem). Returns the
/// full file content as `body`, plus the same derived metadata
/// `list_knowledge_docs` surfaces, so the reader can render header
/// + body in one round-trip.
///
/// # Errors
///
/// `AppError::FileNotFound` when the doc id doesn't resolve to a
/// `.md` file. `AppError::Internal` on other IO failures.
#[tauri::command]
pub fn read_knowledge_doc(id: String) -> Result<KnowledgeDoc, AppError> {
    if id.is_empty() || id.contains(['/', '\\']) || id.contains("..") {
        return Err(AppError::InvalidInput(format!(
            "Invalid knowledge doc id: '{id}'"
        )));
    }

    let path = knowledge_dir().join(format!("{id}.md"));

    let body = fs::read_to_string(&path).map_err(|e| match e.kind() {
        ErrorKind::NotFound => AppError::FileNotFound(format!("Knowledge doc '{id}' not found")),
        _ => AppError::Internal(format!(
            "Failed to read '{}': {}",
            path.display(),
            e
        )),
    })?;

    let meta = meta_from_file(&path, &body).ok_or_else(|| {
        AppError::Internal(format!("Failed to derive metadata for '{}'", path.display()))
    })?;

    Ok(KnowledgeDoc {
        id: meta.id,
        num: meta.num,
        title: meta.title,
        word_count: meta.word_count,
        body,
    })
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn split_num_prefix_strips_leading_digits() {
        let (num, rest) = split_num_prefix("01-unity-project-architecture");
        assert_eq!(num, "01");
        assert_eq!(rest, "unity-project-architecture");
    }

    #[test]
    fn split_num_prefix_with_no_dash_returns_full_stem() {
        let (num, rest) = split_num_prefix("12foo");
        assert_eq!(num, "");
        assert_eq!(rest, "12foo");
    }

    #[test]
    fn split_num_prefix_with_no_digits_returns_full_stem() {
        let (num, rest) = split_num_prefix("getting-started");
        assert_eq!(num, "");
        assert_eq!(rest, "getting-started");
    }

    #[test]
    fn extract_first_h1_finds_leading_heading() {
        let body = "# The Title\n\nbody";
        assert_eq!(extract_first_h1(body), Some("The Title".to_string()));
    }

    #[test]
    fn extract_first_h1_returns_none_when_only_h2() {
        let body = "## Sub heading\nbody";
        assert_eq!(extract_first_h1(body), None);
    }

    #[test]
    fn title_from_stem_capitalizes_each_word() {
        assert_eq!(
            title_from_stem("unity-project-architecture"),
            "Unity Project Architecture"
        );
    }
}