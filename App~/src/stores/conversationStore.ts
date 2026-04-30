/**
 * Zustand store for the active conversation.
 *
 * Owns the message list, current session id, and permission mode.
 * Exposes an optimistic `sendMessage` that appends the user message
 * locally before forwarding the text to the supervisor; assistant
 * replies arrive as streamed `text-delta` events (consumed by
 * `ChatRoute`'s `onAgentMessage` listener and dispatched here via
 * `appendDelta` / `completeTurn`).
 */

import { create } from "zustand";
import { sendMessage as sendMessageCommand } from "../ipc/commands";
import type { Message, PermissionMode } from "../ipc/types";

// #region State shape

/**
 * Shape of the conversation-state store that backs the chat panel.
 *
 * Owns the full message history for the active session, the current
 * session identifier, and the user-selected permission mode. Mutators
 * are streaming-shaped: deltas append to a turn-keyed assistant
 * message, completion is a marker, and errors land as system entries.
 */
interface ConversationState
{
  messages: Message[];
  currentSessionId: string | null;
  permissionMode: PermissionMode;
  appendDelta: (turnId: string, text: string) => void;
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
        const next = [...state.messages];
        next[idx] = {
          ...next[idx],
          content: next[idx].content + text,
        };
        return { messages: next };
      }

      const newMsg: Message = {
        id: turnId,
        role: "assistant",
        content: text,
        timestamp: Date.now(),
      };
      return { messages: [...state.messages, newMsg] };
    }),
  completeTurn: (_turnId) => {
  },
  appendErrorMessage: (text) =>
    set((state) => ({
      messages: [
        ...state.messages,
        {
          id: makeLocalId("err"),
          role: "system",
          content: `error: ${text}`,
          timestamp: Date.now(),
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
      content: trimmed,
      timestamp: Date.now(),
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
        content: `error: ${formatError(err)}`,
        timestamp: Date.now(),
      };
      set((state) => ({ messages: [...state.messages, errorMsg] }));
    }
  },
}));

// #endregion