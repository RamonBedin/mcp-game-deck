/**
 * Buttons — the four canonical variants of the v2.0 design system.
 *
 *   default     gray surface · neutral · most common
 *   primary     brand gradient · reserved for primary CTAs (Send, Resume, Execute)
 *   ghost       transparent · for secondary actions inside dense rows
 *   danger      red-filled · destructive permission "Allow"
 *   destructive red-outlined · destructive list actions (Delete plan, etc)
 *
 * Compose a leading icon via `icon` prop. The icon slot is just a
 * span — pass any ReactNode (SVG, emoji, glyph).
 *
 * Resist adding `variant="warning"` or `"success"`. Status is conveyed
 * by `Pill` / `StatusDot`, never by button color.
 */

import type { ButtonHTMLAttributes, ReactNode } from "react";

// #region Types

export type ButtonVariant = "default" | "primary" | "ghost" | "danger" | "destructive";
export type ButtonSize = "sm" | "md" | "lg";

/**
 * Props for the `Button` component.
 *
 * Wraps the native `<button>` element with the design system's variant and
 * size tokens, plus an optional leading icon slot. All standard button HTML
 * attributes are passed through, except `size`, which is overridden to carry
 * the design-system token instead of the native pixel value.
 */
interface ButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "size">
{
  variant?: ButtonVariant;
  size?: ButtonSize;
  icon?: ReactNode;
}

// #endregion

// #region Variant + size tables

const VARIANT_CLASSES: Record<ButtonVariant, string> = {
  default:     "bg-bg-3 text-txt-1 border border-line-hard hover:bg-bg-4 hover:border-bg-5",
  primary:     "bg-grad-brand text-white border border-brand-violet/30 shadow-glow-brand hover:shadow-elev-3",
  ghost:       "bg-transparent text-txt-2 border border-transparent hover:bg-bg-3 hover:text-txt-1",
  danger:      "bg-bad text-white border border-transparent hover:opacity-90",
  destructive: "bg-bad/10 text-bad border border-bad/30 hover:bg-bad/20",
};

const SIZE_CLASSES: Record<ButtonSize, string> = {
  sm: "px-2.5 py-1 text-[11.5px] gap-1.5",
  md: "px-3.5 py-1.5 text-[13px] gap-2",
  lg: "px-6 py-2.5 text-[14px] gap-2",
};

// #endregion

/**
 * Renders a button with the requested variant and size. Falls through
 * any extra HTML button props (onClick, disabled, type, aria-*, etc).
 *
 * @param props - See {@link ButtonProps}.
 * @returns The button element.
 */
export default function Button({variant = "default", size = "md", icon, children, className = "", type = "button", ...rest}: ButtonProps)
{
  const variantClass = VARIANT_CLASSES[variant];
  const sizeClass = SIZE_CLASSES[size];

  return (
    <button
      type={type}
      {...rest}
      className={[
        "inline-flex items-center justify-center font-medium font-body rounded-r-2 transition-all duration-[120ms]",
        "disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-current",
        variantClass,
        sizeClass,
        className,
      ].join(" ")}
    >
      {icon !== undefined && <span className="inline-flex">{icon}</span>}
      {children}
    </button>
  );
}