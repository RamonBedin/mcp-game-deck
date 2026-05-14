/**
 * Markdown viewer for the right pane of the Plans tab.
 *
 * Pure read-only component: renders `plan.content` via `react-markdown`
 * using the shared `markdownRenderers` overrides from the requests
 * subdir, so the slate-palette typography matches F04's permission /
 * question cards. The component fills its flex parent vertically and
 * scrolls overflow internally.
 */

import ReactMarkdown from "react-markdown";
import type { Plan } from "../ipc/types";
import { markdownRenderers } from "./requests/markdown-renderers";

// #region Component

/**
 * Props for {@link PlanViewer}.
 */
interface PlanViewerProps
{
  plan: Plan;
}

/**
 * Renders the body of a plan as styled markdown.
 *
 * @param props - See {@link PlanViewerProps}.
 * @returns The viewer element, sized to fill its flex parent.
 */
export default function PlanViewer({ plan }: PlanViewerProps)
{
  return (
    <div className="flex-1 overflow-y-auto p-4">
      <ReactMarkdown components={markdownRenderers}>
        {plan.content}
      </ReactMarkdown>
    </div>
  );
}

// #endregion