/**
 * Session management — tracks sessions with file-based persistence.
 * Sessions are stored in a JSON file so they survive server restarts.
 * User config (preferred agent, model) is also persisted here.
 */

import { readFile, writeFile, mkdir } from "node:fs/promises";
import { join, dirname } from "node:path";
import { SESSIONS_FILE, MAX_SESSIONS, MAX_MESSAGES_PER_SESSION, SESSION_EXPIRATION_MS } from "./constants.js";

const SESSIONS = new Map<string, SessionInfo>();

let userConfig: UserConfig = {};
let dataFilePath = "";

// ─── Types ───

/** A single message in a conversation (user or assistant). */
export interface ChatMessage 
{
  role: "user" | "assistant";
  content: string;
}

/** Metadata for a single chat session. */
export interface SessionInfo 
{
  id: string;
  createdAt: number;
  lastActiveAt: number;
  agent?: string;
  turns: number;
  totalCostUsd: number;
  lastPrompt?: string;
  messages?: ChatMessage[];
}

/** Persisted user preferences. */
export interface UserConfig 
{
  preferredAgent?: string;
  preferredModel?: string;
  lastSessionId?: string;
}

/** Shape of the JSON file written to disk by {@link persist} and read by {@link initSessions}. */
interface PersistedData 
{
  sessions: SessionInfo[];
  userConfig: UserConfig;
}

// ─── Initialization ───

/**
 * Initializes session storage, loading persisted data from disk.
 * @param projectDir Absolute path to the Unity project root.
 */
export async function initSessions(projectDir: string): Promise<void> 
{
  dataFilePath = join(projectDir, SESSIONS_FILE);

  try 
  {
    const raw = await readFile(dataFilePath, "utf-8");
    const data: PersistedData = JSON.parse(raw);

    if (Array.isArray(data.sessions)) 
    {
      for (const s of data.sessions) 
      {
        SESSIONS.set(s.id, s);
      }
    }
    if (data.userConfig) 
    {
      userConfig = data.userConfig;
    }

    console.log(`[sessions] Loaded ${SESSIONS.size} sessions from disk`);
    pruneExpiredSessions();
  } 
  catch 
  {
    console.log("[sessions] No persisted sessions found, starting fresh");
  }
}

/**
 * Persists current state to disk. Called after each mutation.
 */
async function persist(): Promise<void> 
{
  if (!dataFilePath)
  {
    return;
  }

  const data: PersistedData = {
    sessions: Array.from(SESSIONS.values())
      .sort((a, b) => b.lastActiveAt - a.lastActiveAt)
      .slice(0, MAX_SESSIONS),
    userConfig,
  };
  try 
  {
    await mkdir(dirname(dataFilePath), { recursive: true });
    await writeFile(dataFilePath, JSON.stringify(data, null, 2), "utf-8");
  } 
  catch (err) 
  {
    console.error("[sessions] Failed to persist:", err);
  }
}

// ─── Expiration ───

/**
 * Removes sessions that haven't been active within {@link SESSION_EXPIRATION_MS}.
 * Called on startup and when listing sessions.
 */
function pruneExpiredSessions(): void
{
  const cutoff = Date.now() - SESSION_EXPIRATION_MS;
  let pruned = 0;

  for (const [id, session] of SESSIONS)
  {
    if (session.lastActiveAt < cutoff)
    {
      SESSIONS.delete(id);
      pruned++;
    }
  }

  if (pruned > 0)
  {
    console.log(`[sessions] Pruned ${pruned} expired session(s)`);
    persist();
  }
}

// ─── Session CRUD ───

/**
 * Creates or updates a session record and persists to disk.
 * @param id Session identifier from the Agent SDK.
 * @param agent Optional agent name used in this session.
 * @param costUsd API cost for the latest turn.
 * @param turns Number of turns completed in the latest interaction.
 * @param lastPrompt Text of the most recent user prompt.
 * @returns The created or updated {@link SessionInfo}.
 */
export function upsertSession(id: string, agent?: string, costUsd: number = 0, turns: number = 0, lastPrompt?: string): SessionInfo 
{
  const existing = SESSIONS.get(id);

  if (existing) 
  {
    existing.lastActiveAt = Date.now();
    existing.turns += turns;
    existing.totalCostUsd += costUsd;

    if (lastPrompt)
    {
      existing.lastPrompt = lastPrompt;
    }

    persist();
    return existing;
  }

  const session: SessionInfo = {
    id,
    createdAt: Date.now(),
    lastActiveAt: Date.now(),
    agent,
    turns,
    totalCostUsd: costUsd,
    lastPrompt,
  };

  SESSIONS.set(id, session);
  userConfig.lastSessionId = id;
  persist();

  return session;
}

/**
 * Lists all known sessions sorted by most recently active first.
 * @returns Array of {@link SessionInfo} ordered by lastActiveAt descending.
 */
export function listSessions(): SessionInfo[]
{
  pruneExpiredSessions();
  return Array.from(SESSIONS.values()).sort((a, b) => b.lastActiveAt - a.lastActiveAt);
}

/**
 * Gets a session by ID.
 * @param id The session identifier to look up.
 * @returns The matching {@link SessionInfo}, or undefined if not found.
 */
export function getSession(id: string): SessionInfo | undefined 
{
  return SESSIONS.get(id);
}

/**
 * Appends messages to a session's history and persists.
 * Keeps at most 50 messages per session to limit storage.
 * @param id Session identifier.
 * @param newMessages Messages to append.
 */
export function appendMessages(id: string, newMessages: ChatMessage[]): void 
{
  const session = SESSIONS.get(id);

  if (!session)
  {
    return;
  }

  if (!session.messages)
  {
    session.messages = [];
  }

  for (const msg of newMessages)
  {
    session.messages.push(msg);
  }

  if (session.messages.length > MAX_MESSAGES_PER_SESSION)
  {
    session.messages = session.messages.slice(session.messages.length - MAX_MESSAGES_PER_SESSION);
  }

  persist();
}

/**
 * Deletes a session by ID and persists to disk.
 * @param id The session identifier to delete.
 * @returns True if the session was found and deleted, false otherwise.
 */
export function deleteSession(id: string): boolean 
{
  const deleted = SESSIONS.delete(id);

  if (deleted)
  {
    persist();
  }

  return deleted;
}

