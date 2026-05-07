import type { ReactNode } from "react";

/**
 * Visual lifecycle of a request card. Drives the chrome opacity and
 * tells variant components whether their footer actions should be
 * interactive or disabled.
 *
 * - `pending` — full color, footer interactive.
 * - `answered` — dimmed (`opacity-70`); variant disables footer buttons.
 * - `interrupted` — heavily dimmed (`opacity-50`); variant disables
 *   footer; "Conversation interrupted" caption appended underneath.
 *   Triggered by supervisor crash mid-request.
 * - `auto-allowed` — compact one-line rendering with no body and no
 *   footer. Used when the in-session Allow Always cache short-circuited
 *   the request (task 1.4). In practice `BlockView` (task 3.5) is
 *   expected to render this branch directly inline rather than via
 *   `RequestCard` (it has typed access to the synthesized payload's
 *   `toolName`); this branch stays here as a salvaguarda for callers
 *   that do route auto-allowed through `RequestCard`.
 */
export type RequestCardState =
  | "pending"
  | "answered"
  | "interrupted"
  | "auto-allowed";

/**
 * Variant of the card. Drives the accent border color (yellow for
 * permission, blue for question) and the leading icon. Other layout
 * (body markdown, footer actions) is the variant component's job.
 */
export type RequestCardVariant = "permission" | "question";

/**
 * Props for the `RequestCard` component.
 *
 * Drives the visual treatment, body content, and footer controls of a single
 * agent request card — covering tool-use, permission prompts, and other
 * variants surfaced during a conversation turn.
 */
export interface RequestCardProps
{
  variant: RequestCardVariant;
  label: string;
  body: ReactNode;
  agentId: string | null;
  state: RequestCardState;
  footer: ReactNode;
}

/**
 * Shared base component for permission and question cards. Owns the
 * chrome (border accent, header strip with icon + label + optional
 * agent id, markdown body wrapper, footer slot) and the visual state
 * transitions. Variant components own the body markdown
 * formatting and the footer's interactive behavior.
 *
 * F09 (Design Handoff) restyles this component once and both variants
 * pick up the changes for free.
 *
 * @param props - See {@link RequestCardProps}.
 * @returns The rendered card, or a compact one-line synthesis for the
 *   `auto-allowed` state.
 */
export function RequestCard(props: RequestCardProps)
{
  const { variant, label, body, agentId, state, footer } = props;

  if (state === "auto-allowed")
  {
    return (
      <div className="text-xs text-slate-500 italic my-2">
        Auto-allowed: {label}
      </div>
    );
  }

  const accentBorder =
    variant === "permission" ? "border-yellow-500" : "border-blue-500";
  const icon = variant === "permission" ? "🛡️" : "❓";
  const stateOpacity =
    state === "answered"
      ? "opacity-70"
      : state === "interrupted"
        ? "opacity-50"
        : "";

  return (
    <div
      className={`rounded border-l-4 ${accentBorder} bg-slate-800/60 p-3 my-2 ${stateOpacity}`}
    >
      <div className="flex items-center gap-2 mb-2">
        <span aria-hidden="true">{icon}</span>
        <span className="text-xs uppercase tracking-wider text-slate-400">
          {label}
        </span>
        {agentId !== null && (
          <span className="ml-auto text-xs text-slate-500">
            from {agentId}
          </span>
        )}
      </div>
      <div>{body}</div>
      <div className="mt-3 flex flex-wrap gap-2 justify-end">{footer}</div>
      {state === "interrupted" && (
        <div className="text-xs text-slate-500 mt-2">
          Conversation interrupted — answer no longer applicable.
        </div>
      )}
    </div>
  );
}