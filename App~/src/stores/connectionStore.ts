/**
 * Zustand store for live connection status.
 *
 * Mirrors the two state machines on the Rust side: the Unity TCP client
 * (`ConnectionStatus`) and the Claude Code supervisor (`SupervisorStatus`).
 * Updated by `App.tsx` from both the 2s polling backstop and the
 * event-driven fast path.
 */

import { create } from "zustand";
import type { ConnectionStatus, SupervisorStatus } from "../ipc/types";

// #region State shape

/**
 * Shape of the connection-state store consumed across the chat UI.
 *
 * Tracks the live status of both the Unity-side MCP server and the Claude
 * Code supervisor, and exposes setters so transport layers can push updates
 * as connections are established, lost, or retried.
 */
interface ConnectionState
{
  unityStatus: ConnectionStatus;
  supervisorStatus: SupervisorStatus;
  setUnityStatus: (status: ConnectionStatus) => void;
  setSupervisorStatus: (status: SupervisorStatus) => void;
}

// #endregion

// #region Store

export const useConnectionStore = create<ConnectionState>((set) => ({
  unityStatus: "disconnected",
  supervisorStatus: "idle",
  setUnityStatus: (status) => set({ unityStatus: status }),
  setSupervisorStatus: (status) => set({ supervisorStatus: status }),
}));

// #endregion