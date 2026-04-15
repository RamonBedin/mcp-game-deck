/**
 * System prompt for the Claude Agent SDK query.
 * Loads core-system-prompt.md from the package at runtime and resolves the
 * {{KB_PATH}} placeholder to the absolute knowledge-base path.
 *
 * @packageDocumentation
 */

import { readFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

/** Total number of MCP tools available in the MCP Game Deck package. */
export const TOOL_COUNT = 269;

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));   // Server~/dist
const SERVER_ROOT = dirname(SCRIPT_DIR);                      // Server~
const PACKAGE_ROOT = dirname(SERVER_ROOT);                    // <package root>
const PROMPTS_DIR = join(SERVER_ROOT, "prompts");             // Server~/prompts
const KB_DIR = join(PACKAGE_ROOT, "KnowledgeBase~");          // <package root>/KnowledgeBase~

let _cachedPrompt: string | null = null;

/**
 * Loads and caches the core system prompt, resolving the {{KB_PATH}} placeholder
 * to the absolute path of the package's knowledge-base directory.
 * @returns The fully resolved core system prompt text.
 */
export async function getSystemPrompt(): Promise<string>
{
  if (_cachedPrompt !== null)
  {
    return _cachedPrompt;
  }

  const raw = await readFile(join(PROMPTS_DIR, "core-system-prompt.md"), "utf-8");
  _cachedPrompt = raw.replaceAll("{{KB_PATH}}", KB_DIR);
  return _cachedPrompt;
}

/**
 * @returns Absolute path to the package's KnowledgeBase~ directory.
 */
export function getKnowledgeBasePath(): string
{
  return KB_DIR;
}