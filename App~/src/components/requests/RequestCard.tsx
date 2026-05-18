/**
 * Shared chrome for permission + question cards.
 *
 * v2.0 UX Pass rewrite. The previous `RequestCard` carried emoji
 * ("🛡️", "❓") and a left-border accent. The new card uses:
 *   - tier-aware accent (color drives border + bar)
 *   - Pill-based label (component vocabulary, not raw text)
 *   - footer band with separator, "keyboard shortcut hint" affordance
 *
 * Variant specifics (body markdown, footer buttons) still live in the
 * variant components — this base only owns the frame + state opacity
 * + interrupted caption + auto-allowed inline branch.
 */

import type { ReactNode } from "react";

// #region Types

/**
 * Visual lifecycle of a request card. The same four values the v1
 * card used — UX Pass keeps them stable so wire payloads don't change.
 */
export type RequestCardState = "pending" | "answered" | "interrupted" | "auto-allowed";

/**
 * Card flavor. Drives accent color and the icon label. `permission`
 * cards further specialize by tier (read/write/destr) — that happens
 * inside `PermissionRequestCard`, not here.
 */
export type RequestCardVariant = "permission" | "question";

/**
 * Color palette applied to the frame. Permission tier maps to
 * read/write/destr; question cards use a separate brand-violet
 * variant.
 */
export type RequestCardAccent = "read" | "write" | "destr" | "violet";

/**
 * Props for the `RequestCard` component.
 *
 * Drives the visual treatment, header, body, and footer slots of a single
 * agent request card — covering tool-use, permission prompts, questions, and
 * other variants surfaced during a conversation turn.
 */
interface RequestCardProps
{
  variant: RequestCardVariant;
  accent: RequestCardAccent;
  label: ReactNode;
  headerRight?: ReactNode;
  body: ReactNode;
  footer: ReactNode;
  footerHint?: ReactNode;
  state: RequestCardState;
}

// #endregion

// #region Accent table

const ACCENT_STYLES: Record<RequestCardAccent, { border: string; bar: string; shadow?: string }> = {
  read: {
    border: "rgba(91, 189, 255, 0.35)",
    bar:    "var(--info)",
  },
  write: {
    border: "rgba(245, 185, 70, 0.35)",
    bar:    "var(--warn)",
  },
  destr: {
    border: "rgba(255, 92, 122, 0.35)",
    bar:    "var(--bad)",
    shadow: "0 0 0 1px rgba(255,92,122,0.08), 0 8px 24px -8px rgba(255,92,122,0.2)",
  },
  violet: {
    border: "rgba(123, 92, 255, 0.35)",
    bar:    "var(--violet)",
  },
};

// #endregion

/**
 * Renders the card frame. Auto-allowed short-circuits into a compact
 * one-line caption — the consumer (`BlockView`) usually renders that
 * branch directly without instantiating `RequestCard`, but the
 * fallback keeps the type complete.
 *
 * @param props - See {@link RequestCardProps}.
 * @returns The card element, or a compact synthesis for `auto-allowed`.
 */
export default function RequestCard({variant, accent, label, headerRight, body, footer, footerHint, state,}: RequestCardProps)
{
  if (state === "auto-allowed")
  {
    return (
      <div className="text-[11.5px] font-mono text-txt-4 italic my-1 pl-2">
        ✓ auto-allowed
      </div>
    );
  }

  const a = ACCENT_STYLES[accent];
  const opacity = state === "answered" ? "opacity-70" : state === "interrupted"  ? "opacity-50" : "";

  return (
    <div
      className={`rounded-r-3 bg-bg-2 overflow-hidden ${opacity}`}
      style={{
        border:       `1px solid ${a.border}`,
        borderLeft:   `3px solid ${a.bar}`,
        boxShadow:    a.shadow,
      }}
      data-variant={variant}
    >
      {/* Header row */}
      <div className="px-[18px] pt-3.5 pb-3 flex items-center gap-2.5">
        <div className="flex items-center gap-2.5 min-w-0 flex-1">
          {label}
        </div>
        {headerRight !== undefined && (
          <div className="shrink-0">{headerRight}</div>
        )}
      </div>

      {/* Body */}
      <div className="px-[18px] pb-3">{body}</div>

      {/* Footer */}
      <div className="px-[18px] py-2.5 flex items-center gap-2 border-t border-line-soft bg-black/20">
        {footerHint !== undefined && (
          <span className="mr-auto font-mono text-[10.5px] text-txt-4 inline-flex items-center gap-1.5">
            {footerHint}
          </span>
        )}
        {/* If no hint, push footer items to the right by default. */}
        {footerHint === undefined && <span className="mr-auto" />}
        {footer}
      </div>

      {/* Interrupted caption */}
      {state === "interrupted" && (
        <div className="px-[18px] pt-2 pb-3 text-[12px] text-txt-4 italic border-t border-line-soft">
          Conversation interrupted — answer no longer applicable.
        </div>
      )}
    </div>
  );
}