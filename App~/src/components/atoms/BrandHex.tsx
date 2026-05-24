/**
 * Brand mark — the MCP Game Deck hexagon icon.
 *
 * Composes the flat-top hexagon outline + D-pad cross + 2 action dots
 * from the brand guide. The single SVG `<symbol>` is referenced via
 * `<use>` from every consumer so all instances share one source. Pair
 * with {@link BrandGradientDefs} mounted once at the app root for the
 * default gradient stroke; standalone instances can pass `mode="mono"`
 * or `mode="white"` to skip the gradient dependency.
 *
 * @example
 *   <BrandGradientDefs />     // once at <App> root
 *   <BrandHex size={32} />    // anywhere; uses gradient
 *   <BrandHex size={14} mode="mono" />
 */

// #region Types

type BrandHexMode = "gradient" | "mono" | "white";

/**
 * Props for the `BrandHex` component.
 *
 * Renders the hexagonal brand mark with optional size and rendering-mode
 * overrides for the contexts where the default styling doesn't fit.
 */
interface BrandHexProps
{
  size?: number;
  mode?: BrandHexMode;
}

// #endregion

/**
 * Renders the hex brand mark at the requested size.
 *
 * @param props - See {@link BrandHexProps}.
 * @returns The SVG element.
 */
export default function BrandHex({ size = 24, mode = "gradient" }: BrandHexProps)
{
  let strokeFill: string;

  if (mode === "white")
  {
    strokeFill = "#ffffff";
  }
  else if (mode === "mono")
  {
    strokeFill = "currentColor";
  }
  else
  {
    strokeFill = "url(#mk-brand-grad)";
  }

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 200 200"
      style={{ display: "block" }}
      aria-hidden="true"
    >
      <polygon
        points="192,100 146,180 54,180 8,100 54,20 146,20"
        fill="none"
        stroke={strokeFill}
        strokeWidth="10"
        strokeLinejoin="round"
      />
      <g transform="translate(72 100)" fill={strokeFill}>
        <rect x="-26" y="-9" width="52" height="18" rx="3" />
        <rect x="-9" y="-26" width="18" height="52" rx="3" />
      </g>
      <circle cx="120" cy="92" r="6.5" fill={strokeFill} />
      <circle cx="138" cy="108" r="6.5" fill={strokeFill} />
    </svg>
  );
}

/**
 * Mounts the brand gradient once at the React root. Sibling
 * `<BrandHex mode="gradient">` instances reference it via
 * `url(#mk-brand-grad)`.
 *
 * The component renders an invisible 0×0 SVG container that holds the
 * `<defs>`; placing it under `<App>` (or anywhere a stable ancestor of
 * every consumer) is sufficient.
 *
 * @returns The hidden `<defs>` container.
 */
export function BrandGradientDefs()
{
  return (
    <svg
      width="0"
      height="0"
      style={{ position: "absolute", pointerEvents: "none" }}
      aria-hidden="true"
    >
      <defs>
        <linearGradient id="mk-brand-grad" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#9D7CFF" />
          <stop offset="100%" stopColor="#4CC9FF" />
        </linearGradient>
      </defs>
    </svg>
  );
}