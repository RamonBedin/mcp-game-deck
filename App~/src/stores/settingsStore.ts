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

/**
 * Shape of the settings-state store backing the settings panel.
 *
 * Holds the full <c>AppSettings</c> object currently in effect and exposes
 * both a wholesale replace and a partial patch mutator so consumers can either
 * hydrate from disk or apply targeted edits without rebuilding the whole
 * object.
 */
interface SettingsState
{
  settings: AppSettings;
  setSettings: (settings: AppSettings) => void;
  patchSettings: (patch: AppSettingsPatch) => void;
}

// #endregion

// #region Defaults

const DEFAULT_SETTINGS: AppSettings = {
  theme: "dark",
  unityProjectPath: null,
};

// #endregion

// #region Store

export const useSettingsStore = create<SettingsState>((set) => ({
  settings: DEFAULT_SETTINGS,
  setSettings: (settings) => set({ settings }),
  patchSettings: (patch) =>
    set((state) => ({ settings: { ...state.settings, ...patch } })),
}));

// #endregion