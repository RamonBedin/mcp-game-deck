/**
 * Monospace textarea for editing a rule's full markdown body
 * (frontmatter delimiters + YAML + content).
 *
 * Pure controlled component: `value` is the draft text held in
 * `rulesStore.editDraft`; `onChange` flushes user edits back to the
 * store; `onSave` / `onCancel` are wired by the parent.
 *
 * Keyboard shortcuts are scoped to the textarea itself (no
 * document-level listeners) so editing here doesn't interfere with
 * shortcuts elsewhere in the app:
 * - `Cmd/Ctrl+S` → `onSave` (preventDefault stops the browser's
 *   Save Page dialog).
 * - `Esc` → `onCancel`.
 *
 * A live token count appears below the textarea, recomputed on
 * every keystroke via `estimateTokens`. Rust re-computes the
 * authoritative count from the saved file's full content on the
 * next `list_rules`; the editor's number may drift slightly during
 * editing — acceptable for v2.0.
 */

import type { KeyboardEvent } from "react";
import { estimateTokens } from "../lib/tokenEstimate";

// #region Component

/**
 * Props for {@link RuleEditor}.
 */
interface RuleEditorProps
{
  value: string;
  onChange: (next: string) => void;
  onSave: () => void;
  onCancel: () => void;
}

/**
 * Renders the editing textarea with a live token-count footer.
 *
 * @param props - See {@link RuleEditorProps}.
 * @returns The editor element, sized to fill its flex parent.
 */
export default function RuleEditor({ value, onChange, onSave, onCancel }: RuleEditorProps)
{
  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "s")
    {
      e.preventDefault();
      onSave();
      return;
    }

    if (e.key === "Escape")
    {
      e.preventDefault();
      onCancel();
    }
  };

  const tokens = estimateTokens(value);

  return (
    <div className="flex flex-1 flex-col">
      <textarea
        value={value}
        onChange={(e) => onChange(e.target.value)}
        onKeyDown={handleKeyDown}
        spellCheck={false}
        className="flex-1 w-full resize-none border-0 bg-slate-900 p-4 font-mono text-sm text-slate-200 outline-none focus:bg-slate-900"
      />
      <div className="border-t border-slate-800 px-4 py-2 text-[10px] text-slate-500">
        ~{tokens} tokens
      </div>
    </div>
  );
}

// #endregion