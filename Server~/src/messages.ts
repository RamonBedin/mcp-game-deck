/**
 * WebSocket protocol message types between Unity Chat UI and Agent SDK Server.
 * See docs/ARCHITECTURE.md for full protocol specification.
 */

import type { SessionInfo, ChatMessage } from "./sessions.js";

// ─── Unity → Server ───

/** A file attached to a prompt message (image or document). */
export interface Attachment
{
  type: "image" | "document";
  mediaType: string;
  name: string;
  data: string;
}

/** Client message requesting the AI to process a prompt. */
export interface PromptAction 
{
  action: "prompt";
  prompt: string;
  sessionId?: string | null;
  agent?: string | null;
  model?: string | null;
  permissionMode?: string | null;
  attachments?: Attachment[] | null;
}

/** Client message requesting execution of a slash command (skill). */
export interface CommandAction
{
  action: "command";
  command: string;
  prompt?: string | null;
  sessionId?: string | null;
}

/** Client message requesting cancellation of the active generation. */
export interface CancelAction 
{
  action: "cancel";
}

/** Client message requesting a list of sessions, agents, or commands. */
export interface ListAction 
{
  action: "list-sessions" | "list-agents" | "list-commands";
}

/** Client message requesting deletion of a session. */
export interface DeleteSessionAction 
{
  action: "delete-session";
  sessionId: string;
}

/** Client message requesting a single session's history. */
export interface GetSessionAction 
{
  action: "get-session";
  sessionId: string;
}

/** Client health-check message. Server responds with a PongMessage. */
export interface PingAction 
{
  action: "ping";
}

/** Client message responding to a permission request from the server. */
export interface PermissionResponseAction
{
  action: "permission_response";
  toolUseId: string;
  allow: boolean;
  message?: string;
}

/** Union of all messages the Unity client can send to the server. */
export type ClientMessage =
  | PromptAction
  | CommandAction
  | CancelAction
  | ListAction
  | DeleteSessionAction
  | GetSessionAction
  | PingAction
  | PermissionResponseAction;

// ─── Server → Unity ───

/** Streaming or final text content from the Claude assistant. */
export interface AssistantMessage 
{
  type: "assistant";
  content: string;
  streaming: boolean;
}

/** Notification that the AI is invoking an MCP tool. */
export interface ToolUseMessage 
{
  type: "tool_use";
  name: string;
  input: Record<string, unknown>;
}

/** Result of a completed MCP tool invocation. */
export interface ToolResultMessage 
{
  type: "tool_result";
  name: string;
  success: boolean;
  output: string;
}

/** Extended thinking content from the AI (collapsible in UI). */
export interface ThinkingMessage 
{
  type: "thinking";
  content: string;
}

/** Final result of a completed query, including session ID and cost. */
export interface ResultMessage 
{
  type: "result";
  sessionId: string;
  costUsd: number;
  durationMs: number;
  turns: number;
}

/** Error message from the server. */
export interface ErrorMessage 
{
  type: "error";
  message: string;
}

/** Wire format for a single agent entry in the list-agents response. */
export interface AgentInfo 
{
  name: string;
  description: string;
}

/** Server response listing all available agents. */
export interface AgentsMessage 
{
  type: "agents";
  agents: AgentInfo[];
}

/** Wire format for a single command entry in the list-commands response. */
export interface CommandInfo 
{
  name: string;
  description: string;
}

/** Server response listing all available slash commands. */
export interface CommandsMessage 
{
  type: "commands";
  commands: CommandInfo[];
}

/** Server health-check response. */
export interface PongMessage 
{
  type: "pong";
  status: "healthy" | "degraded";
  mcpConnected: boolean;
  model: string;
}

/** Server response listing all sessions. */
export interface SessionsMessage 
{
  type: "sessions";
  sessions: SessionInfo[];
}

/** Server response with a single session's message history. */
export interface SessionHistoryMessage 
{
  type: "session-history";
  sessionId: string;
  messages: ChatMessage[];
}

/** Server message requesting permission from the user before executing a tool. */
export interface PermissionRequestMessage
{
  type: "permission_request";
  toolName: string;
  toolInput: Record<string, unknown>;
  toolUseId: string;
  reason?: string;
}

export type ServerMessage =
  | AssistantMessage
  | ToolUseMessage
  | ToolResultMessage
  | ThinkingMessage
  | ResultMessage
  | ErrorMessage
  | AgentsMessage
  | CommandsMessage
  | PongMessage
  | SessionsMessage
  | SessionHistoryMessage
  | PermissionRequestMessage;
