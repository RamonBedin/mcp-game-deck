/**
 * Library route — Agents · Commands · Knowledge.
 *
 * Brand-new surface in v2.0 UX Pass (audit M.01). The plugin's
 * investment (10 specialists, 22 slash commands, 16 knowledge docs)
 * was previously only reachable through autocomplete dropdowns inside
 * the chat composer. The Library makes them browseable, searchable,
 * and "open-in-chat"-able.
 *
 * Three tabs share the same shell: header + search + tab strip + grid
 * (Agents / Commands) or reader (Knowledge). Search query filters
 * inline against name + description.
 */

import { useEffect, useMemo, useState } from "react";
import { useAgents } from "../hooks/useAgents";
import { useCommands } from "../hooks/useCommands";
import { listKnowledgeDocs, readKnowledgeDoc, type KnowledgeDocMeta, } from "../ipc/commands";
import type { CatalogAgent, CatalogCommand, CommandSource } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";
import { useNavigate } from "react-router-dom";
import AgentCard from "../components/library/AgentCard";
import KnowledgeReader from "../components/library/KnowledgeReader";
import LibraryTabs from "../components/library/LibraryTabs";
import Pill from "../components/atoms/Pill";

// #region Types

export type LibraryTab = "agents" | "commands" | "knowledge";

// #endregion

// #region Helpers

const filterAgents = (agents: CatalogAgent[], query: string): CatalogAgent[] => {
  const q = query.trim().toLowerCase();

  if (q.length === 0)
  {
    return agents;
  }

  return agents.filter(
    (a) =>
      a.name.toLowerCase().includes(q) ||
      a.description.toLowerCase().includes(q),
  );
};

const filterCommands = (commands: CatalogCommand[], query: string): CatalogCommand[] => {
  const q = query.trim().toLowerCase();

  if (q.length === 0)
  {
    return commands;
  }

  return commands.filter(
    (c) =>
      c.name.toLowerCase().includes(q) ||
      c.description.toLowerCase().includes(q),
  );
};

const SOURCE_BADGE: Record<CommandSource, string> = {
  "built-in":     "BUILT-IN",
  plugin:         "PLUGIN",
  "user-command": "USER",
  "third-party":  "EXT",
};

// #endregion

/**
 * Library route.
 *
 * @returns The route element.
 */
export default function LibraryRoute()
{
  const agents = useAgents();
  const commands = useCommands();
  const sendMessage = useConversationStore((s) => s.sendMessage);
  const inFlight = useConversationStore((s) => s.inFlight);
  const navigate = useNavigate();

  const [active, setActive] = useState<LibraryTab>("agents");
  const [search, setSearch] = useState<string>("");
  const [knowledgeDocs, setKnowledgeDocs] = useState<KnowledgeDocMeta[] | null>(null);

  const filteredAgents   = useMemo(() => filterAgents(agents, search),       [agents, search]);
  const filteredCommands = useMemo(() => filterCommands(commands, search), [commands, search]);

  // Fetch the knowledge doc list once on mount (B.10). Failures fall
  // through to the placeholder body so users still see something useful.
  useEffect(() => {
    let cancelled = false;

    listKnowledgeDocs()
      .then((list) => {
        if (cancelled)
        {
          return;
        }
        setKnowledgeDocs(list);
      })
      .catch((err) => {
        console.error("[library] list_knowledge_docs failed:", err);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // #region Handlers

  const handleOpenAgent = (agent: CatalogAgent) => {
    navigate("/chat");
    void sendMessage(`@agent-${agent.name} `);
  };

  const handleOpenCommand = (cmd: CatalogCommand) => {
    navigate("/chat");
    void sendMessage(`/${cmd.name} `);
  };

  // #endregion

  const counts: Record<LibraryTab, number> = {
    agents:    agents.length,
    commands:  commands.length,
    knowledge: knowledgeDocs?.length ?? 16,
  };

  const handleOpenKnowledge = (doc: KnowledgeDocMeta) => {
    navigate("/chat");
    void sendMessage(`Please read knowledge doc ${doc.id} and apply it to the current task.`);
  };

  return (
    <div className="flex flex-col flex-1 min-h-0">
      {/* Header */}
      <div className="px-7 pt-5 pb-3 border-b border-line bg-bg-2 shrink-0">
        <h1 className="m-0 font-hud text-[18px] font-bold tracking-[-0.005em] text-txt-1">
          Library
        </h1>
        <p className="m-0 mt-1 text-[12.5px] text-txt-3">
          Everything this plugin makes available. Click any card to open a chat with it pre-mentioned.
        </p>
      </div>

      <LibraryTabs
        active={active}
        onChange={setActive}
        counts={counts}
        searchQuery={search}
        onSearchChange={setSearch}
      />

      {active === "agents" && (
        <div className="flex-1 overflow-auto px-7 py-6 bg-bg-1">
          <div className="grid gap-3.5" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))" }}>
            {filteredAgents.map((a) => (
              <AgentCard key={a.name} agent={a} onOpen={handleOpenAgent} disabled={inFlight} />
            ))}
          </div>
          {filteredAgents.length === 0 && (
            <p className="text-[13px] text-txt-3">No agents match "{search}".</p>
          )}
        </div>
      )}

      {active === "commands" && (
        <div className="flex-1 overflow-auto px-7 py-6 bg-bg-1">
          <div className="grid gap-3.5" style={{ gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))" }}>
            {filteredCommands.map((c) => (
              <CommandCard key={c.name} command={c} onOpen={handleOpenCommand} disabled={inFlight} />
            ))}
          </div>
          {filteredCommands.length === 0 && (
            <p className="text-[13px] text-txt-3">No commands match "{search}".</p>
          )}
        </div>
      )}

      {active === "knowledge" && (
        knowledgeDocs === null || knowledgeDocs.length === 0
          ? <KnowledgePlaceholder />
          : <KnowledgeReader
              docs={knowledgeDocs}
              loadDoc={readKnowledgeDoc}
              onOpenInChat={handleOpenKnowledge}
            />
      )}
    </div>
  );
}

// #region CommandCard

/**
 * Props for the `CommandCard` component.
 *
 * Renders a single catalog command as a clickable card, reporting open
 * requests back to the parent and exposing a disabled state for contexts
 * where the command shouldn't be actionable.
 */
interface CommandCardProps
{
  command: CatalogCommand;
  onOpen: (cmd: CatalogCommand) => void;
  disabled?: boolean;
}

const CommandCard = ({ command, onOpen, disabled = false }: CommandCardProps) => (
  <button
    type="button"
    onClick={() => onOpen(command)}
    disabled={disabled}
    className="text-left rounded-r-3 border border-line bg-bg-2 px-[18px] py-4 flex flex-col gap-2.5 transition-colors duration-[120ms] hover:bg-bg-3/40 hover:border-line-hard disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-bg-2 disabled:hover:border-line"
  >
    <div className="flex items-center gap-2">
      <span className="font-mono text-[13px] text-txt-1 font-medium">/{command.name}</span>
      {command.argumentHint !== undefined && (
        <span className="font-mono text-[10px] text-txt-4">{command.argumentHint}</span>
      )}
      <span className="ml-auto">
        <Pill variant="subtle" size="sm">{SOURCE_BADGE[command.source]}</Pill>
      </span>
    </div>
    <div className="text-[12.5px] text-txt-3 leading-[1.55]" style={{ minHeight: 36 }}>
      {command.description}
    </div>
  </button>
);

// #endregion

// #region KnowledgePlaceholder

const KnowledgePlaceholder = () => (
  <div className="flex-1 flex items-center justify-center bg-bg-1 p-12">
    <div className="max-w-md text-center">
      <div className="font-hud text-[10px] tracking-[0.22em] uppercase text-txt-4 mb-3">
        No knowledge docs found
      </div>
      <h2 className="m-0 text-[18px] text-txt-1 font-semibold mb-3">
        Plugin~/knowledge/ is empty or missing
      </h2>
      <p className="m-0 text-[13px] text-txt-3 leading-[1.6]">
        The Library Knowledge tab reads from <code className="font-mono text-[12px] text-brand-cyan px-1.5 py-px bg-bg-3 rounded">Plugin~/knowledge/</code> via the Tauri commands <code className="font-mono text-[12px] text-brand-cyan px-1.5 py-px bg-bg-3 rounded">list_knowledge_docs()</code> and <code className="font-mono text-[12px] text-brand-cyan px-1.5 py-px bg-bg-3 rounded">read_knowledge_doc(id)</code>. Check that the package ships with knowledge docs.
      </p>
    </div>
  </div>
);

// #endregion