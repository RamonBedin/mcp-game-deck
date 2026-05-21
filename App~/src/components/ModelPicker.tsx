/**
 * Model picker — small popover with one option per model the SDK
 * reports as selectable on the current `claude` login. Mirrors
 * `PermissionModePicker`: same `hud` / `inline` variants, same
 * optimistic-update-then-IPC apply pattern, same revert-on-failure.
 *
 * Source of truth for the list: `conversationStore.availableModels`,
 * populated by the `models-available` agent message at supervisor
 * init. The picker is intentionally dynamic — there is no hardcoded
 * model table in this file (or anywhere else). New SDK releases that
 * surface new models light them up here automatically.
 *
 * Two surfaces consume this atom:
 *   - `HudStrip` — the "MODEL · X" label opens this popover.
 *   - `Settings → Claude Code → Default model` — inline picker.
 *
 * `currentModel` is null until the user picks something; while null
 * the supervisor's `query()` omits `options.model` and the CLI default
 * applies. The HUD label collapses to "DEFAULT" in that state.
 */

import { useEffect, useRef, useState } from "react";
import { setModel as setModelCommand } from "../ipc/commands";
import { useConversationStore } from "../stores/conversationStore";

// #region Types

/**
 * Display style:
 *  - `hud`     — popover triggered by the "MODEL · X" label
 *  - `inline`  — segmented row used inside Settings
 */
interface ModelPickerProps
{
  variant?: "hud" | "inline";
}

// #endregion

/**
 * Resolves the short label shown on the model HUD chip.
 *
 * Looks up the current model id in the available-models catalog and returns
 * its `displayName`, falling back to the raw id when the model isn't in the
 * catalog and to `"Default"` when no model is currently selected.
 *
 * @param currentModel - Currently selected model id, or `null` when the
 *   default model is in effect.
 * @param availableModels - Catalog of models with their display labels.
 * @returns A short label suitable for rendering in the HUD chip.
 */
function modelHudLabel(currentModel: string | null, availableModels: ReadonlyArray<{ value: string; displayName: string }>): string
{
  if (currentModel === null)
  {
    return "Default";
  }

  const match = availableModels.find((m) => m.value === currentModel);
  return match?.displayName ?? currentModel;
}

/**
 * Renders the picker. `hud` variant returns a clickable label that
 * toggles a popover; `inline` variant returns a flat segmented row.
 *
 * @param props - See {@link ModelPickerProps}.
 * @returns The picker element.
 */
export default function ModelPicker({ variant = "hud" }: ModelPickerProps)
{
  const currentModel = useConversationStore((s) => s.currentModel);
  const availableModels = useConversationStore((s) => s.availableModels);
  const setCurrentModel = useConversationStore((s) => s.setCurrentModel);
  const [open, setOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement | null>(null);

  // Close popover on outside click (hud variant only).
  useEffect(() => {
    if (!open)
    {
      return;
    }

    const handleClick = (e: MouseEvent) => {
      if (popoverRef.current !== null && !popoverRef.current.contains(e.target as Node))
      {
        setOpen(false);
      }
    };

    window.addEventListener("mousedown", handleClick);
    return () => window.removeEventListener("mousedown", handleClick);
  }, [open]);

  const applyModel = (next: string | null) => {
    if (next === currentModel)
    {
      setOpen(false);
      return;
    }

    const previous = currentModel;
    setCurrentModel(next);
    setOpen(false);

    void setModelCommand(next).catch((err) => {
      console.error("[model] set failed, reverting:", err);
      setCurrentModel(previous);
    });
  };

  if (variant === "inline")
  {
    return <SegmentedRow currentModel={currentModel} availableModels={availableModels} onPick={applyModel} />;
  }

  const hudLabel = modelHudLabel(currentModel, availableModels);

  return (
    <div ref={popoverRef} className="relative inline-flex">
      <button
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        aria-haspopup="menu"
        aria-expanded={open}
        title="Pick the Claude model used for the next turn"
        disabled={availableModels.length === 0}
        className="inline-flex items-center gap-1 rounded-r-1 px-1.5 py-0.5 font-mono text-[11px] text-brand-cyan hover:bg-bg-3 transition-colors duration-[120ms] disabled:opacity-50 disabled:cursor-not-allowed"
      >
        <span>MODEL · {hudLabel.toUpperCase()}</span>
        <span className="text-txt-4" style={{ fontSize: 9 }}>▾</span>
      </button>

      {open && availableModels.length > 0 && (
        <div
          role="menu"
          className="absolute top-full left-0 mt-1.5 z-50 min-w-[280px] rounded-r-3 border border-line-hard bg-bg-2 shadow-elev-3 overflow-hidden"
        >
          <div className="font-hud text-[9px] tracking-[0.18em] uppercase text-txt-4 px-3 pt-2.5 pb-1.5">
            Claude model
          </div>
          {availableModels.map((m) => {
            const active = m.value === currentModel;
            return (
              <button
                type="button"
                key={m.value}
                role="menuitemradio"
                aria-checked={active}
                onClick={() => applyModel(m.value)}
                className={[
                  "w-full text-left px-3 py-2 transition-colors duration-[120ms]",
                  active
                    ? "bg-brand-cyan/10 shadow-[inset_2px_0_0_var(--cyan)]"
                    : "hover:bg-bg-3",
                ].join(" ")}
              >
                <div className="flex items-center gap-2">
                  <span
                    className="text-[13px]"
                    style={{
                      color: active ? "var(--cyan)" : "var(--txt-1)",
                      fontWeight: active ? 500 : 400,
                    }}
                  >
                    {m.displayName}
                  </span>
                  {active && (
                    <span className="font-mono text-[9px] text-brand-cyan ml-auto">ACTIVE</span>
                  )}
                </div>
                {m.description.length > 0 && (
                  <div className="text-[11.5px] text-txt-3 mt-0.5 leading-snug">{m.description}</div>
                )}
              </button>
            );
          })}
          <button
            type="button"
            role="menuitemradio"
            aria-checked={currentModel === null}
            onClick={() => applyModel(null)}
            className={[
              "w-full text-left px-3 py-2 border-t border-line-soft transition-colors duration-[120ms]",
              currentModel === null
                ? "bg-brand-cyan/10 shadow-[inset_2px_0_0_var(--cyan)]"
                : "hover:bg-bg-3",
            ].join(" ")}
          >
            <div className="flex items-center gap-2">
              <span
                className="text-[13px]"
                style={{
                  color: currentModel === null ? "var(--cyan)" : "var(--txt-1)",
                  fontWeight: currentModel === null ? 500 : 400,
                }}
              >
                CLI default
              </span>
              {currentModel === null && (
                <span className="font-mono text-[9px] text-brand-cyan ml-auto">ACTIVE</span>
              )}
            </div>
            <div className="text-[11.5px] text-txt-3 mt-0.5 leading-snug">
              Let Claude Code pick — whatever the CLI is configured to use.
            </div>
          </button>
        </div>
      )}
    </div>
  );
}

// #region SegmentedRow (inline variant)

/**
 * Props for the `SegmentedRow` component.
 *
 * Renders the segmented model picker, highlighting the active model
 * and reporting user selections back to the parent via `onPick`.
 */
interface SegmentedRowProps
{
  currentModel: string | null;
  availableModels: ReadonlyArray<{ value: string; displayName: string; description: string }>;
  onPick: (next: string | null) => void;
}

const SegmentedRow = ({ currentModel, availableModels, onPick }: SegmentedRowProps) => {
  if (availableModels.length === 0)
  {
    return (
      <span className="font-mono text-[11.5px] text-txt-4 italic">
        loading models…
      </span>
    );
  }

  return (
    <div
      role="radiogroup"
      aria-label="Claude model"
      className="inline-flex flex-wrap gap-1 rounded-r-2 border border-line-hard bg-bg-1 p-0.5"
    >
      {availableModels.map((m) => {
        const active = m.value === currentModel;
        return (
          <button
            type="button"
            key={m.value}
            role="radio"
            aria-checked={active}
            onClick={() => onPick(m.value)}
            title={m.description}
            className={[
              "px-3 py-1 rounded-r-1 text-[12px] transition-colors duration-[120ms]",
              active
                ? "bg-bg-4 text-txt-1 font-medium"
                : "text-txt-3 hover:text-txt-1",
            ].join(" ")}
          >
            {m.displayName}
          </button>
        );
      })}
      <button
        type="button"
        role="radio"
        aria-checked={currentModel === null}
        onClick={() => onPick(null)}
        title="Let Claude Code pick — uses the CLI's configured default"
        className={[
          "px-3 py-1 rounded-r-1 text-[12px] transition-colors duration-[120ms]",
          currentModel === null
            ? "bg-bg-4 text-txt-1 font-medium"
            : "text-txt-3 hover:text-txt-1",
        ].join(" ")}
      >
        Default
      </button>
    </div>
  );
};

// #endregion