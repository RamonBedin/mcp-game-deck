/**
 * Zustand store for the slash command + agent catalog received from
 * the supervisor.
 *
 * Populated by `useCatalogSubscription` on every `agent-message`
 * envelope of type `catalog-ready` — the supervisor (`sdk_entry.js`)
 * caches the last emit JS-side so React sees one update per session
 * boot in the common case. `ready` flips true on the first emit and
 * stays true; supervisor restart replaces arrays atomically rather
 * than clearing first (avoids dropdown flicker mid-restart).
 */

import { create } from "zustand";
import type { CatalogAgent, CatalogCommand } from "../ipc/types";

// #region State shape

/**
 * Shape of the catalog-state store backing the slash dropdown
 *
 * `ready` is intentionally permanent-once-true: the consumer
 * dropdowns can use it to gate a "loading commands…" hint on first
 * launch, but a supervisor restart re-emits without clearing the
 * arrays, so the flag stays sticky.
 */
interface CatalogState
{
  commands: CatalogCommand[];
  agents: CatalogAgent[];
  ready: boolean;
  setCatalog: (commands: CatalogCommand[], agents: CatalogAgent[]) => void;
}

// #endregion

// #region Store

export const useCatalogStore = create<CatalogState>((set) => ({
  commands: [],
  agents: [],
  ready: false,
  setCatalog: (commands, agents) => set({ commands, agents, ready: true }),
}));

// #endregion