/**
 * Settings route — v2.0 UX Pass rewrite.
 *
 * Vertical sub-nav on the left, 5 panels on the right:
 *
 *   Connection  · live Unity + supervisor status, MCP server URL, project path
 *   Appearance  · theme (dark locked in v2.0; light deferred to v2.3+)
 *   Claude Code · CLI version, default permission mode picker, docs link
 *   Plugin      · loaded agents / commands / knowledge counts, cross-links to Library
 *   About       · package + app version, GitHub link, license, dev tools (gated)
 *
 * Dev tools moved into About → Diagnostics behind `import.meta.env.DEV`.
 */

import { openUrl } from "@tauri-apps/plugin-opener";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import StatusDot from "../components/atoms/StatusDot";
import Button from "../components/atoms/Button";
import Pill from "../components/atoms/Pill";
import ModelPicker from "../components/ModelPicker";
import PermissionModePicker from "../components/PermissionModePicker";
import { SettingsGroup, SettingRow } from "../components/settings/SettingsGroup";
import { useAgents } from "../hooks/useAgents";
import { useCommands } from "../hooks/useCommands";
import { checkClaudeInstallStatus, devCallUnityTool, devEmitTestEvent, getMcpEndpoint, listKnowledgeDocs, restartSupervisor, } from "../ipc/commands";
import { onUnityStatusChanged } from "../ipc/events";
import type { ClaudeInstallStatus, ConnectionStatus, SupervisorStatus, } from "../ipc/types";
import { useConnectionStore } from "../stores/connectionStore";
import { useSettingsStore } from "../stores/settingsStore";

// #region Constants

const PANELS = [
  { id: "connection",  label: "Connection" },
  { id: "appearance",  label: "Appearance" },
  { id: "claude-code", label: "Claude Code" },
  { id: "plugin",      label: "Plugin" },
  { id: "about",       label: "About" },
] as const;

type PanelId = typeof PANELS[number]["id"];

const GITHUB_URL = "https://github.com/RamonBedin/mcp-game-deck";
const LICENSE_URL = "https://github.com/RamonBedin/mcp-game-deck/blob/main/LICENSE";
const CLAUDE_DOCS_URL = "https://docs.claude.com/en/docs/claude-code/overview";
const DEFAULT_MCP_PORT = 8090;

// #endregion

// #region Helpers

const connectionToDot = (s: ConnectionStatus): "ok" | "busy" | "down" => {
  if (s === "connected")
  { 
    return "ok"; 
  }

  if (s === "busy")         
  { 
    return "busy"; 
  }

  return "down";
};

const supervisorToDot = (s: SupervisorStatus): "ok" | "busy" | "down" | "idle" => {
  if (s === "ready")
  { 
    return "ok"; 
  }

  if (s === "starting")
  {
    return "busy"; 
  }

  if (s === "crashed" || s === "failed")
  { 
    return "down"; 
  }

  return "idle";
};

const formatError = (err: unknown): string => {
  if (err instanceof Error)
  {
    return err.message;
  }

  if (typeof err === "string")
  {
    return err;
  }
  try
  {
    return JSON.stringify(err);
  }
  catch
  {
    return String(err);
  }
};

const openExternal = (url: string) => {
  void openUrl(url).catch((err) => {
    console.error("[settings] open url failed:", err);
  });
};

// #endregion

/**
 * Settings route component.
 *
 * @returns The route element.
 */
export default function SettingsRoute()
{
  const [active, setActive] = useState<PanelId>("connection");

  return (
    <div className="flex flex-col flex-1 min-h-0">
      <div className="px-7 pt-5 pb-4 border-b border-line bg-bg-2 shrink-0">
        <h1 className="m-0 font-hud text-[18px] font-bold tracking-[-0.005em] text-txt-1">
          Settings
        </h1>
      </div>

      <div className="flex flex-1 min-h-0">
        <SettingsNav active={active} onChange={setActive} />
        <section className="flex-1 overflow-auto bg-bg-1 px-12 pt-8 pb-16">
          <div style={{ maxWidth: 720 }}>
            {active === "connection"  && <ConnectionPanel />}
            {active === "appearance"  && <AppearancePanel />}
            {active === "claude-code" && <ClaudeCodePanel />}
            {active === "plugin"      && <PluginPanel />}
            {active === "about"       && <AboutPanel />}
          </div>
        </section>
      </div>
    </div>
  );
}

// #region Sub-nav

/**
 * Props for the `SettingsNav` component.
 *
 * Renders the navigation column inside the settings panel, highlighting the
 * active sub-panel and reporting user selections back to the parent.
 */
interface SettingsNavProps
{
  active: PanelId;
  onChange: (id: PanelId) => void;
}

const SettingsNav = ({ active, onChange }: SettingsNavProps) => (
  <aside
    className="shrink-0 flex flex-col gap-0.5 border-r border-line bg-bg-0 px-3 py-5"
    style={{ width: 200 }}
  >
    {PANELS.map((p) => {
      const isActive = p.id === active;
      return (
        <button
          key={p.id}
          type="button"
          onClick={() => onChange(p.id)}
          className={[
            "rounded-r-1 px-3 py-2 text-left text-[13px] transition-colors duration-[120ms]",
            isActive
              ? "bg-bg-3 text-txt-1 font-medium shadow-[inset_2px_0_0_var(--violet)]"
              : "text-txt-3 hover:bg-bg-3/60 hover:text-txt-2",
          ].join(" ")}
        >
          {p.label}
        </button>
      );
    })}
  </aside>
);

// #endregion

// #region Panel head

const PanelHead = ({ title, body }: { title: string; body: string }) => (
  <div className="mb-7">
    <h2 className="m-0 mb-1 text-[22px] text-txt-1 font-semibold">{title}</h2>
    <p className="m-0 text-[13.5px] text-txt-3">{body}</p>
  </div>
);

// #endregion

// #region Panel: Connection

const ConnectionPanel = () => {
  const unityStatus      = useConnectionStore((s) => s.unityStatus);
  const supervisorStatus = useConnectionStore((s) => s.supervisorStatus);
  const setUnityStatus   = useConnectionStore((s) => s.setUnityStatus);
  const projectPath      = useSettingsStore((s) => s.settings.unityProjectPath);

  const [restartResult, setRestartResult] = useState<string | null>(null);
  const [restarting, setRestarting] = useState(false);
  const [copiedUrl, setCopiedUrl] = useState(false);
  const [mcpUrl, setMcpUrl] = useState<string>(`http://localhost:${DEFAULT_MCP_PORT}`);

  useEffect(() => {
    let cancelled = false;

    getMcpEndpoint()
      .then((url) => {
        if (!cancelled)
        {
          setMcpUrl(url);
        }
      })
      .catch((err) => {
        console.error("[settings] failed to load mcp endpoint:", err);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  // Fast-path Unity status subscription (App.tsx polls; this catches transitions in ms).
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onUnityStatusChanged((payload) => {
      if (cancelled)
      {
        return;
      }

      setUnityStatus(payload.status);
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[settings] failed to subscribe to unity-status-changed:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [setUnityStatus]);

  const handleRestart = async () => {
    setRestarting(true);
    setRestartResult("restarting…");

    try
    {
      await restartSupervisor();
      setRestartResult("ok — watch status above");
    }
    catch (err)
    {
      setRestartResult(`error: ${formatError(err)}`);
    }
    finally
    {
      setRestarting(false);
    }
  };

  const mcpPort = (() => {
    try { return new URL(mcpUrl).port || String(DEFAULT_MCP_PORT); }
    catch { return String(DEFAULT_MCP_PORT); }
  })();

  const handleCopyUrl = () => {
    void navigator.clipboard.writeText(mcpUrl).then(() => {
      setCopiedUrl(true);
      window.setTimeout(() => setCopiedUrl(false), 2000);
    }).catch((err) => {
      console.error("[settings] copy mcp url failed:", err);
    });
  };

  return (
    <>
      <PanelHead
        title="Connection"
        body="Status of Unity Editor, the Claude Code supervisor, and MCP server endpoints."
      />

      <SettingsGroup label="Live status">
        <SettingRow
          label="Unity Editor"
          value={<StatusDot status={connectionToDot(unityStatus)} label={`${unityStatus.toUpperCase()} · ${mcpPort}`} />}
          meta="Polled every 2s · event-driven fast path active"
        />
        <SettingRow
          label="Claude Supervisor"
          value={<StatusDot status={supervisorToDot(supervisorStatus)} label={supervisorStatus.toUpperCase()} />}
          meta="Spawned by Tauri at app launch"
          action={
            <Button
              variant="default"
              size="sm"
              onClick={() => void handleRestart()}
              disabled={restarting}
            >
              {restarting ? "Restarting…" : "Restart"}
            </Button>
          }
        />
        <SettingRow
          label="MCP server URL"
          value={
            <code className="font-mono text-[12px] text-brand-cyan bg-bg-3 px-2.5 py-1 rounded-r-2 truncate">
              {mcpUrl}
            </code>
          }
          meta="Unity Editor MCP transport endpoint"
          action={
            <Button variant="ghost" size="sm" onClick={handleCopyUrl}>
              {copiedUrl ? "Copied!" : "Copy"}
            </Button>
          }
        />
        {restartResult !== null && (
          <div className="px-[18px] py-2 border-t border-line-soft font-mono text-[11px] text-txt-3">
            {restartResult}
          </div>
        )}
      </SettingsGroup>

      <SettingsGroup label="Active Unity project">
        <SettingRow
          label="Project path"
          value={
            <code className="rounded-r-2 bg-bg-3 px-2.5 py-1 text-[12px] text-txt-1 font-mono truncate">
              {projectPath ?? "(not set)"}
            </code>
          }
          meta="Set automatically when the Editor pin launches the app"
        />
      </SettingsGroup>
    </>
  );
};

// #endregion

// #region Panel: Appearance

const AppearancePanel = () => (
  <>
    <PanelHead
      title="Appearance"
      body="Theme + visual density. v2.0 is dark-only; the token layer is structured so a light theme is a one-screen change in v2.3+."
    />

    <SettingsGroup label="Theme">
      <SettingRow
        label="Color theme"
        value={
          <div
            role="radiogroup"
            aria-label="Theme"
            className="inline-flex rounded-r-2 border border-line-hard bg-bg-1 p-0.5"
          >
            <button
              type="button"
              role="radio"
              aria-checked={true}
              className="px-3 py-1 rounded-r-1 text-[12px] bg-bg-4 text-txt-1 font-medium"
            >
              Dark
            </button>
            <button
              type="button"
              role="radio"
              aria-checked={false}
              disabled
              title="Light theme arrives in v2.3"
              className="px-3 py-1 rounded-r-1 text-[12px] text-txt-4 cursor-not-allowed"
            >
              Light
            </button>
          </div>
        }
        meta="Light theme deferred to v2.3+. Tokens.css already namespaces the dark palette under `:root` so the swap stays trivial."
      />

      <SettingRow
        label="Density"
        value={<Pill variant="subtle" size="sm">compact</Pill>}
        meta="Senior-dev density (Linear / Raycast tone). Spacious mode is not on the roadmap."
      />
    </SettingsGroup>

    <SettingsGroup label="Typography">
      <SettingRow
        label="UI font"
        value={
          <code className="font-mono text-[12px] text-txt-2 bg-bg-3 px-2.5 py-1 rounded-r-2">
            Inter
          </code>
        }
        meta="Body copy, controls, headings"
      />
      <SettingRow
        label="Mono font"
        value={
          <code className="font-mono text-[12px] text-txt-2 bg-bg-3 px-2.5 py-1 rounded-r-2">
            JetBrains Mono
          </code>
        }
        meta="Code, paths, tool names, timestamps"
      />
      <SettingRow
        label="HUD font"
        value={
          <code className="font-mono text-[12px] text-txt-2 bg-bg-3 px-2.5 py-1 rounded-r-2">
            Orbitron
          </code>
        }
        meta="HUD strip text, section labels, brand mark"
      />
    </SettingsGroup>
  </>
);

// #endregion

// #region Panel: Claude Code

const ClaudeCodePanel = () => {
  const [installStatus, setInstallStatus] = useState<ClaudeInstallStatus | null>(null);

  useEffect(() => {
    let cancelled = false;

    checkClaudeInstallStatus()
      .then((status) => {
        if (cancelled)
        {
          return;
        }
        setInstallStatus(status);
      })
      .catch((err) => console.error("[settings] install status failed:", err));

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <PanelHead
        title="Claude Code"
        body="The underlying CLI agent and the model running your chats."
      />

      <SettingsGroup label="CLI install">
        <SettingRow
          label="Claude CLI version"
          value={
            installStatus === null ? (
              <span className="font-mono text-[11.5px] text-txt-4 italic">checking…</span>
            ) : installStatus.claudeVersion !== null ? (
              <code className="font-mono text-[12px] text-brand-cyan bg-bg-3 px-2.5 py-1 rounded-r-2">
                {installStatus.claudeVersion}
              </code>
            ) : (
              <Pill variant="tier-write" size="sm" dot>NOT DETECTED</Pill>
            )
          }
          meta="Output of `claude --version` on this machine"
          action={
            <Button variant="ghost" size="sm" onClick={() => openExternal(CLAUDE_DOCS_URL)}>
              Docs ↗
            </Button>
          }
        />
        <SettingRow
          label="Sign-in"
          value={
            installStatus === null ? (
              <span className="font-mono text-[11.5px] text-txt-4 italic">checking…</span>
            ) : installStatus.claudeAuthenticated ? (
              <Pill variant="ok" size="sm" dot>SIGNED IN</Pill>
            ) : (
              <Pill variant="tier-write" size="sm" dot>SIGNED OUT</Pill>
            )
          }
          meta="Run `claude /login` in a terminal to refresh credentials"
        />
        <SettingRow
          label="Agent SDK"
          value={
            installStatus === null ? (
              <span className="font-mono text-[11.5px] text-txt-4 italic">checking…</span>
            ) : installStatus.sdkInstalled ? (
              <Pill variant="ok" size="sm" dot>INSTALLED</Pill>
            ) : (
              <Pill variant="tier-write" size="sm" dot>MISSING</Pill>
            )
          }
          meta="`@anthropic-ai/claude-agent-sdk` bundled with the app"
        />
      </SettingsGroup>

      <SettingsGroup label="Behavior">
        <SettingRow
          label="Default permission mode"
          value={<PermissionModePicker variant="inline" />}
          meta="What Claude does on tool calls. Cycle with ⇧⇥ from the composer."
        />
        <SettingRow
          label="Model"
          value={<ModelPicker variant="inline" />}
          meta="Which Claude model handles your turns. Populated dynamically from your CLI login — picking applies to the next turn without resetting the session."
        />
      </SettingsGroup>
    </>
  );
};

// #endregion

// #region Panel: Plugin

const PluginPanel = () => {
  const navigate = useNavigate();
  const agents = useAgents();
  const commands = useCommands();
  const [knowledgeCount, setKnowledgeCount] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;

    listKnowledgeDocs()
      .then((list) => {
        if (cancelled)
        {
          return;
        }
        setKnowledgeCount(list.length);
      })
      .catch((err) => console.error("[settings] list knowledge failed:", err));

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <>
      <PanelHead
        title="Plugin"
        body="What MCP Game Deck contributes to Claude Code at runtime — bundled agents, slash commands, and the knowledge base."
      />

      <SettingsGroup label="Loaded surfaces">
        <SettingRow
          label="Agents"
          value={
            <span className="font-mono text-[13px] text-txt-1">
              {agents.length}
            </span>
          }
          meta="Specialists invokable via @agent-<name>"
          action={
            <Button variant="ghost" size="sm" onClick={() => navigate("/library")}>
              Browse ↗
            </Button>
          }
        />
        <SettingRow
          label="Slash commands"
          value={
            <span className="font-mono text-[13px] text-txt-1">
              {commands.length}
            </span>
          }
          meta="Plugin + built-in + user commands the autocomplete picks up"
          action={
            <Button variant="ghost" size="sm" onClick={() => navigate("/library")}>
              Browse ↗
            </Button>
          }
        />
        <SettingRow
          label="Knowledge docs"
          value={
            knowledgeCount === null ? (
              <span className="font-mono text-[11.5px] text-txt-4 italic">checking…</span>
            ) : (
              <span className="font-mono text-[13px] text-txt-1">{knowledgeCount}</span>
            )
          }
          meta="Markdown reference docs under Plugin~/knowledge/"
          action={
            <Button variant="ghost" size="sm" onClick={() => navigate("/library")}>
              Read ↗
            </Button>
          }
        />
      </SettingsGroup>

      <SettingsGroup label="Source">
        <SettingRow
          label="Package version"
          value={
            <code className="font-mono text-[12px] text-brand-cyan bg-bg-3 px-2.5 py-1 rounded-r-2">
              v{__PACKAGE_VERSION__}
            </code>
          }
          meta="Versioned by the repo-root package.json"
        />
      </SettingsGroup>
    </>
  );
};

// #endregion

// #region Panel: About

const AboutPanel = () => (
  <>
    <PanelHead
      title="About"
      body="Versions, license, and developer diagnostics."
    />

    <SettingsGroup label="Versions">
      <SettingRow
        label="Unity package"
        value={
          <code className="font-mono text-[12px] text-brand-cyan bg-bg-3 px-2.5 py-1 rounded-r-2">
            v{__PACKAGE_VERSION__}
          </code>
        }
        meta="From the repo root package.json"
      />
      <SettingRow
        label="App"
        value={
          <code className="font-mono text-[12px] text-brand-cyan bg-bg-3 px-2.5 py-1 rounded-r-2">
            v{__APP_VERSION__}
          </code>
        }
        meta="External Tauri app version"
      />
    </SettingsGroup>

    <SettingsGroup label="Project">
      <SettingRow
        label="License"
        value={<Pill variant="subtle" size="sm">MIT</Pill>}
        meta="Open source; see LICENSE for details"
        action={
          <Button variant="ghost" size="sm" onClick={() => openExternal(LICENSE_URL)}>
            View ↗
          </Button>
        }
      />
      <SettingRow
        label="Source"
        value={
          <code className="font-mono text-[12px] text-txt-2 bg-bg-3 px-2.5 py-1 rounded-r-2">
            github.com/RamonBedin/mcp-game-deck
          </code>
        }
        meta="Bug reports, PRs, releases"
        action={
          <Button variant="ghost" size="sm" onClick={() => openExternal(GITHUB_URL)}>
            Open ↗
          </Button>
        }
      />
    </SettingsGroup>

    {import.meta.env.DEV && <DevTools />}
  </>
);

// #endregion

// #region DevTools (gated by Vite DEV)

const DevTools = () => {
  const [callingUnityTool, setCallingUnityTool] = useState(false);
  const [unityToolResult, setUnityToolResult] = useState<string | null>(null);

  const handleCallUnityTool = async () => {
    setCallingUnityTool(true);
    setUnityToolResult("…");
    const start = performance.now();
    try
    {
      const result = await devCallUnityTool("console-get-logs", { count: 5 });
      const elapsed = Math.round(performance.now() - start);
      const preview = JSON.stringify(result).slice(0, 240);
      setUnityToolResult(`(${elapsed}ms) ${preview}${preview.length === 240 ? "…" : ""}`);
    }
    catch (err)
    {
      setUnityToolResult(`error: ${formatError(err)}`);
    }
    finally
    {
      setCallingUnityTool(false);
    }
  };

  return (
    <SettingsGroup label="Diagnostics · dev only">
      <SettingRow
        label="Emit test event"
        value="Simulate a Unity disconnect; polling reverts within 2s."
        action={
          <Button
            variant="default"
            size="sm"
            onClick={() => {
              void devEmitTestEvent().catch((err) => console.error("[dev] emit test event failed:", err));
            }}
          >
            Emit
          </Button>
        }
      />
      <SettingRow
        label="Call Unity tool"
        value={
          unityToolResult !== null
            ? <code className="font-mono text-[11px] text-txt-2 truncate">{unityToolResult}</code>
            : "Calls `console-get-logs` against the MCP server."
        }
        action={
          <Button
            variant="default"
            size="sm"
            onClick={() => void handleCallUnityTool()}
            disabled={callingUnityTool}
          >
            Call
          </Button>
        }
      />
    </SettingsGroup>
  );
};

// #endregion