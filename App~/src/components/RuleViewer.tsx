/**
 * Markdown viewer for the right pane of the Rules tab.
 *
 * Pure read-only component: renders `rule.content` via `react-markdown`
 * using the same `markdownRenderers` overrides as `PlanViewer` and
 * F04's permission cards so typography is consistent across the app.
 *
 * Below the body: an `applies-to` chip strip (omitted when empty)
 * with a small grey caption explaining that the field is parsed,
 * displayed, and round-tripped through writes but NOT acted on by
 * Sets user expectation per spec —
 * v2.1 may flip the switch to filter per-subagent. The chip strip
 * reads `frontmatter["applies-to"]` defensively (the Rust side
 * preserves the raw frontmatter map verbatim, so the value may be
 * a YAML array, a scalar, or absent).
 */

import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { Rule } from "../ipc/types";
import { markdownRenderers } from "./requests/markdown-renderers";

// #region Helpers

const extractAppliesTo = (rule: Rule): string[] => {
  const raw = rule.frontmatter["applies-to"];

  if (!Array.isArray(raw))
  {
    return [];
  }


  return raw.filter((v): v is string => typeof v === "string");
};

// #endregion

// #region Component

/**
 * Props for {@link RuleViewer}.
 */
interface RuleViewerProps
{
  rule: Rule;
}

/**
 * Renders the body of a rule as styled markdown, with an optional
 * `applies-to` chip strip + v2.0 informational caption below.
 *
 * @param props - See {@link RuleViewerProps}.
 * @returns The viewer element, sized to fill its flex parent.
 */
export default function RuleViewer({ rule }: RuleViewerProps)
{
  const appliesTo = extractAppliesTo(rule);
  return (
    <div className="flex-1 overflow-y-auto p-4">
      <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownRenderers}>
        {rule.content}
      </ReactMarkdown>
      {appliesTo.length > 0 && (
        <div className="mt-6 border-t border-slate-800 pt-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs text-slate-500">applies to:</span>
            {appliesTo.map((tag) => (
              <span
                key={tag}
                className="rounded-full border border-slate-700 bg-slate-800 px-2 py-0.5 text-[10px] text-slate-300"
              >
                {tag}
              </span>
            ))}
          </div>
          <p className="mt-2 text-[10px] italic text-slate-600">
            v2.0: informational only
          </p>
        </div>
      )}
    </div>
  );
}

// #endregion