import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

const __dirname = dirname(fileURLToPath(import.meta.url));
const host = process.env.TAURI_DEV_HOST;

const pkgVersion = JSON.parse(
  readFileSync(resolve(__dirname, "../package.json"), "utf-8"),
).version as string;
const appVersion = JSON.parse(
  readFileSync(resolve(__dirname, "package.json"), "utf-8"),
).version as string;

// https://vitejs.dev/config/
// Tauri-recommended Vite config (see https://v2.tauri.app/start/frontend/vite/)
export default defineConfig(async () => ({
  plugins: [react()],

  // Tauri expects a fixed port; fail if it's not available.
  clearScreen: false,
  server: {
    port: 1420,
    strictPort: true,
    host: host || false,
    hmr: host
      ? {
          protocol: "ws",
          host,
          port: 1421,
        }
      : undefined,
    watch: {
      ignored: ["**/src-tauri/**"],
    },
  },

  // Env vars starting with TAURI_ENV_* are exposed to the frontend.
  envPrefix: ["VITE_", "TAURI_ENV_"],

  define: {
    __PACKAGE_VERSION__: JSON.stringify(pkgVersion),
    __APP_VERSION__:     JSON.stringify(appVersion),
  },
}));