//! Spawns the Node Agent SDK child process.
//!
//! Path resolution is dev-only for now: anchored at `CARGO_MANIFEST_DIR`
//! (resolves to `App~/src-tauri/` at compile time), walks up to the repo root,
//! then targets `Server~/agent-sdk-stub.js`. Production resolution lands later
//! when the Tauri binary needs to find the bundled Node SDK.

use std::path::PathBuf;
use std::process::Stdio;

use tokio::process::{Child, Command};

// region: Constants

/// Executable invoked to run the SDK script. Must be on `PATH`.
const NODE_BINARY: &str = "node";

/// Filename of the dev-only stub script (Feature 02 replaces it with the real SDK entry point).
const STUB_SCRIPT_NAME: &str = "agent-sdk-stub.js";

/// Repo-relative directory housing the Node-side server / SDK code.
const SERVER_DIR_NAME: &str = "Server~";

// endregion

// region: Path resolution

/// Resolves the absolute path to the dev stub script.
///
/// Anchored at `CARGO_MANIFEST_DIR` (`App~/src-tauri/`), walks up two levels
/// to the repo root, then joins `Server~/agent-sdk-stub.js`. Falls back to
/// the manifest dir if either parent lookup fails — keeps `spawn` honest:
/// it logs a clear "stub script not found" warning instead of panicking.
///
/// # Returns
///
/// Best-effort absolute path to the stub script.
fn resolve_stub_script() -> PathBuf {
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    let repo_root = manifest
        .parent()
        .and_then(|p| p.parent())
        .unwrap_or(&manifest);
    repo_root.join(SERVER_DIR_NAME).join(STUB_SCRIPT_NAME)
}

// endregion

// region: Spawn

/// Spawns `node <stub>` with stdio piped for JSON-RPC framing.
///
/// `stdin` and `stdout` are captured for the framing layer. `stderr` is
/// inherited so panics and module errors surface in the dev console.
/// `kill_on_drop` is set as a safety net in case the `Child` is dropped
/// without an explicit shutdown.
///
/// # Returns
///
/// The spawned `Child`.
///
/// # Errors
///
/// Returns `std::io::Error` when `node` is not on `PATH` or the OS denies
/// the spawn. The missing-script case only emits a warning; spawn still
/// proceeds so Node's own error message reaches stderr.
pub async fn spawn_node_sdk() -> std::io::Result<Child> {
    let script = resolve_stub_script();

    if !script.exists() {
        eprintln!(
            "[node-supervisor] stub script not found at {} — \
             did you keep Server~/agent-sdk-stub.js?",
            script.display()
        );
    }

    let mut cmd = Command::new(NODE_BINARY);
    cmd.arg(&script)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .kill_on_drop(true);

    cmd.spawn().map_err(|e| {
        eprintln!(
            "[node-supervisor] failed to spawn '{NODE_BINARY}' \
             (is Node installed and on PATH?): {e}"
        );
        e
    })
}

// endregion