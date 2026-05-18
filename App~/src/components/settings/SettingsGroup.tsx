/**
 * Atomic settings primitives — `SettingsGroup` and `SettingRow`.
 *
 * Used by every Settings panel (Connection, Appearance, Claude Code,
 * Plugin, About) so they share a visual rhythm. Each group has a
 * top-aligned eyebrow label + a bordered surface containing rows.
 */

import type { ReactNode } from "react";

// #region SettingsGroup

/**
 * Props for the `SettingsGroup` component.
 *
 * Renders a labeled group of related settings controls inside the settings
 * panel, providing consistent heading and spacing around an arbitrary set of
 * child controls.
 */
interface SettingsGroupProps
{
  label: string;
  children: ReactNode;
}

/**
 * A bordered card with a visual section header. Holds N `SettingRow`s.
 *
 * @param props - See {@link SettingsGroupProps}.
 * @returns The group element.
 */
export function SettingsGroup({ label, children }: SettingsGroupProps)
{
  return (
    <div className="mb-9">
      <div className="flex items-center gap-2.5 mb-3">
        <span className="font-hud text-[10px] tracking-[0.22em] uppercase text-txt-3">
          {label}
        </span>
        <span className="flex-1 h-px bg-line" aria-hidden="true" />
      </div>
      <div className="rounded-r-3 border border-line bg-bg-2 overflow-hidden">
        {children}
      </div>
    </div>
  );
}

// #endregion

// #region SettingRow

/**
 * Props for the `SettingRow` component.
 *
 * Renders a single row inside a settings group, with slots for the setting's
 * label, supplementary metadata, current value, and an optional trailing
 * action control (e.g. an edit button or toggle).
 */
interface SettingRowProps
{
  label: ReactNode;
  meta?: ReactNode;
  value?: ReactNode;
  action?: ReactNode;
}

/**
 * Single row inside a `SettingsGroup`. Label on the left (fixed
 * width), value/control in the middle, optional action on the right.
 *
 * @param props - See {@link SettingRowProps}.
 * @returns The row element.
 */
export function SettingRow({ label, meta, value, action }: SettingRowProps)
{
  return (
    <div className="flex items-center gap-[18px] px-[18px] py-3.5 border-b border-line-soft last:border-b-0">
      <div className="shrink-0" style={{ width: 160 }}>
        <div className="text-[13px] text-txt-2">{label}</div>
        {meta !== undefined && (
          <div className="text-[11.5px] text-txt-4 mt-1 leading-snug">{meta}</div>
        )}
      </div>
      {value !== undefined && (
        <div className="flex-1 flex items-center min-w-0">{value}</div>
      )}
      {action !== undefined && (
        <div className="shrink-0">{action}</div>
      )}
    </div>
  );
}

// #endregion