/**
 * `@`-mention autocomplete state machine for the chat input.
 *
 * Mirrors the shape of `useSlashAutocomplete` but for the unified
 * agents + project-files picker. The trigger-detection rules are
 * shared via `findActiveTrigger` (different char, same logic) — see
 * `triggerDetection.ts` for the rationale.
 *
 * **Candidate model.** Agents and files are surfaced as a single
 * tagged-union array so the dropdown can group them visually
 * (section headers) while the keyboard navigation remains a flat
 * cycle:
 *
 *   AtCandidate =
 *     | { kind: "agent"; agent: CatalogAgent }
 *     | { kind: "file";  file:  FileIndexEntry }
 *
 * Filter is substring case-insensitive on each candidate's natural
 * search field (agent name + description; file path). Within each
 * section the candidates are sorted alphabetically; agents always
 * appear before files in the concatenated array per the spec's
 * "Agents alphabetical, Files alphabetical (per section)" ordering.
 * Empty query returns every agent followed by every file — the
 * dropdown component handles overflow via its own scroll cap.
 *
 * **Insertion shape.** `applyAtSelection` writes:
 *   - For an agent: `@agent-<name> ` (with trailing space)
 *   - For a file:   `@<path> `       (with trailing space)
 *
 * Both forms insert the trigger char so the user can keep typing
 * arguments after the insertion without re-triggering the dropdown.
 */

import { useCallback, useEffect, useMemo, useState } from "react";
import type { CatalogAgent, FileIndexEntry } from "../ipc/types";
import { findActiveTrigger } from "./triggerDetection";

// #region Types

/**
 * One entry in the unified `@` candidate list. Tagged so the
 * dropdown can dispatch row rendering and so `applyAtSelection`
 * knows which insertion shape to use.
 */
export type AtCandidate =
  | { kind: "agent"; agent: CatalogAgent }
  | { kind: "file"; file: FileIndexEntry };

/**
 * Snapshot of the pure trigger-detection + filter result. Returned
 * by `computeAtAutocompleteState` and folded into the hook's public
 * surface.
 *
 * `range` is `[r0, r1]` where `r0` is the position of the `@` and
 * `r1` is the cursor; the substring `value.slice(r0, r1)` is what
 * a selected candidate replaces.
 */
export interface AtAutocompleteState
{
  active: boolean;
  query: string;
  candidates: AtCandidate[];
  range: [number, number] | null;
}

/**
 * Result of applying a candidate to the current textarea state.
 * Callers update the textarea to `newValue` and set the cursor at
 * `newCursor` (one position past the inserted trailing space).
 */
export interface AtAutocompleteApplyResult
{
  newValue: string;
  newCursor: number;
}

/**
 * Public surface of `useAtAutocomplete`. Mirrors `AtAutocompleteState`
 * for the read side and adds navigation methods plus `applySelection`
 * for the write side. Symmetric to `UseSlashAutocompleteResult`.
 */
export interface UseAtAutocompleteResult extends AtAutocompleteState
{
  selectedIndex: number;
  next: () => void;
  prev: () => void;
  select: (index?: number) => void;
  cancel: () => void;
  applySelection: () => AtAutocompleteApplyResult | null;
}

// #endregion

// #region Pure helpers

const INACTIVE_STATE: AtAutocompleteState = {
  active: false,
  query: "",
  candidates: [],
  range: null,
};

/**
 * Computes the pure autocomplete state for a given textarea value,
 * cursor position, agent catalog, and file index. Stateless — does
 * not consider user dismissal (Esc) or selection index, both of
 * which live in the React hook.
 *
 * @param value - Current textarea content.
 * @param cursorPosition - Caret position within `value`.
 * @param agents - Agent catalog from `useAgents()`.
 * @param files - Project file index from `useProjectFiles()`.
 * @returns The detection + filter snapshot.
 */
export function computeAtAutocompleteState(value: string, cursorPosition: number, agents: CatalogAgent[], files: FileIndexEntry[],): AtAutocompleteState
{
  const match = findActiveTrigger(value, cursorPosition, "@");

  if (match === null)
  {
    return INACTIVE_STATE;
  }

  const candidates = filterAndSortCandidates(agents, files, match.query);

  return {
    active: true,
    query: match.query,
    candidates,
    range: [match.triggerStart, cursorPosition],
  };
}

/**
 * Builds the new textarea state after the user selects a candidate.
 * Per-kind insertion shapes are described in the module docblock.
 *
 * @param value - Current textarea content.
 * @param range - Trigger range produced by `computeAtAutocompleteState`.
 * @param candidate - The selected candidate.
 * @returns The post-insertion `{ newValue, newCursor }` pair.
 */
export function applyAtSelection(value: string, range: [number, number], candidate: AtCandidate,): AtAutocompleteApplyResult
{
  const [r0, r1] = range;
  const inserted = renderInsertion(candidate);
  const newValue = value.substring(0, r0) + inserted + value.substring(r1);
  const newCursor = r0 + inserted.length;
  return { newValue, newCursor };
}

function renderInsertion(candidate: AtCandidate): string
{
  if (candidate.kind === "agent")
  {
    return `@agent-${candidate.agent.name} `;
  }
  return `@${candidate.file.path} `;
}

/**
 * Filters and sorts agents + files by `query`, returning a single
 * concatenated `AtCandidate[]` with agents first, then files. Each
 * section is alphabetically sorted (agent by `name`, file by
 * `path`). An empty query passes everything through unmatched.
 */
function filterAndSortCandidates(agents: CatalogAgent[], files: FileIndexEntry[], query: string,): AtCandidate[]
{
  const lowerQuery = query.toLowerCase();

  const filteredAgents =
    query.length === 0
      ? [...agents]
      : agents.filter((a) => {
          const nameMatch = a.name.toLowerCase().includes(lowerQuery);
          const descMatch = (a.description ?? "")
            .toLowerCase()
            .includes(lowerQuery);
          return nameMatch || descMatch;
        });

  const filteredFiles =
    query.length === 0
      ? [...files]
      : files.filter((f) => f.path.toLowerCase().includes(lowerQuery));

  filteredAgents.sort((a, b) => a.name.localeCompare(b.name));
  filteredFiles.sort((a, b) => a.path.localeCompare(b.path));

  const out: AtCandidate[] = [];
  for (const agent of filteredAgents)
  {
    out.push({ kind: "agent", agent });
  }
  for (const file of filteredFiles)
  {
    out.push({ kind: "file", file });
  }
  return out;
}

// #endregion

// #region Hook

/**
 * React hook wrapping `computeAtAutocompleteState` with selection
 * state and Esc-dismissal tracking. Consumers drive it with the
 * controlled textarea's `value`, `cursorPosition`, the agent
 * catalog, and the project file index.
 *
 * Dismissal model: `cancel()` records the current trigger's start
 * offset; while that offset still anchors the trigger, `active`
 * stays false even though the pure state would say otherwise. The
 * dismissal clears when the user breaks the trigger and a new `@`
 * re-anchors the state at a different offset. Same semantics as
 * `useSlashAutocomplete`.
 *
 * @param value - Current textarea content.
 * @param cursorPosition - Caret position within `value`.
 * @param agents - Agent catalog from `useAgents()`.
 * @param files - Project file index from `useProjectFiles()`.
 * @returns The hook's public surface (see `UseAtAutocompleteResult`).
 */
export function useAtAutocomplete(value: string, cursorPosition: number, agents: CatalogAgent[], files: FileIndexEntry[],): UseAtAutocompleteResult
{
  const computed = useMemo(
    () => computeAtAutocompleteState(value, cursorPosition, agents, files),
    [value, cursorPosition, agents, files],
  );

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

  const applySelection = useCallback((): AtAutocompleteApplyResult | null => {
    if (!active || computed.range === null || computed.candidates.length === 0)
    {
      return null;
    }

    const safeIndex = selectedIndex < computed.candidates.length ? selectedIndex : 0;
    const candidate = computed.candidates[safeIndex];
    return applyAtSelection(value, computed.range, candidate);
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