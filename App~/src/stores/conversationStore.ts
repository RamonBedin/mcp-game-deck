/**
 * Zustand store for the active conversation.
 *
 * Owns the message list, current session id, and permission mode. Exposes
 * an optimistic `sendMessage` that appends the user message locally before
 * forwarding the text to the Node SDK; assistant replies arrive later via
 * the `message-received` event (subscribed in `ChatRoute`).
 */

import { create } from "zustand";
import { sendMessage as sendMessageCommand } from "../ipc/commands";
import type { Message, PermissionMode } from "../ipc/types";

// #region State shape

/**
 * Shape of the conversation-state store that backs the chat panel.
 *
 * Owns the full message history for the active session, the current session
 * identifier, and the user-selected permission mode. Exposes mutators for
 * appending messages, clearing history, and dispatching new prompts to the
 * agent.
 */
interface ConversationState
{
  messages: Message[];
  currentSessionId: string | null;
  permissionMode: PermissionMode;
  appendMessage: (message: Message) => void;
  clearMessages: () => void;
  setPermissionMode: (mode: PermissionMode) => void;
  setCurrentSessionId: (sessionId: string | null) => void;
  sendMessage: (text: string) => Promise<void>;
}

// #endregion

// #region Helpers

const makeLocalId = (prefix: string): string => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

// #endregion

// #region Store

export const useConversationStore = create<ConversationState>((set) => ({
  messages: [],
  currentSessionId: null,
  permissionMode: "ask",
  appendMessage: (message) =>
    set((state) => ({ messages: [...state.messages, message] })),
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
        content: `error: ${String(err)}`,
        timestamp: Date.now(),
      };
      set((state) => ({ messages: [...state.messages, errorMsg] }));
    }
  },
}));

// #endregion