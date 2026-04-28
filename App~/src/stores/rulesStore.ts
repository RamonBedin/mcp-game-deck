/**
 * Zustand store for the rules list.
 *
 * Backed by `list_rules` / `toggle_rule` Tauri commands; rules UI lands
 * in Feature 08.
 */

import { create } from "zustand";
import type { RuleMeta } from "../ipc/types";

// #region State shape

interface RulesState {
  rules: RuleMeta[];
  setRules: (rules: RuleMeta[]) => void;
  toggleRule: (name: string, enabled: boolean) => void;
}

// #endregion

// #region Store

/** Hook for the rules store. */
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

// #endregion