/**
 * Zustand store for the rules tab — list, current rule, edit mode,
 * and CRUD actions backed by the Tauri commands.
 *
 * Mirrors the on-disk rules dir under
 * `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/rules/`. External
 * writes (direct VS Code edits, toggles, the rules watcher's own
 * surgical `toggle_rule` rewrites) surface here via the
 * `rules-changed` event subscription in `useRulesSubscription` — any
 * FS change triggers a `loadList` refetch, so the synthesized `kind`
 * is informational only.
 *
 * Error handling split:
 * - `loadList` / `selectRule` swallow errors (logged) — these run on
 *   subscription ticks or click handlers where the watcher will
 *   recover state on the next event.
 * - `saveEdit` swallows and surfaces via `editError` so the editor
 *   can display inline and keep `editMode=true` for retry.
 * - `toggleRule` swallows and returns a `{ok, error?}` shape so the
 *   list / pane can render a toast on cap-reached or non-mapping
 *   frontmatter without a try/catch dance at every call site.
 *   `loadList` still runs via the watcher event, not from the action's
 *   return — the action only reports success/failure.
 * - `deleteRule` / `createNewRule` propagate — these are deliberate
 *   user actions whose failure must reach the caller (the route layer
 *   in task 4.4 will wrap them in try/catch).
 *
 * `createNewRule` adds a client-side collision check against the
 * cached `rules` list (the Rust `write_rule` is overwrite-semantics
 * per spec, so the UI is the gatekeeper for the "+ New rule" flow).
 */

import { create } from "zustand";
import { deleteRule as deleteRuleCommand, listRules, readRule, toggleRule as toggleRuleCommand, writeRule,} from "../ipc/commands";
import type { Rule, RuleMeta } from "../ipc/types";

// #region State shape

/**
 * Result returned by `toggleRule`. `ok: true` after a successful
 * `invoke`; `ok: false` carries the error message (typically the
 * server-side cap message, but also surfaces non-mapping-frontmatter
 * and any IO error verbatim). The list refresh that follows a
 * successful toggle comes from the `rules-changed` watcher event,
 * not from this return value.
 */
export interface ToggleResult
{
  ok: boolean;
  error?: string;
}

/**
 * Shape of the rules-state store backing the Rules tab.
 *
 * `editDraft` lives separately from `currentRule.content` so the
 * textarea can diverge from the on-disk content during editing
 * without mutating the source-of-truth read; `editError` is the
 * inline error surface for the editor (set by `saveEdit` on failure,
 * cleared by `enterEdit` / `cancelEdit` / a successful save).
 */
interface RulesState
{
  rules: RuleMeta[];
  selectedName: string | null;
  currentRule: Rule | null;
  editMode: boolean;
  editDraft: string | null;
  editError: string | null;
  loadList: () => Promise<void>;
  selectRule: (name: string) => Promise<void>;
  enterEdit: () => void;
  cancelEdit: () => void;
  setEditDraft: (next: string) => void;
  saveEdit: () => Promise<void>;
  deleteRule: (name: string) => Promise<void>;
  toggleRule: (name: string, enabled: boolean) => Promise<ToggleResult>;
  createNewRule: (name: string, content: string) => Promise<void>;
}

// #endregion

// #region Helpers

const formatError = (err: unknown): string => {
  if (err instanceof Error)
  {
    return err.message;
  }

  if (typeof err === "string")
  {
    return err;
  }

  if (err !== null && typeof err === "object" && "message" in err && typeof (err as { message: unknown }).message === "string")
  {
    return (err as { message: string }).message;
  }
  try
  {
    return JSON.stringify(err);
  }
  catch
  {
    return String(err);
  }
};

// #endregion

// #region Store

export const useRulesStore = create<RulesState>((set, get) => ({
  rules: [],
  selectedName: null,
  currentRule: null,
  editMode: false,
  editDraft: null,
  editError: null,
  loadList: async () => {
    try
    {
      const rules = await listRules();
      set({ rules });
    }
    catch (err)
    {
      console.error("[rules] loadList failed:", err);
    }
  },
  selectRule: async (name) => {
    try
    {
      const rule = await readRule(name);
      set({
        currentRule: rule,
        selectedName: name,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
    catch (err)
    {
      console.error("[rules] selectRule failed:", err);
    }
  },
  enterEdit: () => {
    const { currentRule } = get();
    if (currentRule === null)
    {
      return;
    }
    set({
      editMode: true,
      editDraft: currentRule.content,
      editError: null,
    });
  },
  cancelEdit: () =>
    set({
      editMode: false,
      editDraft: null,
      editError: null,
    }),
  setEditDraft: (next) => set({ editDraft: next }),
  saveEdit: async () => {
    const { currentRule, editDraft } = get();

    if (currentRule === null || editDraft === null)
    {
      return;
    }
    try
    {
      await writeRule(currentRule.name, editDraft);
      const refreshed = await readRule(currentRule.name);
      set({
        currentRule: refreshed,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
    catch (err)
    {
      set({ editError: formatError(err) });
    }
  },
  deleteRule: async (name) => {
    await deleteRuleCommand(name);
    if (get().selectedName === name)
    {
      set({
        currentRule: null,
        selectedName: null,
        editMode: false,
        editDraft: null,
        editError: null,
      });
    }
  },
  toggleRule: async (name, enabled) => {
    try
    {
      await toggleRuleCommand(name, enabled);
      return { ok: true };
    }
    catch (err)
    {
      return { ok: false, error: formatError(err) };
    }
  },
  createNewRule: async (name, content) => {
    const exists = get().rules.some((r) => r.name === name);
    if (exists)
    {
      throw new Error(`Rule "${name}" already exists.`);
    }
    await writeRule(name, content);
  },
}));

// #endregion