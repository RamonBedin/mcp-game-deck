// STUB — replaced by Feature 02 orchestrator.
//
// Bridges stdio with a tiny JSON-RPC 2.0 dialect. Used by the Group 3 / 5
// plumbing tasks in App~/src-tauri/src/node_supervisor:
// - 3.1: prove the spawn pipe works
// - 3.2: exercise request/response correlation + notifications
// - 3.3: support restart / crash detection
// - 5.2: closes the Feature 01 chat round-trip via a literal echo
//
// Supported requests:
//   ping              →  { pong: true }
//   echo              →  whatever was sent in `params`
//   conversation/send →  { message_id }, plus a `message/received`
//                        notification with the assistant echo
//
// Periodic notifications:
//   log   every 5s heartbeat (proves the notification path)
//
// ESM module — Server~/package.json declares `"type": "module"`.

import readline from "node:readline";

const sendNotification = (method, params) => {
  process.stdout.write(
    JSON.stringify({ jsonrpc: "2.0", method, params }) + "\n",
  );
};

const sendResponse = (id, { result, error } = {}) => {
  const msg = { jsonrpc: "2.0", id };
  if (error !== undefined) msg.error = error;
  else msg.result = result ?? null;
  process.stdout.write(JSON.stringify(msg) + "\n");
};

const log = (text, level = "info") => sendNotification("log", { level, text });

log("[stub] agent-sdk-stub.js started");

// Heartbeat — proves the notification path independently of any request flow.
const heartbeat = setInterval(() => {
  log(`[stub] heartbeat ts=${Date.now()}`);
}, 5000);
heartbeat.unref();

// STUB — Feature 02 orchestrator replaces this with real Claude Agent SDK
// driven multi-turn conversations. The literal `echo:` prefix is the
// signature that Feature 01's round-trip is alive end-to-end.
const handleConversationSend = (requestId, params) => {
  const text = (params && typeof params.text === "string") ? params.text : "";

  const assistantId = `asst-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const message = {
    id: assistantId,
    role: "assistant",
    content: `echo: ${text}`,
    timestamp: Date.now(),
  };

  // Emit the assistant message first so the UI renders it on the same tick
  // the send-promise resolves (avoids a flicker where the user sees the
  // promise complete before the reply lands).
  sendNotification("message/received", message);
  sendResponse(requestId, { result: { message_id: assistantId } });
};

const handleRequest = ({ id, method, params }) => {
  switch (method) {
    case "ping":
      sendResponse(id, { result: { pong: true } });
      break;
    case "echo":
      sendResponse(id, { result: params ?? null });
      break;
    case "conversation/send":
      handleConversationSend(id, params);
      break;
    default:
      sendResponse(id, {
        error: { code: -32601, message: `method not found: ${method}` },
      });
  }
};

const rl = readline.createInterface({ input: process.stdin });

rl.on("line", (line) => {
  const trimmed = line.trim();
  if (trimmed.length === 0) return;

  let msg;
  try {
    msg = JSON.parse(trimmed);
  } catch (err) {
    log(`[stub] failed to parse line: ${err.message}`, "error");
    return;
  }

  if (msg.id != null && typeof msg.method === "string") {
    handleRequest(msg);
    return;
  }

  // Notifications from supervisor (none expected today, but log them).
  log(`[stub] received notification: ${msg.method ?? "?"}`);
});

rl.on("close", () => {
  clearInterval(heartbeat);
  process.exit(0);
});

process.on("SIGTERM", () => process.exit(0));
process.on("SIGINT", () => process.exit(0));