/**
 * Subscribes to the `plans-changed` Tauri event and keeps `plansStore`
 * in sync with the on-disk plans directory.
 *
 * Mount-time fires a one-shot `loadList` so the cache is warm before
 * the user navigates to the Plans tab — letting the `/save-plan` skill
 * populate the list silently while another tab is active. The
 * subscription itself fires `loadList` on every emitted event:
 * `PlansChangedPayload.kind` is informational (the Rust watcher
 * synthesizes it best-effort), and a blanket refetch settles the
 * final state regardless of which kind was reported. See
 * `plans_watcher.rs` for the producer.
 */

import { useEffect } from "react";
import { onPlansChanged } from "../ipc/events";
import { usePlansStore } from "../stores/plansStore";

/**
 * Wires `plansStore.loadList()` to the `plans-changed` Tauri event for
 * the lifetime of the component that calls this hook. Mount-time
 * triggers a one-shot initial load.
 */
export function usePlansSubscription(): void
{
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    void usePlansStore.getState().loadList();

    onPlansChanged(() => {
      if (cancelled)
      {
        return;
      }
      void usePlansStore.getState().loadList();
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
        console.error("[plans] failed to subscribe to plans-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);
}
