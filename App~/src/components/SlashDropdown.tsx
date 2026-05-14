/**
 * Portal-rendered dropdown panel for the slash-command autocomplete.
 *
 * Pure controlled component: every interaction (selection change,
 * close intent) bubbles up via `onSelect` / `onClose`. The state
 * machine lives in `useSlashAutocomplete`  and the
 * positioning math lives in the chat-input consumer —
 * this component only renders the panel at the supplied anchor.
 *
 * Anchor coordinates are interpreted as viewport pixels (compatible
 * with `getBoundingClientRect()`), so the rendered panel uses
 * `position: fixed`. The consumer is expected to pre-compute the
 * top-left corner with above-vs-below flip logic; the dropdown does
 * not measure or re-flip.
 *
 * **Why portal:** the chat composer lives inside a stacking-context
 * sandwich (sticky toolbar + drag-drop overlay siblings). Rendering
 * via `createPortal` to `document.body` sidesteps z-index ordering
 * surprises and is the conventional approach for floating UI in
 * v2.0+ work (see spec → "Slash dropdown behavior").
 *
 * **Why `onMouseDown` instead of `onClick`:** clicking a row needs
 * to insert without first transferring focus away from the textarea
 * (which would fire `blur`-driven side effects in the input
 * wrapper). `onMouseDown` + `preventDefault` is the standard
 * autocomplete pattern.
 *
 * **Click-outside via capture-phase mousedown:** registering on
 * `document` in the capture phase ensures the close fires before any
 * bubbling handler the rest of the app might attach. The handler
 * ignores clicks whose target is inside `panelRef`, so dropdown rows
 * keep working normally.
 */

import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import type { CatalogCommand, CommandSource } from "../ipc/types";

// #region Types

/**
 * Props for {@link SlashDropdown}.
 *
 * `anchor` is the viewport-pixel top-left where the panel renders;
 * the consumer computes this from the textarea's bounding box and
 * passes it in unchanged.
 */
export interface SlashDropdownProps
{
  candidates: CatalogCommand[];
  selectedIndex: number;
  anchor: { top: number; left: number };
  onSelect: (index: number) => void;
  onClose: () => void;
}

// #endregion

// #region Helpers

/**
 * Maps the abstract `source` enum to a short uppercase badge label.
 * Keeps the rendering layer free of switch statements at the JSX
 * level and lets the legend stay in one place if labels change.
 */
function sourceBadgeLabel(source: CommandSource): string
{
  switch (source)
  {
    case "built-in":
      return "BUILT-IN";
    case "plugin":
      return "PLUGIN";
    case "user-command":
      return "USER";
    case "third-party":
      return "EXT";
  }
}

// #endregion

// #region Component

/**
 * Renders the slash-command dropdown panel as a portal under
 * `document.body`. See module docblock for the interaction model.
 *
 * @param props - See {@link SlashDropdownProps}.
 * @returns The portal element, or `null` when there are no
 *   candidates to show (consumer is responsible for hiding the
 *   `active` state; rendering an empty panel would still steal
 *   click-outside attention).
 */
export default function SlashDropdown({candidates, selectedIndex, anchor, onSelect, onClose,}: SlashDropdownProps)
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
  // beyond the visible scroll window. `block: "nearest"` avoids
  // jarring re-centers when the row is already on-screen.
  useEffect(() => {
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
      {candidates.map((cmd, i) => {
        const active = i === selectedIndex;

        return (
          <button
            key={cmd.name}
            ref={active ? selectedRowRef : undefined}
            type="button"
            role="option"
            aria-selected={active}
            onMouseDown={(e) => {
              e.preventDefault();
              onSelect(i);
            }}
            className={`flex w-full items-center gap-2 px-2 py-1.5 text-left text-xs transition-colors ${
              active
                ? "bg-sky-900/40 text-sky-100"
                : "text-slate-300 hover:bg-slate-700/40"
            }`}
          >
            <span className="font-mono text-slate-100 shrink-0">
              /{cmd.name}
            </span>
            {cmd.argumentHint && (
              <span className="font-mono text-[10px] text-slate-500 shrink-0">
                {cmd.argumentHint}
              </span>
            )}
            <span className="flex-1 truncate text-slate-400">
              {cmd.description}
            </span>
            <span className="shrink-0 rounded bg-slate-700/60 px-1.5 py-0.5 text-[10px] uppercase tracking-wider text-slate-400">
              {sourceBadgeLabel(cmd.source)}
            </span>
          </button>
        );
      })}
    </div>
  );

  return createPortal(panel, document.body);
}

// #endregion