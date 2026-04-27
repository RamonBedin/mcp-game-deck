//! JSON-RPC 2.0 framing over child-process stdio.
//!
//! Two tokio tasks run per Connection:
//! - **Reader** consumes newline-delimited JSON from child stdout. Responses
//!   (have `id`) are dispatched to the matching pending oneshot. Notifications
//!   (no `id`) are forwarded to Tauri events (`node-log` for "log", or a
//!   generic `node-notification` envelope for everything else).
//! - **Writer** drains an mpsc queue, writing newline-delimited JSON to
//!   child stdin.
//!
//! Failure modes:
//! - Stdout EOF → child died → all pending requests reject with "child exited".
//! - Stdin write error → writer task exits; subsequent requests fail with
//!   ChildDead because `writer_tx.send()` errors.
//! - 30s elapsed without response → request returns Timeout; pending entry is
//!   removed so a late response is logged-and-dropped (not delivered to a
//!   stale receiver).

use std::collections::HashMap;
use std::sync::Arc;
use std::sync::Mutex as StdMutex;
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::Duration;

use serde_json::{Value, json};
use tauri::{AppHandle, Emitter};
use tokio::io::{AsyncBufReadExt, AsyncWriteExt, BufReader};
use tokio::process::{ChildStdin, ChildStdout};
use tokio::sync::{Mutex, mpsc, oneshot};
use tokio::time::timeout;

use crate::events::emit_node_sdk_status_changed;
use crate::types::{NodeSdkStatus, NodeSdkStatusChangedPayload};

use super::protocol::{Incoming, Request, RpcError};

const REQUEST_TIMEOUT_SECS: u64 = 30;
const WRITER_QUEUE_CAPACITY: usize = 64;
const EVT_NODE_LOG: &str = "node-log";
const EVT_NODE_NOTIFICATION: &str = "node-notification";

type PendingMap = Arc<Mutex<HashMap<u64, oneshot::Sender<Result<Value, RpcError>>>>>;
pub type StatusArc = Arc<StdMutex<NodeSdkStatus>>;

#[derive(Clone)]
pub struct Connection {
    next_id: Arc<AtomicU64>,
    pending: PendingMap,
    writer_tx: mpsc::Sender<String>,
}

/// Atomically swap the Node SDK status and emit `node-sdk-status-changed`
/// iff the value actually changed. Idempotent: no event if already the
/// target status. Used by both the supervisor (Starting/Running) and the
/// reader_loop (Crashed on EOF), so identical state machines either path.
pub(super) fn transition_node_status(
    app: &AppHandle,
    status_arc: &StatusArc,
    new_status: NodeSdkStatus,
    pid: Option<u32>,
) {
    let changed = {
        let mut guard = status_arc.lock().unwrap();
        if *guard == new_status {
            false
        } else {
            *guard = new_status;
            true
        }
    };
    if changed {
        let _ = emit_node_sdk_status_changed(
            app,
            NodeSdkStatusChangedPayload {
                status: new_status,
                pid,
            },
        );
    }
}

#[derive(Debug)]
pub enum RequestError {
    /// Child process is not running (never spawned, exited, or stdin closed).
    ChildDead,
    /// No response within REQUEST_TIMEOUT_SECS.
    Timeout,
    /// Node SDK returned a JSON-RPC `error` object.
    Rpc(RpcError),
    /// Failed to serialize the outgoing request.
    Serde(String),
}

impl std::fmt::Display for RequestError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            RequestError::ChildDead => write!(f, "node child process is not running"),
            RequestError::Timeout => {
                write!(f, "request timed out after {REQUEST_TIMEOUT_SECS}s")
            }
            RequestError::Rpc(e) => write!(f, "rpc error {}: {}", e.code, e.message),
            RequestError::Serde(s) => write!(f, "serialization error: {s}"),
        }
    }
}

impl std::error::Error for RequestError {}

impl Connection {
    pub fn new(
        stdin: ChildStdin,
        stdout: ChildStdout,
        app: AppHandle,
        status: StatusArc,
    ) -> Self {
        let pending: PendingMap = Arc::new(Mutex::new(HashMap::new()));
        let (writer_tx, writer_rx) = mpsc::channel::<String>(WRITER_QUEUE_CAPACITY);

        tokio::spawn(writer_loop(stdin, writer_rx));
        tokio::spawn(reader_loop(stdout, pending.clone(), app, status));

        Self {
            next_id: Arc::new(AtomicU64::new(1)),
            pending,
            writer_tx,
        }
    }

    pub async fn request(
        &self,
        method: &str,
        params: Option<Value>,
    ) -> Result<Value, RequestError> {
        let id = self.next_id.fetch_add(1, Ordering::SeqCst);
        let (tx, rx) = oneshot::channel();

        // Insert the pending entry FIRST so a fast response can never race
        // ahead of registration.
        self.pending.lock().await.insert(id, tx);

        let req = Request::new(id, method, params);
        let json_str =
            serde_json::to_string(&req).map_err(|e| RequestError::Serde(e.to_string()))?;

        if self.writer_tx.send(json_str).await.is_err() {
            // Writer task is gone — the child is dead or stdin failed.
            self.pending.lock().await.remove(&id);
            return Err(RequestError::ChildDead);
        }

        match timeout(Duration::from_secs(REQUEST_TIMEOUT_SECS), rx).await {
            Ok(Ok(Ok(value))) => Ok(value),
            Ok(Ok(Err(rpc_err))) => Err(RequestError::Rpc(rpc_err)),
            // oneshot sender dropped without sending — only happens if drain_pending
            // didn't fire, which is a bug. Treat as ChildDead defensively.
            Ok(Err(_)) => Err(RequestError::ChildDead),
            Err(_) => {
                // Timed out — remove our pending entry so a late response is
                // logged-and-dropped, not delivered to a stale receiver.
                self.pending.lock().await.remove(&id);
                Err(RequestError::Timeout)
            }
        }
    }
}

async fn writer_loop(mut stdin: ChildStdin, mut rx: mpsc::Receiver<String>) {
    while let Some(line) = rx.recv().await {
        let payload = format!("{line}\n");
        if let Err(e) = stdin.write_all(payload.as_bytes()).await {
            eprintln!("[jsonrpc] writer write_all failed: {e}");
            break;
        }
        if let Err(e) = stdin.flush().await {
            eprintln!("[jsonrpc] writer flush failed: {e}");
            break;
        }
    }
    // Channel closed (Connection dropped) or write failure → exit task.
}

async fn reader_loop(
    stdout: ChildStdout,
    pending: PendingMap,
    app: AppHandle,
    status: StatusArc,
) {
    let mut lines = BufReader::new(stdout).lines();
    loop {
        match lines.next_line().await {
            Ok(Some(line)) => {
                let trimmed = line.trim();
                if trimmed.is_empty() {
                    continue;
                }
                handle_incoming_line(trimmed, &pending, &app).await;
            }
            Ok(None) => {
                eprintln!("[jsonrpc] child stdout closed (EOF)");
                drain_pending(&pending).await;
                // If shutdown() already set Crashed, transition is a no-op
                // (no spurious event during intentional teardown).
                transition_node_status(&app, &status, NodeSdkStatus::Crashed, None);
                break;
            }
            Err(e) => {
                eprintln!("[jsonrpc] read error: {e}");
                drain_pending(&pending).await;
                transition_node_status(&app, &status, NodeSdkStatus::Crashed, None);
                break;
            }
        }
    }
}

async fn handle_incoming_line(line: &str, pending: &PendingMap, app: &AppHandle) {
    let msg: Incoming = match serde_json::from_str(line) {
        Ok(m) => m,
        Err(e) => {
            eprintln!("[jsonrpc] parse failed: {e}\n  line: {line}");
            return;
        }
    };

    if let Some(id) = msg.id {
        let removed = pending.lock().await.remove(&id);
        let Some(tx) = removed else {
            eprintln!("[jsonrpc] response for unknown or expired id {id}");
            return;
        };

        let payload = if let Some(err) = msg.error {
            Err(err)
        } else if let Some(value) = msg.result {
            Ok(value)
        } else {
            Err(RpcError {
                code: -32603,
                message: "response missing both result and error".to_string(),
                data: None,
            })
        };
        let _ = tx.send(payload);
    } else if let Some(method) = msg.method {
        dispatch_notification(&method, msg.params, app);
    } else {
        eprintln!("[jsonrpc] message has neither id nor method: {line}");
    }
}

async fn drain_pending(pending: &PendingMap) {
    let entries: Vec<_> = {
        let mut map = pending.lock().await;
        map.drain().collect()
    };
    for (_, tx) in entries {
        let _ = tx.send(Err(RpcError {
            code: -32000,
            message: "node child process exited".to_string(),
            data: None,
        }));
    }
}

fn dispatch_notification(method: &str, params: Option<Value>, app: &AppHandle) {
    let payload = params.unwrap_or(Value::Null);
    match method {
        "log" => {
            let _ = app.emit(EVT_NODE_LOG, payload);
        }
        _ => {
            // Generic envelope for unrouted notifications. Feature 02 will
            // replace this with typed dispatches to message-received,
            // ask-user-requested, etc.
            let _ = app.emit(
                EVT_NODE_NOTIFICATION,
                json!({ "method": method, "params": payload }),
            );
        }
    }
}