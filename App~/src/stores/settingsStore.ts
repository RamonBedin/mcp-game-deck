/**
 * Zustand store for application settings.
 *
 * Mirrors `AppSettings` on the Rust side; persistence is handled there.
 * The store keeps an in-memory cache plus a `patchSettings` helper for
 * partial updates.
 */

import { create } from "zustand";
import type { AppSettings, AppSettingsPatch } from "../ipc/types";

// #region State shape

interface SettingsState {
  settings: AppSettings;
  setSettings: (settings: AppSettings) => void;
  patchSettings: (patch: AppSettingsPatch) => void;
}

// #endregion

// #region Defaults

/** Initial settings used until the first `get_settings` call returns. */
const DEFAULT_SETTINGS: AppSettings = {
  theme: "dark",
  unityProjectPath: null,
};

// #endregion

// #region Store

/** Hook for the settings store. */
export const useSettingsStore = create<SettingsState>((set) => ({
  settings: DEFAULT_SETTINGS,
  setSettings: (settings) => set({ settings }),
  patchSettings: (patch) =>
    set((state) => ({ settings: { ...state.settings, ...patch } })),
}));

// #endregion