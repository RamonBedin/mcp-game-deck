# Feature 12 — Plan Execution Events

## Status

`proposed` — design pending Ramon approval. Companion specs (`12-plan-execution-spec.md` + `12-plan-execution-tasks.md`) will follow when execution starts.

## Problem

The `/plan-execute` skill exists in `Plugin~/skills/plan-execute/SKILL.md` and instructs Claude to read a plan from `ProjectSettings/GameDeck/plans/<name>.md` and execute it step-by-step. F06 designed a `PlanExecutionPanel` (third column) that should show live progress: which step is active, which are done, an animated progress bar. The component is fully built — and orphan in the codebase. No data flows into it because the skill has no way to signal step lifecycle to the front.

Worse, the cycle 2 attempt to wire this up surfaced three intertwined bugs:

1. **Skill body leaked as assistant message** — when the user typed `/plan-execute test-sweep`, the chat showed the user's bubble as `mcp-game-deck:plan-execute <command-name>/...` with raw routing tags, and the assistant bubble was the literal `SKILL.md` content (text dumped, never executed).
2. **Marker tool required user approval** — even after the skill eventually started to execute, each `plan_step_marker` call popped a permission card, which makes the feature unusable.
3. **Tool name display rendered with broken formatting** — `mcp__plan-events__plan_step_marker` showed as `Working with mcp__plan: events_plan_step_marker` (hyphen treated as separator).

(#3 is solved in F10 — formatter rewrite. This feature covers #1 and #2.)

## Proposal

Two parts, both server-side:

**(a) Skill routing fix.** When the user types `/plan-execute <name>`, the slash command interceptor must forward the matched skill content as a **system prompt addendum** (so the model treats it as instructions to execute), not as user content (which causes the content-echo behavior). The routing-internal `<command-name>` / `<command-args>` tags must be stripped from the user message bubble before render. Investigate `App~/src/routes/ChatRoute.tsx` slash command handling and the supervisor's skill registration path (`Server~/src/sdk_entry.js` and possibly a new `Server~/src/plan-events-server.ts`).

**(b) In-process MCP server for step markers.** Add a new in-process MCP server `plan-events` via `createSdkMcpServer({name: "plan-events"})`. It exposes one tool: `plan_step_marker(step_id, status, title?)`. Status is `"started"` or `"completed"`. Handler is fire-and-forget: emits a stdio event (`plan-step-started` or `plan-step-completed`) and returns `{ok: true}` immediately — no Unity round-trip, no I/O. Microseconds.

The skill body is updated to instruct Claude to call `plan_step_marker(N, "started", "<short label>")` before each step and `plan_step_marker(N, "completed")` after. Rust forwards the events; a new `planExecutionStore` (Zustand) tracks `{activeStepId, completedStepIds, stepTitles}`; `PlansRoute` reads the store and wires the orphan `PlanExecutionPanel`.

**Critical:** the marker tool must be auto-allowed in the permission layer. Whitelist the entire `plan-events` MCP server in the supervisor's `canUseTool` callback — it's in-process, owned by the app itself, no user-facing permission decision applies.

## Scope IN

- **Skill routing fix:**
  - Investigate slash command flow in `ChatRoute.tsx` and `conversationStore.sendMessage`
  - Forward skill content as system prompt addendum (not user content)
  - Strip routing tags from user message bubble before render
- **In-process MCP server `plan-events`:**
  - New file `Server~/src/plan-events-server.ts` (or inline in `sdk_entry.js`)
  - One tool: `plan_step_marker(step_id: string, status: "started" | "completed", title?: string)`
  - Handler emits structured stdio events, returns immediately
- **Skill body update:** `Plugin~/skills/plan-execute/SKILL.md` instructs Claude to call markers before/after each step.
- **Permission whitelist:** `canUseTool` callback auto-allows any tool from the `plan-events` server.
- **Rust events:** `EVT_PLAN_STEP_STARTED`, `EVT_PLAN_STEP_COMPLETED` in `events.rs`.
- **TS types:** `PlanStepStartedEvent {step_id, title?, ts}`, `PlanStepCompletedEvent {step_id, ts}`.
- **Store:** `planExecutionStore` (Zustand) tracks active/completed step IDs and titles.
- **PlansRoute wiring:** mount subscribers, pass state to `PlanExecutionPanel` + `StepRow`.

## Scope OUT (deferred to v2.1+)

- **Persistent plan run history** — only the current in-progress run is visible; no log of past plan executions.
- **Pause / resume / cancel of a running plan mid-execution** — once started, runs to completion or chat-level cancel.
- **Plan templates with `<placeholder>` substitution** — F24 plans polish work; this feature only handles plain plans.
- **Last-run timestamp tracking on plans** — F24 follow-up; needs `/plan-execute` skill to Write back mid-conversation.
- **Multi-plan parallel execution** — one plan at a time; second `/plan-execute` while one is running rejects or queues (TBD in spec, likely rejects).

## Dependencies

None hard, but better experience when shipped after:
- **F10** — for tool name display (KI-011 fix; otherwise marker tool calls render as `mcp__plan: events_plan_step_marker` in the chat).
- **F11** — for activity stream (so WorkingStrip shows turn progression while the plan runs).

Recommended order: F10 → F11 → F12.

## Risks

- **Skill routing investigation is open-ended** — we don't yet know whether the cycle 2 leak was caused by the supervisor's skill registration, the front's slash interceptor, or the SKILL.md format itself. Spec phase must include a discovery pass with reading the relevant code before writing implementation tasks.
- **`createSdkMcpServer` API stability** — relatively recent addition to the Claude Agent SDK. Verify against the actual `.d.ts` types (lesson from past: Options interface field names are sometimes silently ignored).
- **Permission whitelist semantics** — if the `canUseTool` callback is per-tool-name and not per-server-prefix, whitelisting "the whole `plan-events` server" may need a glob-match. Investigate the SDK API for this.
- **Step ID semantics** — if a plan author uses non-numeric step labels in markdown (e.g., bullet points), the model may improvise IDs that don't correlate. Mitigation: skill body teaches Claude to use stable slugs from the step text if numbering is ambiguous.

## Open questions

1. **Should `plan_step_marker` accept a `status: "failed"` variant?**
   - Recommendation: not in v2.0. Failure is signaled by the *absence* of a `"completed"` marker (skill body already says "if a step fails, do not emit completion"). Adding a third status complicates the state machine for marginal benefit.
2. **What if the plan has 50+ steps — does the PlanExecutionPanel scroll?**
   - Recommendation: scroll. Already covered in F06's `PlanExecutionPanel` design; just verify it works with real step counts.
3. **Should we throttle marker emission rate?**
   - Recommendation: no. Markers are emitted at human-readable steps (seconds apart), not at sub-step granularity. Rate is bounded by Claude's reasoning speed.

## Related cycle 2 attempt notes

The cycle 2 attempt tried this and surfaced two failures documented above (skill leak, marker approval). The fix patches are clear in this design. The third issue (display formatter, "mcp__plan: events_plan_step_marker") is handled in F10 — this feature can assume catalog `humanLabel` is correct by the time it ships.

The cycle 2 attempt also implemented the `plan-events` server and step marker tool (worked technically — the leak was upstream, not in the server). The `cycle-2-attempt-1` branch code for `plan-events-server.ts` and the skill update is reusable as reference.
