import React from "react";
import ReactDOM from "react-dom/client";
import { MemoryRouter, Navigate, Route, Routes } from "react-router-dom";
import App from "./App";
import ChatRoute from "./routes/ChatRoute";
import PlansRoute from "./routes/PlansRoute";
import RulesRoute from "./routes/RulesRoute";
import SettingsRoute from "./routes/SettingsRoute";
import "./styles/globals.css";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <MemoryRouter initialEntries={["/chat"]}>
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