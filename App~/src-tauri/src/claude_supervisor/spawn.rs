//! Spawn helpers for the Claude Code supervisor — Node child launcher
//! plus stdout/stderr reader tasks that translate the line-based
//! AgentMessage protocol into Tauri events.

use std::process::Stdio;
use std::sync::Arc;
use std::sync::Mutex as StdMutex;
use std::time::{SystemTime, UNIX_EPOCH};

use tauri::AppHandle;
use tokio::io::{AsyncBufReadExt, BufReader};
use tokio::process::{Child, ChildStderr, ChildStdout, Command};

use crate::claude_supervisor::paths;
use crate::events::{
    emit_agent_message, emit_message_received, emit_supervisor_status_changed,
};
use crate::types::{
    AgentMessage, AgentMessagePayload, Message, MessageRole, SupervisorStatus,
    SupervisorStatusChangedPayload,
};

// region: Spawn

/// Spawns a Node child running `App~/runtime/sdk-entry.js`. Sets the
/// F07 env contract explicitly (UNITY_PROJECT_PATH / UNITY_MCP_HOST /
/// UNITY_MCP_PORT) instead of relying on Tauri's default env
/// inheritance — guarantees the contract regardless of platform /
/// shell quirks.
///
/// # Arguments
///
/// * `project_path` - Pre-validated UNITY_PROJECT_PATH (caller is
///   responsible for ensuring it is non-empty).
///
/// # Errors
///
/// Returns `std::io::Error` when Node is missing on PATH or the
/// child process can't be created.
pub fn spawn_node_child(project_path: &str) -> std::io::Result<Child> {
    let runtime_dir = paths::runtime_dir();
    let entry = paths::sdk_entry_script();

    let unity_host = std::env::var("UNITY_MCP_HOST").unwrap_or_default();
    let unity_port = std::env::var("UNITY_MCP_PORT").unwrap_or_default();

    let mut cmd = Command::new("node");
    cmd.arg(&entry)
        .current_dir(&runtime_dir)
        .env("UNITY_PROJECT_PATH", project_path)
        .env("UNITY_MCP_HOST", &unity_host)
        .env("UNITY_MCP_PORT", &unity_port)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped());

    cmd.spawn()
}

// endregion

// region: stdout reader

/// Reads the child's stdout line-by-line, parses each line as an
/// `AgentMessagePayload`, and dispatches: status transitions for
/// `Ready`, `message-received` for `AssistantText` / `Error`, no-op
/// for `AssistantTurnComplete`. Always re-emits the raw envelope as
/// `agent-message` for DevTools console debugging.
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
            AgentMessage::AssistantText { text } => {
                let _ = emit_message_received(
                    &app,
                    Message {
                        id: make_message_id("asst"),
                        role: MessageRole::Assistant,
                        content: text.clone(),
                        timestamp: now_ms(),
                        agent: None,
                    },
                );
            }
            AgentMessage::AssistantTurnComplete => {.
            }
            AgentMessage::Error { message } => {
                let _ = emit_message_received(
                    &app,
                    Message {
                        id: make_message_id("err"),
                        role: MessageRole::System,
                        content: format!("error: {message}"),
                        timestamp: now_ms(),
                        agent: None,
                    },
                );
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

// region: Helpers

fn now_ms() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0)
}

fn make_message_id(prefix: &str) -> String {
    use std::sync::atomic::{AtomicU64, Ordering};
    static COUNTER: AtomicU64 = AtomicU64::new(0);
    let n = COUNTER.fetch_add(1, Ordering::SeqCst);
    format!("{prefix}-{}-{n}", now_ms())
}

// endregion