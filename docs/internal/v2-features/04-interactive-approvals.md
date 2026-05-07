# Feature 04 — Interactive Approvals & Clarifying Questions

> **Note on history:** this feature absorbs the original Features 04 (Interactive Plan Mode) and 05 (Permission System Fix). Both were rewritten under ADR-001 — the custom Agent SDK Server they targeted no longer exists, and Claude Code's own SDK provides everything they were re-implementing plus a built-in `AskUserQuestion` tool. The remaining work is a single React surface that renders both kinds of user prompts (permission requests + clarifying questions) and routes the user's answer back through the SDK's `canUseTool` callback. F05's design doc is deleted; this doc replaces it.

## Status

`design locked` — all decisions resolved. Ready to generate `04-interactive-approvals-spec.md` (executable spec) and `04-interactive-approvals-tasks.md` (decomposed task list for Claude Code).

## Problem

After F02 shipped, two distinct user-input flows are required by Claude Code but neither has a visible UI in the Tauri app:

1. **Permission requests when `permissionMode === "default"`.** Claude wants to use a tool that isn't auto-approved (Edit, Write, Bash, MCP tools, etc). The SDK fires its `canUseTool` callback expecting an `allow` or `deny` decision. Today the callback isn't registered — `claude` falls back to its native CLI prompt that renders to the subprocess's stdin (which the user never sees), so the conversation stalls indefinitely. Default mode is **functionally broken** in Tauri today; users avoid it by switching to `bypassPermissions`, which is the wrong long-term answer.

2. **Clarifying questions via the `AskUserQuestion` built-in tool.** When Claude is mid-task and needs information only the user has — which framework variant to scaffold, which scene to target, how to handle a tradeoff — it calls the built-in `AskUserQuestion` tool with a structured question payload (1–N questions, each with options + free-text fallback). The SDK fires `canUseTool` with `toolName === "AskUserQuestion"` and waits for a response. Today nothing surfaces in the UI; Claude either fabricates an answer or stops mid-task.

Both flows go through the **same** `canUseTool` callback. Implementing them as separate features (as F04 + F05 originally proposed) means writing two parallel routings and two parallel UI surfaces with the same shape. They unify naturally.

## Proposal

Register a single `canUseTool` callback in `sdk-entry.js` that handles both cases by emitting a typed event to Tauri, which forwards to React, which renders an inline card in the chat with the appropriate action affordances. The user's selection round-trips back through the same channel and resolves the callback's promise.

Two flavors of the card share a common base:

- **Permission card** — for tool-call approvals. Shows tool name + parameters preview, exposes Allow / Allow Always / Deny buttons.
- **Question card** — for `AskUserQuestion` clarifying questions. Shows 1–N stacked questions, each with the response type Claude requested (single-select / multi-select / free-text). Multi-question cards are submitted together.

Both render markdown content using `react-markdown` for rich preview (bold, lists, inline code) without committing to custom HTML — Feature 09 (Design Handoff) refines styling later via the same Tailwind classes.

## Scope IN

- `canUseTool` callback wired in `sdk-entry.js` with two routing branches:
  - `toolName === "AskUserQuestion"` → emit `ask-user-requested` event with the structured payload
  - everything else → emit `permission-requested` event with `{ toolName, input, suggestions, blockedPath, decisionReason, toolUseId }`
- Tauri Rust forwards both event types through the existing `agent-message` channel as new typed variants
- React side:
  - `RequestCard` shared base component (header strip, markdown body via `react-markdown`, action footer)
  - `PermissionRequestCard` — Allow / Allow Always / Deny
  - `QuestionCard` — supports single-select / multi-select / free-text per question; 1–3 questions per card stacked vertically; single submit button at the bottom
  - Cards rendered inline in the chat as a new `Block` variant (alongside `text` / `tool-use` / `tool-result`); they live in the message stream and stay as historical context after answered
- User's answer round-trips: React → Tauri command (`respond_to_request`) → supervisor stdin → `sdk-entry.js` resolves the awaited promise → `query()` continues
- Three terminal states for a request: **answered** (normal flow), **dismissed** (user closed the window or ignored), **interrupted** (Tauri restart / supervisor crash mid-request)
- Auto-resolve to `deny` (permission) or `dismissed` (question) when the supervisor crashes mid-request — prevents zombie awaits in `sdk-entry.js`
- `previewFormat: "markdown"` set in `query()` options for `AskUserQuestion`
- `react-markdown` added as `App~/package.json` dependency
- The existing stub types in `App~/src/ipc/types.ts` (`AskUserRequestedPayload`, `PermissionRequestedPayload`) are replaced/expanded to match the actual shapes the SDK exchanges
- Smoke validation in two passes: main-thread `canUseTool` round-trip, then subagent `canUseTool` round-trip (subagents must inherit both behaviors as built-ins)

## Scope OUT (deferred)

- **Allow Always persistence across sessions** — F04 implements the in-session "Allow Always" decision (subsequent calls of the same tool with the same input shape auto-approved for the rest of the session). Cross-session persistence (write to `ProjectSettings/GameDeck/permissions.json`) is v2.1.
- **Per-tool / per-scope rule library** — "always allow Read in subdirectory X" or "never allow Bash" beyond a single session. v2.1+.
- **Branching plans based on Question answers** — agent already handles this naturally via its own logic; no special UI for "alternative path" suggestions in v2.0.
- **Voice input for free-text questions.** v2.x+.
- **Question card collapse after answered** — stays in the chat as a regular block; no auto-collapse toggle. F09 may revisit visually.
- **Custom HTML rendering** (`previewFormat: "html"`) — v2.x+ if a real use case appears. Markdown covers everything users need at v2.0 quality.
- **Hooks-based interception** (`PreToolUse` / `PostToolUse` for blanket allow/deny) — v2.1+. F04 only wires `canUseTool`.
- **Read-only "what would Claude ask?" preview without sending** — v2.x.

## Dependencies

- **Feature 02 (Claude Code Supervisor)** — done as of 2026-04-30. F04 builds on the existing `sdk-entry.js`, the `agent-message` event channel, the React `conversationStore` block-based message model, the `PermissionModeToggle` component, and the existing stub types in `ipc/types.ts`. Without F02, none of this plumbing exists.
- **Feature 07 (Editor Status Pin)** — done. F04 doesn't touch Unity-side or pin code.

This feature replaces Feature 05 (Permission System Fix) entirely. The 5 test cases F05 spec'd ("set auto → no prompt", "set ask → prompt", "set plan → no permission prompts during planning", "switch ask → auto mid-message", "/auto slash command") are all covered by Claude Code's native permission system (already exposed via F02 task 4.2 dropdown) plus this feature's `canUseTool` callback handling the `default`-mode prompt UI.

---

## Locked decision #1 — One `canUseTool` callback handles both flows

**Decided:** April 2026.

The SDK's design is explicit: *"Claude requests user input in two situations: when it needs permission to use a tool, and when it has clarifying questions (via the AskUserQuestion tool). **Both trigger your `canUseTool` callback.**"* (Anthropic Agent SDK docs, "Handle approvals and user input", 2026-04.)

Implementation:

```js
// sdk-entry.js (sketch)
async function canUseTool(toolName, input, opts) {
  if (toolName === "AskUserQuestion") {
    const answers = await surfaceQuestionToReact(input, opts.toolUseID);
    return { behavior: "allow", updatedInput: answers };
  }
  const decision = await surfacePermissionToReact(toolName, input, opts);
  return decision; // { behavior: "allow" | "deny", ... }
}
```

A single callback owning both branches keeps the wire protocol unified, lets the React side share UI primitives (RequestCard base + variant components), and avoids two parallel state machines in the supervisor's stdin/stdout dispatcher.

---

## Locked decision #2 — Use the SDK's built-in `AskUserQuestion` tool, not a custom one

**Decided:** April 2026.

The original F04 design (pre-ADR-001) proposed defining a custom `ask_user` tool via the SDK's `tool()` + `createSdkMcpServer()` helpers. That predates the addition of `AskUserQuestion` to the built-in tool catalog (confirmed in the official TypeScript SDK reference, April 2026: `AskUserQuestionInput` and `AskUserQuestionOutput` types exported from `@anthropic-ai/claude-agent-sdk`).

Using the built-in instead of a custom tool gives:

- **Zero tool-definition code.** No `tool()` calls, no Zod schema, no `createSdkMcpServer` registration.
- **Native integration with Claude's prompt.** Anthropic ships the system-prompt instructions that teach Claude when and how to use `AskUserQuestion`; we don't have to engineer prompts to convince a custom tool gets called.
- **Subagents inherit the tool automatically.** Built-ins flow into subagent contexts without listing them in agent frontmatter. (Custom MCP tools require explicit listing per F02 task 3.4 findings — wouldn't work for the 10 specialists in `Plugin~/agents/` without modifying their YAML.)
- **Schema is stable.** `AskUserQuestionInput` is a versioned Anthropic API shape; we get bug fixes and capability extensions for free.

The host responsibility is purely UI — surface the question payload to the user, return the answer in the shape `AskUserQuestionOutput` expects.

---

## Locked decision #3 — `previewFormat: "markdown"` rendered via `react-markdown`

**Decided:** April 2026.

`AskUserQuestion`'s SDK config exposes `ToolConfig.askUserQuestion.previewFormat: "markdown" | "html"` to control how Claude formats the question body. Markdown chosen for three reasons:

1. **Library cost is small.** `react-markdown` is ~5 KB gzipped, single dep, zero peer requirements.
2. **No XSS surface.** `react-markdown` parses to AST then renders React nodes — no `dangerouslySetInnerHTML`, no DOMPurify needed.
3. **Composes cleanly with Tailwind.** Components rendered by `react-markdown` accept `className` overrides via the `components` prop. F09 (Design Handoff) tunes typography/spacing globally without rewriting question/permission rendering.

The chat itself today renders text as `whitespace-pre-wrap font-mono` (no markdown). F04 introduces `react-markdown` only inside `RequestCard`'s body — chat text rendering is unchanged in this feature. F09 may decide to extend markdown rendering to assistant text later; not F04's concern.

---

## Locked decision #4 — `AskUserQuestion` is always available, not gated by plan mode

**Decided:** April 2026.

The original F04 design said the tool should be available *only* in `permissionMode === "plan"`. That assumption came from defining a custom `ask_user` tool — we'd have controlled when it's exposed. With the built-in `AskUserQuestion`, Claude Code itself decides when the tool fires, and the system prompt teaches it to use the tool when an answer is non-derivable, regardless of mode.

Gating `AskUserQuestion` behind plan mode would require the `canUseTool` callback to inspect the current mode and `behavior: "deny"` when not in plan — Claude would then either (a) hallucinate an answer or (b) keep retrying the tool call uselessly. Both outcomes are worse than letting the tool fire freely.

The mode-gating UX the original design wanted is already covered by `permissionMode === "plan"` itself: in plan mode Claude doesn't execute, only plans. If the plan needs information, `AskUserQuestion` fires; otherwise no tool calls happen.

Conclusion: leave `AskUserQuestion` always-available. The user-visible behavior degrades into "Claude asks slightly more often than strictly necessary" worst-case, which is a minor calibration issue, not a UX failure.

---

## Locked decision #5 — Subagents inherit both flows as built-ins

**Decided:** April 2026.

The 10 Unity specialists in `Plugin~/agents/` are invoked via the `Agent` tool (a.k.a. `Task`). Subagents spawned this way inherit the main thread's tool inventory for built-in tools (per the SDK docs and the bug-fix release notes referenced in F02 task 3.4 — Anthropic shipped *"Fixed MCP tools not available to subagents"* in April 2026). `AskUserQuestion` and the `canUseTool` callback both apply at the SDK level, not at the agent level — subagents get them transparently.

F04's empirical smoke test (Group 4) repeats F02 task 3.4's pattern but for `canUseTool` specifically: invoke a specialist, induce a tool call requiring approval (default mode), confirm the permission card surfaces; induce a clarifying question, confirm the question card surfaces.

If the smoke test fails (subagent calls don't trigger `canUseTool` in the parent SDK process), fallback options at task time:

- (a) Add `AskUserQuestion` explicitly to each specialist's frontmatter `tools:` array
- (b) Document the limitation and ship without subagent support; specialists fall back to "main thread asks the user, then delegates"

Either fallback is small. Don't block on this; plan for it as a known-risk task with two pre-thought outcomes.

---

## Locked decision #6 — Single `RequestCard` base with two variants

**Decided:** April 2026.

UI structure:

```
<RequestCard variant="permission" | "question">
  ├── Header strip — variant-specific label + icon
  ├── Body — markdown content (tool params preview OR question text)
  └── Footer — variant-specific actions (Allow/Always/Deny OR option buttons / submit)
```

`RequestCard.tsx` owns: card chrome (border, background, padding), markdown body via `react-markdown`, dismissed-state visual (greyed out after answered), and the timeout-to-dismiss handler.

`PermissionRequestCard.tsx` and `QuestionCard.tsx` compose `RequestCard` with their specific footer + body shape. The two variant files contain about 50 lines each (footer + payload-to-markdown formatting); the shared base is the bulk of the code.

Why this matters: F09 (Design Handoff) will restyle once and both variants pick up the changes. Branding pin / color / typography updates happen in `RequestCard.tsx` only.

---

## Locked decision #7 — Auto-resolve on supervisor crash / Tauri restart

**Decided:** April 2026.

Pending requests in `sdk-entry.js` are awaited promises. If the supervisor crashes (Group 6 of F02 emits `supervisor-status-changed: crashed`) while a request is pending, those promises stay forever-unresolved in the dying Node child — the child gets killed by F02 task 6.3's clean shutdown, which is fine, but the React side has a card waiting for an answer that will never come.

Resolution behavior:

- On `supervisor-status-changed: crashed` event, React iterates over any active `RequestCard`s and marks them as "interrupted" (visually greyed, disabled buttons, "Conversation interrupted" caption underneath). The card stays in the message stream as historical context; the user can't act on it anymore.
- On `supervisor-status-changed: ready` after a manual restart, no automatic re-emit of the original request. The user clicks the latest session in `SessionList`, the supervisor reconnects with `pendingResumeSessionId` primed, and the `resume: sessionId` option is **only applied on the next `query()` call** — which only happens when the user sends a new message. Sending any input after resume (e.g. "continue" or a repeat of the original prompt) triggers `query()` with resume context: Claude reads the full session JSONL (including the orphaned tool_use without a tool_result) and either re-fires `canUseTool` or chooses a different path based on the new input. Behavior is non-deterministic; orphaned tool calls without a follow-up message stay stranded indefinitely. **Empirically validated during F04 task 4.1 smoke** — the original design assumed reconnect alone replays pending tool calls, which doesn't match SDK reality.
- On Tauri window close mid-request (with no crash), shutdown sequence (F02 task 6.3) kills the supervisor before the user sees any state change. The promise simply doesn't resolve — `query()` is being torn down anyway.

For the in-flight Tauri-alive case where the user just ignores the card, no auto-timeout. The card stays pending until answered. Claude can be left waiting indefinitely; that matches CLI Claude Code's behavior where prompts in plan/default mode wait at the terminal prompt forever.

---

## Locked decision #8 — F04 absorbs F05 entirely; F05 doc deleted

**Decided:** April 2026.

The original Feature 05 (Permission System Fix) was a refactor of the custom Agent SDK Server's permission resolver. Under ADR-001 that server is gone, taking the bug with it. The remaining piece of F05's scope — *"surface Claude Code's mode in the Tauri React UI and validate end-to-end"* — splits cleanly:

- **Mode toggle UI** → already shipped in F02 task 4.2 (PermissionModeToggle dropdown, Shift+Tab cycling in F02 task 4.3).
- **Per-tool prompt UI in default mode** → exactly what this feature builds via `canUseTool` permission branch.

Nothing of value remains in `05-permission-system-fix.md` that isn't covered here. The file (and its `.meta`) are deleted as part of this feature's scope. The roadmap's F05 row is removed; the F04 row is renamed to match this doc.

---

## Cost estimate

**Small-to-medium.** Smaller than F02 (24 tasks) but bigger than the original F04 estimate because absorbing F05 adds the permission card variant and its semantics.

- `canUseTool` callback wiring in `sdk-entry.js` (Tauri ↔ Node JSON protocol extension): ~1 day
- Tauri Rust event marshalling (`agent-message` channel additions, `respond_to_request` command): ~0.5 day
- React `RequestCard` base + markdown body via `react-markdown`: ~1 day
- React `PermissionRequestCard` variant: ~0.5 day
- React `QuestionCard` variant (single/multi/free-text + multi-question support): ~1 day
- Block integration into `conversationStore` and `ChatRoute`: ~0.5 day
- Lifecycle handling (interrupted state, supervisor crash, in-session "Allow Always"): ~0.5 day
- Smoke tests (main-thread + subagent): ~0.5 day

Total: ~5–6 days focused work, ~10 tasks across 4 groups.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `AskUserQuestion` schema differs across SDK versions | low | Pin SDK version range in `App~/runtime/package.json` (already pinned per F02 task 6.5 work). Track Anthropic's changelog. |
| Subagent inheritance is incomplete despite the documented fix | medium | Group 4 smoke validates explicitly. Two fallback options pre-thought (decision #5). |
| `react-markdown` clashes with Tailwind defaults (e.g. list bullet style) | low | `components` prop overrides per element; sample `<RequestCard>` with markdown stress-test in task 2.1. |
| User confused by two distinct cards looking too similar | medium | Different header label + icon per variant; permission cards have a yellow accent stripe, question cards have a blue accent stripe. F09 may refine. |
| In-session "Allow Always" leaks across sessions accidentally | low | State lives in supervisor memory only, cleared on supervisor shutdown. Verified via task 4.x lifecycle smoke. |
| Multi-question cards become visually heavy on small windows | low | Limit to 3 questions per card (matches Anthropic's own guidance for `AskUserQuestion`). Vertical scroll inside card if it overflows. |
| Markdown body contains malicious content (path-traversal CTAs, fake buttons) | low | `react-markdown` with default rehypePlugins disables raw HTML; markdown can render text but cannot inject components. Click handlers only on our own rendered footer buttons, never on markdown content. |
| Supervisor crash mid-question leaves card in inconsistent state | medium | Decision #7's explicit "interrupted" state. Tested in Group 4. |

## Milestone

v2.0.

## Open questions (deferred to implementation)

1. **"Allow Always" granularity** — does it match on `(toolName, exact input)` or `(toolName, input shape)`? Probably exact input for simplicity in v2.0; loosen in v2.1 if users complain.
2. **Question card visual differentiation when 1 vs N questions** — single-question card may want compact layout, multi-question card needs section dividers. Decide at task 2.3 time when rendering looks real.
3. **Should `RequestCard` autofocus the primary action button?** Probably yes for permission (Deny is the safe default; user can confirm Allow with Enter), debatable for question (depends on whether free-text is in the mix). Decide during smoke validation.

## Notes

- Don't over-engineer the "Allow Always" cache — it's an in-memory `Set` keyed by `${toolName}:${stableHash(input)}`. Don't reach for IndexedDB.
- The `canUseTool` callback runs in the supervisor's Node process, not in Tauri's Rust or in React. The "wait for user response" mechanism is a `Promise` held in Node memory, with a `Map<requestId, resolver>` table. React's job is to send back `respond_to_request(requestId, decision)` via Tauri command; Tauri writes a JSON line on stdin; `sdk-entry.js` looks up the resolver and calls it.
- `permission-requested` is one of the few cases where the React → Node direction needs to carry structured payload (the user's decision, possibly with `updatedInput`). Existing F02 stdin protocol (newline-delimited JSON with `type` discriminator) handles this without protocol bumps.
