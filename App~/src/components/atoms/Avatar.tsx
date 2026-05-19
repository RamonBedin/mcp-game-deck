/**
 * Squared agent avatar — 2-letter initials over the agent's accent color.
 *
 * Used for:
 *  - Assistant message headers (Claude or a specialist)
 *  - Subagent indicator strip when a delegated turn replies
 *  - `Library` grid rows
 *  - User messages (variant="user", neutral)
 *
 * Variant identity is fixed per agent — never invent a new color for a
 * new specialist. New specialists either use a registered variant or
 * fall back to `unity` (neutral light gray). The mapping lives in
 * tokens.css under `--ag-*`.
 */

// #region Types

/**
 * Identifies which preset avatar treatment to apply. `claude` is the
 * main assistant (brand gradient); the rest map to specialist accents.
 * `user` is the human-side fallback (neutral surface, lower contrast).
 */
export type AvatarVariant =
  | "claude"
  | "shader"
  | "ui"
  | "dots"
  | "perf"
  | "gameplay"
  | "unity"
  | "systems"
  | "techart"
  | "addr"
  | "qa"
  | "user";

/**
 * Props for the `Avatar` component.
 *
 * Renders a compact circular avatar showing user initials, with optional
 * visual variant and size overrides for the contexts where the default
 * styling doesn't fit.
 */
interface AvatarProps
{
  variant?: AvatarVariant;
  initials: string;
  size?: number;
}

// #endregion

// #region Variant map

const VARIANT_STYLES: Record<AvatarVariant, { background: string; color: string; border?: string }> = {
  claude:   { background: "var(--grad-brand)", color: "#ffffff" },
  shader:   { background: "var(--ag-shader)",  color: "#ffffff" },
  ui:       { background: "var(--ag-ui)",      color: "var(--bg-0)" },
  dots:     { background: "var(--ag-dots)",    color: "var(--bg-0)" },
  perf:     { background: "var(--ag-perf)",    color: "var(--bg-0)" },
  gameplay: { background: "var(--ag-gameplay)",color: "#ffffff" },
  unity:    { background: "var(--ag-unity)",   color: "var(--bg-0)" },
  systems:  { background: "var(--ag-systems)", color: "#ffffff" },
  techart:  { background: "var(--ag-techart)", color: "var(--bg-0)" },
  addr:     { background: "var(--ag-addr)",    color: "var(--bg-0)" },
  qa:       { background: "var(--ag-qa)",      color: "var(--bg-0)" },
  user:     { background: "var(--bg-4)",       color: "var(--txt-2)", border: "1px solid var(--line-hard)" },
};

// #endregion

/**
 * Renders the avatar at the requested size, centering the initials and
 * scaling the font with the box.
 *
 * @param props - See {@link AvatarProps}.
 * @returns The avatar element.
 */
export default function Avatar({ variant = "claude", initials, size = 28 }: AvatarProps)
{
  const v = VARIANT_STYLES[variant];
  const fontSize = Math.max(9, Math.round(size * 0.42));

  return (
    <span
      className="inline-flex items-center justify-center font-hud font-bold flex-shrink-0 rounded-r-2 leading-none select-none"
      style={{
        width:  size,
        height: size,
        fontSize,
        color:      v.color,
        background: v.background,
        border:     v.border,
      }}
    >
      {initials}
    </span>
  );
}