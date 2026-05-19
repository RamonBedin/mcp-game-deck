/**
 * Step row used by the plans execution view. Renders one step in the
 * "Live progress" right column.
 *
 * Visual states:
 *   - `done`    · solid OK dot + checkmark
 *   - `active`  · pulsing cyan dot, brand-violet inset accent on the row
 *   - `pending` · empty outline dot, faded text
 *   - `failed`  · solid BAD dot + ✗
 *
 * Tools associated with the step (when known) render as a row of mono
 * chips beneath the step label.
 *
 * @requires-backend B.05 plan-execution events for `status` updates.
 */

// #region Types

export type StepRowStatus = "done" | "active" | "pending" | "failed";

/**
 * Props for the `StepRow` component.
 *
 * Renders a single step row inside the plan-execution panel, showing the
 * step's number, title, current status, and an optional list of tools the
 * step is expected to use.
 */
interface StepRowProps
{
  num: string;
  title: string;
  status: StepRowStatus;
  tools?: string[];
}

// #endregion

const STATUS_STYLES: Record<StepRowStatus, { color: string; ringColor: string; icon: string; pulse: boolean }> = {
  done:    { color: "var(--ok)",   ringColor: "var(--ok)",   icon: "✓", pulse: false },
  active:  { color: "var(--cyan)", ringColor: "var(--cyan)", icon: "●", pulse: true  },
  pending: { color: "var(--txt-5)",ringColor: "var(--txt-5)",icon: "",  pulse: false },
  failed:  { color: "var(--bad)",  ringColor: "var(--bad)",  icon: "✗", pulse: false },
};

/**
 * Renders the step row.
 *
 * @param props - See {@link StepRowProps}.
 * @returns The row element.
 */
export default function StepRow({ num, title, status, tools }: StepRowProps)
{
  const s = STATUS_STYLES[status];
  const titleColor = status === "pending" ? "text-txt-4" : "text-txt-1";
  const bgWhenActive = status === "active" ? "bg-bg-3" : "";
  const insetWhenActive = status === "active" ? "shadow-[inset_2px_0_0_var(--cyan)]" : "";

  return (
    <div className={`flex gap-2.5 px-2.5 py-2 rounded-r-1 ${bgWhenActive} ${insetWhenActive}`}>
      <span
        aria-hidden="true"
        className="inline-flex items-center justify-center shrink-0 mt-px text-[9px] font-bold"
        style={{
          width: 16,
          height: 16,
          borderRadius: "50%",
          background: status === "pending" ? "transparent" : s.color,
          color:      status === "pending" ? "var(--txt-5)" : "var(--bg-0)",
          border:     status === "pending" ? "1.5px solid var(--txt-5)" : `1.5px solid ${s.color}`,
          animation:  s.pulse ? "pulse-soft 1.2s ease-in-out infinite" : "none",
        }}
      >
        {s.icon}
      </span>

      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          <span className="font-mono text-[10px] text-txt-4">{num}</span>
          <span className={`text-[12.5px] ${titleColor}`}>{title}</span>
        </div>
        {tools !== undefined && tools.length > 0 && status !== "pending" && (
          <div className="mt-1.5 flex flex-wrap gap-1">
            {tools.map((t) => (
              <span
                key={t}
                className="font-mono text-[9.5px] px-1.5 py-px rounded-full border border-line bg-bg-2"
                style={{ color: status === "active" ? "var(--cyan)" : "var(--txt-3)" }}
              >
                {t}
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}