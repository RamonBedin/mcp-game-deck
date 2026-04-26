//! Spawns the Node Agent SDK child process.
//!
//! Path resolution is dev-only for now: anchored at CARGO_MANIFEST_DIR
//! (resolves to App~/src-tauri/ at compile time), walks up to the repo root,
//! then targets Server~/agent-sdk-stub.js. Production resolution lands later
//! when the Tauri binary needs to find the bundled Node SDK.

use std::path::PathBuf;
use std::process::Stdio;

use tokio::process::{Child, Command};

const NODE_BINARY: &str = "node";
const STUB_SCRIPT_NAME: &str = "agent-sdk-stub.js";
const SERVER_DIR_NAME: &str = "Server~";

fn resolve_stub_script() -> PathBuf {
    let manifest = PathBuf::from(env!("CARGO_MANIFEST_DIR"));
    // App~/src-tauri/ → App~/ → repo root
    let repo_root = manifest
        .parent()
        .and_then(|p| p.parent())
        .unwrap_or(&manifest);
    repo_root.join(SERVER_DIR_NAME).join(STUB_SCRIPT_NAME)
}

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
        // stderr inherits so panics / module errors surface in the dev console.
        .stderr(Stdio::inherit())
        // Belt-and-suspenders: if we drop the Child without explicit cleanup,
        // tokio still kills the process.
        .kill_on_drop(true);

    cmd.spawn().map_err(|e| {
        eprintln!(
            "[node-supervisor] failed to spawn '{NODE_BINARY}' \
             (is Node installed and on PATH?): {e}"
        );
        e
    })
}