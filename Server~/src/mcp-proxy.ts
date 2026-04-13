/**
 * MCP Game Deck — MCP Proxy Server
 *
 * Transparent STDIO-based MCP proxy that forwards all tool, resource, and prompt
 * requests to the C# MCP server running inside the Unity Editor via HTTP JSON-RPC.
 *
 * This is the entry point for Claude Desktop / Claude Code MCP integration and is
 * also spawned by the Agent SDK Server ({@link index.ts}) as a child process.
 * No tool schemas are declared locally — everything is proxied as-is, which is why
 * the low-level {@link Server} class is used instead of {@link McpServer}.
 *
 * Connection errors during Unity assembly reloads are handled with automatic retry
 * and exponential backoff (up to {@link MAX_RETRIES} attempts).
 * 
 * Architecture:
 *   Claude ←→ STDIO ←→ This Proxy ←→ HTTP JSON-RPC ←→ C# MCP Server (:8090) ←→ Unity Editor
 * 
 * @packageDocumentation
 */

import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import type { ServerResult } from "@modelcontextprotocol/sdk/types.js";
import { ListToolsRequestSchema, CallToolRequestSchema, ListResourcesRequestSchema, ReadResourceRequestSchema, ListPromptsRequestSchema, GetPromptRequestSchema, } from "@modelcontextprotocol/sdk/types.js";
import { readFileSync } from "fs";
import path from "path";
import {
  DEFAULT_MCP_PORT, DEFAULT_HOST, DEFAULT_REQUEST_TIMEOUT_MS,
  MAX_RETRIES, RETRY_BASE_DELAY_MS, RETRY_MAX_DELAY_MS, RETRY_BACKOFF_FACTOR,
  WAIT_MAX_ATTEMPTS, WAIT_BASE_DELAY_MS, WAIT_MAX_DELAY_MS, WAIT_BACKOFF_FACTOR,
  HEALTH_CHECK_TIMEOUT_MS, MCP_SERVER_NAME, MCP_SERVER_VERSION,
  CONTENT_TYPE_JSON, TRANSIENT_ERROR_CODES, TRANSIENT_ERROR_MESSAGES, FATAL_ERROR_CODES,
  AUTH_TOKEN_FILE,
} from "./constants.js";

// ─── Host Validation ───

const ALLOWED_HOSTS = new Set(["localhost", "127.0.0.1", "::1"]);

/**
 * Validates that the MCP host is a loopback address.
 * Falls back to localhost if a non-local host is configured.
 * @param host The host string to validate.
 * @returns A safe loopback host string.
 */
function validateHost(host: string): string
{
  if (ALLOWED_HOSTS.has(host))
  {
    return host;
  }

  process.stderr.write(`[warn] UNITY_MCP_HOST '${host}' is not a loopback address. Falling back to localhost for security.\n`);
  return DEFAULT_HOST;
}

// ─── Configuration ───

const UNITY_PORT = parseInt(process.env.UNITY_MCP_PORT ?? String(DEFAULT_MCP_PORT), 10);
const UNITY_HOST = validateHost(process.env.UNITY_MCP_HOST ?? DEFAULT_HOST);
const UNITY_URL = `http://${UNITY_HOST}:${UNITY_PORT}/`;
const REQUEST_TIMEOUT_MS = parseInt(process.env.REQUEST_TIMEOUT_MS ?? String(DEFAULT_REQUEST_TIMEOUT_MS), 10);
const AUTH_TOKEN = loadAuthToken();
const SERVER = new Server(
  {
    name: MCP_SERVER_NAME,
    version: MCP_SERVER_VERSION,
  },
  {
    capabilities: {
      tools: {},
      resources: {},
      prompts: {},
    },
  }
);

let requestCounter = 0;
let isShuttingDown = false;

// ─── Auth Token ───

/**
 * Loads the auth token from environment variable or from the token file
 * written by the C# MCP server at startup.
 * @returns The auth token string, or empty string if not found.
 */
function loadAuthToken(): string
{
  const envToken = process.env.UNITY_MCP_AUTH_TOKEN;
  
  if (envToken)
  {
    return envToken;
  }
  try
  {
    const cwd = process.env.PROJECT_CWD ?? process.cwd();
    return readFileSync(path.join(cwd, AUTH_TOKEN_FILE), "utf-8").trim();
  }
  catch
  {
    log("warn", "No auth token found — requests will be unauthenticated");
    return "";
  }
}

// ─── Logging  ───

function log(level: string, msg: string): void 
{
  process.stderr.write(`[${level}] ${msg}\n`);
}

// ─── HTTP JSON-RPC bridge to C# MCP server ───

/**
 * Sends a JSON-RPC request to the Unity C# MCP server and returns the result.
 * Retries automatically on transient connection errors (e.g. during assembly reload)
 * with exponential backoff for up to ~15 seconds.
 * @param method The JSON-RPC method (e.g. "tools/list", "tools/call")
 * @param params The parameters for the request
 * @returns The result field from the JSON-RPC response
 * @throws Error if the server returns an error or all retries are exhausted
 */
async function forwardToUnity(method: string, params?: unknown): Promise<unknown>
{
  for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) 
  {
    const id = `proxy-${++requestCounter}`;
    const body = JSON.stringify({
      jsonrpc: "2.0",
      id,
      method,
      params: params ?? {},
    });

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);
    try 
    {
      const res = await fetch(UNITY_URL, {
        method: "POST",
        headers: {
          "Content-Type": CONTENT_TYPE_JSON,
          ...(AUTH_TOKEN ? { "Authorization": `Bearer ${AUTH_TOKEN}` } : {}),
        },
        body,
        signal: controller.signal,
      });

      clearTimeout(timeout);

      if (!res.ok) 
      {
        throw new Error(`Unity MCP HTTP ${res.status}: ${res.statusText}`);
      }

      const json = (await res.json()) as {result?: unknown; error?: { code?: number; message?: string; data?: unknown };};

      if (json.error) 
      {
        const err = json.error;
        throw new Error(err.message ?? `Unity MCP error code ${err.code}`);
      }

      return json.result;
    } 
    catch (err: unknown) 
    {
      clearTimeout(timeout);

      if (err instanceof DOMException && err.name === "AbortError") 
      {
        throw new Error(`Unity MCP request timed out after ${REQUEST_TIMEOUT_MS}ms (method: ${method})`);
      }

      const errAny = err as Record<string, unknown>;
      const causeAny = errAny.cause as Record<string, unknown> | undefined;
      const errCode = (causeAny?.code as string) ?? (errAny.code as string) ?? "";
      const errMsg = err instanceof Error ? err.message : String(err);
      const isConnectionError =
        (TRANSIENT_ERROR_CODES as readonly string[]).includes(errCode) ||
        (TRANSIENT_ERROR_MESSAGES as readonly string[]).some((m) => errMsg.includes(m));

      if (isConnectionError && attempt < MAX_RETRIES) 
      {
        const delay = Math.min(RETRY_BASE_DELAY_MS * Math.pow(RETRY_BACKOFF_FACTOR, attempt - 1), RETRY_MAX_DELAY_MS,);

        log("warn", `Unity MCP unreachable (${errCode || errMsg.substring(0, 50)}), retry ${attempt}/${MAX_RETRIES} in ${Math.round(delay)}ms... [${method}]`);
        await new Promise((r) => setTimeout(r, delay));
        continue;
      }

      throw err;
    }
  }

  throw new Error(`Unity MCP unreachable after ${MAX_RETRIES} retries (method: ${method})`);
}

/**
 * Waits for the Unity C# MCP server to become reachable.
 * Sends GET requests with exponential backoff (factor 1.5, capped at 5s).
 * If the server never responds, logs a warning and returns anyway so the
 * proxy can start — requests will fail-and-retry individually via {@link forwardToUnity}.
 * @param maxAttempts Maximum number of connection attempts before giving up. Default: 10.
 * @param baseDelayMs Initial delay between retries in milliseconds. Default: 500.
 * @returns Resolves when the server is reachable or all attempts are exhausted.
 */
async function waitForUnity(maxAttempts = WAIT_MAX_ATTEMPTS, baseDelayMs = WAIT_BASE_DELAY_MS): Promise<void>
{
  for (let attempt = 1; attempt <= maxAttempts; attempt++)
  {
    try 
    {
      const res = await fetch(UNITY_URL, {method: "GET", signal: AbortSignal.timeout(HEALTH_CHECK_TIMEOUT_MS), });

      if (res.ok) 
      {
        log("info", `Unity MCP server reachable at ${UNITY_URL}`);
        return;
      }
    } 
    catch(error)
    {
      log("warn", `Unity MCP not reachable on attempt ${attempt}: ${error instanceof Error ? error.message : String(error)}`);
    }

    if (attempt < maxAttempts) 
    {
      const delay = Math.min(baseDelayMs * Math.pow(WAIT_BACKOFF_FACTOR, attempt - 1), WAIT_MAX_DELAY_MS);
      log("info", `Unity MCP not ready, retry ${attempt}/${maxAttempts} in ${Math.round(delay)}ms...`);
      await new Promise((r) => setTimeout(r, delay));
    }
  }

  log("warn", `Unity MCP server not reachable after ${maxAttempts} attempts — starting proxy anyway (requests will fail until Unity is ready)`);
}

// ─── Tool handlers ───

SERVER.setRequestHandler(ListToolsRequestSchema, async () => {
  log("info", "tools/list → forwarding to Unity");
  const result = (await forwardToUnity("tools/list")) as { tools?: unknown[] };
  const toolCount = result?.tools?.length ?? 0;
  log("info", `tools/list ← ${toolCount} tools`);
  return result;
});

SERVER.setRequestHandler(CallToolRequestSchema, async (request) => {
  const toolName = request.params.name;
  log("info", `tools/call [${toolName}] → forwarding to Unity`);
  const result = await forwardToUnity("tools/call", request.params);
  log("info", `tools/call [${toolName}] ← done`);
  return result as ServerResult;
});

// ─── Resource handlers ───

SERVER.setRequestHandler(ListResourcesRequestSchema, async () => {
  log("info", "resources/list → forwarding to Unity");
  const result = (await forwardToUnity("resources/list")) as { resources?: unknown[] };
  const count = result?.resources?.length ?? 0;
  log("info", `resources/list ← ${count} resources`);
  return result;
});

SERVER.setRequestHandler(ReadResourceRequestSchema, async (request) => {
  const uri = request.params.uri;
  log("info", `resources/read [${uri}] → forwarding to Unity`);
  const result = await forwardToUnity("resources/read", request.params);
  log("info", `resources/read [${uri}] ← done`);
  return result as ServerResult;
});

// ─── Prompt handlers ───

SERVER.setRequestHandler(ListPromptsRequestSchema, async () => {
  log("info", "prompts/list → forwarding to Unity");
  const result = (await forwardToUnity("prompts/list")) as { prompts?: unknown[] };
  const count = result?.prompts?.length ?? 0;
  log("info", `prompts/list ← ${count} prompts`);
  return result;
});

SERVER.setRequestHandler(GetPromptRequestSchema, async (request) => {
  const promptName = request.params.name;
  log("info", `prompts/get [${promptName}] → forwarding to Unity`);
  const result = await forwardToUnity("prompts/get", request.params);
  log("info", `prompts/get [${promptName}] ← done`);
  return result as ServerResult;
});

// ─── Startup ───

/**
 * Entry point for the MCP Proxy process.
 * Waits for the Unity C# MCP server to become reachable, then binds a
 * {@link StdioServerTransport} and starts listening for JSON-RPC messages.
 * @returns Resolves when the server is connected and ready.
 */
async function main(): Promise<void> 
{
  log("info", "MCP Game Deck Proxy starting...");
  log("info", `Target: ${UNITY_URL}`);

  await waitForUnity();

  const transport = new StdioServerTransport();
  await SERVER.connect(transport);

  log("info", "MCP Proxy server ready — listening on STDIO");
}

// ─── Graceful shutdown ───

/**
 * Gracefully shuts down the MCP Proxy.
 * Closes the {@link Server} connection and exits the process.
 * Guarded by {@link isShuttingDown} to prevent re-entrant calls from
 * multiple signals (SIGINT, SIGTERM, SIGHUP, stdin close).
 */
async function shutdown(): Promise<void> 
{
  if (isShuttingDown)
  {
    return;
  }

  isShuttingDown = true;

  log("info", "Shutting down...");
  try 
  {
    await SERVER.close();
  } 
  catch(error)
  {
    log("warn", `Error closing MCP server during shutdown: ${error instanceof Error ? error.message : String(error)}`);
  }

  process.exit(0);
}

// ─── Start ───

main().catch((err) => {
  log("error", `Fatal: ${err instanceof Error ? err.message : String(err)}`);
  process.exit(1);
});

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
process.on("SIGHUP", shutdown);
process.stdin.on("close", shutdown);
process.stdin.on("end", shutdown);

process.on("uncaughtException", (error: NodeJS.ErrnoException) => {
  if ((FATAL_ERROR_CODES as readonly string[]).includes(error.code ?? ""))
  {
    shutdown();
    return;
  }
  
  log("error", `Uncaught exception: ${error.message}`);
  process.exit(1);
});

process.on("unhandledRejection", (reason) => {
  log("error", `Unhandled rejection: ${reason}`);
  process.exit(1);
});
