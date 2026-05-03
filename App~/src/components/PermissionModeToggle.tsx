/**
 * Permission-mode dropdown rendered in the chat input toolbar.
 *
 * Mirrors the five surface-level modes the Claude Code chat exposes
 * (`default` / `acceptEdits` / `plan` / `bypassPermissions` / `auto`).
 * Reads the current mode on mount via `getPermissionMode()` and
 * forwards changes via `setPermissionMode()` — both round-trip
 * through the Tauri-managed `ClaudeSupervisor` and end up as a
 * `setPermissionMode` control message on `sdk-entry.js`'s stdin.
 *
 * Subscription to passive Shift+Tab cycles (Task 4.3) plugs into the
 * same local state by replacing the `useState` setter via a future
 * `onPermissionModeChanged` listener.
 */

import { useEffect, useState } from "react";
import { getPermissionMode, setPermissionMode as setPermissionModeCommand } from "../ipc/commands";
import { onPermissionModeChanged } from "../ipc/events";
import type { PermissionMode } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

// #region Types

const MODE_OPTIONS: Array<{ value: PermissionMode; label: string }> = [
  { value: "default", label: "Default" },
  { value: "acceptEdits", label: "Accept Edits" },
  { value: "plan", label: "Plan" },
  { value: "bypassPermissions", label: "Bypass" },
  { value: "auto", label: "Auto" },
];

// #endregion

/**
 * Toolbar dropdown that surfaces and controls the supervisor's
 * permission mode.
 *
 * @returns The dropdown element wrapped in a label.
 */
export default function PermissionModeToggle()
{
  const mode = useConversationStore((s) => s.permissionMode);
  const setMode = useConversationStore((s) => s.setPermissionMode);
  const [busy, setBusy] = useState(false);

  // #region Effects

  // On mount, hydrate the store with the supervisor's actual mode.
  // Avoids drift when the supervisor was started with a non-default
  // mode (e.g. after a respawn that re-pushed the stored mode).
  useEffect(() => {
    let cancelled = false;
    getPermissionMode()
      .then((m) => {

        if (!cancelled)
        {
          setMode(m);
        }

      })
      .catch((err) => {
        console.error("[permission-mode] failed to read initial mode:", err);
      });
    return () => {
      cancelled = true;
    };
  }, [setMode]);

  // Passive sync: every time the supervisor confirms a mode change
  // (echo from sdk-entry.js after applying setPermissionMode, or a
  // future Shift+Tab / SDK-driven cycle), update the store. Idempotent
  // when the mode already matches the optimistic update.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onPermissionModeChanged((payload) => {
      if (cancelled)
      {
        return;
      }
      
      setMode(payload.mode);
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[permission-mode] failed to subscribe to mode-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [setMode]);

  // #endregion

  // #region Handlers

  const handleChange = async (next: PermissionMode) => {
    if (next === mode || busy)
    {
      return;
    }

    setBusy(true);
    const previous = mode;
    setMode(next);
    try
    {
      await setPermissionModeCommand(next);
    }
    catch (err)
    {
      console.error("[permission-mode] set failed:", err);
      setMode(previous);
    }
    finally
    {
      setBusy(false);
    }
  };

  // #endregion

  return (
    <label className="flex items-center gap-2 text-xs text-slate-400">
      <span className="uppercase tracking-wider">Permissions</span>
      <select
        value={mode}
        onChange={(e) => void handleChange(e.target.value as PermissionMode)}
        disabled={busy}
        className="rounded border border-slate-700 bg-slate-800 px-2 py-1 font-mono text-xs text-slate-100 focus:border-slate-500 focus:outline-none disabled:opacity-50"
      >
        {MODE_OPTIONS.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </label>
  );
}