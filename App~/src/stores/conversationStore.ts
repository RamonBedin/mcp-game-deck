import { create } from "zustand";

export type MessageRole = "user" | "assistant" | "system";
export type PermissionMode = "auto" | "ask" | "plan";

export interface Message {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: number;
  agent?: string;
}

interface ConversationState {
  messages: Message[];
  currentSessionId: string | null;
  permissionMode: PermissionMode;
  appendMessage: (message: Message) => void;
  clearMessages: () => void;
  setPermissionMode: (mode: PermissionMode) => void;
  setCurrentSessionId: (sessionId: string | null) => void;
}

export const useConversationStore = create<ConversationState>((set) => ({
  messages: [],
  currentSessionId: null,
  permissionMode: "ask",
  appendMessage: (message) =>
    set((state) => ({ messages: [...state.messages, message] })),
  clearMessages: () => set({ messages: [] }),
  setPermissionMode: (mode) => set({ permissionMode: mode }),
  setCurrentSessionId: (sessionId) => set({ currentSessionId: sessionId }),
}));