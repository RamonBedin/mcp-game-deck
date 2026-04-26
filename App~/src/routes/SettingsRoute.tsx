import type { ConnectionStatus } from "../ipc/types";
import { useConnectionStore } from "../stores/connectionStore";
import { useSettingsStore } from "../stores/settingsStore";

const statusClass = (status: ConnectionStatus): string => {
  switch (status) {
    case "connected":
      return "text-emerald-400";
    case "busy":
      return "text-amber-400";
    case "disconnected":
      return "text-rose-400";
  }
};

export default function SettingsRoute() {
  const theme = useSettingsStore((state) => state.settings.theme);
  const unityStatus = useConnectionStore((s) => s.unityStatus);
  const nodeSdkStatus = useConnectionStore((s) => s.nodeSdkStatus);

  return (
    <section>
      <h1 className="text-2xl font-semibold">Settings</h1>
      <p className="mt-2 text-slate-400">
        Live connection status (polled every 2s). Real settings UI lands in
        later tasks.
      </p>

      <dl className="mt-6 grid grid-cols-[140px_1fr] gap-y-2 text-sm">
        <dt className="text-slate-500">Theme</dt>
        <dd className="text-slate-200">{theme}</dd>

        <dt className="text-slate-500">Unity</dt>
        <dd className={statusClass(unityStatus)}>{unityStatus}</dd>

        <dt className="text-slate-500">Node SDK</dt>
        <dd className={statusClass(nodeSdkStatus)}>{nodeSdkStatus}</dd>
      </dl>
    </section>
  );
}