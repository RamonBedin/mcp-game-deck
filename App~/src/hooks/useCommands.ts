/**
 * Selector hook that exposes the slash command catalog with an
 * optional filter. Wraps `catalogStore` so consumers don't reach
 * into Zustand directly.
 *
 * Filter runs every render — the catalog is at most a few dozen
 * items and the function is invoked once per render, so a `useMemo`
 * wrapper would buy little and complicate the API (memo on the
 * caller side via `useCallback` is available if it matters).
 */

import type { CatalogCommand } from "../ipc/types";
import { useCatalogStore } from "../stores/catalogStore";

/**
 * Returns the current catalog commands, optionally filtered.
 *
 * @param filter - Predicate applied to each command; when omitted,
 *   returns the full array.
 * @returns The filtered (or full) command array.
 */
export function useCommands(filter?: (c: CatalogCommand) => boolean,): CatalogCommand[]
{
  const commands = useCatalogStore((s) => s.commands);
  return filter ? commands.filter(filter) : commands;
}