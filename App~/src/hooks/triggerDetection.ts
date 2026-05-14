/**
 * Shared trigger-detection helper for the autocomplete hooks.
 *
 * Both `useSlashAutocomplete` (`/` commands) and `useAtAutocomplete`
 * (`@` agents + files) need the same "is the cursor inside a
 * trigger token?" parse — only the trigger character differs.
 * Extracting it here keeps the two hooks honest: a bug fix or rule
 * change lands in one place.
 *
 * The rules, lifted from `06-plans-crud-spec.md` → "Slash dropdown
 * behavior":
 *
 * 1. Anchor on the most recent `triggerChar` left of the cursor.
 * 2. The character immediately before the anchor must be whitespace
 *    OR the anchor must be at index 0. Anything else (letters,
 *    digits, `:`, `/`, `@`, etc.) means we're inside a word and the
 *    `triggerChar` is part of that word — no autocomplete.
 * 3. The text between the anchor and the cursor cannot contain
 *    whitespace — once the user typed a space the trigger token is
 *    settled and we're now into argument territory.
 *
 * Together these rules block:
 * - `abc/foo` (letter before `/`)
 * - `https://foo/bar` (letter before the last `/`)
 * - `foo@bar.com` (letter before `@`)
 * - `/save plan` (whitespace inside the query)
 */

// #region Types

/**
 * A detected active trigger: the anchor index of the trigger
 * character and the query string between it and the cursor (exclusive
 * of the trigger char itself).
 */
export interface TriggerMatch
{
  triggerStart: number;
  query: string;
}

// #endregion

// #region Helpers

/**
 * Scans `value` backward from `cursorPosition` for an active trigger
 * anchored on `triggerChar`. Returns the match's start offset and
 * the typed query when active; `null` when no trigger applies under
 * the rules in the module docblock.
 *
 * @param value - Current textarea content.
 * @param cursorPosition - Caret position within `value`.
 * @param triggerChar - Single-character trigger (`'/'` or `'@'`).
 * @returns The active trigger match or `null`.
 */
export function findActiveTrigger(value: string, cursorPosition: number, triggerChar: string,): TriggerMatch | null
{
  const triggerStart = value.lastIndexOf(triggerChar, cursorPosition - 1);

  if (triggerStart < 0)
  {
    return null;
  }

  if (triggerStart > 0)
  {
    const prevChar = value[triggerStart - 1];

    if (!/\s/.test(prevChar))
    {
      return null;
    }
  }

  const query = value.substring(triggerStart + 1, cursorPosition);

  if (/\s/.test(query))
  {
    return null;
  }

  return { triggerStart, query };
}

// #endregion