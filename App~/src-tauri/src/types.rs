// Canonical contract types shared between Rust (Tauri) and TS (React).
// Mirrors src/ipc/types.ts. Edit both sides together when changing.

use serde::{Deserialize, Serialize};
use serde_json::{Map, Value};

// --- Connection ----------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ConnectionStatus {
    Connected,
    Busy,
    Disconnected,
}

// --- Permissions ---------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum PermissionMode {
    Auto,
    Ask,
    Plan,
}

// --- Messages ------------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MessageRole {
    User,
    Assistant,
    System,
}

pub type MessageId = String;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Message {
    pub id: MessageId,
    pub role: MessageRole,
    pub content: String,
    pub timestamp: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub agent: Option<String>,
}

// --- Plans ---------------------------------------------------------------

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PlanMeta {
    pub name: String,
    pub last_modified: i64,
}

// Frontmatter schema is not pinned yet — Feature 06 will tighten it.
pub type PlanFrontmatter = Map<String, Value>;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Plan {
    pub name: String,
    pub last_modified: i64,
    pub content: String,
    pub frontmatter: PlanFrontmatter,
}

// --- Rules ---------------------------------------------------------------

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuleMeta {
    pub name: String,
    pub enabled: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Rule {
    pub name: String,
    pub enabled: bool,
    pub content: String,
}

// --- Settings ------------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Theme {
    Dark,
    Light,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    pub theme: Theme,
    pub unity_project_path: Option<String>,
}

#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettingsPatch {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub theme: Option<Theme>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub unity_project_path: Option<String>,
}

// --- Errors --------------------------------------------------------------

// Tagged enum — JSON shape: { "kind": "<snake_case>", "message": "..." }
// matches TS `{ kind: AppErrorKind, message: string }`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind", content = "message", rename_all = "snake_case")]
pub enum AppError {
    UnityDisconnected(String),
    NodeSdkUnavailable(String),
    FileNotFound(String),
    PermissionDenied(String),
    InvalidInput(String),
    Internal(String),
}

impl std::fmt::Display for AppError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let (kind, msg) = match self {
            AppError::UnityDisconnected(m) => ("unity_disconnected", m),
            AppError::NodeSdkUnavailable(m) => ("node_sdk_unavailable", m),
            AppError::FileNotFound(m) => ("file_not_found", m),
            AppError::PermissionDenied(m) => ("permission_denied", m),
            AppError::InvalidInput(m) => ("invalid_input", m),
            AppError::Internal(m) => ("internal", m),
        };
        write!(f, "{kind}: {msg}")
    }
}

impl std::error::Error for AppError {}