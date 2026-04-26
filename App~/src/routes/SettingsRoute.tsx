import { useEffect, useState } from "react";
import { devEmitTestEvent, nodePing } from "../ipc/commands";
import { onUnityStatusChanged } from "../ipc/events";
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
  const setUnityStatus = useConnectionStore((s) => s.setUnityStatus);

  const [pingResult, setPingResult] = useState<string | null>(null);
  const [pinging, setPinging] = useState(false);

  // Subscribe to unity-status-changed events. The 2s poll in App.tsx will
  // overwrite the disconnected state on the next tick — that's the intended
  // "revert" path for the dev test trigger.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onUnityStatusChanged((payload) => {
      if (cancelled) return;
      setUnityStatus(payload.status);
    })
      .then((u) => {
        if (cancelled) {
          u();
        } else {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[settings] failed to subscribe to unity-status-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [setUnityStatus]);

  const handlePing = async () => {
    setPinging(true);
    setPingResult("…");
    const start = performance.now();
    try {
      const pong = await nodePing();
      const elapsed = Math.round(performance.now() - start);
      setPingResult(`pong=${pong} (${elapsed}ms)`);
    } catch (err) {
      setPingResult(`error: ${String(err)}`);
    } finally {
      setPinging(false);
    }
  };

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

      {import.meta.env.DEV && (
        <div className="mt-8 border-t border-slate-800 pt-6">
          <h2 className="text-xs font-semibold uppercase tracking-wider text-slate-500">
            Dev tools
          </h2>

          <div className="mt-3 flex flex-col gap-3">
            <div>
              <button
                type="button"
                onClick={() => {
                  void devEmitTestEvent().catch((err) =>
                    console.error("[dev] emit test event failed:", err),
                  );
                }}
                className="rounded bg-amber-700 px-3 py-1.5 text-sm text-amber-50 hover:bg-amber-600"
              >
                Emit unity-status-changed (disconnected)
              </button>
              <p className="mt-1 text-xs text-slate-500">
                Polling reverts Unity to "connected" within ~2s.
              </p>
            </div>

            <div>
              <button
                type="button"
                onClick={() => void handlePing()}
                disabled={pinging}
                className="rounded bg-sky-700 px-3 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:cursor-not-allowed disabled:opacity-50"
              >
                Ping Node SDK
              </button>
              {pingResult !== null && (
                <p className="mt-1 font-mono text-xs text-slate-400">
                  {pingResult}
                </p>
              )}
              <p className="mt-1 text-xs text-slate-500">
                Round-trips a JSON-RPC `ping`. Watch DevTools console for the
                node heartbeat (every 5s).
              </p>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
