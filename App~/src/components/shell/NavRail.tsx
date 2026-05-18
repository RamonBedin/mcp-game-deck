/**
 * Left navigation rail.
 *
 * Five primary destinations grouped into three sections:
 *   Workspace  — Chat · Plans · Rules
 *   Knowledge  — Library     (new in v2.0 UX Pass)
 *   System     — Settings
 *
 * Each item is a <NavLink> driving react-router. Badges (string or
 * number) sit on the right; the active item shows a left accent bar
 * in brand violet.
 *
 * Badge sources (consumer wires them):
 *  - Chat: pulse dot when an assistant turn is streaming.
 *  - Plans: count of plans (cached in `plansStore`).
 *  - Rules: "enabled/cap" mini-fraction.
 *  - Library: optional "new" pill when fresh content lands.
 */

import { NavLink } from "react-router-dom";
import type { ReactNode } from "react";
import { useCollapsedColumn } from "../../hooks/useCollapsedColumn";

// #region Types

/**
 * Props for the `NavRail` component.
 *
 * Renders the persistent left-side navigation rail and accepts optional badge
 * overrides — typically used to surface unread counts or status indicators
 * next to specific destinations.
 */
interface NavRailProps
{
  badges?: Partial<Record<NavId, ReactNode>>;
}

type NavId = "chat" | "plans" | "rules" | "library" | "settings";

/**
 * Definition of a single entry in the navigation rail.
 *
 * Pairs the destination identifier with its router path, display label,
 * optional section grouping, and the icon rendered alongside the label.
 */
interface NavSpec
{
  id: NavId;
  to: string;
  label: string;
  section: string | null;
  icon: NavIconName;
}

// #endregion

// #region Nav config

const NAV_ITEMS: readonly NavSpec[] = [
  { id: "chat",     to: "/chat",     label: "Chat",     section: "Workspace", icon: "chat" },
  { id: "plans",    to: "/plans",    label: "Plans",    section: null,        icon: "plans" },
  { id: "rules",    to: "/rules",    label: "Rules",    section: null,        icon: "rules" },
  { id: "library",  to: "/library",  label: "Library",  section: "Knowledge", icon: "library" },
  { id: "settings", to: "/settings", label: "Settings", section: "System",    icon: "settings" },
];

// #endregion

/**
 * Renders the navigation rail with all five items and badges.
 *
 * @param props - See {@link NavRailProps}.
 * @returns The rail element.
 */
export default function NavRail({ badges = {} }: NavRailProps)
{
  const [collapsed, toggleCollapsed] = useCollapsedColumn("nav-rail");

  return (
    <aside
      className="shrink-0 flex flex-col gap-0.5 border-r border-line bg-bg-0 py-4 transition-[width] duration-[200ms]"
      style={{ width: collapsed ? 56 : 200, paddingLeft: collapsed ? 8 : 12, paddingRight: collapsed ? 8 : 12 }}
    >
      <CollapseToggle collapsed={collapsed} onToggle={toggleCollapsed} />

      {NAV_ITEMS.map((item, idx) => (
        <div key={item.id}>
          {!collapsed && item.section !== null && (
            <div
              className="font-hud text-[9px] uppercase tracking-[0.18em] text-txt-4 px-2"
              style={{ margin: idx === 0 ? "0 0 6px" : "16px 0 6px" }}
            >
              {item.section}
            </div>
          )}
          {collapsed && item.section !== null && idx !== 0 && (
            <div className="h-2.5" aria-hidden="true" />
          )}
          <NavItem
            to={item.to}
            label={item.label}
            icon={item.icon}
            badge={badges[item.id]}
            collapsed={collapsed}
          />
        </div>
      ))}
    </aside>
  );
}

/**
 * Props for the `CollapseToggle` component.
 *
 * Renders a toggle control for expanding or collapsing an adjacent panel,
 * reflecting the current state in its iconography and reporting toggle
 * requests back to the parent.
 */
interface CollapseToggleProps
{
  collapsed: boolean;
  onToggle: () => void;
}

const CollapseToggle = ({ collapsed, onToggle }: CollapseToggleProps) => (
  <button
    type="button"
    onClick={onToggle}
    title={collapsed ? "Expand sidebar" : "Collapse sidebar"}
    aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"}
    className="self-end mb-3 inline-flex items-center justify-center w-6 h-6 rounded-r-1 text-txt-4 hover:text-txt-1 hover:bg-bg-3 transition-colors duration-[120ms]"
  >
    <span style={{ fontSize: 11 }}>{collapsed ? "›" : "‹"}</span>
  </button>
);

// #region NavItem

/**
 * Props for the `NavItem` component.
 *
 * Renders a single navigation entry inside the nav rail, with an icon, label,
 * optional badge, and a collapsed mode that hides the label to leave only
 * the icon visible.
 */
interface NavItemProps
{
  to: string;
  label: string;
  icon: NavIconName;
  badge?: ReactNode;
  collapsed?: boolean;
}

const NavItem = ({ to, label, icon, badge, collapsed = false }: NavItemProps) => (
  <NavLink
    to={to}
    title={collapsed ? label : undefined}
    className={({ isActive }) =>
      [
        "flex items-center rounded-r-1 transition-colors duration-[120ms]",
        collapsed
          ? "justify-center py-2"
          : "gap-2.5 px-2.5 py-2 text-[13px]",
        isActive
          ? "bg-bg-3 text-txt-1 font-medium shadow-[inset_2px_0_0_var(--violet)]"
          : "text-txt-2 hover:bg-bg-3/60 hover:text-txt-1",
      ].join(" ")
    }
  >
    {({ isActive }) => (
      <>
        <NavIcon name={icon} color={isActive ? "var(--violet-soft)" : "var(--txt-3)"} />
        {!collapsed && <span className="flex-1">{label}</span>}
        {!collapsed && badge !== undefined && badge !== null && (
          <NavBadge active={isActive}>{badge}</NavBadge>
        )}
      </>
    )}
  </NavLink>
);

// #endregion

// #region NavBadge

/**
 * Props for the `NavBadge` component.
 *
 * Renders a small badge attached to a nav entry — typically an unread count
 * or status indicator — with active-state styling for the entry currently in
 * focus.
 */
interface NavBadgeProps
{
  active: boolean;
  children: ReactNode;
}

const NavBadge = ({ active, children }: NavBadgeProps) => {
  const cls = active ? "bg-brand-violet text-white" : "bg-bg-4 text-txt-3";

  return (
    <span
      className={[
        "font-mono text-[9.5px] leading-snug min-w-[16px] text-center px-1.5 py-[1px] rounded-full",
        cls,
      ].join(" ")}
    >
      {children}
    </span>
  );
};

// #endregion

// #region Icons

type NavIconName = "chat" | "plans" | "rules" | "library" | "settings";

/**
 * Props for the `NavIcon` component.
 *
 * Renders one of the named nav-rail icons by identifier, with an optional
 * color override for contexts where the inherited token isn't appropriate.
 */
interface NavIconProps
{
  name: NavIconName;
  color?: string;
}

const NavIcon = ({ name, color = "currentColor" }: NavIconProps) => {
  const common = {
    width: 16,
    height: 16,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: color,
    strokeWidth: 1.75,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    style: { flexShrink: 0 },
  };

  switch (name)
  {
    case "chat":
      return (
        <svg {...common}>
          <path d="M4 5h16v12H8l-4 4V5z" />
        </svg>
      );
    case "plans":
      return (
        <svg {...common}>
          <rect x="4" y="4" width="16" height="16" rx="2" />
          <path d="M8 9h8M8 13h6M8 17h4" />
        </svg>
      );
    case "rules":
      return (
        <svg {...common}>
          <path d="M5 5h14v3H5zM5 11h14v3H5zM5 17h10v3H5z" />
        </svg>
      );
    case "library":
      return (
        <svg {...common}>
          <path d="M4 4h6v16H4zM10 8h6v12h-6zM16 12h4v8h-4z" />
        </svg>
      );
    case "settings":
      return (
        <svg {...common}>
          <circle cx="12" cy="12" r="3" />
          <path d="M12 3v2M12 19v2M3 12h2M19 12h2M5.6 5.6l1.4 1.4M17 17l1.4 1.4M5.6 18.4L7 17M17 7l1.4-1.4" />
        </svg>
      );
  }
};

// #endregion