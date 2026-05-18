/**
 * Inline pill / badge — uppercase mono caption inside a colored capsule.
 *
 * Two main jobs:
 *  1. **Tier indicators** on permission cards (`tier-read`, `tier-write`,
 *     `tier-destr`).
 *  2. **Source/version tags** in lists and toolbars (`subtle`, `brand`,
 *     `ok`, `cyan`, default).
 *
 * Variants are intentionally limited; if a screen needs something
 * different, add a new variant here rather than overriding via className
 * — keeping the design vocabulary closed prevents drift.
 */

import type { ReactNode } from "react";

// #region Types

export type PillVariant =
  | "default"
  | "subtle"
  | "brand"
  | "ok"
  | "cyan"
  | "tier-read"
  | "tier-write"
  | "tier-destr";

export type PillSize = "sm" | "md" | "lg";

/**
 * Props for the `Pill` component.
 *
 * Renders a small rounded badge for status, count, or tag labels, with
 * optional variant, size, and a leading status dot for at-a-glance state
 * cues.
 */
interface PillProps
{
  children: ReactNode;
  variant?: PillVariant;
  size?: PillSize;
  dot?: boolean;
}

// #endregion

// #region Variant tables

const VARIANT_STYLES: Record<PillVariant, { color: string; background: string; border: string }> = {
  default:      { color: "var(--txt-2)",     background: "var(--bg-3)",                  border: "var(--line)" },
  subtle:       { color: "var(--txt-3)",     background: "transparent",                  border: "var(--line)" },
  brand:        { color: "var(--violet-soft)", background: "rgba(123, 92, 255, 0.10)",   border: "rgba(123, 92, 255, 0.30)" },
  ok:           { color: "var(--ok)",        background: "rgba(74, 222, 128, 0.08)",     border: "rgba(74, 222, 128, 0.30)" },
  cyan:         { color: "var(--cyan)",      background: "rgba(76, 201, 255, 0.08)",     border: "rgba(76, 201, 255, 0.30)" },
  "tier-read":  { color: "var(--tier-read)", background: "rgba(91, 189, 255, 0.08)",     border: "rgba(91, 189, 255, 0.30)" },
  "tier-write": { color: "var(--tier-write)",background: "rgba(245, 185, 70, 0.08)",     border: "rgba(245, 185, 70, 0.30)" },
  "tier-destr": { color: "var(--tier-destr)",background: "rgba(255, 92, 122, 0.08)",     border: "rgba(255, 92, 122, 0.30)" },
};

const SIZE_STYLES: Record<PillSize, { fontSize: number; padding: string; letterSpacing: string }> = {
  sm: { fontSize: 9,  padding: "1px 6px",  letterSpacing: "0.08em" },
  md: { fontSize: 10, padding: "2px 8px",  letterSpacing: "0.08em" },
  lg: { fontSize: 11, padding: "3px 10px", letterSpacing: "0.06em" },
};

// #endregion

/**
 * Renders a pill. Children are rendered verbatim (the pill applies
 * uppercase + tracking to them but doesn't coerce content type).
 *
 * @param props - See {@link PillProps}.
 * @returns The pill element.
 */
export default function Pill({ children, variant = "default", size = "md", dot = false }: PillProps)
{
  const v = VARIANT_STYLES[variant];
  const sz = SIZE_STYLES[size];

  return (
    <span
      className="inline-flex items-center gap-[5px] font-mono uppercase font-medium whitespace-nowrap rounded-full leading-snug"
      style={{
        color:         v.color,
        background:    v.background,
        border:        `1px solid ${v.border}`,
        fontSize:      sz.fontSize,
        padding:       sz.padding,
        letterSpacing: sz.letterSpacing,
      }}
    >
      {dot && (
        <span
          aria-hidden="true"
          style={{
            width: 5,
            height: 5,
            borderRadius: "50%",
            background: "currentColor",
            flexShrink: 0,
          }}
        />
      )}
      {children}
    </span>
  );
}