/**
 * Chat route — message list + composer.
 *
 * Subscribes to `agent-message` for streamed assistant replies (text
 * deltas, turn-complete markers, errors), auto-scrolls to the bottom
 * on every new message, and submits user input on Enter (Shift+Enter
 * inserts a newline).
 */

import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import PermissionModeToggle from "../components/PermissionModeToggle";
import SessionList from "../components/SessionList";
import ToolResultBlock from "../components/ToolResultBlock";
import ToolUseBlock from "../components/ToolUseBlock";
import { useFileDragDrop } from "../hooks/useFileDragDrop";
import { setPermissionMode as setPermissionModeCommand } from "../ipc/commands";
import { onAgentMessage } from "../ipc/events";
import type { Block, MessageRole, PermissionMode } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

// #region Helpers

const roleColor = (role: MessageRole): string => {
  switch (role) {
    case "user":
      return "text-sky-400";
    case "assistant":
      return "text-emerald-400";
    case "system":
      return "text-amber-400";
  }
};

const PERMISSION_MODE_CYCLE: PermissionMode[] = [
  "default",
  "acceptEdits",
  "plan",
  "bypassPermissions",
  "auto",
];

const nextPermissionMode = (current: PermissionMode): PermissionMode => {
  const idx = PERMISSION_MODE_CYCLE.indexOf(current);
  const next = (idx + 1) % PERMISSION_MODE_CYCLE.length;
  return PERMISSION_MODE_CYCLE[next];
};

const basenameOf = (filePath: string): string => {
  const idx = Math.max(filePath.lastIndexOf("/"), filePath.lastIndexOf("\\"));
  return idx >= 0 ? filePath.slice(idx + 1) : filePath;
};

/**
 * Renders a single message block — text inline, tool blocks via the
 * dedicated collapsible components.
 *
 * @param props - The block to render.
 * @returns The rendered block element.
 */
function BlockView({ block }: { block: Block })
{
  switch (block.type)
  {
    case "text":
      return (
        <div className="whitespace-pre-wrap font-mono text-sm text-slate-200">
          {block.text}
        </div>
      );
    case "tool-use":
      return <ToolUseBlock name={block.name} input={block.input} />;
    case "tool-result":
      return (
        <ToolResultBlock content={block.content} isError={block.isError} />
      );
  }
}

// #endregion

/**
 * Chat route component.
 *
 * @returns The chat view: message list above, composer below.
 */
export default function ChatRoute() {
  const messages = useConversationStore((s) => s.messages);
  const sendMessage = useConversationStore((s) => s.sendMessage);
  const appendDelta = useConversationStore((s) => s.appendDelta);
  const appendToolUseBlock = useConversationStore((s) => s.appendToolUseBlock);
  const appendToolResultBlock = useConversationStore((s) => s.appendToolResultBlock,);
  const completeTurn = useConversationStore((s) => s.completeTurn);
  const appendErrorMessage = useConversationStore((s) => s.appendErrorMessage);
  const permissionMode = useConversationStore((s) => s.permissionMode);
  const setPermissionMode = useConversationStore((s) => s.setPermissionMode);

  const [input, setInput] = useState("");
  const [pendingAttachments, setPendingAttachments] = useState<string[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);

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

  // #region Effects

  // Subscribe once: agent messages from the Claude Code supervisor
  // arrive via `agent-message` (dispatched in spawn.rs::read_stdout
  // after parsing each sdk-entry.js stdout line).
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onAgentMessage((payload) => {
      if (cancelled)
      {
        return;
      }

      const m = payload.message;
      switch (m.type)
      {
        case "text-delta":
          appendDelta(m.turnId, m.text);
          break;
        case "tool-use":
          appendToolUseBlock(m.turnId, m.toolUseId, m.name, m.input);
          break;
        case "tool-result":
          appendToolResultBlock(m.turnId, m.toolUseId, m.content, m.isError);
          break;
        case "assistant-turn-complete":
          completeTurn(m.turnId);
          break;
        case "error":
          appendErrorMessage(m.message);
          break;
        case "ready":
        case "assistant-text":
          break;
      }
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
        console.error("[chat] failed to subscribe to agent-message:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [
    appendDelta,
    appendToolUseBlock,
    appendToolResultBlock,
    completeTurn,
    appendErrorMessage,
  ]);

  // Auto-anchor the scroll to the bottom on every new message.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length]);

  // #endregion

  // #region Handlers

  const submit = () => {
    if (!input.trim() && pendingAttachments.length === 0)
    {
      return;
    }

    void sendMessage(input, pendingAttachments);
    setInput("");
    setPendingAttachments([]);
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    submit();
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
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

  return (
    <div className="flex h-full gap-4">
      <aside className="w-60 shrink-0 border-r border-slate-800 pr-3">
        <SessionList />
      </aside>

      <div className="flex h-full min-w-0 flex-1 flex-col">
        <h1 className="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-500">
          Chat
        </h1>

        <div className="flex-1 space-y-2 overflow-y-auto pr-1">
          {messages.length === 0 ? (
            <p className="text-sm text-slate-500">
              Type a message and press Enter to send.
            </p>
          ) : (
            messages.map((m) => (
              <div key={m.id} className="rounded bg-slate-800/40 p-3">
                <div
                  className={`mb-1 text-xs font-semibold uppercase tracking-wide ${roleColor(m.role)}`}
                >
                  {m.role}
                </div>
                <div className="space-y-2">
                  {m.blocks.map((b, i) => (
                    <BlockView key={i} block={b} />
                  ))}
                </div>
              </div>
            ))
          )}
          <div ref={bottomRef} />
        </div>

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
              value={input}
              onChange={(e) => setInput(e.target.value)}
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
        </form>
      </div>
    </div>
  );
}