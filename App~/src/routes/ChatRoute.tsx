/**
 * Renders the conversation with the new visual atoms:
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
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import ChatInput from "../components/ChatInput";
import ChatLaunchpad from "../components/chat/ChatLaunchpad";
import SubagentStatusPanel from "../components/chat/SubagentStatusPanel";
import SystemMessageBlock from "../components/chat/SystemMessageBlock";
import ToolCallNarrativeBlock, { type ToolCallStatus } from "../components/chat/ToolCallNarrativeBlock";
import WorkingStrip from "../components/chat/WorkingStrip";
import { markdownRenderers } from "../components/requests/markdown-renderers";
import PermissionRequestCard from "../components/requests/PermissionRequestCard";
import PlanSummaryCard from "../components/requests/PlanSummaryCard";
import QuestionCard from "../components/requests/QuestionCard";
import SessionList from "../components/SessionList";
import Avatar from "../components/atoms/Avatar";
import { useCollapsedColumn } from "../hooks/useCollapsedColumn";
import { useUserInitials } from "../hooks/useUserInitials";
import { cancelCurrentTurn, respondToRequest } from "../ipc/commands";
import type { AskUserQuestionOutput, AskUserRequestedPayload, Block, Message, PermissionRequestedPayload, PlanSummaryPayload, SubagentPhase, SubagentUsage, SystemMessageSource,} from "../ipc/types";
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
  | { kind: "request"; block: Extract<Block, { type: "request" }> }
  | { kind: "system-message"; text: string; source: SystemMessageSource }
  | { kind: "subagent-status"; phase: SubagentPhase; description: string; summary: string | null; usage: SubagentUsage | null; lastToolName: string | null };

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
      continue;
    }

    if (b.type === "system-message")
    {
      rendered.push({ kind: "system-message", text: b.text, source: b.source });
      continue;
    }

    if (b.type === "subagent-status")
    {
      rendered.push({
        kind: "subagent-status",
        phase: b.phase,
        description: b.description,
        summary: b.summary,
        usage: b.usage,
        lastToolName: b.lastToolName,
      });
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
  const markRequestAnswered = useConversationStore((s) => s.markRequestAnswered);
  const inFlight = useConversationStore((s) => s.inFlight);

  const bottomRef = useRef<HTMLDivElement>(null);
  const [sessionsCollapsed, toggleSessionsCollapsed] = useCollapsedColumn("sessions");
  const userInitials = useUserInitials();

  // #region Effects

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
                userInitials={userInitials}
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
  userInitials: string;
  onPermissionDecision: (block: Extract<Block, { type: "request" }>, outcome: "allow" | "allow-always" | "deny") => void;
  onQuestionSubmit:    (block: Extract<Block, { type: "request" }>, answer: AskUserQuestionOutput) => void;
}

const MessageView = ({ message, userInitials, onPermissionDecision, onQuestionSubmit }: MessageViewProps) => {
  if (message.role === "user")
  {
    return (
      <div className="flex justify-end gap-2.5 pl-20">
        <div className="rounded-r-3 border border-line bg-bg-3 px-3.5 py-2.5 text-[13.5px] leading-relaxed text-txt-1 max-w-full">
          {message.blocks.map((b, i) => b.type === "text" ? <span key={i} className="whitespace-pre-wrap">{b.text}</span> : null)}
        </div>
        <Avatar variant="user" initials={userInitials} size={28} />
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
              <div key={i} className="text-[14px] leading-relaxed text-txt-1">
                <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownRenderers}>
                  {r.text}
                </ReactMarkdown>
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

          if (r.kind === "system-message")
          {
            return (
              <SystemMessageBlock
                key={i}
                text={r.text}
                source={r.source}
              />
            );
          }

          if (r.kind === "subagent-status")
          {
            return (
              <SubagentStatusPanel
                key={i}
                phase={r.phase}
                description={r.description}
                summary={r.summary}
                usage={r.usage}
                lastToolName={r.lastToolName}
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

          if (block.subtype === "plan-summary")
          {
            const payload = block.payload as PlanSummaryPayload;
            return (
              <PlanSummaryCard
                key={block.requestId}
                payload={payload}
                state={block.state}
                outcome={
                  block.outcome === "allow" || block.outcome === "deny"
                    ? block.outcome
                    : undefined
                }
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