// App~/runtime/sdk-entry.js
//
// Bridge process between the Tauri Rust supervisor and
// @anthropic-ai/claude-agent-sdk. Reads JSON lines from stdin,
// forwards to the SDK via query(), and emits typed JSON lines back
// on stdout per the AgentMessage protocol from
// App~/src-tauri/src/types.rs.
//
// Generated at runtime by claude_supervisor::runtime_setup. Do NOT
// commit edits — they will be overwritten on next launch.
//
// SDK note: query() returns an AsyncGenerator<SDKMessage>. The SDK
// resolves its own claude binary via optional dependency npm packages
// (@anthropic-ai/claude-agent-sdk-<platform>-<arch>); if those fail
// to install, options.pathToClaudeCodeExecutable can point at the
// system PATH `claude` instead. F02 task 6.5 wires that fallback if
// version-drift detection finds a problem — for 2.2, default behavior.

import { query } from "@anthropic-ai/claude-agent-sdk";
import { promises as fsp } from "node:fs";
import fsSync from "node:fs";
import path from "node:path";
import readline from "node:readline";

// region: stdio hygiene

process.stdin.setEncoding("utf8");

// endregion

// region: stdout protocol

/**
 * Writes a structured payload to stdout as one NDJSON line so the C# host can
 * parse each emission independently.
 *
 * @param {object} message - The payload to serialize and emit.
 * @returns {void}
 */
function emit(message)
{
  process.stdout.write(JSON.stringify({ message }) + "\n");
}

/**
 * Emits a `text-delta` envelope carrying one streamed chunk of model-
 * generated text within an in-flight turn. The `turnId` ties the
 * delta to the assistant message accumulating it on the host side.
 *
 * @param {string} turnId - Stable id for the current turn.
 * @param {string} text - The text chunk to append.
 * @returns {void}
 */
function emitTextDelta(turnId, text)
{
  emit({ type: "text-delta", turnId, text });
}

/**
 * Emits a `tool-use` envelope: Claude is calling an MCP tool. Sent
 * pre-permission so the host can render the call before the result
 * arrives. `input` is the parsed JSON object the tool will be invoked
 * with; on parse failure the caller emits a `_parseError` placeholder.
 *
 * @param {string} turnId - Stable id of the current turn.
 * @param {string} toolUseId - SDK-assigned id for this tool invocation.
 * @param {string} name - Tool name (e.g. `mcp__game-deck__list_scenes`).
 * @param {unknown} input - Parsed input payload.
 * @returns {void}
 */
function emitToolUse(turnId, toolUseId, name, input)
{
  emit({ type: "tool-use", turnId, toolUseId, name, input });
}

/**
 * Emits a `tool-result` envelope: the matching `tool-use` returned.
 * `content` matches the SDK's `tool_result.content` shape — usually a
 * string but can be a structured array; the host handles both. The
 * `toolUseId` ties this back to the originating `tool-use`.
 *
 * @param {string} turnId - Stable id of the current turn.
 * @param {string} toolUseId - Matches the originating `tool-use`.
 * @param {unknown} content - Raw payload returned by the tool.
 * @param {boolean} isError - True when the tool reported an error.
 * @returns {void}
 */
function emitToolResult(turnId, toolUseId, content, isError)
{
  emit({ type: "tool-result", turnId, toolUseId, content, isError });
}

/**
 * Emits an `assistant-turn-complete` envelope to signal that the
 * current `query()` round-trip has finished. Carries the same
 * `turnId` as the deltas it closes.
 *
 * @param {string} turnId - Stable id for the turn that just ended.
 * @returns {void}
 */
function emitTurnComplete(turnId)
{
  emit({ type: "assistant-turn-complete", turnId });
}

/**
 * Emits an `error` envelope describing a failure that the host should surface
 * to the user.
 *
 * @param {string} message - Human-readable failure description.
 * @returns {void}
 */
function emitError(message)
{
  emit({ type: "error", message });
}

/**
 * Emits a `permission-mode-changed` envelope echoing the mode that
 * was just applied. The Rust side translates this to the
 * `permission-mode-changed` Tauri event so React's
 * `PermissionModeToggle` can passively sync (Task 4.3).
 *
 * @param {string} mode - One of the five UI permission modes.
 * @returns {void}
 */
function emitPermissionModeChanged(mode)
{
  emit({ type: "permission-mode-changed", mode });
}

/**
 * Emits a `health-ok` envelope. The Rust side transitions the
 * supervisor status from `Starting` to `Ready` on receipt — Task 6.1.
 *
 * @returns {void}
 */
function emitHealthOk()
{
  emit({ type: "health-ok" });
}

/**
 * Emits a `health-failed` envelope. The Rust side transitions the
 * supervisor status to `Crashed` and surfaces the message in stderr.
 *
 * @param {string} message - Cause of failure (timeout, SDK error, etc.).
 * @returns {void}
 */
function emitHealthFailed(message)
{
  emit({ type: "health-failed", message });
}

/**
 * Emits an `ask-user-requested` envelope: Claude is calling the
 * built-in `AskUserQuestion` tool with one or more clarifying
 * questions. The host renders a question card; the user's answer
 * round-trips back via a `respond-to-request` stdin line (task 1.3).
 *
 * @param {string} requestId - SDK's `toolUseID` for this invocation.
 * @param {string | null} turnId - Stable id of the current turn.
 * @param {string | null} agentId - Subagent id when the call originates
 *   from a delegated context, else null.
 * @param {object} input - Raw `AskUserQuestionInput` from the SDK.
 * @returns {void}
 */
function emitAskUserRequested(requestId, turnId, agentId, input)
{
  emit({ type: "ask-user-requested", requestId, turnId, agentId, input });
}

/**
 * Emits a `permission-requested` envelope: Claude wants to call a tool
 * that isn't auto-approved under the current `permissionMode`. The
 * host renders a permission card; the user's choice round-trips back
 * via a `respond-to-request` stdin line (task 1.3).
 *
 * @param {string} requestId - SDK's `toolUseID` for this invocation.
 * @param {string | null} turnId - Stable id of the current turn.
 * @param {string | null} agentId - Subagent id when the call originates
 *   from a delegated context, else null.
 * @param {string} toolName - Name of the tool the SDK is gating.
 * @param {unknown} input - The tool input that would be passed to the call.
 * @param {string | null} blockedPath - Filesystem path the tool would
 *   touch, when the SDK supplied it; else null.
 * @param {string | null} decisionReason - SDK's reason hint, when supplied; else null.
 * @returns {void}
 */
function emitPermissionRequested(requestId, turnId, agentId, toolName, input, blockedPath, decisionReason)
{
  emit({ type: "permission-requested", requestId, turnId, agentId, toolName, input, blockedPath, decisionReason });
}

/**
 * Emits a `request-resolved` envelope: the awaited `canUseTool`
 * promise resolved (either via user response or via the in-session
 * Allow Always cache short-circuit). Lets the host transition the
 * matching card to its terminal visual state.
 *
 * @param {string} requestId - SDK's `toolUseID` for this invocation.
 * @param {"allow" | "allow-always" | "deny" | "auto-allowed"} outcome
 *   - The terminal decision applied to the request.
 * @param {object | null | undefined} answer - When the request was a
 *   question, the `AskUserQuestionOutput` payload; else null.
 * @param {string | null | undefined} toolName - Populated specifically
 *   on `outcome === "auto-allowed"` so the host can synthesize a
 *   compact block in the chat (task 3.5) without having seen a prior
 *   `permission-requested`. Null/undefined for other outcomes.
 * @param {string | null | undefined} turnId - Same as `toolName` —
 *   present on `auto-allowed` to attach the synthetic block to the
 *   correct turn.
 * @returns {void}
 */
function emitRequestResolved(requestId, outcome, answer, toolName, turnId)
{
  emit({ type: "request-resolved", requestId, outcome, answer: answer ?? null, toolName: toolName ?? null, turnId: turnId ?? null });
}

/**
 * Writes a debug line to stderr, prefixed with `[sdk-entry]`. Non-string args
 * are JSON-stringified so structured payloads remain inspectable in the host
 * log.
 *
 * @param {...unknown} args - Arbitrary values to serialize and log.
 * @returns {void}
 */
function debug(...args)
{
  process.stderr.write(
    "[sdk-entry] " +
      args.map((a) => (typeof a === "string" ? a : JSON.stringify(a))).join(" ") +
      "\n",
  );
}

// endregion

// region: permission mode

/**
 * Five surface-level permission modes mirrored from the Rust enum
 * (`types::PermissionMode`). The SDK only accepts the first four —
 * `auto` is a UI alias for `bypassPermissions` (see CLAUDE.md gotcha)
 * and is mapped via {@link resolveSdkMode} before reaching `query()`.
 */
const VALID_PERMISSION_MODES = new Set([
  "default",
  "acceptEdits",
  "plan",
  "bypassPermissions",
  "auto",
]);

/**
 * Currently-selected permission mode, kept in sync with the Rust-side
 * `ClaudeSupervisor.permission_mode` via stdin control messages
 * (`{type:"setPermissionMode", mode:"..."}`). Applied to every
 * `query()` round-trip via `options.permissionMode`.
 *
 * @type {string}
 */
let currentPermissionMode = "default";

/**
 * Maps the UI-level permission mode string to one the SDK's
 * `query()` actually understands. `auto` collapses to
 * `bypassPermissions` (CLAUDE.md gotcha — historical v1 behavior we
 * preserve in v2); the other four pass through verbatim.
 *
 * @param {string} mode - One of the five UI modes.
 * @returns {string} A mode the SDK accepts.
 */
function resolveSdkMode(mode)
{
  if (mode === "auto")
  {
    return "bypassPermissions";
  }
  return mode;
}

// endregion

// region: attachments

/**
 * Maps a lower-cased file extension (without leading dot) to the
 * Anthropic API media-type string for image / document blocks.
 * Returns `null` for extensions we treat as plain text — those are
 * read as utf-8 and embedded via a `text` content block.
 *
 * @param {string} ext - Extension without leading dot, lower-cased.
 * @returns {{kind: "image" | "document", mediaType: string} | null}
 */
function attachmentKindFor(ext)
{
  switch (ext)
  {
    case "png":
      return { kind: "image", mediaType: "image/png" };
    case "jpg":
    case "jpeg":
      return { kind: "image", mediaType: "image/jpeg" };
    case "gif":
      return { kind: "image", mediaType: "image/gif" };
    case "webp":
      return { kind: "image", mediaType: "image/webp" };
    case "pdf":
      return { kind: "document", mediaType: "application/pdf" };
    default:
      return null;
  }
}

/**
 * Reads `filePath` and converts it to an Anthropic content block.
 * Image / PDF files become base64-sourced `image` / `document` blocks;
 * everything else is read as utf-8 text and embedded inside a
 * `document` block with a `PlainTextSource`. The SDK then passes the
 * block straight to the model — no path leakage past this boundary.
 *
 * Read failures emit an `error` envelope to the host and return
 * `null` so the caller can drop that attachment from the prompt.
 *
 * @param {string} filePath - Absolute path supplied by Tauri.
 * @returns {Promise<object | null>} The content block, or `null` on
 *   read error.
 */
async function buildAttachmentBlock(filePath)
{
  const ext = path.extname(filePath).slice(1).toLowerCase();
  const kind = attachmentKindFor(ext);
  const baseName = path.basename(filePath);
  try
  {
    if (kind === null)
    {
      const text = await fsp.readFile(filePath, "utf8");

      return {
        type: "document",
        source: {
          type: "text",
          media_type: "text/plain",
          data: text,
        },
        title: baseName,
      };
    }

    const buffer = await fsp.readFile(filePath);
    const data = buffer.toString("base64");
    return {
      type: kind.kind,
      source: {
        type: "base64",
        media_type: kind.mediaType,
        data,
      },
      ...(kind.kind === "document" ? { title: baseName } : {}),
    };
  }
  catch (err)
  {
    debug("attachment read failed:", filePath, String(err));
    emitError(`Could not read attachment ${baseName}: ${err instanceof Error ? err.message : String(err)}`);
    return null;
  }
}

// endregion

// region: rules bundle resolution

/**
 * Reads the rules bundle env var and returns the bundle file's
 * contents when the file exists with non-zero size, so the caller
 * can set `queryOptions.systemPrompt.append`. Returns `null` when
 * the env var is unset, the path doesn't exist, the file is empty,
 * or any stat / read error occurs — those cases all collapse into
 * "no rules" and the caller omits the option entirely.
 *
 * Sync `statSync` + `readFileSync` are fine on the boot / per-query
 * hot path: the file is local, sub-millisecond, and we'd read it
 * once per turn no matter what. The size check via `statSync` runs
 * first so an oversized rule bundle doesn't slurp into memory
 * unnecessarily (cap of 10 enabled rules keeps practical size well
 * under any concern, but the staged check costs nothing).
 *
 * F08 task 3.3 — passed inline via the SDK's `systemPrompt: { type:
 * "preset", preset: "claude_code", append }` shape. The SDK handles
 * the Windows ~32KB CreateProcess ceiling internally by spilling
 * long appends to a tempfile and forwarding `--append-system-prompt-file`
 * to the CLI, so the host doesn't need to mediate that.
 *
 * @returns {string | null} The bundle file's UTF-8 contents when
 *   it's safe to inject; null to omit.
 */
function resolveRulesBundleContent()
{
  const bundlePath = process.env.MCP_GAME_DECK_RULES_BUNDLE_PATH;

  if (!bundlePath || bundlePath.length === 0)
  {
    return null;
  }
  try
  {
    const stats = fsSync.statSync(bundlePath);

    if (!stats.isFile() || stats.size === 0)
    {
      return null;
    }

    const content = fsSync.readFileSync(bundlePath, "utf8");
    return content.length > 0 ? content : null;
  }
  catch (err)
  {
    return null;
  }
}

// endregion

// region: health check

const HEALTH_CHECK_TIMEOUT_MS = 15000;

/**
 * Runs a minimal `query()` round-trip with the prompt `"__health__"`,
 * consuming the assistant turn locally and emitting `health-ok` on
 * success or `health-failed` on timeout / SDK error. The internal
 * messages (text-delta / tool-use / turn-complete) are NOT forwarded
 * to the host — the chat UI stays untouched by the probe.
 *
 * Future optimization: switch to streaming-input mode and use
 * `q.initializationResult()` instead of a real query — burns zero
 * tokens. Requires refactor of `handleInput` to streaming input.
 * Consider when spawn frequency / cost becomes a concern.
 *
 * @returns {Promise<void>} Resolves once an outcome envelope has
 *   been emitted.
 */
async function runHealthCheck()
{
  let q;
  try
  {
    const healthOptions = {
      cwd: projectPath,
      plugins: buildPlugins(),
      additionalDirectories: buildAdditionalDirectories(),
    };
    const healthBundleContent = resolveRulesBundleContent();

    if (healthBundleContent !== null)
    {
      healthOptions.systemPrompt = {
        type: "preset",
        preset: "claude_code",
        append: healthBundleContent,
      };
    }
    q = query({
      prompt: "__health__",
      options: healthOptions,
    });
  }
  catch (err)
  {
    emitHealthFailed(err instanceof Error ? err.message : String(err));
    return;
  }

  let timer;
  const timeoutPromise = new Promise((_, reject) => {
    timer = setTimeout(
      () => reject(new Error(`health check timed out after ${HEALTH_CHECK_TIMEOUT_MS}ms`)),
      HEALTH_CHECK_TIMEOUT_MS,
    );
  });

  const consume = (async () => {
    for await (const msg of q)
    {
      if (msg?.type === "system" && msg?.subtype === "init")
      {
        emitCatalogOnInit(msg);
      }
      else if (msg?.type === "result")
      {
        return;
      }
    }
  })();
  try
  {
    await Promise.race([consume, timeoutPromise]);
    emitHealthOk();
  }
  catch (err)
  {
    debug("health check failed:", String(err));
    emitHealthFailed(err instanceof Error ? err.message : String(err));
  }
  finally
  {
    if (timer !== undefined)
    {
      clearTimeout(timer);
    }
  }
}

// endregion

// region: resume session

/**
 * Session id the supervisor wants the SDK to resume on the next
 * `query()` round-trip. Updated via `setResumeSession` /
 * `clearResumeSession` stdin control messages from the Rust side.
 * Stays set across consecutive turns — the SDK appends each turn to
 * the same session JSONL until React picks a different session or
 * starts a new one.
 *
 * @type {string | null}
 */
let pendingResumeSessionId = null;

// endregion

// region: catalog emit


const BUILTIN_COMMANDS = new Set([
  "clear", "help", "cost", "permissions", "agents", "init",
  "login", "logout", "model", "review", "security-review",
  "status", "exit",
  "update-config", "debug", "simplify", "batch",
  "fewer-permission-prompts", "loop", "schedule", "claude-api",
  "compact", "context", "heapdump", "extra-usage", "usage",
  "insights", "team-onboarding",
]);

/**
 * Classifies a command name's source for the React-side catalog.
 *
 * @param {string} name - Raw command name from the SDK's init payload.
 * @returns {"built-in" | "user-command" | "plugin" | "third-party"}
 */
function classifyCommandSource(name)
{
  if (name.startsWith("mcp-game-deck:"))
  {
    return "plugin";
  }

  if (name.includes(":"))
  {
    return "third-party";
  }

  const bare = name.startsWith("/") ? name.slice(1) : name;

  if (BUILTIN_COMMANDS.has(bare))
  {
    return "built-in";
  }

  return "user-command";
}

/**
 * Classifies an agent name's source. No built-in agent set today —
 * un-namespaced names fall through to `built-in` as a sensible
 * default until the SDK starts surfacing platform-level agents.
 *
 * @param {string} name - Raw agent name from the SDK's init payload.
 * @returns {"built-in" | "plugin" | "third-party"}
 */
function classifyAgentSource(name)
{
  if (name.startsWith("mcp-game-deck:"))
  {
    return "plugin";
  }

  if (name.includes(":"))
  {
    return "third-party";
  }

  return "built-in";
}

/**
 * Last emitted catalog payload as JSON, for compare-and-skip. Reset
 * to null on every fresh JS process (supervisor restart). String
 * compare is cheap and the payload is small.
 *
 * @type {string | null}
 */
let lastCatalogJson = null;

/**
 * One-shot flag: true after the first `system/init` shape has been
 * dumped to stderr. Lets diagnostic output happen once per process
 * without flooding the log on every turn.
 *
 * @type {boolean}
 */
let hasLoggedInitShape = false;

/**
 * Transforms the SDK's `system/init` payload into a `catalog-ready`
 * envelope and emits it on stdout when the contents differ from the
 * cached last emit.
 *
 * Field paths are defensive: the exact shape of `system/init` varies
 * by SDK version, so two common conventions are tried per field
 * (`slash_commands` / `commands`, `agents` / `available_agents`).
 * The raw shape is dumped once per process via debug() — if the
 * catalog ends up empty on smoke, that dump is the diagnostic to
 * inspect.
 *
 * @param {object} initMessage - The full `system/init` SDK message.
 * @returns {void}
 */
function emitCatalogOnInit(initMessage)
{
  if (!hasLoggedInitShape)
  {
    hasLoggedInitShape = true;
    debug("system/init shape:", JSON.stringify(initMessage, null, 2));
  }

  const rawCommands = initMessage.slash_commands ?? initMessage.commands ?? [];
  const rawAgents = initMessage.agents ?? initMessage.available_agents ?? [];

  const commands = rawCommands
    .map((c) => {
      const name = typeof c === "string" ? c : (c?.name ?? c?.command ?? "");
      const description = typeof c === "object" && c !== null ? (c.description ?? "") : "";
      const hint = typeof c === "object" && c !== null ? (c.argumentHint ?? c.argument_hint) : undefined;
      return {
        name,
        description,
        ...(hint ? { argumentHint: hint } : {}),
        source: classifyCommandSource(name),
      };
    })
    .filter((c) => c.name.length > 0);

  const agents = rawAgents
    .map((a) => {
      const name = typeof a === "string" ? a : (a?.name ?? "");
      const description =
        typeof a === "object" && a !== null ? (a.description ?? "") : "";
      return {
        name,
        description,
        source: classifyAgentSource(name),
      };
    })
    .filter((a) => a.name.length > 0);

  const payload = { type: "catalog-ready", commands, agents };
  const payloadJson = JSON.stringify(payload);

  if (payloadJson !== lastCatalogJson)
  {
    emit(payload);
    lastCatalogJson = payloadJson;
    debug(
      "catalog emitted:",
      `${commands.length} commands, ${agents.length} agents`,
    );
  }
}

/**
 * Updates `pendingResumeSessionId` from an SDK message that carries
 * `session_id` (system/init or result). No-op when the id is missing
 * or unchanged. Used by handleInput's stream loop; runHealthCheck
 * deliberately skips this to keep the `__health__` session isolated
 * from the user's conversation continuity.
 *
 * @param {object} msg - Any SDK message; missing session_id is no-op.
 * @returns {void}
 */
function captureSessionId(msg)
{
  if (typeof msg.session_id === "string" && msg.session_id.length > 0)
  {
    if (msg.session_id !== pendingResumeSessionId)
    {
      pendingResumeSessionId = msg.session_id;
      debug("captured session_id:", msg.session_id);
    }
  }
}

/**
 * Composite handler for `system/init` from a user-turn query:
 * captures session_id AND emits the catalog. The health check uses
 * `emitCatalogOnInit` directly because it intentionally skips
 * session_id capture to keep the `__health__` session isolated.
 *
 * @param {object} msg - The full `system/init` SDK message.
 * @returns {void}
 */
function handleSystemInitForUserTurn(msg)
{
  captureSessionId(msg);
  emitCatalogOnInit(msg);
}

// endregion

// region: env contract from F07

const projectPath = process.env.UNITY_PROJECT_PATH;

if (!projectPath || projectPath.length === 0)
{
  emitError("UNITY_PROJECT_PATH env var not set");
  process.exit(1);
}

debug("boot", JSON.stringify({
  cwd: process.cwd(),
  unityProjectPath: projectPath,
  unityMcpHost: process.env.UNITY_MCP_HOST ?? null,
  unityMcpPort: process.env.UNITY_MCP_PORT ?? null,
}));

// endregion

// region: canUseTool callback

/** @typedef {import("@anthropic-ai/claude-agent-sdk").CanUseTool} CanUseTool */
/** @typedef {import("@anthropic-ai/claude-agent-sdk").PermissionResult} PermissionResult */

const pending = new Map();
const allowAlwaysCache = new Set();

/**
 * Deterministic JSON serialization with recursively sorted object keys.
 * Used to build cache keys whose value depends on the *content* of an
 * input, not its incidental property order. Arrays preserve order
 * (positional semantics matter in tool inputs).
 *
 * @param {unknown} value - Any JSON-serializable value.
 * @returns {string} Canonical JSON string.
 */
function stableJSON(value)
{
  if (value === null || typeof value !== "object")
  {
    return JSON.stringify(value);
  }

  if (Array.isArray(value))
  {
    return "[" + value.map(stableJSON).join(",") + "]";
  }

  const keys = Object.keys(value).sort();
  return "{" + keys.map((k) => JSON.stringify(k) + ":" + stableJSON(value[k])).join(",") + "}";
}

/**
 * Builds the Allow Always cache key for a (toolName, input) pair.
 *
 * @param {string} toolName - SDK tool name being gated.
 * @param {unknown} input - Tool input that would be passed to the call.
 * @returns {string} Key suitable for `allowAlwaysCache.has` /
 *   `allowAlwaysCache.add`.
 */
function cacheKey(toolName, input)
{
  return `${toolName}:${stableJSON(input)}`;
}

/**
 * Whether a given option already qualifies as a free-text fallback
 * under React's `isFreeTextOption` heuristic
 * label matches `/^other\b/i` OR description contains the
 * literal substring `"free text"`. Used to skip auto-injection in
 * {@link augmentAskUserQuestionInput} when Claude already provided
 * an Other-style option — avoids double rendering.
 *
 * @param {{label: string, description?: string}} opt - Single option entry.
 * @returns {boolean} True when React's heuristic would treat this as
 *   a free-text fallback already.
 */
function isAlreadyFreeText(opt)
{
  return /^other\b/i.test(opt.label) || (opt.description ?? "").includes("free text");
}

/**
 * Auto-injects an `"Other (specify)"` option into every question that
 * doesn't already have a free-text fallback. Mirrors the behavior of
 * Claude Code CLI's internal AskUserQuestion system prompt ("Users
 * will always be able to select 'Other' to provide custom text
 * input") which the SDK does NOT inject automatically — SDK is
 * deliberately raw, leaving free-text policy to the host.
 *
 * The injected option's `label` and `description` both match React's
 * `isFreeTextOption` heuristic so `QuestionCard` renders the text
 * input without further changes.
 *
 * @param {{questions: Array<object>}} input - The original
 *   `AskUserQuestionInput` from the SDK.
 * @returns {{questions: Array<object>}} Augmented input with Other
 *   options added where missing.
 */
function augmentAskUserQuestionInput(input)
{
  return {
    ...input,
    questions: input.questions.map((q) =>
    {
      if (q.options.some(isAlreadyFreeText))
      {
        return q;
      }

      return {
        ...q,
        options: [
          ...q.options,
          {
            label: "Other (specify)",
            description: "Provide a custom answer as free text.",
          },
        ],
      };
    }),
  };
}

/**
 * Single dispatcher for both kinds of user-input requests Claude Code
 * emits to its host: permission prompts for tool calls in `default`
 * mode, and clarifying questions via the built-in `AskUserQuestion`
 * tool. Branches on `toolName`; React renders each variant with its
 * own card UI (Group 3).
 *
 * Task 1.1 wires the skeleton only — both branches log a marker to
 * stderr and resolve immediately with an `allow` placeholder so the
 * SDK does not stall while wire emit (1.2) and stdin response
 * handling (1.3) are still being built.
 *
 * @type {CanUseTool}
 */
async function canUseToolCallback(toolName, input, opts)
{
  const requestId = opts.toolUseID;
  const turnId = currentTurnId;
  const agentId = opts.agentID ?? null;

  if (toolName === "AskUserQuestion")
  {
    const augmentedInput = augmentAskUserQuestionInput(input);
    emitAskUserRequested(requestId, turnId, agentId, augmentedInput);
    const answer = await new Promise((resolve, reject) => { pending.set(requestId, { resolve, reject, requestType: "question" });});
    emitRequestResolved(requestId, "allow", answer);
    const sanitizedAnswer = { ...answer, questions: input.questions };
    return { behavior: "allow", updatedInput: sanitizedAnswer };
  }

  const key = cacheKey(toolName, input);

  if (allowAlwaysCache.has(key))
  {
    emitRequestResolved(requestId, "auto-allowed", null, toolName, turnId);
    return { behavior: "allow", updatedInput: input };
  }

  emitPermissionRequested(requestId, turnId, agentId, toolName, input, opts.blockedPath ?? null, opts.decisionReason ?? null,);
  const decision = await new Promise((resolve, reject) => { pending.set(requestId, { resolve, reject, requestType: "permission", toolName, input });});
  emitRequestResolved(requestId, decision.outcome);

  if (decision.outcome === "deny")
  {
    return { behavior: "deny", message: "User denied via Tauri UI", interrupt: false };
  }
  return { behavior: "allow", updatedInput: input };
}

// endregion

// region: input → query() round-trip

/**
 * Generates a stable id for a single `handleInput` call. Used to
 * tie every `text-delta` to the assistant message it builds, and
 * to mark that turn complete.
 *
 * @returns {string} Unique turn id of the form `asst-<ms>-<rand>`.
 */
function makeTurnId()
{
  const rand = Math.random().toString(36).slice(2, 8);
  return `asst-${Date.now()}-${rand}`;
}

/**
 * Set at the start of each `handleInput` call so the `canUseTool`
 * callback (which fires synchronously from inside the SDK during
 * `query()` execution) can stamp every emitted request envelope with
 * the same `turnId` as the surrounding text/tool-use blocks. A single
 * mutable slot is safe because `handleInput` calls are serialized
 * through the {@link inputQueue} Promise chain in the stdin loop —
 * only one round-trip runs at a time.
 *
 * @type {string | null}
 */
let currentTurnId = null;

/**
 * Live reference to the in-flight `query()` AsyncGenerator. Exposed
 * at module scope so the `cancel-current-turn` stdin branch (B.02)
 * can call `.return()` on it, breaking out of the `for await` loop
 * inside `handleInput` and ending the turn early.
 *
 * Set in {@link handleInput} immediately after `query()` returns;
 * cleared in the `finally` block of the same function so a stale
 * generator can't be cancelled after the turn has already finished.
 *
 * @type {AsyncGenerator | null}
 */
let currentQuery = null;

/**
 * Promise chain used to serialize successive `handleInput` calls
 * without blocking the stdin `for await` loop. Each new `input`
 * message is appended to the chain via `.then()`; control messages
 * (`respond-to-request`, `setPermissionMode`, etc.) bypass the chain
 * and are processed immediately.
 *
 * Why this matters: `handleInput` blocks until `query()` finishes,
 * which itself blocks while `canUseToolCallback` awaits the Promise
 * stored in {@link pending}. That Promise is only resolved when a
 * `respond-to-request` line arrives on stdin — which the loop must
 * still be free to read. `await handleInput(…)` directly inside the
 * loop deadlocks the whole supervisor.
 *
 * @type {Promise<void>}
 */
let inputQueue = Promise.resolve();

/**
 * Builds the `mcpServers` config passed to `query()` when the host
 * has resolved a built `mcp-proxy.js`. Returns `undefined` when
 * `MCP_PROXY_PATH` is unset — `spawn.rs` already surfaced the soft
 * warn to React, so the SDK simply runs without MCP tools.
 *
 * @returns {Record<string, object> | undefined} The `mcpServers`
 *   config for `query()`'s options, or `undefined` to omit it.
 */
function buildMcpServers()
{
  const proxyPath = process.env.MCP_PROXY_PATH;

  if (!proxyPath || proxyPath.length === 0)
  {
    return undefined;
  }

  return {
    "game-deck": {
      command: "node",
      args: [proxyPath],
      env: {
        UNITY_MCP_HOST: process.env.UNITY_MCP_HOST ?? "",
        UNITY_MCP_PORT: process.env.UNITY_MCP_PORT ?? "",
      },
    },
  };
}

/**
 * Builds the `plugins` array passed to `query()` from the env-var
 * contract `spawn.rs` sets up:
 *
 * - `MCP_GAME_DECK_PLUGIN_DIR` — `<package>/Plugin~/`, the bundled
 *   Claude Code plugin (manifest at `.claude-plugin/plugin.json`,
 *   skills under `skills/<name>/SKILL.md`, agents under
 *   `agents/<name>.md`). Set by Rust when present.
 *
 * Loaded via the SDK's first-class `plugins` option — the plugin
 * mechanism is what auto-discovers skills AND agents (both share
 * the `mcp-game-deck:` namespace from the manifest's `name`).
 *
 * Skills appear as `mcp-game-deck:<skill-name>` in the chat;
 * agents are invoked via `@agent-mcp-game-deck:<agent-name>`.
 *
 * IMPORTANT: registering the plugin via `plugins` does NOT auto-grant
 * filesystem read access for paths inside the plugin directory. When
 * agents/skills reference content via `${CLAUDE_PLUGIN_ROOT}/...` (e.g.
 * the knowledge base under `Plugin~/knowledge/`), the SDK substitutes
 * the path correctly but the working-directory restriction still
 * applies. The same env var is therefore included in
 * {@link buildAdditionalDirectories} to grant read access for those
 * `Read` calls.
 *
 * @returns {Array<object> | undefined} The `plugins` config for
 *   `query()`'s options, or `undefined` to omit it when the env var is
 *   not set (package install is corrupt / dev hot-path skipping it).
 */
function buildPlugins()
{
  const pluginDir = process.env.MCP_GAME_DECK_PLUGIN_DIR;

  if (!pluginDir || pluginDir.length === 0)
  {
    return undefined;
  }

  return [{ type: "local", path: pluginDir }];
}

/**
 * Builds the `additionalDirectories` array passed to `query()` from
 * the env-var contract `spawn.rs` sets up.
 *
 * Two purposes today:
 *
 * - `MCP_GAME_DECK_PLUGIN_DIR` — `<package>/Plugin~/`. Granted read
 *   access so agents/skills can `Read` files referenced via
 *   `${CLAUDE_PLUGIN_ROOT}/...` (notably the knowledge base under
 *   `Plugin~/knowledge/`). Discovery is handled separately via the
 *   `plugins` option (see {@link buildPlugins}); this entry only
 *   widens the working-directory allowlist.
 * - `MCP_GAME_DECK_COMMANDS_DIR` — `<unity-project>/ProjectSettings/
 *   GameDeck/commands/` (opt-in user-authored commands). Set by Rust
 *   only when the directory exists.
 *
 * Both env vars set by Rust only when the corresponding paths exist.
 *
 * @returns {string[]} Absolute paths in load order. Empty when neither
 *   env var is set.
 */
function buildAdditionalDirectories()
{
  return [
    process.env.MCP_GAME_DECK_PLUGIN_DIR,
    process.env.MCP_GAME_DECK_COMMANDS_DIR,
  ].filter((p) => typeof p === "string" && p.length > 0);
}

/**
 * Runs one `query()` round-trip for a user input string with
 * `includePartialMessages: true`. The SDK emits one `stream_event`
 * per content delta; we discriminate by event type:
 *
 * - `content_block_start` with `text` block → no-op (next delta brings
 *   the text)
 * - `content_block_start` with `tool_use` block → register an
 *   accumulator slot keyed by content-block index (carries the SDK's
 *   `tool_use_id` and `name`); if the start already includes a
 *   non-empty `input` field, emit immediately
 * - `content_block_delta` with `text_delta` → forward to the host
 *   as a `text-delta` envelope
 * - `content_block_delta` with `input_json_delta` → append the
 *   `partial_json` chunk to the matching accumulator
 * - `content_block_stop` → if the block was a pending tool_use,
 *   `JSON.parse` the accumulator and emit a `tool-use` envelope
 *
 * Tool results arrive as `user` messages whose `content` array carries
 * `tool_result` blocks; we extract and forward each as a `tool-result`
 * envelope. The turn is closed with `assistant-turn-complete` when a
 * `result` SDK message arrives.
 *
 * Long pauses between deltas are expected (see Anthropic SDK issue
 * #44). Multi-block turns accumulate into the same turn — every
 * envelope carries the same `turnId`, so the host appends in order.
 *
 * @param {string} text - The user-authored prompt to forward to the SDK.
 * @param {string[]} attachments - Absolute paths the user attached.
 *   When non-empty, the prompt is upgraded from a string to an
 *   AsyncIterable yielding one `SDKUserMessage` whose content
 *   includes the text plus one image / document block per file.
 *   {@link buildAttachmentBlock} reads the file and base64-encodes
 *   it inline; the SDK has no file-path source.
 * @returns {Promise<void>} Resolves once the round-trip has finished
 *   and all envelopes have been emitted.
 */
async function handleInput(text, attachments)
{
  const turnId = makeTurnId();
  currentTurnId = turnId;
  const activeBlocks = new Map();
  try
  {
    const queryOptions = {
      cwd: projectPath,
      includePartialMessages: true,
      permissionMode: resolveSdkMode(currentPermissionMode),
      mcpServers: buildMcpServers(),
      plugins: buildPlugins(),
      additionalDirectories: buildAdditionalDirectories(),
      canUseTool: canUseToolCallback,
    };

    const rulesBundleContent = resolveRulesBundleContent();
    
    if (rulesBundleContent !== null)
    {
      queryOptions.systemPrompt = {
        type: "preset",
        preset: "claude_code",
        append: rulesBundleContent,
      };
    }
    debug(
      "rules bundle:",
      rulesBundleContent !== null ? `${rulesBundleContent.length} chars` : "(none)",
    );

    if (pendingResumeSessionId !== null)
    {
      queryOptions.resume = pendingResumeSessionId;
    }

    let prompt;

    if (attachments.length === 0)
    {
      prompt = text;
    }
    else
    {
      const blocks = [{ type: "text", text }];

      for (const filePath of attachments)
      {
        const block = await buildAttachmentBlock(filePath);
        
        if (block !== null)
        {
          blocks.push(block);
        }
      }
      prompt = (async function* singleUserMessage()
      {
        yield {
          type: "user",
          message: { role: "user", content: blocks },
          parent_tool_use_id: null,
        };
      })();
    }

    const q = query({
      prompt,
      options: queryOptions,
    });
    currentQuery = q;

    for await (const msg of q)
    {
      // system/init from a user-turn query carries session_id (for
      // resume continuity) AND the commands/agents catalog. result
      // re-asserts session_id at end-of-turn as defense-in-depth;
      // no catalog there. stream_event / user are content dispatch.
      if (msg?.type === "system" && msg?.subtype === "init")
      {
        handleSystemInitForUserTurn(msg);
      }
      else if (msg?.type === "stream_event")
      {
        handleStreamEvent(msg.event, turnId, activeBlocks);
      }
      else if (msg?.type === "user" && Array.isArray(msg.message?.content))
      {
        for (const block of msg.message.content)
        {
          if (block?.type === "tool_result" && typeof block.tool_use_id === "string")
          {
            emitToolResult(
              turnId,
              block.tool_use_id,
              block.content,
              block.is_error === true,
            );
          }
        }
      }
      else if (msg?.type === "result")
      {
        captureSessionId(msg);
        emitTurnComplete(turnId);
      }
    }
  }
  catch (err)
  {
    debug("query error:", err);
    emitError(err instanceof Error ? err.message : String(err));
  }
  finally
  {
    currentQuery = null;
  }
}

/**
 * Dispatches one `stream_event` payload from the SDK. Pulled out of
 * `handleInput` to keep the `for await` loop readable.
 *
 * @param {object} ev - The stream-event body (`msg.event`).
 * @param {string} turnId - Current turn id.
 * @param {Map<number, object>} activeBlocks - Per-turn block state.
 * @returns {void}
 */
function handleStreamEvent(ev, turnId, activeBlocks)
{
  if (ev?.type === "content_block_start")
  {
    const idx = ev.index;
    const block = ev.content_block;

    if (block?.type === "tool_use")
    {
      const toolUseId = block.id;
      const name = block.name;
      const initial = block.input;
      const hasInitialInput =
        initial !== null &&
        typeof initial === "object" &&
        Object.keys(initial).length > 0;

      if (hasInitialInput)
      {
        emitToolUse(turnId, toolUseId, name, initial);
        activeBlocks.set(idx, { kind: "tool_use_emitted" });
      }
      else
      {
        activeBlocks.set(idx, {
          kind: "tool_use_pending",
          toolUseId,
          name,
          inputBuffer: "",
        });
      }
    }
    else if (block?.type === "text")
    {
      activeBlocks.set(idx, { kind: "text" });
    }
    return;
  }

  if (ev?.type === "content_block_delta")
  {
    const idx = ev.index;
    const delta = ev.delta;

    if (delta?.type === "text_delta")
    {
      const text = delta.text;

      if (typeof text === "string" && text.length > 0)
      {
        emitTextDelta(turnId, text);
      }
      return;
    }

    if (delta?.type === "input_json_delta")
    {
      const block = activeBlocks.get(idx);

      if (block?.kind === "tool_use_pending" && typeof delta.partial_json === "string")
      {
        block.inputBuffer += delta.partial_json;
      }
      return;
    }

    return;
  }

  if (ev?.type === "content_block_stop")
  {
    const idx = ev.index;
    const block = activeBlocks.get(idx);
    
    if (block?.kind === "tool_use_pending")
    {
      let input;
      try
      {
        input = block.inputBuffer.length > 0 ? JSON.parse(block.inputBuffer) : {};
      }
      catch (e)
      {
        debug("tool input JSON parse failed:", String(e), block.inputBuffer);
        input = {
          _parseError: e instanceof Error ? e.message : String(e),
          _raw: block.inputBuffer,
        };
      }
      emitToolUse(turnId, block.toolUseId, block.name, input);
    }
    activeBlocks.delete(idx);
  }
}

// endregion

// region: stdin loop

emit({ type: "ready" });
debug("ready, projectPath=", projectPath);

const rl = readline.createInterface({ input: process.stdin, terminal: false });

for await (const line of rl)
{
  let parsed;
  try 
  {
    parsed = JSON.parse(line);
  } 
  catch (e)
  {
    debug("bad input line:", line);
    continue;
  }

  if (parsed?.type === "input" && typeof parsed.text === "string")
  {
    const attachments = Array.isArray(parsed.attachments) ? parsed.attachments.filter((p) => typeof p === "string") : [];
    inputQueue = inputQueue
      .then(() => handleInput(parsed.text, attachments))
      .catch((err) => {
        debug("input handler error:", err);
        emitError(err instanceof Error ? err.message : String(err));
      });
  }
  else if (parsed?.type === "setPermissionMode" && typeof parsed.mode === "string")
  {
    if (VALID_PERMISSION_MODES.has(parsed.mode))
    {
      currentPermissionMode = parsed.mode;
      debug("permission mode set:", parsed.mode);
      emitPermissionModeChanged(parsed.mode);
    }
    else
    {
      debug("ignored unknown permission mode:", parsed.mode);
    }
  }
  else if (parsed?.type === "setResumeSession" && typeof parsed.sessionId === "string")
  {
    pendingResumeSessionId = parsed.sessionId;
    debug("resume session set:", parsed.sessionId);
  }
  else if (parsed?.type === "clearResumeSession")
  {
    pendingResumeSessionId = null;
    debug("resume session cleared");
  }
  else if (parsed?.type === "healthCheck")
  {
    void runHealthCheck();
  }
  else if (parsed?.type === "cancel-current-turn")
  {
    if (currentQuery !== null && currentTurnId !== null)
    {
      const cancellingTurnId = currentTurnId;
      debug("[cancel] interrupting turn", cancellingTurnId);
      const target = currentQuery;
      currentQuery = null;
      void target.return(undefined).catch((err) => {
        debug("[cancel] generator.return rejected:", err);
      });
      emitTurnComplete(cancellingTurnId);
    }
    else
    {
      debug("[cancel] no active turn — ignored");
    }
  }
  else if (parsed?.type === "respond-to-request" && typeof parsed.requestId === "string")
  {
    const entry = pending.get(parsed.requestId);

    if (entry === undefined)
    {
      debug(`[canUseTool] received response for unknown requestId ${parsed.requestId} — ignoring`);
      continue;
    }

    const decision = parsed.decision;
    const actualKind = decision?.kind;

    if (actualKind !== entry.requestType)
    {
      pending.delete(parsed.requestId);
      debug(`[canUseTool] decision kind mismatch for ${parsed.requestId}: expected ${entry.requestType}, got ${actualKind}`);
      entry.reject(new Error(`Decision kind mismatch: expected ${entry.requestType}, got ${actualKind}`));
      continue;
    }

    pending.delete(parsed.requestId);

    if (entry.requestType === "question")
    {
      entry.resolve(decision.answer);
    }
    else
    {
      if (decision.outcome === "allow-always")
      {
        allowAlwaysCache.add(cacheKey(entry.toolName, entry.input));
      }
      
      entry.resolve({ outcome: decision.outcome });
    }
  }
}

// endregion