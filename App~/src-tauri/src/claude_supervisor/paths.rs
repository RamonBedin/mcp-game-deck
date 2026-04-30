//! Path resolution for the Tauri-managed Node runtime.
//!
//! Centralizes the dev-mode anchor used by `sdk_install.rs`. Production
//! packaging (F02 task 7.x) replaces this with an `app_local_data_dir`
//! resolution. `install_check.rs` currently has its own inline copy of
//! the same calculation; that file will adopt this helper when 7.x
//! revisits the anchor decision.

use std::path::PathBuf;

// region: Public surface

/// Absolute path to the Tauri-managed Node runtime directory.
///
/// Anchored at `CARGO_MANIFEST_DIR` (= `App~/src-tauri/` at compile
/// time), walked up one level to `App~/`, then joined with `runtime/`.
/// Resolves to `<repo>/App~/runtime/` in dev.
///
/// # Panics
///
/// Panics if `CARGO_MANIFEST_DIR` has no parent — that would mean the
/// crate is at filesystem root, which never happens in any realistic
/// build setup.
pub fn runtime_dir() -> PathBuf {
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest
        .parent()
        .expect("CARGO_MANIFEST_DIR has no parent")
        .join("runtime")
}

/// Path to the Tauri-managed runtime's own `package.json`. Written by
/// `sdk_install.rs` on first launch when missing.
pub fn runtime_package_json() -> PathBuf {
    runtime_dir().join("package.json")
}

/// Path to the SDK package's `package.json` once installed by npm.
/// Used as the canonical "is the SDK present?" probe.
pub fn sdk_package_json() -> PathBuf {
    runtime_dir()
        .join("node_modules")
        .join("@anthropic-ai")
        .join("claude-agent-sdk")
        .join("package.json")
}

/// Path to the Node entry script written by `runtime_setup`. The
/// supervisor spawns `node <this path>` to bridge stdin/stdout to
/// the Agent SDK.
pub fn sdk_entry_script() -> PathBuf {
    runtime_dir().join("sdk-entry.js")
}

/// Absolute path to the MCP Game Deck Unity package root.
///
/// Walks up two levels from `CARGO_MANIFEST_DIR` (= `App~/src-tauri/`)
/// → `App~/` → `<package>/`. Resolves to the repo root in dev.
///
/// `CARGO_MANIFEST_DIR` is a compile-time
/// anchor that points at the source tree even after Tauri bundles
/// the binary into an MSI. Production builds need a different
/// resolution (e.g., walking up from `current_exe()` or asset-side
/// embedding). For dev/preview, this is correct.
pub fn package_root() -> PathBuf {
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest
        .parent()
        .and_then(|p| p.parent())
        .expect("CARGO_MANIFEST_DIR has no grandparent")
        .to_path_buf()
}

/// Path to the compiled MCP proxy script that bridges Claude Code's
/// MCP transport to the C# MCP Server in Unity. Built from
/// `<package>/Server~/` via `npm run build`. Skipped silently by
/// the supervisor when missing — the warning surfaces as an
/// `AgentMessage::Error` to React.
pub fn mcp_proxy_script() -> PathBuf {
    package_root()
        .join("Server~")
        .join("dist")
        .join("mcp-proxy.js")
}

/// Path to the package's `Plugin~/` directory — the bundled Claude
/// Code plugin shipped with MCP Game Deck. This is the **source**
/// for `asset_install::install_plugin`, which mirrors it into a
/// processed per-project copy (see `installed_plugin_dir`). Plugin~/
/// is package-shipped (lives in the Unity PackageCache, conceptually
/// read-only), so substitution must happen on a copy.
pub fn plugin_dir() -> PathBuf {
    package_root().join("Plugin~")
}

/// Path to the package's `KnowledgeBase~/` directory. This is what
/// `{{KB_PATH}}` substitutes to inside processed `Plugin~/` markdown
/// files — agents/skills then `Read` documents at this absolute
/// path. The KB itself is not copied; only the placeholder gets
/// resolved during `asset_install::install_plugin`.
pub fn knowledge_base_dir() -> PathBuf {
    package_root().join("KnowledgeBase~")
}

/// Path to the per-project processed plugin directory written by
/// `asset_install::install_plugin`. Lives under `Library/`
/// (Unity convention; gitignored by default) so it's tied to the
/// user's Unity project rather than the package or the Tauri
/// runtime. `MCP_GAME_DECK_PLUGIN_DIR` points here after task 3.2 —
/// not at `plugin_dir()` directly.
pub fn installed_plugin_dir(project_path: &str) -> PathBuf {
    std::path::Path::new(project_path)
        .join("Library")
        .join("GameDeck")
        .join("plugin")
}

// endregion