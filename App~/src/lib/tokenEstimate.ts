/**
 * Estimates token count for a string using the chars/4 heuristic.
 *
 * Single source of truth for the JS-side token estimate, mirroring
 * the Rust-side formula in `commands::rules::estimate_tokens`. Used
 * by `RuleEditor` for the live count while editing; `RulesList` and
 * `RulePane` consume the Rust-computed `RuleMeta.estimated_tokens`
 * which covers the full file content (frontmatter delimiters + YAML
 * + body), so the editor's number may slightly under-count during
 * editing until save — acceptable for v2.0.
 *
 * Note: `text.length` counts UTF-16 code units, while the Rust side
 * uses `chars().count()` (Unicode scalar values). They differ for
 * non-BMP code points (e.g. some emoji); for the typical English
 * rule text this is a non-issue at v2.0 scale. v2.1 may swap in a
 * real tokenizer if usage signals demand.
 */

/**
 * Rounded-up `chars / 4` heuristic.
 *
 * @param text - The string to estimate.
 * @returns Token count (always >= 0).
 */
export function estimateTokens(text: string): number
{
  return Math.ceil(text.length / 4);
}