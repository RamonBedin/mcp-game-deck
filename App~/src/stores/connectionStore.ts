import { create } from "zustand";
import type { ConnectionStatus } from "../ipc/types";

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