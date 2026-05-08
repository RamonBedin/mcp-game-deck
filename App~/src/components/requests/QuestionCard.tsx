import { useState } from "react";
import ReactMarkdown from "react-markdown";

import type {
  AskUserQuestionOutput,
  AskUserRequestedPayload,
} from "../../ipc/types";

import { markdownRenderers } from "./markdown-renderers";
import { RequestCard, type RequestCardState } from "./RequestCard";

/**
 * Heuristic: detect whether an option in `AskUserQuestionInput` is a
 * free-text fallback. Two signals:
 *
 * 1. `option.label` matches `/^other\b/i` — convention from the SDK's
 *    example prompts (e.g. "Other (specify)").
 * 2. `option.description` contains the literal substring "free text"
 *    — explicit hint from Claude's prompt formatting.
 *
 * Either signal flags the option as a free-text fallback. When the
 * user selects that option, the question card surfaces a text input
 * next to the radio/checkbox group; the typed value goes into
 * `AskUserQuestionOutput.answers[i].freeTextResponse`. The convention
 * is approximate — Anthropic's `AskUserQuestion` schema doesn't expose
 * an explicit "is free text" flag; refine here if real prompts surface.
 *
 * @param opt - One option from a question's `options` array.
 * @returns `true` when the option should trigger the free-text input.
 */
function isFreeTextOption(opt: { label: string; description?: string; }): boolean
{
  if (/^other\b/i.test(opt.label))
  {
    return true;
  }

  return opt.description?.includes("free text") ?? false;
}

/**
 * Props for the `QuestionCard` component.
 *
 * Renders an agent-issued question for the user to answer, exposes the card's
 * lifecycle state for styling, and reports the submitted answer back to the
 * parent via `onSubmit`.
 */
export interface QuestionCardProps
{
  payload: AskUserRequestedPayload;
  state: RequestCardState;
  onSubmit: (answer: AskUserQuestionOutput) => void;
  previousAnswer?: AskUserQuestionOutput;
}

/**
 * Question card variant — surfaces an `ask-user-requested` event
 * (task 1.2) carrying one or more `AskUserQuestionInput` questions
 * as an inline card. Renders each question stacked vertically with
 * the response type Claude requested (single-select via radio,
 * multi-select via checkbox, or free-text fallback via text input
 * detected by {@link isFreeTextOption}). Composes `RequestCard`
 * for the chrome.
 *
 * Local state holds per-question selections and free-text values.
 * Component identity is expected to be keyed by `requestId` from the
 * parent `BlockView` so a new payload produces a fresh
 * instance — no `useEffect` reset.
 *
 * @param props - See {@link QuestionCardProps}.
 * @returns The rendered question card.
 */
export function QuestionCard(props: QuestionCardProps)
{
  const { payload, state, onSubmit, previousAnswer } = props;
  const isPending = state === "pending";
  const questions = payload.input.questions;

  const [selectedOptions, setSelectedOptions] = useState<string[][]>(() => questions.map(() => []),);
  const [freeText, setFreeText] = useState<string[]>(() => questions.map(() => ""),);
  const showAnsweredView = state === "answered" && previousAnswer !== undefined;

  const toggleOption = (qIdx: number, label: string, multiSelect: boolean) =>
  {
    setSelectedOptions((prev) =>
    {
      const next = prev.map((arr) => [...arr]);

      if (multiSelect)
      {
        next[qIdx] = next[qIdx].includes(label) ? next[qIdx].filter((l) => l !== label) : [...next[qIdx], label];
      }
      else
      {
        next[qIdx] = [label];
      }

      return next;
    });
  };

  const setFreeTextAt = (qIdx: number, value: string) =>
  {
    setFreeText((prev) =>
    {
      const next = [...prev];
      next[qIdx] = value;
      return next;
    });
  };

  const isFreeTextActive = (qIdx: number, selected: string[][] = selectedOptions,): boolean =>
  {
    const opt = questions[qIdx].options.find(isFreeTextOption);
    return opt !== undefined && (selected[qIdx] ?? []).includes(opt.label);
  };

  const allAnswered = questions.every((_, idx) =>
  {
    const selLen = selectedOptions[idx]?.length ?? 0;
    const freeLen = freeText[idx]?.trim().length ?? 0;
    return selLen > 0 || (isFreeTextActive(idx) && freeLen > 0);
  });

  const handleSubmit = () =>
  {
    const answers: Record<string, string> = {};

    questions.forEach((q, idx) =>
    {
      const free = freeText[idx]?.trim();
      const selected = selectedOptions[idx] ?? [];

      if (isFreeTextActive(idx) && free !== undefined && free.length > 0)
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

    const answer: AskUserQuestionOutput = { questions, answers };
    onSubmit(answer);
  };

  const body = (
    <div>
      {questions.map((q, idx) =>
      {
        const selectedForQ = selectedOptions[idx] ?? [];
        const freeTextForQ = freeText[idx] ?? "";
        const freeTextOpt = q.options.find(isFreeTextOption);
        const showFreeText = freeTextOpt !== undefined && selectedForQ.includes(freeTextOpt.label);

        const previousAnswerStr = showAnsweredView ? previousAnswer.answers[q.question] ?? "" : null;

        return (
          <div
            key={idx}
            className="my-3 first:mt-0 last:mb-0 pb-3 border-b border-slate-700 last:border-b-0"
          >
            {q.header && (
              <div className="text-sm font-semibold mb-1 text-slate-200">
                {q.header}
              </div>
            )}
            <ReactMarkdown components={markdownRenderers}>
              {q.question}
            </ReactMarkdown>
            {showAnsweredView ? (
              <div className="mt-2 text-sm text-slate-300 italic">
                {previousAnswerStr ?? "(no answer)"}
              </div>
            ) : (
              <div className="mt-2 grid grid-cols-1 gap-1.5">
                {q.options.map((opt) =>
                {
                  const isSelected = selectedForQ.includes(opt.label);
                  return (
                    <label
                      key={opt.label}
                      className="flex items-start gap-2 rounded p-1.5 hover:bg-slate-800/40 cursor-pointer"
                    >
                      <input
                        type={q.multiSelect ? "checkbox" : "radio"}
                        name={`question-${idx}`}
                        checked={isSelected}
                        onChange={() =>
                          toggleOption(idx, opt.label, q.multiSelect)
                        }
                        disabled={!isPending}
                        className="mt-1"
                      />
                      <div className="flex-1 min-w-0">
                        <div className="text-sm text-slate-200">{opt.label}</div>
                        {opt.description && (
                          <ReactMarkdown components={markdownRenderers}>
                            {opt.description}
                          </ReactMarkdown>
                        )}
                      </div>
                    </label>
                  );
                })}
                {showFreeText && (
                  <input
                    type="text"
                    value={freeTextForQ}
                    onChange={(e) => setFreeTextAt(idx, e.target.value)}
                    placeholder="Type your custom answer..."
                    disabled={!isPending}
                    className="rounded border border-slate-700 bg-slate-800 px-2 py-1 text-sm text-slate-100 disabled:opacity-50"
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
    <button
      onClick={handleSubmit}
      disabled={!allAnswered || !isPending}
      className="rounded bg-sky-700 px-4 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:opacity-50 disabled:cursor-not-allowed"
    >
      {state === "answered" ? "Answered" : "Submit"}
    </button>
  );

  return (
    <RequestCard
      variant="question"
      label="Clarifying questions"
      body={body}
      agentId={payload.agentId}
      state={state}
      footer={footer}
    />
  );
}