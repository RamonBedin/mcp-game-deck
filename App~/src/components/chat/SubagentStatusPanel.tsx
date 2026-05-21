/**
 * Live telemetry panel for a `Task` tool subagent.
 *
 * The supervisor folds three SDK message subtypes into one stream:
 *   - `task_started`    → phase `"started"`, with the initial prompt-derived description
 *   - `task_progress`   → phase `"progress"`, fired per step with the
 *                          current action (e.g. `"Reading X.cs"`) and
 *                          cumulative usage stats
 *   - `task_notification`→ phase `"completed"`, with summary text and
 *                          final usage
 *
 * The store upserts these into a single block keyed by `taskId` so
 * this component renders the latest snapshot. Without F37's
 * out-of-process server, the subagent's parent Task tool-use block
 * still owns the "Finished Agent" caption — this panel sits next to
 * it and surfaces what would otherwise be lost between the kickoff
 * and the final return.
 */

import { useMemo } from "react";
import type { SubagentPhase, SubagentUsage } from "../../ipc/types";
import Pill from "../atoms/Pill";

// #region Types

/**
 * Props for the `SubagentStatusPanel` component.
 *
 * Renders the inline status panel shown while a subagent is executing,
 * surfacing the current phase, a description of what's happening, an optional
 * running summary, token/usage stats, and the most recent tool name for
 * at-a-glance progress.
 */
interface SubagentStatusPanelProps
{
  phase: SubagentPhase;
  description: string;
  summary: string | null;
  usage: SubagentUsage | null;
  lastToolName: string | null;
}

// #endregion

// #region Helpers

const PHASE_CONFIG: Record<SubagentPhase, { icon: string; color: string; label: string }> = {
  started:   { icon: "○", color: "var(--info)", label: "Subagent started" },
  progress:  { icon: "●", color: "var(--info)", label: "Subagent working" },
  completed: { icon: "✓", color: "var(--ok)",   label: "Subagent done" },
};

const formatDuration = (ms: number | undefined): string | null => {
  if (ms === undefined || !Number.isFinite(ms) || ms <= 0)
  {
    return null;
  }

  if (ms < 1000)
  {
    return `${Math.round(ms)}ms`;
  }
  const s = ms / 1000;

  if (s < 60)
  {
    return `${s.toFixed(s < 10 ? 1 : 0)}s`;
  }
  const m = Math.floor(s / 60);
  const rem = Math.round(s - m * 60);
  return `${m}m ${rem}s`;
};

const formatTokens = (n: number | undefined): string | null => {
  if (n === undefined || !Number.isFinite(n) || n <= 0)
  {
    return null;
  }

  if (n >= 1000)
  {
    return `${(n / 1000).toFixed(1)}k tok`;
  }
  return `${n} tok`;
};

// #endregion

// #region Component

/**
 * Renders the subagent status panel.
 *
 * @param props - See {@link SubagentStatusPanelProps}.
 * @returns The panel element.
 */
export default function SubagentStatusPanel({phase, description, summary, usage, lastToolName,}: SubagentStatusPanelProps)
{
  const cfg = PHASE_CONFIG[phase];

  const stats = useMemo(() => {
    if (usage === null)
    {
      return [];
    }

    const out: string[] = [];
    const dur = formatDuration(usage.duration_ms);

    if (dur !== null)
    {
      out.push(dur);
    }

    if (typeof usage.tool_uses === "number" && usage.tool_uses > 0)
    {
      out.push(`${usage.tool_uses} tool${usage.tool_uses === 1 ? "" : "s"}`);
    }

    const tok = formatTokens(usage.total_tokens);

    if (tok !== null)
    {
      out.push(tok);
    }

    return out;
  }, [usage]);

  const headline = summary ?? (description.length > 0 ? description : cfg.label);

  return (
    <div
      className="rounded-r-2 border bg-bg-2/60 overflow-hidden"
      style={{ borderColor: "rgba(91, 189, 255, 0.25)" }}
      data-phase={phase}
    >
      <div className="px-3 py-2 flex items-center gap-2.5">
        <span style={{ color: cfg.color }} className="font-mono text-[12px] leading-none select-none">
          {cfg.icon}
        </span>
        <span className="text-[12px] text-txt-2 truncate flex-1 min-w-0">{headline}</span>
        {lastToolName !== null && phase !== "completed" && (
          <Pill variant="subtle" size="sm">{lastToolName}</Pill>
        )}
      </div>
      {stats.length > 0 && (
        <div className="px-3 pb-2 pt-0 flex items-center gap-2 font-mono text-[10.5px] text-txt-4">
          {stats.map((s, i) => (
            <span key={i} className="inline-flex items-center gap-1.5">
              {i > 0 && <span className="text-txt-5">·</span>}
              {s}
            </span>
          ))}
        </div>
      )}
    </div>
  );
}

// #endregion