/**
 * Chat route — message list + composer.
 *
 * Owns the agent-message subscription that streams the supervisor's
 * output into `conversationStore` (text deltas, tool calls, tool
 * results, turn-complete markers, request cards, errors) and the
 * scroll anchor that pins the view to the latest message. The
 * composer (textarea, attachments, autocomplete, key handlers) is
 * extracted into {@link ChatInput} so this route stays focused on
 * messages + store wiring.
 */

import { useEffect, useRef } from "react";
import ChatInput from "../components/ChatInput";
import { PermissionRequestCard } from "../components/requests/PermissionRequestCard";
import { QuestionCard } from "../components/requests/QuestionCard";
import SessionList from "../components/SessionList";
import ToolResultBlock from "../components/ToolResultBlock";
import ToolUseBlock from "../components/ToolUseBlock";
import { respondToRequest } from "../ipc/commands";
import { onAgentMessage } from "../ipc/events";
import type { AskUserQuestionOutput, AskUserRequestedPayload, Block, MessageRole, PermissionRequestedPayload, } from "../ipc/types";
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
  const markRequestAnswered = useConversationStore((s) => s.markRequestAnswered,);

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
    case "request": {
      if (block.state === "auto-allowed")
      {
        const payload = block.payload as PermissionRequestedPayload;
        return (
          <div className="text-xs text-slate-500 italic my-2">
            Auto-allowed: {payload.toolName}
          </div>
        );
      }

      if (block.subtype === "permission")
      {
        const payload = block.payload as PermissionRequestedPayload;
        const handleDecision = (
          outcome: "allow" | "allow-always" | "deny",
        ) =>
        {
          markRequestAnswered(block.requestId, undefined, outcome);
          void respondToRequest(block.requestId, {
            kind: "permission",
            outcome,
          });
        };
        return (
          <PermissionRequestCard
            payload={payload}
            state={block.state}
            outcome={
              block.outcome === "auto-allowed" ? undefined : block.outcome
            }
            onDecision={handleDecision}
          />
        );
      }

      const payload = block.payload as AskUserRequestedPayload;
      const handleQuestionSubmit = (answer: AskUserQuestionOutput) =>
      {
        markRequestAnswered(block.requestId, answer);
        void respondToRequest(block.requestId, { kind: "question", answer });
      };
      return (
        <QuestionCard
          payload={payload}
          state={block.state}
          onSubmit={handleQuestionSubmit}
          previousAnswer={block.answer}
        />
      );
    }
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
  const appendDelta = useConversationStore((s) => s.appendDelta);
  const appendToolUseBlock = useConversationStore((s) => s.appendToolUseBlock);
  const appendToolResultBlock = useConversationStore((s) => s.appendToolResultBlock,);
  const completeTurn = useConversationStore((s) => s.completeTurn);
  const appendErrorMessage = useConversationStore((s) => s.appendErrorMessage);
  const appendRequestBlock = useConversationStore((s) => s.appendRequestBlock);
  const appendAutoAllowedBlock = useConversationStore((s) => s.appendAutoAllowedBlock,);
  const markRequestAnswered = useConversationStore((s) => s.markRequestAnswered,);

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
        case "permission-requested":
          appendRequestBlock(m.turnId, {
            type: "request",
            requestId: m.requestId,
            subtype: "permission",
            payload: {
              requestId: m.requestId,
              turnId: m.turnId,
              agentId: m.agentId,
              toolName: m.toolName,
              input: m.input,
              blockedPath: m.blockedPath,
              decisionReason: m.decisionReason,
            },
            state: "pending",
          });
          break;
        case "ask-user-requested":
          appendRequestBlock(m.turnId, {
            type: "request",
            requestId: m.requestId,
            subtype: "question",
            payload: {
              requestId: m.requestId,
              turnId: m.turnId,
              agentId: m.agentId,
              input: m.input,
            },
            state: "pending",
          });
          break;
        case "request-resolved":
          if (m.outcome === "auto-allowed")
          {
            if (m.turnId !== null && m.toolName !== null)
            {
              appendAutoAllowedBlock(m.turnId, m.requestId, m.toolName);
            }
          }
          else
          {
            markRequestAnswered(
              m.requestId,
              m.answer ?? undefined,
              m.outcome,
            );
          }
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
    appendRequestBlock,
    appendAutoAllowedBlock,
    markRequestAnswered,
  ]);

  // Auto-anchor the scroll to the bottom on every new message.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length]);

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
                    <BlockView
                      key={b.type === "request" ? b.requestId : i}
                      block={b}
                    />
                  ))}
                </div>
              </div>
            ))
          )}
          <div ref={bottomRef} />
        </div>

        <ChatInput />
      </div>
    </div>
  );
}