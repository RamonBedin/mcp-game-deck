/**
 * Sidebar that lists every Claude Code session for the active Unity
 * project, plus a "New Session" button at the top.
 *
 * Sessions live under `<home>/.claude/projects/<encoded-cwd>/*.jsonl`
 * (Claude Code's storage is the source of truth).
 * Clicking a row pre-loads its history into the conversation store
 * and pins the supervisor to resume that session on the next prompt;
 * the New Session button clears both pieces of state.
 *
 * v2.0 UX Pass: visual rewrite to match the `SessionsPanel` mockup —
 * font-hud header with count, refresh `IconButton`, neutral `Button`
 * for "New chat", rows with left-violet bar accent when active and
 * `txt-1/2/4` text hierarchy.
 */

import { useCallback, useEffect, useRef, useState } from "react";
import { deleteSession, getSessionMessages, getSessions, resumeSession, startNewSession, } from "../ipc/commands";
import type { SessionSummary } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";
import Button from "./atoms/Button";
import IconButton from "./atoms/IconButton";

/**
 * Props for the `SessionList` component.
 *
 * Renders the sidebar list of past sessions and optionally exposes a callback
 * for collapsing the sidebar — typically wired to a close button rendered
 * inside the list header.
 */
interface SessionListProps
{
  onCollapse?: () => void;
}

const HIDDEN_SESSION_TITLES = new Set(["__health__"]);

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
export default function SessionList({ onCollapse }: SessionListProps = {})
{
  const currentSessionId = useConversationStore((s) => s.currentSessionId);
  const setCurrentSessionId = useConversationStore((s) => s.setCurrentSessionId);
  const loadHistory = useConversationStore((s) => s.loadHistory);
  const clearMessages = useConversationStore((s) => s.clearMessages);

  const [sessions, setSessions] = useState<SessionSummary[]>([]);
  const [busy, setBusy] = useState(false);
  const [refreshTick, setRefreshTick] = useState(0);

  const autoResumedRef = useRef(false);

  // #region Effects

  useEffect(() => {
    let cancelled = false;
    getSessions()
      .then((list) => {
        if (!cancelled)
        {
          const visible = list.filter((s) => !HIDDEN_SESSION_TITLES.has(s.title));
          setSessions(visible);
        }

      })
      .catch((err) => {
        console.error("[sessions] list failed:", err);
      });
    return () => {
      cancelled = true;
    };
  }, [refreshTick]);

  useEffect(() => {
    if (autoResumedRef.current)
    {
      return;
    }

    if (currentSessionId !== null)
    {
      autoResumedRef.current = true;
      return;
    }

    if (sessions.length === 0)
    {
      return;
    }

    const mostRecent = sessions.reduce((acc, s) => s.lastModified > acc.lastModified ? s : acc,);
    autoResumedRef.current = true;
    void handleResume(mostRecent.id);
  }, [sessions]);

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

  const handleDelete = async (id: string, title: string) => {
    if (busy)
    {
      return;
    }

    const confirmed = window.confirm(
      `Delete session "${title}"? This removes the JSONL file from Claude Code's storage and cannot be undone.`,
    );

    if (!confirmed)
    {
      return;
    }

    setBusy(true);
    try
    {
      await deleteSession(id);

      if (id === currentSessionId)
      {
        try
        {
          await startNewSession();
        }
        catch (err)
        {
          console.error("[sessions] startNewSession after delete failed:", err);
        }
        clearMessages();
        setCurrentSessionId(null);
      }
      refresh();
    }
    catch (err)
    {
      console.error("[sessions] delete failed:", err);
    }
    finally
    {
      setBusy(false);
    }
  };

  // #endregion

  return (
    <div className="flex h-full flex-col min-h-0">
      <div className="mb-3 flex items-center justify-between gap-1">
        <span className="font-hud text-[9px] tracking-[0.18em] uppercase text-txt-4 flex-1">
          Sessions · {sessions.length}
        </span>
        <IconButton
          size={20}
          onClick={() => refresh()}
          disabled={busy}
          aria-label="Refresh sessions"
          title="Refresh list"
        >
          <span style={{ fontSize: 12 }}>↻</span>
        </IconButton>
        {onCollapse !== undefined && (
          <IconButton
            size={20}
            onClick={onCollapse}
            aria-label="Collapse sessions"
            title="Collapse sessions"
          >
            <span style={{ fontSize: 11 }}>‹</span>
          </IconButton>
        )}
      </div>

      <Button
        variant="default"
        size="sm"
        onClick={() => void handleNew()}
        disabled={busy}
        className="mb-3.5 justify-center"
      >
        <span className="mr-1">+</span> New chat
      </Button>

      <div className="flex-1 min-h-0 overflow-y-auto flex flex-col gap-0.5 pr-1">
        {sessions.length === 0 ? (
          <p className="text-[11.5px] text-txt-4 italic px-2 py-1">No sessions yet.</p>
        ) : (
          sessions.map((s) => {
            const active = s.id === currentSessionId;
            return (
              <div
                key={s.id}
                className={[
                  "group relative rounded-r-1 transition-colors duration-[120ms]",
                  active
                    ? "bg-bg-3 shadow-[inset_2px_0_0_var(--violet)]"
                    : "border-l-2 border-transparent hover:bg-bg-3/50",
                ].join(" ")}
              >
                <button
                  type="button"
                  onClick={() => void handleResume(s.id)}
                  disabled={busy}
                  className="w-full text-left px-2.5 py-2 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <div
                    className="truncate text-[12.5px] mb-1 pr-6"
                    style={{
                      color: active ? "var(--txt-1)" : "var(--txt-2)",
                      fontWeight: active ? 500 : 400,
                    }}
                  >
                    {s.title}
                  </div>
                  <div className="flex justify-between font-mono text-[9.5px] text-txt-4">
                    <span>{formatRelative(s.lastModified)}</span>
                    <span>
                      {s.messageCount} {s.messageCount === 1 ? "msg" : "msgs"}
                    </span>
                  </div>
                </button>
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    void handleDelete(s.id, s.title);
                  }}
                  disabled={busy}
                  aria-label={`Delete session ${s.title}`}
                  title="Delete session"
                  className="absolute top-1.5 right-1 hidden group-hover:inline-flex items-center justify-center w-5 h-5 rounded-r-1 text-txt-4 hover:text-bad hover:bg-bad/10 transition-colors duration-[120ms] disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  <TrashIcon />
                </button>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}

const TrashIcon = () => (
  <svg
    width={11}
    height={11}
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth={2}
    strokeLinecap="round"
    strokeLinejoin="round"
    aria-hidden="true"
  >
    <polyline points="3 6 5 6 21 6" />
    <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
    <path d="M10 11v6M14 11v6" />
    <path d="M9 6V4a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v2" />
  </svg>
);