/**
 * Owns the agent-message subscription and renders the conversation
 * with new visual atoms:
 *
 *   - Empty state replaced by `ChatLaunchpad`.
 *   - `tool-use` + `tool-result` blocks pair into a single
 *     `ToolCallNarrativeBlock` keyed by `toolUseId`. Result content +
 *     duration get attached to the matching call when the result
 *     lands.
 *   - In-flight assistant turn surfaces a `WorkingStrip` between the
 *     scroller and the composer. Activity text is heuristic until
 *     B.01 (supervisor activity stream) ships proper events.
 *   - Permission cards use the tier-aware rewrite.
 *
 * The composer (textarea, autocomplete, drag-drop) is the existing
 * `ChatInput` component — unchanged in this pass other than the
 * permission-mode toggle moving up to the HUD strip. ChatInput still
 * works as-is; the HUD shows the same mode in parallel.
 */

import { useEffect, useMemo, useRef } from "react";
import ChatInput from "../components/ChatInput";
import ChatLaunchpad from "../components/chat/ChatLaunchpad";
import ToolCallNarrativeBlock, { type ToolCallStatus } from "../components/chat/ToolCallNarrativeBlock";
import WorkingStrip from "../components/chat/WorkingStrip";
import PermissionRequestCard from "../components/requests/PermissionRequestCard";
import QuestionCard from "../components/requests/QuestionCard";
import SessionList from "../components/SessionList";
import Avatar from "../components/atoms/Avatar";
import { useCollapsedColumn } from "../hooks/useCollapsedColumn";
import { cancelCurrentTurn, respondToRequest, startNewSession } from "../ipc/commands";
import { onAgentMessage } from "../ipc/events";
import type { AskUserQuestionOutput, AskUserRequestedPayload, Block, Message, PermissionRequestedPayload,} from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

// #region Helpers

const isTurnStreaming = (messages: Message[]): boolean => {
  const last = messages[messages.length - 1];

  if (last === undefined || last.role !== "assistant")
  {
    return false;
  }

  const lastBlock = last.blocks[last.blocks.length - 1];

  if (lastBlock === undefined)
  {
    return true;
  }

  if (lastBlock.type === "tool-use")
  {
    const matchingResult = last.blocks.find((b) => b.type === "tool-result" && b.toolUseId === lastBlock.toolUseId,);
    return matchingResult === undefined;
  }

  if (lastBlock.type === "request" && lastBlock.state === "pending")
  {
    return false;
  }

  return false;
};

/**
 * For an assistant message, pair up `tool-use` with its matching
 * `tool-result` (by toolUseId) and return a render-ready list of
 * narrative slots. Text blocks pass through; request blocks pass
 * through; tool-use blocks absorb their tool-result.
 */
type RenderedBlock =
  | { kind: "text"; text: string }
  | { kind: "tool"; toolUseId: string; name: string; input: unknown; status: ToolCallStatus; output?: unknown; isError?: boolean }
  | { kind: "request"; block: Extract<Block, { type: "request" }> };

const pairToolBlocks = (blocks: Block[]): RenderedBlock[] => {
  const rendered: RenderedBlock[] = [];
  const resultIndex = new Map<string, Extract<Block, { type: "tool-result" }>>();

  for (const b of blocks)
  {
    if (b.type === "tool-result")
    {
      resultIndex.set(b.toolUseId, b);
    }
  }

  for (const b of blocks)
  {
    if (b.type === "text")
    {
      rendered.push({ kind: "text", text: b.text });
      continue;
    }

    if (b.type === "tool-use")
    {
      const result = resultIndex.get(b.toolUseId);
      rendered.push({
        kind: "tool",
        toolUseId: b.toolUseId,
        name: b.name,
        input: b.input,
        status: result === undefined ? "running" : result.isError ? "failed" : "done",
        output: result?.content,
        isError: result?.isError,
      });
      continue;
    }

    if (b.type === "tool-result")
    {
      continue;
    }

    if (b.type === "request")
    {
      rendered.push({ kind: "request", block: b });
    }
  }

  return rendered;
};

// #endregion

/**
 * Chat route component. Subscribes to agent messages once; everything
 * else is derived state.
 *
 * @returns The chat route element.
 */
export default function ChatRoute()
{
  const messages = useConversationStore((s) => s.messages);
  const appendDelta = useConversationStore((s) => s.appendDelta);
  const appendToolUseBlock = useConversationStore((s) => s.appendToolUseBlock);
  const appendToolResultBlock = useConversationStore((s) => s.appendToolResultBlock);
  const completeTurn = useConversationStore((s) => s.completeTurn);
  const appendErrorMessage = useConversationStore((s) => s.appendErrorMessage);
  const appendRequestBlock = useConversationStore((s) => s.appendRequestBlock);
  const appendAutoAllowedBlock = useConversationStore((s) => s.appendAutoAllowedBlock);
  const markRequestAnswered = useConversationStore((s) => s.markRequestAnswered);
  const endTurn = useConversationStore((s) => s.endTurn);
  const inFlight = useConversationStore((s) => s.inFlight);

  const bottomRef = useRef<HTMLDivElement>(null);
  const [sessionsCollapsed, toggleSessionsCollapsed] = useCollapsedColumn("sessions");

  // #region Effects

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
          endTurn();
          break;
        case "error":
          appendErrorMessage(m.message);
          endTurn();

          if (/no conversation found with session id/i.test(m.message))
          {
            void startNewSession().catch((err) => {
              console.error("[chat] auto-recover startNewSession failed:", err);
            });
            useConversationStore.getState().setCurrentSessionId(null);
          }
          break;
        case "permission-requested":
          appendRequestBlock(m.turnId, {
            type: "request",
            requestId: m.requestId,
            subtype: "permission",
            payload: {
              requestId:      m.requestId,
              turnId:         m.turnId,
              agentId:        m.agentId,
              toolName:       m.toolName,
              input:          m.input,
              blockedPath:    m.blockedPath,
              decisionReason: m.decisionReason,
            },
            state: "pending",
          });
          break;
        case "ask-user-requested":
          appendRequestBlock(m.turnId, {
            type:      "request",
            requestId: m.requestId,
            subtype:   "question",
            payload: {
              requestId: m.requestId,
              turnId:    m.turnId,
              agentId:   m.agentId,
              input:     m.input,
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
            markRequestAnswered(m.requestId, m.answer ?? undefined, m.outcome);
          }
          break;
        case "ready":
        case "assistant-text":
        case "permission-mode-changed":
        case "health-ok":
        case "health-failed":
        case "catalog-ready":
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
    endTurn,
  ]);

  // Pin scroll to bottom on new messages.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length]);

  // #endregion

  // #region Derived state

  const isEmpty = messages.length === 0;
  const streaming = useMemo(() => isTurnStreaming(messages), [messages]);
  const showWorkingStrip = inFlight || streaming;

  // #endregion

  // #region Handlers

  const handleCancel = () => {
    void cancelCurrentTurn().catch((err) => {
      console.error("[chat] cancel turn failed:", err);
    });
  };

  const handleLaunchpadPick = (prefill: string) => {
    void useConversationStore.getState().sendMessage(prefill);
  };

  const handlePermissionDecision = (block: Extract<Block, { type: "request" }>, outcome: "allow" | "allow-always" | "deny") => {
    markRequestAnswered(block.requestId, undefined, outcome);
    void respondToRequest(block.requestId, { kind: "permission", outcome });
  };

  const handleQuestionSubmit = (block: Extract<Block, { type: "request" }>, answer: AskUserQuestionOutput) => {
    markRequestAnswered(block.requestId, answer);
    void respondToRequest(block.requestId, { kind: "question", answer });
  };

  // #endregion

  return (
    <div className="flex flex-1 min-h-0">
      {/* Sessions */}
      {sessionsCollapsed ? (
        <aside className="w-8 shrink-0 border-r border-line bg-bg-0 flex flex-col items-center py-3">
          <button
            type="button"
            onClick={toggleSessionsCollapsed}
            title="Expand sessions"
            aria-label="Expand sessions"
            className="inline-flex items-center justify-center w-6 h-6 rounded-r-1 text-txt-4 hover:text-txt-1 hover:bg-bg-3 transition-colors duration-[120ms]"
          >
            <span style={{ fontSize: 11 }}>›</span>
          </button>
        </aside>
      ) : (
        <aside className="w-[200px] shrink-0 border-r border-line bg-bg-0 px-3 py-4 flex flex-col min-h-0">
          <SessionList onCollapse={toggleSessionsCollapsed} />
        </aside>
      )}

      {/* Main column */}
      <div className="flex flex-col flex-1 min-w-0">
        {isEmpty ? (
          <ChatLaunchpad
            onPickWorkflow={handleLaunchpadPick}
            onPickAgent={handleLaunchpadPick}
          />
        ) : (
          <div className="flex-1 overflow-auto bg-bg-1 px-8 py-7 flex flex-col gap-[22px]">
            {messages.map((m) => (
              <MessageView
                key={m.id}
                message={m}
                onPermissionDecision={handlePermissionDecision}
                onQuestionSubmit={handleQuestionSubmit}
              />
            ))}
            <div ref={bottomRef} />
          </div>
        )}

        {showWorkingStrip && (
          <WorkingStrip
            message="Claude is working…"
            onCancel={handleCancel}
          />
        )}

        <ChatInput />
      </div>
    </div>
  );
}

// #region MessageView

interface MessageViewProps
{
  message: Message;
  onPermissionDecision: (block: Extract<Block, { type: "request" }>, outcome: "allow" | "allow-always" | "deny") => void;
  onQuestionSubmit:    (block: Extract<Block, { type: "request" }>, answer: AskUserQuestionOutput) => void;
}

const MessageView = ({ message, onPermissionDecision, onQuestionSubmit }: MessageViewProps) => {
  if (message.role === "user")
  {
    return (
      <div className="flex justify-end gap-2.5 pl-20">
        <div className="rounded-r-3 border border-line bg-bg-3 px-3.5 py-2.5 text-[13.5px] leading-relaxed text-txt-1 max-w-full">
          {message.blocks.map((b, i) => b.type === "text" ? <span key={i} className="whitespace-pre-wrap">{b.text}</span> : null)}
        </div>
        <Avatar variant="user" initials="RB" size={28} />
      </div>
    );
  }

  if (message.role === "system")
  {
    return (
      <div className="text-[11.5px] text-txt-4 italic px-2 py-1">
        {message.blocks.map((b) => b.type === "text" ? b.text : "").join("")}
      </div>
    );
  }

  const rendered = pairToolBlocks(message.blocks);

  return (
    <div className="flex gap-3 pr-16">
      <Avatar variant="claude" initials="CC" size={28} />
      <div className="flex-1 min-w-0 flex flex-col gap-2.5">
        {rendered.map((r, i) => {
          if (r.kind === "text")
          {
            return (
              <div key={i} className="text-[14px] leading-relaxed text-txt-1 whitespace-pre-wrap">
                {r.text}
              </div>
            );
          }

          if (r.kind === "tool")
          {
            return (
              <ToolCallNarrativeBlock
                key={r.toolUseId}
                name={r.name}
                status={r.status}
                input={r.input}
                output={r.output}
                isError={r.isError}
              />
            );
          }

          // request
          const block = r.block;

          if (block.subtype === "permission")
          {
            const payload = block.payload as PermissionRequestedPayload;
            return (
              <PermissionRequestCard
                key={block.requestId}
                payload={payload}
                state={block.state}
                outcome={block.outcome === "auto-allowed" ? undefined : block.outcome}
                onDecision={(outcome) => onPermissionDecision(block, outcome)}
              />
            );
          }

          const payload = block.payload as AskUserRequestedPayload;
          return (
            <QuestionCard
              key={block.requestId}
              payload={payload}
              state={block.state}
              onSubmit={(answer) => onQuestionSubmit(block, answer)}
              previousAnswer={block.answer}
            />
          );
        })}
      </div>
    </div>
  );
};

// #endregion