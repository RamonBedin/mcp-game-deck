import { useSettingsStore } from "../stores/settingsStore";

export default function SettingsRoute() {
  const theme = useSettingsStore((state) => state.settings.theme);

  return (
    <section>
      <h1 className="text-2xl font-semibold">Settings</h1>
      <p className="mt-2 text-slate-400">
        Placeholder. Connection status and dev tools land in tasks 2.5 and 2.6.
      </p>

      <dl className="mt-6 grid grid-cols-[140px_1fr] gap-y-2 text-sm">
        <dt className="text-slate-500">Theme</dt>
        <dd className="text-slate-200">{theme}</dd>
      </dl>
    </section>
  );
}
