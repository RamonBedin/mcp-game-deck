/**
 * Chat route — message list + composer.
 *
 * Subscribes to `agent-message` for streamed assistant replies (text
 * deltas, turn-complete markers, errors), auto-scrolls to the bottom
 * on every new message, and submits user input on Enter (Shift+Enter
 * inserts a newline).
 */

import { useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import PermissionModeToggle from "../components/PermissionModeToggle";
import ToolResultBlock from "../components/ToolResultBlock";
import ToolUseBlock from "../components/ToolUseBlock";
import { onAgentMessage } from "../ipc/events";
import type { Block, MessageRole } from "../ipc/types";
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

  const [input, setInput] = useState("");
  const bottomRef = useRef<HTMLDivElement>(null);

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
    if (!input.trim())
    {
      return;
    }

    void sendMessage(input);
    setInput("");
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
    }
  };

  // #endregion

  return (
    <div className="flex h-full flex-col">
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
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          rows={3}
          placeholder="Type a message... (Enter to send, Shift+Enter for newline)"
          className="resize-none rounded border border-slate-700 bg-slate-800 px-3 py-2 font-mono text-sm text-slate-100 focus:border-slate-500 focus:outline-none"
        />
        <button
          type="submit"
          disabled={!input.trim()}
          className="self-end rounded bg-sky-700 px-4 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Send
        </button>
      </form>
    </div>
  );
}