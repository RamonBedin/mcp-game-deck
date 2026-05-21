import type { Components } from "react-markdown";

/**
 * Tailwind-styled component overrides for `react-markdown`.
 *
 * Used by `RequestCard` (task 3.2) to render markdown content in
 * permission and question card bodies. Styled to match the chat's
 * existing slate palette; F09 (Design Handoff) restyles in-place
 * without touching variant components.
 *
 * v9 API note: `react-markdown@9` removed the `inline` prop from the
 * `code` component. Inline-vs-block detection now uses a `language-*`
 * className regex plus a newline heuristic. Fenced blocks already get
 * wrapped in `<pre><code>` by the v9 AST, so the outer container
 * chrome lives in the `pre` override; the `code` override only carries
 * typography (and the inline pill styling for backticks).
 */
export const markdownRenderers: Components = {
  p: ({ children }) => (
    <p className="text-sm text-slate-200 mb-2 last:mb-0 leading-relaxed">
      {children}
    </p>
  ),
  strong: ({ children }) => (
    <strong className="font-semibold text-slate-100">{children}</strong>
  ),
  em: ({ children }) => (
    <em className="italic text-slate-300">{children}</em>
  ),
  pre: ({ children }) => (
    <pre className="rounded bg-slate-900 p-2 my-2 overflow-x-auto">
      {children}
    </pre>
  ),
  code: ({ children, className }) => {
    const isBlock =
      /language-/.test(className ?? "") || String(children).includes("\n");
    return isBlock ? (
      <code className={`text-xs font-mono text-slate-200 ${className ?? ""}`}>
        {children}
      </code>
    ) : (
      <code className="rounded bg-slate-900 px-1 py-0.5 text-xs font-mono text-emerald-300">
        {children}
      </code>
    );
  },
  ul: ({ children }) => (
    <ul className="list-disc list-inside text-sm text-slate-200 mb-2 space-y-0.5">
      {children}
    </ul>
  ),
  ol: ({ children }) => (
    <ol className="list-decimal list-inside text-sm text-slate-200 mb-2 space-y-0.5">
      {children}
    </ol>
  ),
  li: ({ children }) => <li className="leading-relaxed">{children}</li>,
  a: ({ children, href }) => (
    <a
      href={href}
      className="text-sky-400 hover:text-sky-300 underline"
      target="_blank"
      rel="noopener noreferrer"
    >
      {children}
    </a>
  ),
  h1: ({ children }) => (
    <h1 className="text-base font-semibold text-slate-100 mb-2 mt-1">
      {children}
    </h1>
  ),
  h2: ({ children }) => (
    <h2 className="text-sm font-semibold text-slate-100 mb-1.5 mt-2">
      {children}
    </h2>
  ),
  h3: ({ children }) => (
    <h3 className="text-sm font-semibold text-slate-200 mb-1 mt-1.5">
      {children}
    </h3>
  ),
  blockquote: ({ children }) => (
    <blockquote className="border-l-2 border-slate-600 pl-3 my-2 text-slate-300 italic">
      {children}
    </blockquote>
  ),
  del: ({ children }) => (
    <del className="text-slate-400 line-through">{children}</del>
  ),
  table: ({ children }) => (
    <div className="my-2 overflow-x-auto">
      <table className="w-full border-collapse text-sm text-slate-200">
        {children}
      </table>
    </div>
  ),
  thead: ({ children }) => (
    <thead className="border-b border-slate-600 bg-slate-900/40">{children}</thead>
  ),
  tbody: ({ children }) => <tbody>{children}</tbody>,
  tr: ({ children }) => (
    <tr className="border-b border-slate-800 last:border-b-0">{children}</tr>
  ),
  th: ({ children }) => (
    <th className="px-2 py-1.5 text-left font-semibold text-slate-100">{children}</th>
  ),
  td: ({ children }) => (
    <td className="px-2 py-1.5 align-top">{children}</td>
  ),
};