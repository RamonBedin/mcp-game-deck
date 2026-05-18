/**
 * Permission mode picker — a small popover with 4 segmented options
 * (Ask / Auto-edit / Plan / Free) that drives the conversation
 * store's `permissionMode` plus the backend `set_permission_mode`
 * command.
 *
 * Two surfaces consume this atom:
 *   - `HudStrip` — the "MODE · ASK" label opens this popover when
 *     clicked. Replaces the v1 `PermissionModeToggle` button that was
 *     removed during the UX Pass; the design vision specs a
 *     Segmented Control here (§Components / Segmented control).
 *   - `Settings → Claude Code → Default mode` — inline picker.
 *
 * The 5 SDK modes collapse to 4 user-facing labels:
 *   - `default`           → Ask
 *   - `acceptEdits`       → Auto-edit
 *   - `plan`              → Plan
 *   - `bypassPermissions` → Free
 *   - `auto`              → Free (UI alias for bypassPermissions; see CLAUDE.md gotcha)
 *
 * Apply order: optimistic store update → IPC call. On IPC failure
 * the previous mode is restored and an error is logged.
 */

import { useEffect, useRef, useState } from "react";
import { setPermissionMode as setPermissionModeCommand } from "../ipc/commands";
import type { PermissionMode } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

// #region Types

/**
 * Display style:
 *  - `hud`     — popover triggered by the "MODE · X" label
 *  - `inline`  — segmented row used inside Settings
 */
interface PermissionModePickerProps
{
  variant?: "hud" | "inline";
}

/**
 * Shape of a single option in the permission-mode selector.
 *
 * Pairs the underlying enum value with the human-readable label rendered in
 * the picker and a short hint explaining what the mode does.
 */
interface ModeOption
{
  value: PermissionMode;
  label: string;
  hint: string;
}

// #endregion

// #region Mode table

const MODE_OPTIONS: readonly ModeOption[] = [
  { value: "default",           label: "Ask",       hint: "Prompt for every write or destructive tool call." },
  { value: "acceptEdits",       label: "Auto-edit", hint: "Auto-allow file edits; still prompts for destructive." },
  { value: "plan",              label: "Plan",      hint: "Read-only — Claude plans without touching disk." },
  { value: "bypassPermissions", label: "Free",      hint: "Allow everything. Use only when you trust the turn." },
];

/** Display label for the currently active mode. `auto` collapses to "Free". */
export function permissionModeLabel(mode: PermissionMode): string
{
  if (mode === "auto" || mode === "bypassPermissions")
  {
    return "Free";
  }

  return MODE_OPTIONS.find((o) => o.value === mode)?.label ?? "Ask";
}

// #endregion

/**
 * Renders the picker. `hud` variant returns a clickable label that
 * toggles a popover; `inline` variant returns a flat segmented row.
 *
 * @param props - See {@link PermissionModePickerProps}.
 * @returns The picker element.
 */
export default function PermissionModePicker({ variant = "hud" }: PermissionModePickerProps)
{
  const mode = useConversationStore((s) => s.permissionMode);
  const setMode = useConversationStore((s) => s.setPermissionMode);
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

  const applyMode = (next: PermissionMode) => {
    if (next === mode)
    {
      setOpen(false);
      return;
    }

    const previous = mode;
    setMode(next);
    setOpen(false);

    void setPermissionModeCommand(next).catch((err) => {
      console.error("[permission-mode] set failed, reverting:", err);
      setMode(previous);
    });
  };

  if (variant === "inline")
  {
    return <SegmentedRow mode={mode} onPick={applyMode} />;
  }

  return (
    <div ref={popoverRef} className="relative inline-flex">
      <button
        type="button"
        onClick={() => setOpen((prev) => !prev)}
        aria-haspopup="menu"
        aria-expanded={open}
        title="Cycle permission mode (⇧⇥)"
        className="inline-flex items-center gap-1 rounded-r-1 px-1.5 py-0.5 font-mono text-[11px] text-brand-violet-soft hover:bg-bg-3 transition-colors duration-[120ms]"
      >
        <span>MODE · {permissionModeLabel(mode).toUpperCase()}</span>
        <span className="text-txt-4" style={{ fontSize: 9 }}>▾</span>
      </button>

      {open && (
        <div
          role="menu"
          className="absolute top-full left-0 mt-1.5 z-50 min-w-[240px] rounded-r-3 border border-line-hard bg-bg-2 shadow-elev-3 overflow-hidden"
        >
          <div className="font-hud text-[9px] tracking-[0.18em] uppercase text-txt-4 px-3 pt-2.5 pb-1.5">
            Permission mode
          </div>
          {MODE_OPTIONS.map((opt) => {
            const active = isOptionActive(opt.value, mode);
            return (
              <button
                type="button"
                key={opt.value}
                role="menuitemradio"
                aria-checked={active}
                onClick={() => applyMode(opt.value)}
                className={[
                  "w-full text-left px-3 py-2 transition-colors duration-[120ms]",
                  active
                    ? "bg-brand-violet/10 shadow-[inset_2px_0_0_var(--violet)]"
                    : "hover:bg-bg-3",
                ].join(" ")}
              >
                <div className="flex items-center gap-2">
                  <span
                    className="text-[13px]"
                    style={{
                      color: active ? "var(--violet-soft)" : "var(--txt-1)",
                      fontWeight: active ? 500 : 400,
                    }}
                  >
                    {opt.label}
                  </span>
                  {active && (
                    <span className="font-mono text-[9px] text-brand-violet-soft ml-auto">ACTIVE</span>
                  )}
                </div>
                <div className="text-[11.5px] text-txt-3 mt-0.5 leading-snug">{opt.hint}</div>
              </button>
            );
          })}
          <div className="border-t border-line-soft px-3 py-1.5 font-mono text-[10px] text-txt-4">
            ⇧⇥ cycle · click to set
          </div>
        </div>
      )}
    </div>
  );
}

// #region Helpers

/** True when `option` matches `current`, with `auto`↔`bypassPermissions` aliasing. */
function isOptionActive(option: PermissionMode, current: PermissionMode): boolean
{
  if (option === "bypassPermissions" && current === "auto")
  {
    return true;
  }

  return option === current;
}

// #endregion

// #region SegmentedRow (inline variant)

/**
 * Props for the `SegmentedRow` component.
 *
 * Renders the segmented permission-mode picker, highlighting the active mode
 * and reporting user selections back to the parent via `onPick`.
 */
interface SegmentedRowProps
{
  mode: PermissionMode;
  onPick: (next: PermissionMode) => void;
}

const SegmentedRow = ({ mode, onPick }: SegmentedRowProps) => (
  <div
    role="radiogroup"
    aria-label="Permission mode"
    className="inline-flex rounded-r-2 border border-line-hard bg-bg-1 p-0.5"
  >
    {MODE_OPTIONS.map((opt) => {
      const active = isOptionActive(opt.value, mode);
      return (
        <button
          type="button"
          key={opt.value}
          role="radio"
          aria-checked={active}
          onClick={() => onPick(opt.value)}
          title={opt.hint}
          className={[
            "px-3 py-1 rounded-r-1 text-[12px] transition-colors duration-[120ms]",
            active
              ? "bg-bg-4 text-txt-1 font-medium"
              : "text-txt-3 hover:text-txt-1",
          ].join(" ")}
        >
          {opt.label}
        </button>
      );
    })}
  </div>
);

// #endregion