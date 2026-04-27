//! TCP client for Unity's MCP server (HTTP/1.1 transport).
//!
//! Group 4 scope:
//! - 4.1: connect to 127.0.0.1:8090, heartbeat every 5s, reconnect with
//!   backoff [1s, 2s, 5s, 10s, 30s capped], emit `unity-status-changed`
//!   events on transitions.
//! - 4.2 (this task): POST `/` MCP RPC helper + `dev_call_unity_tool` —
//!   forwards `tools/call` requests with Bearer auth from
//!   `<unity_project>/Library/GameDeck/auth-token`. Project path comes from
//!   the `UNITY_PROJECT_PATH` env var until Feature 07's pin sets it.

pub mod connection;
pub mod protocol;

use std::net::SocketAddr;
use std::path::PathBuf;
use std::sync::Arc;
use std::sync::Mutex as StdMutex;
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::Duration;

use serde_json::Value;
use tauri::AppHandle;

use crate::events::emit_unity_status_changed;
use crate::types::{ConnectionStatus, UnityStatusChangedPayload};

const DEFAULT_HOST: &str = "127.0.0.1";
const DEFAULT_PORT: u16 = 8090;
const REQUEST_TIMEOUT: Duration = Duration::from_secs(30);
const ENV_UNITY_PROJECT_PATH: &str = "UNITY_PROJECT_PATH";
const AUTH_TOKEN_RELATIVE_PATH: &str = "Library/GameDeck/auth-token";

/// Tauri managed state for the Unity TCP client.
///
/// Holds the live `ConnectionStatus` behind a sync Mutex (microsecond locks,
/// never held across await — same pattern as `NodeSupervisor::status`).
/// Cloneable so the run loop spawned in `start()` can own its own handle.
#[derive(Clone)]
pub struct UnityClient {
    status: Arc<StdMutex<ConnectionStatus>>,
    addr: SocketAddr,
    /// Cached MCP auth token. Lazily loaded from disk on first
    /// `call_tool` — no token is needed for the GET heartbeat.
    auth_token: Arc<StdMutex<Option<String>>>,
    /// JSON-RPC request id source. Each tool call uses a fresh id (HTTP
    /// makes correlation trivial since request and response share the same
    /// connection — but unique ids still help when reading server logs).
    next_id: Arc<AtomicU64>,
}

#[derive(Debug)]
pub enum ToolCallError {
    /// Could not load the Bearer token (env var unset, file missing, etc).
    AuthMissing(String),
    /// TCP / HTTP transport failure.
    Transport(std::io::Error),
    /// Server replied with a non-200 status code.
    HttpStatus(u16, String),
    /// Response body was not valid JSON.
    Json(String),
    /// JSON-RPC `error` object.
    McpError(Value),
    /// Response was valid JSON but had neither `result` nor `error`.
    Malformed(String),
}

impl std::fmt::Display for ToolCallError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            ToolCallError::AuthMissing(s) => write!(f, "auth: {s}"),
            ToolCallError::Transport(e) => write!(f, "transport: {e}"),
            ToolCallError::HttpStatus(code, body) => {
                let preview: String = body.chars().take(200).collect();
                write!(f, "http {code}: {preview}")
            }
            ToolCallError::Json(s) => write!(f, "json parse: {s}"),
            ToolCallError::McpError(v) => write!(f, "mcp error: {v}"),
            ToolCallError::Malformed(s) => write!(f, "malformed response: {s}"),
        }
    }
}

impl std::error::Error for ToolCallError {}

impl UnityClient {
    pub fn new() -> Self {
        let addr: SocketAddr = format!("{DEFAULT_HOST}:{DEFAULT_PORT}")
            .parse()
            .expect("hardcoded host:port should parse");
        Self {
            status: Arc::new(StdMutex::new(ConnectionStatus::Disconnected)),
            addr,
            auth_token: Arc::new(StdMutex::new(None)),
            next_id: Arc::new(AtomicU64::new(1)),
        }
    }

    pub fn current_status(&self) -> ConnectionStatus {
        *self.status.lock().unwrap()
    }

    /// Spawns the background connection task. Idempotent only at the
    /// caller's discretion — calling twice would start two tasks. Setup
    /// should call this exactly once.
    pub fn start(&self, app: AppHandle) {
        let client = self.clone();
        tauri::async_runtime::spawn(async move {
            client.run(app).await;
        });
    }

    /// Connection management loop. Runs until the app exits.
    async fn run(&self, app: AppHandle) {
        let mut backoff_idx: usize = 0;

        loop {
            if connection::heartbeat(self.addr).await {
                self.transition(&app, ConnectionStatus::Connected);
                backoff_idx = 0;

                loop {
                    tokio::time::sleep(connection::HEARTBEAT_INTERVAL).await;
                    if !connection::heartbeat(self.addr).await {
                        break;
                    }
                }

                self.transition(&app, ConnectionStatus::Disconnected);
            } else {
                self.transition(&app, ConnectionStatus::Disconnected);
            }

            tokio::time::sleep(connection::backoff_delay(backoff_idx)).await;
            backoff_idx = (backoff_idx + 1).min(connection::BACKOFF_SCHEDULE_SECS.len() - 1);
        }
    }

    fn transition(&self, app: &AppHandle, new_status: ConnectionStatus) {
        let changed = {
            let mut guard = self.status.lock().unwrap();
            if *guard == new_status {
                false
            } else {
                *guard = new_status;
                true
            }
        };
        if changed {
            let _ = emit_unity_status_changed(
                app,
                UnityStatusChangedPayload {
                    status: new_status,
                    reason: None,
                },
            );
            let label = match new_status {
                ConnectionStatus::Connected => "connected",
                ConnectionStatus::Busy => "busy",
                ConnectionStatus::Disconnected => "disconnected",
            };
            println!("[unity-client] status → {label}");
        }
    }

    /// Forwards `tools/call` to Unity's MCP server. Constructs the JSON-RPC
    /// envelope, sends it via authenticated POST, and unwraps the
    /// `result` / `error` fields from the response.
    pub async fn call_tool(
        &self,
        name: &str,
        arguments: Value,
    ) -> Result<Value, ToolCallError> {
        let token = self.get_auth_token()?;
        let id = self.next_id.fetch_add(1, Ordering::SeqCst);

        let envelope = serde_json::json!({
            "jsonrpc": "2.0",
            "id": id,
            "method": "tools/call",
            "params": { "name": name, "arguments": arguments },
        });
        let body = envelope.to_string();

        let (status, response_body) =
            protocol::http_post_json(self.addr, &body, &token, REQUEST_TIMEOUT)
                .await
                .map_err(ToolCallError::Transport)?;

        if status != 200 {
            return Err(ToolCallError::HttpStatus(status, response_body));
        }

        let parsed: Value = serde_json::from_str(&response_body)
            .map_err(|e| ToolCallError::Json(e.to_string()))?;

        if let Some(error) = parsed.get("error") {
            return Err(ToolCallError::McpError(error.clone()));
        }
        if let Some(result) = parsed.get("result") {
            return Ok(result.clone());
        }
        Err(ToolCallError::Malformed(
            "response has neither result nor error".to_string(),
        ))
    }

    /// Returns the cached auth token, loading it from disk on first access.
    /// Cache lives for the app's lifetime — restart Tauri after switching
    /// Unity projects.
    fn get_auth_token(&self) -> Result<String, ToolCallError> {
        {
            let cache = self.auth_token.lock().unwrap();
            if let Some(t) = cache.as_ref() {
                return Ok(t.clone());
            }
        }
        let token = load_auth_token()?;
        *self.auth_token.lock().unwrap() = Some(token.clone());
        Ok(token)
    }
}

impl Default for UnityClient {
    fn default() -> Self {
        Self::new()
    }
}

fn load_auth_token() -> Result<String, ToolCallError> {
    let project_path = std::env::var(ENV_UNITY_PROJECT_PATH).map_err(|_| {
        ToolCallError::AuthMissing(format!(
            "{ENV_UNITY_PROJECT_PATH} env var not set; \
             point it at the Unity project root (folder containing Library/) \
             before launching Tauri. Feature 07's pin will set this automatically."
        ))
    })?;

    let token_path = PathBuf::from(&project_path).join(AUTH_TOKEN_RELATIVE_PATH);

    let raw = std::fs::read_to_string(&token_path).map_err(|e| {
        ToolCallError::AuthMissing(format!(
            "failed to read {}: {e}",
            token_path.display()
        ))
    })?;

    let token = raw.trim().to_string();
    if token.is_empty() {
        return Err(ToolCallError::AuthMissing(format!(
            "{} is empty",
            token_path.display()
        )));
    }
    Ok(token)
}