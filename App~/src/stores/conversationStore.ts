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
import { sendMessage as sendMessageCommand } from "../ipc/commands";
import type { Block, Message, PermissionMode } from "../ipc/types";

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
  appendDelta: (turnId: string, text: string) => void;
  appendToolUseBlock: (turnId: string, toolUseId: string, name: string, input: unknown,) => void;
  appendToolResultBlock: (turnId: string, toolUseId: string, content: unknown, isError: boolean,) => void;
  completeTurn: (turnId: string) => void;
  appendErrorMessage: (text: string) => void;
  clearMessages: () => void;
  setPermissionMode: (mode: PermissionMode) => void;
  setCurrentSessionId: (sessionId: string | null) => void;
  sendMessage: (text: string) => Promise<void>;
}

// #endregion

// #region Helpers

const makeLocalId = (prefix: string): string => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

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

// #endregion

// #region Store

export const useConversationStore = create<ConversationState>((set) => ({
  messages: [],
  currentSessionId: null,
  permissionMode: "ask",
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
  clearMessages: () => set({ messages: [] }),
  setPermissionMode: (mode) => set({ permissionMode: mode }),
  setCurrentSessionId: (sessionId) => set({ currentSessionId: sessionId }),
  sendMessage: async (text) => {
    const trimmed = text.trim();

    if (!trimmed)
    {
      return;
    }

    const userMsg: Message = {
      id: makeLocalId("user"),
      role: "user",
      timestamp: Date.now(),
      blocks: [{ type: "text", text: trimmed }],
    };
    set((state) => ({ messages: [...state.messages, userMsg] }));

    try
    {
      await sendMessageCommand(trimmed);
    }
    catch (err)
    {
      const errorMsg: Message = {
        id: makeLocalId("err"),
        role: "system",
        timestamp: Date.now(),
        blocks: [{ type: "text", text: `error: ${formatError(err)}` }],
      };
      set((state) => ({ messages: [...state.messages, errorMsg] }));
    }
  },
}));

// #endregion