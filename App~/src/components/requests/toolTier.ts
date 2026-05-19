/**
 * Helpers for tool risk classification — the "tier" surfaced by
 * permission cards.
 *
 * Tier inference is layered:
 *  1. If the tool catalog (B.03/B.04) supplies a tier directly, use it.
 *  2. Otherwise, classify by name prefix using TOOL_TIER_PATTERNS below.
 *  3. Default to `write` when no rule matches — destructive prefixes
 *     should explicitly opt in.
 *
 * @requires-backend B.04 per-tool risk tier — the prefix table below
 *   is a placeholder until the catalog ships the tier on each tool.
 */

// #region Types


export type PermissionTier = "read" | "write" | "destr";

/**
 * Mapping from a tool-name pattern to the permission tier it requires.
 *
 * Used by the permission classifier to decide which tier applies to an
 * incoming tool call, by matching the tool's name against each rule's
 * pattern in order.
 */
interface TierRule
{
  pattern: RegExp;
  tier: PermissionTier;
}

// #endregion

// #region Inference table

/**
 * Heuristic prefix → tier map. Ordered: first match wins. Until the
 * tool catalog supplies the tier field, this is the source of truth.
 * Extend cautiously — every rule should reflect a verifiable property
 * of the tool family.
 */
const TOOL_TIER_PATTERNS: readonly TierRule[] = [
  { pattern: /^asset-(delete|move|rename)/,         tier: "destr" },
  { pattern: /^scene-delete/,                       tier: "destr" },
  { pattern: /^script-(delete|update)/,             tier: "destr" },
  { pattern: /^prefab-(delete)/,                    tier: "destr" },
  { pattern: /^gameobject-delete/,                  tier: "destr" },
  { pattern: /^build-/,                             tier: "destr" }, 
  { pattern: /-(delete|destroy|clear|reset)$/,      tier: "destr" },
  { pattern: /^(console|profiler|reflect|type)-/,   tier: "read" },
  { pattern: /^.*-(get-?(info|state|hierarchy)|inspect|list|find)$/i, tier: "read" },
  { pattern: /^screenshot-/,                        tier: "read" },
  { pattern: /^unitydocs-/,                         tier: "read" },
];

// #endregion

/**
 * Classify a tool by name, returning the inferred permission tier.
 *
 * @param toolName - The MCP tool name (e.g. `scene-create`, `asset-delete`).
 * @returns The inferred tier; `write` when no rule matches.
 */
export function classifyTool(toolName: string): PermissionTier
{
  for (const rule of TOOL_TIER_PATTERNS)
  {
    if (rule.pattern.test(toolName))
    {
      return rule.tier;
    }
  }

  return "write";
}

// #region Narrative templates

/**
 * Heuristic verb mapper used when the tool catalog doesn't supply a
 * human-friendly narrative for a permission request. Pulls the verb
 * from the tool's action suffix (everything after the first `-`).
 *
 * @param toolName - The MCP tool name.
 * @returns A verb phrase like "create", "modify", "delete", etc.
 */
export function verbFor(toolName: string): string
{
  if (/-(delete|destroy)/.test(toolName))   
  { 
    return "delete"; 
  }

  if (/-(create|add|new)$/.test(toolName))  
  { 
    return "create"; 
  }

  if (/-(update|set|configure|edit)/.test(toolName))
  { 
    return "modify"; 
  }

  if (/-(move|rename)/.test(toolName))       
  { 
    return "rename"; 
  }

  if (/-(get|list|find|inspect|read)/.test(toolName))
  { 
    return "read"; 
  }

  if (/-(build)/.test(toolName))             
  { 
    return "build"; 
  }
  
  return "run";
}

// #endregion