#!/usr/bin/env node
/**
 * Builds Server~/ (TypeScript -> dist/) and stages the compiled
 * mcp-proxy.js into App~/src-tauri/proxy-bundle/ where Tauri's
 * bundle.resources picks it up at build time and `BaseDirectory::Resource`
 * resolves it at runtime.
 *
 * Invoked indirectly via `pnpm build:proxy` (manually) and via the
 * `beforeDevCommand` / `beforeBuildCommand` hooks in tauri.conf.json
 * (automatically, every dev start and every release build).
 *
 * Pure Node, no extra deps. Works on Windows/macOS/Linux because all
 * shell-out commands run via execSync with cwd set explicitly.
 */

import { execSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const appRoot = resolve(__dirname, "..");
const serverRoot = resolve(appRoot, "../Server~");
const proxySrc = resolve(serverRoot, "dist/mcp-proxy.js");
const proxyDstDir = resolve(appRoot, "src-tauri/proxy-bundle");
const proxyDst = resolve(proxyDstDir, "mcp-proxy.js");

/**
 * Runs a shell command synchronously, streaming stdio through to the parent
 * process. Logs the command and working directory before invocation so the
 * build log shows exactly what's about to execute.
 *
 * @param {string} cmd - The shell command to execute.
 * @param {string} cwd - Working directory to run the command in.
 * @returns {void}
 * @throws Re-throws whatever `execSync` raises when the command exits with a
 *   non-zero status, halting the build.
 */
function run(cmd, cwd)
{
  console.log(`[build-proxy] $ ${cmd}    (cwd: ${cwd})`);
  execSync(cmd, { cwd, stdio: "inherit" });
}

console.log(`[build-proxy] App~ root:    ${appRoot}`);
console.log(`[build-proxy] Server~ root: ${serverRoot}`);
console.log(`[build-proxy] Output:       ${proxyDst}`);

run("npm install", serverRoot);
run("npm run build", serverRoot);
run("npm run bundle:proxy", serverRoot);

if (!existsSync(proxySrc))
{
  console.error(`[build-proxy] ERROR: expected output missing at ${proxySrc}`);
  process.exit(1);
}

mkdirSync(proxyDstDir, { recursive: true });
copyFileSync(proxySrc, proxyDst);
console.log(`[build-proxy] OK ${proxyDst}`);