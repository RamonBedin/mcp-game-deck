//! Recent commands cache (B.08 backend ask).
//!
//! Tracks which slash commands the user has launched recently so the
//! v2.0 `ChatLaunchpad` can surface a "Recent" section without polling
//! Claude Code's session history.
//!
//! Storage: a JSON file at
//! `<project>/ProjectSettings/GameDeck/recent-commands.json`. The
//! schema is intentionally trivial — a single ordered list of command
//! names, capped at `MAX_RECENT`. `track` moves an existing entry to
//! the front and appends new ones; `list` reads the current array.
//!
//! Concurrency: every call resolves the project root, reads the file
//! fresh, mutates, writes atomically. Two near-simultaneous `track`
//! calls could lose one entry under last-writer-wins; acceptable
//! since the dataset is "what did you type recently."

use std::fs;
use std::io::ErrorKind;
use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::markdown_doc::{atomic_write, ensure_dir};
use crate::project_root::try_resolve_project_root;
use crate::types::AppError;

// region: Constants

const CACHE_SUBDIR: &str = "ProjectSettings/GameDeck";
const CACHE_FILENAME: &str = "recent-commands.json";
const MAX_RECENT: usize = 12;

// endregion

// region: Helpers

/// Resolves the on-disk path for the knowledge cache file.
///
/// Anchors the path under the project root reported by
/// [`try_resolve_project_root`], joining the cache subdirectory and filename
/// constants so all cache I/O routes through a single canonical location.
///
/// # Returns
///
/// `Some(path)` when the project root can be resolved, or `None` when the
/// app is running outside a recognised project (in which case caching is
/// skipped).
fn cache_path() -> Option<PathBuf> {
    try_resolve_project_root().map(|root| root.join(CACHE_SUBDIR).join(CACHE_FILENAME))
}

/// On-disk representation of the knowledge cache file.
///
/// Persists the list of catalog command identifiers between sessions so the
/// catalog can be rehydrated without re-scanning the knowledge directory on
/// every launch. Serialised as JSON via [`serde`].
#[derive(Debug, Serialize, Deserialize, Default)]
struct CacheFile {
    commands: Vec<String>,
}

/// Reads + parses the cache. Returns an empty cache when:
///   - no project root resolves
///   - the file doesn't exist
///   - the JSON is malformed (defensive — never let a corrupt cache
///     bubble an error to the UI)
fn read_cache() -> CacheFile {
    let Some(path) = cache_path() else {
        return CacheFile::default();
    };

    match fs::read_to_string(&path) {
        Ok(text) => serde_json::from_str(&text).unwrap_or_default(),
        Err(_) => CacheFile::default(),
    }
}

/// Persists the recent-commands cache to disk atomically.
///
/// Resolves the cache path via [`cache_path`], ensures the parent directory
/// exists, serialises `cache` as pretty-printed JSON, and writes it through
/// [`atomic_write`] so a crash mid-write cannot leave the cache half-updated.
///
/// # Arguments
///
/// * `cache` - Snapshot of the recent-commands cache to persist.
///
/// # Errors
///
/// * [`AppError::Internal`] when no project is active, the path has no
///   parent, the cache cannot be created, or serialisation fails.
/// * [`AppError::PermissionDenied`] when the filesystem rejects the write
///   due to insufficient permissions.
fn write_cache(cache: &CacheFile) -> Result<(), AppError> {
    let Some(path) = cache_path() else {
        return Err(AppError::Internal(
            "No active Unity project — recent commands cache unavailable.".into(),
        ));
    };

    let parent = path
        .parent()
        .ok_or_else(|| AppError::Internal("Recent commands path missing parent".into()))?;
    ensure_dir(parent).map_err(|e| AppError::Internal(format!("ensure_dir: {e}")))?;

    let body = serde_json::to_vec_pretty(cache)
        .map_err(|e| AppError::Internal(format!("Cache serialize: {e}")))?;

    atomic_write(&path, &body).map_err(|e| match e.kind() {
        ErrorKind::PermissionDenied => AppError::PermissionDenied(format!(
            "Cannot write recent-commands.json at '{}'",
            path.display()
        )),
        _ => AppError::Internal(format!("atomic_write: {e}")),
    })
}

/// Normalises a raw command name into a canonical cache key.
///
/// Trims whitespace and any leading `/`, then rejects entries that are empty
/// or contain internal whitespace — commands are single tokens, so anything
/// that looks like a free-form prompt accidentally passed in is discarded.
///
/// # Arguments
///
/// * `name` - Raw command name as supplied by the caller.
///
/// # Returns
///
/// `Some(canonical)` when `name` is a valid single-token command, or `None`
/// when it should be rejected.
fn sanitize(name: &str) -> Option<String> {
    let trimmed = name.trim().trim_start_matches('/');

    if trimmed.is_empty() {
        return None;
    }

    if trimmed.contains([' ', '\t', '\n']) {
        return None;
    }

    Some(trimmed.to_string())
}

// endregion

// region: Commands

/// Returns the cached MRU list of slash commands, most-recent first.
/// Empty vec when no project is active or the cache hasn't been
/// touched yet — both are normal pre-onboarding states.
#[tauri::command]
pub fn list_recent_commands() -> Vec<String> {
    read_cache().commands
}

/// Records that the user just launched `command`. Moves an existing
/// entry to the front; appends new ones; truncates to `MAX_RECENT`.
/// Silently no-ops when the input is empty or contains whitespace
/// (defensive: bad inputs shouldn't pollute the MRU).
///
/// # Errors
///
/// `AppError::Internal` only on real IO failures during write. The
/// "no project root" path returns `Internal`; everything else stays
/// best-effort.
#[tauri::command]
pub fn track_recent_command(command: String) -> Result<(), AppError> {
    let Some(name) = sanitize(&command) else {
        return Ok(());
    };

    let mut cache = read_cache();
    cache.commands.retain(|c| c != &name);
    cache.commands.insert(0, name);
    cache.commands.truncate(MAX_RECENT);

    write_cache(&cache)
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn sanitize_strips_leading_slash() {
        assert_eq!(sanitize("/plan-execute").as_deref(), Some("plan-execute"));
    }

    #[test]
    fn sanitize_trims_whitespace() {
        assert_eq!(sanitize("  /foo  ").as_deref(), Some("foo"));
    }

    #[test]
    fn sanitize_rejects_empty() {
        assert!(sanitize("").is_none());
        assert!(sanitize("/").is_none());
    }

    #[test]
    fn sanitize_rejects_with_spaces() {
        assert!(sanitize("/plan-execute setup").is_none());
    }
}