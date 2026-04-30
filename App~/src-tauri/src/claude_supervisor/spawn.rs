//! Spawn helpers for the Claude Code supervisor — Node child launcher
//! plus stdout/stderr reader tasks that translate the line-based
//! AgentMessage protocol into Tauri events.

use std::process::Stdio;
use std::sync::Arc;
use std::sync::Mutex as StdMutex;

use tauri::AppHandle;
use tokio::io::{AsyncBufReadExt, BufReader};
use tokio::process::{Child, ChildStderr, ChildStdout, Command};

use crate::claude_supervisor::paths;
use crate::events::{emit_agent_message, emit_supervisor_status_changed};
use crate::types::{
    AgentMessage, AgentMessagePayload, SupervisorStatus, SupervisorStatusChangedPayload,
};

// region: MCP proxy resolution

/// Resolves the absolute path to `Server~/dist/mcp-proxy.js` if it
/// exists. Returns `None` when the script is missing — the caller
/// surfaces a soft warning to React via `AgentMessage::Error` and
/// proceeds without the `mcpServers` config (tools become unavailable
/// for that session, but unrelated prompts still work).
fn resolve_mcp_proxy() -> Option<std::path::PathBuf> {
    let path = paths::mcp_proxy_script();
    if path.is_file() { Some(path) } else { None }
}

/// Resolves `<package>/Plugin~/` if the directory exists. Logs an
/// `eprintln!` warning when missing (package corruption / odd dev
/// setup) but does not propagate — `claude` keeps running with
/// built-in tools only (no package-bundled skills or agents).
fn resolve_plugin_dir() -> Option<std::path::PathBuf> {
    let path = paths::plugin_dir();
    if path.is_dir() {
        Some(path)
    } else {
        eprintln!(
            "[claude-supervisor] Plugin~/ not found at {}. Package skills and agents disabled; user-level extensions in ~/.claude/ still load.",
            path.display()
        );
        None
    }
}

/// Resolves `<unity-project>/ProjectSettings/GameDeck/commands/` if
/// the directory exists. Silently skipped otherwise — the directory
/// is opt-in user content and absence is the common case.
fn resolve_commands_dir(project_path: &str) -> Option<std::path::PathBuf> {
    let path = std::path::Path::new(project_path)
        .join("ProjectSettings")
        .join("GameDeck")
        .join("commands");
    if path.is_dir() { Some(path) } else { None }
}

// endregion

// region: Spawn

/// Spawns a Node child running `App~/runtime/sdk-entry.js`. Sets the
/// env contract explicitly (UNITY_PROJECT_PATH / UNITY_MCP_HOST /
/// UNITY_MCP_PORT) plus the task-2.4 `MCP_PROXY_PATH` when the
/// proxy script is built. Default env inheritance is bypassed —
/// the contract is guaranteed regardless of platform / shell quirks.
///
/// When `Server~/dist/mcp-proxy.js` is missing, an `AgentMessage::Error`
/// is emitted to React explaining the build step and the child is
/// spawned without `MCP_PROXY_PATH`. `sdk-entry.js` then skips the
/// `mcpServers` config and tool calls become unavailable for that
/// session.
///
/// # Arguments
///
/// * `app` - Application handle used to surface the soft-warn event.
/// * `project_path` - Pre-validated UNITY_PROJECT_PATH (caller is
///   responsible for ensuring it is non-empty).
///
/// # Errors
///
/// Returns `std::io::Error` when Node is missing on PATH or the
/// child process can't be created.
pub fn spawn_node_child(app: &AppHandle, project_path: &str) -> std::io::Result<Child> {
    let runtime_dir = paths::runtime_dir();
    let entry = paths::sdk_entry_script();

    let unity_host = std::env::var("UNITY_MCP_HOST").unwrap_or_default();
    let unity_port = std::env::var("UNITY_MCP_PORT").unwrap_or_default();
    let mcp_proxy = resolve_mcp_proxy();
    let plugin_dir = resolve_plugin_dir();
    let commands_dir = resolve_commands_dir(project_path);

    if mcp_proxy.is_none() {
        let expected = paths::mcp_proxy_script();
        let _ = emit_agent_message(
            app,
            AgentMessagePayload {
                message: AgentMessage::Error {
                    message: format!(
                        "MCP proxy not found at {}. Build with `cd Server~ && npm run build` to enable MCP tool calls. Spawning without mcpServers — non-tool prompts still work.",
                        expected.display()
                    ),
                },
            },
        );
    }

    let mut cmd = Command::new("node");
    cmd.arg(&entry)
        .current_dir(&runtime_dir)
        .env("UNITY_PROJECT_PATH", project_path)
        .env("UNITY_MCP_HOST", &unity_host)
        .env("UNITY_MCP_PORT", &unity_port)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    if let Some(path) = mcp_proxy {
        cmd.env("MCP_PROXY_PATH", path.to_string_lossy().as_ref());
    }
    if let Some(path) = plugin_dir {
        cmd.env("MCP_GAME_DECK_PLUGIN_DIR", path.to_string_lossy().as_ref());
    }
    if let Some(path) = commands_dir {
        cmd.env(
            "MCP_GAME_DECK_COMMANDS_DIR",
            path.to_string_lossy().as_ref(),
        );
    }

    cmd.spawn()
}

// endregion

// region: stdout reader

/// Reads the child's stdout line-by-line, parses each line as an
/// `AgentMessagePayload`, applies the side-effect for `Ready` (status
/// transition), and re-emits every envelope as `agent-message` for
/// the React-side `conversationStore` (`appendDelta` / `completeTurn`
/// / `appendErrorMessage`) and for DevTools console debugging.
///
/// Single-canal post: the React store consumes `text-delta`,
/// `assistant-turn-complete`, and `error` directly from the
/// `agent-message` event. `emit_message_received` is no longer
/// invoked from this path.
pub async fn read_stdout(
    stdout: ChildStdout,
    app: AppHandle,
    status: Arc<StdMutex<SupervisorStatus>>,
) {
    let mut reader = BufReader::new(stdout).lines();
    while let Ok(Some(line)) = reader.next_line().await {
        let payload: AgentMessagePayload = match serde_json::from_str(&line) {
            Ok(p) => p,
            Err(e) => {
                eprintln!("[claude-supervisor] bad stdout line: {line} — {e}");
                continue;
            }
        };

        match &payload.message {
            AgentMessage::Ready => {
                {
                    let mut s = status.lock().expect("supervisor status mutex poisoned");
                    *s = SupervisorStatus::Ready;
                }
                let _ = emit_supervisor_status_changed(
                    &app,
                    SupervisorStatusChangedPayload {
                        status: SupervisorStatus::Ready,
                        pid: None,
                    },
                );
            }
            AgentMessage::TextDelta { .. }
            | AgentMessage::ToolUse { .. }
            | AgentMessage::ToolResult { .. }
            | AgentMessage::AssistantTurnComplete { .. }
            | AgentMessage::AssistantText { .. }
            | AgentMessage::Error { .. } => {
            }
        }

        let _ = emit_agent_message(&app, payload);
    }
}

// endregion

// region: stderr passthrough

/// Forwards the child's stderr to the Tauri host's stderr. Debug-only
/// — never reaches React. `sdk-entry.js` writes `[sdk-entry] ...`
/// lines here; npm install errors during `sdk_install.rs` use a
/// separate channel.
pub async fn read_stderr(stderr: ChildStderr) {
    let mut reader = BufReader::new(stderr).lines();
    while let Ok(Some(line)) = reader.next_line().await {
        eprintln!("[sdk-entry/stderr] {line}");
    }
}

// endregion