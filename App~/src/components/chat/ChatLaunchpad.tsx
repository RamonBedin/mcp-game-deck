/**
 * Chat launchpad — empty-state replacement for the conversation panel.
 *
 *   1. A short brand-aware welcome.
 *   2. A 2×2 grid of workflow cards (curated entry points).
 *   3. A list of specialists, each with a one-liner.
 *
 * Both card kinds are clickable: workflow cards prefill the composer
 * with a slash command (when associated with one) or a free-form
 * prompt; specialist rows prefill `@agent-name `.
 *
 * The launchpad is purely presentational — wiring "click → prefill"
 * is the consumer's job (`ChatRoute` passes `onPickWorkflow` and
 * `onPickAgent` callbacks). When neither is provided, the components
 * stay static.
 *
 * @requires-backend B.08 recent commands cache — once available, a
 *   "Recent" section can be added above "Try a workflow".
 */

import { useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { useAgents } from "../../hooks/useAgents";
import { useCommands } from "../../hooks/useCommands";
import { listRecentCommands } from "../../ipc/commands";
import type { CatalogAgent, CatalogCommand } from "../../ipc/types";
import { useConversationStore } from "../../stores/conversationStore";
import Avatar, { type AvatarVariant } from "../atoms/Avatar";
import BrandHex from "../atoms/BrandHex";
import Pill from "../atoms/Pill";

// #region Types

/**
 * Shape of a single workflow entry rendered in the chat launchpad.
 *
 * Pairs the heading and body shown in the card with the prefill text that
 * gets dropped into the chat composer when the workflow is picked, plus an
 * optional hint for additional context.
 */
interface WorkflowSpec
{
  title: string;
  body: string;
  prefill: string;
  hint?: string;
}

/**
 * Props for the `ChatLaunchpad` component.
 *
 * Renders the empty-state launchpad shown before a conversation has started,
 * surfacing workflow shortcuts and agent mentions and reporting the user's
 * selection back to the parent.
 */
interface ChatLaunchpadProps
{
  onPickWorkflow?: (prefill: string) => void;
  onPickAgent?: (mention: string) => void;
  workflows?: WorkflowSpec[];
}

// #endregion

// #region Defaults

const DEFAULT_WORKFLOWS: WorkflowSpec[] = [
  {
    title: "Set up a 2D scene",
    body: "Camera, URP volume, sprite settings, TextMeshPro, default font.",
    prefill: "/plan-execute setup-2d-scene",
    hint: "↩ /plan-execute setup-2d-scene",
  },
  {
    title: "Bake lightmaps",
    body: "Configure progressive GPU lightmapper, bake current scene, report sizes.",
    prefill: "Bake lightmaps for the current scene.",
    hint: "↩ free-form prompt",
  },
  {
    title: "Profile this frame",
    body: "Capture frame timing, surface top hot paths, allocations, draw calls.",
    prefill: "@agent-performance-analyst profile the current scene",
    hint: "↩ via @performance-analyst",
  },
  {
    title: "Build for Android",
    body: "Switch platform, configure ARM64, set scripting backend, run build.",
    prefill: "/plan-execute build-android",
    hint: "↩ /plan-execute build-android",
  },
];

const variantForAgent = (agentName: string): AvatarVariant => {
  if (agentName.includes("shader"))
  { 
    return "shader"; 
  }

  if (agentName.includes("ui"))          
  { 
    return "ui"; 
  }

  if (agentName.includes("dots"))        
  { 
    return "dots"; 
  }

  if (agentName.includes("performance")) 
  { 
    return "perf"; 
  }

  if (agentName.includes("gameplay"))    
  { 
    return "gameplay"; 
  }

  if (agentName.includes("systems"))     
  { 
    return "systems"; 
  }

  if (agentName.includes("technical"))   
  { 
    return "techart"; 
  }

  if (agentName.includes("addressables"))
  { 
    return "addr"; 
  }

  if (agentName.includes("qa"))          
  { 
    return "qa"; 
  }

  return "unity";
};

const initialsForAgent = (agentName: string): string => {
  const tail = agentName.split("-").map((p) => p.charAt(0).toUpperCase());

  if (tail.length >= 2)
  {
    return (tail[0] + tail[1]).slice(0, 2);
  }

  return agentName.slice(0, 2).toUpperCase();
};

const MAX_AGENTS_VISIBLE = 5;

// #endregion

/**
 * Renders the launchpad. Reads the agent catalog via `useAgents()` so
 * the specialist list reflects whatever is currently registered.
 *
 * @param props - See {@link ChatLaunchpadProps}.
 * @returns The launchpad element.
 */
export default function ChatLaunchpad({onPickWorkflow, onPickAgent, workflows = DEFAULT_WORKFLOWS,}: ChatLaunchpadProps)
{
  const agents = useAgents();
  const commands = useCommands();
  const inFlight = useConversationStore((s) => s.inFlight);
  const visibleAgents = agents.slice(0, MAX_AGENTS_VISIBLE);

  const [recentNames, setRecentNames] = useState<string[]>([]);

  // fetch the MRU list once on mount. Failures fall back to a
  // hidden Recent section (the user just sees the curated workflows).
  useEffect(() => {
    let cancelled = false;

    listRecentCommands()
      .then((list) => {
        if (cancelled)
        {
          return;
        }
        setRecentNames(list);
      })
      .catch((err) => console.error("[launchpad] list_recent_commands failed:", err));

    return () => {
      cancelled = true;
    };
  }, []);

  const recentCommands = useMemo<CatalogCommand[]>(() => {
    const byName = new Map(commands.map((c) => [c.name, c]));
    const out: CatalogCommand[] = [];

    for (const name of recentNames)
    {
      const cmd = byName.get(name);

      if (cmd !== undefined)
      {
        out.push(cmd);
      }

      if (out.length >= 4)
      {
        break;
      }
    }

    return out;
  }, [commands, recentNames]);

  return (
    <div className="flex-1 overflow-auto bg-bg-1 flex flex-col items-center" style={{ padding: "44px 64px" }}>
      <div className="w-full" style={{ maxWidth: 720 }}>

        {/* Hero */}
        <div className="flex items-center gap-[18px] mb-9">
          <BrandHex size={56} />
          <div>
            <div className="font-hud text-[11px] tracking-[0.22em] uppercase text-brand-violet-soft mb-1.5">
              Ready when you are
            </div>
            <h1 className="m-0 font-body text-[24px] font-semibold text-txt-1 leading-tight tracking-[-0.005em]">
              What are we building today?
            </h1>
          </div>
        </div>

        {/* Recent commands — only when the cache has resolvable entries */}
        {recentCommands.length > 0 && (
          <>
            <SectionHeader label="Recent" />
            <div className="grid grid-cols-2 gap-2.5 mb-7">
              {recentCommands.map((c) => (
                <RecentCommandCard
                  key={c.name}
                  command={c}
                  disabled={inFlight}
                  onPick={() => onPickWorkflow?.(`/${c.name} `)}
                />
              ))}
            </div>
          </>
        )}

        {/* Workflows */}
        <SectionHeader label="Try a workflow" />
        <div className="grid grid-cols-2 gap-2.5 mb-7">
          {workflows.map((w) => (
            <WorkflowCard
              key={w.title}
              spec={w}
              disabled={inFlight}
              onPick={() => onPickWorkflow?.(w.prefill)}
            />
          ))}
        </div>

        {/* Agents */}
        <SectionHeader
          label="Or call a specialist"
          trailing={
            agents.length > MAX_AGENTS_VISIBLE
              ? <span className="font-mono text-[10px] text-brand-cyan normal-case tracking-normal">Browse all {agents.length} →</span>
              : undefined
          }
        />
        <div className="flex flex-col gap-1">
          {visibleAgents.map((agent) => (
            <AgentRow
              key={agent.name}
              agent={agent}
              disabled={inFlight}
              onPick={() => onPickAgent?.(`@agent-${agent.name} `)}
            />
          ))}
        </div>

      </div>
    </div>
  );
}

// #region Sub-components

/**
 * Props for the `SectionHeader` component.
 *
 * Renders a section heading with an optional trailing slot — typically used
 * for actions like a "See all" link, count pill, or icon button aligned to
 * the end of the row.
 */
interface SectionHeaderProps
{
  label: string;
  trailing?: ReactNode;
}

const SectionHeader = ({ label, trailing }: SectionHeaderProps) => (
  <div className="flex items-center gap-2.5 mb-3">
    <span className="font-hud text-[10px] tracking-[0.22em] uppercase text-txt-3">{label}</span>
    <span className="flex-1 h-px bg-line" aria-hidden="true" />
    {trailing}
  </div>
);

/**
 * Props for the `WorkflowCard` component.
 *
 * Renders a single workflow entry as a clickable card in the chat launchpad,
 * reporting selection back to the parent and exposing a disabled state for
 * contexts where the workflow shouldn't be actionable.
 */
interface WorkflowCardProps
{
  spec: WorkflowSpec;
  onPick: () => void;
  disabled?: boolean;
}

/**
 * Props for the `RecentCommandCard` component.
 *
 * Renders a recently-used catalog command as a clickable card, reporting
 * selection back to the parent and exposing a disabled state for contexts
 * where the command shouldn't be actionable.
 */
interface RecentCommandCardProps
{
  command: CatalogCommand;
  onPick: () => void;
  disabled?: boolean;
}

const RecentCommandCard = ({ command, onPick, disabled = false }: RecentCommandCardProps) => (
  <button
    type="button"
    onClick={onPick}
    disabled={disabled}
    className="flex flex-col gap-1.5 text-left rounded-r-3 border border-line bg-bg-2 px-[18px] py-4 transition-colors duration-[120ms] hover:bg-bg-3 hover:border-line-hard disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-bg-2 disabled:hover:border-line"
    style={{ minHeight: 92 }}
  >
    <div className="flex items-center gap-2">
      <span className="font-mono text-[13px] font-medium text-txt-1">/{command.name}</span>
      <span className="ml-auto">
        <Pill variant="subtle" size="sm">{command.source}</Pill>
      </span>
    </div>
    <div className="text-[12px] text-txt-3 leading-[1.55] flex-1">{command.description}</div>
    {command.argumentHint !== undefined && (
      <div className="font-mono text-[10px] text-txt-4 mt-1">↩ /{command.name} {command.argumentHint}</div>
    )}
  </button>
);

const WorkflowCard = ({ spec, onPick, disabled = false }: WorkflowCardProps) => (
  <button
    type="button"
    onClick={onPick}
    disabled={disabled}
    className="flex flex-col gap-1.5 text-left rounded-r-3 border border-line bg-bg-2 px-[18px] py-4 transition-colors duration-[120ms] hover:bg-bg-3 hover:border-line-hard disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-bg-2 disabled:hover:border-line"
    style={{ minHeight: 92 }}
  >
    <div className="text-[13.5px] font-medium text-txt-1">{spec.title}</div>
    <div className="text-[12px] text-txt-3 leading-[1.55] flex-1">{spec.body}</div>
    {spec.hint !== undefined && (
      <div className="font-mono text-[10px] text-txt-4 mt-1">{spec.hint}</div>
    )}
  </button>
);

/**
 * Props for the `AgentRow` component.
 *
 * Renders a single catalog agent as a clickable row in the agent picker,
 * reporting selection back to the parent and exposing a disabled state for
 * contexts where the agent shouldn't be actionable.
 */
interface AgentRowProps
{
  agent: CatalogAgent;
  onPick: () => void;
  disabled?: boolean;
}

const AgentRow = ({ agent, onPick, disabled = false }: AgentRowProps) => {
  const variant = variantForAgent(agent.name);
  const initials = initialsForAgent(agent.name);

  return (
    <button
      type="button"
      onClick={onPick}
      disabled={disabled}
      className="flex items-center gap-3 rounded-r-2 border border-line-soft bg-bg-2 px-3 py-2.5 text-left transition-colors duration-[120ms] hover:bg-bg-3 hover:border-line disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-bg-2 disabled:hover:border-line-soft"
    >
      <Avatar variant={variant} initials={initials} size={28} />
      <div className="flex-1 min-w-0">
        <div className="font-mono text-[12.5px] text-txt-1 font-medium">
          @{agent.name}
        </div>
        <div className="text-[11.5px] text-txt-3 leading-snug mt-0.5 truncate">
          {agent.description}
        </div>
      </div>
      <span className="text-[11px] text-txt-4 font-mono shrink-0">→</span>
    </button>
  );
};

// #endregion