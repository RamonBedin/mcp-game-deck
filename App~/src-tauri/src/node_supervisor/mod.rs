//! Node Agent SDK child-process supervisor.
//!
//! Group 3 scope:
//! - 3.1 (this task): spawn at app startup, kill on app close, store handle in
//!   Tauri managed state. PID logged at startup.
//! - 3.2: JSON-RPC framing over stdio (request/response correlation).
//! - 3.3: restart command + crash detection.

pub mod spawn;

use std::sync::Arc;

use tokio::process::Child;
use tokio::sync::Mutex;

/// Tauri managed state. Holds the live child handle behind an async Mutex so
/// spawn / shutdown / future restart can serialize access.
pub struct NodeSupervisor {
    child: Arc<Mutex<Option<Child>>>,
}

impl NodeSupervisor {
    pub fn new() -> Self {
        Self {
            child: Arc::new(Mutex::new(None)),
        }
    }

    /// Spawns the Node SDK child and stores the handle. Returns the child's
    /// PID on success. If a child is already running, it is killed first.
    pub async fn spawn(&self) -> std::io::Result<u32> {
        let mut guard = self.child.lock().await;

        if let Some(mut existing) = guard.take() {
            let _ = existing.kill().await;
            let _ = existing.wait().await;
        }

        let child = spawn::spawn_node_sdk().await?;
        let pid = child.id().unwrap_or(0);
        *guard = Some(child);
        Ok(pid)
    }

    /// Kills the running child (if any) and waits for it to exit. Idempotent.
    /// Note: on Windows this only terminates the direct child, not any
    /// grandchildren it may have spawned. The Group 3 stub doesn't spawn
    /// subprocesses, so this is fine for now.
    pub async fn shutdown(&self) {
        let mut guard = self.child.lock().await;
        if let Some(mut child) = guard.take() {
            let _ = child.kill().await;
            let _ = child.wait().await;
        }
    }
}

impl Default for NodeSupervisor {
    fn default() -> Self {
        Self::new()
    }
}
