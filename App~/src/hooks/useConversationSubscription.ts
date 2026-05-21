/**
 * Subscribes to the `agent-message` Tauri event and routes every
 * conversation-related variant (`text-delta`, `tool-use`, `tool-result`,
 * `assistant-turn-complete`, `error`, `permission-requested`,
 * `ask-user-requested`, `request-resolved`) into `conversationStore`.
 *
 * Mounted at the root (`App.tsx`) so the subscription survives route
 * navigation — previously this listener lived inside `ChatRoute`'s
 * `useEffect` and was torn down whenever the user opened Plans, Rules,
 * Library or Settings, dropping every event emitted in the meantime
 *
 * Uses `useConversationStore.getState()` inside the callback so the
 * effect has zero deps and the listener is registered exactly once
 * for the lifetime of the app.
 */

import { useEffect } from "react";
import { startNewSession } from "../ipc/commands";
import { onAgentMessage } from "../ipc/events";
import { useConversationStore } from "../stores/conversationStore";

/**
 * Subscribes the conversation store to live agent messages from the host
 * process.
 *
 * Registers a listener via `onAgentMessage` and dispatches each payload to
 * the appropriate `useConversationStore` mutator — appending text deltas,
 * tool-use/tool-result blocks, permission and question requests, error
 * messages, and turn-complete markers. Auto-recovers from "no conversation
 * found" errors by starting a fresh session and clearing the cached session
 * id. Cleans up the subscription on unmount and guards against late-resolved
 * listeners using a `cancelled` flag.
 *
 * Intended to be mounted once at the top of the chat panel.
 *
 * @returns void
 */
export function useConversationSubscription(): void
{
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onAgentMessage((payload) => {
      if (cancelled)
      {
        return;
      }

      const store = useConversationStore.getState();
      const m = payload.message;
      switch (m.type)
      {
        case "text-delta":
          store.appendDelta(m.turnId, m.text);
          break;
        case "tool-use":
          store.appendToolUseBlock(m.turnId, m.toolUseId, m.name, m.input);
          break;
        case "tool-result":
          store.appendToolResultBlock(m.turnId, m.toolUseId, m.content, m.isError);
          break;
        case "assistant-turn-complete":
          store.completeTurn(m.turnId);
          store.endTurn();
          break;
        case "error":
          store.appendErrorMessage(m.message);
          store.endTurn();

          if (/no conversation found with session id/i.test(m.message))
          {
            void startNewSession().catch((err) => {
              console.error("[conversation] auto-recover startNewSession failed:", err);
            });
            store.setCurrentSessionId(null);
          }
          break;
        case "permission-requested":
          store.appendRequestBlock(m.turnId, {
            type: "request",
            requestId: m.requestId,
            subtype: "permission",
            payload: {
              requestId:      m.requestId,
              turnId:         m.turnId,
              agentId:        m.agentId,
              toolName:       m.toolName,
              input:          m.input,
              blockedPath:    m.blockedPath,
              decisionReason: m.decisionReason,
            },
            state: "pending",
          });
          break;
        case "ask-user-requested":
          store.appendRequestBlock(m.turnId, {
            type:      "request",
            requestId: m.requestId,
            subtype:   "question",
            payload: {
              requestId: m.requestId,
              turnId:    m.turnId,
              agentId:   m.agentId,
              input:     m.input,
            },
            state: "pending",
          });
          break;
        case "request-resolved":
          if (m.outcome === "auto-allowed")
          {
            if (m.turnId !== null && m.toolName !== null)
            {
              store.appendAutoAllowedBlock(m.turnId, m.requestId, m.toolName);
            }
          }
          else
          {
            store.markRequestAnswered(m.requestId, m.answer ?? undefined, m.outcome);
          }
          break;
        case "system-message":
          store.appendSystemMessageBlock(m.turnId, m.text, m.source);
          break;
        case "subagent-status":
          store.upsertSubagentStatus(
            m.turnId,
            m.taskId,
            m.toolUseId,
            m.phase,
            m.description,
            m.summary,
            m.usage,
            m.lastToolName,
          );
          break;
        case "usage-update":
          store.setTurnUsage(m.model, m.usage);
          break;
        case "plan-summary":
          store.appendPlanSummaryBlock(m.turnId, m.requestId, m.plan);
          break;
        case "ready":
        case "assistant-text":
        case "permission-mode-changed":
        case "health-ok":
        case "health-failed":
        case "catalog-ready":
          break;
      }
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[conversation] failed to subscribe to agent-message:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);
}