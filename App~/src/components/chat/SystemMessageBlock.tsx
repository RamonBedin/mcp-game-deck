/**
 * Renders a CLI-generated synthetic assistant message — the kind that
 * comes from built-in slash commands like `/help` or `/cost` (KI-009).
 *
 * Visually distinct from regular assistant text: monospace, terminal-
 * style background, leading `▸` glyph, small "system" pill in the
 * header. The body supports basic markdown so multi-line responses
 * format nicely.
 *
 * The wire envelope (`SystemMessagePayload`) is plain text; we don't
 * try to parse command-specific shapes here — the host just shows what
 * the CLI sent verbatim. Future per-source styling (e.g. distinct
 * accent for plugin-emitted system text) can extend `source` without
 * breaking the contract.
 */

import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import type { SystemMessageSource } from "../../ipc/types";
import Pill from "../atoms/Pill";
import { markdownRenderers } from "../requests/markdown-renderers";

// #region Types

/**
 * Props for the `SystemMessageBlock` component.
 *
 * Renders an inline system message in the conversation transcript — used for
 * notices, status updates, and other host-originated content that isn't part
 * of the assistant's reply. The source token drives the block's iconography
 * and accent color.
 */
interface SystemMessageBlockProps
{
  text: string;
  source: SystemMessageSource;
}

// #endregion

// #region Component

/**
 * Renders the system block.
 *
 * @param props - See {@link SystemMessageBlockProps}.
 * @returns The block element.
 */
export default function SystemMessageBlock({ text, source }: SystemMessageBlockProps)
{
  return (
    <div
      className="rounded-r-3 border border-line-soft bg-bg-0 overflow-hidden"
      data-source={source}
    >
      <div className="px-3 pt-2 pb-1.5 flex items-center gap-2 border-b border-line-soft/50">
        <span className="font-mono text-[11px] text-txt-4 select-none">▸</span>
        <Pill variant="subtle" size="sm">system</Pill>
        <span className="font-mono text-[10.5px] text-txt-5 ml-auto">{labelFor(source)}</span>
      </div>
      <div className="px-3 py-2 font-mono text-[12px] leading-relaxed text-txt-2">
        <ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownRenderers}>{text}</ReactMarkdown>
      </div>
    </div>
  );
}

// #endregion

// #region Helpers

const labelFor = (source: SystemMessageSource): string => {
  switch (source)
  {
    case "cli-builtin": return "cli";
  }
};

// #endregion