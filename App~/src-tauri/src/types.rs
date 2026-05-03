//! Canonical contract types shared between Rust (Tauri) and TS (React).
//!
//! Mirrors `src/ipc/types.ts`. Edit both sides together when changing.

use serde::{Deserialize, Serialize};
use serde_json::{Map, Value};

// region: Connection

/// State of the Unity Editor connection from the Tauri host's perspective.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum ConnectionStatus {
    Connected,
    Busy,
    Disconnected,
}

// endregion

// region: Install detection

/// Snapshot of the local environment's readiness to run Claude Code.
///
/// Populated by `claude_supervisor::install_check::check_install_status`
/// and surfaced to React via
/// `commands::install::check_claude_install_status`. A field set to
/// `false` (or `None` for `claude_version`) means either the dependency
/// is missing OR the detection probe failed — the React side treats
/// both cases identically and surfaces the appropriate next step.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ClaudeInstallStatus {
    pub claude_installed: bool,
    pub claude_authenticated: bool,
    pub sdk_installed: bool,
    pub claude_version: Option<String>,
}

// endregion

// region: Permissions

/// Permission policy applied to tool calls issued by the agent.
///
/// Mirrors the five surface-level modes the Claude Code chat exposes
/// (`default` / `acceptEdits` / `plan` / `bypassPermissions` / `auto`).
/// `Auto` is a UI alias for `BypassPermissions` (CLAUDE.md gotcha:
/// "Auto permission mode: Uses bypassPermissions, NOT acceptEdits");
/// `sdk-entry.js::resolveSdkMode` performs that mapping before
/// passing the mode to the SDK.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum PermissionMode {
    Default,
    AcceptEdits,
    Plan,
    BypassPermissions,
    Auto,
}

// endregion

// region: Messages

/// Speaker role for a single chat message.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MessageRole {
    User,
    Assistant,
    System,
}

/// Stable identifier for a message within a conversation.
pub type MessageId = String;

/// A single chat message exchanged with the agent.
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

/// A single content block inside a `LoadedMessage` — mirrors React's
/// `Block` union so the frontend can render session history without
/// any further translation. Tagged on the wire by `type` field
/// (`text` / `tool-use` / `tool-result`).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "kebab-case", rename_all_fields = "camelCase")]
pub enum LoadedBlock {
    Text {
        text: String,
    },
    ToolUse {
        tool_use_id: String,
        name: String,
        input: Value,
    },
    ToolResult {
        tool_use_id: String,
        content: Value,
        is_error: bool,
    },
}

/// A single chat message reconstructed from Claude Code's JSONL
/// session storage. Mirrors the React-side `Message` shape exactly
/// (`{id, role, timestamp, blocks}`) so `commands::sessions::
/// get_session_messages` can hand the array straight to
/// `conversationStore.loadHistory`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LoadedMessage {
    pub id: MessageId,
    pub role: MessageRole,
    pub timestamp: i64,
    pub blocks: Vec<LoadedBlock>,
}

/// Lightweight summary of a Claude Code session, derived from the
/// JSONL file at `<home>/.claude/projects/<encoded-cwd>/<id>.jsonl`.
/// `title` is the first user prompt's leading line, trimmed of
/// `<command-message>` wrappers and truncated for the sidebar.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SessionSummary {
    pub id: String,
    pub title: String,
    pub last_modified: i64,
    pub message_count: usize,
}

// endregion

// region: Plans

/// Lightweight metadata for a plan file (used in list views).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PlanMeta {
    pub name: String,
    pub last_modified: i64,
}

/// Free-form frontmatter map for plan documents.
///
/// Schema is not pinned yet — Feature 06 will tighten it.
pub type PlanFrontmatter = Map<String, Value>;

/// Full contents of a plan, including its parsed frontmatter and body.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Plan {
    pub name: String,
    pub last_modified: i64,
    pub content: String,
    pub frontmatter: PlanFrontmatter,
}

// endregion

// region: Rules

/// Lightweight metadata for a rule file (used in list views).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuleMeta {
    pub name: String,
    pub enabled: bool,
}

/// Full contents of a rule, including its activation flag and body.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Rule {
    pub name: String,
    pub enabled: bool,
    pub content: String,
}

// endregion

// region: Settings

/// User-selected color theme.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Theme {
    Dark,
    Light,
}

/// Persistent application settings.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettings {
    pub theme: Theme,
    pub unity_project_path: Option<String>,
}

/// Partial settings update — every field is optional and `None` means "leave unchanged".
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AppSettingsPatch {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub theme: Option<Theme>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub unity_project_path: Option<String>,
}

// endregion

// region: Events
// Payloads emitted by Rust → React via Tauri events. Names mirror the
// emitter helpers in events.rs and the TS payload types in src/ipc/types.ts.

/// Payload for `unity-status-changed`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UnityStatusChangedPayload {
    pub status: ConnectionStatus,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub reason: Option<String>,
}

/// Lifecycle state of the bundled Node.js Agent SDK process.
///
/// Distinct from `ConnectionStatus` — the Node SDK has its own state machine.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum NodeSdkStatus {
    Starting,
    Running,
    Crashed,
}

/// Payload for `node-sdk-status-changed`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct NodeSdkStatusChangedPayload {
    pub status: NodeSdkStatus,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub pid: Option<u32>,
}

/// Payload for `message-stream-chunk` — incremental token delivery for an in-flight message.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MessageStreamChunkPayload {
    pub message_id: MessageId,
    pub chunk: String,
}

/// Payload for `message-stream-complete` — emitted once when streaming finishes.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MessageStreamCompletePayload {
    pub message_id: MessageId,
}

/// Shape of the answer the agent expects from the user.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum AskUserType {
    Single,
    Multi,
    FreeText,
}

/// Payload for `ask-user-requested`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AskUserRequestedPayload {
    pub question_id: String,
    pub question: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub options: Option<Vec<String>>,
    // `type` is a reserved word in Rust — rename on the wire.
    #[serde(rename = "type")]
    pub kind: AskUserType,
}

/// Payload for `permission-requested`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PermissionRequestedPayload {
    pub request_id: String,
    pub tool: String,
    pub params: Value,
}

/// Payload for `route-requested` — single-instance callback asking the running
/// window to navigate after a re-launch carrying a `--route=/path` CLI arg.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RouteRequestedPayload {
    pub route: String,
}

/// Payload for `permission-mode-changed` — fired whenever the
/// supervisor's permission mode is updated (echo from `sdk-entry.js`
/// after applying a `setPermissionMode` control message; future
/// SDK-driven cycles such as Shift+Tab in task 4.3 reuse the same
/// channel).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PermissionModeChangedPayload {
    pub mode: PermissionMode,
}

/// Tagged message envelope sent by `sdk-entry.js` over stdout, then
/// re-emitted to React via the `agent-message` Tauri event.
///
/// added `TextDelta` for streaming and gave
/// `AssistantTurnComplete` a `turn_id`. `AssistantText` is kept as a
/// legacy variant with no producer in 2.3+ — preserved so the wire
/// shape stays additive across feature cycles.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "kebab-case", rename_all_fields = "camelCase")]
pub enum AgentMessage {
    Ready,
    AssistantText { text: String },
    TextDelta {
        turn_id: String,
        text: String,
    },
    ToolUse {
        turn_id: String,
        tool_use_id: String,
        name: String,
        input: Value,
    },
    ToolResult {
        turn_id: String,
        tool_use_id: String,
        content: Value,
        is_error: bool,
    },
    AssistantTurnComplete {
        turn_id: String,
    },
    Error {
        message: String,
    },
    PermissionModeChanged {
        mode: PermissionMode,
    },
    HealthOk,
    HealthFailed {
        message: String,
    },
}

/// Wire payload for `agent-message` — wraps an `AgentMessage` in a
/// `{message: ...}` object so future fields (timestamps, ids) can
/// be added without re-shaping every variant.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AgentMessagePayload {
    pub message: AgentMessage,
}

/// Lifecycle state of the Claude Code supervisor.
///
/// `Failed` and `Crashed` are intentionally distinct: `Failed` means
/// spawn never reached `Ready` (SDK missing, exec error, env issue —
/// requires user action, surface FirstRunPanel-like UX); `Crashed`
/// means a previously-Ready child died unexpectedly (recoverable via
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum SupervisorStatus {
    Idle,
    Starting,
    Ready,
    Crashed,
    Failed,
}

/// Payload for `supervisor-status-changed`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SupervisorStatusChangedPayload {
    pub status: SupervisorStatus,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub pid: Option<u32>,
}

/// Payload for `sdk-install-progress` — emitted while
/// `npm install @anthropic-ai/claude-agent-sdk` runs. `percent: None`
/// signals indeterminate progress (npm output isn't reliably
/// parseable for a numeric percent); React falls back to a pulse
/// animation.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SdkInstallProgressPayload {
    pub percent: Option<f64>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub message: Option<String>,
}

/// Payload for `sdk-install-failed`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SdkInstallFailedPayload {
    pub message: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub exit_code: Option<i32>,
}

// endregion

// region: Errors

/// Tagged error type sent to the frontend.
///
/// The wire format is `{ "kind": "<snake_case>", "message": "..." }`,
/// matching the TS `{ kind: AppErrorKind, message: string }` shape.
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

// endregion