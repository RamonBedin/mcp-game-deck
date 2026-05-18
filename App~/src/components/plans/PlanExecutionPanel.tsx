/**
 * Plans execution panel — third column shown in `PlansRoute` while a
 * plan is mid-execution.
 *
 * Subscribes to plan-execution events (B.05) and renders each step
 * with `StepRow`. The header shows the running plan's progress + an
 * action group (Pause / Open in chat). When no plan is running, the
 * panel is omitted entirely (consumer-side conditional render).
 *
 * Shape of `PlanStep` is the on-wire representation the supervisor
 * emits via the `plan-step-started` / `plan-step-completed` events.
 * Keep this file authoritative: the supervisor side mirrors this
 * shape.
 *
 * @requires-backend B.05 plan execution events.
 */

import StepRow, { type StepRowStatus } from "./StepRow";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";

// #region Types

/** One step inside an executing plan. */
export interface PlanStep
{
  num: string;
  title: string;
  status: StepRowStatus;
  tools?: string[];
}

/**
 * Props for the `PlanExecutionPanel` component.
 *
 * Renders the live execution view for a plan in progress, surfacing the
 * plan's name, ordered steps with their current status, elapsed time, and
 * controls to pause execution or jump into the chat view.
 */
interface PlanExecutionPanelProps
{
  planName: string;
  steps: PlanStep[];
  elapsed: string;
  onPause: () => void;
  onOpenInChat: () => void;
}

// #endregion

/**
 * Renders the panel. Counts `done` and `total` from the step list so
 * the consumer doesn't need to keep a separate progress tally in sync.
 *
 * @param props - See {@link PlanExecutionPanelProps}.
 * @returns The panel element.
 */
export default function PlanExecutionPanel({planName, steps, elapsed, onPause, onOpenInChat,}: PlanExecutionPanelProps)
{
  const done = steps.filter((s) => s.status === "done").length;
  const total = steps.length;
  const pct = total === 0 ? 0 : Math.round((done / total) * 100);

  return (
    <aside
      className="shrink-0 flex flex-col bg-bg-0 border-l border-line"
      style={{ width: 260 }}
    >
      {/* Header */}
      <div className="px-3.5 pt-3.5 pb-2.5 border-b border-line-soft flex items-center gap-2">
        <Pill variant="cyan" size="sm" dot>RUNNING</Pill>
        <span className="font-mono text-[10px] text-txt-3">{done}/{total}</span>
      </div>

      {/* Progress bar */}
      <div className="px-3.5 py-2.5">
        <div className="h-1 rounded-full bg-bg-3 overflow-hidden">
          <div
            className="h-full bg-grad-brand transition-all duration-[360ms]"
            style={{ width: `${pct}%` }}
          />
        </div>
        <div className="mt-2 flex items-baseline justify-between text-[10.5px] font-mono">
          <span className="text-txt-3">{planName}</span>
          <span className="text-txt-4">{elapsed}</span>
        </div>
      </div>

      {/* Step list */}
      <div className="flex-1 overflow-y-auto px-1.5 pb-2.5">
        {steps.map((s) => (
          <StepRow
            key={s.num}
            num={s.num}
            title={s.title}
            status={s.status}
            tools={s.tools}
          />
        ))}
      </div>

      {/* Footer actions */}
      <div className="px-3.5 py-2.5 border-t border-line-soft flex gap-2">
        <Button variant="ghost" size="sm" onClick={onOpenInChat}>
          Open in chat ↗
        </Button>
        <Button variant="default" size="sm" onClick={onPause} className="ml-auto">
          Pause
        </Button>
      </div>
    </aside>
  );
}