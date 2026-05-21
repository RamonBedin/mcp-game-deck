/**
 * Dedicated card for the `ExitPlanMode` built-in tool (KI-004).
 *
 * Before this card existed, `ExitPlanMode` fell through to
 * `PermissionRequestCard` — Claude's drafted plan would render as a
 * generic "wants to use ExitPlanMode" approval, hiding the actual plan
 * body behind a "View inputs" accordion. This component surfaces the
 * markdown plan as the primary content and replaces the
 * Allow/Always/Deny triplet with explicit Accept / Reject.
 *
 * The wire path reuses the same `respond-to-request` permission outcome
 * — `allow` maps to Accept (Claude starts implementing), `deny` maps
 * to Reject (Claude replies but does not start coding). `allow-always`
 * is intentionally not offered: every plan is unique, caching the
 * decision makes no sense.
 */

import ReactMarkdown from "react-markdown";
import type { PlanSummaryPayload } from "../../ipc/types";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";
import RequestCard, { type RequestCardState } from "./RequestCard";
import { markdownRenderers } from "./markdown-renderers";

// #region Types

export type PlanOutcome = "allow" | "deny";

const OUTCOME_LABEL: Record<PlanOutcome, string> = {
  allow: "Accepted",
  deny:  "Rejected",
};

/**
 * Props for the `PlanSummaryCard` component.
 *
 * Renders the plan summary an agent proposes before executing, exposes the
 * card's lifecycle state for styling, and reports the user's accept/reject
 * decision back to the parent via `onDecision`.
 */
interface PlanSummaryCardProps
{
  payload: PlanSummaryPayload;
  state: RequestCardState;
  outcome?: PlanOutcome;
  onDecision: (outcome: PlanOutcome) => void;
}

// #endregion

// #region Component

/**
 * Renders the plan card.
 *
 * @param props - See {@link PlanSummaryCardProps}.
 * @returns The card element.
 */
export default function PlanSummaryCard({payload, state, outcome, onDecision,}: PlanSummaryCardProps)
{
  const isPending = state === "pending";

  const label = (
    <>
      <Pill variant="subtle" size="md" dot>PLAN</Pill>
      <span className="text-[12.5px] text-txt-2">Claude has drafted a plan</span>
    </>
  );

  const body = (
    <div className="rounded-r-2 bg-bg-0 border border-line-soft px-3.5 py-2.5 max-h-96 overflow-y-auto text-[13px] leading-relaxed text-txt-1">
      {payload.plan.length > 0
        ? <ReactMarkdown components={markdownRenderers}>{payload.plan}</ReactMarkdown>
        : <span className="text-txt-4 italic">(empty plan)</span>}
    </div>
  );

  const caption = state === "answered" && outcome !== undefined ? OUTCOME_LABEL[outcome] : null;

  const footer = (
    <>
      {caption !== null && (
        <span className="font-mono text-[11px] text-txt-4 italic self-center mr-2">{caption}</span>
      )}
      <Button variant="ghost" size="sm" disabled={!isPending} onClick={() => onDecision("deny")}>
        Reject
      </Button>
      <Button variant="primary" size="sm" disabled={!isPending} onClick={() => onDecision("allow")}>
        Accept and start coding
      </Button>
    </>
  );

  const footerHint = isPending
    ? (
      <>
        <Pill variant="subtle" size="sm">⇧⏎</Pill>
        <span>accept · ⌫ reject</span>
      </>
    )
    : undefined;

  return (
    <RequestCard
      variant="question"
      accent="violet"
      label={label}
      body={body}
      footer={footer}
      footerHint={footerHint}
      state={state}
    />
  );
}

// #endregion