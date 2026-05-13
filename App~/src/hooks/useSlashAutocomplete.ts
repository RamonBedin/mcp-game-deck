/**
 * Slash-command autocomplete state machine for the chat input.
 *
 * Detects when the textarea cursor sits inside a `/foo` token at the
 * start of a word, filters the supplied catalog against the typed
 * prefix, and exposes navigation + insertion methods so the
 * surrounding chat input can render a dropdown without owning any of
 * the parsing logic itself.
 *
 * The pure helpers `computeSlashAutocompleteState` and
 * `applySlashSelection` are exported so the unit tests can exercise
 * the cases the React hook would otherwise hide behind state.
 *
 * Trigger detection rules (see `06-plans-crud-spec.md` → "Slash
 * dropdown behavior"):
 *
 * 1. The most recent `/` left of the cursor anchors the trigger.
 * 2. The character immediately before that `/` must be whitespace OR
 *    the slash must be at index 0. Anything else (letters, digits,
 *    `:`, another `/`) blocks — this is what keeps URLs like
 *    `https://foo/bar` and inline tokens like `abc/foo` from popping
 *    the dropdown.
 * 3. The text between the `/` and the cursor cannot contain
 *    whitespace — once the user typed a space the command portion is
 *    settled and we're now into argument territory.
 *
 * Sort order in `filterAndSortCandidates`:
 *   tier 1 — name startsWith query
 *   tier 2 — name includes query but does not start with it
 *   tier 3 — description includes query (name doesn't match at all)
 * Within a tier, `mcp-game-deck:`-prefixed names float above
 * unprefixed ones (mild bias so the package's own skills surface
 * first when the user types something ambiguous).
 */

import { useCallback, useEffect, useMemo, useState } from "react";
import type { CatalogCommand } from "../ipc/types";

// #region Types

/**
 * Snapshot of the pure trigger-detection + filter result. Returned
 * by `computeSlashAutocompleteState` and folded into the hook's
 * public surface.
 *
 * `range` is `[r0, r1]` where `r0` is the position of the `/` and
 * `r1` is the cursor; the substring `value.slice(r0, r1)` is what
 * the dropdown selection replaces.
 */
export interface SlashAutocompleteState
{
  active: boolean;
  query: string;
  candidates: CatalogCommand[];
  range: [number, number] | null;
}

/**
 * Result of applying a candidate to the current textarea state.
 * Callers update the textarea to `newValue` and set the cursor at
 * `newCursor` (one position past the inserted trailing space).
 */
export interface SlashAutocompleteApplyResult
{
  newValue: string;
  newCursor: number;
}

/**
 * Public surface of `useSlashAutocomplete`. Mirrors
 * `SlashAutocompleteState` for the read-side and adds navigation
 * methods plus `applySelection` for the write-side.
 */
export interface UseSlashAutocompleteResult extends SlashAutocompleteState
{
  selectedIndex: number;
  next: () => void;
  prev: () => void;
  select: (index?: number) => void;
  cancel: () => void;
  applySelection: () => SlashAutocompleteApplyResult | null;
}

// #endregion

// #region Pure helpers

const MCP_PREFIX = "mcp-game-deck:";

const INACTIVE_STATE: SlashAutocompleteState = {
  active: false,
  query: "",
  candidates: [],
  range: null,
};

/**
 * Computes the pure autocomplete state for a given textarea value,
 * cursor position, and command catalog. Stateless — does not consider
 * user dismissal (Esc) or selection index, both of which live in the
 * React hook.
 *
 * @param value - Current textarea content.
 * @param cursorPosition - Caret position within `value`.
 * @param commands - Catalog to filter against.
 * @returns The detection + filter snapshot.
 */
export function computeSlashAutocompleteState(value: string, cursorPosition: number, commands: CatalogCommand[],): SlashAutocompleteState
{
  const slashIndex = value.lastIndexOf("/", cursorPosition - 1);

  if (slashIndex < 0)
  {
    return INACTIVE_STATE;
  }

  if (slashIndex > 0)
  {
    const prevChar = value[slashIndex - 1];

    if (!/\s/.test(prevChar))
    {
      return INACTIVE_STATE;
    }
  }

  const query = value.substring(slashIndex + 1, cursorPosition);

  if (/\s/.test(query))
  {
    return INACTIVE_STATE;
  }

  const candidates = filterAndSortCandidates(commands, query);

  return {
    active: true,
    query,
    candidates,
    range: [slashIndex, cursorPosition],
  };
}

/**
 * Builds the new textarea state after the user selects a candidate.
 * Inserts `/<commandName> ` (with trailing space) over the trigger
 * range and places the cursor immediately after the inserted space.
 *
 * @param value - Current textarea content.
 * @param range - Trigger range produced by `computeSlashAutocompleteState`.
 * @param commandName - The selected command's `name` field.
 * @returns The post-insertion `{ newValue, newCursor }` pair.
 */
export function applySlashSelection(value: string, range: [number, number], commandName: string,): SlashAutocompleteApplyResult
{
  const [r0, r1] = range;
  const inserted = `/${commandName} `;
  const newValue = value.substring(0, r0) + inserted + value.substring(r1);
  const newCursor = r0 + inserted.length;
  return { newValue, newCursor };
}

/**
 * Tiered substring filter + stable sort over `commands`. See module
 * docblock for the tier definitions. Empty query short-circuits to
 * "all commands, mcp-game-deck:-prefixed first" so the dropdown
 * presents a usable initial list the instant the user types `/`.
 */
function filterAndSortCandidates(commands: CatalogCommand[], query: string,): CatalogCommand[]
{
  if (query.length === 0)
  {
    return [...commands].sort((a, b) => mcpBoost(a) - mcpBoost(b));
  }

  const lowerQuery = query.toLowerCase();

  interface Scored
  {
    command: CatalogCommand;
    tier: number;
    boost: number;
    index: number;
  }

  const scored: Scored[] = [];

  for (let i = 0; i < commands.length; i++)
  {
    const cmd = commands[i];
    const lowerName = cmd.name.toLowerCase();
    const lowerDesc = (cmd.description ?? "").toLowerCase();

    let tier: number | null = null;

    if (lowerName.startsWith(lowerQuery))
    {
      tier = 1;
    }
    else if (lowerName.includes(lowerQuery))
    {
      tier = 2;
    }
    else if (lowerDesc.includes(lowerQuery))
    {
      tier = 3;
    }

    if (tier !== null)
    {
      scored.push({ command: cmd, tier, boost: mcpBoost(cmd), index: i });
    }
  }

  scored.sort((a, b) => {
    if (a.tier !== b.tier)
    {
      return a.tier - b.tier;
    }

    if (a.boost !== b.boost)
    {
      return a.boost - b.boost;
    }

    return a.index - b.index;
  });

  return scored.map((s) => s.command);
}

function mcpBoost(cmd: CatalogCommand): number
{
  return cmd.name.startsWith(MCP_PREFIX) ? 0 : 1;
}

// #endregion

// #region Hook

/**
 * React hook wrapping `computeSlashAutocompleteState` with selection
 * state and Esc-dismissal tracking. Consumers drive it with the
 * controlled textarea's `value`, `cursorPosition`, and the command
 * catalog from `useCommands()`.
 *
 * Dismissal model: `cancel()` records the current trigger's start
 * offset; while that offset still anchors the trigger, `active`
 * stays false even though the pure state would say otherwise. The
 * dismissal clears when the user breaks the trigger (deletes the
 * `/`, types whitespace into the query, moves the cursor away) and
 * a new `/` re-anchors the state at a different offset — at which
 * point the dropdown is welcome to reopen.
 *
 * @param value - Current textarea content.
 * @param cursorPosition - Caret position within `value`.
 * @param commands - Catalog of slash commands from the supervisor.
 * @returns The hook's public surface (see `UseSlashAutocompleteResult`).
 */
export function useSlashAutocomplete(value: string, cursorPosition: number, commands: CatalogCommand[],): UseSlashAutocompleteResult
{
  const computed = useMemo(() => computeSlashAutocompleteState(value, cursorPosition, commands), [value, cursorPosition, commands],);

  const [selectedIndex, setSelectedIndex] = useState(0);
  const [cancelledTriggerStart, setCancelledTriggerStart] = useState<number | null>(null);

  const rangeStart = computed.range ? computed.range[0] : null;

  useEffect(() => {
    setSelectedIndex(0);
  }, [computed.candidates]);

  useEffect(() => {
    setCancelledTriggerStart(null);
  }, [rangeStart]);

  const active = computed.active && cancelledTriggerStart !== rangeStart;

  const next = useCallback(() => {
    setSelectedIndex((i) => {
      const len = computed.candidates.length;
      if (len === 0)
      {
        return 0;
      }

      return (i + 1) % len;
    });
  }, [computed.candidates.length]);

  const prev = useCallback(() => {
    setSelectedIndex((i) => {
      const len = computed.candidates.length;
      if (len === 0)
      {
        return 0;
      }

      return (i - 1 + len) % len;
    });
  }, [computed.candidates.length]);

  const select = useCallback((index?: number) => {
    if (typeof index === "number")
    {
      setSelectedIndex(index);
    }
  }, []);

  const cancel = useCallback(() => {
    if (rangeStart !== null)
    {
      setCancelledTriggerStart(rangeStart);
    }
  }, [rangeStart]);

  const applySelection = useCallback((): SlashAutocompleteApplyResult | null => {
    if (!active || computed.range === null || computed.candidates.length === 0)
    {
      return null;
    }

    const safeIndex = selectedIndex < computed.candidates.length ? selectedIndex : 0;
    const candidate = computed.candidates[safeIndex];
    return applySlashSelection(value, computed.range, candidate.name);
  }, [active, computed.candidates, computed.range, selectedIndex, value]);

  return {
    active,
    query: computed.query,
    candidates: computed.candidates,
    range: computed.range,
    selectedIndex,
    next,
    prev,
    select,
    cancel,
    applySelection,
  };
}

// #endregion