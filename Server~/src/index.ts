/**
 * MCP Game Deck — Agent SDK Server
 *
 * WebSocket server that bridges the Unity Editor Chat UI with the Claude Agent SDK.
 * Uses mcp-proxy.js as a STDIO MCP server for tool access.
 *
 * Architecture:
 *   Unity Chat UI ←→ WebSocket (:port) ←→ This Server ←→ Claude Agent SDK
 *                                               ↕ (MCP tools via stdio)
 *                                         mcp-proxy.js ←→ HTTP ←→ C# MCP (:port) ←→ Unity Editor
 */

import path from "path";
import { fileURLToPath } from "url";
import { WebSocketServer, WebSocket } from "ws";
import { randomUUID } from "crypto";
import { query } from "@anthropic-ai/claude-agent-sdk";
import type { Query } from "@anthropic-ai/claude-agent-sdk";
import type { SDKUserMessage } from "@anthropic-ai/claude-agent-sdk";

import { loadConfig } from "./config.js";
import { loadAgents, loadSkills, agentsToInfo, skillsToCommands, getAgentPrompt } from "./agents.js";
import { initSessions, upsertSession, listSessions, deleteSession, getSession, appendMessages } from "./sessions.js";
import type { ClientMessage, ServerMessage, Attachment } from "./messages.js";
import type { PermissionResult, CanUseTool } from "@anthropic-ai/claude-agent-sdk";
import { HEALTH_CHECK_TIMEOUT_MS, MCP_SERVER_ID, MCP_TRANSPORT_TYPE, MCP_COMMAND, DEFAULT_PERMISSION_MODE, PERMISSION_TIMEOUT_MS } from "./constants.js";
import { getSystemPrompt } from "./system-prompt.js";

const DIR_NAME = path.dirname(fileURLToPath(import.meta.url));
const MCP_PROXY_PATH = path.join(DIR_NAME, "mcp-proxy.js");
const CONFIG = loadConfig();
const ACTIVE_QUERIES = new Map<WebSocket, Query>();

const PENDING_PERMISSIONS = new Map<string, PendingPermission>();
const AGENTS = await loadAgents(CONFIG.cwd);
const SKILLS = await loadSkills(CONFIG.cwd);
const COMMANDS = skillsToCommands(SKILLS);
const CORE_SYSTEM_PROMPT = await getSystemPrompt();
const WSS = new WebSocketServer({ port: CONFIG.port });

/** Pending permission requests waiting for user response. */
interface PendingPermission
{
  ws: WebSocket;
  originalInput: Record<string, unknown>;
  resolve: (result: PermissionResult) => void;
  timer: ReturnType<typeof setTimeout>;
}

// ─── Initialize persistent storage ───

await initSessions(CONFIG.cwd);

// ─── WebSocket Server ───

WSS.on("connection", (ws: WebSocket) => {
  console.log("[server] Client connected");

  ws.on("message", async (data: Buffer) => {
    let msg: ClientMessage;
    try 
    {
      msg = JSON.parse(data.toString()) as ClientMessage;
    } 
    catch 
    {
      send(ws, { type: "error", message: "Invalid JSON" });
      return;
    }

    try 
    {
      await handleMessage(ws, msg);
    } 
    catch (err) 
    {
      const errorMsg = err instanceof Error ? err.message : String(err);
      console.error(`[server] Error handling message:`, errorMsg);
      send(ws, { type: "error", message: errorMsg });
    }
  });

  ws.on("close", () => {
    console.log("[server] Client disconnected");
    cancelQuery(ws);
  });

  ws.on("error", (err) => {
    console.error("[server] WebSocket error:", err.message);
  });
});

// ─── MCP health check ───

/**
 * Probes the Unity C# MCP server to check if it is reachable.
 * @returns True if the server responded with HTTP 200.
 */
async function isMcpReachable(): Promise<boolean> 
{
  try 
  {
    const res = await fetch(CONFIG.mcpServerUrl, {
      method: "GET",
      signal: AbortSignal.timeout(HEALTH_CHECK_TIMEOUT_MS),
    });
    return res.ok;
  } 
  catch 
  {
    return false;
  }
}

// ─── Message handler ───

/**
 * Routes an incoming {@link ClientMessage} to the appropriate handler.
 * Covers health checks, agent/skill listing, session CRUD, and prompt execution.
 * @param ws The WebSocket connection that sent the message.
 * @param msg The parsed client message to handle.
 * @returns Resolves when the message has been fully handled and any response sent.
 */
async function handleMessage(ws: WebSocket, msg: ClientMessage): Promise<void> 
{
  switch (msg.action) 
  {
    case "ping": {
      const mcpConnected = await isMcpReachable();
      send(ws, {
        type: "pong",
        status: mcpConnected ? "healthy" : "degraded",
        mcpConnected,
        model: CONFIG.model,
      });
      break;
    }

    case "list-agents":
      send(ws, { type: "agents", agents: agentsToInfo(AGENTS) });
      break;

    case "list-commands":
      send(ws, { type: "commands", commands: COMMANDS });
      break;

    case "list-sessions":
      send(ws, { type: "sessions", sessions: listSessions() });
      break;

    case "delete-session":
      deleteSession(msg.sessionId);
      send(ws, { type: "sessions", sessions: listSessions() });
      break;

    case "get-session": {
      const session = getSession(msg.sessionId);
      send(ws, {
        type: "session-history",
        sessionId: msg.sessionId,
        messages: session?.messages ?? [],
      });
      break;
    }

    case "cancel":
      cancelQuery(ws);
      send(ws, { type: "assistant", content: "Generation cancelled.", streaming: false });
      break;

    case "permission_response": {
      const pending = PENDING_PERMISSIONS.get(msg.toolUseId);
      if (!pending)
      {
        break;
      }
      clearTimeout(pending.timer);
      PENDING_PERMISSIONS.delete(msg.toolUseId);

      if (msg.allow)
      {
        pending.resolve({ behavior: "allow", updatedInput: pending.originalInput });
      }
      else
      {
        pending.resolve({ behavior: "deny", message: msg.message ?? "User denied permission." });
      }
      
      break;
    }

    case "prompt":
      await handlePrompt(ws, msg.prompt, msg.sessionId ?? undefined, msg.agent ?? undefined, msg.model ?? undefined, msg.permissionMode ?? undefined, msg.attachments ?? undefined);
      break;

    case "command":
      await handlePrompt(ws, msg.command, msg.sessionId ?? undefined);
      break;
  }
}

// ─── Permission Callback ───

/**
 * Creates a {@link CanUseTool} callback bound to a specific WebSocket connection.
 * When the Agent SDK needs permission, it sends a request to the Chat UI and
 * waits for the user to accept or reject via WebSocket.
 * @param ws The WebSocket connection to send permission requests to.
 * @returns A {@link CanUseTool} callback for the Agent SDK.
 */
function createCanUseTool(ws: WebSocket): CanUseTool
{
  return (toolName, input, options) => new Promise<PermissionResult>((resolve) =>
  {
    const { toolUseID, decisionReason } = options;

    const timer = setTimeout(() =>
    {
      PENDING_PERMISSIONS.delete(toolUseID);
      resolve({ behavior: "deny", message: "Permission request timed out." });
    }, PERMISSION_TIMEOUT_MS);

    PENDING_PERMISSIONS.set(toolUseID, { ws, originalInput: input, resolve, timer });

    send(ws, {
      type: "permission_request",
      toolName,
      toolInput: input,
      toolUseId: toolUseID,
      reason: decisionReason,
    });
  });
}

// ─── Agent SDK Query Loop ───

/**
 * Executes a prompt against the Claude Agent SDK and streams results back over WebSocket.
 * Builds the query options (MCP server, system prompt, permission mode), iterates the
 * async message stream, forwards each content block to the client, and persists the
 * session when the query completes.
 * @param ws The WebSocket connection to stream responses to.
 * @param prompt The user's prompt text.
 * @param sessionId Optional session ID to resume an existing conversation.
 * @param agentName Optional agent name to delegate to a specific agent definition.
 * @param modelOverride Optional model identifier that overrides {@link ServerConfig.model}.
 * @param permissionModeOverride Optional permission mode that overrides {@link ServerConfig.permissionMode}.
 * @param attachments Optional array of file attachments (images/PDFs) to include as multi-modal content.
 * @returns Resolves when the query finishes, is cancelled, or errors out.
 */
async function handlePrompt(ws: WebSocket, prompt: string, sessionId?: string, agentName?: string, modelOverride?: string, permissionModeOverride?: string, attachments?: Attachment[]): Promise<void> 
{
  const startTime = Date.now();
  const effectiveMode = permissionModeOverride ?? CONFIG.permissionMode;
  const isBypass = effectiveMode === "bypassPermissions";
  const options: Record<string, unknown> = {
    mcpServers: {
      [MCP_SERVER_ID]: {
        type: MCP_TRANSPORT_TYPE,
        command: MCP_COMMAND,
        args: [MCP_PROXY_PATH],
      },
    },

    permissionMode: effectiveMode,
    ...(isBypass && { allowDangerouslySkipPermissions: true }),
    ...(!isBypass && { canUseTool: createCanUseTool(ws) }),
    model: modelOverride ?? CONFIG.model,
    cwd: CONFIG.cwd,
  };

  if (sessionId)
  {
    options.resume = sessionId;
  }

  let appendPrompt: string = CORE_SYSTEM_PROMPT;

  if (agentName)
  {
    const agentPrompt = await getAgentPrompt(AGENTS, agentName);

    if (agentPrompt)
    {
      appendPrompt = `${CORE_SYSTEM_PROMPT}\n\n---\n\n# Active Agent: ${agentName}\n\n${agentPrompt}`;
    }
  }

  options.systemPrompt = {
    type: "preset" as const,
    preset: "claude_code" as const,
    append: appendPrompt,
  };
  try
  {
    let effectivePrompt: string | AsyncIterable<SDKUserMessage> = prompt;

    if (attachments && attachments.length > 0)
    {
      const contentBlocks: Array<Record<string, unknown>> = [];

      for (const att of attachments)
      {
        if (att.type === "image")
        {
          contentBlocks.push({
            type: "image",
            source: { type: "base64", media_type: att.mediaType, data: att.data }
          });
        }
        else if (att.type === "document")
        {
          contentBlocks.push({
            type: "document",
            source: { type: "base64", media_type: att.mediaType, data: att.data }
          });
        }
      }

      contentBlocks.push({ type: "text", text: prompt });

      async function* multiModalPrompt(): AsyncGenerator<SDKUserMessage>
      {
        yield {
          type: "user",
          message: { role: "user" as const, content: contentBlocks as any },
          parent_tool_use_id: null,
          session_id: sessionId ?? "",
          uuid: randomUUID(),
        };
      }

      effectivePrompt = multiModalPrompt();
    }

    const activeQ = query({ prompt: effectivePrompt, options });
    ACTIVE_QUERIES.set(ws, activeQ);

    let resultSessionId = sessionId ?? "";
    let totalCost = 0;
    let turns = 0;
    let assistantTextParts: string[] = [];

    for await (const message of activeQ) 
    {
      if (ws.readyState !== WebSocket.OPEN)
      {
        break;
      }

      switch (message.type) 
      {
        case "assistant": {
          const content = (message as Record<string, unknown>).message as
            { content?: Array<Record<string, unknown>> } | undefined;

          if (content?.content) 
          {
            for (const block of content.content) 
            {
              if (block.type === "text") 
              {
                const textContent = (block.text as string) ?? "";
                assistantTextParts.push(textContent);
                send(ws, {
                  type: "assistant",
                  content: textContent,
                  streaming: true,
                });
              } 
              else if (block.type === "thinking") 
              {
                send(ws, {
                  type: "thinking",
                  content: (block.thinking as string) ?? "",
                });
              } 
              else if (block.type === "tool_use") 
              {
                send(ws, {
                  type: "tool_use",
                  name: (block.name as string) ?? "unknown",
                  input: (block.input as Record<string, unknown>) ?? {},
                });
              }
              else if (block.type === "tool_result") 
              {
                const blockContent = block.content;
                send(ws, {
                  type: "tool_result",
                  name: (block.tool_use_id as string) ?? "unknown",
                  success: !block.is_error,
                  output: typeof blockContent === "string" ? blockContent : JSON.stringify(blockContent ?? ""),
                });
              }
            }
          }
          break;
        }

        case "tool_progress": {
          const prog = message as Record<string, unknown>;
          send(ws, {
            type: "tool_use",
            name: (prog.tool_name as string) ?? "unknown",
            input: {},
          });
          break;
        }

        case "result": {
          const res = message as Record<string, unknown>;
          resultSessionId = (res.session_id as string) ?? resultSessionId;
          totalCost = (res.total_cost_usd as number) ?? 0;
          turns = (res.num_turns as number) ?? 0;
          break;
        }
      }
    }

    const durationMs = Date.now() - startTime;
    upsertSession(resultSessionId, agentName, totalCost, turns, prompt);

    if (resultSessionId)
    {
      const fullAssistantText = assistantTextParts.join("");
      const newMessages: Array<{ role: "user" | "assistant"; content: string }> = [{ role: "user", content: prompt },];

      if (fullAssistantText) 
      {
        newMessages.push({ role: "assistant", content: fullAssistantText });
      }

      appendMessages(resultSessionId, newMessages);
    }

    send(ws, {
      type: "result",
      sessionId: resultSessionId,
      costUsd: totalCost,
      durationMs,
      turns,
    });
  } 
  catch (err) 
  {
    const errorMsg = err instanceof Error ? err.message : String(err);
    send(ws, { type: "error", message: errorMsg });
  } 
  finally 
  {
    ACTIVE_QUERIES.delete(ws);
  }
}

/**
 * Cancels the active query for a specific WebSocket connection.
 * @param ws The WebSocket whose query should be cancelled.
 */
function cancelQuery(ws: WebSocket): void 
{
  for (const [id, pending] of PENDING_PERMISSIONS)
  {
    if (pending.ws === ws)
    {
      clearTimeout(pending.timer);
      pending.resolve({ behavior: "deny", message: "Query cancelled." });
      PENDING_PERMISSIONS.delete(id);
    }
  }

  const q = ACTIVE_QUERIES.get(ws);

  if (q) 
  {
    try 
    {
      q.interrupt();
    } 
    catch(error)
    {
      console.warn("[server] Could not interrupt query (may already be finished):", error);
    }

    ACTIVE_QUERIES.delete(ws);
  }
}

// ─── Helpers ───

/**
 * Sends a JSON-serialized {@link ServerMessage} to the client if the connection is still open.
 * @param ws The target WebSocket connection.
 * @param msg The server message to serialize and send.
 */
function send(ws: WebSocket, msg: ServerMessage): void 
{
  if (ws.readyState === WebSocket.OPEN) 
  {
    ws.send(JSON.stringify(msg));
  }
}

// ─── Graceful shutdown ───

/**
 * Gracefully shuts down the server: interrupts all active queries,
 * clears the query map, and closes the WebSocket server before exiting.
 */
function shutdown(): void
{
  for (const q of ACTIVE_QUERIES.values()) 
  {
    try 
    { 
      q.interrupt(); 
    } 
    catch(error)
    {
      console.warn("[server] Could not interrupt query during shutdown:", error);
    }
  }
  
  ACTIVE_QUERIES.clear();
  WSS.close(() => process.exit(0));
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);