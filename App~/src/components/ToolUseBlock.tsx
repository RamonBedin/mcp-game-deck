/**
 * Inline collapsible block rendered inside an assistant `Message` to
 * surface an MCP tool invocation that Claude is making (or has made).
 *
 * Pre-permission display: shows the tool name and the input JSON
 * before any roundtrip happens. The matching `<ToolResultBlock />`
 * appears as a sibling once the tool returns.
 *
 * Collapsed by default so prompts that fan out into many tool calls
 * don't drown out the assistant text. Native `<details>` + Tailwind
 * — no JSON syntax-highlight library on the hot path.
 */

// #region Types

interface ToolUseBlockProps
{
  name: string;
  input: unknown;
}

// #endregion

/**
 * Renders the tool invocation as a collapsible card with the tool
 * name in the summary and the prettified input JSON in the body.
 *
 * @param props - Tool name + input payload.
 * @returns The collapsible tool-use card.
 */
export default function ToolUseBlock({ name, input }: ToolUseBlockProps)
{
  return (
    <details className="rounded border border-violet-900/60 bg-violet-950/20 p-2 text-xs">
      <summary className="cursor-pointer font-mono text-violet-300">
        Tool call: {name}
      </summary>
      <pre className="mt-2 max-h-60 overflow-y-auto whitespace-pre-wrap rounded bg-slate-950 p-2 font-mono text-slate-300">
        {JSON.stringify(input, null, 2)}
      </pre>
    </details>
  );
}