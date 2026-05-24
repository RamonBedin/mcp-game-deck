/**
 * Non-blocking banner shown at the top of the window when the local
 * `claude --version` falls outside the smoke-tested range published
 * by `package.json`'s `claudeCode` field.
 *
 * Subscribes to the Rust-emitted `claude-version-out-of-range` event
 * (fired at most once per supervisor startup from
 * `claude_supervisor::version_check::run`). Dismissable per session;
 * re-appears on the next launch if still out of range, intentional —
 * version drift may resolve between boots.
 */

import { useEffect, useState } from "react";
import { onClaudeVersionOutOfRange } from "../ipc/events";
import type { ClaudeVersionOutOfRangePayload } from "../ipc/types";

/**
 * Renders the warning when the supervisor reports a version mismatch
 * and the user has not dismissed it this session. Returns `null`
 * otherwise (no DOM, no layout cost).
 *
 * @returns The banner element when active, or `null`.
 */
export default function ClaudeVersionWarningBanner()
{
  const [warning, setWarning] = useState<ClaudeVersionOutOfRangePayload | null>(null);
  const [dismissed, setDismissed] = useState(false);

  // #region Effects

  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onClaudeVersionOutOfRange((payload) => {
      if (cancelled)
      {
        return;
      }

      setWarning(payload);
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
        console.error("[claude-version-warning] failed to subscribe:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

  // #endregion

  if (warning === null || dismissed)
  {
    return null;
  }

  // #region Handlers

  const handleDismiss = () => {
    setDismissed(true);
  };

  // #endregion

  return (
    <div className="flex shrink-0 items-center justify-between border-b border-amber-700 bg-amber-900/40 px-4 py-2 text-sm text-slate-100">
      <span>
        Claude Code v{warning.detected} detected; tested with {warning.supported}. Some features may behave differently.
      </span>
      <button
        type="button"
        onClick={handleDismiss}
        aria-label="Dismiss Claude Code version warning"
        className="rounded px-2 py-1 text-base leading-none text-slate-300 transition-colors hover:bg-slate-800"
      >
        ×
      </button>
    </div>
  );
}