/**
 * Centralized constants for the Agent SDK Server.
 * All magic strings and numbers extracted here to improve maintainability.
 * Convention: SCREAMING_SNAKE_CASE for all constants.
 *
 * @packageDocumentation
 */

// ── Network Defaults ──

/** Default TCP port for the Agent SDK WebSocket server. */
export const DEFAULT_AGENT_PORT = 9100;

/** Default TCP port for the Unity C# MCP HTTP server. */
export const DEFAULT_MCP_PORT = 8090;

/** Default hostname for server binding. */
export const DEFAULT_HOST = "localhost";

/** Default full URL for the Unity C# MCP server. */
export const DEFAULT_MCP_URL = "http://localhost:8090";

/** Default Claude model identifier. */
export const DEFAULT_MODEL = "claude-sonnet-4-6";

/** Default permission mode for the Agent SDK. */
export const DEFAULT_PERMISSION_MODE = "default";

/** Default HTTP request timeout in milliseconds. */
export const DEFAULT_REQUEST_TIMEOUT_MS = 30000;

/** Timeout for waiting on user permission responses in milliseconds (2 minutes). */
export const PERMISSION_TIMEOUT_MS = 120000;

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

/** MCP server identifier used in Agent SDK mcpServers config. */
export const MCP_SERVER_ID = "unity-mcp";

/** MCP transport type (STDIO-based). */
export const MCP_TRANSPORT_TYPE = "stdio" as const;

/** Command used to spawn the MCP proxy process. */
export const MCP_COMMAND = "node";

// ── HTTP ──

/** Standard JSON content type header value. */
export const CONTENT_TYPE_JSON = "application/json";

/** Relative path to the auth token file written by the C# MCP server. */
export const AUTH_TOKEN_FILE = "Library/GameDeck/auth-token";

// ── Agent / Skill Discovery ──

/** Package-relative directory for agent definitions. */
export const PACKAGE_AGENTS_DIR = "Agents~";

/** Package-relative directory for skill definitions. */
export const PACKAGE_SKILLS_DIR = "Skills~";

/** Markdown file extension. */
export const MARKDOWN_EXT = ".md";

/** Filenames to check (in order) when loading a skill description. */
export const FALLBACK_DESC_FILES = ["SKILL.md", "prompt.md", "README.md", "index.md"] as const;

/** Frontmatter key that holds the tools list. */
export const FRONTMATTER_TOOLS_KEY = "tools";

/** Maximum character length for auto-extracted descriptions. */
export const MAX_DESCRIPTION_LENGTH = 120;

// ── Sessions ──

/** Filename for persisted session data. */
export const SESSIONS_FILE = ".game-deck-sessions.json";

/** Maximum number of sessions to persist to disk. */
export const MAX_SESSIONS = 100;

/** Maximum number of messages to keep per session. */
export const MAX_MESSAGES_PER_SESSION = 50;

/** Session expiration time in milliseconds (30 days). */
export const SESSION_EXPIRATION_MS = 30 * 24 * 60 * 60 * 1000;

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
