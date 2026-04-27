import { create } from "zustand";
import { sendMessage as sendMessageCommand } from "../ipc/commands";
import type { Message, PermissionMode } from "../ipc/types";

interface ConversationState {
  messages: Message[];
  currentSessionId: string | null;
  permissionMode: PermissionMode;
  appendMessage: (message: Message) => void;
  clearMessages: () => void;
  setPermissionMode: (mode: PermissionMode) => void;
  setCurrentSessionId: (sessionId: string | null) => void;
  /// Optimistically appends the user message, then forwards the text to the
  /// Node SDK via the `send_message` Tauri command. Assistant replies arrive
  /// later via the `message-received` event (subscribed in ChatRoute).
  /// Errors are appended as a `system`-role message so they surface in the UI.
  sendMessage: (text: string) => Promise<void>;
}

const makeLocalId = (prefix: string): string =>
  `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

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
    if (!trimmed) return;

    const userMsg: Message = {
      id: makeLocalId("user"),
      role: "user",
      content: trimmed,
      timestamp: Date.now(),
    };
    set((state) => ({ messages: [...state.messages, userMsg] }));

    try {
      await sendMessageCommand(trimmed);
    } catch (err) {
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