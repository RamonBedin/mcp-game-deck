import { create } from "zustand";
import type { ConnectionStatus, NodeSdkStatus } from "../ipc/types";

interface ConnectionState {
  unityStatus: ConnectionStatus;
  nodeSdkStatus: NodeSdkStatus;
  setUnityStatus: (status: ConnectionStatus) => void;
  setNodeSdkStatus: (status: NodeSdkStatus) => void;
}

export const useConnectionStore = create<ConnectionState>((set) => ({
  unityStatus: "disconnected",
  // Matches the supervisor's initial state on the Rust side until the first
  // `setup` spawn transitions to Starting → Running.
  nodeSdkStatus: "crashed",
  setUnityStatus: (status) => set({ unityStatus: status }),
  setNodeSdkStatus: (status) => set({ nodeSdkStatus: status }),
}));