/**
 * `ToolResultBlock` pair with a single narrative row.
 *
 * Two-stage rendering:
 *  1. While the tool is in flight (no result yet): pending dot + tool
 *     name + narrative if available.
 *  2. Once the result lands: status icon flips to ✓/✗, optional
 *     duration appears, narrative is finalized.
 *
 * "Narrative" is the human-readable description of what the call does
 * — derived from the tool metadata catalog (B.03). The fallback when
 * metadata is missing is the literal tool name + a generic verb based
 * on category prefix (`scene-` → "Working with scene", etc).
 *
 * Click the row to expand into raw JSON inputs/outputs (devs still
 * want this; it's the second click, not the first).
 *
 * @requires-backend B.03 tool metadata catalog — without it, narrative
 *   degrades to "{toolName} called" / "{toolName} returned"; the row
 *   still works, just less informative.
 */

import { useState } from "react";
import type { ReactNode } from "react";

// #region Types

/** Visual status of the call. Drives the leading icon + color. */
export type ToolCallStatus = "queued" | "running" | "done" | "failed";

/**
 * Props for the `ToolCallNarrativeBlock` component.
 *
 * Renders a single tool call inside the conversation transcript, showing the
 * tool's name, a human-readable narrative summary, status, optional duration,
 * and collapsible input/output payloads for inspection.
 */
interface ToolCallNarrativeBlockProps
{
  name: string;
  narrative?: ReactNode;
  status: ToolCallStatus;
  duration?: string;
  input?: unknown;
  output?: unknown;
  isError?: boolean;
}

// #endregion

// #region Helpers

const STATUS_CONFIG: Record<ToolCallStatus, { icon: string; color: string; bg: string }> = {
  queued:  { icon: "○", color: "var(--txt-4)", bg: "var(--bg-3)" },
  running: { icon: "●", color: "var(--cyan)",  bg: "rgba(76, 201, 255, 0.15)" },
  done:    { icon: "✓", color: "var(--ok)",    bg: "rgba(74, 222, 128, 0.15)" },
  failed:  { icon: "✗", color: "var(--bad)",   bg: "rgba(255, 92, 122, 0.15)" },
};

const fallbackNarrative = (name: string, status: ToolCallStatus): string => {
  const dash = name.indexOf("-");
  const category = dash >= 0 ? name.slice(0, dash) : name;
  const action = dash >= 0 ? name.slice(dash + 1) : "";

  if (status === "done")
  {
    return action.length > 0 ? `Finished ${category}: ${action}` : `Finished ${category}`;
  }

  if (status === "failed")
  {
    return action.length > 0 ? `Failed: ${category} ${action}` : `Failed: ${category}`;
  }

  return action.length > 0 ? `Working with ${category}: ${action}` : `Working with ${category}`;
};

const stringify = (value: unknown): string => {
  if (typeof value === "string")
  {
    return value;
  }
  try
  {
    return JSON.stringify(value, null, 2);
  }
  catch
  {
    return String(value);
  }
};

// #endregion

/**
 * Renders the narrative row, optionally expanding to show the raw
 * JSON payload(s) below.
 *
 * @param props - See {@link ToolCallNarrativeBlockProps}.
 * @returns The narrative block element.
 */
export default function ToolCallNarrativeBlock({name, narrative, status, duration, input, output, isError = false,}: ToolCallNarrativeBlockProps)
{
  const [expanded, setExpanded] = useState(false);
  const cfg = STATUS_CONFIG[status];
  const text = narrative ?? fallbackNarrative(name, status);
  const hasDetails = input !== undefined || output !== undefined;

  return (
    <div className="rounded-r-2 border border-line bg-bg-2 overflow-hidden">
      <button
        type="button"
        onClick={() => hasDetails && setExpanded((prev) => !prev)}
        className={[
          "w-full flex items-center gap-2.5 px-3 py-2 text-left text-[12.5px] leading-snug",
          hasDetails ? "cursor-pointer hover:bg-bg-3/40" : "cursor-default",
        ].join(" ")}
        aria-expanded={hasDetails ? expanded : undefined}
      >
        <span
          aria-hidden="true"
          className="inline-flex items-center justify-center text-[10px] font-bold shrink-0 rounded-full"
          style={{
            width: 16,
            height: 16,
            color: cfg.color,
            background: cfg.bg,
            animation: status === "running" ? "pulse-soft 1.2s ease-in-out infinite" : "none",
          }}
        >
          {cfg.icon}
        </span>

        <span className="flex-1 min-w-0 text-txt-1 truncate">{text}</span>

        <span className="font-mono text-[10.5px] text-txt-3 whitespace-nowrap shrink-0">
          {name}
        </span>

        {duration !== undefined && (
          <span className="font-mono text-[10px] text-txt-4 shrink-0" style={{ minWidth: 36, textAlign: "right" }}>
            {duration}
          </span>
        )}

        {hasDetails && (
          <span className="text-txt-4 text-[11px] shrink-0" aria-hidden="true">
            {expanded ? "▾" : "▸"}
          </span>
        )}
      </button>

      {expanded && hasDetails && (
        <div className="border-t border-line-soft px-3 py-2.5 flex flex-col gap-2.5 bg-bg-0">
          {input !== undefined && (
            <DetailsBlock label="input" body={stringify(input)} tone="neutral" />
          )}
          {output !== undefined && (
            <DetailsBlock
              label="output"
              body={stringify(output)}
              tone={isError ? "error" : "neutral"}
            />
          )}
        </div>
      )}
    </div>
  );
}

// #region DetailsBlock

/**
 * Props for the `DetailsBlock` component.
 *
 * Renders a labeled, collapsible block of pre-formatted text — typically used
 * inside tool-call cards to surface raw input/output payloads or error
 * details. The tone token switches between neutral and error styling.
 */
interface DetailsBlockProps
{
  label: string;
  body: string;
  tone: "neutral" | "error";
}

const DetailsBlock = ({ label, body, tone }: DetailsBlockProps) => {
  const textColor = tone === "error" ? "text-bad" : "text-txt-2";

  return (
    <div>
      <div className="font-hud text-[9px] tracking-[0.18em] uppercase text-txt-4 mb-1">
        {label}
      </div>
      <pre className={[
        "font-mono text-[11px] leading-[1.5] whitespace-pre-wrap max-h-60 overflow-y-auto",
        "rounded-r-1 bg-bg-2 border border-line-soft px-2.5 py-2",
        textColor,
      ].join(" ")}>
        {body}
      </pre>
    </div>
  );
};

// #endregion