/**
 * Zustand store for the plans tab — list, current plan, edit mode,
 * and CRUD actions backed by the Tauri commands.
 *
 * The list mirrors the on-disk plans dir under
 * `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/`. External
 * writes (the `/save-plan` skill, direct edits, etc) surface here via
 * the `plans-changed` event subscription in `usePlansSubscription` —
 * any FS change triggers a `loadList` refetch, so the synthesized
 * `kind` is informational only.
 *
 * Error handling split:
 * - `loadList` / `selectPlan` swallow errors (logged) — these run on
 *   subscription ticks or click handlers where the watcher will
 *   recover state on the next event.
 * - `saveEdit` swallows and surfaces via `editError` so the editor
 *   can display inline and keep `editMode=true` for retry.
 * - `deletePlan` / `createNewPlan` propagate — these are deliberate
 *   user actions whose failure must reach the caller (route layer
 *   uses try/catch around them).
 */

import { create } from "zustand";
import { deletePlan as deletePlanCommand, listPlans, readPlan, writePlan, } from "../ipc/commands";
import type { Plan, PlanMeta } from "../ipc/types";

// #region State shape

/**
 * Shape of the plans-state store backing the Plans tab.
 *
 * `editDraft` lives separately from `currentPlan.content` so the
 * textarea can diverge from the on-disk content during editing
 * without mutating the source-of-truth read; `editError` is the
 * inline error surface for the editor (set by `saveEdit` on failure,
 * cleared by `enterEdit` / `cancelEdit` / a successful save).
 */
interface PlansState
{
  plans: PlanMeta[];
  selectedName: string | null;
  currentPlan: Plan | null;
  editMode: boolean;
  editDraft: string | null;
  editError: string | null;
  loadList: () => Promise<void>;
  selectPlan: (name: string) => Promise<void>;
  enterEdit: () => void;
  cancelEdit: () => void;
  saveEdit: () => Promise<void>;
  deletePlan: (name: string) => Promise<void>;
  createNewPlan: (name: string, content: string) => Promise<void>;
}

// #endregion

// #region Helpers

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

// #region Store

export const usePlansStore = create<PlansState>((set, get) => ({
  plans: [],
  selectedName: null,
  currentPlan: null,
  editMode: false,
  editDraft: null,
  editError: null,
  loadList: async () => {
    try
    {
      const plans = await listPlans();
      set({ plans });
    }
    catch (err)
    {
      console.error("[plans] loadList failed:", err);
    }
  },
  selectPlan: async (name) => {
    try
    {
      const plan = await readPlan(name);
      set({
        currentPlan: plan,
        selectedName: name,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
    catch (err)
    {
      console.error("[plans] selectPlan failed:", err);
    }
  },
  enterEdit: () => {
    const { currentPlan } = get();
    if (currentPlan === null)
    {
      return;
    }
    set({
      editMode: true,
      editDraft: currentPlan.content,
      editError: null,
    });
  },
  cancelEdit: () =>
    set({
      editMode: false,
      editDraft: null,
      editError: null,
    }),
  saveEdit: async () => {
    const { currentPlan, editDraft } = get();
    if (currentPlan === null || editDraft === null)
    {
      return;
    }
    try
    {
      await writePlan(currentPlan.name, editDraft);
      const refreshed = await readPlan(currentPlan.name);
      set({
        currentPlan: refreshed,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
    catch (err)
    {
      set({ editError: formatError(err) });
    }
  },
  deletePlan: async (name) => {
    await deletePlanCommand(name);
    if (get().selectedName === name)
    {
      set({
        currentPlan: null,
        selectedName: null,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
  },
  createNewPlan: async (name, content) => {
    await writePlan(name, content);
  },
}));

// #endregion