//! Path resolution for the Tauri-managed Node runtime + the Unity
//! package surface (Plugin~/) + the embedded MCP proxy script.
//!
//! Three resolution channels:
//!
//! - **`MCP_GAME_DECK_PACKAGE_ROOT`** env var (populated by Unity's
//!   `PinLauncher` via `PackageInfo.resolvedPath`) — source of
//!   `Plugin~/`. Falls back to a `CARGO_MANIFEST_DIR` walk-up in dev.
//! - **`MCP_GAME_DECK_RUNTIME_DIR`** env var (also populated by
//!   `PinLauncher`) — writable per-version dir under the OS user-data
//!   tree, owns the npm SDK install, the generated `sdk-entry.js`,
//!   and the extracted `proxy-bundle/mcp-proxy.cjs`. Falls back to
//!   `App~/runtime/` in dev.
//! - **Compiled-in `EMBEDDED_PROXY` blob** — the entire `mcp-proxy.cjs`
//!   (esbuild output) is `include_bytes!`-embedded into the .exe at
//!   build time. `mcp_proxy_script` extracts it to `runtime_dir()`
//!   on demand so the binary is self-contained — no installer-staged
//!   resource folder required.

use std::path::PathBuf;

use tauri::AppHandle;

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

const EMBEDDED_PROXY: &[u8] = include_bytes!("../../proxy-bundle/mcp-proxy.cjs");

/// Path to the MCP proxy script that bridges Claude Code's MCP transport
/// to the C# MCP Server in Unity. Always lives under `runtime_dir()` —
/// the file is extracted from the compiled-in `EMBEDDED_PROXY` blob on
/// every call so the binary stays self-contained (no separate
/// `proxy-bundle/` folder required next to the .exe).
///
/// The `.cjs` extension is preserved deliberately: esbuild emits a
/// CommonJS bundle, and Node's ESM-vs-CJS decision normally walks up
/// looking for the nearest `package.json` `"type"` field. `.cjs`
/// short-circuits that lookup unconditionally.
///
/// Writes are idempotent — the bytes are identical across calls within
/// a single binary version, and `runtime_dir()` is version-isolated
/// (`%APPDATA%\MCPGameDeck\runtime\<version>\` in release), so stale
/// extractions from older versions can't poison the current one.
///
/// Returns `None` only if the filesystem operations fail (cannot create
/// the parent directory or cannot write the file). Existence of the
/// file after a successful call is guaranteed.
pub fn mcp_proxy_script(_app: &AppHandle) -> Option<PathBuf> {
    let dest = runtime_dir().join("proxy-bundle").join("mcp-proxy.cjs");
    let parent = dest.parent()?;

    if let Err(e) = std::fs::create_dir_all(parent) {
        eprintln!("[paths] Failed to create {}: {e}", parent.display());
        return None;
    }
    if let Err(e) = std::fs::write(&dest, EMBEDDED_PROXY) {
        eprintln!(
            "[paths] Failed to extract embedded mcp-proxy.cjs to {}: {e}",
            dest.display()
        );
        return None;
    }

    Some(dest)
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