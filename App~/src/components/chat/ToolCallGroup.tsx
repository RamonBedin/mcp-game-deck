/**
 * Tool-call group — collapses a fan-out of related calls into a single
 * card that summarizes progress and lets the user drill into each step.
 *
 * Use case: Claude generating 8 animation clips, baking 12 lightmap
 * tiles, sweeping a folder of textures. Without grouping, each call
 * is a separate block and the conversation becomes a wall of green
 * checkmarks; with grouping, the user sees `"Generating animation
 * clips · 5/8"` with the calls neatly indented when expanded.
 *
 * Grouping logic lives in the consumer (ChatRoute / conversationStore).
 * A pragmatic rule: collapse 3+ consecutive `tool-use` blocks whose
 * names share a category prefix (`animation-*`, `lightmap-*`, etc).
 *
 * @requires-backend B.03 tool metadata catalog for the group title /
 *   narrative; without it, the title falls back to the shared prefix.
 */

import { useState } from "react";
import type { ReactNode } from "react";
import type { ToolCallStatus } from "./ToolCallNarrativeBlock";

// #region Types

/** Single call inside a group. */
export interface GroupedToolCall
{
  name: string;
  status: ToolCallStatus;
  narrative?: ReactNode;
  duration?: string;
}

/**
 * Props for the `ToolCallGroup` component.
 *
 * Renders a collapsible group of related tool calls under a single heading,
 * with an optional initial expansion state for contexts where the group
 * should default to open (e.g. the most recent turn).
 */
interface ToolCallGroupProps
{
  title: string;
  calls: GroupedToolCall[];
  defaultExpanded?: boolean;
}

// #endregion

// #region Helpers

const groupStatus = (calls: GroupedToolCall[]): { running: boolean; done: number; failed: number; total: number } => {
  let done = 0;
  let failed = 0;
  let running = false;

  for (const c of calls)
  {
    if (c.status === "done")
    {
      done += 1;
    }
    else if (c.status === "failed")
    {
      failed += 1;
    }
    else if (c.status === "running")
    {
      running = true;
    }
  }

  return { running, done, failed, total: calls.length };
};

// #endregion

/**
 * Renders the group card. Header is always visible; member rows show
 * when expanded.
 *
 * @param props - See {@link ToolCallGroupProps}.
 * @returns The group element.
 */
export default function ToolCallGroup({ title, calls, defaultExpanded }: ToolCallGroupProps)
{
  const status = groupStatus(calls);
  const inProgress = status.running || status.done < status.total;
  const hasFailures = status.failed > 0;
  const initialExpanded = defaultExpanded ?? inProgress;
  const [expanded, setExpanded] = useState(initialExpanded);

  const headerBg = inProgress ? "linear-gradient(90deg, rgba(76,201,255,0.06), transparent 60%)" : hasFailures ? "linear-gradient(90deg, rgba(255,92,122,0.06), transparent 60%)" : "transparent";

  const tally = hasFailures ? `${status.done}/${status.total} · ${status.failed} failed` : `${status.done}/${status.total}`;

  return (
    <div className="rounded-r-3 border border-line bg-bg-2 overflow-hidden">
      <button
        type="button"
        onClick={() => setExpanded((prev) => !prev)}
        className={[
          "w-full flex items-center gap-3 px-3.5 py-2.5 text-left",
          expanded ? "border-b border-line-soft" : "",
        ].join(" ")}
        style={{ background: headerBg }}
        aria-expanded={expanded}
      >
        <GroupBadge inProgress={inProgress} hasFailures={hasFailures} />
        <span className="flex-1 min-w-0 truncate text-[13.5px] text-txt-1 font-medium">{title}</span>
        <span className="font-mono text-[11px] shrink-0" style={{ color: inProgress ? "var(--cyan)" : hasFailures ? "var(--bad)" : "var(--txt-3)" }}>
          {tally}
        </span>
        <span className="text-txt-4 text-[11px] ml-1.5 shrink-0" aria-hidden="true">
          {expanded ? "▾" : "▸"}
        </span>
      </button>

      {expanded && (
        <div className="px-3 py-1.5 flex flex-col gap-0.5">
          {calls.map((c, i) => (
            <GroupRow key={i} call={c} />
          ))}
        </div>
      )}
    </div>
  );
}

// #region Sub-components

const GroupBadge = ({ inProgress, hasFailures }: { inProgress: boolean; hasFailures: boolean }) => {
  let icon = "✓";
  let color = "var(--ok)";
  let bg = "rgba(74, 222, 128, 0.12)";

  if (hasFailures && !inProgress)
  {
    icon = "✗";
    color = "var(--bad)";
    bg = "rgba(255, 92, 122, 0.12)";
  }
  else if (inProgress)
  {
    icon = "●●";
    color = "var(--cyan)";
    bg = "rgba(76, 201, 255, 0.12)";
  }

  return (
    <span
      aria-hidden="true"
      className="inline-flex items-center justify-center font-hud font-bold text-[9px] shrink-0"
      style={{
        width: 22,
        height: 22,
        borderRadius: "var(--r-1)",
        color,
        background: bg,
      }}
    >
      {icon}
    </span>
  );
};

const GroupRow = ({ call }: { call: GroupedToolCall }) => {
  const dotColor = call.status === "done" ? "var(--ok)" : call.status === "failed"  ? "var(--bad)" : call.status === "running" ? "var(--cyan)" : "var(--bg-3)";
  const dotIcon = call.status === "done"    ? "✓" : call.status === "failed"  ? "✗" : call.status === "running" ? "●" : "";
  const textColor = call.status === "queued" ? "text-txt-4" : "text-txt-2";

  return (
    <div className="flex items-center gap-2.5 px-2.5 py-1.5 text-[12px]">
      <span
        aria-hidden="true"
        className="inline-flex items-center justify-center text-[9px] font-bold shrink-0 rounded-full"
        style={{
          width: 14,
          height: 14,
          color: call.status === "queued" ? "var(--txt-4)" : "var(--bg-0)",
          background: dotColor,
          animation: call.status === "running" ? "pulse-soft 1.2s ease-in-out infinite" : "none",
        }}
      >
        {dotIcon}
      </span>
      <span className={`flex-1 min-w-0 truncate ${textColor}`}>
        {call.narrative ?? call.name}
      </span>
      {call.duration !== undefined && (
        <span className="font-mono text-[10px] text-txt-4 shrink-0">{call.duration}</span>
      )}
    </div>
  );
};

// #endregion