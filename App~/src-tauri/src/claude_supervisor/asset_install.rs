//! Pre-processes `<package>/Plugin~/` into a per-project runtime
//! copy at `<unity-project>/Library/GameDeck/plugin/` with the
//! `{{KB_PATH}}` placeholder substituted by the absolute path to
//! `<package>/KnowledgeBase~/`.
//!
//! `Plugin~/` is package-shipped (lives in the Unity PackageCache,
//! conceptually read-only), so substitution must happen on a copy.
//! The processed copy is what `MCP_GAME_DECK_PLUGIN_DIR` points at
//! after task 3.2 — `mod.rs::spawn` passes the resolved path into
//! `spawn::spawn_node_child` instead of the source `Plugin~/`.
//!
//! No manifest tracking, no version check, no refuse-to-overwrite.
//! Mirror copy runs every spawn; idempotency comes from overwriting
//! the destination wholesale. Manifest tracking lands in task 3.3.

use std::path::{Path, PathBuf};

use tauri::AppHandle;
use tokio::fs;

use crate::claude_supervisor::paths;

// region: AssetInstallError

/// Failure modes for `install_plugin`. Each variant carries the
/// path that failed plus the underlying I/O error so the `spawn()`
/// caller can build a useful soft-warn for React.
#[derive(Debug)]
pub enum AssetInstallError {
    CreateDir(PathBuf, std::io::Error),
    Walk(PathBuf, std::io::Error),
    ReadFile(PathBuf, std::io::Error),
    WriteFile(PathBuf, std::io::Error),
    KbPathInvalid(PathBuf),
    StripPrefix(PathBuf),
}

impl std::fmt::Display for AssetInstallError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            AssetInstallError::CreateDir(p, e) => {
                write!(f, "create_dir_all failed for {}: {e}", p.display())
            }
            AssetInstallError::Walk(p, e) => {
                write!(f, "directory walk failed at {}: {e}", p.display())
            }
            AssetInstallError::ReadFile(p, e) => {
                write!(f, "read_to_string failed for {}: {e}", p.display())
            }
            AssetInstallError::WriteFile(p, e) => {
                write!(f, "write failed for {}: {e}", p.display())
            }
            AssetInstallError::KbPathInvalid(p) => {
                write!(f, "KnowledgeBase~ path is not valid UTF-8: {}", p.display())
            }
            AssetInstallError::StripPrefix(p) => {
                write!(f, "could not derive relative path for {}", p.display())
            }
        }
    }
}

impl std::error::Error for AssetInstallError {}

// endregion

// region: Public surface

/// Mirror-copies `<package>/Plugin~/` to
/// `<project>/Library/GameDeck/plugin/`, substituting `{{KB_PATH}}`
/// in every `.md` file with the absolute path to
/// `<package>/KnowledgeBase~/`. Non-`.md` assets are byte-copied
/// verbatim. Existing destination files are overwritten.
///
/// Backslashes in the resolved KB path are normalized to forward
/// slashes — Markdown / Claude Code consume forward slashes more
/// reliably across platforms.
///
/// # Arguments
///
/// * `project_path` - Pre-validated UNITY_PROJECT_PATH.
/// * `_app` - Reserved for future progress events; today the
///   function is fast enough that no progress feedback is needed.
///
/// # Returns
///
/// Absolute path to the processed plugin directory on success — the
/// caller wires this into `MCP_GAME_DECK_PLUGIN_DIR`.
///
/// # Errors
///
/// `AssetInstallError` variants distinguish which step failed (dir
/// creation, recursive walk, file read, file write) and on which path.
pub async fn install_plugin(
    project_path: &str,
    _app: &AppHandle,
) -> Result<PathBuf, AssetInstallError> {
    let source_root = paths::plugin_dir();
    let dest_root = paths::installed_plugin_dir(project_path);
    let kb_path = paths::knowledge_base_dir();

    let kb_replacement = match kb_path.to_str() {
        Some(s) => s.replace('\\', "/"),
        None => return Err(AssetInstallError::KbPathInvalid(kb_path)),
    };

    fs::create_dir_all(&dest_root)
        .await
        .map_err(|e| AssetInstallError::CreateDir(dest_root.clone(), e))?;

    copy_tree(&source_root, &dest_root, &kb_replacement).await?;

    eprintln!(
        "[asset-install] processed {} → {}",
        source_root.display(),
        dest_root.display()
    );
    Ok(dest_root)
}

// endregion

// region: Internal — recursive walk

/// Iterative directory mirror. Uses an explicit stack to avoid the
/// `Pin<Box<dyn Future>>` boilerplate that recursive `async fn`
/// requires in Rust today.
async fn copy_tree(
    source_root: &Path,
    dest_root: &Path,
    kb_replacement: &str,
) -> Result<(), AssetInstallError> {
    let mut stack: Vec<PathBuf> = vec![source_root.to_path_buf()];

    while let Some(current) = stack.pop() {
        let mut entries = fs::read_dir(&current)
            .await
            .map_err(|e| AssetInstallError::Walk(current.clone(), e))?;

        loop {
            let next = entries
                .next_entry()
                .await
                .map_err(|e| AssetInstallError::Walk(current.clone(), e))?;
            let entry = match next {
                Some(e) => e,
                None => break,
            };

            let entry_path = entry.path();
            let rel = entry_path
                .strip_prefix(source_root)
                .map_err(|_| AssetInstallError::StripPrefix(entry_path.clone()))?;
            let dest_path = dest_root.join(rel);

            let file_type = entry
                .file_type()
                .await
                .map_err(|e| AssetInstallError::Walk(entry_path.clone(), e))?;

            if file_type.is_dir() {
                fs::create_dir_all(&dest_path)
                    .await
                    .map_err(|e| AssetInstallError::CreateDir(dest_path.clone(), e))?;
                stack.push(entry_path);
            } else if file_type.is_file() {
                copy_file(&entry_path, &dest_path, kb_replacement).await?;
            }
        }
    }

    Ok(())
}

/// Copies a single file. `.md` files get `{{KB_PATH}}` substituted
/// with `kb_replacement`; everything else is byte-copied verbatim.
async fn copy_file(
    source: &Path,
    dest: &Path,
    kb_replacement: &str,
) -> Result<(), AssetInstallError> {
    let is_markdown = source
        .extension()
        .and_then(|s| s.to_str())
        .map(|s| s.eq_ignore_ascii_case("md"))
        .unwrap_or(false);

    if let Some(parent) = dest.parent() {
        fs::create_dir_all(parent)
            .await
            .map_err(|e| AssetInstallError::CreateDir(parent.to_path_buf(), e))?;
    }

    if is_markdown {
        let content = fs::read_to_string(source)
            .await
            .map_err(|e| AssetInstallError::ReadFile(source.to_path_buf(), e))?;
        let resolved = content.replace("{{KB_PATH}}", kb_replacement);
        fs::write(dest, resolved)
            .await
            .map_err(|e| AssetInstallError::WriteFile(dest.to_path_buf(), e))?;
    } else {
        fs::copy(source, dest)
            .await
            .map_err(|e| AssetInstallError::WriteFile(dest.to_path_buf(), e))?;
    }
    Ok(())
}

// endregion