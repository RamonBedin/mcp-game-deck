//! JSON-RPC 2.0 message types for stdio framing with the Node Agent SDK.
//!       § "Tauri ↔ Node Agent SDK protocol"

use serde::{Deserialize, Serialize};
use serde_json::Value;

// region: Constants

/// JSON-RPC version literal — pinned to 2.0 on every outgoing request.
const JSONRPC_VERSION: &str = "2.0";

// endregion

// region: Outgoing

/// Outgoing request from Tauri → Node SDK.
#[derive(Debug, Serialize)]
pub struct Request {
    pub jsonrpc: &'static str,
    pub id: u64,
    pub method: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub params: Option<Value>,
}

impl Request {
    /// Builds a JSON-RPC request with the version literal pre-filled.
    ///
    /// # Arguments
    ///
    /// * `id` - Monotonic request id supplied by the caller.
    /// * `method` - JSON-RPC method name (any `Into<String>`).
    /// * `params` - Optional `params` value.
    ///
    /// # Returns
    ///
    /// A new `Request` ready to be serialized and written to the wire.
    pub fn new(id: u64, method: impl Into<String>, params: Option<Value>) -> Self {
        Self {
            jsonrpc: JSONRPC_VERSION,
            id,
            method: method.into(),
            params,
        }
    }
}

// endregion

// region: Incoming

/// JSON-RPC 2.0 error object.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RpcError {
    pub code: i64,
    pub message: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub data: Option<Value>,
}

/// Generic incoming message from Node SDK. May be either:
/// - a response (`id` present, plus `result` xor `error`), or
/// - a notification (`method` present, no `id`).
///
/// Parsed permissively so a malformed message logs a warning instead of
/// crashing the reader task.
#[derive(Debug, Deserialize)]
pub struct Incoming {
    #[serde(default)]
    #[allow(dead_code)]
    pub jsonrpc: Option<String>,
    #[serde(default)]
    pub id: Option<u64>,
    #[serde(default)]
    pub method: Option<String>,
    #[serde(default)]
    pub params: Option<Value>,
    #[serde(default)]
    pub result: Option<Value>,
    #[serde(default)]
    pub error: Option<RpcError>,
}

// endregion