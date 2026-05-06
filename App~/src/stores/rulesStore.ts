/**
 * Zustand store for the rules list.
 *
 * Backed by `list_rules` / `toggle_rule` Tauri commands; rules UI lands
 */

import { create } from "zustand";
import type { RuleMeta } from "../ipc/types";

// #region State shape

/**
 * Shape of the rules-state store backing the rules panel.
 *
 * Tracks the available agent rules and their enabled/disabled state, exposing
 * a bulk replace setter for initial hydration and a per-rule toggle for user
 * interactions in the rules picker.
 */interface RulesState
{
  rules: RuleMeta[];
  setRules: (rules: RuleMeta[]) => void;
  toggleRule: (name: string, enabled: boolean) => void;
}

// #endregion

// #region Store

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