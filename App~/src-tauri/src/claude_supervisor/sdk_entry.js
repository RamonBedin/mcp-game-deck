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
 * Emits an `assistant-text` envelope carrying a chunk of model-generated text.
 *
 * @param {string} text - The assistant text to forward to the host.
 * @returns {void}
 */
function emitText(text)
{
  emit({ type: "assistant-text", text });
}

/**
 * Emits an `assistant-turn-complete` envelope to signal that the current
 * `query()` round-trip has finished.
 *
 * @returns {void}
 */
function emitTurnComplete()
{
  emit({ type: "assistant-turn-complete" });
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
 * Runs one `query()` round-trip for a user input string. Aggregates assistant
 * text into a single `assistant-text` emission (2.2 monolithic shape) and
 * always concludes with an `assistant-turn-complete` envelope. Task 2.3
 * replaces this with streaming text deltas.
 *
 * Errors raised by the SDK are caught and surfaced as `error` envelopes so the
 * host can recover without the entry process crashing.
 *
 * @param {string} text - The user-authored prompt to forward to the SDK.
 * @returns {Promise<void>} Resolves once the round-trip has completed and all
 *   envelopes have been emitted.
 */
async function handleInput(text)
{
  let buffer = "";
  try
  {
    const q = query({
      prompt: text,
      options: { cwd: projectPath },
    });

    for await (const sdkMessage of q)
    {
      if (sdkMessage?.type === "assistant" && Array.isArray(sdkMessage.message?.content)) 
      {
        const textChunk = sdkMessage.message.content
          .filter(
            (block) =>
              block?.type === "text" && typeof block.text === "string",
          )
          .map((block) => block.text)
          .join("");
        buffer += textChunk;
      }
    }

    if (buffer.length > 0)
    {
      emitText(buffer);
    }
    emitTurnComplete();
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