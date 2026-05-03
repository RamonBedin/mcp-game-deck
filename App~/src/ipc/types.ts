/**
 * Canonical contract types shared between Rust (Tauri) and TS (React).
 *
 * Mirrors `src-tauri/src/types.rs` (task 2.2). Edit both sides together
 * when changing.
 */

// #region Connection

/** State of the Unity Editor connection from the Tauri host's perspective. */
export type ConnectionStatus = "connected" | "busy" | "disconnected";

// #endregion

// #region Install detection

/**
 * Snapshot of the local environment's readiness to run Claude Code.
 *
 * Populated by the `check_claude_install_status` Tauri command. A `false`
 * (or `null` for `claudeVersion`) field means either the dependency is
 * missing OR the detection probe failed — the FirstRunPanel treats both
 * cases identically and surfaces the appropriate next step.
 */
export interface ClaudeInstallStatus
{
  claudeInstalled: boolean;
  claudeAuthenticated: boolean;
  sdkInstalled: boolean;
  claudeVersion: string | null;
}

// #endregion

// #region Permissions

/**
 * Permission policy applied to tool calls issued by the agent.
 *
 * Mirrors the five surface-level modes the Claude Code chat exposes.
 * `auto` is a UI alias for `bypassPermissions` (CLAUDE.md gotcha);
 * the JS side maps it via `resolveSdkMode` before reaching the SDK.
 */
export type PermissionMode =
  | "default"
  | "acceptEdits"
  | "plan"
  | "bypassPermissions"
  | "auto";

// #endregion

// #region Messages

/** Speaker role for a single chat message. */
export type MessageRole = "user" | "assistant" | "system";

/** Stable identifier for a message within a conversation. */
export type MessageId = string;

/**
 * Discriminated union of block types that can appear inside a chat
 * `Message`. introduced this shape so tool calls
 * interleave with assistant text in display order.
 *
 * - `text` — streamed assistant text or static system/user content.
 * - `tool-use` — Claude is calling an MCP tool; pre-permission display.
 * - `tool-result` — the tool returned (or errored); raw payload kept,
 *   UI truncates display via scrollable container.
 */
export type Block =
  | { type: "text"; text: string }
  | { type: "tool-use"; toolUseId: string; name: string; input: unknown }
  | { type: "tool-result"; toolUseId: string; content: unknown; isError: boolean };

/**
 * A single chat message exchanged with the agent. Content lives in
 * `blocks` (introduced in task 2.4); the F01 `content: string` field
 * was dropped — `Message` is now block-based.
 */
export interface Message
{
  id: MessageId;
  role: MessageRole;
  timestamp: number;
  blocks: Block[];
  agent?: string;
}

// #endregion

// #region Sessions

/**
 * Lightweight summary of a Claude Code session, surfaced in the
 * SessionList sidebar. Reads from
 * `<home>/.claude/projects/<encoded-cwd>/<id>.jsonl` — Decision #6.
 */
export interface SessionSummary
{
  id: string;
  title: string;
  lastModified: number;
  messageCount: number;
}

// #endregion

// #region Plans

/** Lightweight metadata for a plan file (used in list views). */
export interface PlanMeta
{
  name: string;
  lastModified: number;
}

/**
 * Free-form frontmatter map for plan documents.
 *
 * Schema is not pinned yet — Feature 06 will tighten it.
 */
export type PlanFrontmatter = Record<string, unknown>;

/** Full contents of a plan, including its parsed frontmatter and body. */
export interface Plan extends PlanMeta
{
  content: string;
  frontmatter: PlanFrontmatter;
}

// #endregion

// #region Rules

/** Lightweight metadata for a rule file (used in list views). */
export interface RuleMeta
{
  name: string;
  enabled: boolean;
}

/** Full contents of a rule, including its activation flag and body. */
export interface Rule extends RuleMeta
{
  content: string;
}

// #endregion

// #region Settings

/** User-selected color theme. */
export type Theme = "dark" | "light";

/** Persistent application settings. */
export interface AppSettings
{
  theme: Theme;
  unityProjectPath: string | null;
}

/** Partial settings update — every field is optional and missing means "leave unchanged". */
export type AppSettingsPatch = Partial<AppSettings>;

// #endregion

// #region Events

/** Payload for `unity-status-changed`. */
export interface UnityStatusChangedPayload
{
  status: ConnectionStatus;
  reason?: string;
}

/**
 * Lifecycle state of the Claude Code supervisor.
 *
 * `failed` and `crashed` are intentionally distinct: `failed` means
 * spawn never reached `ready` (SDK missing, exec error — needs user
 * action); `crashed` means a previously-ready child died (recoverable
 * via Restart).
 */
export type SupervisorStatus = "idle" | "starting" | "ready" | "crashed" | "failed";

/** Payload for `supervisor-status-changed`. */
export interface SupervisorStatusChangedPayload
{
  status: SupervisorStatus;
  pid?: number;
}

/** Payload for `message-stream-chunk` — incremental token delivery for an in-flight message. */
export interface MessageStreamChunkPayload
{
  messageId: MessageId;
  chunk: string;
}

/** Payload for `message-stream-complete` — emitted once when streaming finishes. */
export interface MessageStreamCompletePayload
{
  messageId: MessageId;
}

/** Shape of the answer the agent expects from the user. */
export type AskUserType = "single" | "multi" | "free-text";

/** Payload for `ask-user-requested`. */
export interface AskUserRequestedPayload
{
  questionId: string;
  question: string;
  options?: string[];
  type: AskUserType;
}

/** Payload for `permission-requested`. */
export interface PermissionRequestedPayload
{
  requestId: string;
  tool: string;
  params: unknown;
}

/**
 * Diagnostic — used by the Group 3 stub. The real Node SDK will emit
 * typed events instead (message-received, ask-user-requested, etc).
 */
export interface NodeLogPayload
{
  level: "info" | "warn" | "error";
  text: string;
}

/**
 * Payload for `route-requested` — single-instance callback asking the running
 * window to navigate after a re-launch carrying a `--route=/path` CLI arg.
 */
export interface RouteRequestedPayload
{
  route: string;
}

/**
 * Payload for `sdk-install-progress` — emitted by Rust while
 * `npm install @anthropic-ai/claude-agent-sdk` runs on first launch
 * `percent: null` signals indeterminate progress (npm
 * output couldn't be parsed for a numeric percentage); the
 * FirstRunPanel falls back to a pulse animation in that case.
 */
export interface SdkInstallProgressPayload {
  percent: number | null;
  message?: string;
}

/**
 * Payload for `sdk-install-failed` — last few stderr lines plus the
 * npm exit code (when known). Surfaced verbatim in the Retry card.
 */
export interface SdkInstallFailedPayload
{
  message: string;
  exitCode?: number;
}

/**
 * Tagged message envelope emitted by `sdk-entry.js` and re-emitted
 * to React via the `agent-message` Tauri event.
 *
 * added `text-delta` for streaming and gave
 * `assistant-turn-complete` a `turnId`. `assistant-text` is kept as
 * a legacy variant with no producer in 2.3+ — preserved so the wire
 * shape stays additive across feature cycles.
 */
export type AgentMessage =
  | { type: "ready" }
  | { type: "assistant-text"; text: string }
  | { type: "text-delta"; turnId: string; text: string }
  | { type: "tool-use"; turnId: string; toolUseId: string; name: string; input: unknown }
  | { type: "tool-result"; turnId: string; toolUseId: string; content: unknown; isError: boolean }
  | { type: "assistant-turn-complete"; turnId: string }
  | { type: "error"; message: string }
  | { type: "permission-mode-changed"; mode: PermissionMode };

/** Wire payload for `agent-message`. */
export interface AgentMessagePayload
{
  message: AgentMessage;
}

/** Payload for `permission-mode-changed`. */
export interface PermissionModeChangedPayload
{
  mode: PermissionMode;
}

// #endregion

// #region Errors

/** Discriminator tag for `AppError`. */
export type AppErrorKind =
  | "unity_disconnected"
  | "node_sdk_unavailable"
  | "file_not_found"
  | "permission_denied"
  | "invalid_input"
  | "internal";

/** Tagged error type received from Tauri command failures. */
export interface AppError
{
  kind: AppErrorKind;
  message: string;
}

// #endregion