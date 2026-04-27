//! Node Agent SDK child-process supervisor.
//!
//! Group 3 scope:
//! - 3.1: spawn at app startup, kill on app close, store handle in Tauri
//!   managed state. PID logged at startup.
//! - 3.2: JSON-RPC framing over stdio (request/response correlation,
//!   notification dispatch, 30s timeout).
//! - 3.3 (this task): restart command + crash detection. NodeSdkStatus state
//!   machine: Starting → Running (after first ping succeeds) → Crashed (on
//!   stdout EOF or ping failure). Manual restart only — no auto-restart.

pub mod jsonrpc;
pub mod protocol;
pub mod spawn;

use std::sync::Arc;
use std::sync::Mutex as StdMutex;

use serde_json::Value;
use tauri::AppHandle;
use tokio::process::Child;
use tokio::sync::Mutex;

pub use jsonrpc::{Connection, RequestError};
use jsonrpc::{StatusArc, transition_node_status};

use crate::types::NodeSdkStatus;

/// Tauri managed state. Two locks:
/// - `state` (async Mutex): serializes spawn/shutdown, holds child + connection.
/// - `status` (sync Mutex): brief read/write of the current NodeSdkStatus.
///   Sync because Tauri's `get_node_sdk_status` command stays sync — and we
///   never hold this lock across an await.
#[derive(Clone)]
pub struct NodeSupervisor {
    state: Arc<Mutex<State>>,
    status: StatusArc,
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
            // Initial state: nothing running. UI shows "crashed" briefly
            // until setup() calls spawn().
            status: Arc::new(StdMutex::new(NodeSdkStatus::Crashed)),
        }
    }

    /// Returns the current NodeSdkStatus. Sync (microsecond-cheap).
    pub fn current_status(&self) -> NodeSdkStatus {
        *self.status.lock().unwrap()
    }

    /// Spawns the Node SDK child, wires up JSON-RPC framing, and stores both
    /// handles. Returns the child PID. Idempotent — kills any prior child.
    /// Also serves as the implementation of `restart_node_sdk`.
    ///
    /// Status flow:
    ///   (any) → Starting → spawn child → Running (if ping OK) | Crashed (else)
    ///
    /// The `Starting → Running` transition happens off this future, in a
    /// background health-check task. spawn() returns as soon as the child
    /// exists; the UI sees "starting" until ping completes.
    pub async fn spawn(&self, app: AppHandle) -> std::io::Result<u32> {
        let mut s = self.state.lock().await;

        // Tear down any prior session. Drop connection first (closes writer
        // mpsc → writer task exits), then kill child (causes reader EOF →
        // reader drains pending requests). Set status to Crashed so the
        // reader's EOF transition is a no-op (no "ghost crash" event during
        // an intentional restart).
        transition_node_status(&app, &self.status, NodeSdkStatus::Crashed, None);
        s.connection = None;
        if let Some(mut existing) = s.child.take() {
            let _ = existing.kill().await;
            let _ = existing.wait().await;
        }

        // Now publicly transition to Starting and spawn the new child.
        transition_node_status(&app, &self.status, NodeSdkStatus::Starting, None);

        let mut child = match spawn::spawn_node_sdk().await {
            Ok(c) => c,
            Err(e) => {
                transition_node_status(&app, &self.status, NodeSdkStatus::Crashed, None);
                return Err(e);
            }
        };
        let stdin = child.stdin.take().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::Other, "child stdin not piped")
        })?;
        let stdout = child.stdout.take().ok_or_else(|| {
            std::io::Error::new(std::io::ErrorKind::Other, "child stdout not piped")
        })?;
        let pid = child.id().unwrap_or(0);

        let connection = Connection::new(stdin, stdout, app.clone(), self.status.clone());
        s.child = Some(child);
        s.connection = Some(connection);
        drop(s); // release lock before spawning the health-check task.

        // Health check: ping in background. Status transitions to Running
        // (or Crashed if the child died before responding).
        let supervisor = self.clone();
        let app_for_check = app;
        tokio::spawn(async move {
            match supervisor.ping().await {
                Ok(_) => transition_node_status(
                    &app_for_check,
                    &supervisor.status,
                    NodeSdkStatus::Running,
                    Some(pid),
                ),
                Err(e) => {
                    eprintln!("[node-supervisor] initial ping failed: {e}");
                    transition_node_status(
                        &app_for_check,
                        &supervisor.status,
                        NodeSdkStatus::Crashed,
                        None,
                    );
                }
            }
        });

        Ok(pid)
    }

    /// Kills the running child (if any) and tears down the Connection.
    /// Idempotent. Sets status to Crashed silently — the reader's EOF
    /// transition then becomes a no-op (no event during intentional close).
    ///
    /// Note: on Windows `child.kill()` only terminates the immediate child,
    /// not grandchildren. The stub doesn't spawn subprocesses; Feature 02
    /// will need a process-tree kill once it spawns the Claude Agent SDK CLI.
    pub async fn shutdown(&self) {
        // Mark Crashed in the local atomic — no event emitted (we're closing).
        *self.status.lock().unwrap() = NodeSdkStatus::Crashed;
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

    /// Convenience: round-trip `ping`, return the `pong` boolean.
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