/**
 * Portal-rendered dropdown panel for the unified `@` picker
 * (agents + project files).
 *
 * Receives `candidates: AtCandidate[]` from `useAtAutocomplete` and
 * renders the array with section headers emitted at every `kind`
 * transition. The hook is the source of truth for ordering and
 * concatenation; this component does NOT reorder or regroup.
 * `selectedIndex` is a flat index into `candidates` — keyboard
 * navigation cycles seamlessly across the agent/file boundary.
 *
 * Visual conventions match `SlashDropdown` for cohesion: portal to
 * `document.body`, `position: fixed` with consumer-supplied anchor,
 * `onMouseDown + preventDefault` for click-without-blur, capture
 * phase mousedown for click-outside, `scrollIntoView({block:
 * "nearest"})` on selection change.
 *
 * **Why no icons:** consistent with `SlashDropdown` — `lucide-react`
 * is not installed and adding a dep for two icons isn't worth it for
 * v2.0. Section headers (`Agents` / `Files`) carry the cross-kind
 * cue; within the Files section, directories are marked with a
 * trailing `/` on the basename (unix convention).
 */

import { useEffect, useLayoutEffect, useRef } from "react";
import { createPortal } from "react-dom";
import type { AtCandidate } from "../hooks/useAtAutocomplete";

// #region Types

/**
 * Props for {@link AtDropdown}. `anchor` is the viewport-pixel
 * top-left where the panel renders; the consumer (`ChatInput`)
 * computes this from the textarea's bounding box.
 */
export interface AtDropdownProps
{
  candidates: AtCandidate[];
  selectedIndex: number;
  anchor: { top: number; left: number };
  onSelect: (index: number) => void;
  onClose: () => void;
}

// #endregion

// #region Helpers

/**
 * Returns the last `/`-delimited segment of `path`. Falls back to
 * the full path when there's no `/` — covers the top-level case
 * (`README.md` has no parent).
 */
function basenameOf(path: string): string
{
  const idx = path.lastIndexOf("/");
  return idx >= 0 ? path.substring(idx + 1) : path;
}

/**
 * Returns everything before the last `/` in `path`. Empty string at
 * the top level — the row collapses the right column gracefully.
 */
function parentOf(path: string): string
{
  const idx = path.lastIndexOf("/");
  return idx >= 0 ? path.substring(0, idx) : "";
}

/**
 * Section label for a candidate kind. Kept as a tiny function so
 * the JSX doesn't sprout switch statements.
 */
function sectionLabel(kind: AtCandidate["kind"]): string
{
  return kind === "agent" ? "Agents" : "Files";
}

// #endregion

// #region Rows

/**
 * Renders one agent row.
 *
 * @param props - Candidate + selection state + click handler.
 * @returns The row element, with a forwarded ref when selected.
 */
function AgentRow({candidate, active, selectedRef, onMouseDown,}: {candidate: Extract<AtCandidate, { kind: "agent" }>; active: boolean; selectedRef: React.RefObject<HTMLButtonElement> | null; onMouseDown: (e: React.MouseEvent) => void;}) 
{
  return (
    <button
      ref={selectedRef ?? undefined}
      type="button"
      role="option"
      aria-selected={active}
      onMouseDown={onMouseDown}
      className={`flex w-full items-center gap-2 px-2 py-1.5 text-left text-xs transition-colors ${
        active
          ? "bg-sky-900/40 text-sky-100"
          : "text-slate-300 hover:bg-slate-700/40"
      }`}
    >
      <span className="font-mono text-slate-100 shrink-0">
        @agent-{candidate.agent.name}
      </span>
      <span className="flex-1 truncate text-slate-400">
        {candidate.agent.description}
      </span>
    </button>
  );
}

/**
 * Renders one file (or directory) row. Directories get a trailing
 * `/` on the basename so the user doesn't have to read the icon
 * column (there isn't one) to know what they're inserting.
 */
function FileRow({candidate, active, selectedRef, onMouseDown,}: {candidate: Extract<AtCandidate, { kind: "file" }>; active: boolean; selectedRef: React.RefObject<HTMLButtonElement> | null; onMouseDown: (e: React.MouseEvent) => void;}) 
{
  const { path, kind } = candidate.file;
  const base = basenameOf(path);
  const parent = parentOf(path);
  const isDir = kind === "directory";

  return (
    <button
      ref={selectedRef ?? undefined}
      type="button"
      role="option"
      aria-selected={active}
      onMouseDown={onMouseDown}
      className={`flex w-full items-center gap-2 px-2 py-1.5 text-left text-xs transition-colors ${
        active
          ? "bg-sky-900/40 text-sky-100"
          : "text-slate-300 hover:bg-slate-700/40"
      }`}
    >
      <span className="font-mono text-slate-100 shrink-0">
        {base}{isDir ? "/" : ""}
      </span>
      <span className="flex-1 truncate text-right text-slate-500">
        {parent}
      </span>
    </button>
  );
}

// #endregion

// #region Component

/**
 * Renders the unified `@` picker dropdown. Returns `null` when there
 * are no candidates — consumer is expected to gate on the hook's
 * `active` state too, but this is a safety net so an empty
 * `candidates` array doesn't render an empty panel that still steals
 * the click-outside dismiss.
 *
 * @param props - See {@link AtDropdownProps}.
 * @returns The portal element or `null`.
 */
export default function AtDropdown({candidates, selectedIndex, anchor, onSelect, onClose,}: AtDropdownProps)
{
  const panelRef = useRef<HTMLDivElement | null>(null);
  const selectedRowRef = useRef<HTMLButtonElement | null>(null);

  // #region Effects

  // Document-level click-outside in capture phase so we close before
  // any bubbling click reaches the textarea or surrounding form.
  useEffect(() => {
    const handleMouseDown = (event: MouseEvent) => {
      const panel = panelRef.current;

      if (panel === null)
      {
        return;
      }

      if (event.target instanceof Node && panel.contains(event.target))
      {
        return;
      }

      onClose();
    };

    document.addEventListener("mousedown", handleMouseDown, true);
    return () => {
      document.removeEventListener("mousedown", handleMouseDown, true);
    };
  }, [onClose]);

  // Keep the selected row in view when keyboard navigation moves
  // beyond the visible scroll window. `useLayoutEffect` so the
  // scroll commits before the next paint, avoiding a one-frame jump
  // when the user holds ArrowDown across the agent/file boundary.
  useLayoutEffect(() => {
    selectedRowRef.current?.scrollIntoView({ block: "nearest" });
  }, [selectedIndex]);

  // #endregion

  if (candidates.length === 0)
  {
    return null;
  }

  const panel = (
    <div
      ref={panelRef}
      style={{ top: anchor.top, left: anchor.left }}
      className="fixed z-50 min-w-[320px] max-w-[480px] w-max max-h-[280px] overflow-y-auto rounded border border-slate-700 bg-slate-800 shadow-lg"
      role="listbox"
    >
      {candidates.map((c, i) => {
        const active = i === selectedIndex;
        const prevKind = i > 0 ? candidates[i - 1].kind : null;
        const showHeader = prevKind !== c.kind;
        const selectedRef = active ? selectedRowRef : null;
        const onMouseDown = (e: React.MouseEvent) => {
          e.preventDefault();
          onSelect(i);
        };

        return (
          <div key={`${c.kind}:${c.kind === "agent" ? c.agent.name : c.file.path}`}>
            {showHeader && (
              <div className="sticky top-0 bg-slate-800/95 px-2 pt-2 pb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
                {sectionLabel(c.kind)}
              </div>
            )}
            {c.kind === "agent" ? (
              <AgentRow
                candidate={c}
                active={active}
                selectedRef={selectedRef}
                onMouseDown={onMouseDown}
              />
            ) : (
              <FileRow
                candidate={c}
                active={active}
                selectedRef={selectedRef}
                onMouseDown={onMouseDown}
              />
            )}
          </div>
        );
      })}
    </div>
  );

  return createPortal(panel, document.body);
}

// #endregion