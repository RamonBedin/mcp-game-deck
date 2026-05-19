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
///
/// `last_modified` is the file's mtime in **milliseconds since the
/// Unix epoch** — matches `SessionSummary.last_modified` and the
/// React side's `Date.now()`-based math (no compensating `* 1000` in
/// the UI). `description` is convenience-extracted from the file's
/// YAML frontmatter so the list view doesn't have to read every
/// plan's full body. Falls back to `None` when the field is absent,
/// blank, or the frontmatter is malformed.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PlanMeta {
    pub name: String,
    pub last_modified: i64,
    pub description: Option<String>,
}

/// Free-form frontmatter map for plan documents.
///
/// Schema is not pinned yet — Feature 06 will tighten it.
pub type PlanFrontmatter = Map<String, Value>;

/// Full contents of a plan, including its parsed frontmatter and body.
///
/// `last_modified` is the file's mtime in **milliseconds since the
/// Unix epoch**, matching [`PlanMeta`].
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Plan {
    pub name: String,
    pub last_modified: i64,
    pub content: String,
    pub frontmatter: PlanFrontmatter,
}

/// Kind of filesystem change emitted by `plans-changed`.
///
/// Synthesized in `plans_watcher::classify_event` by comparing the
/// in-memory set of known names against `path.exists()` at delivery
/// time — `notify-debouncer-mini` collapses native event kinds into
/// `DebouncedEventKind::Any`, so the create/modify/delete distinction
/// is reconstructed rather than carried from the OS.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum PlansChangedKind {
    Created,
    Modified,
    Deleted,
}

/// Payload for `plans-changed` — emitted by the plans-directory file
/// watcher whenever a `.md` file is created, modified, or deleted.
///
/// `name` is the file stem (no `.md` extension); `None` if the watcher
/// can't extract a name (e.g. non-UTF8 path).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PlansChangedPayload {
    pub kind: PlansChangedKind,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
}

// endregion

// region: Files

/// Kind of entry returned by `list_project_files` — directories are
/// included so the user can `@SomeFolder/` to reference a folder in
/// the `@` picker.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum FileKind {
    File,
    Directory,
}

/// One entry in the project file index, relative to `UNITY_PROJECT_PATH`.
///
/// `path` is normalized to forward slashes regardless of OS so the
/// React side can treat it as a URL-style identifier without
/// platform-conditional parsing.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct FileIndexEntry {
    pub path: String,
    pub kind: FileKind,
}

/// Payload for `project-files-changed` — emitted by the files-root
/// watcher whenever a path under the active project changes.
///
/// `debounced` is `true` when the notify-debouncer-mini batch carried
/// more than one event (multiple FS changes coalesced into a single
/// emit). React's `useProjectFiles` ignores the flag and re-fetches
/// the full index on every event — the field exists for diagnostics
/// and future v2.1 throttling experiments.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ProjectFilesChangedPayload {
    pub debounced: bool,
}

// endregion

// region: Catalog

/// Source classification for a slash command, mirrored to React for
/// the slash dropdown  Built-ins are the Claude Code
/// CLI's first-party commands; user-commands live under the project's
/// `ProjectSettings/GameDeck/commands/`; plugin commands come from
/// this package (`mcp-game-deck:` prefix); third-party covers any
/// other namespaced prefix.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum CommandSource {
    BuiltIn,
    UserCommand,
    Plugin,
    ThirdParty,
}

/// Source classification for an agent (subagent), mirrored to React
/// for the `@` picker (F06 group 6). Same prefix scheme as
/// `CommandSource` minus the `user-command` variant — agents don't
/// have a per-project user-authored equivalent today.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum AgentSource {
    BuiltIn,
    Plugin,
    ThirdParty,
}

/// One entry in the `catalog-ready` agent message's `commands` array.
///
/// `argument_hint` is optional and omitted from the wire when absent;
/// it mirrors the `argument-hint` field of a SKILL.md frontmatter.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CatalogCommand {
    pub name: String,
    pub description: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub argument_hint: Option<String>,
    pub source: CommandSource,
}

/// One entry in the `catalog-ready` agent message's `agents` array.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CatalogAgent {
    pub name: String,
    pub description: String,
    pub source: AgentSource,
}

// endregion

// region: Rules

/// Lightweight metadata for a rule file (used in list views).
///
/// `last_modified` is in **milliseconds since the Unix epoch**
/// (matches [`PlanMeta`] and [`SessionSummary`]). `description` and
/// `applies_to` are convenience-extracted from the file's YAML
/// frontmatter so the list view doesn't have to read every rule's
/// full body. `estimated_tokens` is a chars/4 heuristic computed
/// from the full file content (frontmatter + body) so the Rules
/// tab's header can show the bundle cost at a glance.
///
/// `applies_to` is **informational only** in v2.0 — the bundle
/// compiler ignores it; v2.1 may filter per-subagent.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RuleMeta {
    pub name: String,
    pub last_modified: i64,
    pub enabled: bool,
    pub description: Option<String>,
    pub applies_to: Vec<String>,
    pub estimated_tokens: u32,
}

/// Free-form frontmatter map for rule documents.
///
/// Schema is intentionally open — v2.0 reads `enabled` / `description`
/// / `applies-to` (see [`RuleMeta`]), but writes preserve unknown
/// fields verbatim so user-authored frontmatter round-trips through
/// toggles and edits (F08 task 2.5).
pub type RuleFrontmatter = Map<String, Value>;

/// Full contents of a rule, including its parsed frontmatter and
/// body.
///
/// `last_modified` is in **milliseconds since the Unix epoch**
/// (matches [`RuleMeta`] / [`PlanMeta`] / [`SessionSummary`]).
/// `content` is the body **without** `---` delimiters. The full
/// frontmatter map is surfaced separately so the React pane can
/// render the `applies-to` chip strip and so v2.1+ surfaces can
/// expand the schema without re-shaping `Rule`.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Rule {
    pub name: String,
    pub last_modified: i64,
    pub content: String,
    pub frontmatter: RuleFrontmatter,
    pub estimated_tokens: u32,
}

/// Kind of filesystem change emitted by `rules-changed`.
///
/// Synthesized in `rules_watcher::classify_event` by comparing the
/// in-memory set of known names against `path.exists()` at delivery
/// time — `notify-debouncer-mini` collapses native event kinds into
/// `DebouncedEventKind::Any`, so the create/modify/delete distinction
/// is reconstructed rather than carried from the OS.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum RulesChangedKind {
    Created,
    Modified,
    Deleted,
}

/// Payload for `rules-changed` — emitted by the rules-directory file
/// watcher whenever a `.md` file is created, modified, or deleted.
///
/// `name` is the file stem (no `.md` extension); `None` if the watcher
/// can't extract a name (e.g. non-UTF8 path).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RulesChangedPayload {
    pub kind: RulesChangedKind,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub name: Option<String>,
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
    AskUserRequested {
        request_id: String,
        turn_id: String,
        agent_id: Option<String>,
        input: Value,
    },
    PermissionRequested {
        request_id: String,
        turn_id: String,
        agent_id: Option<String>,
        tool_name: String,
        input: Value,
        blocked_path: Option<String>,
        decision_reason: Option<String>,
    },
    RequestResolved {
        request_id: String,
        outcome: String,
        answer: Option<Value>,
        tool_name: Option<String>,
        turn_id: Option<String>,
    },
    HealthOk,
    HealthFailed {
        message: String,
    },
    CatalogReady {
        commands: Vec<CatalogCommand>,
        agents: Vec<CatalogAgent>,
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

/// Payload for `claude-version-out-of-range` — emitted once per
/// supervisor startup when the local `claude --version` falls outside
/// the smoke-tested range advertised by `package.json`'s `claudeCode`
/// field. Surfaces a non-blocking banner; the app keeps running on the
/// detected version.
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ClaudeVersionOutOfRangePayload {
    pub detected: String,
    pub supported: String,
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

// region: canUseTool decisions

/// User-side outcome for a permission card. Wire format is kebab-case
/// (`"allow"` / `"allow-always"` / `"deny"`) so the JSON line
/// `sdk-entry.js`'s stdin handler decodes (task 1.3) maps cleanly to
/// this enum.
///
/// `auto-allowed` is intentionally absent — that outcome is synthesized
/// inside the supervisor on Allow Always cache hits (task 1.4) and is
/// never sent from the host into the supervisor.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum PermissionOutcome {
    Allow,
    AllowAlways,
    Deny,
}

/// Payload of `respond_to_request` (task 2.2) — the user's response
/// to a permission card or a question card. Discriminated by `kind`
/// (`"permission"` carries an outcome, `"question"` carries an
/// `AskUserQuestionOutput`-shaped answer). Routed to the supervisor
/// via stdin and dispatched in `sdk-entry.js`'s
/// `respond-to-request` branch (task 1.3).
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum DecisionPayload {
    Permission { outcome: PermissionOutcome },
    Question { answer: Value },
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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ask_user_requested_serializes_kebab() {
        let m = AgentMessage::AskUserRequested {
            request_id: "tu_123".into(),
            turn_id: "t_1".into(),
            agent_id: None,
            input: serde_json::json!({ "questions": [] }),
        };
        let json = serde_json::to_string(&m).unwrap();
        assert!(json.contains("\"type\":\"ask-user-requested\""));
        assert!(json.contains("\"requestId\":\"tu_123\""));
    }

    #[test]
    fn plans_changed_payload_serializes_kebab_kind() {
        let p = PlansChangedPayload {
            kind: PlansChangedKind::Created,
            name: Some("foo".into()),
        };
        let json = serde_json::to_string(&p).unwrap();
        assert!(json.contains("\"kind\":\"created\""));
        assert!(json.contains("\"name\":\"foo\""));
    }

    #[test]
    fn rules_changed_payload_serializes_kebab_kind() {
        let p = RulesChangedPayload {
            kind: RulesChangedKind::Created,
            name: Some("foo".into()),
        };
        let json = serde_json::to_string(&p).unwrap();
        assert!(json.contains("\"kind\":\"created\""));
        assert!(json.contains("\"name\":\"foo\""));
    }

    #[test]
    fn catalog_ready_serializes_kebab() {
        let m = AgentMessage::CatalogReady {
            commands: vec![CatalogCommand {
                name: "save-plan".into(),
                description: "Save a plan.".into(),
                argument_hint: Some("[plan-name]".into()),
                source: CommandSource::Plugin,
            }],
            agents: vec![CatalogAgent {
                name: "my-agent".into(),
                description: "Test agent.".into(),
                source: AgentSource::BuiltIn,
            }],
        };
        let json = serde_json::to_string(&m).unwrap();
        assert!(json.contains("\"type\":\"catalog-ready\""));
        assert!(json.contains("\"source\":\"plugin\""));
        assert!(json.contains("\"source\":\"built-in\""));
        assert!(json.contains("\"argumentHint\":\"[plan-name]\""));
    }
}