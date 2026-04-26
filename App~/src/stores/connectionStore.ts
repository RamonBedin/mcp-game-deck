import { create } from "zustand";

// Types here are placeholders for the inline shape used by stub UI in tasks 1.4–2.0.
// In task 2.1 they move into src/ipc/types.ts as the canonical Rust↔TS contract.
export type ConnectionStatus = "connected" | "busy" | "disconnected";

interface ConnectionState {
  unityStatus: ConnectionStatus;
  nodeSdkStatus: ConnectionStatus;
  setUnityStatus: (status: ConnectionStatus) => void;
  setNodeSdkStatus: (status: ConnectionStatus) => void;
}

export const useConnectionStore = create<ConnectionState>((set) => ({
  unityStatus: "disconnected",
  nodeSdkStatus: "disconnected",
  setUnityStatus: (status) => set({ unityStatus: status }),
  setNodeSdkStatus: (status) => set({ nodeSdkStatus: status }),
}));