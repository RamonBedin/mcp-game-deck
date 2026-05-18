/**
 * First-run wizard — v2.0 UX Pass rewrite.
 *
 * Replaces v1's three independent card states with a single 3-step
 * wizard. Step indicator runs at the top of a hero with brand mark +
 * tagline + animated grid backdrop. Each step's active body slot is
 * the only thing that changes between Claude-missing /
 * not-authenticated / installing-SDK; the surrounding chrome
 * persists for visual continuity.
 *
 * State the wizard reflects:
 *   step 1 · Claude Code  — done | active | pending
 *   step 2 · Authenticate — done | active | pending
 *   step 3 · Agent SDK    — done | active | pending
 *
 * App.tsx mounts the wizard whenever `isInstallReady` returns false,
 * exactly as v1.
 */

import { openUrl } from "@tauri-apps/plugin-opener";
import { useCallback, useEffect, useState } from "react";
import type { ClaudeInstallStatus } from "../../ipc/types";
import { startSdkInstall } from "../../ipc/commands";
import { onSdkInstallCompleted, onSdkInstallFailed, onSdkInstallProgress, } from "../../ipc/events";
import BrandHex, { BrandGradientDefs } from "../atoms/BrandHex";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";
import StatusDot from "../atoms/StatusDot";

// #region Constants

const CLAUDE_INSTALL_DOCS_URL = "https://docs.claude.com/en/docs/claude-code/setup";
const LOGIN_COMMAND = "claude /login";
const COPY_FEEDBACK_MS = 2000;

// #endregion

// #region Types

type StepStatus = "done" | "active" | "pending";
type WizardVariant = "claude-missing" | "not-authenticated" | "installing-sdk";

/**
 * Props for the `FirstRunPanel` component.
 *
 * Renders the first-run setup panel that walks the user through installing
 * Claude Code, authenticating, and provisioning the SDK, driven by the
 * current install status reported by the host.
 */
interface FirstRunPanelProps
{
  status: ClaudeInstallStatus;
}

/**
 * Per-step status snapshot rendered by the first-run panel's progress
 * indicator.
 *
 * Maps each of the three setup steps — Claude Code install, authentication,
 * and SDK provisioning — to its current lifecycle state.
 */
interface StepStates
{
  claude: StepStatus;
  auth:   StepStatus;
  sdk:    StepStatus;
}

// #endregion

// #region Helpers

/** Returns true when every install field signals readiness. */
export function isInstallReady(status: ClaudeInstallStatus): boolean
{
  return status.claudeInstalled && status.claudeAuthenticated && status.sdkInstalled;
}

const variantFor = (status: ClaudeInstallStatus): WizardVariant => {
  if (!status.claudeInstalled)
  {
    return "claude-missing";
  }

  if (!status.claudeAuthenticated)
  {
    return "not-authenticated";
  }

  return "installing-sdk";
};

const stepsFor = (status: ClaudeInstallStatus): StepStates => {
  if (!status.claudeInstalled)
  {
    return { claude: "active", auth: "pending", sdk: "pending" };
  }

  if (!status.claudeAuthenticated)
  {
    return { claude: "done", auth: "active", sdk: "pending" };
  }

  if (!status.sdkInstalled)
  {
    return { claude: "done", auth: "done", sdk: "active" };
  }

  return { claude: "done", auth: "done", sdk: "done" };
};

// #endregion

/**
 * Pre-mount placeholder shown for the ~500ms before the first
 * `checkClaudeInstallStatus` call lands. Avoids a flash of CTA card
 * before we know the actual state.
 */
export function FirstRunCheckingScreen()
{
  return (
    <div className="flex h-screen w-full items-center justify-center bg-bg-1">
      <div className="text-[13px] text-txt-3">Checking installation…</div>
    </div>
  );
}

/**
 * Renders the wizard for the current install status.
 *
 * @param props - See {@link FirstRunPanelProps}.
 * @returns The wizard element.
 */
export default function FirstRunPanel({ status }: FirstRunPanelProps)
{
  const variant = variantFor(status);
  const steps = stepsFor(status);

  return (
    <div className="relative flex h-screen w-full flex-col overflow-hidden bg-bg-1 text-txt-1">
      <BrandGradientDefs />

      {/* Backdrop layers */}
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            "radial-gradient(70% 50% at 30% 0%, rgba(123,92,255,0.18) 0%, transparent 60%)," +
            "radial-gradient(50% 40% at 70% 100%, rgba(76,201,255,0.14) 0%, transparent 70%)",
        }}
      />
      <div
        className="pointer-events-none absolute inset-0"
        style={{
          backgroundImage:
            "linear-gradient(rgba(255,255,255,0.025) 1px, transparent 1px)," +
            "linear-gradient(90deg, rgba(255,255,255,0.025) 1px, transparent 1px)",
          backgroundSize: "32px 32px",
          maskImage: "radial-gradient(circle at center, transparent 0%, black 80%)",
          WebkitMaskImage: "radial-gradient(circle at center, transparent 0%, black 80%)",
        }}
      />

      <TopStrip />

      <main className="relative z-10 flex flex-1 flex-col items-center justify-center px-10 py-10">
        <BrandHex size={96} />
        <div className="mt-5 font-hud text-[10px] tracking-[0.32em] uppercase text-brand-cyan">
          MCP GAME DECK
        </div>
        <h1 className="mt-3.5 mb-3 text-center font-hud text-[40px] font-extrabold leading-[1.05] tracking-[-0.01em] text-txt-1">
          Let's get{" "}
          <span
            className="bg-grad-brand bg-clip-text"
            style={{ WebkitBackgroundClip: "text", color: "transparent" }}
          >
            Claude Code
          </span>{" "}
          online
        </h1>
        <p className="m-0 max-w-[480px] text-center text-[15px] leading-[1.55] text-txt-2">
          Your AI control deck for Unity. Two-minute setup.
        </p>

        <StepIndicator steps={steps} />

        <div
          className="mt-8 w-full max-w-[480px] rounded-r-4 border border-line bg-bg-2 px-7 py-6"
          style={{ boxShadow: "0 12px 48px -12px rgba(0,0,0,0.6), 0 0 0 1px rgba(123,92,255,0.15)" }}
        >
          {variant === "claude-missing"    && <ClaudeMissingBody />}
          {variant === "not-authenticated" && <NotAuthenticatedBody />}
          {variant === "installing-sdk"    && <InstallingSdkBody />}
        </div>
      </main>

      <Footer />
    </div>
  );
}

// #region Top strip

const TopStrip = () => (
  <div className="relative z-10 flex h-9 items-center gap-3 border-b border-line px-3.5 text-[11px] text-txt-3 backdrop-blur" style={{ background: "rgba(7,7,13,0.6)" }}>
    <BrandHex size={14} />
    <span className="text-txt-2">MCP Game Deck</span>
    <span className="text-txt-5">·</span>
    <span>Setup</span>
    <span className="ml-auto">
      <StatusDot status="idle" label="not connected" />
    </span>
  </div>
);

const Footer = () => (
  <div className="relative z-10 flex items-center gap-3.5 border-t border-line px-[22px] py-3 text-[11px] text-txt-4 backdrop-blur" style={{ background: "rgba(7,7,13,0.4)" }}>
    <a className="cursor-pointer text-txt-3" href={CLAUDE_INSTALL_DOCS_URL}>
      Need help? Read setup docs ↗
    </a>
    <span className="ml-auto">v{__PACKAGE_VERSION__} · MIT</span>
  </div>
);

// #endregion

// #region Step indicator

const StepIndicator = ({ steps }: { steps: StepStates }) => (
  <div className="mt-11 flex items-center gap-3.5">
    <WizardStep n="1" label="Claude Code"  status={steps.claude} />
    <WizardConnector status={steps.claude === "done" ? "done" : "pending"} />
    <WizardStep n="2" label="Authenticate" status={steps.auth} />
    <WizardConnector status={steps.auth === "done" ? "done" : "pending"} />
    <WizardStep n="3" label="Agent SDK"    status={steps.sdk} />
  </div>
);

const WizardStep = ({ n, label, status }: { n: string; label: string; status: StepStatus }) => {
  const config = {
    done:    { border: "rgba(74, 222, 128, 0.4)",  bg: "rgba(74, 222, 128, 0.06)", color: "var(--ok)",          icon: "✓", glow: false },
    active:  { border: "rgba(123, 92, 255, 0.5)",  bg: "rgba(123, 92, 255, 0.10)", color: "var(--violet-soft)", icon: n,   glow: true  },
    pending: { border: "var(--line-hard)",         bg: "transparent",              color: "var(--txt-4)",       icon: n,   glow: false },
  }[status];

  return (
    <div
      className="inline-flex items-center gap-2.5 rounded-full px-[18px] py-2.5 font-mono text-[12px]"
      style={{
        border: `1px solid ${config.border}`,
        background: config.bg,
        color: config.color,
        boxShadow: config.glow ? "0 0 24px -4px rgba(123,92,255,0.4)" : "none",
      }}
    >
      <span
        className="inline-flex items-center justify-center rounded-full text-[10px] font-bold"
        style={{
          width: 18,
          height: 18,
          background: status === "pending" ? "transparent" : config.color,
          color:      status === "pending" ? config.color : status === "done" ? "var(--bg-0)" : "#fff",
          border:     status === "pending" ? `1px solid ${config.color}` : "none",
        }}
      >
        {config.icon}
      </span>
      <span>{label}</span>
    </div>
  );
};

const WizardConnector = ({ status }: { status: "done" | "pending" }) => (
  <span
    style={{
      width:    28,
      height:   1,
      background: status === "done" ? "var(--ok)" : "var(--line-hard)",
      opacity:    status === "done" ? 0.6 : 1,
    }}
  />
);

// #endregion

// #region Step body — Claude missing

const ClaudeMissingBody = () => {
  const handleOpenDocs = async () => {
    try
    {
      await openUrl(CLAUDE_INSTALL_DOCS_URL);
    }
    catch (err)
    {
      console.error("[first-run] failed to open install docs:", err);
    }
  };

  return (
    <>
      <Eyebrow>Step 1 · Install Claude Code</Eyebrow>
      <p className="mb-4 text-[13.5px] leading-[1.55] text-txt-2">
        MCP Game Deck needs the Claude Code CLI installed locally. Follow Anthropic's setup
        guide — typical install is 1–2 minutes.
      </p>
      <Button variant="primary" size="lg" onClick={() => void handleOpenDocs()}>
        Open install docs ↗
      </Button>
      <p className="mt-3.5 flex items-center gap-2 font-mono text-[11.5px] text-txt-4">
        <StatusDot status="busy" />
        <span>Detecting install… auto-advances when ready</span>
      </p>
    </>
  );
};

// #endregion

// #region Step body — Not authenticated

const NotAuthenticatedBody = () => {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try
    {
      await navigator.clipboard.writeText(LOGIN_COMMAND);
      setCopied(true);
      window.setTimeout(() => setCopied(false), COPY_FEEDBACK_MS);
    }
    catch (err)
    {
      console.error("[first-run] clipboard copy failed:", err);
    }
  };

  return (
    <>
      <Eyebrow>Step 2 · Sign in to Claude</Eyebrow>
      <p className="mb-4 text-[13.5px] leading-[1.55] text-txt-2">
        Claude Code is installed but signed out. Open a terminal in your project and run:
      </p>
      <div className="mb-3.5 flex items-center gap-2.5 rounded-r-2 border border-line-hard bg-bg-0 px-3.5 py-2.5 font-mono text-[13px] text-brand-cyan">
        <span className="text-txt-4">$</span>
        <span className="flex-1">{LOGIN_COMMAND}</span>
        <Button variant="default" size="sm" onClick={() => void handleCopy()}>
          {copied ? "Copied!" : "Copy"}
        </Button>
      </div>
      <p className="m-0 flex items-center gap-2 font-mono text-[11.5px] text-txt-4">
        <StatusDot status="busy" />
        <span>Detecting sign-in… auto-advances when complete</span>
      </p>
    </>
  );
};

// #endregion

// #region Step body — Installing SDK

const InstallingSdkBody = () => {
  const [phase, setPhase] = useState<"installing" | "failed">("installing");
  const [progressMessage, setProgressMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const startInstall = useCallback(async () => {
    setPhase("installing");
    setErrorMessage(null);
    setProgressMessage(null);
    try
    {
      await startSdkInstall();
    }
    catch (err)
    {
      console.error("[first-run] startSdkInstall failed:", err);
      setPhase("failed");
      setErrorMessage(String(err));
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    let unlistenProgress:  (() => void) | null = null;
    let unlistenCompleted: (() => void) | null = null;
    let unlistenFailed:    (() => void) | null = null;

    onSdkInstallProgress((payload) => {
      if (cancelled)
      {
        return;
      }

      if (payload.message !== undefined)
      {
        setProgressMessage(payload.message);
      }
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlistenProgress = u;
        }
      })
      .catch((err) => console.error("[first-run] progress subscribe failed:", err));

    onSdkInstallCompleted(() => {})
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlistenCompleted = u;
        }
      })
      .catch((err) => console.error("[first-run] completed subscribe failed:", err));

    onSdkInstallFailed((payload) => {
      if (cancelled)
      {
        return;
      }

      setPhase("failed");
      setErrorMessage(payload.message);
    })
      .then((u) => {
        if (cancelled)
        {
          u();
        }
        else
        {
          unlistenFailed = u;
        }
      })
      .catch((err) => console.error("[first-run] failed subscribe failed:", err));

    void startInstall();

    return () => {
      cancelled = true;
      unlistenProgress?.();
      unlistenCompleted?.();
      unlistenFailed?.();
    };
  }, [startInstall]);

  if (phase === "failed")
  {
    return (
      <>
        <Eyebrow>Step 3 · Install failed</Eyebrow>
        <p className="mb-3.5 text-[13.5px] leading-[1.55] text-txt-2">
          The Agent SDK install didn't complete. Common causes: no network, npm not on PATH,
          registry blocked.
        </p>
        {errorMessage !== null && (
          <pre className="mb-3.5 max-h-40 overflow-y-auto whitespace-pre-wrap rounded-r-2 border border-line-soft bg-bg-0 p-3 font-mono text-[11px] text-bad">
            {errorMessage}
          </pre>
        )}
        <Button variant="primary" size="lg" onClick={() => void startInstall()}>
          Retry
        </Button>
      </>
    );
  }

  return (
    <>
      <Eyebrow>Step 3 · Installing Agent SDK</Eyebrow>
      <p className="mb-5 text-[13.5px] leading-[1.55] text-txt-2">
        {progressMessage ?? "Installing @anthropic-ai/claude-agent-sdk…"}
      </p>
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-bg-3" role="progressbar">
        <div
          className="h-full bg-grad-brand"
          style={{ width: "33%", animation: "pulse-soft 1.6s ease-in-out infinite" }}
        />
      </div>
      <p className="mt-3.5 m-0 flex items-center gap-2 font-mono text-[11.5px] text-txt-4">
        <Pill variant="subtle" size="sm">first launch</Pill>
        <span>usually 30 seconds to 2 minutes</span>
      </p>
    </>
  );
};

// #endregion

// #region Shared

const Eyebrow = ({ children }: { children: React.ReactNode }) => (
  <div className="mb-2.5 font-hud text-[11px] tracking-[0.22em] uppercase text-brand-violet-soft">
    {children}
  </div>
);

// #endregion