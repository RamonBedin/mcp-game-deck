import { create } from "zustand";
import type { AppSettings, AppSettingsPatch } from "../ipc/types";

interface SettingsState {
  settings: AppSettings;
  setSettings: (settings: AppSettings) => void;
  patchSettings: (patch: AppSettingsPatch) => void;
}

const DEFAULT_SETTINGS: AppSettings = {
  theme: "dark",
  unityProjectPath: null,
};

export const useSettingsStore = create<SettingsState>((set) => ({
  settings: DEFAULT_SETTINGS,
  setSettings: (settings) => set({ settings }),
  patchSettings: (patch) =>
    set((state) => ({ settings: { ...state.settings, ...patch } })),
}));