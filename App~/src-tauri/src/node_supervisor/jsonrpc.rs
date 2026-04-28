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

use crate::events::{emit_message_received, emit_node_sdk_status_changed};
use crate::types::{Message, NodeSdkStatus, NodeSdkStatusChangedPayload};

use super::protocol::{Incoming, Request, RpcError};

// region: Constants

/// Per-request timeout. Late responses are dropped after this elapses.
const REQUEST_TIMEOUT_SECS: u64 = 30;

/// Buffer capacity of the writer's mpsc queue. Backpressures `request`
/// callers when the child can't keep up with stdin writes.
const WRITER_QUEUE_CAPACITY: usize = 64;

/// Tauri event name for forwarded `log` notifications from the Node SDK.
const EVT_NODE_LOG: &str = "node-log";

/// Tauri event name for the catch-all notification envelope.
const EVT_NODE_NOTIFICATION: &str = "node-notification";

// endregion

// region: Type aliases

/// Maps in-flight request ids to the oneshot that will deliver their reply.
/// Async mutex because both `request` (insert/remove) and the reader loop
/// (remove on response) need it across awaits.
type PendingMap = Arc<Mutex<HashMap<u64, oneshot::Sender<Result<Value, RpcError>>>>>;

/// Shared handle to the supervisor's `NodeSdkStatus`. Sync mutex — locks
/// must never be held across an `await` point.
pub type StatusArc = Arc<StdMutex<NodeSdkStatus>>;

// endregion

// region: Status transition

/// Atomically swaps the Node SDK status and emits `node-sdk-status-changed`
/// iff the value actually changed.
///
/// Idempotent: no event if already at the target status. Used by both the
/// supervisor (Starting/Running) and the reader loop (Crashed on EOF), so
/// both paths see identical state-machine semantics.
///
/// # Arguments
///
/// * `app` - Application handle used to emit the status event.
/// * `status_arc` - Shared status cell to update.
/// * `new_status` - Target status.
/// * `pid` - Optional OS process id, included in the event payload.
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

// endregion

// region: Errors

/// Failure modes for `Connection::request`.
#[derive(Debug)]
pub enum RequestError {
    ChildDead,
    Timeout,
    Rpc(RpcError),
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

// endregion

// region: Connection

/// JSON-RPC framing layer wired to a child process's stdio.
///
/// Owns the request id source, the pending-response table, and the writer
/// queue. Cloning is cheap and shares the same underlying tasks — drop the
/// last clone to tear the connection down.
#[derive(Clone)]
pub struct Connection {
    next_id: Arc<AtomicU64>,
    pending: PendingMap,
    writer_tx: mpsc::Sender<String>,
}

impl Connection {
    /// Builds a new connection and spawns its reader + writer tasks.
    ///
    /// # Arguments
    ///
    /// * `stdin` - Child stdin pipe; consumed by the writer task.
    /// * `stdout` - Child stdout pipe; consumed by the reader task.
    /// * `app` - Application handle used to forward notifications and emit
    ///   status changes on EOF.
    /// * `status` - Shared status cell, transitioned to `Crashed` on EOF.
    ///
    /// # Returns
    ///
    /// A `Connection` value. The two background tasks are already running
    /// when this returns.
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

    /// Sends a JSON-RPC request and awaits the matching response.
    ///
    /// # Arguments
    ///
    /// * `method` - JSON-RPC method name.
    /// * `params` - Optional `params` value (omitted from the wire when `None`).
    ///
    /// # Returns
    ///
    /// The unwrapped `result` value on success.
    ///
    /// # Errors
    ///
    /// See `RequestError` for the full taxonomy. A timed-out or dead-child
    /// request always cleans up its pending entry before returning.
    pub async fn request(
        &self,
        method: &str,
        params: Option<Value>,
    ) -> Result<Value, RequestError> {
        let id = self.next_id.fetch_add(1, Ordering::SeqCst);
        let (tx, rx) = oneshot::channel();

        self.pending.lock().await.insert(id, tx);

        let req = Request::new(id, method, params);
        let json_str =
            serde_json::to_string(&req).map_err(|e| RequestError::Serde(e.to_string()))?;

        if self.writer_tx.send(json_str).await.is_err() {
            self.pending.lock().await.remove(&id);
            return Err(RequestError::ChildDead);
        }

        match timeout(Duration::from_secs(REQUEST_TIMEOUT_SECS), rx).await {
            Ok(Ok(Ok(value))) => Ok(value),
            Ok(Ok(Err(rpc_err))) => Err(RequestError::Rpc(rpc_err)),
            Ok(Err(_)) => Err(RequestError::ChildDead),
            Err(_) => {
                self.pending.lock().await.remove(&id);
                Err(RequestError::Timeout)
            }
        }
    }
}

// endregion

// region: Background tasks

/// Drains the writer queue, writing newline-delimited JSON to child stdin.
///
/// Exits on the first write/flush failure or when the channel is closed
/// (last `Connection` clone dropped).
///
/// # Arguments
///
/// * `stdin` - Child stdin pipe (owned).
/// * `rx` - Receiver end of the writer queue.
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

/// Reads newline-delimited JSON from child stdout, dispatching responses
/// and notifications until EOF or read error.
///
/// On EOF/error the reader drains every pending request with `ChildDead`
/// and transitions status to `Crashed`.
///
/// # Arguments
///
/// * `stdout` - Child stdout pipe (owned).
/// * `pending` - Pending-response table; drained on exit.
/// * `app` - Application handle used to emit notifications and status events.
/// * `status` - Shared status cell, transitioned to `Crashed` on exit.
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

/// Parses a single incoming line and routes it to the right handler.
///
/// Lines with `id` are treated as responses; lines with `method` and no
/// `id` as notifications. Malformed JSON is logged and skipped.
///
/// # Arguments
///
/// * `line` - Trimmed input line (caller has stripped whitespace).
/// * `pending` - Pending-response table.
/// * `app` - Application handle used to emit notifications.
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

/// Rejects every pending request with `ChildDead` and clears the table.
///
/// Called when the reader exits (EOF/error) so request callers don't hang
/// waiting on a child that will never reply.
///
/// # Arguments
///
/// * `pending` - Pending-response table to drain.
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

/// Routes a JSON-RPC notification to the appropriate Tauri event.
///
/// Known methods are mapped to typed events (`log` → `node-log`,
/// `message/received` → `message-received`); unknown methods fall back to
/// the generic `node-notification` envelope.
///
/// # Arguments
///
/// * `method` - JSON-RPC method name from the notification.
/// * `params` - Notification payload (`null` when missing).
/// * `app` - Application handle used to emit the event.
fn dispatch_notification(method: &str, params: Option<Value>, app: &AppHandle) {
    let payload = params.unwrap_or(Value::Null);
    match method {
        "log" => {
            let _ = app.emit(EVT_NODE_LOG, payload);
        }
        "message/received" => match serde_json::from_value::<Message>(payload.clone()) {
            Ok(msg) => {
                let _ = emit_message_received(app, msg);
            }
            Err(e) => {
                eprintln!("[jsonrpc] message/received bad payload: {e}");
                let _ = app.emit(
                    EVT_NODE_NOTIFICATION,
                    json!({ "method": method, "params": payload }),
                );
            }
        },
        _ => {
            let _ = app.emit(
                EVT_NODE_NOTIFICATION,
                json!({ "method": method, "params": payload }),
            );
        }
    }
}

// endregion