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

/**
 * Shape of the connection-state store consumed across the chat UI.
 *
 * Tracks the live status of both the Unity-side MCP server and the Node-side
 * Agent SDK, and exposes setters so transport layers can push updates as
 * connections are established, lost, or retried.
 */
interface ConnectionState 
{
  unityStatus: ConnectionStatus;
  nodeSdkStatus: NodeSdkStatus;
  setUnityStatus: (status: ConnectionStatus) => void;
  setNodeSdkStatus: (status: NodeSdkStatus) => void;
}

// #endregion

// #region Store

export const useConnectionStore = create<ConnectionState>((set) => ({
  unityStatus: "disconnected",
  nodeSdkStatus: "crashed",
  setUnityStatus: (status) => set({ unityStatus: status }),
  setNodeSdkStatus: (status) => set({ nodeSdkStatus: status }),
}));

// #endregion