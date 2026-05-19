/**
 * Subscribes to the `agent-message` Tauri event and routes
 * `catalog-ready` envelopes into `catalogStore`.
 *
 * No mount-time pre-fetch (unlike `usePlansSubscription`): the
 * supervisor pushes the catalog automatically during the health
 * check, which lands well before the user can interact with the
 * dropdown. If React mounts AFTER the emit (rare — HMR rebuild with
 * supervisor still running), the store stays empty until the next
 * emit; the JS-side cache prevents redundant re-emissions, so we
 * accept that gap as v2.0 behavior. A `get_catalog` Tauri command
 * or an on-demand re-emit can be added later if it shows up in
 * practice.
 */

import { useEffect } from "react";
import { onAgentMessage } from "../ipc/events";
import { useCatalogStore } from "../stores/catalogStore";

/**
 * Wires `catalogStore.setCatalog` to the `agent-message` Tauri event
 * for the lifetime of the component that calls this hook. Only the
 * `catalog-ready` variant is consumed — every other variant
 * (text-delta, tool-use, etc.) is left for its own dedicated
 * subscribers.
 */
export function useCatalogSubscription(): void
{
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onAgentMessage((payload) => {
      if (cancelled)
      {
        return;
      }

      if (payload.message.type === "catalog-ready")
      {
        useCatalogStore.getState().setCatalog(
          payload.message.commands,
          payload.message.agents,
        );
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
        console.error("[catalog] failed to subscribe to agent-message:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);
}