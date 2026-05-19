/**
 * Status indicator dot with optional caption.
 *
 * Surfaces a ternary connection state — ok / busy / down / idle — as a
 * small colored dot with a subtle glow (except `idle`, which is muted).
 * `busy` animates with a soft pulse to communicate "actively processing".
 *
 * Drives:
 *  - `HudStrip` for Unity + Supervisor status
 *  - `NavRail` per-item connection indicator
 *  - `Settings → Connection` panel rows
 *  - `FirstRun` step-detection caption ("detecting sign-in…")
 *
 * The mapping from app concepts to status values is the caller's job:
 *   ConnectionStatus → "connected" maps to "ok", "busy" stays "busy",
 *   "disconnected" maps to "down".
 *   SupervisorStatus → "ready" → "ok"; "starting" → "busy";
 *   "crashed"/"failed" → "down"; "idle" → "idle".
 */

// #region Types

/**
 * Visual status the dot displays. Keep this enum independent of the
 * app's `ConnectionStatus` / `SupervisorStatus` so the atom stays
 * reusable for non-connection statuses (e.g. plan execution state).
 */
export type DotStatus = "ok" | "busy" | "down" | "idle";

/**
 * Props for the `StatusDot` component.
 *
 * Renders a small colored dot indicating a status state, with optional
 * accessible label, size override, and a glow effect for emphasis.
 */
interface StatusDotProps
{
  status?: DotStatus;
  label?: string;
  size?: number;
  glow?: boolean;
}

// #endregion

// #region Helpers

const STATUS_COLOR: Record<DotStatus, string> = {
  ok:   "var(--ok)",
  busy: "var(--warn)",
  down: "var(--bad)",
  idle: "var(--txt-4)",
};

// #endregion

/**
 * Renders the dot. When `label` is present, wraps both dot and label
 * in an inline-flex span so calling sites can drop it into nav rows
 * and HUDs without extra layout glue.
 *
 * @param props - See {@link StatusDotProps}.
 * @returns The status-dot element.
 */
export default function StatusDot({status = "ok", label, size = 8, glow = true,}: StatusDotProps)
{
  const color = STATUS_COLOR[status];
  const showGlow = glow && status !== "idle";
  const isBusy = status === "busy";

  const dot = (
    <span
      style={{
        width: size,
        height: size,
        borderRadius: "50%",
        background: color,
        boxShadow: showGlow ? `0 0 6px ${color}` : "none",
        animation: isBusy
          ? "pulse-soft 1.2s cubic-bezier(0.65, 0, 0.35, 1) infinite"
          : "none",
        flexShrink: 0,
        display: "inline-block",
      }}
      aria-hidden="true"
    />
  );

  if (label === undefined)
  {
    return dot;
  }

  return (
    <span className="inline-flex items-center gap-[7px] font-mono text-[11px] leading-none text-txt-2">
      {dot}
      <span>{label}</span>
    </span>
  );
}