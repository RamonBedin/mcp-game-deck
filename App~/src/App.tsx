/**
 * Root layout
 *
 * Replaces the old `slate-950` left sidebar with the new `HudStrip`
 * (global state band) + `NavRail` (5-item navigation including the
 * new Library tab). The cross-cutting effects (install poll, connection
 * poll, supervisor fast path, single-instance route requests) are
 * preserved verbatim from the previous App — they are not visual code
 * and don't change with the design pass.
 *
 * Badges fed into NavRail:
 *  - chat: pulse dot when an assistant turn is currently streaming
 *  - plans: number of plans cached in plansStore
 *  - rules: `enabled / cap` fraction
 */

import { useEffect, useMemo, useState } from "react";
import { Outlet, useNavigate } from "react-router-dom";
import ClaudeVersionWarningBanner from "./components/ClaudeVersionWarningBanner";
import { BrandGradientDefs } from "./components/atoms/BrandHex";
import StatusDot from "./components/atoms/StatusDot";
import FirstRunPanel, { FirstRunCheckingScreen, isInstallReady } from "./components/firstrun/FirstRunPanel";
import HudStrip from "./components/shell/HudStrip";
import NavRail from "./components/shell/NavRail";
import UpdateBanner from "./components/UpdateBanner";
import { useCatalogSubscription } from "./hooks/useCatalogSubscription";
import { useConversationSubscription } from "./hooks/useConversationSubscription";
import { usePlansSubscription } from "./hooks/usePlansSubscription";
import { useRulesSubscription } from "./hooks/useRulesSubscription";
import { checkClaudeInstallStatus, getSettings, getSupervisorStatus, getUnityStatus } from "./ipc/commands";
import { onRouteRequested, onSupervisorStatusChanged } from "./ipc/events";
import type { ClaudeInstallStatus } from "./ipc/types";
import { useConnectionStore } from "./stores/connectionStore";
import { useConversationStore } from "./stores/conversationStore";
import { usePlansStore } from "./stores/plansStore";
import { useRulesStore } from "./stores/rulesStore";
import { useSettingsStore } from "./stores/settingsStore";

// #region Constants

const CONNECTION_POLL_INTERVAL_MS = 2000;
const INSTALL_POLL_INTERVAL_MS = 5000;
const ENABLED_RULES_CAP = 10;

// #endregion

/**
 * Root layout. Hosts the install gate, then renders the HUD + nav rail
 * + routed outlet.
 *
 * @returns The root layout element.
 */
export default function App()
{
  const setUnityStatus = useConnectionStore((s) => s.setUnityStatus);
  const setSupervisorStatus = useConnectionStore((s) => s.setSupervisorStatus);
  const setSettings = useSettingsStore((s) => s.setSettings);
  const navigate = useNavigate();
  const [installStatus, setInstallStatus] = useState<ClaudeInstallStatus | null>(null);

  usePlansSubscription();
  useRulesSubscription();
  useCatalogSubscription();
  useConversationSubscription();

  // #region Effects

  // Hydrate settings store from the Rust side on boot — populates
  // `unityProjectPath` so the HUD shows the bound Unity project
  // instead of "no project". The Rust `get_settings` resolves the
  // path live via env-then-saved-settings, so this single call is
  // enough for the current single-instance / single-project model.
  useEffect(() => {
    let cancelled = false;

    void getSettings()
      .then((settings) => {
        if (!cancelled)
        {
          setSettings(settings);
        }
      })
      .catch((err) => {
        console.error("[app] failed to hydrate settings:", err);
      });

    return () => {
      cancelled = true;
    };
  }, [setSettings]);

  // Claude install-detection poll. Drives the FirstRunPanel gate.
  useEffect(() => {
    let cancelled = false;
    let intervalId: number | null = null;

    const tick = async () => {
      try
      {
        const status = await checkClaudeInstallStatus();

        if (cancelled)
        {
          return;
        }

        setInstallStatus(status);

        if (isInstallReady(status) && intervalId !== null)
        {
          window.clearInterval(intervalId);
          intervalId = null;
        }
      }
      catch (err)
      {
        console.error("[first-run] install check failed:", err);
      }
    };

    void tick();
    intervalId = window.setInterval(tick, INSTALL_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;

      if (intervalId !== null)
      {
        window.clearInterval(intervalId);
      }
    };
  }, []);

  // Connection poll (every CONNECTION_POLL_INTERVAL_MS) — backstop for
  // the supervisor fast path below.
  useEffect(() => {
    let cancelled = false;

    const tick = async () => {
      try
      {
        const [unity, supervisor] = await Promise.all([getUnityStatus(), getSupervisorStatus()]);

        if (cancelled)
        {
          return;
        }

        setUnityStatus(unity);
        setSupervisorStatus(supervisor);
      }
      catch (err)
      {
        console.error("[connection] poll failed:", err);
      }
    };

    void tick();
    const id = window.setInterval(tick, CONNECTION_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(id);
    };
  }, [setUnityStatus, setSupervisorStatus]);

  // Supervisor fast path — sub-second transitions.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onSupervisorStatusChanged((payload) => {
      if (cancelled)
      {
        return;
      }

      setSupervisorStatus(payload.status);

      if (payload.status === "crashed" || payload.status === "failed")
      {
        useConversationStore.getState().markAllPendingRequestsInterrupted();
      }
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[app] failed to subscribe to supervisor-status-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [setSupervisorStatus]);

  // Single-instance --route= forwarder.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onRouteRequested((payload) => {
      if (cancelled)
      {
        return;
      }

      navigate(payload.route);
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
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

  // #region NavRail badges

  const isStreaming = useConversationStore((s) => s.inFlight);

  const plansCount    = usePlansStore((s) => s.plans.length);
  const rulesEnabled  = useRulesStore((s) => s.rules.filter((r) => r.enabled).length);

  const badges = useMemo(() => ({
    chat:  isStreaming ? <StatusDot status="busy" size={6} /> : undefined,
    plans: plansCount > 0 ? plansCount : undefined,
    rules: `${rulesEnabled}/${ENABLED_RULES_CAP}`,
  }), [isStreaming, plansCount, rulesEnabled]);

  // #endregion

  if (installStatus === null)
  {
    return <FirstRunCheckingScreen />;
  }

  if (!isInstallReady(installStatus))
  {
    return <FirstRunPanel status={installStatus} />;
  }

  return (
    <div className="flex h-screen w-full flex-col overflow-hidden bg-bg-1 text-txt-1">
      <BrandGradientDefs />
      <UpdateBanner />
      <ClaudeVersionWarningBanner />
      <HudStrip />
      <div className="flex flex-1 overflow-hidden">
        <NavRail badges={badges} />
        <main className="flex-1 min-w-0 flex flex-col overflow-hidden">
          <Outlet />
        </main>
      </div>
    </div>
  );
}