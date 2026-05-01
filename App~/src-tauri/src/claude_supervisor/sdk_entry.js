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
import readline from "node:readline";

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
 * @returns {Promise<void>} Resolves once the round-trip has finished
 *   and all envelopes have been emitted.
 */
async function handleInput(text)
{
  const turnId = makeTurnId();
  const activeBlocks = new Map();
  try
  {
    const q = query({
      prompt: text,
      options: {
        cwd: projectPath,
        includePartialMessages: true,
        mcpServers: buildMcpServers(),
        plugins: buildPlugins(),
        additionalDirectories: buildAdditionalDirectories(),
      },
    });

    for await (const msg of q)
    {
      if (msg?.type === "stream_event")
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
        emitTurnComplete(turnId);
      }
    }
  }
  catch (err)
  {
    debug("query error:", err);
    emitError(err instanceof Error ? err.message : String(err));
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
    await handleInput(parsed.text);
  }
}

// endregion