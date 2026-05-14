/**
 * Right-pane orchestrator for the Plans tab.
 *
 * Pure controlled component: all CRUD state lives in `plansStore` and
 * is plumbed in through props by `PlansRoute`. The only
 * local state is the transient `confirmingDelete` flag for the inline
 * delete confirmation flow — it doesn't need to round-trip through
 * the store.
 *
 * Render states:
 * - `plan === null`: centered empty-state hint.
 * - `plan !== null && !editMode`: header with `[Re-execute]
 *   [Delete | Yes,delete + Cancel] [View | Edit]` plus the markdown
 *   viewer.
 * - `plan !== null && editMode`: header with `[Save (primary)]
 *   [Cancel]` plus the monospace editor.
 *
 * The `confirmingDelete` flag resets on any transition of the active
 * plan or the edit mode — that way a stale "are you sure?" can't
 * survive the user switching plans or entering edit via another path.
 */

import { useEffect, useState } from "react";
import type { Plan } from "../ipc/types";
import PlanEditor from "./PlanEditor";
import PlanViewer from "./PlanViewer";

// #region Component

/**
 * Props for {@link PlanPane}.
 */
interface PlanPaneProps
{
  plan: Plan | null;
  editMode: boolean;
  editDraft: string | null;
  onEnterEdit: () => void;
  onCancelEdit: () => void;
  onSaveEdit: () => void;
  onChangeDraft: (next: string) => void;
  onDelete: () => void;
  onReExecute: () => void;
}

/**
 * Renders the pane: empty state, viewer + action header, or editor +
 * save/cancel header — picked from the controlled props.
 *
 * @param props - See {@link PlanPaneProps}.
 * @returns The pane element, sized to fill its flex parent.
 */
export default function PlanPane({plan, editMode, editDraft, onEnterEdit, onCancelEdit, onSaveEdit, onChangeDraft, onDelete, onReExecute,}: PlanPaneProps)
{
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  useEffect(() => {
    setConfirmingDelete(false);
  }, [plan?.name, editMode]);

  if (plan === null)
  {
    return (
      <div className="flex h-full flex-col">
        <p className="m-auto p-4 text-center text-sm text-slate-500">
          Select a plan or create a new one.
        </p>
      </div>
    );
  }

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center gap-2 border-b border-slate-800 p-3">
        <h2 className="truncate text-sm font-medium text-slate-100">
          {plan.name}
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
                onClick={onReExecute}
                className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
              >
                Re-execute
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
        <PlanEditor
          value={editDraft ?? ""}
          onChange={onChangeDraft}
          onSave={onSaveEdit}
          onCancel={onCancelEdit}
        />
      ) : (
        <PlanViewer plan={plan} />
      )}
    </div>
  );
}

// #endregion