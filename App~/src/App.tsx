import { useEffect } from "react";
import { NavLink, Outlet } from "react-router-dom";
import { getNodeSdkStatus, getUnityStatus } from "./ipc/commands";
import { useConnectionStore } from "./stores/connectionStore";

const NAV_ITEMS = [
  { to: "/chat", label: "Chat" },
  { to: "/plans", label: "Plans" },
  { to: "/rules", label: "Rules" },
  { to: "/settings", label: "Settings" },
] as const;

const CONNECTION_POLL_INTERVAL_MS = 2000;

export default function App() {
  const setUnityStatus = useConnectionStore((s) => s.setUnityStatus);
  const setNodeSdkStatus = useConnectionStore((s) => s.setNodeSdkStatus);

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
        // Stub commands can't fail; this guards against future real impls.
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

  return (
    <div className="flex min-h-screen w-full bg-slate-900 text-slate-100">
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
      <main className="flex-1 p-8">
        <Outlet />
      </main>
    </div>
  );
}