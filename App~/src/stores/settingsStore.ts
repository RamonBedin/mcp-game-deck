import { create } from "zustand";

export type Theme = "dark" | "light";

export interface AppSettings {
  theme: Theme;
  unityProjectPath: string | null;
}

interface SettingsState {
  settings: AppSettings;
  setSettings: (settings: AppSettings) => void;
  patchSettings: (patch: Partial<AppSettings>) => void;
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