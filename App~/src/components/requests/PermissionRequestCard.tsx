/**
 * Major changes from v1:
 *   - **Tier-aware accent** — every card now classifies the tool as
 *     `read`/`write`/`destr` (see toolTier.ts). The accent + Allow
 *     button color reflect the risk.
 *   - **Narrative-first body** — the headline reads "Claude wants to
 *     `<verb>` `<target>`" instead of dumping JSON inputs upfront.
 *   - **Decision reason** rendered as a styled quote, not inline text.
 *   - **Inputs collapsible** — raw JSON moves to a "View inputs"
 *     accordion. Devs still get it; just not in their face.
 *   - **Footer hint** surfacing the ⇧⏎ shortcut for keyboard-driven
 *     approval.
 *
 * The wire payload (`PermissionRequestedPayload`) is unchanged — the
 * rewrite is purely visual. Tier inference uses `classifyTool` (see
 * `toolTier.ts`) until the backend supplies B.04.
 *
 * @requires-backend B.04 per-tool risk tier — without it, tier is
 *   inferred from the tool name (see classifyTool).
 */

import { useMemo, useState } from "react";
import type { PermissionRequestedPayload } from "../../ipc/types";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";
import RequestCard, { type RequestCardState } from "./RequestCard";
import { classifyTool, verbFor, type PermissionTier } from "./toolTier";

// #region Types

/**
 * Outcome the user selected. Excludes `auto-allowed`, which never
 * routes through this component (the in-session cache short-circuit
 * synthesizes a compact inline caption from `BlockView` directly).
 */
export type PermissionOutcome = "allow" | "allow-always" | "deny";

const OUTCOME_LABEL: Record<PermissionOutcome, string> = {
  allow:         "Allowed",
  "allow-always":"Allowed (always)",
  deny:          "Denied",
};
interface PermissionRequestCardProps
{
  payload: PermissionRequestedPayload;
  state: RequestCardState;
  outcome?: PermissionOutcome;
  onDecision: (outcome: PermissionOutcome) => void;
}

// #endregion

// #region Helpers

const INPUT_PREVIEW_LIMIT = 1200;

const TIER_TO_ACCENT: Record<PermissionTier, "read" | "write" | "destr"> = {
  read:  "read",
  write: "write",
  destr: "destr",
};

const TIER_PILL_VARIANT: Record<PermissionTier, "tier-read" | "tier-write" | "tier-destr"> = {
  read:  "tier-read",
  write: "tier-write",
  destr: "tier-destr",
};

const TIER_LABEL: Record<PermissionTier, string> = {
  read:  "READ",
  write: "WRITE",
  destr: "DESTRUCTIVE",
};

/** Pretty-print the input payload, truncating past the preview limit. */
const stringifyInput = (input: unknown): { body: string; truncated: boolean } => {
  let str: string;
  try
  {
    str = JSON.stringify(input, null, 2);
  }
  catch
  {
    str = String(input);
  }

  if (str.length > INPUT_PREVIEW_LIMIT)
  {
    return { body: str.slice(0, INPUT_PREVIEW_LIMIT) + "\n… (truncated)", truncated: true };
  }

  return { body: str, truncated: false };
};

/**
 * Best-effort extraction of a single "target" string from the input
 * (a path, a name, an id) so the headline can name what's affected
 * instead of just `the inputs`. Looks at common keys in order; falls
 * back to `null` when none match.
 */
const extractTarget = (input: unknown): string | null => {
  if (input === null || typeof input !== "object")
  {
    return null;
  }

  const candidates = ["path", "assetPath", "scenePath", "targetPath", "name", "scenName", "objectName", "url"];
  const record = input as Record<string, unknown>;

  for (const key of candidates)
  {
    const v = record[key];

    if (typeof v === "string" && v.length > 0)
    {
      return v;
    }
  }

  return null;
};

// #endregion

/**
 * Renders the permission card.
 *
 * @param props - See {@link PermissionRequestCardProps}.
 * @returns The card element.
 */
export default function PermissionRequestCard({
  payload,
  state,
  outcome,
  onDecision,
}: PermissionRequestCardProps)
{
  const isPending = state === "pending";
  const [showInputs, setShowInputs] = useState(false);

  const tier = useMemo(() => classifyTool(payload.toolName), [payload.toolName]);
  const accent = TIER_TO_ACCENT[tier];
  const pillVariant = TIER_PILL_VARIANT[tier];
  const tierLabel = TIER_LABEL[tier];

  const target = useMemo(() => extractTarget(payload.input), [payload.input]);
  const verb = useMemo(() => verbFor(payload.toolName), [payload.toolName]);
  const { body: inputJson, truncated } = useMemo(() => stringifyInput(payload.input), [payload.input]);

  const label = (
    <>
      <Pill variant={pillVariant} size="md" dot>{tierLabel}</Pill>
      <span className="font-mono text-[11px] text-txt-3 truncate">{payload.toolName}</span>
    </>
  );

  const headerRight = payload.agentId !== null
    ? <span className="font-mono text-[10.5px] text-txt-4">via {payload.agentId}</span>
    : undefined;

  const body = (
    <div className="flex flex-col gap-3">
      {/* Narrative */}
      <div className="text-[14.5px] text-txt-1 leading-snug">
        Claude wants to <strong>{verb}</strong>
        {target !== null && (
          <>{" "}<span className="font-mono" style={{ color: TIER_TARGET_COLOR[tier] }}>{target}</span></>
        )}
        .
      </div>

      {/* Decision reason as quote */}
      {payload.decisionReason !== null && payload.decisionReason.length > 0 && (
        <div className="text-[12.5px] text-txt-3 leading-snug pl-2.5 border-l-2 border-line-hard">
          {payload.decisionReason}
        </div>
      )}

      {/* Blocked path note */}
      {payload.blockedPath !== null && (
        <div className="text-[11.5px] text-txt-4 font-mono">
          blocked path · <span className="text-txt-2">{payload.blockedPath}</span>
        </div>
      )}

      {/* Inputs (collapsed) */}
      <button
        type="button"
        onClick={() => setShowInputs((prev) => !prev)}
        className="self-start font-mono text-[11px] text-txt-3 hover:text-txt-1 transition-colors duration-[120ms]"
        aria-expanded={showInputs}
      >
        {showInputs ? "▾ Hide raw inputs" : "▸ View raw inputs"}
        {truncated && <span className="ml-2 text-txt-4">· truncated</span>}
      </button>

      {showInputs && (
        <pre className="font-mono text-[11px] leading-[1.5] whitespace-pre-wrap max-h-60 overflow-y-auto rounded-r-1 bg-bg-0 border border-line-soft px-2.5 py-2 text-txt-2">
          {inputJson}
        </pre>
      )}
    </div>
  );

  const caption = state === "answered" && outcome !== undefined ? OUTCOME_LABEL[outcome] : null;

  const footer = (
    <>
      {caption !== null && (
        <span className="font-mono text-[11px] text-txt-4 italic self-center mr-2">{caption}</span>
      )}
      <Button variant="ghost" size="sm" disabled={!isPending} onClick={() => onDecision("deny")}>
        Deny
      </Button>
      <Button variant="default" size="sm" disabled={!isPending} onClick={() => onDecision("allow-always")}>
        Always
      </Button>
      <Button
        variant={tier === "destr" ? "danger" : "primary"}
        size="sm"
        disabled={!isPending}
        onClick={() => onDecision("allow")}
      >
        Allow
      </Button>
    </>
  );

  const footerHint = isPending
    ? (
      <>
        <Pill variant="subtle" size="sm">⇧⏎</Pill>
        <span>allow · ⌫ deny</span>
      </>
    )
    : undefined;

  return (
    <RequestCard
      variant="permission"
      accent={accent}
      label={label}
      headerRight={headerRight}
      body={body}
      footer={footer}
      footerHint={footerHint}
      state={state}
    />
  );
}

// #region Constants

const TIER_TARGET_COLOR: Record<PermissionTier, string> = {
  read:  "var(--tier-read)",
  write: "var(--tier-write)",
  destr: "var(--tier-destr)",
};

// #endregion
