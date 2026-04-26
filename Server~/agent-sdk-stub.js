// STUB — replaced by Feature 02 orchestrator.
//
// Reads newline-delimited input from stdin and emits a `log` JSON-RPC 2.0
// notification on stdout for each line received. Exists only to prove the
// pipe between the Tauri Rust supervisor and a Node child process works
// during Group 3 plumbing tasks (3.1 spawn, 3.2 framing, 3.3 restart).
//
// ESM module — Server~/package.json declares `"type": "module"`.

import readline from "node:readline";

const sendNotification = (method, params) => {
  process.stdout.write(
    JSON.stringify({ jsonrpc: "2.0", method, params }) + "\n",
  );
};

const log = (text, level = "info") => sendNotification("log", { level, text });

log("[stub] agent-sdk-stub.js started");

const rl = readline.createInterface({ input: process.stdin });

rl.on("line", (line) => {
  const trimmed = line.trim();
  if (trimmed.length === 0) return;
  log(`[stub] received: ${trimmed}`);
});

rl.on("close", () => {
  process.exit(0);
});

// Graceful shutdown when the supervisor sends SIGTERM (Unix) or
// closes stdin (Windows — readline's "close" handles that).
process.on("SIGTERM", () => process.exit(0));
process.on("SIGINT", () => process.exit(0));