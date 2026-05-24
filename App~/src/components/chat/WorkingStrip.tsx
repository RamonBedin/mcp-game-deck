/**
 * "Working on…" strip — slides in between the message list and the
 * composer whenever an assistant turn is in flight.
 *
 * The strip is the answer to F.04 from the audit: today the user sends
 * a message and stares at an inactive textarea for 5–30 seconds with
 * zero feedback. This component closes that gap by surfacing what the
 * supervisor is doing right now (text generation, tool calls, sub-agent
 * delegation) plus a Cancel affordance.
 *
 * `message` is the human-readable activity string fed by the consumer
 * (typically `ChatRoute.tsx` derived from the most recent agent message
 * type). When `agent` is supplied, an avatar precedes the message to
 * indicate which specialist is on the current step.
 *
 * @requires-backend B.01 supervisor activity stream — without B.01 the
 *   strip still works but defaults to "Claude is thinking…" until the
 *   next `text-delta` or `tool-use` arrives.
 * @requires-backend B.02 cancel_current_turn() — the Cancel affordance.
 */

import type { ReactNode } from "react";
import Avatar, { type AvatarVariant } from "../atoms/Avatar";
import Pill from "../atoms/Pill";

// #region Types

/**
 * Shape of the subagent metadata surfaced in the working strip when delegation
 * is active.
 *
 * Carries just enough to render the agent's avatar inline alongside the
 * activity message.
 */
interface WorkingStripAgent
{
  variant: AvatarVariant;
  initials: string;
}

/**
 * Props for the `WorkingStrip` component.
 *
 * Renders the inline progress strip shown while the agent is working, with
 * an activity message, optional subagent badge for delegated turns, and a
 * cancel control that can be omitted during cleanup or late-stage rendering.
 */
interface WorkingStripProps
{
  message?: ReactNode;
  agent?: WorkingStripAgent;
  onCancel?: () => void;
}

// #endregion

/**
 * Renders the strip. The component is presentational — wiring
 * `message`, `agent`, and `onCancel` is the consumer's responsibility.
 *
 * @param props - See {@link WorkingStripProps}.
 * @returns The strip element.
 */
export default function WorkingStrip({message = "Claude is thinking…", agent, onCancel,}: WorkingStripProps)
{
  return (
    <div
      className="flex shrink-0 items-center gap-3 border-t border-line px-[18px] py-2.5 text-[12.5px] text-txt-2"
      style={{
        background: "linear-gradient(90deg, rgba(123,92,255,0.06) 0%, rgba(76,201,255,0.04) 100%)",
      }}
      role="status"
      aria-live="polite"
    >
      <span
        aria-hidden="true"
        className="shrink-0 rounded-full"
        style={{
          width: 10,
          height: 10,
          background: "var(--cyan)",
          boxShadow: "0 0 10px var(--cyan)",
          animation: "pulse-soft 1.2s cubic-bezier(0.65, 0, 0.35, 1) infinite",
        }}
      />

      {agent !== undefined && <Avatar variant={agent.variant} initials={agent.initials} size={20} />}

      <span className="flex-1 min-w-0 truncate">{message}</span>

      {onCancel !== undefined && (
        <button
          type="button"
          onClick={onCancel}
          className="inline-flex items-center gap-2 font-mono text-[11px] text-txt-3 hover:text-txt-1 transition-colors duration-[120ms]"
          aria-label="Cancel current turn"
        >
          <Pill variant="subtle" size="sm">esc</Pill>
          <span>cancel</span>
        </button>
      )}
    </div>
  );
}