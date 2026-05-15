/**
 * Rules list sidebar component for the Rules tab's left column.
 *
 * Pure controlled component: receives `rules`, `selectedName`, and
 * three callbacks (`onSelect`, `onNewRule`, `onToggle`) from the
 * consumer. The rules store is NOT read here — `RulesRoute`
 * (task 4.4) wires the props from `useRulesStore` selectors so this
 * same list could be reused at a different width in future surfaces
 * without coupling to a specific store.
 *
 * **Width:** intentionally not set on the component itself. The
 * consumer wraps the element in a `w-[250px]` (or whatever) container;
 * this component only commits to filling its parent vertically
 * (`h-full`) and scrolling internally.
 *
 * **Optimistic toggle:** clicking a row's checkbox flips a local
 * `Map<name, enabled>` immediately so the UI reflects the intent
 * before the round-trip resolves. The `rules-changed` watcher event
 * (subscribed in `useRulesSubscription`) eventually triggers a
 * `loadList` refetch in the store; when the resulting props arrive
 * with `enabled` matching the optimistic value, the entry is
 * dropped via `useEffect([rules])`. When `onToggle` resolves with
 * `{ok: false}` (cap reached, non-mapping frontmatter, IO error),
 * the optimistic entry is reverted immediately; surfacing the error
 * to the user is the route's responsibility (task 4.4 consolidated
 * the toast into a single route-level banner above both columns).
 *
 * Visual conventions mirror `PlansList` for cohesion across the
 * app's sidebars (header style, row chrome, active-state highlight).
 */

import { useEffect, useState } from "react";
import type { RuleMeta } from "../ipc/types";

// #region Constants

const ENABLED_CAP = 10;
const TOKENS_WARNING_THRESHOLD = 500;

// #endregion

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
 * Props for {@link RulesList}.
 *
 * `onToggle` returns the same `{ok, error?}` shape that
 * `rulesStore.toggleRule` surfaces; this component reverts the
 * optimistic entry on `{ok: false}` but does not render a toast —
 * the route consumer owns the user-facing error surface.
 */
interface RulesListProps
{
  rules: RuleMeta[];
  selectedName: string | null;
  onSelect: (name: string) => void;
  onNewRule: () => void;
  onToggle: (name: string, next: boolean) => Promise<{ ok: boolean; error?: string }>;
}

/**
 * Renders the rules sidebar: header with cap-aware count + estimated
 * token total, "+ New rule" button, and optimistic per-row enable
 * checkbox. Toggle failures revert the optimistic entry locally;
 * the route consumer surfaces the error.
 *
 * @param props - See {@link RulesListProps}.
 * @returns The sidebar element.
 */
export default function RulesList({ rules, selectedName, onSelect, onNewRule, onToggle, }: RulesListProps)
{
  const [optimistic, setOptimistic] = useState<Map<string, boolean>>(new Map());

  // Reconcile optimistic entries when fresh props arrive (watcher event →
  // loadList → new `rules`). Any entry whose props value matches the
  // optimistic intent gets dropped.
  useEffect(() => {
    setOptimistic((prev) => {
      if (prev.size === 0)
      {
        return prev;
      }

      const next = new Map(prev);
      let changed = false;

      for (const r of rules)
      {
        if (next.has(r.name) && next.get(r.name) === r.enabled)
        {
          next.delete(r.name);
          changed = true;
        }
      }
      return changed ? next : prev;
    });
  }, [rules]);

  const isEnabled = (r: RuleMeta): boolean => optimistic.get(r.name) ?? r.enabled;

  const enabledCount = rules.filter(isEnabled).length;
  const totalTokens = rules.reduce((sum, r) => sum + (isEnabled(r) ? r.estimatedTokens : 0), 0,);

  const handleToggle = async (rule: RuleMeta): Promise<void> => {
    const next = !isEnabled(rule);
    setOptimistic((prev) => new Map(prev).set(rule.name, next));
    const result = await onToggle(rule.name, next);

    if (!result.ok)
    {
      setOptimistic((prev) => {
        const m = new Map(prev);
        m.delete(rule.name);
        return m;
      });
    }
  };

  return (
    <div className="flex h-full flex-col">
      <h2 className="mb-2 text-xs font-semibold uppercase tracking-wider text-slate-500">
        Rules ({enabledCount}/{ENABLED_CAP} enabled, ~{totalTokens} tokens)
      </h2>

      <button
        type="button"
        onClick={onNewRule}
        className="mb-3 rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
      >
        + New rule
      </button>

      <div className="flex-1 space-y-1 overflow-y-auto pr-1">
        {rules.length === 0 ? (
          <p className="p-4 text-center text-xs text-slate-500">
            No rules yet. Click <span className="font-semibold">+ New rule</span> to create one,
            or drop a markdown file into{" "}
            <code>ProjectSettings/GameDeck/rules/</code>.
          </p>
        ) : (
          rules.map((r) => {
            const enabled = isEnabled(r);
            const active = r.name === selectedName;
            const warn = r.estimatedTokens > TOKENS_WARNING_THRESHOLD;
            return (
              <div key={r.name} className="flex gap-1">
                <button
                  type="button"
                  onClick={() => { void handleToggle(r); }}
                  aria-label={enabled ? `Disable ${r.name}` : `Enable ${r.name}`}
                  className={`shrink-0 rounded px-1.5 py-1 text-sm transition-colors ${
                    enabled
                      ? "text-sky-300 hover:text-sky-100"
                      : "text-slate-500 hover:text-slate-300"
                  }`}
                >
                  {enabled ? "☑" : "☐"}
                </button>
                <button
                  type="button"
                  onClick={() => onSelect(r.name)}
                  className={`flex-1 rounded px-2 py-1.5 text-left text-xs transition-colors ${
                    active
                      ? "border border-sky-700/60 bg-sky-900/40 text-sky-100"
                      : "border border-transparent bg-slate-800/40 text-slate-300 hover:border-slate-700 hover:bg-slate-800"
                  } ${enabled ? "" : "opacity-60"}`}
                >
                  <div className="truncate font-medium">{r.name}</div>
                  {r.description !== null && (
                    <div className="mt-0.5 truncate text-[10px] text-slate-500">
                      {r.description}
                    </div>
                  )}
                  <div className="mt-0.5 flex items-center gap-1 text-[10px] text-slate-500">
                    {warn && (
                      <span
                        className="text-yellow-400"
                        title="Estimated above 500 tokens"
                      >
                        ⚠
                      </span>
                    )}
                    <span>~{r.estimatedTokens} tokens</span>
                    <span>·</span>
                    <span>{formatRelative(r.lastModified)}</span>
                    {enabled ? null : (
                      <>
                        <span>·</span>
                        <span>disabled</span>
                      </>
                    )}
                  </div>
                </button>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}

// #endregion