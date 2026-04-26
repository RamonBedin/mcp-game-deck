// Canonical contract types shared between Rust (Tauri) and TS (React).
// Mirrors src-tauri/src/types.rs (task 2.2). Edit both sides together when changing.

// --- Connection ----------------------------------------------------------

export type ConnectionStatus = "connected" | "busy" | "disconnected";

// --- Permissions ---------------------------------------------------------

export type PermissionMode = "auto" | "ask" | "plan";

// --- Messages ------------------------------------------------------------

export type MessageRole = "user" | "assistant" | "system";

export type MessageId = string;

export interface Message {
  id: MessageId;
  role: MessageRole;
  content: string;
  timestamp: number;
  agent?: string;
}

// --- Plans ---------------------------------------------------------------

export interface PlanMeta {
  name: string;
  lastModified: number;
}

// Frontmatter schema is not pinned yet — Feature 06 will tighten it.
export type PlanFrontmatter = Record<string, unknown>;

export interface Plan extends PlanMeta {
  content: string;
  frontmatter: PlanFrontmatter;
}

// --- Rules ---------------------------------------------------------------

export interface RuleMeta {
  name: string;
  enabled: boolean;
}

export interface Rule extends RuleMeta {
  content: string;
}

// --- Settings ------------------------------------------------------------

export type Theme = "dark" | "light";

export interface AppSettings {
  theme: Theme;
  unityProjectPath: string | null;
}

export type AppSettingsPatch = Partial<AppSettings>;

// --- Events --------------------------------------------------------------
// Payloads emitted by Rust → React via Tauri events. Names mirror the
// emitter helpers in src-tauri/src/events.rs. Event names themselves are
// kebab-case strings (see src/ipc/events.ts).

export interface UnityStatusChangedPayload {
  status: ConnectionStatus;
  reason?: string;
}

// Distinct from ConnectionStatus — Node SDK has its own state machine.
export type NodeSdkStatus = "starting" | "running" | "crashed";

export interface NodeSdkStatusChangedPayload {
  status: NodeSdkStatus;
  pid?: number;
}

export interface MessageStreamChunkPayload {
  messageId: MessageId;
  chunk: string;
}

export interface MessageStreamCompletePayload {
  messageId: MessageId;
}

export type AskUserType = "single" | "multi" | "free-text";

export interface AskUserRequestedPayload {
  questionId: string;
  question: string;
  options?: string[];
  type: AskUserType;
}

export interface PermissionRequestedPayload {
  requestId: string;
  tool: string;
  params: unknown;
}

// --- Errors --------------------------------------------------------------

export type AppErrorKind =
  | "unity_disconnected"
  | "node_sdk_unavailable"
  | "file_not_found"
  | "permission_denied"
  | "invalid_input"
  | "internal";

export interface AppError {
  kind: AppErrorKind;
  message: string;
}