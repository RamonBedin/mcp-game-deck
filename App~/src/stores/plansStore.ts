/**
 * Zustand store for the plans list and the currently open plan.
 *
 * Backed by the `list_plans` / `read_plan` Tauri commands; CRUD UI lands
 * in Feature 06.
 */

import { create } from "zustand";
import type { Plan, PlanMeta } from "../ipc/types";

// #region State shape

interface PlansState {
  plans: PlanMeta[];
  currentPlan: Plan | null;
  setPlans: (plans: PlanMeta[]) => void;
  setCurrentPlan: (plan: Plan | null) => void;
}

// #endregion

// #region Store

/** Hook for the plans store. */
export const usePlansStore = create<PlansState>((set) => ({
  plans: [],
  currentPlan: null,
  setPlans: (plans) => set({ plans }),
  setCurrentPlan: (plan) => set({ currentPlan: plan }),
}));

// #endregion