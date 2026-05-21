/**
 * Library tab switcher — Agents · Commands · Knowledge.
 *
 * Pure controlled component. Source-of-truth for which tab is active
 * lives in the consumer (LibraryRoute), not here. Each tab carries a
 * count badge derived from `useAgents`, `useCommands`, and the
 * (forthcoming) knowledge-docs hook.
 */

import type { LibraryTab } from "../../routes/LibraryRoute";

// #region Types

/**
 * Props for the `LibraryTabs` component.
 *
 * Renders the tab bar at the top of the library panel, surfacing the active
 * tab, per-tab entry counts, and a free-text search field whose value is
 * lifted into the parent for cross-tab filtering.
 */
interface LibraryTabsProps
{
  active: LibraryTab;
  onChange: (tab: LibraryTab) => void;
  counts: Record<LibraryTab, number>;
  searchQuery: string;
  onSearchChange: (next: string) => void;
}

// #endregion

const TABS: ReadonlyArray<{ id: LibraryTab; label: string }> = [
  { id: "agents",    label: "Agents" },
  { id: "commands",  label: "Commands" },
  { id: "knowledge", label: "Knowledge" },
];

/**
 * Renders the tab strip.
 *
 * @param props - See {@link LibraryTabsProps}.
 * @returns The tabs element.
 */
export default function LibraryTabs({active, onChange, counts, searchQuery, onSearchChange,}: LibraryTabsProps)
{
  return (
    <div className="flex border-b border-line bg-bg-2 px-7 gap-0.5 shrink-0">
      {TABS.map((t) => {
        const isActive = t.id === active;
        return (
          <button
            type="button"
            key={t.id}
            onClick={() => onChange(t.id)}
            className={[
              "flex items-center gap-1.5 px-4 pt-3.5 pb-2.5 text-[13px] -mb-px transition-colors duration-[120ms]",
              isActive
                ? "text-txt-1 font-medium border-b-2 border-brand-violet"
                : "text-txt-3 border-b-2 border-transparent hover:text-txt-2",
            ].join(" ")}
          >
            {t.label}
            <span
              className={[
                "font-mono text-[10px] px-1.5 py-px rounded-full",
                isActive ? "bg-brand-violet/10 text-brand-violet-soft" : "bg-bg-3 text-txt-4",
              ].join(" ")}
            >
              {counts[t.id]}
            </span>
          </button>
        );
      })}
      <div className="ml-auto flex items-center pb-2">
        <input
          type="search"
          value={searchQuery}
          onChange={(e) => onSearchChange(e.target.value)}
          placeholder="Search…"
          className="rounded-r-2 bg-bg-1 px-2.5 py-1.5 text-[12px] text-txt-1 border border-line-hard outline-none focus:border-brand-violet focus:focus-ring"
          style={{ width: 200 }}
        />
      </div>
    </div>
  );
}