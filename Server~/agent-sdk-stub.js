// STUB — replaced by Feature 02 orchestrator.
//
// Bridges stdio with a tiny JSON-RPC 2.0 dialect. Used by the Group 3
// plumbing tasks in App~/src-tauri/src/node_supervisor:
// - 3.1: prove the spawn pipe works
// - 3.2 (current): exercise request/response correlation + notifications
// - 3.3: support restart / crash detection
//
// Supported requests:
//   ping  →  { pong: true }
//   echo  →  whatever was sent in `params`
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

const handleRequest = ({ id, method, params }) => {
  switch (method) {
    case "ping":
      sendResponse(id, { result: { pong: true } });
      break;
    case "echo":
      sendResponse(id, { result: params ?? null });
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