/**
 * Persistent banner shown at the top of the window when the Unity pin sets
 * `MCP_GAME_DECK_UPDATE_AVAILABLE=true` at app spawn.
 *
 * Reads the three `MCP_GAME_DECK_*` env vars via the `getEnvVar` command and
 * renders a single-line strip with the latest version + a "View release"
 * button (opens the release URL in the user's default browser via
 * `tauri-plugin-opener`). Dismissable per session via the "×" button.
 */

import { openUrl } from "@tauri-apps/plugin-opener";
import { useEffect, useState } from "react";
import { getEnvVar } from "../ipc/commands";

// #region Constants

const ENV_UPDATE_AVAILABLE = "MCP_GAME_DECK_UPDATE_AVAILABLE";
const ENV_LATEST_VERSION = "MCP_GAME_DECK_LATEST_VERSION";
const ENV_RELEASE_URL = "MCP_GAME_DECK_RELEASE_URL";
const ENV_AVAILABLE_TRUE = "true";

// #endregion

// #region Types

/** Resolved update information used to render the banner. */
interface UpdateInfo {
  version: string;
  releaseUrl: string | null;
}

// #endregion

/**
 * Renders the update banner when the host environment indicates an update is
 * available. Returns `null` (no DOM) when there's no update or the user has
 * dismissed the banner this session.
 */
export default function UpdateBanner() {
  const [updateInfo, setUpdateInfo] = useState<UpdateInfo | null>(null);
  const [dismissed, setDismissed] = useState(false);

  // #region Effects

  // Read the three MCP_GAME_DECK_* env vars on mount. The pin sets them at
  // Process.Start; they don't change for the life of this Tauri instance, so
  // a single read at mount is enough.
  useEffect(() => {
    let cancelled = false;

    async function loadUpdateInfo() {
      try {
        const [available, version, releaseUrl] = await Promise.all([
          getEnvVar(ENV_UPDATE_AVAILABLE),
          getEnvVar(ENV_LATEST_VERSION),
          getEnvVar(ENV_RELEASE_URL),
        ]);

        if (cancelled) return;

        if (
          available === ENV_AVAILABLE_TRUE &&
          version !== null &&
          version.length > 0
        ) {
          setUpdateInfo({
            version,
            releaseUrl: releaseUrl && releaseUrl.length > 0 ? releaseUrl : null,
          });
        }
      } catch (err) {
        console.warn("[update-banner] failed to read env vars:", err);
      }
    }

    void loadUpdateInfo();

    return () => {
      cancelled = true;
    };
  }, []);

  // #endregion

  if (updateInfo === null || dismissed) {
    return null;
  }

  // #region Handlers

  const handleViewRelease = async () => {
    if (!updateInfo.releaseUrl) return;
    try {
      await openUrl(updateInfo.releaseUrl);
    } catch (err) {
      console.error("[update-banner] failed to open release URL:", err);
    }
  };

  const handleDismiss = () => {
    setDismissed(true);
  };

  // #endregion

  return (
    <div className="flex shrink-0 items-center justify-between border-b border-blue-700 bg-blue-900/40 px-4 py-2 text-sm text-slate-100">
      <span>Update available: v{updateInfo.version}</span>
      <div className="flex items-center gap-2">
        {updateInfo.releaseUrl && (
          <button
            type="button"
            onClick={handleViewRelease}
            className="rounded bg-blue-700 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-blue-600"
          >
            View release
          </button>
        )}
        <button
          type="button"
          onClick={handleDismiss}
          aria-label="Dismiss update banner"
          className="rounded px-2 py-1 text-base leading-none text-slate-300 transition-colors hover:bg-slate-800"
        >
          ×
        </button>
      </div>
    </div>
  );
}