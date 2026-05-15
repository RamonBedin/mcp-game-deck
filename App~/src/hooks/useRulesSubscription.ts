/**
 * Subscribes to the `rules-changed` Tauri event and keeps `rulesStore`
 * in sync with the on-disk rules directory.
 *
 * Mount-time fires a one-shot `loadList` so the cache is warm before
 * the user navigates to the Rules tab — a useful side-effect when an
 * external edit (VS Code, the rules watcher's bundle compose, etc)
 * lands while another tab is active. The subscription itself fires
 * `loadList` on every emitted event: `RulesChangedPayload.kind` is
 * informational (the Rust watcher synthesizes it best-effort), and a
 * blanket refetch settles the final state regardless of which kind
 * was reported. See `rules_watcher.rs` for the producer.
 */

import { useEffect } from "react";
import { onRulesChanged } from "../ipc/events";
import { useRulesStore } from "../stores/rulesStore";

/**
 * Wires `rulesStore.loadList()` to the `rules-changed` Tauri event for
 * the lifetime of the component that calls this hook. Mount-time
 * triggers a one-shot initial load.
 */
export function useRulesSubscription(): void
{
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    void useRulesStore.getState().loadList();

    onRulesChanged(() => {
      if (cancelled)
      {
        return;
      }
      
      void useRulesStore.getState().loadList();
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
        console.error("[rules] failed to subscribe to rules-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);
}