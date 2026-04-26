import { NavLink, Outlet } from "react-router-dom";

const NAV_ITEMS = [
  { to: "/chat", label: "Chat" },
  { to: "/plans", label: "Plans" },
  { to: "/rules", label: "Rules" },
  { to: "/settings", label: "Settings" },
] as const;

export default function App() {
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