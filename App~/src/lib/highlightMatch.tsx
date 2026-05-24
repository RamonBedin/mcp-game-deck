/**
 * Match-highlighting helpers used by the Library Knowledge reader.
 *
 * `highlightInChildren` walks a React subtree (strings, fragments,
 * arrays, elements with children) and wraps every case-insensitive
 * occurrence of `query` in a styled `<mark>`. Designed to thread
 * through react-markdown's component children — text nodes arrive as
 * plain strings in `p`, `li`, `em`, etc, and we replace them with an
 * array of `string | <mark>` segments.
 *
 * The `<mark>` styling uses the design system's `--warn` token (amber)
 * with a 0.30 opacity background — same legibility logic as VS Code's
 * search highlight, scaled to the dark palette.
 */

import { Children, cloneElement, isValidElement } from "react";
import type { ReactNode } from "react";

// #region Public API

/**
 * Wraps every case-insensitive occurrence of `query` in `children`
 * with a `<mark>` element. Walks element children recursively so
 * inline markdown (em, strong, code) inside a paragraph still gets
 * its text-leaf matches highlighted.
 *
 * Returns `children` unchanged when `query` is empty or whitespace.
 *
 * @param children - The React subtree to scan.
 * @param query - Free-text search term; case-insensitive substring.
 * @returns A tree with the same shape, plus `<mark>` wrappers on hits.
 */
export function highlightInChildren(children: ReactNode, query: string): ReactNode
{
  if (query.trim().length === 0)
  {
    return children;
  }

  return Children.map(children, (child) => highlightOne(child, query));
}

// #endregion

// #region Internals

/**
 * Recursively walks a single React child and wraps occurrences of `query`
 * with the highlight mark.
 *
 * Handles strings via `splitAndMark`, passes primitive non-string nodes
 * through unchanged, recurses into arrays and element children, and returns
 * any other value as-is when no transformation applies.
 *
 * @param child - The React child to inspect and potentially transform.
 * @param query - Text to highlight inside any string children encountered.
 * @returns The (possibly transformed) child node.
 */
function highlightOne(child: ReactNode, query: string): ReactNode
{
  if (typeof child === "string")
  {
    return splitAndMark(child, query);
  }

  if (typeof child === "number" || typeof child === "boolean")
  {
    return child;
  }

  if (Array.isArray(child))
  {
    return child.map((c, i) => <Wrap key={i}>{highlightOne(c, query)}</Wrap>);
  }

  if (isValidElement(child))
  {
    const props = child.props as { children?: ReactNode };

    if (props.children === undefined)
    {
      return child;
    }

    return cloneElement(child, undefined, highlightInChildren(props.children, query));
  }

  return child;
}

/**
 * Splits a string around case-insensitive occurrences of `query` and wraps
 * each match in a `<mark>` highlight element.
 *
 * Returns the original string unchanged when the query is empty or has no
 * matches; otherwise returns an array of alternating plain-text fragments
 * and highlight elements, preserving the original casing of the matched
 * substrings.
 *
 * @param text - The source string to scan.
 * @param query - Text to highlight inside `text`, matched case-insensitively.
 * @returns The original string when no transformation applies, or an array
 *   of nodes interleaving plain text with `<mark>` elements.
 */
function splitAndMark(text: string, query: string): ReactNode
{
  const needle = query.toLowerCase();

  if (needle.length === 0)
  {
    return text;
  }

  const haystack = text.toLowerCase();
  const parts: ReactNode[] = [];
  let cursor = 0;
  let idx = haystack.indexOf(needle, cursor);

  while (idx !== -1)
  {
    if (idx > cursor)
    {
      parts.push(text.slice(cursor, idx));
    }
    parts.push(
      <mark
        key={`m-${idx}`}
        data-search-hit=""
        className="rounded-sm px-0.5 font-medium transition-shadow duration-[120ms]"
        style={{
          background: "rgba(245, 185, 70, 0.30)",
          color: "var(--warn)",
        }}
      >
        {text.slice(idx, idx + needle.length)}
      </mark>,
    );
    cursor = idx + needle.length;
    idx = haystack.indexOf(needle, cursor);
  }

  if (parts.length === 0)
  {
    return text;
  }

  if (cursor < text.length)
  {
    parts.push(text.slice(cursor));
  }

  return parts;
}

const Wrap = ({ children }: { children: ReactNode }) => <>{children}</>;

// #endregion