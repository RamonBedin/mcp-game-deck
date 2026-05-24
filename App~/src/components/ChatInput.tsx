/**
 * Chat composer — owns the textarea, autocomplete state, drag-drop
 * attachments, and submission handlers.
 *
 * visual layer rewritten to match the `ChatComposer`
 * mockup in `atoms.jsx` — single elevated surface with bg-bg-2 and
 * line-hard border, attachments rendered as `Pill` chips above the
 * textarea, mode hint + gradient Send button in the footer row. All
 * autocomplete + keyboard + drag-drop logic is preserved verbatim
 * from v1; only the chrome changed.
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
import { useAgents } from "../hooks/useAgents";
import { applyAtSelection, useAtAutocomplete } from "../hooks/useAtAutocomplete";
import { useCommands } from "../hooks/useCommands";
import { useFileDragDrop } from "../hooks/useFileDragDrop";
import { useProjectFiles } from "../hooks/useProjectFiles";
import { applySlashSelection, useSlashAutocomplete, } from "../hooks/useSlashAutocomplete";
import { setPermissionMode as setPermissionModeCommand } from "../ipc/commands";
import type { PermissionMode } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";
import AtDropdown from "./AtDropdown";
import SlashDropdown from "./SlashDropdown";
import Button from "./atoms/Button";
import Pill from "./atoms/Pill";

// #region Constants & helpers

const PERMISSION_MODE_CYCLE: PermissionMode[] = [
  "default",
  "acceptEdits",
  "plan",
  "bypassPermissions",
  "auto",
];

const DROPDOWN_ROW_HEIGHT_PX = 28;
const DROPDOWN_PANEL_MAX_HEIGHT_PX = 280;
const DROPDOWN_PANEL_GAP_PX = 4;

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
 * Renders the bottom-of-chat composer: attachment chips, textarea with
 * drag-drop overlay, footer hint + gradient Send button, plus the
 * floating slash/@-command dropdowns.
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

  const agents = useAgents();
  const { files } = useProjectFiles();
  const at = useAtAutocomplete(input, cursorPosition, agents, files);

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

  const applyAtByIndex = (index: number) => {
    if (at.range === null)
    {
      return;
    }

    const candidate = at.candidates[index];

    if (candidate === undefined)
    {
      return;
    }

    const result = applyAtSelection(input, at.range, candidate);
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

    if (at.active)
    {
      if (e.key === "ArrowDown")
      {
        e.preventDefault();
        at.next();
        return;
      }

      if (e.key === "ArrowUp")
      {
        e.preventDefault();
        at.prev();
        return;
      }

      if (e.key === "Enter")
      {
        e.preventDefault();
        applyAtByIndex(at.selectedIndex);
        return;
      }

      if (e.key === "Tab" && !e.shiftKey)
      {
        e.preventDefault();
        applyAtByIndex(at.selectedIndex);
        return;
      }

      if (e.key === "Escape")
      {
        e.preventDefault();
        at.cancel();
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

  const computeDropdownAnchor = (candidatesLen: number): { top: number; left: number } => {
    const ta = textareaRef.current;

    if (ta === null)
    {
      return { top: 0, left: 0 };
    }

    const rect = ta.getBoundingClientRect();
    const estimatedHeight = Math.min(
      DROPDOWN_PANEL_MAX_HEIGHT_PX,
      candidatesLen * DROPDOWN_ROW_HEIGHT_PX + DROPDOWN_PANEL_GAP_PX,
    );
    return {
      top: rect.top - estimatedHeight - DROPDOWN_PANEL_GAP_PX,
      left: rect.left,
    };
  };

  // #endregion

  const canSend = input.trim().length > 0 || pendingAttachments.length > 0;

  return (
    <form
      onSubmit={handleSubmit}
      className="shrink-0 border-t border-line bg-bg-0 px-[18px] pt-3 pb-3.5"
    >
      {pendingAttachments.length > 0 && (
        <div className="flex flex-wrap gap-1.5 mb-2">
          {pendingAttachments.map((p) => (
            <Pill key={p} variant="subtle" size="md">
              <span
                className="normal-case font-mono"
                style={{ letterSpacing: "normal" }}
                title={p}
              >
                {basenameOf(p)}
              </span>
              <button
                type="button"
                onClick={() => removeAttachment(p)}
                className="ml-1 text-txt-4 hover:text-txt-1 transition-colors duration-[120ms]"
                aria-label={`Remove ${basenameOf(p)}`}
              >
                ×
              </button>
            </Pill>
          ))}
        </div>
      )}

      <div className="relative rounded-r-3 border border-line-hard bg-bg-2 px-3 py-2.5 transition-colors duration-[120ms] focus-within:border-brand-violet">
        <textarea
          ref={textareaRef}
          value={input}
          onChange={(e) => {
            setInput(e.target.value);
            setCursorPosition(e.target.selectionStart);
          }}
          onSelect={handleTextareaSelect}
          onKeyDown={handleKeyDown}
          rows={2}
          placeholder="Type / for commands, @ for files…"
          className="w-full resize-none bg-transparent border-none outline-none text-txt-1 font-body text-[13.5px] leading-relaxed placeholder:text-txt-4"
        />

        <div className="flex items-center gap-2.5 mt-2 font-mono text-[11px] text-txt-4">
          <span className="inline-flex items-center gap-1.5">
            <span
              aria-hidden="true"
              className="inline-block rounded-r-1 border border-line-hard bg-bg-3"
              style={{ width: 12, height: 12 }}
            />
            <span>attach</span>
          </span>
          <span className="text-txt-5" aria-hidden="true">·</span>
          <span>⏎ send · ⇧⏎ newline · ⇧⇥ cycle mode</span>
          <span className="ml-auto">
            <Button
              type="submit"
              variant="primary"
              size="sm"
              disabled={!canSend}
              icon={<span style={{ fontSize: 12 }}>↗</span>}
            >
              Send
            </Button>
          </span>
        </div>

        {isDragging && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center rounded-r-3 border-2 border-dashed border-brand-violet bg-brand-violet/10 font-mono text-[11px] uppercase tracking-wider text-brand-violet-soft">
            Drop files to attach
          </div>
        )}
      </div>

      {slash.active && (
        <SlashDropdown
          candidates={slash.candidates}
          selectedIndex={slash.selectedIndex}
          anchor={computeDropdownAnchor(slash.candidates.length)}
          onSelect={(i) => applySlashByIndex(i)}
          onClose={() => slash.cancel()}
        />
      )}

      {!slash.active && at.active && (
        <AtDropdown
          candidates={at.candidates}
          selectedIndex={at.selectedIndex}
          anchor={computeDropdownAnchor(at.candidates.length)}
          onSelect={(i) => applyAtByIndex(i)}
          onClose={() => at.cancel()}
        />
      )}
    </form>
  );
}

// #endregion