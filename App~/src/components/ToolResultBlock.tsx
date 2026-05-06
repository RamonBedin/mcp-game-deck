/**
 * Inline collapsible block rendered inside an assistant `Message`
 * after a `<ToolUseBlock />` to surface what the MCP tool returned
 * (or errored with).
 *
 * Content shape matches Anthropic's `tool_result` block: usually a
 * string but can be a structured array of content items. We display
 * strings verbatim and JSON-stringify everything else.
 *
 * Collapsed by default. `isError: true` re-skins the summary +
 * payload in red to distinguish tool failures from successful
 * returns.
 */

// #region Types

interface ToolResultBlockProps
{
  content: unknown;
  isError: boolean;
}

// #endregion

// #region Helpers

/**
 * Renders any tool-result content as a printable string. Strings
 * pass through unchanged; everything else is JSON-stringified with
 * 2-space indent.
 *
 * @param content - The raw `content` payload from the tool_result block.
 * @returns A printable string for the `<pre>` body.
 */
function stringifyContent(content: unknown): string
{
  if (typeof content === "string")
  {
    return content;
  }

  return JSON.stringify(content, null, 2);
}

// #endregion

/**
 * Renders the tool result as a collapsible card. Red-skinned when
 * `isError`, neutral otherwise.
 *
 * @param props - Tool result payload + error flag.
 * @returns The collapsible tool-result card.
 */
export default function ToolResultBlock({ content, isError }: ToolResultBlockProps)
{
  const label = isError ? "Tool error" : "Tool result";
  const summaryColor = isError ? "text-rose-300" : "text-emerald-300";
  const borderColor = isError ? "border-rose-900/60" : "border-emerald-900/60";
  const bgColor = isError ? "bg-rose-950/20" : "bg-emerald-950/20";
  const bodyColor = isError ? "text-rose-200" : "text-slate-300";

  return (
    <details className={`rounded border p-2 text-xs ${borderColor} ${bgColor}`}>
      <summary className={`cursor-pointer font-mono ${summaryColor}`}>
        {label}
      </summary>
      <pre className={`mt-2 max-h-60 overflow-y-auto whitespace-pre-wrap rounded bg-slate-950 p-2 font-mono ${bodyColor}`}>
        {stringifyContent(content)}
      </pre>
    </details>
  );
}