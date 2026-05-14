/**
 * Plans route — 2-column layout: `PlansList` on the left,
 * `PlanPane` (or the inline new-plan form) on the right.
 *
 * Wires `plansStore` actions to component props; owns the
 * transient new-plan form state locally (open flag, name draft,
 * inline error string) since none of it needs to outlive the route.
 * Re-execute switches to the Chat route and submits
 * `/plan-execute <name>` through `conversationStore.sendMessage`.
 *
 * Collision check uses the cached `plans` array rather than a fresh
 * `invoke('list_plans')` — the 250ms watcher debounce window makes
 * the cache stale-by-at-most-250ms, and the worst case (two parallel
 * writers picking the same name) is naturally absorbed by
 * `write_plan`'s overwrite-by-design semantics.
 */

import { useState } from "react";
import { useNavigate } from "react-router-dom";
import PlanPane from "../components/PlanPane";
import PlansList from "../components/PlansList";
import { useConversationStore } from "../stores/conversationStore";
import { usePlansStore } from "../stores/plansStore";

// #region Helpers

const KEBAB_RE = /^[a-z0-9][a-z0-9-]*$/;
const MAX_NAME_LENGTH = 64;
const NEW_PLAN_TEMPLATE = "---\ndescription: \n---\n\n# New plan\n\n";

const formatError = (err: unknown): string => {
  if (err instanceof Error)
  {
    return err.message;
  }

  if (typeof err === "string")
  {
    return err;
  }
  try
  {
    return JSON.stringify(err);
  }
  catch
  {
    return String(err);
  }
};

// #endregion

// #region Component

/**
 * Renders the Plans tab. Owns the new-plan form's local state; the
 * rest of the tab's state lives in `plansStore`.
 *
 * @returns The route element.
 */
export default function PlansRoute()
{
  const plans = usePlansStore((s) => s.plans);
  const selectedName = usePlansStore((s) => s.selectedName);
  const currentPlan = usePlansStore((s) => s.currentPlan);
  const editMode = usePlansStore((s) => s.editMode);
  const editDraft = usePlansStore((s) => s.editDraft);
  const selectPlan = usePlansStore((s) => s.selectPlan);
  const enterEdit = usePlansStore((s) => s.enterEdit);
  const cancelEdit = usePlansStore((s) => s.cancelEdit);
  const setEditDraft = usePlansStore((s) => s.setEditDraft);
  const saveEdit = usePlansStore((s) => s.saveEdit);
  const deletePlan = usePlansStore((s) => s.deletePlan);
  const createNewPlan = usePlansStore((s) => s.createNewPlan);

  const sendMessage = useConversationStore((s) => s.sendMessage);
  const navigate = useNavigate();

  const [newPlanFormOpen, setNewPlanFormOpen] = useState(false);
  const [newPlanName, setNewPlanName] = useState("");
  const [newPlanError, setNewPlanError] = useState<string | null>(null);

  // #region Handlers

  const handleOpenNewPlanForm = () => {
    setNewPlanFormOpen(true);
    setNewPlanName("");
    setNewPlanError(null);
  };

  const handleCancelNewPlanForm = () => {
    setNewPlanFormOpen(false);
    setNewPlanName("");
    setNewPlanError(null);
  };

  const handleCreate = async () => {
    const name = newPlanName.trim();

    if (name.length === 0)
    {
      setNewPlanError("Name is required.");
      return;
    }

    if (!KEBAB_RE.test(name) || name.length > MAX_NAME_LENGTH)
    {
      setNewPlanError(
        "Use kebab-case: lowercase letters, digits, hyphens. Max 64 chars.",
      );
      return;
    }

    if (plans.some((p) => p.name === name))
    {
      setNewPlanError(
        "A plan with this name already exists. Try a different name.",
      );
      return;
    }
    try
    {
      await createNewPlan(name, NEW_PLAN_TEMPLATE);
      await selectPlan(name);
      enterEdit();
      setNewPlanFormOpen(false);
      setNewPlanName("");
      setNewPlanError(null);
    }
    catch (err)
    {
      setNewPlanError(formatError(err));
    }
  };

  const handleDelete = () => {
    if (currentPlan === null)
    {
      return;
    }
    deletePlan(currentPlan.name).catch((err) => {
      console.error("[plans] delete failed:", err);
    });
  };

  const handleReExecute = () => {
    if (currentPlan === null)
    {
      return;
    }
    navigate("/chat");
    void sendMessage(`/plan-execute ${currentPlan.name}`);
  };

  // #endregion

  return (
    <div className="flex h-full gap-4">
      <div className="w-[250px] shrink-0">
        <PlansList
          plans={plans}
          selectedName={selectedName}
          onSelect={(name) => void selectPlan(name)}
          onNewPlan={handleOpenNewPlanForm}
        />
      </div>

      <div className="flex h-full min-w-0 flex-1 flex-col">
        {newPlanFormOpen ? (
          <div className="flex h-full flex-col p-6">
            <h2 className="mb-4 text-sm font-medium text-slate-100">
              New plan
            </h2>

            <label
              className="mb-1 text-xs text-slate-400"
              htmlFor="new-plan-name"
            >
              Name
            </label>
            <input
              id="new-plan-name"
              type="text"
              value={newPlanName}
              onChange={(e) => setNewPlanName(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter")
                {
                  e.preventDefault();
                  void handleCreate();
                }
                else if (e.key === "Escape")
                {
                  e.preventDefault();
                  handleCancelNewPlanForm();
                }
              }}
              placeholder="setup-2d-scene"
              autoFocus
              className="mb-1 rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-slate-500"
            />
            <p className="mb-3 text-[10px] text-slate-500">
              Lowercase letters, digits, hyphens. Max 64 chars.
            </p>

            {newPlanError !== null && (
              <p className="mb-3 text-xs text-red-400">{newPlanError}</p>
            )}

            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => void handleCreate()}
                className="rounded bg-sky-700 px-3 py-1 text-xs font-medium text-sky-50 hover:bg-sky-600"
              >
                Create
              </button>
              <button
                type="button"
                onClick={handleCancelNewPlanForm}
                className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <PlanPane
            plan={currentPlan}
            editMode={editMode}
            editDraft={editDraft}
            onEnterEdit={enterEdit}
            onCancelEdit={cancelEdit}
            onSaveEdit={() => void saveEdit()}
            onChangeDraft={setEditDraft}
            onDelete={handleDelete}
            onReExecute={handleReExecute}
          />
        )}
      </div>
    </div>
  );
}

// #endregion