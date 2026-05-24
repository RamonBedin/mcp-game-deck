/**
 * Compact ring HUD showing how much of the model's context window the
 * current session is using. Mounted in the right-aligned section of
 * `HudStrip` next to the session label.
 *
 * Reads `turnUsage` + `turnContextWindow` from `conversationStore`,
 * both written every time the supervisor emits a `usage-update`
 * envelope (end of each turn). The arc fills proportional to
 * `input + cache_read + cache_creation` over `turnContextWindow` —
 * which is the SDK's `result.modelUsage[<model>].contextWindow`, the
 * authoritative max for whatever model the turn used. No per-model
 * limits are hardcoded here.
 *
 * Color band tracks risk:
 *   <  50% → info (cyan)
 *   50–75% → warn (orange)
 *   ≥ 75% → bad (red)
 *
 * Clicking the ring sends `/compact` as a regular user message — the
 * SDK exposes this as a real slash command (seen in
 * `system/init.slash_commands`), so the supervisor forwards it like
 * any other prompt and the CLI compacts the session.
 */

import { useMemo } from "react";
import type { TurnUsage } from "../../ipc/types";
import { useConversationStore } from "../../stores/conversationStore";

// #region Helpers

const usedTokens = (usage: TurnUsage): number => {
  const input         = typeof usage.input_tokens                 === "number" ? usage.input_tokens                 : 0;
  const cacheRead     = typeof usage.cache_read_input_tokens      === "number" ? usage.cache_read_input_tokens      : 0;
  const cacheCreation = typeof usage.cache_creation_input_tokens  === "number" ? usage.cache_creation_input_tokens  : 0;
  return input + cacheRead + cacheCreation;
};

const formatTokensShort = (n: number): string => {
  if (n >= 1_000_000)
  {
    return `${(n / 1_000_000).toFixed(1)}M`;
  }

  if (n >= 1000)
  {
    return `${(n / 1000).toFixed(n >= 10_000 ? 0 : 1)}k`;
  }
  return `${n}`;
};

const arcColor = (pct: number): string => {
  if (pct >= 75)
  {
    return "var(--bad)";
  }

  if (pct >= 50)
  {
    return "var(--warn)";
  }
  return "var(--info)";
};

// #endregion

// #region Component

/**
 * Renders the ring. Returns `null` until both a turn `usage` and a
 * `contextWindow` are available — no value to show on a fresh session
 * or while the first turn is still streaming.
 *
 * @returns The ring element, or `null` when no usage data is available.
 */
export default function ContextRing()
{
  const turnUsage = useConversationStore((s) => s.turnUsage);
  const turnContextWindow = useConversationStore((s) => s.turnContextWindow);
  const sendMessage = useConversationStore((s) => s.sendMessage);

  const view = useMemo(() => {
    if (turnUsage === null || turnContextWindow === null || turnContextWindow <= 0)
    {
      return null;
    }

    const used = usedTokens(turnUsage);
    const pct = Math.max(0, Math.min(100, (used / turnContextWindow) * 100));
    return { used, max: turnContextWindow, pct, color: arcColor(pct) };
  }, [turnUsage, turnContextWindow]);

  if (view === null)
  {
    return null;
  }

  const radius = 7;
  const stroke = 2;
  const size = (radius + stroke) * 2;
  const circumference = 2 * Math.PI * radius;
  const dashOffset = circumference * (1 - view.pct / 100);
  const tooltip = `${formatTokensShort(view.used)} / ${formatTokensShort(view.max)} (${view.pct.toFixed(0)}%) — click to /compact`;

  const handleCompact = () => {
    void sendMessage("/compact").catch((err) => {
      console.error("[context-ring] /compact send failed:", err);
    });
  };

  return (
    <button
      type="button"
      onClick={handleCompact}
      title={tooltip}
      aria-label={tooltip}
      className="inline-flex items-center gap-1.5 rounded-r-1 px-1.5 py-0.5 hover:bg-bg-3 transition-colors duration-[120ms]"
    >
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} aria-hidden="true">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="var(--line-soft)"
          strokeWidth={stroke}
        />
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke={view.color}
          strokeWidth={stroke}
          strokeDasharray={circumference}
          strokeDashoffset={dashOffset}
          strokeLinecap="round"
          transform={`rotate(-90 ${size / 2} ${size / 2})`}
        />
      </svg>
      <span className="font-mono text-[10.5px] text-txt-3" style={{ color: view.color }}>
        {view.pct.toFixed(0)}%
      </span>
    </button>
  );
}

// #endregion