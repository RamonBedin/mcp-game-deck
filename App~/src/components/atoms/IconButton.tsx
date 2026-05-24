/**
 * Square icon button — fixed 28×28 by default, transparent background,
 * subtle hover. Used for refresh, close, expand/collapse, menus,
 * project-switcher trigger, etc.
 *
 * Children should be a single icon (SVG or glyph). No text — pair with
 * `aria-label` for screen readers.
 */

import type { ButtonHTMLAttributes, ReactNode } from "react";

// #region Types

/**
 * Props for the `IconButton` component.
 *
 * Wraps the native `<button>` element to render a square, icon-only control.
 * All standard button HTML attributes are passed through, except `size`,
 * which is overridden to carry the icon's pixel diameter instead of the
 * native string value.
 */
interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "size">
{
  size?: number;
  children: ReactNode;
}

// #endregion

/**
 * Renders the icon button. Falls through onClick, disabled, aria-label,
 * etc to the underlying `<button>`.
 *
 * @param props - See {@link IconButtonProps}.
 * @returns The button element.
 */
export default function IconButton({size = 28, children, className = "", type = "button", ...rest}: IconButtonProps)
{
  return (
    <button
      type={type}
      {...rest}
      className={[
        "inline-flex items-center justify-center bg-transparent border-none rounded-r-2",
        "text-txt-3 hover:bg-bg-3 hover:text-txt-1 transition-colors duration-[120ms]",
        "disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent",
        className,
      ].join(" ")}
      style={{ width: size, height: size }}
    >
      {children}
    </button>
  );
}