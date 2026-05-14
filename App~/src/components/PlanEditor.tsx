/**
 * Monospace textarea for editing a plan's markdown body.
 *
 * Pure controlled component: `value` is the draft text held in
 * `plansStore.editDraft`; `onChange` flushes user edits back to the
 * store; `onSave` / `onCancel` are wired by the parent.
 *
 * Keyboard shortcuts are scoped to the textarea itself (no
 * document-level listeners) so editing in this surface doesn't
 * interfere with shortcuts elsewhere in the app:
 * - `Cmd/Ctrl+S` → `onSave` (preventDefault stops the browser's
 *   Save Page dialog).
 * - `Esc` → `onCancel`.
 */

import type { KeyboardEvent } from "react";

// #region Component

/**
 * Props for {@link PlanEditor}.
 */
interface PlanEditorProps
{
  value: string;
  onChange: (next: string) => void;
  onSave: () => void;
  onCancel: () => void;
}

/**
 * Renders the editing textarea.
 *
 * @param props - See {@link PlanEditorProps}.
 * @returns The textarea element, sized to fill its flex parent.
 */
export default function PlanEditor({ value, onChange, onSave, onCancel }: PlanEditorProps)
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

  return (
    <textarea
      value={value}
      onChange={(e) => onChange(e.target.value)}
      onKeyDown={handleKeyDown}
      spellCheck={false}
      className="flex-1 w-full resize-none border-0 bg-slate-900 p-4 font-mono text-sm text-slate-200 outline-none focus:bg-slate-900"
    />
  );
}

// #endregion
