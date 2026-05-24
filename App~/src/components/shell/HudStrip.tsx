/**
 * Global HUD strip rendered above the nav rail and main content.
 *
 * Surfaces the four pieces of state that drive every decision the
 * user makes:
 *   1. Which Unity project is bound  (project switcher trigger)
 *   2. Unity Editor connection state (`StatusDot`)
 *   3. Claude supervisor lifecycle    (`StatusDot`)
 *   4. Active permission mode         (human-labeled, not the SDK jargon)
 * plus, right-aligned: current session label and Claude version.
 *
 * This component is presentational — it reads from the existing
 * `connectionStore`, `conversationStore`, and `settingsStore` and does
 * not own any state.
 *
 * @requires-backend B.11 (theme tokens layer — addressed by tokens.css)
 */

import { useMemo } from "react";
import type { ConnectionStatus, SupervisorStatus } from "../../ipc/types";
import { useConnectionStore } from "../../stores/connectionStore";
import { useConversationStore } from "../../stores/conversationStore";
import { useSettingsStore } from "../../stores/settingsStore";
import BrandHex from "../atoms/BrandHex";
import Pill from "../atoms/Pill";
import StatusDot, { type DotStatus } from "../atoms/StatusDot";
import ModelPicker from "../ModelPicker";
import PermissionModePicker from "../PermissionModePicker";
import ContextRing from "./ContextRing";

// #region Helpers

const connectionToDot = (status: ConnectionStatus): DotStatus => {
  switch (status)
  {
    case "connected":    return "ok";
    case "busy":         return "busy";
    case "disconnected": return "down";
  }
};

const supervisorToDot = (status: SupervisorStatus): DotStatus => {
  switch (status)
  {
    case "ready":    return "ok";
    case "starting": return "busy";
    case "crashed":  return "down";
    case "failed":   return "down";
    case "idle":     return "idle";
  }
};

const projectNameOf = (projectPath: string | null): string => {
  if (projectPath === null || projectPath.length === 0)
  {
    return "no project";
  }

  const normalized = projectPath.replace(/[\\/]+$/, "");
  const idx = Math.max(normalized.lastIndexOf("/"), normalized.lastIndexOf("\\"));

  if (idx === -1)
  {
    return normalized;
  }

  return normalized.slice(idx + 1);
};

// #endregion

// #region Component

/**
 * Renders the strip. Height is fixed at 36px so layout above the rail
 * stays predictable. The strip is `flex-shrink-0` so the chat scroller
 * below it never crowds it out.
 *
 * @returns The HUD strip element.
 */
export default function HudStrip()
{
  const unityStatus = useConnectionStore((s) => s.unityStatus);
  const supervisorStatus = useConnectionStore((s) => s.supervisorStatus);
  const currentSessionId = useConversationStore((s) => s.currentSessionId);
  const messageCount = useConversationStore((s) => s.messages.length);
  const projectPath = useSettingsStore((s) => s.settings.unityProjectPath);

  const projectName = useMemo(() => projectNameOf(projectPath), [projectPath]);

  const sessionLabel = useMemo(() => {
    if (currentSessionId === null)
    {
      return "Ready";
    }

    return `Session active · ${messageCount} msgs`;
  }, [currentSessionId, messageCount]);

  return (
    <div
      className="flex shrink-0 items-center gap-3.5 border-b border-line bg-bg-0 px-3.5 font-mono text-[11px] text-txt-3"
      style={{ height: 36 }}
    >
      {/* Project switcher trigger (left) */}
      <button
        type="button"
        className="inline-flex items-center gap-[7px] text-txt-1 font-medium rounded-r-1 px-1.5 py-1 hover:bg-bg-3 transition-colors duration-[120ms]"
        aria-label="Switch Unity project"
      >
        <BrandHex size={14} />
        <span>{projectName}</span>
        <span className="text-txt-4">▾</span>
      </button>

      <Sep />

      <StatusDot status={connectionToDot(unityStatus)}      label="UNITY" />
      <StatusDot status={supervisorToDot(supervisorStatus)} label="SUPERVISOR" />

      <Sep />

      <PermissionModePicker variant="hud" />

      <Sep />

      <ModelPicker variant="hud" />

      <div className="ml-auto flex items-center gap-3.5">
        <ContextRing />
        <Sep />
        <span>{sessionLabel}</span>
        <Sep />
        <Pill variant="subtle" size="sm">claude</Pill>
      </div>
    </div>
  );
}

// #endregion

/** Visual middle-dot separator used inside the strip. */
const Sep = () => <span className="text-txt-5" aria-hidden="true">·</span>;