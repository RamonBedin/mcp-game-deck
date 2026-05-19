/**
 * Right-pane orchestrator for the Rules tab.
 *
 * Pure controlled component (mirror of `PlanPane`): all CRUD state
 * lives in `rulesStore`; props are plumbed through by `RulesRoute`
 * (task 4.4). Local state is only for the transient
 * delete-confirmation flow, the pane-local optimistic toggle, and
 * the pane-local toast.
 *
 * Render states:
 * - `rule === null`: centered empty-state hint per spec.
 * - `rule !== null && !editMode`: header `[Toggle ☑/☐] [Delete |
 *   Yes,delete + Cancel] [View | Edit]` plus the markdown viewer.
 * - `rule !== null && editMode`: header `[Save (primary)] [Cancel]`
 *   plus the monospace editor.
 *
 * **Optimistic toggle:** the pane's Toggle button flips a local
 * `boolean | null` that overrides `rule.frontmatter.enabled` for
 * the visual state until either the active rule changes
 * (navigation), the edit mode changes, or the action returns
 * `{ok: false}` (in which case we revert). Task 4.4 consolidated
 * the toggle-error toast at the route level above both columns;
 * this component no longer renders one.
 *
 * The `confirmingDelete` flag resets on any transition of the
 * active rule or the edit mode, mirroring `PlanPane`.
 */

import { useEffect, useState } from "react";
import type { Rule } from "../ipc/types";
import RuleEditor from "./RuleEditor";
import RuleViewer from "./RuleViewer";

// #region Component

/**
 * Props for {@link RulePane}.
 */
interface RulePaneProps
{
  rule: Rule | null;
  editMode: boolean;
  editDraft: string | null;
  onEnterEdit: () => void;
  onCancelEdit: () => void;
  onSaveEdit: () => void;
  onChangeDraft: (next: string) => void;
  onDelete: () => void;
  onToggle: (name: string, next: boolean) => Promise<{ ok: boolean; error?: string }>;
}

/**
 * Renders the pane: empty state, viewer + action header, or editor +
 * save/cancel header — picked from the controlled props.
 *
 * @param props - See {@link RulePaneProps}.
 * @returns The pane element, sized to fill its flex parent.
 */
export default function RulePane({rule, editMode, editDraft, onEnterEdit, onCancelEdit, onSaveEdit, onChangeDraft, onDelete, onToggle,}: RulePaneProps)
{
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [optimisticEnabled, setOptimisticEnabled] = useState<boolean | null>(null);

  useEffect(() => {
    setConfirmingDelete(false);
    setOptimisticEnabled(null);
  }, [rule?.name, editMode]);

  if (rule === null)
  {
    return (
      <div className="flex h-full flex-col">
        <p className="m-auto p-4 text-center text-sm text-slate-500">
          Select a rule, or add a new one to give Claude project-specific
          instructions that persist across conversations.
        </p>
      </div>
    );
  }

  const propsEnabled = rule.frontmatter.enabled === true;
  const effectiveEnabled = optimisticEnabled ?? propsEnabled;

  const handleToggle = async (): Promise<void> => {
    const next = !effectiveEnabled;
    setOptimisticEnabled(next);
    const result = await onToggle(rule.name, next);
    if (!result.ok)
    {
      setOptimisticEnabled(null);
    }
  };

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-2 border-b border-slate-800 p-3">
        <h2 className="truncate text-sm font-medium text-slate-100">
          {rule.name}
        </h2>
        <div className="ml-auto flex items-center gap-2">
          {editMode ? (
            <>
              <button
                type="button"
                onClick={onSaveEdit}
                className="rounded bg-sky-700 px-3 py-1 text-xs font-medium text-sky-50 hover:bg-sky-600"
              >
                Save
              </button>
              <button
                type="button"
                onClick={onCancelEdit}
                className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
              >
                Cancel
              </button>
            </>
          ) : (
            <>
              <button
                type="button"
                onClick={() => { void handleToggle(); }}
                aria-label={effectiveEnabled ? "Disable rule" : "Enable rule"}
                className={`rounded border px-3 py-1 text-xs transition-colors ${
                  effectiveEnabled
                    ? "border-sky-700 bg-sky-900/40 text-sky-100 hover:bg-sky-900/60"
                    : "border-slate-700 bg-slate-800 text-slate-300 hover:border-slate-500 hover:bg-slate-700"
                }`}
              >
                {effectiveEnabled ? "☑ Enabled" : "☐ Disabled"}
              </button>
              {confirmingDelete ? (
                <>
                  <button
                    type="button"
                    onClick={() => {
                      onDelete();
                      setConfirmingDelete(false);
                    }}
                    className="rounded border border-red-700 bg-red-900/40 px-3 py-1 text-xs text-red-300 hover:bg-red-900/60"
                  >
                    Yes, delete
                  </button>
                  <button
                    type="button"
                    onClick={() => setConfirmingDelete(false)}
                    className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
                  >
                    Cancel
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => setConfirmingDelete(true)}
                  className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
                >
                  Delete
                </button>
              )}
              <div className="inline-flex overflow-hidden rounded border border-slate-700">
                <span className="border-r border-slate-700 bg-sky-900/40 px-3 py-1 text-xs text-sky-100">
                  View
                </span>
                <button
                  type="button"
                  onClick={onEnterEdit}
                  className="bg-slate-800 px-3 py-1 text-xs text-slate-300 hover:bg-slate-700"
                >
                  Edit
                </button>
              </div>
            </>
          )}
        </div>
      </div>

      {editMode ? (
        <RuleEditor
          value={editDraft ?? ""}
          onChange={onChangeDraft}
          onSave={onSaveEdit}
          onCancel={onCancelEdit}
        />
      ) : (
        <RuleViewer rule={rule} />
      )}
    </div>
  );
}

// #endregion