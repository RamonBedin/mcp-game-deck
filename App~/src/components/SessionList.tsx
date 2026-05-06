/**
 * Sidebar that lists every Claude Code session for the active Unity
 * project, plus a "New Session" button at the top.
 *
 * Sessions live under `<home>/.claude/projects/<encoded-cwd>/*.jsonl`
 * (Claude Code's storage is the source of truth).
 * Clicking a row pre-loads its history into the conversation store
 * and pins the supervisor to resume that session on the next prompt;
 * the New Session button clears both pieces of state.
 */

import { useCallback, useEffect, useState } from "react";
import { getSessionMessages, getSessions, resumeSession, startNewSession, } from "../ipc/commands";
import type { SessionSummary } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

// #region Helpers

const formatRelative = (millis: number): string => {
  if (millis <= 0)
  {
    return "—";
  }

  const diff = Date.now() - millis;
  const seconds = Math.floor(diff / 1000);

  if (seconds < 60)
  {
    return "just now";
  }

  const minutes = Math.floor(seconds / 60);

  if (minutes < 60)
  {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);

  if (hours < 24)
  {
    return `${hours}h ago`;
  }

  const days = Math.floor(hours / 24);

  if (days < 30)
  {
    return `${days}d ago`;
  }

  const months = Math.floor(days / 30);
  return `${months}mo ago`;
};

// #endregion

/**
 * Sidebar component. Fetches session list on mount + when the active
 * session changes (so a new session created via prompt appears after
 * the first turn lands).
 *
 * @returns The sidebar element with the New Session button + scrolling list.
 */
export default function SessionList()
{
  const currentSessionId = useConversationStore((s) => s.currentSessionId);
  const setCurrentSessionId = useConversationStore((s) => s.setCurrentSessionId);
  const loadHistory = useConversationStore((s) => s.loadHistory);
  const clearMessages = useConversationStore((s) => s.clearMessages);

  const [sessions, setSessions] = useState<SessionSummary[]>([]);
  const [busy, setBusy] = useState(false);
  const [refreshTick, setRefreshTick] = useState(0);

  // #region Effects

  useEffect(() => {
    let cancelled = false;
    getSessions()
      .then((list) => {
        if (!cancelled)
        {
          setSessions(list);
        }

      })
      .catch((err) => {
        console.error("[sessions] list failed:", err);
      });
    return () => {
      cancelled = true;
    };
  }, [refreshTick]);

  // #endregion

  // #region Handlers

  const refresh = useCallback(() => setRefreshTick((n) => n + 1), []);

  const handleResume = async (id: string) => {
    if (busy || id === currentSessionId)
    {
      return;
    }

    setBusy(true);
    try
    {
      const history = await getSessionMessages(id);
      loadHistory(history);
      setCurrentSessionId(id);
      await resumeSession(id);
    }
    catch (err)
    {
      console.error("[sessions] resume failed:", err);
    }
    finally
    {
      setBusy(false);
    }
  };

  const handleNew = async () => {
    if (busy)
    {
      return;
    }

    setBusy(true);
    try
    {
      await startNewSession();
      clearMessages();
      setCurrentSessionId(null);
      refresh();
    }
    catch (err)
    {
      console.error("[sessions] new session failed:", err);
    }
    finally
    {
      setBusy(false);
    }
  };

  // #endregion

  return (
    <div className="flex h-full flex-col">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
          Sessions
        </h2>
        <button
          type="button"
          onClick={() => void refresh()}
          disabled={busy}
          className="text-xs text-slate-500 hover:text-slate-300 disabled:opacity-50"
          title="Refresh list"
        >
          ↻
        </button>
      </div>

      <button
        type="button"
        onClick={() => void handleNew()}
        disabled={busy}
        className="mb-3 rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        + New Session
      </button>

      <div className="flex-1 space-y-1 overflow-y-auto pr-1">
        {sessions.length === 0 ? (
          <p className="text-xs text-slate-600">No sessions yet.</p>
        ) : (
          sessions.map((s) => {
            const active = s.id === currentSessionId;
            return (
              <button
                key={s.id}
                type="button"
                onClick={() => void handleResume(s.id)}
                disabled={busy}
                className={`w-full rounded px-2 py-1.5 text-left text-xs transition-colors disabled:cursor-not-allowed disabled:opacity-50 ${
                  active
                    ? "border border-sky-700/60 bg-sky-900/40 text-sky-100"
                    : "border border-transparent bg-slate-800/40 text-slate-300 hover:border-slate-700 hover:bg-slate-800"
                }`}
              >
                <div className="truncate font-medium">{s.title}</div>
                <div className="mt-0.5 flex justify-between text-[10px] text-slate-500">
                  <span>{formatRelative(s.lastModified)}</span>
                  <span>
                    {s.messageCount} {s.messageCount === 1 ? "msg" : "msgs"}
                  </span>
                </div>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}