> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.
> **Status post-ADR:** `needs revision (medium) — defer to execution time.` Plan mode itself is native to Claude Code (Shift+Tab cycle, `permissionMode: "plan"`). The original contribution of this feature — the `ask_user` tool with three response types — is implemented as an in-process tool via the Agent SDK's `@tool` decorator (or TS equivalent) when Tauri configures the SDK, instead of being added to a custom server. The React question card UI is unchanged work. **Read the ADR before executing this feature.**

# Feature 04 — Interactive Plan Mode

## Status

`agreed` — pattern observed in Claude Code (`ask_user_input_v0`), needs adaptation.

## Problem

Plan mode is supposed to let the agent think out a plan before executing. In practice, the agent often realizes mid-plan that it needs information only the user has — what variant of a system to use, which scene to target, how the user wants to handle a tradeoff. With the current chat, the agent has no way to ask. It has to either:

- Fabricate an answer (hallucination, wrong assumptions)
- Stop with "I need more info" and force the user to start over
- Ask in the chat reply, breaking out of plan mode

None of these are the right behavior. A plan that requires an unstated assumption is worse than a plan that asks first.

## Proposal

Give the agent in plan mode an `ask_user` tool that:

1. Pauses the plan generation
2. Posts a question to the chat with one of three response types:
   - **Single select** — agent provides 2-4 options, user clicks one
   - **Multi-select** — agent provides options, user picks any number
   - **Free text** — user types a custom answer
3. Resumes plan generation when user answers

This is the same pattern as Claude Code's `ask_user_input_v0` tool. The tool is **only available in plan mode** — auto mode and ask mode don't have it (they have their own flows).

## Scope IN

- `ask_user` tool exposed only when permission mode = `plan`
- Three response types: single-select, multi-select, free-text
- React UI:
  - Question card in chat with the question and options
  - Selection buttons / checkboxes / text field
  - Submitted answer becomes part of the conversation history
- Plan resumes automatically after answer is submitted
- Multi-question support (agent asks 1-3 questions in one card, user answers them together)

## Scope OUT (deferred)

- Branching plans based on answers (the agent just continues with the answer; no automatic alternative-path generation)
- Persisting answers across sessions for similar questions
- Voice input

## Dependencies

- **Feature 01 (External app)** — UI implementation
- **Feature 05 (Permission system fix)** — plan mode has to actually be in effect for the tool to be available
- **Feature 02 (Orchestrator agent)** — main agent decides whether plan mode delegates ask_user to subagents (probably yes — subagents need this too)

## Cost estimate

**Small.**

- `ask_user` tool definition in Agent SDK Server: ~2 days
- React UI for question cards (3 response types): ~3-4 days
- Wiring tool result back into conversation: ~2 days

Total: ~1.5 weeks.

Pattern is well-defined (Claude Code does it cleanly), so most of the work is straightforward.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Agent overuses `ask_user` and asks too many questions | medium | Strong system prompt guidance: ask only when answer is non-derivable. Test in real workflows. |
| User dismisses the question without answering | low | Treat as "skip" — agent gets a "user dismissed" result, can either continue with a default or abandon plan |
| Plan with many questions feels interrogatory | medium | Limit to 3 questions per card. Encourage agent to bundle related questions. |
| UI for free-text questions confuses users (looks like new chat input) | low | Visual distinction: card background, "Reply to question" framing |

## Milestone

v2.0.

## Open questions

1. **Should the agent see all unanswered questions, or only the most recent?** If user is mid-answer to question 1 and agent realizes question 2 is needed, does it queue or wait?
2. **Skip / cancel behavior** — if user dismisses, does the plan just complete with assumptions, or fail with "needed input"? Probably configurable per question by the agent ("required" vs "optional").
3. **Question card persistence** — after answered, does it stay in the chat as historical context (yes, probably) or collapse?

## Notes

- This is a small but high-impact UX feature. It makes plan mode actually usable for non-trivial plans.
- Pattern is already in Claude Code's `ask_user_input_v0`. Read its system prompt for calibration on when to use it.
