/**
 * Typed wrappers around Tauri's `invoke()` — one function per command in
 * `src-tauri/src/commands/`. Callers should NEVER import `invoke` directly:
 * going through these wrappers preserves the contract types from `./types`.
 *
 * Wire conventions:
 * - Command names are snake_case (matches the Rust function names).
 * - Argument keys are camelCase on the JS side; Tauri auto-converts to
 *   snake_case for Rust (e.g. `sessionId` → `session_id`).
 * - Errors from `Result<_, AppError>` Rust returns surface as a thrown
 *   value on the Promise. Catch with try/catch and narrow with the
 *   `AppError` shape from `./types`.
 */

import { invoke } from "@tauri-apps/api/core";
import type {
  AppSettings,
  AppSettingsPatch,
  ClaudeInstallStatus,
  ConnectionStatus,
  Message,
  MessageId,
  NodeSdkStatus,
  PermissionMode,
  Plan,
  PlanMeta,
  Rule,
  RuleMeta,
} from "./types";

// #region Connection

/**
 * Reads the live Unity TCP connection status.
 *
 * @returns The current `ConnectionStatus`.
 */
export const getUnityStatus = (): Promise<ConnectionStatus> =>
  invoke("get_unity_status");

/**
 * Reads the live Node Agent SDK process status.
 *
 * @returns The current `NodeSdkStatus`.
 */
export const getNodeSdkStatus = (): Promise<NodeSdkStatus> =>
  invoke("get_node_sdk_status");

/**
 * Manual reconnect hook for the Unity client.
 *
 * No-op today (the connection loop already retries on backoff); reserved
 * for future UX needs.
 *
 * @returns Resolves once the Rust side returns.
 */
export const reconnectUnity = (): Promise<void> => invoke("reconnect_unity");

/**
 * Restarts the Node Agent SDK child process.
 *
 * Idempotent on the Rust side — kills any prior child first.
 *
 * @returns Resolves once the new child has been spawned.
 */
export const restartNodeSdk = (): Promise<void> => invoke("restart_node_sdk");

// #endregion

// #region Install

/**
 * Probes whether `claude` is on PATH + authenticated and whether
 * `@anthropic-ai/claude-agent-sdk` is present in the Tauri-managed Node
 * runtime.
 *
 * Polled by `FirstRunPanel` every 5s while mounted; each call runs
 * fresh subprocess probes (no cache today). Internal 5s timeout per
 * probe — never hangs the React side.
 *
 * @returns A `ClaudeInstallStatus` with the four detection fields populated.
 */
export const checkClaudeInstallStatus = (): Promise<ClaudeInstallStatus> =>
  invoke("check_claude_install_status");

// #endregion

// #region Conversation

/**
 * Forwards a chat message to the Node SDK.
 *
 * @param text - User's message text.
 * @param agent - Optional sub-agent name to route the message through.
 * @returns The message id assigned by the Node SDK.
 */
export const sendMessage = (
  text: string,
  agent?: string | null,
): Promise<MessageId> =>
  invoke("send_message", { text, agent: agent ?? null });

/**
 * Reads the recent conversation history for a session.
 *
 * @param sessionId - Session whose history to retrieve.
 * @param limit - Maximum number of messages to return.
 * @returns The most recent `Message` entries (oldest first).
 */
export const getConversationHistory = (
  sessionId: string,
  limit: number,
): Promise<Message[]> =>
  invoke("get_conversation_history", { sessionId, limit });

/**
 * Clears the message history for a session.
 *
 * @param sessionId - Session to clear.
 * @returns Resolves once the Rust side returns.
 */
export const clearConversation = (sessionId: string): Promise<void> =>
  invoke("clear_conversation", { sessionId });

/**
 * Persists the agent's permission mode.
 *
 * @param mode - Desired permission policy.
 * @returns Resolves once the Rust side returns.
 */
export const setPermissionMode = (mode: PermissionMode): Promise<void> =>
  invoke("set_permission_mode", { mode });

/**
 * Reads the agent's current permission mode.
 *
 * @returns The active `PermissionMode`.
 */
export const getPermissionMode = (): Promise<PermissionMode> =>
  invoke("get_permission_mode");

// #endregion

// #region Plans

/**
 * Lists known plan files.
 *
 * @returns Lightweight metadata for every available plan.
 */
export const listPlans = (): Promise<PlanMeta[]> => invoke("list_plans");

/**
 * Reads a plan by name.
 *
 * @param name - Plan filename without extension.
 * @returns The full `Plan`, including parsed frontmatter and body.
 */
export const readPlan = (name: string): Promise<Plan> =>
  invoke("read_plan", { name });

/**
 * Writes a plan to disk.
 *
 * @param name - Plan filename without extension.
 * @param content - Markdown body to persist.
 * @returns Resolves once the Rust side returns.
 */
export const writePlan = (name: string, content: string): Promise<void> =>
  invoke("write_plan", { name, content });

/**
 * Deletes a plan.
 *
 * @param name - Plan filename without extension.
 * @returns Resolves once the Rust side returns.
 */
export const deletePlan = (name: string): Promise<void> =>
  invoke("delete_plan", { name });

// #endregion

// #region Rules

/**
 * Lists known rule files.
 *
 * @returns Lightweight metadata for every available rule.
 */
export const listRules = (): Promise<RuleMeta[]> => invoke("list_rules");

/**
 * Reads a rule by name.
 *
 * @param name - Rule filename without extension.
 * @returns The full `Rule`, including the activation flag and body.
 */
export const readRule = (name: string): Promise<Rule> =>
  invoke("read_rule", { name });

/**
 * Writes a rule to disk.
 *
 * @param name - Rule filename without extension.
 * @param content - Markdown body to persist.
 * @returns Resolves once the Rust side returns.
 */
export const writeRule = (name: string, content: string): Promise<void> =>
  invoke("write_rule", { name, content });

/**
 * Deletes a rule.
 *
 * @param name - Rule filename without extension.
 * @returns Resolves once the Rust side returns.
 */
export const deleteRule = (name: string): Promise<void> =>
  invoke("delete_rule", { name });

/**
 * Enables or disables a rule.
 *
 * @param name - Rule filename without extension.
 * @param enabled - Desired activation flag.
 * @returns Resolves once the Rust side returns.
 */
export const toggleRule = (name: string, enabled: boolean): Promise<void> =>
  invoke("toggle_rule", { name, enabled });

// #endregion

// #region Settings

/**
 * Reads the persisted application settings.
 *
 * @returns The full `AppSettings` value (defaults when nothing is persisted yet).
 */
export const getSettings = (): Promise<AppSettings> => invoke("get_settings");

/**
 * Applies a partial settings update.
 *
 * @param patch - Fields to update; missing fields are left unchanged.
 * @returns Resolves once the Rust side returns.
 */
export const updateSettings = (patch: AppSettingsPatch): Promise<void> =>
  invoke("update_settings", { patch });

// #endregion

// #region Environment

/**
 * Reads a single environment variable from the Tauri host process.
 *
 * @param name - Environment variable name (e.g. `"MCP_GAME_DECK_UPDATE_AVAILABLE"`).
 * @returns The variable's value, or `null` when unset or not valid UTF-8.
 *   An explicitly empty value comes back as `""` — treat empty strings as
 *   absent at the call site.
 */
export const getEnvVar = (name: string): Promise<string | null> =>
  invoke("get_env_var", { name });

// #endregion

// #region Dev-only
// Gated by `import.meta.env.DEV` at the call site (see SettingsRoute).
// In release builds the underlying command returns an error; the wrapper
// signature stays stable to keep the typed surface consistent.

/**
 * Emits a one-shot `unity-status-changed` event for verification testing.
 *
 * @returns Resolves once the Rust side returns. Rejects in release builds.
 */
export const devEmitTestEvent = (): Promise<void> =>
  invoke("dev_emit_test_event");

/**
 * Round-trips a JSON-RPC `ping` to the Node SDK child.
 *
 * Used by Group 3 verification.
 *
 * @returns `true` if the child responds with `{ pong: true }`.
 */
export const nodePing = (): Promise<boolean> => invoke("node_ping");

/**
 * Forwards a `tools/call` to Unity's MCP server.
 *
 * The Rust side reads the auth token from
 * `$UNITY_PROJECT_PATH/Library/GameDeck/auth-token`.
 *
 * @param name - MCP tool name to invoke.
 * @param args - Optional arguments object; defaults to `{}` when omitted.
 * @returns The unwrapped `result` field of the JSON-RPC response.
 */
export const devCallUnityTool = (
  name: string,
  args?: Record<string, unknown>,
): Promise<unknown> =>
  invoke("dev_call_unity_tool", { name, arguments: args ?? {} });

// #endregion