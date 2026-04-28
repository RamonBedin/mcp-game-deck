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

// #region Permissions

/** Permission policy applied to tool calls issued by the agent. */
export type PermissionMode = "auto" | "ask" | "plan";

// #endregion

// #region Messages

/** Speaker role for a single chat message. */
export type MessageRole = "user" | "assistant" | "system";

/** Stable identifier for a message within a conversation. */
export type MessageId = string;

/** A single chat message exchanged with the agent. */
export interface Message {
  id: MessageId;
  role: MessageRole;
  content: string;
  timestamp: number;
  agent?: string;
}

// #endregion

// #region Plans

/** Lightweight metadata for a plan file (used in list views). */
export interface PlanMeta {
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
export interface Plan extends PlanMeta {
  content: string;
  frontmatter: PlanFrontmatter;
}

// #endregion

// #region Rules

/** Lightweight metadata for a rule file (used in list views). */
export interface RuleMeta {
  name: string;
  enabled: boolean;
}

/** Full contents of a rule, including its activation flag and body. */
export interface Rule extends RuleMeta {
  content: string;
}

// #endregion

// #region Settings

/** User-selected color theme. */
export type Theme = "dark" | "light";

/** Persistent application settings. */
export interface AppSettings {
  theme: Theme;
  unityProjectPath: string | null;
}

/** Partial settings update — every field is optional and missing means "leave unchanged". */
export type AppSettingsPatch = Partial<AppSettings>;

// #endregion

// #region Events
// Payloads emitted by Rust → React via Tauri events. Names mirror the
// emitter helpers in src-tauri/src/events.rs. Event names themselves are
// kebab-case strings (see src/ipc/events.ts).

/** Payload for `unity-status-changed`. */
export interface UnityStatusChangedPayload {
  status: ConnectionStatus;
  reason?: string;
}

/**
 * Lifecycle state of the bundled Node.js Agent SDK process.
 *
 * Distinct from `ConnectionStatus` — the Node SDK has its own state machine.
 */
export type NodeSdkStatus = "starting" | "running" | "crashed";

/** Payload for `node-sdk-status-changed`. */
export interface NodeSdkStatusChangedPayload {
  status: NodeSdkStatus;
  pid?: number;
}

/** Payload for `message-stream-chunk` — incremental token delivery for an in-flight message. */
export interface MessageStreamChunkPayload {
  messageId: MessageId;
  chunk: string;
}

/** Payload for `message-stream-complete` — emitted once when streaming finishes. */
export interface MessageStreamCompletePayload {
  messageId: MessageId;
}

/** Shape of the answer the agent expects from the user. */
export type AskUserType = "single" | "multi" | "free-text";

/** Payload for `ask-user-requested`. */
export interface AskUserRequestedPayload {
  questionId: string;
  question: string;
  options?: string[];
  type: AskUserType;
}

/** Payload for `permission-requested`. */
export interface PermissionRequestedPayload {
  requestId: string;
  tool: string;
  params: unknown;
}

/**
 * Diagnostic — used by the Group 3 stub. The real Node SDK will emit
 * typed events instead (message-received, ask-user-requested, etc).
 */
export interface NodeLogPayload {
  level: "info" | "warn" | "error";
  text: string;
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
export interface AppError {
  kind: AppErrorKind;
  message: string;
}

// #endregion