import { create } from "zustand";

export interface RuleMeta {
  name: string;
  enabled: boolean;
}

interface RulesState {
  rules: RuleMeta[];
  setRules: (rules: RuleMeta[]) => void;
  toggleRule: (name: string, enabled: boolean) => void;
}

export const useRulesStore = create<RulesState>((set) => ({
  rules: [],
  setRules: (rules) => set({ rules }),
  toggleRule: (name, enabled) =>
    set((state) => ({
      rules: state.rules.map((rule) =>
        rule.name === name ? { ...rule, enabled } : rule,
      ),
    })),
}));