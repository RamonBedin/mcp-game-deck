/**
 * Knowledge reader — list of bundled markdown docs + reader pane.
 *
 * Consumes docs with bodies already loaded (the route bulk-fetches
 * via `readAllKnowledgeDocs` once on mount). No async loading state
 * needed here; clicking a row is a synchronous body swap.
 *
 * Full-text search: when `highlightQuery` is non-empty, every text
 * leaf in the rendered markdown is scanned and matches are wrapped
 * with a styled `<mark>` (warn-amber, matches the design vision's
 * "search active" affordance). Sidebar titles get the same treatment.
 */

import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import ReactMarkdown from "react-markdown";
import type { Components } from "react-markdown";
import { highlightInChildren } from "../../lib/highlightMatch";
import Button from "../atoms/Button";
import IconButton from "../atoms/IconButton";
import Pill from "../atoms/Pill";

// #region Types

interface KnowledgeDocMeta
{
  id: string;
  num: string;
  title: string;
  wordCount: number;
}

interface KnowledgeDoc extends KnowledgeDocMeta
{
  body: string;
}

interface KnowledgeReaderProps
{
  /** Pre-fetched docs (metadata + body). */
  docs: KnowledgeDoc[];
  /** Optionally a pre-selected doc id; defaults to the first entry. */
  initialDocId?: string;
  /** Called when "Open in chat" is hit; consumer prefills the composer. */
  onOpenInChat: (doc: KnowledgeDocMeta) => void;
  /**
   * Free-text search query. When non-empty, occurrences are wrapped
   * in `<mark>` inside both the sidebar titles and the rendered body.
   * Empty string disables highlighting (no overhead).
   */
  highlightQuery?: string;
}

// #endregion

// #region Helpers

const formatReadTime = (wordCount: number): string => {
  const minutes = Math.max(1, Math.round(wordCount / 220));
  return `${minutes}m read`;
};

const formatWordCount = (count: number): string => {
  if (count >= 1000)
  {
    return `${(count / 1000).toFixed(1)}k words`;
  }

  return `${count} words`;
};

/**
 * Builds the react-markdown component map tuned for full-page
 * knowledge reading (v2 tokens, larger type, looser leading). When
 * `query` is non-empty, every text-bearing element passes its
 * `children` through `highlightInChildren` so matches get wrapped.
 *
 * Memoise the result at the consumer so a stable identity hits
 * react-markdown's child-equality fast path.
 */
function buildRenderers(query: string): Components
{
  const wrap = (children: React.ReactNode) => highlightInChildren(children, query);

  return {
    p: ({ children }) => (
      <p className="text-[14px] text-txt-2 mb-3.5 last:mb-0 leading-[1.65]">
        {wrap(children)}
      </p>
    ),
    strong: ({ children }) => (
      <strong className="font-semibold text-txt-1">{wrap(children)}</strong>
    ),
    em: ({ children }) => (
      <em className="italic text-txt-2">{wrap(children)}</em>
    ),
    pre: ({ children }) => (
      <pre className="rounded-r-2 bg-bg-0 border border-line-soft px-3.5 py-2.5 my-3 overflow-x-auto text-[12.5px] leading-[1.6]">
        {children}
      </pre>
    ),
    code: ({ children, className }) => {
      const isBlock =
        /language-/.test(className ?? "") || String(children).includes("\n");
      return isBlock ? (
        <code className={`font-mono text-txt-2 ${className ?? ""}`}>
          {children}
        </code>
      ) : (
        <code className="rounded-sm bg-bg-3 px-1.5 py-px text-[12.5px] font-mono text-brand-cyan">
          {wrap(children)}
        </code>
      );
    },
    ul: ({ children }) => (
      <ul className="list-disc pl-5 text-[14px] text-txt-2 mb-3.5 space-y-1.5 leading-[1.6]">
        {children}
      </ul>
    ),
    ol: ({ children }) => (
      <ol className="list-decimal pl-5 text-[14px] text-txt-2 mb-3.5 space-y-1.5 leading-[1.6]">
        {children}
      </ol>
    ),
    li: ({ children }) => <li className="leading-[1.6]">{wrap(children)}</li>,
    a: ({ children, href }) => (
      <a
        href={href}
        className="text-brand-cyan hover:text-brand-cyan-soft underline underline-offset-2"
        target="_blank"
        rel="noopener noreferrer"
      >
        {wrap(children)}
      </a>
    ),
    h1: ({ children }) => (
      <h1 className="text-[24px] font-semibold text-txt-1 mb-4 mt-2 leading-tight">
        {wrap(children)}
      </h1>
    ),
    h2: ({ children }) => (
      <h2 className="text-[19px] font-semibold text-txt-1 mb-3 mt-7 leading-snug">
        {wrap(children)}
      </h2>
    ),
    h3: ({ children }) => (
      <h3 className="text-[16px] font-semibold text-txt-1 mb-2 mt-5 leading-snug">
        {wrap(children)}
      </h3>
    ),
    h4: ({ children }) => (
      <h4 className="text-[14.5px] font-semibold text-txt-2 mb-1.5 mt-4">
        {wrap(children)}
      </h4>
    ),
    blockquote: ({ children }) => (
      <blockquote className="border-l-2 border-line-hard pl-3.5 my-3 text-txt-3 italic">
        {wrap(children)}
      </blockquote>
    ),
    hr: () => <hr className="my-6 border-t border-line-soft" />,
  };
}

// #endregion

/**
 * Renders the reader. List on the left, doc body on the right.
 *
 * @param props - See {@link KnowledgeReaderProps}.
 * @returns The reader element.
 */
export default function KnowledgeReader({
  docs,
  initialDocId,
  onOpenInChat,
  highlightQuery = "",
}: KnowledgeReaderProps)
{
  const [selectedId, setSelectedId] = useState<string | null>(
    initialDocId ?? docs[0]?.id ?? null,
  );

  const selectedDoc = selectedId === null ? null : docs.find((d) => d.id === selectedId) ?? null;

  const renderers = useMemo(() => buildRenderers(highlightQuery), [highlightQuery]);

  // #region Find-next/prev (Ctrl+F-style navigation)

  const bodyRef = useRef<HTMLDivElement>(null);
  const [matchCount, setMatchCount] = useState(0);
  const [matchIdx, setMatchIdx] = useState(0);

  // Re-count matches after each render whose inputs could change them
  // (doc swap or new search term). useLayoutEffect runs after DOM commit
  // but before paint, so the count is in sync with what the user sees.
  useLayoutEffect(() => {
    if (bodyRef.current === null)
    {
      setMatchCount(0);
      return;
    }

    if (highlightQuery.trim().length === 0)
    {
      setMatchCount(0);
      return;
    }

    const hits = bodyRef.current.querySelectorAll<HTMLElement>("mark[data-search-hit]");
    setMatchCount(hits.length);
    setMatchIdx(0);
  }, [selectedId, highlightQuery]);

  // Whenever the active index changes, scroll that <mark> into view and
  // mark it as "current" via a data attribute — sibling marks get the
  // attribute cleared so only one carries the strong outline at a time.
  useEffect(() => {
    if (bodyRef.current === null)
    {
      return;
    }

    const hits = bodyRef.current.querySelectorAll<HTMLElement>("mark[data-search-hit]");
    hits.forEach((el, i) => {
      if (i === matchIdx)
      {
        el.setAttribute("data-search-current", "");
        el.style.boxShadow = "0 0 0 2px var(--warn)";
        el.scrollIntoView({ behavior: "smooth", block: "center" });
      }
      else
      {
        el.removeAttribute("data-search-current");
        el.style.boxShadow = "none";
      }
    });
  }, [matchIdx, matchCount]);

  const stepMatch = (direction: 1 | -1) => {
    if (matchCount === 0)
    {
      return;
    }
    setMatchIdx((prev) => (prev + direction + matchCount) % matchCount);
  };

  // #endregion

  return (
    <div className="flex flex-1 min-h-0 bg-bg-1">
      {/* List */}
      <aside className="shrink-0 border-r border-line bg-bg-0 flex flex-col" style={{ width: 260 }}>
        <div className="px-3.5 py-3.5 shrink-0">
          <div className="font-hud text-[9px] tracking-[0.18em] uppercase text-txt-4">
            Knowledge base · {docs.length} docs
          </div>
        </div>
        <div className="px-2 pb-3.5 overflow-y-auto flex-1 min-h-0">
          {docs.map((d) => {
            const active = d.id === selectedId;
            return (
              <button
                key={d.id}
                type="button"
                onClick={() => setSelectedId(d.id)}
                className={[
                  "w-full text-left rounded-r-1 px-2.5 py-2 mb-0.5 cursor-pointer transition-colors duration-[120ms]",
                  active
                    ? "bg-bg-3 shadow-[inset_2px_0_0_var(--violet)]"
                    : "hover:bg-bg-3/50",
                ].join(" ")}
              >
                <div className="flex items-baseline gap-2">
                  <span className="font-mono text-[10px] text-txt-4">{d.num}</span>
                  <span className={`text-[12.5px] ${active ? "text-txt-1 font-medium" : "text-txt-2"}`}>
                    {highlightInChildren(d.title, highlightQuery)}
                  </span>
                </div>
                <div className="font-mono text-[9.5px] text-txt-4 mt-0.5 pl-[22px]">
                  {formatWordCount(d.wordCount)}
                </div>
              </button>
            );
          })}
        </div>
      </aside>

      {/* Reader */}
      <section className="flex-1 min-w-0 flex flex-col">
        {selectedDoc !== null && (
          <div className="flex items-center gap-3.5 px-9 py-3.5 border-b border-line bg-bg-2 shrink-0">
            <span className="font-mono text-[11px] text-brand-violet-soft">{selectedDoc.num}</span>
            <h2 className="m-0 text-[15px] text-txt-1 font-medium">
              {highlightInChildren(selectedDoc.title, highlightQuery)}
            </h2>
            <Pill variant="subtle" size="sm">
              {formatWordCount(selectedDoc.wordCount)} · {formatReadTime(selectedDoc.wordCount)}
            </Pill>

            {highlightQuery.trim().length > 0 && (
              <MatchNav
                idx={matchIdx}
                count={matchCount}
                onPrev={() => stepMatch(-1)}
                onNext={() => stepMatch(1)}
              />
            )}

            <div className="ml-auto flex gap-2">
              <Button variant="ghost" size="sm">Copy reference</Button>
              <Button variant="default" size="sm" onClick={() => onOpenInChat(selectedDoc)}>
                Open in chat ↗
              </Button>
            </div>
          </div>
        )}

        <div ref={bodyRef} className="flex-1 overflow-auto px-12 py-8">
          <div className="mx-auto" style={{ maxWidth: 760 }}>
            {selectedDoc !== null && (
              <ReactMarkdown components={renderers}>{selectedDoc.body}</ReactMarkdown>
            )}
          </div>
        </div>
      </section>
    </div>
  );
}

// #region MatchNav

interface MatchNavProps
{
  idx: number;
  count: number;
  onPrev: () => void;
  onNext: () => void;
}

/**
 * Inline "find next / previous" chip shown in the reader header when a
 * search is active. Mirrors VS Code's Ctrl+F affordance — counter +
 * paired arrow buttons that wrap around at the ends. Counter switches
 * to "no results" copy when `count === 0`.
 */
const MatchNav = ({ idx, count, onPrev, onNext }: MatchNavProps) => (
  <div className="inline-flex items-center gap-1 rounded-r-2 border border-line-hard bg-bg-1 px-1.5 py-0.5">
    <IconButton
      size={20}
      onClick={onPrev}
      disabled={count === 0}
      aria-label="Previous match"
      title="Previous match"
    >
      <span style={{ fontSize: 11 }}>↑</span>
    </IconButton>
    <span
      className="font-mono text-[11px] px-1 select-none"
      style={{ color: count === 0 ? "var(--txt-4)" : "var(--warn)", minWidth: 48, textAlign: "center" }}
    >
      {count === 0 ? "no matches" : `${idx + 1} / ${count}`}
    </span>
    <IconButton
      size={20}
      onClick={onNext}
      disabled={count === 0}
      aria-label="Next match"
      title="Next match"
    >
      <span style={{ fontSize: 11 }}>↓</span>
    </IconButton>
  </div>
);

// #endregion
