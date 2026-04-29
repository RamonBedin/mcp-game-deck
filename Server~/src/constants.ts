/**
 * Centralized constants for the MCP Game Deck proxy server (`mcp-proxy.ts`).
 * All magic strings and numbers extracted here to improve maintainability.
 * Convention: SCREAMING_SNAKE_CASE for all constants.
 *
 * @remarks
 * Under ADR-001 (Claude Code as engine), the custom Agent SDK Server was removed.
 * Constants previously used by `index.ts`, `agents.ts`, `sessions.ts` etc. were
 * removed in the same cleanup pass. Only the constants required by the surviving
 * STDIO MCP proxy remain here.
 *
 * @packageDocumentation
 */

// ── Network Defaults ──

/** Default TCP port for the Unity C# MCP HTTP server. */
export const DEFAULT_MCP_PORT = 8090;

/** Default hostname for server binding. */
export const DEFAULT_HOST = "localhost";

/** Default HTTP request timeout in milliseconds. */
export const DEFAULT_REQUEST_TIMEOUT_MS = 30000;

// ── Retry / Backoff ──

/** Maximum number of retry attempts for transient connection errors. */
export const MAX_RETRIES = 15;

/** Initial delay between retries in milliseconds. */
export const RETRY_BASE_DELAY_MS = 1000;

/** Maximum delay between retries in milliseconds. */
export const RETRY_MAX_DELAY_MS = 3000;

/** Exponential backoff multiplier for retry delays. */
export const RETRY_BACKOFF_FACTOR = 1.3;

/** Maximum connection attempts when waiting for Unity MCP server startup. */
export const WAIT_MAX_ATTEMPTS = 10;

/** Initial delay between startup wait attempts in milliseconds. */
export const WAIT_BASE_DELAY_MS = 500;

/** Maximum delay between startup wait attempts in milliseconds. */
export const WAIT_MAX_DELAY_MS = 5000;

/** Exponential backoff multiplier for startup wait delays. */
export const WAIT_BACKOFF_FACTOR = 1.5;

/** Timeout for MCP health check requests in milliseconds. */
export const HEALTH_CHECK_TIMEOUT_MS = 2000;

// ── MCP Server Identity ──

/** Display name of the MCP proxy server. */
export const MCP_SERVER_NAME = "MCP Game Deck";

/** Version string of the MCP proxy server. */
export const MCP_SERVER_VERSION = "1.1.0";

// ── HTTP ──

/** Standard JSON content type header value. */
export const CONTENT_TYPE_JSON = "application/json";

/** Relative path to the auth token file written by the C# MCP server. */
export const AUTH_TOKEN_FILE = "Library/GameDeck/auth-token";

// ── Connection Error Codes ──

/** Node.js error codes that indicate a transient connection failure. */
export const TRANSIENT_ERROR_CODES = [
  "ECONNREFUSED",
  "ECONNRESET",
  "EPIPE",
  "UND_ERR_SOCKET",
  "UND_ERR_CONNECT_TIMEOUT",
] as const;

/** Error message substrings that indicate a transient connection failure. */
export const TRANSIENT_ERROR_MESSAGES = [
  "fetch failed",
  "terminated",
] as const;

/** Error codes that trigger graceful shutdown on uncaught exceptions. */
export const FATAL_ERROR_CODES = [
  "EPIPE",
  "EOF",
  "ERR_USE_AFTER_CLOSE",
] as const;

