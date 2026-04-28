/**
 * Zustand store for live connection status.
 *
 * Mirrors the two state machines on the Rust side: the Unity TCP client
 * (`ConnectionStatus`) and the Node Agent SDK supervisor (`NodeSdkStatus`).
 * Updated by `App.tsx` from both the 2s polling backstop and the
 * event-driven fast path.
 */

import { create } from "zustand";
import type { ConnectionStatus, NodeSdkStatus } from "../ipc/types";

// #region State shape

interface ConnectionState {
  unityStatus: ConnectionStatus;
  nodeSdkStatus: NodeSdkStatus;
  setUnityStatus: (status: ConnectionStatus) => void;
  setNodeSdkStatus: (status: NodeSdkStatus) => void;
}

// #endregion

// #region Store

/** Hook for the connection-status store. */
export const useConnectionStore = create<ConnectionState>((set) => ({
  unityStatus: "disconnected",
  nodeSdkStatus: "crashed",
  setUnityStatus: (status) => set({ unityStatus: status }),
  setNodeSdkStatus: (status) => set({ nodeSdkStatus: status }),
}));

// #endregion