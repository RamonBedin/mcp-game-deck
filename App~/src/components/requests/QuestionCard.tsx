/**
 * surface.
 *
 * Major changes from v1:
 *   - **Options-as-cards**, not native radio/checkbox. The user clicks
 *     a card-shaped row; selection state highlights with a brand
 *     border + filled radio dot. Multi-select still cycles selection;
 *     single-select replaces.
 *   - **Header counts progress** ("0/2 answered") so the user knows
 *     the card has more than one question.
 *   - **Free-text input** appears below its triggering option, with
 *     an active focus ring tied to the brand violet (not generic
 *     border-slate-500).
 *   - **Submit hint** in the footer surfaces ⌘⏎ for keyboard submit.
 *
 * Wire payload (`AskUserRequestedPayload` / `AskUserQuestionOutput`)
 * is unchanged.
 */

import { useMemo, useState } from "react";
import type { AskUserQuestionOutput, AskUserRequestedPayload } from "../../ipc/types";
import Avatar from "../atoms/Avatar";
import Button from "../atoms/Button";
import Pill from "../atoms/Pill";
import RequestCard, { type RequestCardState } from "./RequestCard";

// #region Types


/**
 * Props for the `QuestionCard` component.
 *
 * Renders an agent-issued question for the user to answer, exposes the card's
 * lifecycle state for styling, and reports the submitted answer back to the
 * parent via `onSubmit`.
 */
interface QuestionCardProps
{
  payload: AskUserRequestedPayload;
  state: RequestCardState;
  onSubmit: (answer: AskUserQuestionOutput) => void;
  previousAnswer?: AskUserQuestionOutput;
}

// #endregion

// #region Helpers

const isFreeTextOption = (opt: { label: string; description?: string }): boolean => {
  if (/^other\b/i.test(opt.label))
  {
    return true;
  }

  return opt.description?.includes("free text") ?? false;
};

// #endregion

/**
 * Renders the question card. Holds per-question selection + free-text
 * state locally; expected to be keyed by `requestId` from the parent
 * so a fresh card produces a fresh instance.
 *
 * @param props - See {@link QuestionCardProps}.
 * @returns The card element.
 */
export default function QuestionCard({payload, state, onSubmit, previousAnswer,}: QuestionCardProps)
{
  const isPending = state === "pending";
  const questions = payload.input.questions;

  const [selectedOptions, setSelectedOptions] = useState<string[][]>(() => questions.map(() => []));
  const [freeText, setFreeText] = useState<string[]>(() => questions.map(() => ""));

  const showAnsweredView = state === "answered" && previousAnswer !== undefined;

  // #region Handlers

  const toggleOption = (qIdx: number, optionLabel: string, multiSelect: boolean) => {
    setSelectedOptions((prev) => {
      const next = prev.map((arr) => [...arr]);

      if (multiSelect)
      {
        next[qIdx] = next[qIdx].includes(optionLabel) ? next[qIdx].filter((l) => l !== optionLabel) : [...next[qIdx], optionLabel];
      }
      else
      {
        next[qIdx] = [optionLabel];
      }

      return next;
    });
  };

  const setFreeTextAt = (qIdx: number, value: string) => {
    setFreeText((prev) => {
      const next = [...prev];
      next[qIdx] = value;
      return next;
    });
  };

  const isFreeTextActive = (qIdx: number): boolean => {
    const opt = questions[qIdx].options.find(isFreeTextOption);
    return opt !== undefined && (selectedOptions[qIdx] ?? []).includes(opt.label);
  };

  const answeredCount = useMemo(() => {
    let count = 0;

    for (let i = 0; i < questions.length; i += 1)
    {
      const sel = selectedOptions[i] ?? [];
      const free = freeText[i]?.trim() ?? "";
      const hasSelection = sel.length > 0;
      const hasFree = isFreeTextActive(i) && free.length > 0;

      if (hasSelection || hasFree)
      {
        count += 1;
      }
    }

    return count;

  }, [questions, selectedOptions, freeText]);

  const allAnswered = answeredCount === questions.length;

  const handleSubmit = () => {
    const answers: Record<string, string> = {};

    questions.forEach((q, idx) => {
      const free = freeText[idx]?.trim() ?? "";
      const selected = selectedOptions[idx] ?? [];

      if (isFreeTextActive(idx) && free.length > 0)
      {
        answers[q.question] = free;
      }
      else if (selected.length > 0)
      {
        answers[q.question] = selected.join(", ");
      }
      else
      {
        answers[q.question] = "";
      }
    });

    onSubmit({ questions, answers });
  };

  // #endregion

  // #region Render parts

  const label = (
    <>
      <Avatar variant="claude" initials="CC" size={24} />
      <div className="flex flex-col min-w-0">
        <span className="text-[13.5px] text-txt-1 font-medium leading-tight">
          Claude has {questions.length} question{questions.length > 1 ? "s" : ""}
        </span>
        <span className="font-mono text-[10.5px] text-txt-3 mt-0.5">
          {answeredCount}/{questions.length} answered
        </span>
      </div>
    </>
  );

  const headerRight = payload.agentId !== null ? <Pill variant="brand" size="sm">via {payload.agentId}</Pill> : undefined;

  const body = (
    <div className="flex flex-col gap-5">
      {questions.map((q, idx) => {
        const selectedForQ = selectedOptions[idx] ?? [];
        const freeTextForQ = freeText[idx] ?? "";
        const freeOpt = q.options.find(isFreeTextOption);
        const showFreeText = freeOpt !== undefined && selectedForQ.includes(freeOpt.label);
        const previousAnswerStr = showAnsweredView ? previousAnswer.answers[q.question] ?? "" : null;

        return (
          <div key={idx}>
            <div className="flex items-baseline gap-2.5 mb-2">
              <span className="font-hud text-[11px] tracking-[0.18em] uppercase text-brand-violet-soft">
                Q{idx + 1}{q.multiSelect && " · multi-select"}
              </span>
              {q.header !== undefined && (
                <span className="text-[12.5px] text-txt-3">{q.header}</span>
              )}
            </div>
            <div className="text-[14.5px] text-txt-1 font-medium leading-snug mb-3">
              {q.question}
            </div>

            {showAnsweredView ? (
              <div className="text-[13px] text-txt-2 italic">
                {previousAnswerStr ?? "(no answer)"}
              </div>
            ) : (
              <div className="flex flex-col gap-1.5">
                {q.options.map((opt) => {
                  const isSelected = selectedForQ.includes(opt.label);
                  const isFree = isFreeTextOption(opt);

                  return (
                    <OptionRow
                      key={opt.label}
                      label={opt.label}
                      description={opt.description}
                      multi={q.multiSelect}
                      selected={isSelected}
                      isFree={isFree}
                      disabled={!isPending}
                      onSelect={() => toggleOption(idx, opt.label, q.multiSelect)}
                    />
                  );
                })}
                {showFreeText && (
                  <input
                    type="text"
                    value={freeTextForQ}
                    onChange={(e) => setFreeTextAt(idx, e.target.value)}
                    placeholder="Type your custom answer…"
                    disabled={!isPending}
                    autoFocus
                    className="mt-1 rounded-r-2 bg-bg-0 px-3 py-2 text-[13px] text-txt-1 font-body border border-brand-violet focus-ring outline-none disabled:opacity-50"
                  />
                )}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );

  const footer = (
    <Button
      variant="primary"
      size="sm"
      disabled={!allAnswered || !isPending}
      onClick={handleSubmit}
      icon={<span style={{ fontSize: 12 }}>↗</span>}
    >
      {state === "answered" ? "Answered" : "Submit"}
    </Button>
  );

  const footerHint = isPending
    ? (
      <>
        <Pill variant="subtle" size="sm">⌘⏎</Pill>
        <span>submit</span>
      </>
    )
    : undefined;

  // #endregion

  return (
    <RequestCard
      variant="question"
      accent="violet"
      label={label}
      headerRight={headerRight}
      body={body}
      footer={footer}
      footerHint={footerHint}
      state={state}
    />
  );
}

// #region OptionRow

/**
 * Props for the `OptionRow` component.
 *
 * Renders a single selectable option inside a question card's answer picker,
 * supporting both single-select and multi-select modes, an optional free-text
 * marker, and a disabled state for resolved or locked questions.
 */
interface OptionRowProps
{
  label: string;
  description?: string;
  multi: boolean;
  selected: boolean;
  isFree: boolean;
  disabled: boolean;
  onSelect: () => void;
}

const OptionRow = ({label, description, multi, selected, isFree, disabled, onSelect,}: OptionRowProps) => (
  <button
    type="button"
    role={multi ? "checkbox" : "radio"}
    aria-checked={selected}
    onClick={onSelect}
    disabled={disabled}
    className={[
      "flex items-start gap-2.5 text-left rounded-r-2 px-3 py-2.5 transition-all duration-[120ms]",
      selected
        ? "bg-brand-violet/10 border border-brand-violet"
        : "bg-bg-1 border border-line hover:bg-bg-3/40",
      disabled ? "opacity-60 cursor-not-allowed" : "cursor-pointer",
    ].join(" ")}
  >
    <span
      aria-hidden="true"
      className="inline-flex items-center justify-center shrink-0 mt-0.5"
      style={{
        width: 16,
        height: 16,
        borderRadius: multi ? "var(--r-1)" : "50%",
        background: selected ? "var(--violet)" : "transparent",
        border: selected ? "1px solid var(--violet)" : "1.5px solid var(--line-hard)",
      }}
    >
      {selected && multi && <span className="text-white text-[9px] font-bold">✓</span>}
      {selected && !multi && <span className="block rounded-full bg-white" style={{ width: 5, height: 5 }} />}
    </span>
    <div className="flex-1 min-w-0">
      <div className="flex items-center gap-2 text-[13.5px] leading-snug" style={{ color: selected ? "var(--txt-1)" : "var(--txt-2)", fontWeight: selected ? 500 : 400 }}>
        {label}
        {isFree && <Pill variant="subtle" size="sm">free text</Pill>}
      </div>
      {description !== undefined && description.length > 0 && (
        <div className="text-[12px] text-txt-3 mt-1 leading-snug">{description}</div>
      )}
    </div>
  </button>
);

// #endregion