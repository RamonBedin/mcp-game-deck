/**
 * Root layout component.
 *
 * Hosts the persistent left-rail navigation and the routed `<Outlet />`,
 * and owns three cross-cutting effects that need to live above any single
 * route: the connection-status poller, the Node SDK status fast-path
 * subscription, and the Node SDK log → console forwarder.
 */

import { useEffect } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import UpdateBanner from "./components/UpdateBanner";
import { getNodeSdkStatus, getUnityStatus } from "./ipc/commands";
import { onNodeLog, onNodeSdkStatusChanged, onRouteRequested } from "./ipc/events";
import { useConnectionStore } from "./stores/connectionStore";

// #region Constants

/** Items rendered in the left-rail navigation, in display order. */
const NAV_ITEMS = [
  { to: "/chat", label: "Chat" },
  { to: "/plans", label: "Plans" },
  { to: "/rules", label: "Rules" },
  { to: "/settings", label: "Settings" },
] as const;

/** Polling cadence for the connection-status backstop (events are the fast path). */
const CONNECTION_POLL_INTERVAL_MS = 2000;

// #endregion

/**
 * Root layout. Renders the navigation rail and the active route, and runs
 * the cross-cutting effects described above.
 *
 * @returns The root layout element.
 */
export default function App() {
  const setUnityStatus = useConnectionStore((s) => s.setUnityStatus);
  const setNodeSdkStatus = useConnectionStore((s) => s.setNodeSdkStatus);
  const navigate = useNavigate();

  // #region Effects

  // Connection-status poll. Runs every CONNECTION_POLL_INTERVAL_MS as a
  // backstop for the event-driven fast path below.
  useEffect(() => {
    let cancelled = false;

    const tick = async () => {
      try {
        const [unity, node] = await Promise.all([
          getUnityStatus(),
          getNodeSdkStatus(),
        ]);
        if (cancelled) return;
        setUnityStatus(unity);
        setNodeSdkStatus(node);
      } catch (err) {
        console.error("[connection] poll failed:", err);
      }
    };

    void tick();
    const id = window.setInterval(tick, CONNECTION_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, [setUnityStatus, setNodeSdkStatus]);

  // Fast-path Node SDK transitions via Tauri events. Polling is the
  // backstop; this catches Starting / Running / Crashed within milliseconds
  // instead of waiting up to 2s for the next tick.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onNodeSdkStatusChanged((payload) => {
      if (cancelled) return;
      setNodeSdkStatus(payload.status);
    })
      .then((u) => {
        if (cancelled) u();
        else unlisten = u;
      })
      .catch((err) => {
        console.error("[app] failed to subscribe to node-sdk-status-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [setNodeSdkStatus]);

  // Forward Node SDK log notifications to the DevTools console. Lives at
  // the layout root so it survives route changes.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onNodeLog((payload) => {
      if (cancelled) return;
      const fn =
        payload.level === "error"
          ? console.error
          : payload.level === "warn"
            ? console.warn
            : console.log;
      fn("[node]", payload.text);
    })
      .then((u) => {
        if (cancelled) u();
        else unlisten = u;
      })
      .catch((err) => {
        console.error("[app] failed to subscribe to node-log:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

  // Navigate the running window when a re-launched instance carries a
  // --route= CLI argument; the single-instance callback (Rust side) emits
  // the `route-requested` event and we consume it here.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onRouteRequested((payload) => {
      if (cancelled) return;
      navigate(payload.route);
    })
      .then((u) => {
        if (cancelled) u();
        else unlisten = u;
      })
      .catch((err) => {
        console.error("[app] failed to subscribe to route-requested:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [navigate]);

  // #endregion

  return (
    <div className="flex h-screen w-full flex-col overflow-hidden bg-slate-900 text-slate-100">
      <UpdateBanner />
      <div className="flex flex-1 overflow-hidden">
        <aside className="w-[200px] shrink-0 border-r border-slate-800 bg-slate-950 p-4">
          <h2 className="mb-4 text-xs font-semibold uppercase tracking-wider text-slate-500">
            MCP Game Deck
          </h2>
          <nav className="flex flex-col gap-1">
            {NAV_ITEMS.map((item) => (
              <NavLink
                key={item.to}
                to={item.to}
                className={({ isActive }) =>
                  [
                    "rounded px-3 py-2 text-sm transition-colors",
                    isActive
                      ? "bg-slate-800 text-slate-100"
                      : "text-slate-400 hover:bg-slate-800/60 hover:text-slate-200",
                  ].join(" ")
                }
              >
                {item.label}
              </NavLink>
            ))}
          </nav>
        </aside>
        <main className="flex-1 overflow-y-auto p-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}