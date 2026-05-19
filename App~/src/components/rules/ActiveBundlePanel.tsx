/**
 * Active Bundle panel — third column in the Rules route showing the
 * exact text the supervisor will inject into the system prompt when
 * the next session starts.
 *
 * Two views:
 *   - Combined  · the bundle as the supervisor sees it (one continuous
 *                 markdown body, headers per rule)
 *   - By rule   · each rule's text in a separate card
 *
 * Footer summary indicates conflict status. v2.0 ships a simple
 * "no conflicts" badge — conflict detection itself is a v2.1 ask.
 */

import { useState } from "react";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";
import type { RuleMeta } from "../../ipc/types";

// #region Types

type View = "combined" | "by-rule";

/**
 * Props for the `ActiveBundlePanel` component.
 *
 * Renders the live preview of the currently active rule bundle, surfacing
 * the list of enabled rules, the assembled bundle text, per-rule bodies for
 * inspection, a conflict indicator, and a copy-to-clipboard control.
 */
interface ActiveBundlePanelProps
{
  enabledRules: RuleMeta[];
  bundleText?: string;
  ruleBodies?: Record<string, string>;
  conflictStatus?: "ok" | "conflicts" | "unknown";
  onCopy: () => void;
}

// #endregion

/**
 * Renders the panel. Consumer wires the data (bundle text + rule
 * bodies) via props since fetching is a cross-cutting concern that
 * doesn't belong in this presentational layer.
 *
 * @param props - See {@link ActiveBundlePanelProps}.
 * @returns The panel element.
 */
export default function ActiveBundlePanel({enabledRules, bundleText, ruleBodies = {}, conflictStatus = "unknown", onCopy,}: ActiveBundlePanelProps)
{
  const [view, setView] = useState<View>("combined");
  const isPlaceholder = bundleText === undefined;

  return (
    <aside
      className="shrink-0 flex flex-col bg-bg-0 border-l border-line"
      style={{ width: 320 }}
    >
      {/* Header */}
      <div className="px-4 pt-3.5 pb-2.5 border-b border-line-soft flex items-center gap-2">
        <span className="font-hud text-[9px] tracking-[0.18em] uppercase text-brand-violet-soft">
          Active bundle
        </span>
        <Pill variant="brand" size="sm">{enabledRules.length} rules</Pill>
        <span className="ml-auto font-mono text-[10px] text-txt-4">preview</span>
      </div>

      {/* Tabs */}
      <div className="bg-bg-1 border-b border-line-soft flex gap-0.5 px-3 py-2">
        <TabBtn label="Combined" active={view === "combined"} onClick={() => setView("combined")} />
        <TabBtn label="By rule"  active={view === "by-rule"}  onClick={() => setView("by-rule")} />
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto px-4 py-3.5 font-mono text-[11px] leading-[1.6] text-txt-3">
        {isPlaceholder ? (
          <PlaceholderBody />
        ) : view === "combined" ? (
          <CombinedView text={bundleText} />
        ) : (
          <ByRuleView rules={enabledRules} bodies={ruleBodies} />
        )}
      </div>

      {/* Footer */}
      <div className="px-4 py-2.5 border-t border-line-soft bg-bg-2 flex items-center gap-2">
        <ConflictPill status={conflictStatus} />
        <Button
          variant="ghost"
          size="sm"
          onClick={onCopy}
          disabled={isPlaceholder}
          className="ml-auto"
        >
          Copy
        </Button>
      </div>
    </aside>
  );
}

// #region Sub-components

/**
 * Props for the `TabBtn` component.
 *
 * Renders a single tab button inside a tab bar, reflecting the active state
 * with appropriate styling and reporting user clicks back to the parent.
 */
interface TabBtnProps
{
  label: string;
  active: boolean;
  onClick: () => void;
}

const TabBtn = ({ label, active, onClick }: TabBtnProps) => (
  <button
    type="button"
    onClick={onClick}
    className={[
      "px-2.5 py-1 rounded-r-1 font-mono text-[11px] transition-colors duration-[120ms]",
      active ? "bg-bg-4 text-txt-1" : "text-txt-3 hover:bg-bg-3/40",
    ].join(" ")}
  >
    {label}
  </button>
);

const ConflictPill = ({ status }: { status: "ok" | "conflicts" | "unknown" }) => {
  switch (status)
  {
    case "ok":
      return <Pill variant="ok" size="sm" dot>NO CONFLICTS</Pill>;
    case "conflicts":
      return <Pill variant="tier-write" size="sm" dot>CONFLICTS DETECTED</Pill>;
    case "unknown":
      return <Pill variant="subtle" size="sm">conflict scan pending</Pill>;
  }
};

const CombinedView = ({ text }: { text: string }) => (
  <pre className="whitespace-pre-wrap">{text}</pre>
);

const ByRuleView = ({ rules, bodies }: { rules: RuleMeta[]; bodies: Record<string, string> }) => (
  <div className="flex flex-col gap-3.5">
    {rules.map((r) => (
      <div key={r.name}>
        <div className="text-brand-cyan mb-1">## {r.name}</div>
        <div className="text-txt-3">
          {bodies[r.name] ?? "(no body fetched)"}
        </div>
      </div>
    ))}
  </div>
);

const PlaceholderBody = () => (
  <div className="text-txt-4 italic">
    Pending backend feature B.06 (preview_rules_bundle). Until the Tauri command exists, the
    bundle text isn't available to render.
  </div>
);

// #endregion