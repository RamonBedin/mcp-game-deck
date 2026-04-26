//! Node Agent SDK child-process supervisor.
//!
//! Group 3 scope:
//! - 3.1: spawn at app startup, kill on app close, store handle in Tauri
//!   managed state. PID logged at startup.
//! - 3.2 (this task): JSON-RPC framing over stdio (request/response
//!   correlation, notification dispatch, 30s timeout).
//! - 3.3: restart command + crash detection.

pub mod jsonrpc;
pub mod protocol;
pub mod spawn;

use std::sync::Arc;

use serde_json::Value;
use tauri::AppHandle;
use tokio::process::Child;
use tokio::sync::Mutex;

pub use jsonrpc::{Connection, RequestError};

/// Tauri managed state. A single async Mutex protects both the child handle
/// and the Connection so spawn / shutdown / future restart serialize cleanly.
/// Long-running operations (e.g. `request`) clone the Connection out of the
/// lock first and don't hold it for the duration of the RPC.
pub struct NodeSupervisor {
    state: Arc<Mutex<State>>,
}

struct State {
    child: Option<Child>,
    connection: Option<Connection>,
}

impl NodeSupervisor {
    pub fn new() -> Self {
        Self {
            state: Arc::new(Mutex::new(State {
                child: None,
                connection: None,
            })),
        }
    }

    /// Spawns the Node SDK child, wires up JSON-RPC framing, and stores both
    /// handles. Returns the child PID on success. If a child is already
    /// running, it is killed first (idempotent — also used by 3.3 restart).
    pub async fn spawn(&self, app: AppHandle) -> std::io::Result<u32> {
        let mut s = self.state.lock().await;

        // Tear down any prior session. Drop connection first so the writer
        // task sees its mpsc close; then kill the child, which causes EOF on
        // stdout and the reader task drains pending requests.
        s.connection = None;
        if let Some(mut existing) = s.child.take() {
            let _ = existing.kill().await;
            let _ = existing.wait().await;
        }

        let mut child = spawn::spawn_node_sdk().await?;
        let stdin = child.stdin.take().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::Other, "child stdin not piped")
        })?;
        let stdout = child.stdout.take().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::Other, "child stdout not piped")
        })?;
        let pid = child.id().unwrap_or(0);

        let connection = Connection::new(stdin, stdout, app);

        s.child = Some(child);
        s.connection = Some(connection);
        Ok(pid)
    }

    /// Kills the running child (if any) and tears down the Connection.
    /// Idempotent.
    ///
    /// Note: on Windows `child.kill()` only terminates the immediate child,
    /// not grandchildren. The current stub doesn't spawn subprocesses, so
    /// this is fine. Feature 02's real orchestrator may need a process-tree
    /// kill (taskkill /T /F) once it spawns the Claude Agent SDK CLI.
    pub async fn shutdown(&self) {
        let mut s = self.state.lock().await;
        s.connection = None;
        if let Some(mut child) = s.child.take() {
            let _ = child.kill().await;
            let _ = child.wait().await;
        }
    }

    /// Sends a JSON-RPC request and awaits the response.
    pub async fn request(
        &self,
        method: &str,
        params: Option<Value>,
    ) -> Result<Value, RequestError> {
        // Clone the Connection out of the lock briefly, then drop the lock
        // before awaiting the (potentially 30s) response.
        let conn = self.state.lock().await.connection.clone();
        match conn {
            Some(c) => c.request(method, params).await,
            None => Err(RequestError::ChildDead),
        }
    }

    /// Convenience helper for the test path: sends `ping`, returns the
    /// `pong` boolean. Used by the `node_ping` Tauri command.
    pub async fn ping(&self) -> Result<bool, RequestError> {
        let result = self.request("ping", None).await?;
        Ok(result
            .get("pong")
            .and_then(|v| v.as_bool())
            .unwrap_or(false))
    }
}

impl Default for NodeSupervisor {
    fn default() -> Self {
        Self::new()
    }
}