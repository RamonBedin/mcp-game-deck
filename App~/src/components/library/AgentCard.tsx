/**
 * Single agent card in the Library grid.
 *
 * Compresses one specialist into:
 *  - colored avatar + @name + plugin-source line
 *  - 2-line description
 *  - tag chips (categories) — small mono tags
 *  - sample-query line + "Open in chat" CTA
 *
 * Click handler routes through `LibraryRoute` → conversationStore →
 * ChatRoute, which prefills the composer with `@agent-name `.
 */

import type { CatalogAgent } from "../../ipc/types";
import Avatar, { type AvatarVariant } from "../atoms/Avatar";
import Button from "../atoms/Button";

// #region Types

/**
 * Props for the `AgentCard` component.
 *
 * Renders a single catalog agent as a clickable card with avatar, optional
 * sample prompt, and an open handler, plus a disabled state for contexts
 * where the agent shouldn't be actionable.
 */
interface AgentCardProps
{
  agent: CatalogAgent;
  variant?: AvatarVariant;
  initials?: string;
  sample?: string;
  onOpen: (agent: CatalogAgent) => void;
  disabled?: boolean;
}

// #endregion

// #region Helpers

const variantForAgent = (name: string): AvatarVariant => {
  if (name.includes("shader"))
  { 
    return "shader";
  }

  if (name.includes("ui"))          
  { 
    return "ui"; 
  }

  if (name.includes("dots"))        
  { 
    return "dots"; 
  }

  if (name.includes("performance")) 
  { 
    return "perf"; 
  }
  
  if (name.includes("gameplay"))    
  { 
    return "gameplay"; 
  }

  if (name.includes("systems"))     
  { 
    return "systems"; 
  }

  if (name.includes("technical"))   
  { 
    return "techart"; 
  }

  if (name.includes("addressables"))
  { 
    return "addr"; 
  }

  if (name.includes("qa"))          
  { 
    return "qa"; 
  }

  return "unity";
};

const initialsForAgent = (name: string): string => {
  const tail = name.split("-").map((p) => p.charAt(0).toUpperCase());

  if (tail.length >= 2)
  {
    return (tail[0] + tail[1]).slice(0, 2);
  }

  return name.slice(0, 2).toUpperCase();
};

const SOURCE_LABEL: Record<CatalogAgent["source"], string> = {
  "built-in":    "built-in",
  plugin:        "plugin",
  "third-party": "third-party",
};

// #endregion

/**
 * Renders one agent card.
 *
 * @param props - See {@link AgentCardProps}.
 * @returns The card element.
 */
export default function AgentCard({ agent, variant, initials, sample, onOpen, disabled = false }: AgentCardProps)
{
  const v = variant ?? variantForAgent(agent.name);
  const inits = initials ?? initialsForAgent(agent.name);

  return (
    <div className="rounded-r-3 border border-line bg-bg-2 px-[18px] py-4 flex flex-col gap-3 transition-colors duration-[120ms] hover:bg-bg-3/40 hover:border-line-hard">
      {/* Header row */}
      <div className="flex items-center gap-3">
        <Avatar variant={v} initials={inits} size={36} />
        <div className="flex-1 min-w-0">
          <div className="font-mono text-[13.5px] text-txt-1 font-medium truncate">
            @{agent.name}
          </div>
          <div className="font-mono text-[10px] text-txt-4 mt-0.5">
            {SOURCE_LABEL[agent.source]} · v1.1.0
          </div>
        </div>
      </div>

      {/* Description */}
      <div className="text-[12.5px] text-txt-3 leading-[1.55]" style={{ minHeight: 56 }}>
        {agent.description}
      </div>

      {/* Footer with sample query + action */}
      <div className="pt-3 border-t border-line-soft border-dashed flex items-center gap-2">
        {sample !== undefined && (
          <span className="font-mono text-[10.5px] text-txt-4 flex-1 min-w-0 truncate">
            "{sample}"
          </span>
        )}
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onOpen(agent)}
          disabled={disabled}
          className="shrink-0 ml-auto"
        >
          Open in chat ↗
        </Button>
      </div>
    </div>
  );
}