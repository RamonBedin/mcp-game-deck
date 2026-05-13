/**
 * Chat composer — owns the textarea, autocomplete state, drag-drop
 * attachments, and submission handlers.
 *
 * Extracted from `ChatRoute.tsx` to keep that route focused on the
 * message list + supervisor subscription. ChatInput is a zero-prop
 * component: it pulls everything it needs from the conversation
 * store and the catalog store, so the route just renders
 * `<ChatInput />` at the bottom of its column.
 *
 * Slash autocomplete integration:
 *
 * - `value` and `cursorPosition` are tracked as local state and fed
 *   into `useSlashAutocomplete`. `cursorPosition` is updated on
 *   every `onSelect` event of the textarea — that single event
 *   covers typing, arrow keys, mouse clicks, and programmatic moves.
 * - On apply, the new value is committed via `setInput` and the new
 *   caret position is queued in `pendingCursorRef`. A `useLayoutEffect`
 *   flushes that into `setSelectionRange` after React commits the
 *   textarea's new value, then clears the ref so subsequent renders
 *   don't fight user-driven cursor moves.
 * - The dropdown's anchor is computed at render time from the
 *   textarea's `getBoundingClientRect()` plus a row-height estimate
 *   (~28px per row, capped at the dropdown's 280px max-height). v2.0
 *   approximation; F09 polish may switch to a mirror-div caret-anchor.
 *
 * Keyboard handler precedence:
 *
 *   1. If the slash dropdown is active: ArrowDown / ArrowUp / Enter
 *      / Tab / Escape are owned by the dropdown.
 *   2. Otherwise: Enter (no shift) submits; Shift+Tab cycles the
 *      permission mode; everything else behaves natively (including
 *      Shift+Enter for newline).
 *
 * Shift+Tab while the dropdown is active still cycles the permission
 * mode — there's no plausible use case for "Shift+Tab inside an
 * autocomplete," and preserving the existing behavior keeps the
 * mental model uniform across the input's lifetime.
 */

import { useCallback, useLayoutEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent, SyntheticEvent } from "react";
import { useCommands } from "../hooks/useCommands";
import { useFileDragDrop } from "../hooks/useFileDragDrop";
import { applySlashSelection, useSlashAutocomplete, } from "../hooks/useSlashAutocomplete";
import { setPermissionMode as setPermissionModeCommand } from "../ipc/commands";
import type { PermissionMode } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";
import PermissionModeToggle from "./PermissionModeToggle";
import SlashDropdown from "./SlashDropdown";

// #region Constants & helpers

const PERMISSION_MODE_CYCLE: PermissionMode[] = [
  "default",
  "acceptEdits",
  "plan",
  "bypassPermissions",
  "auto",
];

const SLASH_ROW_HEIGHT_PX = 28;
const SLASH_PANEL_MAX_HEIGHT_PX = 280;
const SLASH_PANEL_GAP_PX = 4;

const nextPermissionMode = (current: PermissionMode): PermissionMode => {
  const idx = PERMISSION_MODE_CYCLE.indexOf(current);
  const next = (idx + 1) % PERMISSION_MODE_CYCLE.length;
  return PERMISSION_MODE_CYCLE[next];
};

const basenameOf = (filePath: string): string => {
  const idx = Math.max(filePath.lastIndexOf("/"), filePath.lastIndexOf("\\"));
  return idx >= 0 ? filePath.slice(idx + 1) : filePath;
};

// #endregion

// #region Component

/**
 * Renders the bottom-of-chat composer: permission mode toggle,
 * attachment chips, textarea with drag-drop overlay, send button,
 * and the floating slash-command dropdown.
 *
 * @returns The composer form element.
 */
export default function ChatInput()
{
  const sendMessage = useConversationStore((s) => s.sendMessage);
  const permissionMode = useConversationStore((s) => s.permissionMode);
  const setPermissionMode = useConversationStore((s) => s.setPermissionMode);

  const [input, setInput] = useState("");
  const [cursorPosition, setCursorPosition] = useState(0);
  const [pendingAttachments, setPendingAttachments] = useState<string[]>([]);

  const textareaRef = useRef<HTMLTextAreaElement | null>(null);
  const pendingCursorRef = useRef<number | null>(null);

  const commands = useCommands();
  const slash = useSlashAutocomplete(input, cursorPosition, commands);

  // #region Drag-drop

  const handleFilesDropped = useCallback((paths: string[]) => {
    setPendingAttachments((prev) => {
      const merged = [...prev];
      for (const p of paths)
      {
        if (!merged.includes(p))
        {
          merged.push(p);
        }
      }
      return merged;
    });
  }, []);

  const { isDragging } = useFileDragDrop(handleFilesDropped);

  const removeAttachment = (target: string) => {
    setPendingAttachments((prev) => prev.filter((p) => p !== target));
  };

  // #endregion

  // #region Cursor sync

  // Apply queued cursor moves after the textarea's value has
  // committed. Without this layout effect, calling `setSelectionRange`
  // immediately after `setInput` would target the pre-render DOM and
  // leave the caret stuck before the inserted command.
  useLayoutEffect(() => {
    const pending = pendingCursorRef.current;

    if (pending !== null && textareaRef.current !== null)
    {
      textareaRef.current.setSelectionRange(pending, pending);
      textareaRef.current.focus();
      pendingCursorRef.current = null;
    }
  });

  const handleTextareaSelect = (e: SyntheticEvent<HTMLTextAreaElement>) => {
    setCursorPosition(e.currentTarget.selectionStart);
  };

  // #endregion

  // #region Submit + key handlers

  const submit = () => {
    if (!input.trim() && pendingAttachments.length === 0)
    {
      return;
    }

    void sendMessage(input, pendingAttachments);
    setInput("");
    setCursorPosition(0);
    setPendingAttachments([]);
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    submit();
  };

  const applySlashByIndex = (index: number) => {
    if (slash.range === null)
    {
      return;
    }

    const candidate = slash.candidates[index];

    if (candidate === undefined)
    {
      return;
    }

    const result = applySlashSelection(input, slash.range, candidate.name);
    pendingCursorRef.current = result.newCursor;
    setInput(result.newValue);
    setCursorPosition(result.newCursor);
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (slash.active)
    {
      if (e.key === "ArrowDown")
      {
        e.preventDefault();
        slash.next();
        return;
      }

      if (e.key === "ArrowUp")
      {
        e.preventDefault();
        slash.prev();
        return;
      }

      if (e.key === "Enter")
      {
        e.preventDefault();
        applySlashByIndex(slash.selectedIndex);
        return;
      }

      if (e.key === "Tab" && !e.shiftKey)
      {
        e.preventDefault();
        applySlashByIndex(slash.selectedIndex);
        return;
      }

      if (e.key === "Escape")
      {
        e.preventDefault();
        slash.cancel();
        return;
      }
    }

    if (e.key === "Enter" && !e.shiftKey)
    {
      e.preventDefault();
      submit();
      return;
    }

    if (e.key === "Tab" && e.shiftKey)
    {
      e.preventDefault();
      const next = nextPermissionMode(permissionMode);
      const previous = permissionMode;
      setPermissionMode(next);

      void setPermissionModeCommand(next).catch((err) => {
        console.error("[chat] Shift+Tab permission cycle failed:", err);
        setPermissionMode(previous);
      });
    }
  };

  // #endregion

  // #region Anchor

  const computeSlashAnchor = (): { top: number; left: number } => {
    const ta = textareaRef.current;

    if (ta === null)
    {
      return { top: 0, left: 0 };
    }

    const rect = ta.getBoundingClientRect();
    const estimatedHeight = Math.min(SLASH_PANEL_MAX_HEIGHT_PX, slash.candidates.length * SLASH_ROW_HEIGHT_PX + SLASH_PANEL_GAP_PX,);
    return {
      top: rect.top - estimatedHeight - SLASH_PANEL_GAP_PX,
      left: rect.left,
    };
  };

  // #endregion

  return (
    <form onSubmit={handleSubmit} className="mt-3 flex flex-col gap-2">
      <div className="flex items-center justify-end">
        <PermissionModeToggle />
      </div>

      {pendingAttachments.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {pendingAttachments.map((p) => (
            <span
              key={p}
              className="flex items-center gap-1 rounded border border-slate-700 bg-slate-800/60 px-2 py-0.5 text-xs text-slate-300"
              title={p}
            >
              <span className="max-w-[180px] truncate">{basenameOf(p)}</span>
              <button
                type="button"
                onClick={() => removeAttachment(p)}
                className="text-slate-500 hover:text-slate-200"
                aria-label={`Remove ${basenameOf(p)}`}
              >
                ×
              </button>
            </span>
          ))}
        </div>
      )}

      <div className="relative">
        <textarea
          ref={textareaRef}
          value={input}
          onChange={(e) => {
            setInput(e.target.value);
            setCursorPosition(e.target.selectionStart);
          }}
          onSelect={handleTextareaSelect}
          onKeyDown={handleKeyDown}
          rows={3}
          placeholder="Type a message... (Enter to send, Shift+Enter for newline; drop files to attach)"
          className="w-full resize-none rounded border border-slate-700 bg-slate-800 px-3 py-2 font-mono text-sm text-slate-100 focus:border-slate-500 focus:outline-none"
        />
        {isDragging && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center rounded border-2 border-dashed border-sky-500 bg-sky-950/70 text-xs font-semibold uppercase tracking-wider text-sky-200">
            Drop files to attach
          </div>
        )}
      </div>

      <button
        type="submit"
        disabled={!input.trim() && pendingAttachments.length === 0}
        className="self-end rounded bg-sky-700 px-4 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:cursor-not-allowed disabled:opacity-50"
      >
        Send
      </button>

      {slash.active && (
        <SlashDropdown
          candidates={slash.candidates}
          selectedIndex={slash.selectedIndex}
          anchor={computeSlashAnchor()}
          onSelect={(i) => applySlashByIndex(i)}
          onClose={() => slash.cancel()}
        />
      )}
    </form>
  );
}

// #endregion