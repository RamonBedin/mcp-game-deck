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
  PermissionMode,
  Plan,
  PlanMeta,
  Rule,
  RuleMeta,
  SupervisorStatus,
} from "./types";

// #region Connection

export const getUnityStatus = (): Promise<ConnectionStatus> => invoke("get_unity_status");

export const getSupervisorStatus = (): Promise<SupervisorStatus> => invoke("get_supervisor_status");

export const reconnectUnity = (): Promise<void> => invoke("reconnect_unity");

export const restartSupervisor = (): Promise<void> => invoke("restart_supervisor");

// #endregion

// #region Install

export const checkClaudeInstallStatus = (): Promise<ClaudeInstallStatus> => invoke("check_claude_install_status");

export const startSdkInstall = (): Promise<void> => invoke("start_sdk_install");

// #endregion

// #region Conversation

export const sendMessage = (text: string): Promise<void> => invoke("send_message", { text });

export const getConversationHistory = (
  sessionId: string,
  limit: number,
): Promise<Message[]> =>
  invoke("get_conversation_history", { sessionId, limit });

export const clearConversation = (sessionId: string): Promise<void> =>
  invoke("clear_conversation", { sessionId });

export const setPermissionMode = (mode: PermissionMode): Promise<void> =>
  invoke("set_permission_mode", { mode });

export const getPermissionMode = (): Promise<PermissionMode> =>
  invoke("get_permission_mode");

// #endregion

// #region Plans

export const listPlans = (): Promise<PlanMeta[]> => invoke("list_plans");

export const readPlan = (name: string): Promise<Plan> => invoke("read_plan", { name });

export const writePlan = (name: string, content: string): Promise<void> => invoke("write_plan", { name, content });

export const deletePlan = (name: string): Promise<void> => invoke("delete_plan", { name });

// #endregion

// #region Rules

export const listRules = (): Promise<RuleMeta[]> => invoke("list_rules");

export const readRule = (name: string): Promise<Rule> => invoke("read_rule", { name });

export const writeRule = (name: string, content: string): Promise<void> => invoke("write_rule", { name, content });

export const deleteRule = (name: string): Promise<void> => invoke("delete_rule", { name });

export const toggleRule = (name: string, enabled: boolean): Promise<void> => invoke("toggle_rule", { name, enabled });

// #endregion

// #region Settings

export const getSettings = (): Promise<AppSettings> => invoke("get_settings");

export const updateSettings = (patch: AppSettingsPatch): Promise<void> => invoke("update_settings", { patch });

// #endregion

// #region Environment

export const getEnvVar = (name: string): Promise<string | null> => invoke("get_env_var", { name });

// #endregion

// #region Dev-only

export const devEmitTestEvent = (): Promise<void> => invoke("dev_emit_test_event");

export const nodePing = (): Promise<boolean> => invoke("node_ping");

export const devCallUnityTool = (
  name: string,
  args?: Record<string, unknown>,
): Promise<unknown> =>
  invoke("dev_call_unity_tool", { name, arguments: args ?? {} });

// #endregion