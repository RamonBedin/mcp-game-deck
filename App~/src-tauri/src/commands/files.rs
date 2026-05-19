//! Project file index for the `@` picker
//!
//! Exposes `list_project_files` to the React frontend and owns the
//! `FilesIndex` Tauri-managed cache. The companion `files_watcher`
//! module invalidates the cache whenever a filesystem event fires
//! under the project root.
//!
//! **Walker semantics:**
//!
//! - `WalkDir::new(root).follow_links(false)` — symlink loops on
//!   Windows (Library/junction points etc.) and macOS would otherwise
//!   produce duplicates or fail the walk.
//! - `filter_entry` prunes excluded subtrees at the point they're
//!   visited so neither the walker nor the resulting `Vec` ever
//!   contains paths under `Library/`, `Temp/`, `obj/`, `Logs/`,
//!   `.vs/`, `.git/`, `node_modules/`, `dist/`, or any other
//!   dotted directory (with `.claude/` whitelisted by name).
//! - Paths are normalized to forward slashes after stripping the
//!   project root prefix so the React side gets stable
//!   `Assets/Scripts/Foo.cs` strings on every OS.
//!
//! **Cache strategy:**
//!
//! - `Arc<Mutex<Option<CachedIndex>>>` — a watcher invalidation sets
//!   the inner to `None`; a reader rebuilds if `None` or if the
//!   timestamp is older than `CACHE_TTL` (5 minutes).
//! - Both reader (`get_or_build`) and writer (`invalidate`) acquire
//!   the lock explicitly. There is no lock-free read path: races
//!   between a rebuild and an invalidation would otherwise serve a
//!   stale list right after a file change.
//! - The rebuild itself happens INSIDE the lock — this serializes
//!   concurrent rebuilds (the second caller sees the first's fresh
//!   result instead of redoing the walk) at the cost of holding the
//!   mutex for the duration of the walk. For Unity-project-sized
//!   indexes (~thousands of files) this is on the order of <1s on
//!   first hit, which is acceptable for v2.0.

use std::path::Path;
use std::sync::Mutex;
use std::time::{Duration, Instant};

use tauri::State;
use walkdir::{DirEntry, WalkDir};

use crate::project_root::try_resolve_project_root;
use crate::types::{FileIndexEntry, FileKind};

// region: Constants

const CACHE_TTL: Duration = Duration::from_secs(5 * 60);

const EXCLUDED_DIR_NAMES: &[&str] = &[
    "Library",
    "Temp",
    "obj",
    "Logs",
    ".vs",
    ".git",
    "node_modules",
    "dist",
];

const ALLOWED_DOTTED_DIR: &str = ".claude";

// endregion

// region: Cache

/// Snapshot of the project file index, paired with the wall-clock
/// instant it was built. Stored inside the `Mutex` on
/// `FilesIndex` — see module docblock for the locking discipline.
struct CachedIndex{
    entries: Vec<FileIndexEntry>,
    built_at: Instant,
}

/// Tauri-managed cache for the project file index.
///
/// Construct with `FilesIndex::new()` and register via `.manage()` in
/// `lib.rs`. The watcher calls `invalidate()` on FS events; the
/// `list_project_files` command calls `get_or_build()`.
pub struct FilesIndex {
    cache: Mutex<Option<CachedIndex>>,
}

impl FilesIndex {
    /// Builds an empty cache. No filesystem work happens until the
    /// first `get_or_build()` call.
    pub fn new() -> Self {
        Self {
            cache: Mutex::new(None),
        }
    }

    /// Marks the cache as stale. Next `get_or_build()` rebuilds.
    /// Cheap to call — only takes the lock long enough to write
    /// `None`.
    pub fn invalidate(&self) {
        let mut guard = match self.cache.lock() {
            Ok(g) => g,
            Err(poisoned) => poisoned.into_inner(),
        };
        *guard = None;
    }

    /// Returns the index, rebuilding if absent or older than
    /// `CACHE_TTL`. Holds the lock for the duration of the rebuild
    /// (see module docblock for the rationale).
    pub fn get_or_build(&self) -> Vec<FileIndexEntry> {
        let mut guard = match self.cache.lock() {
            Ok(g) => g,
            Err(poisoned) => poisoned.into_inner(),
        };

        let fresh = guard
            .as_ref()
            .map(|c| c.built_at.elapsed() < CACHE_TTL)
            .unwrap_or(false);

        if fresh {
            return guard
                .as_ref()
                .map(|c| c.entries.clone())
                .unwrap_or_default();
        }

        let entries = build_index();
        *guard = Some(CachedIndex {
            entries: entries.clone(),
            built_at: Instant::now(),
        });
        entries
    }
}

impl Default for FilesIndex {
    fn default() -> Self {
        Self::new()
    }
}

// endregion

// region: Walker

/// Returns `true` when `name` should be pruned from the walk.
///
/// Operates on a single basename — the caller is expected to pass
/// `entry.file_name()`, not a full path. Two rules, in order:
///
/// 1. Names in `EXCLUDED_DIR_NAMES` are unconditionally excluded.
/// 2. Any name starting with `.` is excluded EXCEPT `.claude` (which
///    is explicitly whitelisted so user-defined Claude commands /
///    skills under `.claude/` are surfaced in the picker).
fn should_exclude(name: &str) -> bool {
    if EXCLUDED_DIR_NAMES.contains(&name) {
        return true;
    }
    if name.starts_with('.') && name != ALLOWED_DOTTED_DIR {
        return true;
    }
    false
}

/// Walker `filter_entry` predicate. Skips the entry (and its subtree
/// when it's a directory) when its basename matches an exclusion
/// rule. Non-UTF8 basenames are kept — they're rare and falling back
/// to `to_string_lossy` for the path normalization step is harmless.
fn keep_entry(entry: &DirEntry) -> bool {
    let name = entry.file_name().to_string_lossy();
    !should_exclude(name.as_ref())
}

/// Normalizes an absolute path to a forward-slashed string relative
/// to `root`. Returns `None` when the entry is the root itself
/// (filtered out so the consumer doesn't see an empty path) or when
/// the path is somehow not under `root` (shouldn't happen with
/// `WalkDir::new(root)` but `strip_prefix` could fail on symlinked
/// segments).
fn normalize_path(root: &Path, entry_path: &Path) -> Option<String> {
    let rel = entry_path.strip_prefix(root).ok()?;
    let rel_str = rel.to_string_lossy();
    if rel_str.is_empty() {
        return None;
    }
    Some(rel_str.replace('\\', "/"))
}

/// Walks the project root and collects every non-excluded entry.
/// Returns an empty vec when no project root resolves.
fn build_index() -> Vec<FileIndexEntry> {
    let Some(root) = try_resolve_project_root() else {
        return Vec::new();
    };

    walk_at(&root)
}

/// Same as [`build_index`] but takes an explicit root — separated so
/// the unit tests can drive a tmp dir without setting env vars.
fn walk_at(root: &Path) -> Vec<FileIndexEntry> {
    let mut out = Vec::new();
    let walker = WalkDir::new(root)
        .follow_links(false)
        .into_iter()
        .filter_entry(keep_entry);

    for entry in walker.flatten() {
        let entry_path = entry.path();
        let Some(path) = normalize_path(root, entry_path) else {
            continue;
        };
        let file_type = entry.file_type();
        let kind = if file_type.is_dir() {
            FileKind::Directory
        } else if file_type.is_file() {
            FileKind::File
        } else {
            continue;
        };
        out.push(FileIndexEntry { path, kind });
    }
    out
}

// endregion

// region: Command

/// Lists every non-excluded path under the active Unity project.
///
/// Returns an empty vector when no `UNITY_PROJECT_PATH` resolves —
/// the `@` picker treats that as "Files section empty" rather than as
/// an error, mirroring the empty-state pattern used by `list_plans`.
///
/// Backed by `FilesIndex`'s 5-minute cache; the files watcher
/// invalidates it on every filesystem event so an external file
/// create surfaces on the next call.
#[tauri::command]
pub fn list_project_files(state: State<'_, FilesIndex>) -> Vec<FileIndexEntry> {
    state.get_or_build()
}

// endregion

#[cfg(test)]
mod tests {
    use super::*;
    use std::fs;
    use std::path::PathBuf;

    fn make_tmpdir(label: &str) -> PathBuf {
        let mut dir = std::env::temp_dir();
        let unique = format!(
            "mcp-files-{}-{}-{}",
            label,
            std::process::id(),
            std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .map(|d| d.as_nanos())
                .unwrap_or(0)
        );
        dir.push(unique);
        fs::create_dir_all(&dir).expect("create tmp dir");
        dir
    }

    #[test]
    fn should_exclude_listed_dirs() {
        for name in EXCLUDED_DIR_NAMES {
            assert!(should_exclude(name), "expected {name} to be excluded");
        }
    }

    #[test]
    fn should_exclude_dotted_dirs_except_claude() {
        assert!(should_exclude(".hidden"));
        assert!(should_exclude(".idea"));
        assert!(!should_exclude(".claude"));
    }

    #[test]
    fn should_exclude_does_not_match_regular_names() {
        assert!(!should_exclude("Assets"));
        assert!(!should_exclude("Scripts"));
        assert!(!should_exclude("Library.cs"));
        assert!(!should_exclude("README.md"));
    }

    #[test]
    fn normalize_path_strips_root_and_normalizes_slashes() {
        let root = PathBuf::from("/tmp/proj");
        let entry = PathBuf::from("/tmp/proj/Assets/Scripts/Foo.cs");
        let normalized = normalize_path(&root, &entry).expect("relative path");
        assert_eq!(normalized, "Assets/Scripts/Foo.cs");
    }

    #[test]
    fn normalize_path_returns_none_for_root_itself() {
        let root = PathBuf::from("/tmp/proj");
        assert!(normalize_path(&root, &root).is_none());
    }

    #[test]
    fn walk_at_collects_files_and_dirs_excludes_pruned_subtrees() {
        let root = make_tmpdir("walk");

        fs::create_dir_all(root.join("Assets/Scripts")).unwrap();
        fs::create_dir_all(root.join("Library/PackageCache")).unwrap();
        fs::create_dir_all(root.join(".claude")).unwrap();
        fs::create_dir_all(root.join(".idea")).unwrap();
        fs::write(root.join("Assets/Scripts/Foo.cs"), "// foo").unwrap();
        fs::write(root.join("Library/PackageCache/index"), "noise").unwrap();
        fs::write(root.join(".claude/agents.md"), "claude").unwrap();
        fs::write(root.join(".idea/workspace.xml"), "ignored").unwrap();
        fs::write(root.join("README.md"), "hello").unwrap();

        let entries = walk_at(&root);
        let paths: Vec<&str> = entries.iter().map(|e| e.path.as_str()).collect();

        assert!(paths.contains(&"Assets"), "got {paths:?}");
        assert!(paths.contains(&"Assets/Scripts"));
        assert!(paths.contains(&"Assets/Scripts/Foo.cs"));
        assert!(paths.contains(&"README.md"));
        assert!(paths.contains(&".claude"));
        assert!(paths.contains(&".claude/agents.md"));

        assert!(!paths.iter().any(|p| p.starts_with("Library")));
        assert!(!paths.iter().any(|p| p.starts_with(".idea")));

        let dirs: Vec<&str> = entries
            .iter()
            .filter(|e| e.kind == FileKind::Directory)
            .map(|e| e.path.as_str())
            .collect();
        assert!(dirs.contains(&"Assets"));
        assert!(dirs.contains(&".claude"));

        fs::remove_dir_all(&root).ok();
    }

    #[test]
    fn files_index_invalidate_then_get_returns_fresh_via_lock() {
        let idx = FilesIndex::new();
        let _ = idx.get_or_build();
        {
            let guard = idx.cache.lock().unwrap();
            assert!(guard.is_some(), "expected cache populated after first call");
        }
        idx.invalidate();
        {
            let guard = idx.cache.lock().unwrap();
            assert!(guard.is_none(), "expected cache cleared after invalidate");
        }
    }
}