// Typed wrappers around Tauri's `listen()` — one function per event emitted
// by Rust (see src-tauri/src/events.rs). Each wrapper returns a
// `Promise<UnlistenFn>` — call the resolved fn during cleanup to unsubscribe.
//
// Don't import `listen` directly in callers: going through these wrappers
// preserves the typed payloads from ./types.

import { listen, type UnlistenFn } from "@tauri-apps/api/event";
import type {
  AskUserRequestedPayload,
  Message,
  MessageStreamChunkPayload,
  MessageStreamCompletePayload,
  NodeSdkStatusChangedPayload,
  PermissionRequestedPayload,
  UnityStatusChangedPayload,
} from "./types";

const wrap =
  <T>(eventName: string) =>
  (handler: (payload: T) => void): Promise<UnlistenFn> =>
    listen<T>(eventName, (event) => handler(event.payload));

export const onUnityStatusChanged =
  wrap<UnityStatusChangedPayload>("unity-status-changed");

export const onNodeSdkStatusChanged =
  wrap<NodeSdkStatusChangedPayload>("node-sdk-status-changed");

export const onMessageReceived = wrap<Message>("message-received");

export const onMessageStreamChunk =
  wrap<MessageStreamChunkPayload>("message-stream-chunk");

export const onMessageStreamComplete =
  wrap<MessageStreamCompletePayload>("message-stream-complete");

export const onAskUserRequested =
  wrap<AskUserRequestedPayload>("ask-user-requested");

export const onPermissionRequested =
  wrap<PermissionRequestedPayload>("permission-requested");