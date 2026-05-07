import type { PermissionRequestedPayload } from "../../ipc/types";

import { RequestCard, type RequestCardState } from "./RequestCard";

const INPUT_PREVIEW_LIMIT = 800;

/**
 * Outcome the user chose for a permission request. Excludes
 * `auto-allowed`, which bypasses this component entirely
 * (BlockView synthesizes a compact line directly when the in-session
 * Allow Always cache short-circuits).
 */
export type PermissionOutcome = "allow" | "allow-always" | "deny";

const OUTCOME_LABEL: Record<PermissionOutcome, string> = {
  allow: "Allowed",
  "allow-always": "Allowed always",
  deny: "Denied",
};

/**
 * Builds the markdown body for a permission card from the wire
 * payload — tool name + JSON input preview + optional decision
 * reason + optional blocked path. Truncates the JSON preview at
 * {@link INPUT_PREVIEW_LIMIT} characters.
 *
 * @param p - The wire payload triggering the card.
 * @returns Markdown source ready for `react-markdown`.
 */
function formatPermissionBody(p: PermissionRequestedPayload): string
{
  const inputJson = JSON.stringify(p.input, null, 2);
  const truncated = inputJson.length > INPUT_PREVIEW_LIMIT ? inputJson.slice(0, INPUT_PREVIEW_LIMIT) + "\n... (truncated)" : inputJson;
  const lines = [
    `**\`${p.toolName}\`** wants to proceed with these inputs:`,
    "",
    "```json",
    truncated,
    "```",
  ];

  if (p.decisionReason)
  {
    lines.push("", p.decisionReason);
  }

  if (p.blockedPath)
  {
    lines.push("", `_Blocks path:_ \`${p.blockedPath}\``);
  }

  return lines.join("\n");
}

/**
 * Props for the `PermissionRequestCard` component.
 *
 * Renders a permission prompt for a tool call awaiting user approval, exposes
 * the prompt's lifecycle state for styling, and reports the user's decision
 * back to the parent via `onDecision`.
 */
export interface PermissionRequestCardProps
{
  payload: PermissionRequestedPayload;
  state: RequestCardState;
  outcome?: PermissionOutcome;
  onDecision: (outcome: PermissionOutcome) => void;
}

/**
 * Permission card variant — surfaces a `permission-requested` event
 * as an inline card with Allow / Allow Always / Deny
 * actions. Composes `RequestCard` for the chrome; owns the body
 * markdown formatting and the footer's interactive behavior.
 *
 * @param props - See {@link PermissionRequestCardProps}.
 * @returns The rendered permission card.
 */
export function PermissionRequestCard(props: PermissionRequestCardProps)
{
  const { payload, state, outcome, onDecision } = props;
  const isPending = state === "pending";
  const bodyMarkdown = formatPermissionBody(payload);
  const caption = state === "answered" && outcome !== undefined ? OUTCOME_LABEL[outcome] : null;
  const label = state === "auto-allowed" ? payload.toolName : "Permission required";

  const footer = (
    <>
      {caption !== null && (
        <span className="text-xs text-slate-500 italic mr-auto self-center">
          {caption}
        </span>
      )}
      <button
        disabled={!isPending}
        onClick={() => onDecision("deny")}
        className="rounded border border-red-700 px-3 py-1 text-xs text-red-400 hover:bg-red-950 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Deny
      </button>
      <button
        disabled={!isPending}
        onClick={() => onDecision("allow-always")}
        className="rounded border border-slate-600 px-3 py-1 text-xs text-slate-300 hover:bg-slate-800 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Allow Always
      </button>
      <button
        disabled={!isPending}
        onClick={() => onDecision("allow")}
        className="rounded bg-sky-700 px-3 py-1 text-xs text-sky-50 hover:bg-sky-600 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        Allow
      </button>
    </>
  );

  return (
    <RequestCard
      variant="permission"
      label={label}
      bodyMarkdown={bodyMarkdown}
      agentId={payload.agentId}
      state={state}
      footer={footer}
    />
  );
}