/**
 * React entry point.
 *
 * Awaits `getMatches()` from `@tauri-apps/plugin-cli` to read the optional
 * `--route=/path` argument set by the pin when launching from the
 * dropdown's Settings item, then mounts the root `App` layout under a
 * `MemoryRouter` seeded with that initial entry. Falls back to `/chat` when
 * no `--route` is supplied or when the CLI plugin is unavailable.
 */

import { getMatches } from "@tauri-apps/plugin-cli";
import React from "react";
import ReactDOM from "react-dom/client";
import { MemoryRouter, Navigate, Route, Routes } from "react-router-dom";
import App from "./App";
import ChatRoute from "./routes/ChatRoute";
import PlansRoute from "./routes/PlansRoute";
import RulesRoute from "./routes/RulesRoute";
import SettingsRoute from "./routes/SettingsRoute";
import "./styles/globals.css";

const DEFAULT_ROUTE = "/chat";

/**
 * Reads the `--route=/path` CLI argument via `getMatches()` and returns it,
 * falling back to the default route when the argument is missing, malformed,
 * or the CLI plugin is unavailable for any reason.
 */
async function getInitialRoute(): Promise<string> {
  try {
    const matches = await getMatches();
    const route = matches.args.route?.value;
    if (typeof route === "string" && route.length > 0) {
      return route;
    }
  } catch (err) {
    console.warn(
      "[main] failed to read CLI args, falling back to default route:",
      err,
    );
  }
  return DEFAULT_ROUTE;
}

async function bootstrap() {
  const initialRoute = await getInitialRoute();
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>
      <MemoryRouter initialEntries={[initialRoute]}>
        <Routes>
          <Route path="/" element={<App />}>
            <Route index element={<Navigate to="/chat" replace />} />
            <Route path="chat" element={<ChatRoute />} />
            <Route path="plans" element={<PlansRoute />} />
            <Route path="rules" element={<RulesRoute />} />
            <Route path="settings" element={<SettingsRoute />} />
          </Route>
        </Routes>
      </MemoryRouter>
    </React.StrictMode>,
  );
}

void bootstrap();