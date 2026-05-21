/**
 * Zustand store for the active conversation.
 *
 * Owns the message list, current session id, and permission mode.
 * Messages are block-based (task 2.4) — assistant turns interleave
 * streamed text with tool-use / tool-result entries in display order.
 *
 * The optimistic `sendMessage` appends a user message locally before
 * forwarding the text to the supervisor; assistant replies arrive as
 * `text-delta` / `tool-use` / `tool-result` / `assistant-turn-complete`
 * events (consumed by `ChatRoute`'s `onAgentMessage` listener and
 * dispatched here via `appendDelta` / `appendToolUseBlock` /
 * `appendToolResultBlock` / `completeTurn`).
 */

import { create } from "zustand";
import { sendMessage as sendMessageCommand, trackRecentCommand } from "../ipc/commands";
import type { AskUserQuestionOutput, Block, Message, PermissionMode, PermissionRequestedPayload, SubagentPhase, SubagentUsage, SystemMessageSource, TurnUsage, } from "../ipc/types";

// #region State shape

/**
 * Shape of the conversation-state store that backs the chat panel.
 *
 * Mutators are streaming + block-shaped: text deltas append to the
 * trailing text block of a turn-keyed assistant message, tool blocks
 * are pushed in arrival order, completion is a marker, and errors
 * land as system entries.
 */
interface ConversationState
{
  messages: Message[];
  currentSessionId: string | null;
  permissionMode: PermissionMode;
  inFlight: boolean;
  turnUsage: TurnUsage | null;
  turnUsageModel: string | null;
  appendDelta: (turnId: string, text: string) => void;
  appendToolUseBlock: (turnId: string, toolUseId: string, name: string, input: unknown,) => void;
  appendToolResultBlock: (turnId: string, toolUseId: string, content: unknown, isError: boolean,) => void;
  completeTurn: (turnId: string) => void;
  appendErrorMessage: (text: string) => void;
  appendRequestBlock: (turnId: string, block: Extract<Block, { type: "request" }>,) => void;
  appendAutoAllowedBlock: (turnId: string, requestId: string, toolName: string,) => void;
  markRequestAnswered: (requestId: string, answer?: AskUserQuestionOutput, outcome?: "allow" | "allow-always" | "deny" | "auto-allowed",) => void;
  markAllPendingRequestsInterrupted: () => void;
  appendSystemMessageBlock: (turnId: string, text: string, source: SystemMessageSource) => void;
  upsertSubagentStatus: (turnId: string, taskId: string | null, toolUseId: string | null, phase: SubagentPhase, description: string, summary: string | null, usage: SubagentUsage | null, lastToolName: string | null) => void;
  appendPlanSummaryBlock: (turnId: string, requestId: string, plan: string) => void;
  setTurnUsage: (model: string | null, usage: TurnUsage) => void;
  clearMessages: () => void;
  loadHistory: (messages: Message[]) => void;
  setPermissionMode: (mode: PermissionMode) => void;
  setCurrentSessionId: (sessionId: string | null) => void;
  endTurn: () => void;
  sendMessage: (text: string, attachmentPaths?: string[]) => Promise<void>;
}

// #endregion

// #region Helpers

const makeLocalId = (prefix: string): string => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

const extractLeadingSlashCommand = (text: string): string | null => {
  const trimmed = text.trimStart();

  if (!trimmed.startsWith("/"))
  {
    return null;
  }

  const firstToken = trimmed.slice(1).split(/\s/, 1)[0];

  if (firstToken === undefined || firstToken.length === 0)
  {
    return null;
  }

  return firstToken;
};

const formatError = (err: unknown): string => {
  if (err instanceof Error)
  {
    return err.message;
  }

  if (typeof err === "string")
  {
    return err;
  }
  try
  {
    return JSON.stringify(err);
  }
  catch
  {
    return String(err);
  }
};

const pushBlockToTurn = (messages: Message[], turnId: string, block: Block,): Message[] => {
  const idx = messages.findIndex((m) => m.id === turnId);
  if (idx >= 0)
  {
    const next = [...messages];
    next[idx] = {
      ...next[idx],
      blocks: [...next[idx].blocks, block],
    };
    return next;
  }

  return [
    ...messages,
    {
      id: turnId,
      role: "assistant",
      timestamp: Date.now(),
      blocks: [block],
    },
  ];
};

const updateRequestBlock = (
  messages: Message[],
  requestId: string,
  updater: (
    block: Extract<Block, { type: "request" }>,
  ) => Extract<Block, { type: "request" }>,
): Message[] =>
  messages.map((msg) => ({
    ...msg,
    blocks: msg.blocks.map((b) =>
      b.type === "request" && b.requestId === requestId ? updater(b) : b,
    ),
  }));

// #endregion

// #region Store

export const useConversationStore = create<ConversationState>((set, get) => ({
  messages: [],
  currentSessionId: null,
  permissionMode: "default",
  inFlight: false,
  turnUsage: null,
  turnUsageModel: null,
  appendDelta: (turnId, text) =>
    set((state) => {
      const idx = state.messages.findIndex((m) => m.id === turnId);
      if (idx >= 0)
      {
        const msg = state.messages[idx];
        const lastBlock = msg.blocks[msg.blocks.length - 1];
        let newBlocks: Block[];

        if (lastBlock?.type === "text")
        {
          newBlocks = msg.blocks.slice(0, -1);
          newBlocks.push({ type: "text", text: lastBlock.text + text });
        }
        else
        {
          newBlocks = [...msg.blocks, { type: "text", text }];
        }

        const next = [...state.messages];
        next[idx] = { ...msg, blocks: newBlocks };
        return { messages: next };
      }

      const newMsg: Message = {
        id: turnId,
        role: "assistant",
        timestamp: Date.now(),
        blocks: [{ type: "text", text }],
      };
      return { messages: [...state.messages, newMsg] };
    }),
  appendToolUseBlock: (turnId, toolUseId, name, input) =>
    set((state) => ({
      messages: pushBlockToTurn(state.messages, turnId, {
        type: "tool-use",
        toolUseId,
        name,
        input,
      }),
    })),
  appendToolResultBlock: (turnId, toolUseId, content, isError) =>
    set((state) => ({
      messages: pushBlockToTurn(state.messages, turnId, {
        type: "tool-result",
        toolUseId,
        content,
        isError,
      }),
    })),
  completeTurn: (_turnId) => {
  },
  appendErrorMessage: (text) =>
    set((state) => ({
      messages: [
        ...state.messages,
        {
          id: makeLocalId("err"),
          role: "system",
          timestamp: Date.now(),
          blocks: [{ type: "text", text: `error: ${text}` }],
        },
      ],
    })),
  appendRequestBlock: (turnId, block) =>
    set((state) => ({
      messages: pushBlockToTurn(state.messages, turnId, block),
    })),
  appendAutoAllowedBlock: (turnId, requestId, toolName) =>
    set((state) => {
      const payload: PermissionRequestedPayload = {
        requestId,
        turnId,
        agentId: null,
        toolName,
        input: undefined,
        blockedPath: null,
        decisionReason: null,
      };
      return {
        messages: pushBlockToTurn(state.messages, turnId, {
          type: "request",
          requestId,
          subtype: "permission",
          payload,
          state: "auto-allowed",
        }),
      };
    }),
  markRequestAnswered: (requestId, answer, outcome) =>
    set((state) => ({
      messages: updateRequestBlock(state.messages, requestId, (block) => ({
        ...block,
        state: "answered",
        answer,
        outcome,
      })),
    })),
  markAllPendingRequestsInterrupted: () =>
    set((state) => ({
      inFlight: false,
      messages: state.messages.map((msg) => ({
        ...msg,
        blocks: msg.blocks.map((b) =>
          b.type === "request" && b.state === "pending"
            ? { ...b, state: "interrupted" as const }
            : b,
        ),
      })),
    })),
  appendSystemMessageBlock: (turnId, text, source) =>
    set((state) => ({
      messages: pushBlockToTurn(state.messages, turnId, {
        type: "system-message",
        text,
        source,
      }),
    })),
  upsertSubagentStatus: (turnId, taskId, toolUseId, phase, description, summary, usage, lastToolName) =>
    set((state) => {
      const idx = state.messages.findIndex((m) => m.id === turnId);
      const incoming: Extract<Block, { type: "subagent-status" }> = {
        type: "subagent-status",
        toolUseId,
        taskId,
        phase,
        description,
        summary,
        usage,
        lastToolName,
      };

      if (idx === -1)
      {
        return {
          messages: [
            ...state.messages,
            { id: turnId, role: "assistant", timestamp: Date.now(), blocks: [incoming] },
          ],
        };
      }

      const msg = state.messages[idx];
      const existingIdx = taskId === null ? -1 : msg.blocks.findIndex((b) => b.type === "subagent-status" && b.taskId === taskId,);

      let newBlocks: Block[];

      if (existingIdx >= 0)
      {
        newBlocks = [...msg.blocks];
        const prev = newBlocks[existingIdx] as Extract<Block, { type: "subagent-status" }>;
        newBlocks[existingIdx] = {
          ...prev,
          phase,
          description: description.length > 0 ? description : prev.description,
          summary: summary ?? prev.summary,
          usage: usage ?? prev.usage,
          lastToolName: lastToolName ?? prev.lastToolName,
          toolUseId: toolUseId ?? prev.toolUseId,
        };
      }
      else
      {
        newBlocks = [...msg.blocks, incoming];
      }

      const next = [...state.messages];
      next[idx] = { ...msg, blocks: newBlocks };
      return { messages: next };
    }),
  appendPlanSummaryBlock: (turnId, requestId, plan) =>
    set((state) => ({
      messages: pushBlockToTurn(state.messages, turnId, {
        type: "request",
        requestId,
        subtype: "plan-summary",
        payload: { requestId, turnId, plan },
        state: "pending",
      }),
    })),
  setTurnUsage: (model, usage) => set({ turnUsageModel: model, turnUsage: usage }),
  clearMessages: () => set({ messages: [], inFlight: false, turnUsage: null, turnUsageModel: null }),
  loadHistory: (messages) => set({ messages, inFlight: false }),
  setPermissionMode: (mode) => set({ permissionMode: mode }),
  setCurrentSessionId: (sessionId) => set({ currentSessionId: sessionId }),
  endTurn: () => set({ inFlight: false }),
  sendMessage: async (text, attachmentPaths = []) => {
    const trimmed = text.trim();

    if (!trimmed && attachmentPaths.length === 0)
    {
      return;
    }

    if (get().inFlight)
    {
      console.debug("[conversation] sendMessage blocked — turn already in flight");
      return;
    }

    const userMsg: Message = {
      id: makeLocalId("user"),
      role: "user",
      timestamp: Date.now(),
      blocks: [{ type: "text", text: trimmed }],
    };
    set((state) => ({ messages: [...state.messages, userMsg], inFlight: true }));

    try
    {
      await sendMessageCommand(trimmed, attachmentPaths);

      const slashCmd = extractLeadingSlashCommand(trimmed);

      if (slashCmd !== null)
      {
        void trackRecentCommand(slashCmd).catch((err) => {
          console.error("[conversation] trackRecentCommand failed:", err);
        });
      }
    }
    catch (err)
    {
      const errorMsg: Message = {
        id: makeLocalId("err"),
        role: "system",
        timestamp: Date.now(),
        blocks: [{ type: "text", text: `error: ${formatError(err)}` }],
      };
      set((state) => ({ messages: [...state.messages, errorMsg], inFlight: false }));
    }
  },
}));

// #endregion