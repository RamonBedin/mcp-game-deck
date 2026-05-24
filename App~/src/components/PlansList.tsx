/**
 * Plans list sidebar component for the Plans tab's left column.
 *
 * Pure controlled component: receives `plans`, `selectedName`, and
 * two callbacks (`onSelect`, `onNewPlan`) from the consumer. The
 * plans store is NOT read here — `PlansRoute` (task 2.4) wires the
 * props from `usePlansStore` selectors so this same list can be
 * reused at a different width in future surfaces without coupling to
 * a specific store.
 *
 * **Width:** intentionally not set on the component itself. The
 * consumer wraps the element in a `w-[250px]` (or whatever) container;
 * this component only commits to filling its parent vertically
 * (`h-full`) and scrolling internally.
 *
 * Visual conventions mirror `SessionList` for cohesion across the
 * app's sidebars (header style, row chrome, active-state highlight).
 */

import type { PlanMeta } from "../ipc/types";

// #region Helpers

const formatRelative = (millis: number): string => {
  if (millis <= 0)
  {
    return "—";
  }

  const diff = Date.now() - millis;
  const secondsAgo = Math.floor(diff / 1000);

  if (secondsAgo < 60)
  {
    return "just now";
  }

  const minutes = Math.floor(secondsAgo / 60);

  if (minutes < 60)
  {
    return `${minutes}m ago`;
  }

  const hours = Math.floor(minutes / 60);

  if (hours < 24)
  {
    return `${hours}h ago`;
  }

  const days = Math.floor(hours / 24);

  if (days < 30)
  {
    return `${days}d ago`;
  }

  const months = Math.floor(days / 30);
  return `${months}mo ago`;
};

// #endregion

// #region Component

/**
 * Props for {@link PlansList}.
 */
interface PlansListProps
{
  plans: PlanMeta[];
  selectedName: string | null;
  onSelect: (name: string) => void;
  onNewPlan: () => void;
}

/**
 * Renders the plans sidebar: header with "+ New plan" button, then
 * either the empty-state hint or the scrollable list of plan rows.
 *
 * @param props - See {@link PlansListProps}.
 * @returns The sidebar element.
 */
export default function PlansList({plans, selectedName, onSelect, onNewPlan,}: PlansListProps)
{
  return (
    <div className="flex h-full flex-col">
      <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
        Plans
      </h2>

      <button
        type="button"
        onClick={onNewPlan}
        className="mb-3 rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
      >
        + New plan
      </button>

      <div className="flex-1 space-y-1 overflow-y-auto pr-1">
        {plans.length === 0 ? (
          <p className="p-4 text-center text-xs text-slate-500">
            No plans yet. After Claude generates a plan in plan mode,
            run <code>/save-plan</code> to capture it.
          </p>
        ) : (
          plans.map((p) => {
            const active = p.name === selectedName;
            return (
              <button
                key={p.name}
                type="button"
                onClick={() => onSelect(p.name)}
                className={`w-full rounded px-2 py-1.5 text-left text-xs transition-colors ${
                  active
                    ? "border border-sky-700/60 bg-sky-900/40 text-sky-100"
                    : "border border-transparent bg-slate-800/40 text-slate-300 hover:border-slate-700 hover:bg-slate-800"
                }`}
              >
                <div className="truncate font-medium">{p.name}</div>
                <div className="mt-0.5 flex justify-between gap-2 text-[10px] text-slate-500">
                  <span className="truncate">
                    {p.description ?? ""}
                  </span>
                  <span className="shrink-0">
                    {formatRelative(p.lastModified)}
                  </span>
                </div>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}

// #endregion