/**
 * Selector hook that exposes the agent catalog with an optional
 * filter. Symmetric to `useCommands` — same shape, same semantics,
 * same caveat about filter stability.
 */

import type { CatalogAgent } from "../ipc/types";
import { useCatalogStore } from "../stores/catalogStore";

/**
 * Returns the current catalog agents, optionally filtered.
 *
 * @param filter - Predicate applied to each agent; when omitted,
 *   returns the full array.
 * @returns The filtered (or full) agent array.
 */
export function useAgents(filter?: (a: CatalogAgent) => boolean,): CatalogAgent[]
{
  const agents = useCatalogStore((s) => s.agents);
  return filter ? agents.filter(filter) : agents;
}