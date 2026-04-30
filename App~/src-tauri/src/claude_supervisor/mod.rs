//! Claude Code supervisor module — owns the Claude Code subprocess that
//! powers the chat experience.
//!

pub mod install_check;
pub mod paths;
pub mod sdk_install;

use std::sync::Mutex as StdMutex;

use tauri::AppHandle;

use crate::types::SupervisorStatus;

// region: SpawnError

/// Failure mode for `ClaudeSupervisor::spawn`.  add
/// variants for real failure paths (SDK missing, exec error, etc).
#[derive(Debug)]
pub enum SpawnError {
    NotImplemented,
}

impl std::fmt::Display for SpawnError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            SpawnError::NotImplemented => {
                write!(f, "Supervisor not yet implemented (task 2.2)")
            }
        }
    }
}

impl std::error::Error for SpawnError {}

// endregion

// region: ClaudeSupervisor

/// Tauri-managed supervisor for the Claude Code subprocess.
///
/// This skeleton tracks only `SupervisorStatus` today — the child
/// process handle, JSON-RPC connection, and health-check loop land
/// . `spawn` returns `Err(SpawnError::NotImplemented)`
/// and `shutdown` is a no-op-equivalent (resets status to `Idle`).
pub struct ClaudeSupervisor {
    status: StdMutex<SupervisorStatus>,
}

impl ClaudeSupervisor {
    /// Builds a fresh supervisor in the `Idle` state. No child is
    /// spawned until `spawn` is called.
    pub fn new() -> Self {
        Self {
            status: StdMutex::new(SupervisorStatus::Idle),
        }
    }

    /// Returns the current supervisor status. Microsecond-cheap —
    /// the lock is never held across an await.
    pub fn current_status(&self) -> SupervisorStatus {
        *self.status.lock().expect("supervisor status mutex poisoned")
    }

    /// Starts the Claude Code subprocess. Skeleton — flips the
    /// status to `Failed` (so React sees a definite terminal state
    /// instead of staying on `Idle`) and returns
    /// `Err(SpawnError::NotImplemented)`.
    ///
    /// # Arguments
    ///
    /// # Errors
    ///
    /// Returns `SpawnError::NotImplemented` until task 2.2 lands.
    pub async fn spawn(&self, _app: AppHandle) -> Result<u32, SpawnError> {
        *self.status.lock().expect("supervisor status mutex poisoned") =
            SupervisorStatus::Failed;
        Err(SpawnError::NotImplemented)
    }

    /// Tears down the running Claude Code subprocess. Skeleton —
    /// resets the status to `Idle`; adds the SIGTERM/SIGKILL
    /// sequence over the actual child handle.
    pub async fn shutdown(&self) {
        *self.status.lock().expect("supervisor status mutex poisoned") =
            SupervisorStatus::Idle;
    }
}

impl Default for ClaudeSupervisor {
    fn default() -> Self {
        Self::new()
    }
}

// endregion