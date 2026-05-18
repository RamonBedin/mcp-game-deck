/**
 * Rules route — 2-column layout: `RulesList` on the left,
 * `RulePane` (or the inline new-rule form) on the right, plus a
 * route-level toast banner above both columns for toggle errors.
 *
 * Wires `rulesStore` actions to component props; owns the
 * transient new-rule form state locally (open flag, name draft,
 * inline error string) and the toggle-error toast, since none of
 * it needs to outlive the route. The `handleToggle` wrapper
 * forwards to `rulesStore.toggleRule` and lifts `{ok: false}` into
 * the local toast — the list/pane components still receive the raw
 * `{ok, error?}` result so they can revert their optimistic state
 * locally without a second source of truth on the error message.
 *
 * Collision check uses the cached `rules` array rather than a fresh
 * `invoke('list_rules')` — the 250ms watcher debounce window makes
 * the cache stale-by-at-most-250ms, and the worst case (two parallel
 * writers picking the same name) is naturally absorbed by
 * `write_rule`'s overwrite-by-design semantics. The optimistic
 * checkbox state in `RulesList` is reconciled by the next
 * `loadList()` triggered by the `rules-changed` watcher event.
 */

import { useEffect, useMemo, useState } from "react";
import ActiveBundlePanel from "../components/rules/ActiveBundlePanel";
import RulePane from "../components/RulePane";
import RulesList from "../components/RulesList";
import { useCollapsedColumn } from "../hooks/useCollapsedColumn";
import { previewRulesBundle } from "../ipc/commands";
import { onRulesChanged } from "../ipc/events";
import { useRulesStore } from "../stores/rulesStore";

// #region Constants

const KEBAB_RE = /^[a-z0-9][a-z0-9-]*$/;
const MAX_NAME_LENGTH = 64;
const TOAST_DISMISS_MS = 3000;

// #endregion

// #region Helpers

const buildNewRuleTemplate = (name: string): string =>
  `---
enabled: false
description: ""
applies-to: []
---

# ${name}

<rule body — describe what Claude should do, when it applies, and any exceptions>
`;

const formatError = (err: unknown): string => {
  if (err instanceof Error)
  {
    return err.message;
  }

  if (typeof err === "string")
  {
    return err;
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

// #region Component

/**
 * Renders the Rules tab. Owns the new-rule form's local state and
 * the route-level toggle-error toast; the rest of the tab's state
 * lives in `rulesStore`.
 *
 * @returns The route element.
 */
export default function RulesRoute()
{
  const rules = useRulesStore((s) => s.rules);
  const selectedName = useRulesStore((s) => s.selectedName);
  const currentRule = useRulesStore((s) => s.currentRule);
  const editMode = useRulesStore((s) => s.editMode);
  const editDraft = useRulesStore((s) => s.editDraft);
  const selectRule = useRulesStore((s) => s.selectRule);
  const enterEdit = useRulesStore((s) => s.enterEdit);
  const cancelEdit = useRulesStore((s) => s.cancelEdit);
  const setEditDraft = useRulesStore((s) => s.setEditDraft);
  const saveEdit = useRulesStore((s) => s.saveEdit);
  const deleteRule = useRulesStore((s) => s.deleteRule);
  const toggleRule = useRulesStore((s) => s.toggleRule);
  const createNewRule = useRulesStore((s) => s.createNewRule);

  const [newRuleFormOpen, setNewRuleFormOpen] = useState(false);
  const [newRuleName, setNewRuleName] = useState("");
  const [newRuleError, setNewRuleError] = useState<string | null>(null);
  const [toggleToast, setToggleToast] = useState<string | null>(null);
  const [bundleText, setBundleText] = useState<string | undefined>(undefined);
  const [listCollapsed, toggleListCollapsed] = useCollapsedColumn("rules-list");

  const enabledRules = useMemo(() => rules.filter((r) => r.enabled), [rules]);

  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    const refetch = () => {
      previewRulesBundle()
        .then((text) => {
          if (cancelled)
          {
            return;
          }
          setBundleText(text);
        })
        .catch((err) => console.error("[rules] preview bundle failed:", err));
    };

    refetch();

    onRulesChanged(() => refetch())
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
      .catch((err) => console.error("[rules] bundle subscribe failed:", err));

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

  const handleCopyBundle = () => {
    if (bundleText === undefined || bundleText.length === 0)
    {
      return;
    }
    void navigator.clipboard.writeText(bundleText).catch((err) => {
      console.error("[rules] clipboard copy failed:", err);
    });
  };

  // Toast auto-dismiss.
  useEffect(() => {
    if (toggleToast === null)
    {
      return;
    }

    const id = window.setTimeout(() => setToggleToast(null), TOAST_DISMISS_MS);
    return () => window.clearTimeout(id);
  }, [toggleToast]);

  // #region Handlers

  const handleOpenNewRuleForm = () => {
    setNewRuleFormOpen(true);
    setNewRuleName("");
    setNewRuleError(null);
  };

  const handleCancelNewRuleForm = () => {
    setNewRuleFormOpen(false);
    setNewRuleName("");
    setNewRuleError(null);
  };

  const handleCreate = async () => {
    const name = newRuleName.trim();

    if (name.length === 0)
    {
      setNewRuleError("Name is required.");
      return;
    }

    if (!KEBAB_RE.test(name) || name.length > MAX_NAME_LENGTH)
    {
      setNewRuleError(
        "Use kebab-case: lowercase letters, digits, hyphens. Max 64 chars.",
      );
      return;
    }

    if (rules.some((r) => r.name === name))
    {
      setNewRuleError(
        "A rule with this name already exists. Try a different name.",
      );
      return;
    }
    try
    {
      await createNewRule(name, buildNewRuleTemplate(name));
      await selectRule(name);
      enterEdit();
      setNewRuleFormOpen(false);
      setNewRuleName("");
      setNewRuleError(null);
    }
    catch (err)
    {
      setNewRuleError(formatError(err));
    }
  };

  const handleDelete = () => {
    if (currentRule === null)
    {
      return;
    }
    deleteRule(currentRule.name).catch((err) => {
      console.error("[rules] delete failed:", err);
    });
  };

  const handleToggle = async (name: string, next: boolean) => {
    const result = await toggleRule(name, next);

    if (!result.ok)
    {
      setToggleToast(result.error ?? "Toggle failed.");
    }
    return result;
  };

  // #endregion

  return (
    <div className="flex h-full flex-col gap-2">
      {toggleToast !== null && (
        <div
          role="alert"
          className="rounded border border-yellow-700/60 bg-yellow-900/40 px-3 py-2 text-xs text-yellow-100"
        >
          {toggleToast}
        </div>
      )}

      <div className="flex min-h-0 flex-1 gap-4">
        {listCollapsed ? (
          <aside className="w-8 shrink-0 border-r border-line bg-bg-0 flex flex-col items-center py-3">
            <button
              type="button"
              onClick={toggleListCollapsed}
              title="Expand rules"
              aria-label="Expand rules"
              className="inline-flex items-center justify-center w-6 h-6 rounded-r-1 text-txt-4 hover:text-txt-1 hover:bg-bg-3 transition-colors duration-[120ms]"
            >
              <span style={{ fontSize: 11 }}>›</span>
            </button>
          </aside>
        ) : (
          <div className="w-[250px] shrink-0 flex flex-col">
            <div className="mb-1.5 flex justify-end">
              <button
                type="button"
                onClick={toggleListCollapsed}
                title="Collapse rules list"
                aria-label="Collapse rules list"
                className="inline-flex items-center justify-center w-5 h-5 rounded-r-1 text-txt-4 hover:text-txt-1 hover:bg-bg-3 transition-colors duration-[120ms]"
              >
                <span style={{ fontSize: 11 }}>‹</span>
              </button>
            </div>
            <RulesList
              rules={rules}
              selectedName={selectedName}
              onSelect={(name) => void selectRule(name)}
              onNewRule={handleOpenNewRuleForm}
              onToggle={handleToggle}
            />
          </div>
        )}

        <div className="flex h-full min-w-0 flex-1 flex-col">
          {newRuleFormOpen ? (
            <div className="flex h-full flex-col p-6">
              <h2 className="mb-4 text-sm font-medium text-slate-100">
                New rule
              </h2>

              <label
                className="mb-1 text-xs text-slate-400"
                htmlFor="new-rule-name"
              >
                Name
              </label>
              <input
                id="new-rule-name"
                type="text"
                value={newRuleName}
                onChange={(e) => setNewRuleName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter")
                  {
                    e.preventDefault();
                    void handleCreate();
                  }
                  else if (e.key === "Escape")
                  {
                    e.preventDefault();
                    handleCancelNewRuleForm();
                  }
                }}
                placeholder="prefer-textmeshpro"
                autoFocus
                className="mb-1 rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-sm text-slate-100 outline-none focus:border-slate-500"
              />
              <p className="mb-3 text-[10px] text-slate-500">
                Lowercase letters, digits, hyphens. Max 64 chars.
              </p>

              {newRuleError !== null && (
                <p className="mb-3 text-xs text-red-400">{newRuleError}</p>
              )}

              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => void handleCreate()}
                  className="rounded bg-sky-700 px-3 py-1 text-xs font-medium text-sky-50 hover:bg-sky-600"
                >
                  Create
                </button>
                <button
                  type="button"
                  onClick={handleCancelNewRuleForm}
                  className="rounded border border-slate-700 bg-slate-800 px-3 py-1 text-xs text-slate-200 hover:border-slate-500 hover:bg-slate-700"
                >
                  Cancel
                </button>
              </div>
            </div>
          ) : (
            <RulePane
              rule={currentRule}
              editMode={editMode}
              editDraft={editDraft}
              onEnterEdit={enterEdit}
              onCancelEdit={cancelEdit}
              onSaveEdit={() => void saveEdit()}
              onChangeDraft={setEditDraft}
              onDelete={handleDelete}
              onToggle={handleToggle}
            />
          )}
        </div>

        <ActiveBundlePanel
          enabledRules={enabledRules}
          bundleText={bundleText}
          conflictStatus="ok"
          onCopy={handleCopyBundle}
        />
      </div>
    </div>
  );
}

// #endregion