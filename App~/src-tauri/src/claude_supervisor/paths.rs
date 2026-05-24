//! Path resolution for the Tauri-managed Node runtime + the Unity
//! package surface (Plugin~/) + the Tauri-bundled MCP proxy script.
//!
//! Three resolution channels:
//!
//! - **`MCP_GAME_DECK_PACKAGE_ROOT`** env var (populated by Unity's
//!   `PinLauncher` via `PackageInfo.resolvedPath`) — source of
//!   `Plugin~/`. Falls back to a `CARGO_MANIFEST_DIR` walk-up in dev.
//! - **`MCP_GAME_DECK_RUNTIME_DIR`** env var (also populated by
//!   `PinLauncher`) — writable per-version dir under the OS user-data
//!   tree, owns the npm SDK install and the generated `sdk-entry.js`.
//!   Falls back to `App~/runtime/` in dev.
//! - **Tauri `BaseDirectory::Resource`** — owns `mcp-proxy.js`, bundled
//!   into the binary at release time by the `build-proxy.mjs` script
//!   chained via `beforeBuildCommand`. No env var, no dev fallback —
//!   `cargo tauri dev` resolves resources to the source tree.

use std::path::PathBuf;

use tauri::path::BaseDirectory;
use tauri::{AppHandle, Manager};

// region: Public surface

/// Absolute path to the Node runtime directory — where the npm-installed
/// SDK and the generated `sdk-entry.js` live. Must be writable.
///
/// Primary source: `MCP_GAME_DECK_RUNTIME_DIR` env var, set by Unity's
/// `PinLauncher` to a per-version path under the OS user-data directory
/// (e.g. `%APPDATA%\MCPGameDeck\runtime\<version>\` on Windows). This
/// keeps the runtime outside the UPM package tree (which may be
/// read-only when installed via PackageCache).
///
/// Dev fallback: walks up from `CARGO_MANIFEST_DIR` to `App~/runtime/`.
/// Triggered when the env var is unset or empty — typical when running
/// `cargo tauri dev` directly without launching through the Unity pin.
pub fn runtime_dir() -> PathBuf {
    if let Ok(v) = std::env::var("MCP_GAME_DECK_RUNTIME_DIR") {
        if !v.is_empty() {
            return PathBuf::from(v);
        }
    }
    eprintln!("[paths] MCP_GAME_DECK_RUNTIME_DIR not set; falling back to App~/runtime/ (dev mode)");
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
/// Primary source: `MCP_GAME_DECK_PACKAGE_ROOT` env var, set by Unity's
/// `PinLauncher` to the resolved package path (via
/// `PackageInfo.resolvedPath`). Works regardless of how the package
/// was installed — UPM git URL, `file:` reference, embedded, or
/// PackageCache snapshot.
///
/// Dev fallback: walks up from `CARGO_MANIFEST_DIR` to the repo root.
/// Triggered when the env var is unset or empty — typical when running
/// `cargo tauri dev` directly without launching through the Unity pin.
pub fn package_root() -> PathBuf {
    if let Ok(v) = std::env::var("MCP_GAME_DECK_PACKAGE_ROOT") {
        if !v.is_empty() {
            return PathBuf::from(v);
        }
    }
    eprintln!("[paths] MCP_GAME_DECK_PACKAGE_ROOT not set; falling back to CARGO_MANIFEST_DIR walk-up (dev mode)");
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    manifest
        .parent()
        .and_then(|p| p.parent())
        .expect("CARGO_MANIFEST_DIR has no grandparent")
        .to_path_buf()
}

/// Path to the compiled MCP proxy script that bridges Claude Code's
/// MCP transport to the C# MCP Server in Unity.
///
/// Resolved via Tauri's `BaseDirectory::Resource` against the bundled
/// resource `proxy-bundle/mcp-proxy.cjs`. The file is staged at build
/// time by `App~/scripts/build-proxy.mjs` (run automatically via the
/// `beforeDevCommand` / `beforeBuildCommand` hooks in `tauri.conf.json`)
/// and shipped inside the production bundle — users do not need Node
/// or `npm run build` on their side.
///
/// The `.cjs` extension is deliberate: esbuild emits a CommonJS bundle,
/// and Node's ESM-vs-CJS decision normally walks up looking for the
/// nearest `package.json` `"type"` field. In dev mode the resolved
/// path sits under `App~/src-tauri/target/debug/proxy-bundle/`, which
/// would walk up into `App~/package.json` (`"type": "module"`) and
/// blow up with `ReferenceError: require is not defined in ES module
/// scope`. `.cjs` short-circuits that lookup unconditionally.
///
/// On Windows the resolved path comes back with the `\\?\` extended-
/// length prefix; this function strips it so the path can be passed
/// cleanly to Node child processes via `MCP_PROXY_PATH`. See
/// <https://github.com/tauri-apps/tauri/issues/5096> (closed not-planned).
///
/// Returns `None` only if Tauri's resource resolver fails outright —
/// existence of the file is the caller's responsibility to verify via
/// `is_file()` (allows the caller to craft a useful error message that
/// includes the expected path).
pub fn mcp_proxy_script(app: &AppHandle) -> Option<PathBuf> {
    match app
        .path()
        .resolve("proxy-bundle/mcp-proxy.cjs", BaseDirectory::Resource)
    {
        Ok(p) => Some(normalize_resource_path(p)),
        Err(e) => {
            eprintln!("[paths] mcp-proxy.cjs Resource resolve failed: {e}");
            None
        }
    }
}

/// Strips Windows' extended-length path prefix (`\\?\`) so the path
/// can be serialized into env vars / JSON for child processes. No-op
/// on non-Windows platforms.
#[cfg(windows)]
fn normalize_resource_path(p: PathBuf) -> PathBuf {
    let s = p.to_string_lossy().into_owned();
    if let Some(stripped) = s.strip_prefix(r"\\?\") {
        PathBuf::from(stripped)
    } else {
        PathBuf::from(s)
    }
}

#[cfg(not(windows))]
fn normalize_resource_path(p: PathBuf) -> PathBuf {
    p
}

/// Path to the package's `Plugin~/` directory — the bundled Claude
/// Code plugin shipped with MCP Game Deck. Used directly as the
/// value of `MCP_GAME_DECK_PLUGIN_DIR` — the SDK reads from the
/// source path. Knowledge base lives at `Plugin~/knowledge/` inside
/// this directory and is referenced from agent/skill content via
/// `${CLAUDE_PLUGIN_ROOT}/knowledge/<file>.md`.
pub fn plugin_dir() -> PathBuf {
    package_root().join("Plugin~")
}

/// Path to Claude Code's per-project session storage root —
/// `<home>/.claude/projects/`. Reads `USERPROFILE` on Windows and
/// `HOME` on Unix; returns `None` when neither is set.
pub fn claude_projects_root() -> Option<PathBuf> {
    let home = std::env::var_os("USERPROFILE").or_else(|| std::env::var_os("HOME"))?;
    Some(PathBuf::from(home).join(".claude").join("projects"))
}

/// Encodes a project path into Claude Code's directory naming
/// convention — every non-alphanumeric character (except the hyphen
/// itself) is replaced with `-`. Mirrors what the CLI does
/// internally, verified against existing entries under
/// `<home>/.claude/projects/`.
///
/// Examples:
/// * `E:\Projects\mcp-game-deck` → `E--Projects-mcp-game-deck`
/// * `/home/user/foo bar` → `-home-user-foo-bar`
pub fn encode_project_path(project_path: &str) -> String {
    project_path
        .chars()
        .map(|c| {
            if c.is_ascii_alphanumeric() || c == '-' {
                c
            } else {
                '-'
            }
        })
        .collect()
}

/// Path to the per-project session JSONL directory:
/// `<home>/.claude/projects/<encoded-cwd>/`. Returns `None` when no
/// home directory is resolvable. The directory may not exist yet —
/// callers handle that as "no sessions".
pub fn claude_sessions_dir(project_path: &str) -> Option<PathBuf> {
    let root = claude_projects_root()?;
    Some(root.join(encode_project_path(project_path)))
}

// endregion