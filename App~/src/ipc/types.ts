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
  | { type: "tool-result"; toolUseId: string; content: unknown; isError: boolean }
  | { type: "request"; requestId: string; subtype: "permission" | "question"; payload: PermissionRequestedPayload | AskUserRequestedPayload; state: "pending" | "answered" | "interrupted" | "auto-allowed"; answer?: AskUserQuestionOutput; outcome?: "allow" | "allow-always" | "deny" | "auto-allowed" };

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

/**
 * Lightweight metadata for a plan file (used in list views).
 *
 * `description` is convenience-extracted from the file's YAML
 * frontmatter so the list view doesn't have to read every plan's full
 * body. `null` when absent, blank, or unparseable.
 */
export interface PlanMeta
{
  name: string;
  lastModified: number;
  description: string | null;
}

/**
 * Free-form frontmatter map for plan documents.
 *
 * Schema is not pinned yet — Feature 06 will tighten it.
 */
export type PlanFrontmatter = Record<string, unknown>;

/**
 * Full contents of a plan, including its parsed frontmatter and body.
 *
 * Mirrors Rust's `Plan` struct shape: independent of `PlanMeta` because
 * `description` is a list-view convenience, while a full read returns
 * the entire `frontmatter` map for callers that want it.
 */
export interface Plan
{
  name: string;
  lastModified: number;
  content: string;
  frontmatter: PlanFrontmatter;
}

/**
 * Kind of filesystem change emitted by `plans-changed`. Synthesized
 * Rust-side by comparing a known-names set against `path.exists()` —
 * the underlying `notify-debouncer-mini` collapses native event kinds.
 */
export type PlansChangedKind = "created" | "modified" | "deleted";

/**
 * Payload for `plans-changed` — emitted whenever a `.md` file under
 * the active project's plans dir is created, modified, or deleted.
 *
 * `name` is the file stem (no `.md`); `undefined` when the watcher
 * couldn't extract a name (e.g. non-UTF8 path).
 */
export interface PlansChangedPayload
{
  kind: PlansChangedKind;
  name?: string;
}

// #endregion

// #region Files

/**
 * Kind of entry returned by `list_project_files`. Directories are
 * included so the `@` picker can offer `@SomeFolder/` references.
 */
export type FileKind = "file" | "directory";

/**
 * One entry in the project file index. `path` is relative to the
 * active `UNITY_PROJECT_PATH`, normalized to forward slashes
 * regardless of OS.
 */
export interface FileIndexEntry
{
  path: string;
  kind: FileKind;
}

/**
 * Payload for `project-files-changed`. `debounced` is `true` when the
 * underlying watcher batch coalesced more than one filesystem event;
 * `useProjectFiles` refetches the index unconditionally, so the flag
 * is informational only.
 */
export interface ProjectFilesChangedPayload
{
  debounced: boolean;
}

// #endregion

// #region Catalog

/**
 * Source classification for a slash command, mirrored from Rust for
 * the slash dropdown . Built-ins are Claude Code's
 * first-party commands; user-commands live under
 * `ProjectSettings/GameDeck/commands/`; plugin commands come from
 * this package (`mcp-game-deck:` prefix); third-party covers any
 * other namespaced prefix.
 */
export type CommandSource =
  | "built-in"
  | "user-command"
  | "plugin"
  | "third-party";

/**
 * Source classification for an agent, mirrored from Rust for the `@`
 * picker (F06 group 6). Same prefix scheme as `CommandSource` minus
 * the `user-command` variant.
 */
export type AgentSource = "built-in" | "plugin" | "third-party";

/**
 * One entry in the `catalog-ready` agent message's `commands` array.
 * `argumentHint` mirrors a SKILL.md's `argument-hint` frontmatter
 * field; omitted from the wire when the command takes no argument.
 */
export interface CatalogCommand
{
  name: string;
  description: string;
  argumentHint?: string;
  source: CommandSource;
}

/** One entry in the `catalog-ready` agent message's `agents` array. */
export interface CatalogAgent
{
  name: string;
  description: string;
  source: AgentSource;
}

// #endregion

// #region Rules

/**
 * Lightweight metadata for a rule file (used in list views).
 *
 * `lastModified` is in **milliseconds since the Unix epoch**
 * (matches `PlanMeta` and `SessionSummary`). `description` and
 * `appliesTo` are convenience-extracted from the file's YAML
 * frontmatter so the list doesn't have to read each rule's full
 * body. `estimatedTokens` is a chars/4 heuristic computed from the
 * full file content (frontmatter + body) so the Rules tab header
 * can show bundle cost at a glance.
 */
export interface RuleMeta
{
  name: string;
  lastModified: number;
  enabled: boolean;
  description: string | null;
  appliesTo: string[];
  estimatedTokens: number;
}

/**
 * Free-form frontmatter map for rule documents.
 */
export type RuleFrontmatter = Record<string, unknown>;

/**
 * Full contents of a rule, including its parsed frontmatter and
 * body.
 *
 * `lastModified` is in **milliseconds since the Unix epoch**
 * (matches `RuleMeta` / `PlanMeta` / `SessionSummary`). `content` is
 * the body **without** `---` delimiters. The full frontmatter map
 * is surfaced separately so the React pane can render the
 * `applies-to` chip strip without re-parsing.
 */
export interface Rule
{
  name: string;
  lastModified: number;
  content: string;
  frontmatter: RuleFrontmatter;
  estimatedTokens: number;
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

/**
 * Payload for `claude-version-out-of-range` — fired once per supervisor
 * startup when the locally installed `claude --version` falls outside
 * the smoke-tested range advertised by repo-root `package.json`'s
 * `claudeCode` field. Drives a non-blocking warning banner; the app
 * continues to run on the detected version.
 */
export interface ClaudeVersionOutOfRangePayload
{
  detected: string;
  supported: string;
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

/**
 * One question inside an `AskUserQuestionInput`. Mirrors the SDK's
 * `@anthropic-ai/claude-agent-sdk` shape so the React side can render
 * the question card without translation.
 */
export interface AskUserQuestion
{
  header?: string;
  question: string;
  multiSelect: boolean;
  options: Array<{ label: string; description?: string }>;
}

/** Payload for the `ask-user-requested` agent message. */
export interface AskUserRequestedPayload
{
  requestId: string;
  turnId: string;
  agentId: string | null;
  input: { questions: AskUserQuestion[] };
}

/** Payload for the `permission-requested` agent message. */
export interface PermissionRequestedPayload
{
  requestId: string;
  turnId: string;
  agentId: string | null;
  toolName: string;
  input: unknown;
  blockedPath: string | null;
  decisionReason: string | null;
}

/**
 * Output shape returned to the SDK after the user answers an
 * `AskUserQuestion`. Mirrors `AskUserQuestionOutput` from
 * `@anthropic-ai/claude-agent-sdk` (TypeScript reference, April 2026).
 *
 * - `questions` echoes `AskUserQuestionInput.questions` verbatim so
 *   the SDK can re-attach the original schema to the answers.
 * - `answers` is keyed by `question.question` (the prompt string
 *   itself) and the value is the selected label. Multi-select
 *   questions concatenate the labels with `", "`. Free-text
 *   responses (when the user picked an "Other"-conventioned option
 *   and typed) carry the typed string instead of any label.
 */
export interface AskUserQuestionOutput
{
  questions: AskUserQuestion[];
  answers: Record<string, string>;
}

/**
 * Payload for the `respond_to_request` Tauri command — mirrors the
 * Rust `DecisionPayload` enum shape. Discriminated by `kind`:
 * permission outcomes carry an `outcome`; question responses carry
 * the structured `answer`.
 */
export type DecisionPayload =
  | { kind: "permission"; outcome: "allow" | "allow-always" | "deny" }
  | { kind: "question"; answer: AskUserQuestionOutput };

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
 * Payload for the `request-resolved` agent message — the canUseTool
 * promise resolved either via user click (allow / allow-always /
 * deny), via the in-session Allow Always cache short-circuit
 * (`auto-allowed`), or via a question-answer round-trip. `toolName`
 * and `turnId` are populated specifically on `auto-allowed` so the
 * React store can synthesize a compact "Auto-allowed: <toolName>"
 * block (task 3.5) without having seen a prior `permission-requested`
 * — the cache short-circuit means no card was ever rendered.
 */
export interface RequestResolvedPayload
{
  requestId: string;
  outcome: "allow" | "allow-always" | "deny" | "auto-allowed";
  answer: AskUserQuestionOutput | null;
  toolName: string | null;
  turnId: string | null;
}

/**
 * Tagged message envelope emitted by `sdk-entry.js` and re-emitted
 * to React via the `agent-message` Tauri event.
 *
 * added `text-delta` for streaming and gave
 * `assistant-turn-complete` a `turnId`. `assistant-text` is kept as
 * a legacy variant with no producer in — preserved so the wire
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
  | { type: "permission-mode-changed"; mode: PermissionMode }
  | { type: "health-ok" }
  | { type: "health-failed"; message: string }
  | { type: "ask-user-requested"; requestId: string; turnId: string; agentId: string | null; input: { questions: AskUserQuestion[] } }
  | { type: "permission-requested"; requestId: string; turnId: string; agentId: string | null; toolName: string; input: unknown; blockedPath: string | null; decisionReason: string | null }
  | { type: "request-resolved"; requestId: string; outcome: "allow" | "allow-always" | "deny" | "auto-allowed"; answer: AskUserQuestionOutput | null; toolName: string | null; turnId: string | null }
  | { type: "catalog-ready"; commands: CatalogCommand[]; agents: CatalogAgent[] };

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