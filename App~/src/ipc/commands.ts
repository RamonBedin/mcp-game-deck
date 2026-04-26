// Typed wrappers around Tauri's `invoke()` — one function per command in
// src-tauri/src/commands/. Callers should NEVER import `invoke` directly:
// going through these wrappers preserves the contract types from ./types.
//
// Wire conventions:
// - Command names are snake_case (matches the Rust function names).
// - Argument keys are camelCase on the JS side; Tauri auto-converts to
//   snake_case for Rust (e.g. sessionId → session_id).
// - Errors from `Result<_, AppError>` Rust returns surface as a thrown
//   value on the Promise. Catch with try/catch and narrow with the
//   AppError shape from ./types.

import { invoke } from "@tauri-apps/api/core";
import type {
  AppSettings,
  AppSettingsPatch,
  ConnectionStatus,
  Message,
  MessageId,
  PermissionMode,
  Plan,
  PlanMeta,
  Rule,
  RuleMeta,
} from "./types";

// --- Connection ----------------------------------------------------------

export const getUnityStatus = (): Promise<ConnectionStatus> =>
  invoke("get_unity_status");

export const getNodeSdkStatus = (): Promise<ConnectionStatus> =>
  invoke("get_node_sdk_status");

export const reconnectUnity = (): Promise<void> => invoke("reconnect_unity");

export const restartNodeSdk = (): Promise<void> => invoke("restart_node_sdk");

// --- Conversation --------------------------------------------------------

export const sendMessage = (
  text: string,
  agent?: string | null,
): Promise<MessageId> =>
  invoke("send_message", { text, agent: agent ?? null });

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

// --- Plans ---------------------------------------------------------------

export const listPlans = (): Promise<PlanMeta[]> => invoke("list_plans");

export const readPlan = (name: string): Promise<Plan> =>
  invoke("read_plan", { name });

export const writePlan = (name: string, content: string): Promise<void> =>
  invoke("write_plan", { name, content });

export const deletePlan = (name: string): Promise<void> =>
  invoke("delete_plan", { name });

// --- Rules ---------------------------------------------------------------

export const listRules = (): Promise<RuleMeta[]> => invoke("list_rules");

export const readRule = (name: string): Promise<Rule> =>
  invoke("read_rule", { name });

export const writeRule = (name: string, content: string): Promise<void> =>
  invoke("write_rule", { name, content });

export const deleteRule = (name: string): Promise<void> =>
  invoke("delete_rule", { name });

export const toggleRule = (name: string, enabled: boolean): Promise<void> =>
  invoke("toggle_rule", { name, enabled });

// --- Settings ------------------------------------------------------------

export const getSettings = (): Promise<AppSettings> => invoke("get_settings");

export const updateSettings = (patch: AppSettingsPatch): Promise<void> =>
  invoke("update_settings", { patch });