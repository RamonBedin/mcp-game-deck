/**
 * First-run gate shown above the rest of the app while Claude Code,
 * authentication, or the Agent SDK are missing/installing.
 *
 * Renders one of four surfaces driven by the `ClaudeInstallStatus`
 * snapshot from `check_claude_install_status`:
 *
 * 1. **Ready** — all four fields green. App.tsx never mounts the
 *    panel in this state; the gate falls through to the main layout.
 * 2. **Installing SDK** — Claude Code present + authenticated, SDK
 *    missing. Shows an indeterminate progress strip; the real
 *    percentage arrives via `sdk-install-progress`.
 * 3. **Claude Code missing** — `claudeInstalled = false`. CTA opens
 *    https://docs.claude.com/en/docs/claude-code/setup via the
 *    opener plugin.
 * 4. **Not authenticated** — Claude Code present, login missing.
 *    Shows `claude /login` with a copy-to-clipboard button.
 *
 * The panel is presentational — App.tsx owns the install-status
 * polling and decides when to mount it.
 */

import { openUrl } from "@tauri-apps/plugin-opener";
import { useCallback, useEffect, useState } from "react";
import { startSdkInstall } from "../ipc/commands";
import { onSdkInstallCompleted, onSdkInstallFailed, onSdkInstallProgress, } from "../ipc/events";
import type { ClaudeInstallStatus } from "../ipc/types";

// #region Constants

const CLAUDE_INSTALL_DOCS_URL = "https://docs.claude.com/en/docs/claude-code/setup";
const LOGIN_COMMAND = "claude /login";
const COPY_FEEDBACK_MS = 2000;

// #endregion

// #region Types

/**
 * Discriminator for the three non-ready surfaces. The "ready" case
 * unmounts the panel from App.tsx and is not represented here.
 */
type PanelVariant = "claude-missing" | "not-authenticated" | "installing-sdk";

/** Props for the panel — App.tsx supplies a fully-resolved status. */
interface FirstRunPanelProps
{
  status: ClaudeInstallStatus;
}

// #endregion

// #region Helpers

/**
 * Returns true when every install-detection field signals readiness.
 * Used by App.tsx to decide between the panel and the main layout.
 *
 * @param status - The latest snapshot from `check_claude_install_status`.
 * @returns `true` when Claude Code, auth, and the SDK are all present.
 */
export function isInstallReady(status: ClaudeInstallStatus): boolean
{
  return (status.claudeInstalled && status.claudeAuthenticated && status.sdkInstalled);
}

/**
 * Maps a non-ready `ClaudeInstallStatus` to its panel variant. Order
 * matters: missing Claude Code dominates (no point showing login when
 * the binary is absent); auth dominates SDK (login first, install
 * second).
 *
 * @param status - The latest snapshot from `check_claude_install_status`.
 * @returns The variant the panel should render.
 */
function variantFor(status: ClaudeInstallStatus): PanelVariant {
  if (!status.claudeInstalled)
  {
    return "claude-missing";
  }

  if (!status.claudeAuthenticated)
  {
    return "not-authenticated";
  }

  return "installing-sdk";
}

// #endregion

/**
 * Spinner-text screen shown while the very first
 * `checkClaudeInstallStatus()` call is in flight (~500ms typical).
 * Avoids the visual flash of jumping straight into a CTA card before
 * we know the actual state.
 */
export function FirstRunCheckingScreen()
{
  return (
    <div className="flex h-screen w-full items-center justify-center bg-slate-900">
      <div className="text-sm text-slate-400">Checking installation...</div>
    </div>
  );
}

/**
 * Default export — renders the appropriate non-ready surface for the
 * given install status.
 *
 * @param props - The current install-status snapshot.
 * @returns The variant card matching the status.
 */
export default function FirstRunPanel({ status }: FirstRunPanelProps)
{
  const variant = variantFor(status);

  if (variant === "claude-missing")
  {
    return <ClaudeMissingCard />;
  }

  if (variant === "not-authenticated")
  {
    return <NotAuthenticatedCard />;
  }

  return <InstallingSdkCard />;
}

// #region Variant cards

/**
 * State 3 — Claude Code is not on PATH.
 *
 * Renders the install-prompt card with a button that opens Anthropic's setup
 * docs in the system browser. Failures from the URL launcher are swallowed and
 * logged to the console so the card never crashes the wizard.
 *
 * @returns The install-prompt card element.
 */
function ClaudeMissingCard()
{
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
    <CardShell title="Install Claude Code">
      <p className="mb-6 text-sm text-slate-300">
        MCP Game Deck needs Claude Code installed locally to run. Follow the
        official setup instructions from Anthropic.
      </p>
      <button
        type="button"
        onClick={handleOpenDocs}
        className="rounded bg-blue-700 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
      >
        Open install docs
      </button>
      <p className="mt-4 text-xs text-slate-500">
        This screen detects the install automatically once it's complete.
      </p>
    </CardShell>
  );
}

/**
 * State 4 — Claude Code is installed but the user isn't logged in.
 *
 * Renders the login-prompt card with a copy-to-clipboard control for the
 * `claude login` command. Copy success flips a transient "Copied!" label for
 * `COPY_FEEDBACK_MS` before reverting. The wrapping wizard auto-advances once
 * authentication completes.
 *
 * @returns The login-prompt card element.
 */
function NotAuthenticatedCard()
{
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
    <CardShell title="Log in to Claude Code">
      <p className="mb-4 text-sm text-slate-300">
        Claude Code is installed. Open a terminal and run:
      </p>
      <div className="mb-4 flex items-center gap-2">
        <code className="flex-1 rounded bg-slate-950 px-3 py-2 font-mono text-sm text-slate-100">
          {LOGIN_COMMAND}
        </code>
        <button
          type="button"
          onClick={handleCopy}
          className="rounded bg-blue-700 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-600"
        >
          {copied ? "Copied!" : "Copy"}
        </button>
      </div>
      <p className="text-xs text-slate-500">
        This panel auto-advances once you're logged in.
      </p>
    </CardShell>
  );
}

/**
 * State 2 — SDK install in progress, or failed and awaiting retry.
 *
 * Owns its own state machine: dispatches `start_sdk_install` once on
 * mount, then renders the indeterminate progress strip with the latest
 * stdout line as `sdk-install-progress` events arrive. On
 * `sdk-install-failed`, flips to a failure card with the trailing
 * stderr and a Retry button. On `sdk-install-completed`, the App.tsx
 * polling loop detects `sdkInstalled` and unmounts the panel — no
 * local UI change needed.
 *
 * @returns The installing-or-failed card element.
 */
function InstallingSdkCard()
{
  const [phase, setPhase] = useState<"installing" | "failed">("installing");
  const [progressMessage, setProgressMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const startInstall = useCallback(async () =>
  {
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
    let unlistenProgress: (() => void) | null = null;
    let unlistenCompleted: (() => void) | null = null;
    let unlistenFailed: (() => void) | null = null;

    onSdkInstallProgress((payload) => {
      if (cancelled)
      {
        return;
      }

      if (payload.message)
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
      .catch((err) => {
        console.error("[first-run] onSdkInstallProgress subscribe failed:", err);
      });

    onSdkInstallCompleted(() => {
    })
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
      .catch((err) => {
        console.error("[first-run] onSdkInstallCompleted subscribe failed:", err);
      });

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
      .catch((err) => {
        console.error("[first-run] onSdkInstallFailed subscribe failed:", err);
      });

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
      <CardShell title="Install failed">
        <p className="mb-4 text-sm text-slate-300">
          The SDK install did not complete. Common causes: no network,
          npm not on PATH, or registry blocked.
        </p>
        {errorMessage && (
          <pre className="mb-4 max-h-40 overflow-y-auto whitespace-pre-wrap rounded bg-slate-950 p-3 text-xs text-red-300">
            {errorMessage}
          </pre>
        )}
        <button
          type="button"
          onClick={() => void startInstall()}
          className="rounded bg-blue-700 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-600"
        >
          Retry
        </button>
      </CardShell>
    );
  }

  return (
    <CardShell title="Setting up Claude Code SDK">
      <p className="mb-6 text-sm text-slate-300">
        {progressMessage ?? "Installing @anthropic-ai/claude-agent-sdk..."}
      </p>
      <div
        className="h-2 w-full overflow-hidden rounded bg-slate-700"
        role="progressbar"
      >
        <div className="h-full w-1/3 animate-pulse bg-blue-700" />
      </div>
      <p className="mt-4 text-xs text-slate-500">
        First launch only — usually 30 seconds to 2 minutes.
      </p>
    </CardShell>
  );
}

/**
 * Shared chrome for the three non-ready cards — full-screen slate-900 with a
 * centered card.
 *
 * Provides the consistent outer layout (background, centering, card border and
 * shadow) and a heading row, so each state component only has to render its
 * body content.
 *
 * @param props - Component props.
 * @param props.title - Heading rendered at the top of the card.
 * @param props.children - Body content rendered below the heading.
 * @returns The shared card shell element.
 */
function CardShell({title, children,}: {title: string; children: React.ReactNode;}) 
{
  return (
    <div className="flex h-screen w-full items-center justify-center bg-slate-900 p-8 text-slate-100">
      <div className="w-full max-w-md rounded-lg border border-slate-700 bg-slate-800 p-8 shadow-xl">
        <h1 className="mb-3 text-xl font-semibold">{title}</h1>
        {children}
      </div>
    </div>
  );
}

// #endregion