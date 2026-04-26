import { create } from "zustand";

export interface PlanMeta {
  name: string;
  lastModified: number;
}

export interface Plan extends PlanMeta {
  content: string;
}

interface PlansState {
  plans: PlanMeta[];
  currentPlan: Plan | null;
  setPlans: (plans: PlanMeta[]) => void;
  setCurrentPlan: (plan: Plan | null) => void;
}

export const usePlansStore = create<PlansState>((set) => ({
  plans: [],
  currentPlan: null,
  setPlans: (plans) => set({ plans }),
  setCurrentPlan: (plan) => set({ currentPlan: plan }),
}));