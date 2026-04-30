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
/// Code plugin shipped with MCP Game Deck. Surfaced to the SDK
/// via `query()`'s `plugins` option, so all of its skills (under
/// `Plugin~/skills/<name>/SKILL.md`) and agents (under
/// `Plugin~/agents/<name>.md`) become auto-discoverable, namespaced
/// as `mcp-game-deck:<name>`. The plugin manifest at
/// `Plugin~/.claude-plugin/plugin.json` is what makes this directory
/// recognised as a plugin by Claude Code.
pub fn plugin_dir() -> PathBuf {
    package_root().join("Plugin~")
}

// endregion