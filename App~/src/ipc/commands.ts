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
  DecisionPayload,
  FileIndexEntry,
  Message,
  PermissionMode,
  Plan,
  PlanMeta,
  Rule,
  RuleMeta,
  SessionSummary,
  SupervisorStatus,
} from "./types";

// #region Connection

export const getUnityStatus = (): Promise<ConnectionStatus> => invoke("get_unity_status");

export const getSupervisorStatus = (): Promise<SupervisorStatus> => invoke("get_supervisor_status");

export const restartSupervisor = (): Promise<void> => invoke("restart_supervisor");

// #endregion

// #region Install

export const checkClaudeInstallStatus = (): Promise<ClaudeInstallStatus> => invoke("check_claude_install_status");

export const startSdkInstall = (): Promise<void> => invoke("start_sdk_install");

// #endregion

// #region Conversation

export const sendMessage = (
  text: string,
  attachmentPaths: string[] = [],
): Promise<void> => invoke("send_message", { text, attachmentPaths });

export const setPermissionMode = (mode: PermissionMode): Promise<void> =>
  invoke("set_permission_mode", { mode });

export const setModel = (model: string | null): Promise<void> =>
  invoke("set_model", { model });

export const respondToRequest = (requestId: string, decision: DecisionPayload,): Promise<void> => invoke("respond_to_request", { requestId, decision });
export const cancelCurrentTurn = (): Promise<void> => invoke("cancel_current_turn");

// #endregion

// #region Plans

export const listPlans = (): Promise<PlanMeta[]> => invoke("list_plans");

export const readPlan = (name: string): Promise<Plan> => invoke("read_plan", { name });

export const writePlan = (name: string, content: string): Promise<void> => invoke("write_plan", { name, content });

export const deletePlan = (name: string): Promise<void> => invoke("delete_plan", { name });

// #endregion

// #region Files

export const listProjectFiles = (): Promise<FileIndexEntry[]> => invoke("list_project_files");

// #endregion

// #region Sessions

export const getSessions = (): Promise<SessionSummary[]> => invoke("get_sessions");

export const getSessionMessages = (sessionId: string): Promise<Message[]> => invoke("get_session_messages", { sessionId });

export const resumeSession = (sessionId: string): Promise<void> => invoke("resume_session", { sessionId });

export const startNewSession = (): Promise<void> => invoke("start_new_session");

export const deleteSession = (sessionId: string): Promise<void> => invoke("delete_session", { sessionId });

// #endregion

// #region Rules

export const listRules = (): Promise<RuleMeta[]> => invoke("list_rules");

export const readRule = (name: string): Promise<Rule> => invoke("read_rule", { name });

export const writeRule = (name: string, content: string): Promise<void> => invoke("write_rule", { name, content });

export const deleteRule = (name: string): Promise<void> => invoke("delete_rule", { name });

export const toggleRule = (name: string, enabled: boolean): Promise<void> => invoke("toggle_rule", { name, enabled });

export const previewRulesBundle = (): Promise<string> => invoke("preview_rules_bundle");

// #endregion

// #region Recent commands

export const listRecentCommands = (): Promise<string[]> => invoke("list_recent_commands");

export const trackRecentCommand = (command: string): Promise<void> => invoke("track_recent_command", { command });

// #endregion

// #region Knowledge

/**
 * Lightweight metadata for one knowledge doc bundled in
 * `Plugin~/knowledge/`. Mirrors Rust's `KnowledgeDocMeta`.
 */
export interface KnowledgeDocMeta
{
  id: string;
  num: string;
  title: string;
  wordCount: number;
}

/** Full knowledge doc — metadata plus the raw markdown body. */
export interface KnowledgeDoc extends KnowledgeDocMeta
{
  body: string;
}

export const listKnowledgeDocs = (): Promise<KnowledgeDocMeta[]> => invoke("list_knowledge_docs");

export const readKnowledgeDoc = (id: string): Promise<KnowledgeDoc> => invoke("read_knowledge_doc", { id });

export const readAllKnowledgeDocs = (): Promise<KnowledgeDoc[]> => invoke("read_all_knowledge_docs");

// #endregion

// #region Settings

export const getSettings = (): Promise<AppSettings> => invoke("get_settings");

export const updateSettings = (patch: AppSettingsPatch): Promise<void> => invoke("update_settings", { patch });

// #endregion

// #region Environment

export const getEnvVar = (name: string): Promise<string | null> => invoke("get_env_var", { name });

export const getOsUsername = (): Promise<string | null> => invoke("get_os_username");

// #endregion

// #region Dev-only

export const devEmitTestEvent = (): Promise<void> => invoke("dev_emit_test_event");

export const devCallUnityTool = (
  name: string,
  args?: Record<string, unknown>,
): Promise<unknown> =>
  invoke("dev_call_unity_tool", { name, arguments: args ?? {} });

// #endregion