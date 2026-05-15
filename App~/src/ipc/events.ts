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
  AgentMessagePayload,
  AskUserRequestedPayload,
  ClaudeVersionOutOfRangePayload,
  Message,
  MessageStreamChunkPayload,
  MessageStreamCompletePayload,
  PermissionModeChangedPayload,
  PermissionRequestedPayload,
  PlansChangedPayload,
  ProjectFilesChangedPayload,
  RouteRequestedPayload,
  RulesChangedPayload,
  SdkInstallFailedPayload,
  SdkInstallProgressPayload,
  SupervisorStatusChangedPayload,
  UnityStatusChangedPayload,
} from "./types";

const wrap =
  <T>(eventName: string) =>
  (handler: (payload: T) => void): Promise<UnlistenFn> =>
    listen<T>(eventName, (event) => handler(event.payload));

// #region Status events

export const onUnityStatusChanged = wrap<UnityStatusChangedPayload>("unity-status-changed");

export const onSupervisorStatusChanged = wrap<SupervisorStatusChangedPayload>("supervisor-status-changed");

export const onClaudeVersionOutOfRange = wrap<ClaudeVersionOutOfRangePayload>("claude-version-out-of-range");

// #endregion

// #region Message events

export const onMessageReceived = wrap<Message>("message-received");

export const onMessageStreamChunk = wrap<MessageStreamChunkPayload>("message-stream-chunk");

export const onMessageStreamComplete = wrap<MessageStreamCompletePayload>("message-stream-complete");

// #endregion

// #region Agent prompts

export const onAskUserRequested = wrap<AskUserRequestedPayload>("ask-user-requested");

export const onPermissionRequested = wrap<PermissionRequestedPayload>("permission-requested");

// #endregion

// #region Routing

export const onRouteRequested = wrap<RouteRequestedPayload>("route-requested");

// #endregion

// #region Install events

export const onSdkInstallProgress = wrap<SdkInstallProgressPayload>("sdk-install-progress");

export const onSdkInstallCompleted = (
  handler: () => void,
): Promise<UnlistenFn> =>
  listen<null>("sdk-install-completed", () => handler());

export const onSdkInstallFailed = wrap<SdkInstallFailedPayload>("sdk-install-failed");

// #endregion

// #region Agent stream

export const onAgentMessage = wrap<AgentMessagePayload>("agent-message");

export const onPermissionModeChanged = wrap<PermissionModeChangedPayload>("permission-mode-changed");

// #endregion

// #region Plans

export const onPlansChanged = wrap<PlansChangedPayload>("plans-changed");

// #endregion

// #region Rules

export const onRulesChanged = wrap<RulesChangedPayload>("rules-changed");

// #endregion

// #region Files

export const onProjectFilesChanged = wrap<ProjectFilesChangedPayload>("project-files-changed");

// #endregion