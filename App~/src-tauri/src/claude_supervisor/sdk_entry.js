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
 * Runs one `query()` round-trip for a user input string with
 * `includePartialMessages: true`. The SDK emits one `stream_event`
 * per content delta; we forward each `content_block_delta` /
 * `text_delta` payload as a `text-delta` envelope. Long pauses
 * between deltas are expected and intentional — the loop waits
 * indefinitely until a `result` message arrives or the SDK stream
 * closes (see Anthropic SDK GitHub issue #44). The turn is closed
 * with `assistant-turn-complete` when `result` arrives.
 *
 * Tool-use deltas (content_block_start with type `tool_use`) and
 * other non-text events are intentionally ignored here — task 2.4
 * wires those.
 *
 * Multi-block turns (Claude splitting the response into separate
 * `content_block`s) accumulate into the same turn — every delta
 * carries the same `turnId`, so the host appends them in order
 * without resetting between blocks.
 *
 * @param {string} text - The user-authored prompt to forward to the SDK.
 * @returns {Promise<void>} Resolves once the round-trip has finished
 *   and all envelopes have been emitted.
 */
async function handleInput(text)
{
  const turnId = makeTurnId();
  try
  {
    const q = query({
      prompt: text,
      options: {
        cwd: projectPath,
        includePartialMessages: true,
      },
    });

    for await (const msg of q)
    {
      if (msg?.type === "stream_event")
      {
        const ev = msg.event;
        if (ev?.type === "content_block_delta" && ev.delta?.type === "text_delta"
        )
        {
          const delta = ev.delta.text;
          if (typeof delta === "string" && delta.length > 0)
          {
            emitTextDelta(turnId, delta);
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