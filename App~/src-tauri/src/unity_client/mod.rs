//! TCP client for Unity's MCP server (HTTP/1.1 transport).
//!
//! Group 4 scope:
//! - 4.1 (this task): connect to 127.0.0.1:8090, heartbeat every 5s,
//!   reconnect with backoff [1s, 2s, 5s, 10s, 30s capped], emit
//!   `unity-status-changed` events on transitions.
//! - 4.2: add POST `/` MCP RPC helper + `dev_call_unity_tool` command
//!   (requires auth token, which requires knowing the Unity project path).

pub mod connection;
pub mod protocol;

use std::net::SocketAddr;
use std::sync::Arc;
use std::sync::Mutex as StdMutex;

use tauri::AppHandle;

use crate::events::emit_unity_status_changed;
use crate::types::{ConnectionStatus, UnityStatusChangedPayload};

const DEFAULT_HOST: &str = "127.0.0.1";
const DEFAULT_PORT: u16 = 8090;

/// Tauri managed state for the Unity TCP client.
///
/// Holds the live `ConnectionStatus` behind a sync Mutex (microsecond locks,
/// never held across await — same pattern as `NodeSupervisor::status`).
/// Cloneable so the run loop spawned in `start()` can own its own handle.
#[derive(Clone)]
pub struct UnityClient {
    status: Arc<StdMutex<ConnectionStatus>>,
    addr: SocketAddr,
}

impl UnityClient {
    pub fn new() -> Self {
        let addr: SocketAddr = format!("{DEFAULT_HOST}:{DEFAULT_PORT}")
            .parse()
            .expect("hardcoded host:port should parse");
        Self {
            status: Arc::new(StdMutex::new(ConnectionStatus::Disconnected)),
            addr,
        }
    }

    pub fn current_status(&self) -> ConnectionStatus {
        *self.status.lock().unwrap()
    }

    /// Spawns the background connection task. Idempotent only at the
    /// caller's discretion — calling twice would start two tasks. Setup
    /// should call this exactly once.
    ///
    /// Uses `tauri::async_runtime::spawn` (not `tokio::spawn`) so the task
    /// is scheduled on Tauri's managed runtime — `tokio::spawn` requires
    /// being already inside a Tokio context, which `setup()` is not.
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
                // Newly reachable — transition to Connected, reset backoff.
                self.transition(&app, ConnectionStatus::Connected);
                backoff_idx = 0;

                // Stay connected, heartbeat on interval until one fails.
                loop {
                    tokio::time::sleep(connection::HEARTBEAT_INTERVAL).await;
                    if !connection::heartbeat(self.addr).await {
                        break;
                    }
                }

                self.transition(&app, ConnectionStatus::Disconnected);
            } else {
                // Stays Disconnected (idempotent if already so).
                self.transition(&app, ConnectionStatus::Disconnected);
            }

            // Backoff before the next reconnect attempt. Capped at the last
            // schedule entry so we never tight-loop while Unity is down.
            tokio::time::sleep(connection::backoff_delay(backoff_idx)).await;
            backoff_idx = (backoff_idx + 1).min(connection::BACKOFF_SCHEDULE_SECS.len() - 1);
        }
    }

    /// CAS-style status transition: only emits the Tauri event if the
    /// status actually changed. Idempotent on every code path.
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
}

impl Default for UnityClient {
    fn default() -> Self {
        Self::new()
    }
}