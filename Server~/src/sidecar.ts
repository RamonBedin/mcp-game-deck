/**
 * MCP Game Deck — Sidecar process
 *
 * Owns the public MCP HTTP port for the WHOLE Unity Editor session. The
 * listening socket therefore never lives inside the Unity Editor's reloading
 * AppDomain — which is what leaked listen sockets across domain reloads (under
 * connection churn Mono intermittently lost the `closesocket` on teardown,
 * orphaning a LISTENING socket on the port and wedging every connection). This
 * sidecar never reloads, so that bug class cannot occur.
 *
 * Topology:
 *   proxy / Rust dot / pin probe / external Claude
 *        │ HTTP/1.1 on host:port  (UNCHANGED public contract)
 *        ▼
 *   [ this sidecar ]  ── public HTTP server (owns the port)
 *        │ length-prefixed frames over a loopback "backend" socket
 *        ▼
 *   Unity Editor (C#)  ── connects OUT to the backend; serves McpRequestHandler
 *
 * The Editor only ever makes an OUTBOUND connection to this sidecar's backend
 * port, so a domain reload just drops that client socket (no listener to leak)
 * and the Editor reconnects. `GET /` health is answered here locally and
 * instantly, so the heartbeat/pin never flap during a reload.
 *
 * @packageDocumentation
 */

import http from "http";
import net from "net";
import { readFileSync, writeFileSync, appendFileSync, mkdirSync, unlinkSync } from "fs";
import path from "path";
import { DEFAULT_HOST, DEFAULT_MCP_PORT, CONTENT_TYPE_JSON, AUTH_TOKEN_FILE } from "./constants.js";

// ─── Limits (mirror the C# transport that used to live in the Editor) ───

const MAX_BODY_BYTES = 16 * 1024 * 1024;
const REQUEST_TIMEOUT_MS = 30000;
const RATE_LIMIT_MAX_REQUESTS = 120;
const RATE_LIMIT_WINDOW_MS = 60000;
const PARENT_POLL_INTERVAL_MS = 3000;
const BACKEND_WAIT_POLL_MS = 100;

// ─── CLI args ───

/**
 * Reads a `--name value` CLI argument, falling back to a default.
 * @param name Flag name without the leading dashes.
 * @param fallback Value to use when the flag is absent.
 * @returns The argument value or the fallback.
 */
function arg(name: string, fallback = ""): string
{
  const i = process.argv.indexOf(`--${name}`);
  return i >= 0 && i + 1 < process.argv.length ? process.argv[i + 1] : fallback;
}

const ALLOWED_HOSTS = new Set(["localhost", "127.0.0.1", "::1"]);

/**
 * Validates the public host is a loopback address, mirroring the C# server's
 * `ResolveBindAddress` (it forced loopback for security). Falls back to
 * localhost for anything else.
 * @param host Requested public host.
 * @returns A safe loopback host.
 */
function validateHost(host: string): string
{
  return ALLOWED_HOSTS.has(host) ? host : DEFAULT_HOST;
}

const PROJECT = arg("project", process.env.UNITY_PROJECT_PATH ?? process.cwd());
const PUBLIC_HOST = validateHost(arg("public-host", DEFAULT_HOST));
const PUBLIC_PORT = parseInt(arg("public-port", String(DEFAULT_MCP_PORT)), 10) || DEFAULT_MCP_PORT;
const INFO_FILE = arg("info-file", path.join(PROJECT, "Library", "GameDeck", "sidecar.json"));
const PARENT_PID = parseInt(arg("parent-pid", "0"), 10) || 0;
/** Address Node binds for the public listener (localhost → explicit IPv4 loopback to match C#). */
const BIND_ADDRESS = PUBLIC_HOST === "localhost" ? "127.0.0.1" : PUBLIC_HOST;

// ─── Logging (sibling of proxy.log / app.log under Library/GameDeck) ───

const LOG_FILE = ((): string | null =>
{
  try
  {
    const dir = path.join(PROJECT, "Library", "GameDeck");
    mkdirSync(dir, { recursive: true });
    return path.join(dir, "sidecar.log");
  }
  catch
  {
    return null;
  }
})();

/**
 * Writes a timestamped line to stderr and, when available, `sidecar.log`.
 * Never throws — logging must not be able to break the sidecar.
 * @param level Severity label.
 * @param msg Message body.
 */
function log(level: string, msg: string): void
{
  const line = `${new Date().toISOString()} [${level}] ${msg}\n`;
  process.stderr.write(line);

  if (LOG_FILE)
  {
    try { appendFileSync(LOG_FILE, line); }
    catch { /* swallow */ }
  }
}

// ─── Auth ───

/**
 * Loads the bearer token written by the C# Editor side
 * (`Library/GameDeck/auth-token`). Empty string when missing.
 * @returns The trimmed token, or "".
 */
function loadToken(): string
{
  try { return readFileSync(path.join(PROJECT, AUTH_TOKEN_FILE), "utf-8").trim(); }
  catch { return ""; }
}

let AUTH_TOKEN = loadToken();

// ─── Backend channel (Editor connects OUT to here) ───
// Length-prefixed frames, both directions: [cid:int32 BE][len:int32 BE][payload UTF-8].
// `cid` is sidecar-assigned so concurrent clients (each numbering JSON-RPC ids
// from 1) can't collide.

let backend: net.Socket | null = null;
let nextCid = 1;
const pending = new Map<number, (payload: string) => void>();

const backendServer = net.createServer((sock) =>
{
  log("info", "Editor backend connected");
  backend = sock;
  sock.setNoDelay(true);

  let buf = Buffer.alloc(0);
  sock.on("data", (chunk: Buffer) =>
  {
    buf = Buffer.concat([buf, chunk]);

    while (buf.length >= 8)
    {
      const cid = buf.readInt32BE(0);
      const len = buf.readInt32BE(4);

      if (len < 0 || len > MAX_BODY_BYTES)
      {
        log("error", `backend frame length ${len} out of range — dropping connection`);
        sock.destroy();
        return;
      }
      if (buf.length < 8 + len)
      {
        break;
      }

      const payload = buf.toString("utf8", 8, 8 + len);
      buf = buf.subarray(8 + len);

      const resolve = pending.get(cid);
      if (resolve)
      {
        pending.delete(cid);
        resolve(payload);
      }
    }
  });

  const drop = (): void =>
  {
    if (backend === sock)
    {
      backend = null;
      log("warn", "Editor backend disconnected");
    }
  };
  sock.on("close", drop);
  sock.on("error", (e) => { log("warn", `backend socket error: ${e.message}`); drop(); });
});

/**
 * Sends a JSON-RPC request frame to the Editor and resolves with its response
 * payload. Rejects if no backend is connected.
 * @param payload Raw JSON-RPC request string.
 * @returns The raw JSON-RPC response string.
 */
function sendToBackend(payload: string): Promise<string>
{
  return new Promise((resolve, reject) =>
  {
    const sock = backend;

    if (!sock)
    {
      reject(new Error("backend-not-connected"));
      return;
    }

    const cid = nextCid++;
    pending.set(cid, resolve);

    const payloadBuf = Buffer.from(payload, "utf8");
    const header = Buffer.alloc(8);
    header.writeInt32BE(cid, 0);
    header.writeInt32BE(payloadBuf.length, 4);

    sock.write(Buffer.concat([header, payloadBuf]), (err) =>
    {
      if (err)
      {
        pending.delete(cid);
        reject(err);
      }
    });
  });
}

/**
 * Waits until the Editor backend is connected or the timeout elapses. Lets a
 * tool call issued mid-domain-reload wait the reload out instead of failing.
 * @param timeoutMs Maximum wait.
 * @returns true if a backend is connected.
 */
async function waitForBackend(timeoutMs: number): Promise<boolean>
{
  const start = Date.now();

  while (!backend)
  {
    if (Date.now() - start > timeoutMs)
    {
      return false;
    }
    await new Promise((r) => setTimeout(r, BACKEND_WAIT_POLL_MS));
  }

  return true;
}

/**
 * Rejects a promise if it does not settle within `ms`.
 * @param p Promise to guard.
 * @param ms Timeout in milliseconds.
 * @returns The original promise's resolution, or a timeout rejection.
 */
function withTimeout<T>(p: Promise<T>, ms: number): Promise<T>
{
  return Promise.race([
    p,
    new Promise<T>((_, rej) => setTimeout(() => rej(new Error("backend timeout")), ms)),
  ]);
}

// ─── Rate limiting (sliding window, mirrors the old C# IsRateLimited) ───

let rlCount = 0;
let rlWindowStart = 0;

/**
 * Sliding-window rate limiter: up to {@link RATE_LIMIT_MAX_REQUESTS} per
 * {@link RATE_LIMIT_WINDOW_MS}.
 * @returns true if the request should be rejected.
 */
function rateLimited(): boolean
{
  const now = Date.now();

  if (now - rlWindowStart > RATE_LIMIT_WINDOW_MS)
  {
    rlWindowStart = now;
    rlCount = 1;
    return false;
  }

  return ++rlCount > RATE_LIMIT_MAX_REQUESTS;
}

// ─── Public HTTP server (unchanged contract for proxy / dot / pin / external) ───

const CORS_HEADERS: Record<string, string> = {
  "Access-Control-Allow-Origin": "http://localhost",
  "Access-Control-Allow-Methods": "POST, GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization",
};

/**
 * Validates the Authorization header against the current token, reloading the
 * token once if it doesn't match (it may have been (re)generated by the Editor).
 * @param header Raw Authorization header value.
 * @returns true when authorized.
 */
function isAuthorized(header: string): boolean
{
  const candidate = header.startsWith("Bearer ") ? header.slice(7) : "";

  if (AUTH_TOKEN && candidate === AUTH_TOKEN)
  {
    return true;
  }

  AUTH_TOKEN = loadToken();
  return !!AUTH_TOKEN && candidate === AUTH_TOKEN;
}

const publicServer = http.createServer((req, res) =>
{
  const method = req.method ?? "";

  if (method === "OPTIONS")
  {
    res.writeHead(204, CORS_HEADERS);
    res.end();
    return;
  }

  if (method === "GET")
  {
    // Health is answered locally, instantly, regardless of backend state — so
    // the Tauri heartbeat and the pin stay green even while the Editor reloads.
    res.writeHead(200, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"status":"ok"}');
    return;
  }

  if (method !== "POST")
  {
    res.writeHead(405, CORS_HEADERS);
    res.end();
    return;
  }

  if (!isAuthorized(req.headers["authorization"] ?? ""))
  {
    res.writeHead(401, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"error":"Unauthorized"}');
    return;
  }

  if (rateLimited())
  {
    res.writeHead(429, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
    res.end('{"error":"Rate limit exceeded"}');
    return;
  }

  const chunks: Buffer[] = [];
  let size = 0;
  let tooLarge = false;

  req.on("data", (c: Buffer) =>
  {
    size += c.length;
    if (size > MAX_BODY_BYTES) { tooLarge = true; }
    else { chunks.push(c); }
  });

  req.on("end", async () =>
  {
    if (tooLarge)
    {
      res.writeHead(413, CORS_HEADERS);
      res.end();
      return;
    }

    const body = Buffer.concat(chunks).toString("utf8");

    if (!body)
    {
      res.writeHead(400, CORS_HEADERS);
      res.end();
      return;
    }

    try
    {
      const connected = await waitForBackend(REQUEST_TIMEOUT_MS);

      if (!connected)
      {
        res.writeHead(503, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
        res.end('{"error":"Unity backend not connected"}');
        return;
      }

      const responsePayload = await withTimeout(sendToBackend(body), REQUEST_TIMEOUT_MS);

      if (!responsePayload)
      {
        res.writeHead(204, CORS_HEADERS);
        res.end();
        return;
      }

      res.writeHead(200, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
      res.end(responsePayload);
    }
    catch (e)
    {
      log("warn", `relay error: ${e instanceof Error ? e.message : String(e)}`);
      res.writeHead(502, { "Content-Type": CONTENT_TYPE_JSON, ...CORS_HEADERS });
      res.end('{"error":"relay failed"}');
    }
  });
});

// ─── Lifecycle ───

/**
 * Writes the discovery file the Editor polls to find/reconnect to this sidecar.
 * @param backendPort The ephemeral loopback port the Editor connects to.
 */
function writeInfo(backendPort: number): void
{
  const info = { pid: process.pid, publicHost: PUBLIC_HOST, publicPort: PUBLIC_PORT, backendPort };

  try
  {
    mkdirSync(path.dirname(INFO_FILE), { recursive: true });
    writeFileSync(INFO_FILE, JSON.stringify(info));
  }
  catch (e)
  {
    log("warn", `failed to write info file ${INFO_FILE}: ${e instanceof Error ? e.message : String(e)}`);
  }
}

/** Removes the discovery file on shutdown so a stale entry never misleads the Editor. */
function cleanup(): void
{
  try { unlinkSync(INFO_FILE); }
  catch { /* already gone */ }
}

publicServer.on("error", (e: NodeJS.ErrnoException) =>
{
  log("error", `failed to bind public ${BIND_ADDRESS}:${PUBLIC_PORT} (${e.code ?? e.message}) — ` +
    "address already in use (another Editor/sidecar?) — exiting");
  cleanup();
  process.exit(1);
});

backendServer.listen(0, "127.0.0.1", () =>
{
  const backendPort = (backendServer.address() as net.AddressInfo).port;

  publicServer.listen(PUBLIC_PORT, BIND_ADDRESS, () =>
  {
    writeInfo(backendPort);
    log("info", `sidecar up — public ${BIND_ADDRESS}:${PUBLIC_PORT}, backend 127.0.0.1:${backendPort}, pid ${process.pid}`);
  });
});


if (PARENT_PID > 0)
{
  const timer = setInterval(() =>
  {
    try
    {
      process.kill(PARENT_PID, 0);
    }
    catch
    {
      log("info", "parent (Unity) process gone — exiting");
      cleanup();
      process.exit(0);
    }
  }, PARENT_POLL_INTERVAL_MS);
  timer.unref();
}

process.on("SIGINT", () => { cleanup(); process.exit(0); });
process.on("SIGTERM", () => { cleanup(); process.exit(0); });
process.on("exit", cleanup);
process.on("uncaughtException", (e) => log("error", `uncaught: ${e instanceof Error ? (e.stack ?? e.message) : String(e)}`));
process.on("unhandledRejection", (r) => log("error", `unhandled rejection: ${r instanceof Error ? (r.stack ?? r.message) : String(r)}`));