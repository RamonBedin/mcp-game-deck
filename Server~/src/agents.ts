/**
 * Agent and command (skill) loader.
 *
 * Reads agent definitions from Agents~/ and skill definitions from Skills~/.
 * The Agent SDK loads these natively via settingSources: ["project"], so we only need
 * to expose metadata via WebSocket for the Unity Chat UI dropdowns (per T-305 note).
 */

import { readdir, readFile } from "node:fs/promises";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import type { AgentInfo, CommandInfo } from "./messages.js";
import { PACKAGE_AGENTS_DIR, PACKAGE_SKILLS_DIR, MARKDOWN_EXT, FALLBACK_DESC_FILES, FRONTMATTER_TOOLS_KEY, MAX_DESCRIPTION_LENGTH } from "./constants.js";
import { getKnowledgeBasePath } from "./system-prompt.js";

// ─── Agent types ───

/** Full agent metadata loaded from an Agents~/ markdown file. */
export interface AgentDefinition 
{
  name: string;
  description: string;
  filePath: string;
  model?: string;
  tools?: string[];
}

// ─── Agent loading ───

/**
 * Loads all agent definitions from the package Agents~/ directory.
 * Parses YAML-like frontmatter for structured metadata.
 * @param _projectDir Absolute path to the Unity project root (unused, kept for API compat).
 * @returns Sorted array of {@link AgentDefinition} entries.
 */
export async function loadAgents(_projectDir: string): Promise<AgentDefinition[]>
{
  const agents: AgentDefinition[] = [];
  const seenNames = new Set<string>();
  const searchDirs: string[] = [];

  try
  {
    const scriptDir = dirname(fileURLToPath(import.meta.url));
    const packageRoot = dirname(dirname(scriptDir));
    searchDirs.push(join(packageRoot, PACKAGE_AGENTS_DIR));
  }
  catch(error)
  {
    console.warn("[AgentLoader] Could not resolve package root for agent discovery:", error);
  }

  const packageDir = process.env.PACKAGE_DIR;

  if (packageDir)
  {
    searchDirs.push(join(packageDir, PACKAGE_AGENTS_DIR));
  }

  for (const agentsDir of searchDirs) 
  {
    try 
    {
      const files = await readdir(agentsDir);

      for (const file of files) 
      {
        if (!file.endsWith(MARKDOWN_EXT))
        {
          continue;
        }

        const name = file.replace(MARKDOWN_EXT, "");

        if (seenNames.has(name))
        {
          continue;
        }

        seenNames.add(name);

        const filePath = join(agentsDir, file);
        const content = await readFile(filePath, "utf-8");
        const meta = parseFrontmatter(content);

        agents.push({
          name,
          description: meta.description ?? `Agent: ${name}`,
          filePath,
          model: meta.model,
          tools: meta.tools,
        });
      }
    } 
    catch(error)
    {
      if ((error as NodeJS.ErrnoException).code !== "ENOENT") 
      {
        console.warn(`[AgentLoader] Unexpected error reading agents from ${agentsDir}:`, error);
      }
    }
  }

  return agents.sort((a, b) => a.name.localeCompare(b.name));
}

// ─── Skill/command loading ───

/** Metadata for a skill loaded from a Skills~/ subdirectory. */
export interface SkillDefinition 
{
  name: string;
  description: string;
  dirPath: string;
}

/**
 * Loads skill definitions from the package Skills~/ directory.
 * Each subdirectory is a skill. Reads the prompt.md or README.md for description.
 * User commands from ProjectSettings/GameDeck/commands/ are loaded first (override built-in).
 * @param projectDir Absolute path to the Unity project root.
 * @returns Sorted array of {@link SkillDefinition} entries.
 */
export async function loadSkills(projectDir: string): Promise<SkillDefinition[]>
{
  const skills: SkillDefinition[] = [];
  const seenNames = new Set<string>();
  const searchDirs: string[] = [];

  // User custom commands take priority (FEAT-06 prep)
  searchDirs.push(join(projectDir, "ProjectSettings", "GameDeck", "commands"));

  try
  {
    const scriptDir = dirname(fileURLToPath(import.meta.url));
    const packageRoot = dirname(dirname(scriptDir));
    searchDirs.push(join(packageRoot, PACKAGE_SKILLS_DIR));
  }
  catch(error)
  {
    console.warn("[SkillLoader] Could not resolve package root for skill discovery:", error);
  }

  const packageDir = process.env.PACKAGE_DIR;

  if (packageDir)
  {
    searchDirs.push(join(packageDir, PACKAGE_SKILLS_DIR));
  }

  for (const skillsDir of searchDirs) 
    {
    try 
    {
      const entries = await readdir(skillsDir, { withFileTypes: true });

      for (const entry of entries) 
      {
        if (!entry.isDirectory())
        {
          continue;
        }

        if (seenNames.has(entry.name))
        {
          continue;
        }

        seenNames.add(entry.name);

        const dirPath = join(skillsDir, entry.name);
        let description = `Skill: ${entry.name}`;

        for (const descFile of FALLBACK_DESC_FILES)
        {
          try 
          {
            const content = await readFile(join(dirPath, descFile), "utf-8");
            const meta = parseFrontmatter(content);

            if (meta.description) 
            {
              description = meta.description;
              break;
            }

            const firstLine = content.split("\n").find((l) => l.trim() && !l.startsWith("#") && !l.startsWith("---"));

            if (firstLine) 
            {
              description = firstLine.trim().slice(0, MAX_DESCRIPTION_LENGTH);
              break;
            }
          } 
          catch(error)
          {
            if ((error as NodeJS.ErrnoException).code !== "ENOENT") 
            {
              console.warn(`[SkillLoader] Unexpected error reading ${descFile} in ${dirPath}:`, error);
            }
          }
        }

        skills.push({ name: entry.name, description, dirPath });
      }
    } 
    catch(error)
    {
      if ((error as NodeJS.ErrnoException).code !== "ENOENT") 
      {
        console.warn(`[SkillLoader] Unexpected error reading skills from ${skillsDir}:`, error);
      }
    }
  }

  return skills.sort((a, b) => a.name.localeCompare(b.name));
}

// ─── Frontmatter parser ───

/** Parsed frontmatter key-value pairs from an agent or skill markdown file. */
interface Frontmatter 
{
  description?: string;
  model?: string;
  tools?: string[];
  [key: string]: unknown;
}

/**
 * Parses simple YAML-like frontmatter from a markdown file.
 * Handles: description, model, tools (as comma-separated or YAML list).
 * @param content Full file content to parse.
 * @returns Parsed {@link Frontmatter} object (empty if no frontmatter found).
 */
function parseFrontmatter(content: string): Frontmatter 
{
  const result: Frontmatter = {};
  const match = content.match(/^---\s*\n([\s\S]*?)\n---/);

  if (!match)
  {
    return result;
  }

  const block = match[1];

  for (const line of block.split("\n")) 
  {
    const kvMatch = line.match(/^(\w[\w-]*):\s*(.+)/);
    
    if (!kvMatch)
    {
      continue;
    }

    const [, key, value] = kvMatch;
    const trimmedValue = value.trim();

    if (key === FRONTMATTER_TOOLS_KEY)
    {
      const cleaned = trimmedValue.replace(/^\[|\]$/g, "");
      result.tools = cleaned.split(",").map((t) => t.trim()).filter(Boolean);
    } 
    else 
    {
      result[key] = trimmedValue.replace(/^["']|["']$/g, "");
    }
  }

  return result;
}

// ─── Agent prompt resolution ───

/**
 * Reads the body (everything after the YAML frontmatter) of an agent's
 * markdown file and returns it as the agent's system prompt.
 * @param agents The loaded agent list.
 * @param name   Agent name to look up.
 * @returns The agent prompt text, or null if not found.
 */
export async function getAgentPrompt(agents: AgentDefinition[], name: string): Promise<string | null>
{
  const agent = agents.find((a) => a.name === name);

  if (!agent)
  {
    return null;
  }

  const content = await readFile(agent.filePath, "utf-8");
  const fmEnd = content.match(/^---\s*\n[\s\S]*?\n---\s*\n/);
  const body = fmEnd ? content.slice(fmEnd[0].length).trim() : content.trim();

  return body.replaceAll("{{KB_PATH}}", getKnowledgeBasePath());
}

// ─── Skill prompt resolution ───

/**
 * Reads the body (everything after the YAML frontmatter) of a skill's
 * SKILL.md file and returns it as the skill's prompt template.
 * @param skills      The loaded skill list.
 * @param commandName Slash-prefixed command identifier (e.g. "/code-review").
 *                    The leading "/" is stripped before lookup.
 * @returns The skill prompt text, or null if the skill or SKILL.md was not found.
 */
export async function getSkillPrompt(skills: SkillDefinition[], commandName: string): Promise<string | null>
{
  const name = commandName.startsWith("/") ? commandName.slice(1) : commandName;
  const skill = skills.find((s) => s.name === name);

  if (!skill)
  {
    return null;
  }
  try
  {
    const skillFile = join(skill.dirPath, "SKILL.md");
    const content = await readFile(skillFile, "utf-8");
    const fmEnd = content.match(/^---\s*\n[\s\S]*?\n---\s*\n/);
    return fmEnd ? content.slice(fmEnd[0].length).trim() : content.trim();
  }
  catch(error)
  {
    if ((error as NodeJS.ErrnoException).code !== "ENOENT")
    {
      console.warn(`[SkillLoader] Unexpected error reading SKILL.md in ${skill.dirPath}:`, error);
    }
    return null;
  }
}

// ─── Wire format converters ───

/**
 * Converts agent definitions to the wire format for list-agents response.
 * @param agents Array of loaded agent definitions.
 * @returns Array of {@link AgentInfo} for WebSocket transmission.
 */
export function agentsToInfo(agents: AgentDefinition[]): AgentInfo[] 
{
  return agents.map((a) => ({ name: a.name, description: a.description }));
}

/**
 * Converts skill definitions to the wire format for list-commands response.
 * @param skills Array of loaded skill definitions.
 * @returns Array of {@link CommandInfo} for WebSocket transmission.
 */
export function skillsToCommands(skills: SkillDefinition[]): CommandInfo[] 
{
  return skills.map((s) => ({ name: `/${s.name}`, description: s.description }));
}