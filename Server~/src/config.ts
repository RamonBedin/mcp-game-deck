/**
 * Configuration for the Agent SDK Server.
 * Reads from environment variables with sensible defaults.
 */

import { config as dotenvConfig } from "dotenv";
import { DEFAULT_AGENT_PORT, DEFAULT_MCP_URL, DEFAULT_MODEL, DEFAULT_PERMISSION_MODE } from "./constants.js";

dotenvConfig();

/** 
 * Configuration for the Agent SDK Server.
 * Each field maps to an environment variable resolved in {@link loadConfig}.
 */
export interface ServerConfig 
{
  port: number;
  mcpServerUrl: string;
  model: string;
  cwd: string;
  permissionMode: string;
}

/**
 * Loads server configuration from environment variables with sensible defaults.
 * @returns A fully populated {@link ServerConfig} object.
 */
export function loadConfig(): ServerConfig 
{
  return {
    port: parseInt(process.env.PORT ?? String(DEFAULT_AGENT_PORT), 10),
    mcpServerUrl: process.env.MCP_SERVER_URL ?? DEFAULT_MCP_URL,
    model: process.env.MODEL ?? DEFAULT_MODEL,
    cwd: process.env.PROJECT_CWD ?? process.cwd(),
    permissionMode: process.env.PERMISSION_MODE ?? DEFAULT_PERMISSION_MODE,
  };
}
