"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));

// src/sidecar.ts
var import_http = __toESM(require("http"), 1);
var import_net = __toESM(require("net"), 1);
var import_fs = require("fs");
var import_path = __toESM(require("path"), 1);

// src/constants.ts
var DEFAULT_MCP_PORT = 8090;
var DEFAULT_HOST = "localhost";
var CONTENT_TYPE_JSON = "application/json";
var AUTH_TOKEN_FILE = "Library/GameDeck/auth-token";

// src/sidecar.ts
var MAX_BODY_BYTES = 16 * 1024 * 1024;
var REQUEST_TIMEOUT_MS = 3e4;
var RATE_LIMIT_MAX_REQUESTS = 120;
var RATE_LIMIT_WINDOW_MS = 6e4;
var PARENT_POLL_INTERVAL_MS = 3e3;
var BACKEND_WAIT_POLL_MS = 100;
function arg(name, fallback = "") {
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}
var ALLOWED_HOSTS = /* @__PURE__ */ new Set(["localhost", "127.0.0.1", "::1"]);
function validateHost(host) {
  return ALLOWED_HOSTS.has(host) ? host : DEFAULT_HOST;
}
var PROJECT = arg("project", process.env.UNITY_PROJECT_PATH ?? process.cwd());
var PUBLIC_HOST = validateHost(arg("public-host", DEFAULT_HOST));
var PUBLIC_PORT = parseInt(arg("public-port", String(DEFAULT_MCP_PORT)), 10) || DEFAULT_MCP_PORT;
var INFO_FILE = arg("info-file", import_path.default.join(PROJECT, "Library", "GameDeck", "sidecar.json"));
var PARENT_PID = parseInt(arg("parent-pid", "0"), 10) || 0;
var BIND_ADDRESS = PUBLIC_HOST === "localhost" ? "127.0.0.1" : PUBLIC_HOST;
var LOG_FILE = (() => {
  try {
    const dir = import_path.default.join(PROJECT, "Library", "GameDeck");
    (0, import_fs.mkdirSync)(dir, { recursive: true });
    return import_path.default.join(dir, "sidecar.log");
  } catch {
    return null;
  }
})();
function log(level, msg) {
  const line = `${(/* @__PURE__ */ new Date()).toISOString()} [${level}] ${msg}
`;
  process.stderr.write(line);
  if (LOG_FILE) {
    try {
      (0, import_fs.appendFileSync)(LOG_FILE, line);
    } catch {
    }
  }
}
function loadToken() {
  try {
    return (0, import_fs.readFileSync)(import_path.default.join(PROJECT, AUTH_TOKEN_FILE), "utf-8").trim();
  } catch {
    return "";
  }
}
var AUTH_TOKEN = loadToken();
var backend = null;
var nextCid = 1;
var pending = /* @__PURE__ */ new Map();
function flushPending(reason) {
  if (pending.size === 0) {
    return;
  }
  log("warn", `flushing ${pending.size} pending request(s): ${reason}`);
  for (const { reject } of pending.values()) {
    reject(new Error(reason));
  }
  pending.clear();
}
var backendServer = import_net.default.createServer((sock) => {
  log("info", "Editor backend connected");
  const previous = backend;
  backend = sock;
  sock.setNoDelay(true);
  if (previous && previous !== sock) {
    log("warn", "replacing previous Editor backend connection");
    flushPending("backend connection replaced");
    try {
      previous.destroy();
    } catch {
    }
  }
  let buf = Buffer.alloc(0);
  sock.on("data", (chunk) => {
    buf = Buffer.concat([buf, chunk]);
    while (buf.length >= 8) {
      const cid = buf.readInt32BE(0);
      const len = buf.readInt32BE(4);
      if (len < 0 || len > MAX_BODY_BYTES) {
        log("error", `backend frame length ${len} out of range \u2014 dropping connection`);
        sock.destroy();
        return;
      }
      if (buf.length < 8 + len) {
        break;
      }
      const payload = buf.toString("utf8", 8, 8 + len);
      buf = buf.subarray(8 + len);
      const entry = pending.get(cid);
      if (entry) {
        pending.delete(cid);
        entry.resolve(payload);
      }
    }
  });
  const drop = () => {
    if (backend === sock) {
      backend = null;
      flushPending("Editor backend disconnected");
      log("warn", "Editor backend disconnected");
    }
  };
  sock.on("close", drop);
  sock.on("error", (e) => {
    log("warn", `backend socket error: ${e.message}`);
    drop();
  });
});
function sendToBackend(payload) {
  return new Promise((resolve, reject) => {
    const sock = backend;
    if (!sock) {
      reject(new Error("backend-not-connected"));
      return;
    }
    const cid = nextCid++;
    pending.set(cid, { resolve, reject });
    const payloadBuf = Buffer.from(payload, "utf8");
    const header = Buffer.alloc(8);
    header.writeInt32BE(cid, 0);
    header.writeInt32BE(payloadBuf.length, 4);
    sock.write(Buffer.concat([header, payloadBuf]), (err) => {
      if (err) {
        pending.delete(cid);
        reject(err);
      }
    });
  });
}
async function waitForBackend(timeoutMs) {
  const start = Date.now();
  while (!backend) {
    if (Date.now() - start > timeoutMs) {
      return false;
    }
    await new Promise((r) => setTimeout(r, BACKEND_WAIT_POLL_MS));
  }
  return true;
}
function withTimeout(p, ms) {
  return Promise.race([
    p,
    new Promise((_, rej) => setTimeout(() => rej(new Error("backend timeout")), ms))
  ]);
}
var rlCount = 0;
var rlWindowStart = 0;
function rateLimited() {
  const now = Date.now();
  if (now - rlWindowStart > RATE_LIMIT_WINDOW_MS) {
    rlWindowStart = now;
    rlCount = 1;
    return false;
  }
  return ++rlCount > RATE_LIMIT_MAX_REQUESTS;
}
var CORS_HEADERS = {
  "Access-Control-Allow-Origin": "http://localhost",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization"
};
function isAuthorized(header) {
  const candidate = header.startsWith("Bearer ") ? header.slice(7) : "";
  if (AUTH_TOKEN && candidate === AUTH_TOKEN) {
    return true;
  }
  AUTH_TOKEN = loadToken();
  return !!AUTH_TOKEN && candidate === AUTH_TOKEN;
}
var publicServer = import_http.default.createServer((req, res) => {
  const method = req.method ?? "";
  if (method === "OPTIONS") {
    res.writeHead(204, CORS_HEADERS);
    res.end();
    return;
  }
  if (method === "GET") {
    res.writeHead(200, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"status":"ok"}');
    return;
  }
  if (method !== "POST") {
    res.writeHead(405, CORS_HEADERS);
    res.end();
    return;
  }
  if (!isAuthorized(req.headers["authorization"] ?? "")) {
    res.writeHead(401, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"error":"Unauthorized"}');
    return;
  }
  if (rateLimited()) {
    res.writeHead(429, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"error":"Rate limit exceeded"}');
    return;
  }
  const chunks = [];
  let size = 0;
  let tooLarge = false;
  req.on("data", (c) => {
    size += c.length;
    if (size > MAX_BODY_BYTES) {
      tooLarge = true;
    } else {
      chunks.push(c);
    }
  });
  req.on("end", async () => {
    if (tooLarge) {
      res.writeHead(413, CORS_HEADERS);
      res.end();
      return;
    }
    const body = Buffer.concat(chunks).toString("utf8");
    if (!body) {
      res.writeHead(400, CORS_HEADERS);
      res.end();
      return;
    }
    try {
      const connected = await waitForBackend(REQUEST_TIMEOUT_MS);
      if (!connected) {
        res.writeHead(503, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
        res.end('{"error":"Unity backend not connected"}');
        return;
      }
      const responsePayload = await withTimeout(sendToBackend(body), REQUEST_TIMEOUT_MS);
      if (!responsePayload) {
        res.writeHead(204, CORS_HEADERS);
        res.end();
        return;
      }
      res.writeHead(200, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
      res.end(responsePayload);
    } catch (e) {
      log("warn", `relay error: ${e instanceof Error ? e.message : String(e)}`);
      res.writeHead(502, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
      res.end('{"error":"relay failed"}');
    }
  });
});
function writeInfo(backendPort) {
  const info = { pid: process.pid, publicHost: PUBLIC_HOST, publicPort: PUBLIC_PORT, backendPort };
  try {
    (0, import_fs.mkdirSync)(import_path.default.dirname(INFO_FILE), { recursive: true });
    (0, import_fs.writeFileSync)(INFO_FILE, JSON.stringify(info));
  } catch (e) {
    log("warn", `failed to write info file ${INFO_FILE}: ${e instanceof Error ? e.message : String(e)}`);
  }
}
function cleanup() {
  try {
    (0, import_fs.unlinkSync)(INFO_FILE);
  } catch {
  }
}
publicServer.on("error", (e) => {
  log("error", `failed to bind public ${BIND_ADDRESS}:${PUBLIC_PORT} (${e.code ?? e.message}) \u2014 address already in use (another Editor/sidecar?) \u2014 exiting`);
  cleanup();
  process.exit(1);
});
backendServer.listen(0, "127.0.0.1", () => {
  const backendPort = backendServer.address().port;
  publicServer.listen(PUBLIC_PORT, BIND_ADDRESS, () => {
    writeInfo(backendPort);
    log("info", `sidecar up \u2014 public ${BIND_ADDRESS}:${PUBLIC_PORT}, backend 127.0.0.1:${backendPort}, pid ${process.pid}`);
  });
});
if (PARENT_PID > 0) {
  const timer = setInterval(() => {
    try {
      process.kill(PARENT_PID, 0);
    } catch {
      log("info", "parent (Unity) process gone \u2014 exiting");
      cleanup();
      process.exit(0);
    }
  }, PARENT_POLL_INTERVAL_MS);
  timer.unref();
}
process.on("SIGINT", () => {
  cleanup();
  process.exit(0);
});
process.on("SIGTERM", () => {
  cleanup();
  process.exit(0);
});
process.on("exit", cleanup);
process.on("uncaughtException", (e) => log("error", `uncaught: ${e instanceof Error ? e.stack ?? e.message : String(e)}`));
process.on("unhandledRejection", (r) => log("error", `unhandled rejection: ${r instanceof Error ? r.stack ?? r.message : String(r)}`));
