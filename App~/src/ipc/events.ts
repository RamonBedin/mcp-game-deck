/**
 * Typed wrappers around Tauri's `listen()` — one function per event emitted
 * by Rust (see `src-tauri/src/events.rs`). Each wrapper returns a
 * `Promise<UnlistenFn>` — call the resolved fn during cleanup to unsubscribe.
 *
 * Don't import `listen` directly in callers: going through these wrappers
 * preserves the typed payloads from `./types`.
 */

import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import type {
  AskUserRequestedPayload,
  Message,
  MessageStreamChunkPayload,
  MessageStreamCompletePayload,
  NodeLogPayload,
  NodeSdkStatusChangedPayload,
  PermissionRequestedPayload,
  UnityStatusChangedPayload,
} from "./types";

/**
 * Builds a typed `listen` wrapper for a given event name.
 *
 * @param eventName - Tauri event name (kebab-case, must match the Rust emitter).
 * @returns A subscription function: pass it a handler, get back a `Promise<UnlistenFn>`.
 */
const wrap =
  <T>(eventName: string) =>
  (handler: (payload: T) => void): Promise<UnlistenFn> =>
    listen<T>(eventName, (event) => handler(event.payload));

// #region Status events

/** Subscribes to `unity-status-changed`. */
export const onUnityStatusChanged =
  wrap<UnityStatusChangedPayload>("unity-status-changed");

/** Subscribes to `node-sdk-status-changed`. */
export const onNodeSdkStatusChanged =
  wrap<NodeSdkStatusChangedPayload>("node-sdk-status-changed");

// #endregion

// #region Message events

/** Subscribes to `message-received` (full, non-streamed messages). */
export const onMessageReceived = wrap<Message>("message-received");

/** Subscribes to `message-stream-chunk` (incremental streaming). */
export const onMessageStreamChunk =
  wrap<MessageStreamChunkPayload>("message-stream-chunk");

/** Subscribes to `message-stream-complete` (end-of-stream marker). */
export const onMessageStreamComplete =
  wrap<MessageStreamCompletePayload>("message-stream-complete");

// #endregion

// #region Agent prompts

/** Subscribes to `ask-user-requested` (agent → user input prompt). */
export const onAskUserRequested =
  wrap<AskUserRequestedPayload>("ask-user-requested");

/** Subscribes to `permission-requested` (agent → user tool-call confirmation). */
export const onPermissionRequested =
  wrap<PermissionRequestedPayload>("permission-requested");

// #endregion

// #region Diagnostics

/** Group 3 stub diagnostic — Node SDK heartbeat / log lines forwarded to the DevTools console. */
export const onNodeLog = wrap<NodeLogPayload>("node-log");

// #endregion