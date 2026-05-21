/**
 * Derive 2-letter avatar initials from a human or login name.
 *
 * Splits on whitespace, dot, underscore, and dash. With 2+ tokens uses
 * the first letter of the first and last tokens (e.g. "Ramon Bedin" →
 * "RB", "ramon.bedin" → "RB"). With a single token uses the first two
 * characters (e.g. "nicollas" → "NI"). Empty/null input falls back to
 * `"??"` so the avatar always renders a stable 2-char box.
 *
 * @param name - The raw name to derive initials from.
 * @returns A 2-character uppercase string suitable for the Avatar atom.
 */
export function deriveInitials(name: string | null | undefined): string
{
  if (!name)
  {
    return "??";
  }

  const tokens = name.trim().split(/[\s._-]+/).filter(Boolean);

  if (tokens.length >= 2)
  {
    return (tokens[0][0] + tokens[tokens.length - 1][0]).toUpperCase();
  }

  const t = tokens[0] ?? "";

  if (t.length === 0)
  {
    return "??";
  }

  return (t.length >= 2 ? t.slice(0, 2) : t.padEnd(2, "?")).toUpperCase();
}