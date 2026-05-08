# Feature 04 — Interactive Approvals & Clarifying Questions — Spec

> **Status:** `agreed` — design decisions locked April 2026.
> **Companion:** `04-interactive-approvals-tasks.md` (decomposed work breakdown for Claude Code execution).
> **Parent design doc:** `04-interactive-approvals.md` (the 8 locked decisions and rationale).
> **Architectural parent:** `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.

## What this is

A `canUseTool` callback registered in `sdk-entry.js` that intercepts both kinds of user-input requests Claude Code emits — **permission requests** for tool calls in `default` mode, and **clarifying questions** via the built-in `AskUserQuestion` tool — and surfaces them as inline cards in the React chat. The user's response round-trips back through the supervisor's stdin and resolves the awaited promise so `query()` continues.

When this feature ships:

1. User sets `permissionMode` to `default` (via dropdown or Shift+Tab) and types a prompt that triggers a tool call. A **permission card** appears inline in the chat showing tool name + parameters preview + Allow / Allow Always / Deny actions. User clicks; the conversation continues.
2. Mid-task, Claude needs information only the user has and calls the built-in `AskUserQuestion` tool with 1–3 structured questions. A **question card** appears inline in the chat with the questions stacked, each rendered with the response type Claude requested (single-select / multi-select / free-text). User answers all questions and clicks Submit; the conversation continues.
3. Both cards stay in the message stream as historical context after answered — same lifetime as `tool-use` and `tool-result` blocks.
4. Subagents (the 10 specialists in `Plugin~/agents/`) inherit both flows automatically — same canvas surfaces requests originating from any execution context.
5. Supervisor crash mid-request marks pending cards as "interrupted" with greyed-out visual; restart-and-resume re-fires the original tool call and a fresh card appears.

The pre-existing functional gap where `default` mode in the Tauri app stalled the conversation forever (because no `canUseTool` was registered, and the SDK's CLI fallback prompt rendered to an invisible subprocess stdin) is **closed** when this feature ships.

## Architecture overview

```
┌─────────────────────────┐                                       ┌──────────────────────────┐
│   TAURI APP (Rust)      │                                       │   sdk-entry.js (Node)    │
│                         │                                       │                          │
│  ┌──────────────────┐   │  stdin: respond_to_request payload    │  ┌──────────────────┐   │
│  │ commands/         │   │ ─────────────────────────────────►   │  │ canUseTool        │   │
│  │  conversation.rs  │   │  stdout: agent-message events        │  │  callback         │   │
│  │   ::respond_to_   │   │ ◄──────────────────────────────────  │  │   (Decision #1)   │   │
│  │   request         │   │    ask-user-requested                │  │                  │   │
│  └────────┬─────────┘   │    permission-requested               │  │  pending: Map<   │   │
│           │             │                                       │  │   requestId,     │   │
│           │ Tauri event │                                       │  │   resolver>      │   │
│           │ "agent-     │                                       │  └──────────────────┘   │
│           │  message"   │                                       │                          │
│           ▼             │                                       └──────────────────────────┘
│  ┌──────────────────┐   │
│  │ React store +    │   │
│  │ ChatRoute        │   │
│  │                  │   │
│  │ RequestCard ─┐   │   │
│  │   ├─ Perm    │   │   │
│  │   └─ Quest   │   │   │
│  └──────────────────┘   │
└─────────────────────────┘
```

The C# MCP Server, the F07 pin, and Plugin~/ surfacing are **not touched**. The Agent SDK's `query()` already accepts a `canUseTool` option — we register a function there and route by `toolName`. The wire protocol Tauri ↔ Node (newline-delimited JSON over stdin/stdout, established in F02) is extended additively with three new message kinds; nothing existing changes shape.

## Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| Tool surface | SDK built-in `AskUserQuestion` (Decision #2) | No custom `tool()` registration. `AskUserQuestionInput` schema imported as type. |
| Permission gate | SDK `canUseTool` option | Single async function, dispatches by `toolName`. Awaited promise gates `query()`. |
| Markdown rendering | `react-markdown` | ~5 KB gzipped; no XSS surface (no raw HTML). Decision #3. |
| Wire format additions | 2 new outgoing event variants on `agent-message`; 1 new stdin command type | Additive; existing `agent-message` consumers unaffected. |
| In-session "Allow Always" cache | `Map<string, true>` keyed by `${toolName}:${stableHash(input)}` in `sdk-entry.js` | In-memory, supervisor-lifetime. Cross-session persistence is v2.1 (Decision #8 of design doc, scope OUT). |
| Card components | React functional components in `App~/src/components/requests/` | New subfolder. Tailwind classes consistent with the rest of the app. |
| Lifecycle | Existing supervisor lifecycle events (`supervisor-status-changed`) | F02 task 6.2 already emits crashed; F04 React subscribes to mark cards interrupted. |

## File layout

**New files:**

```
App~/src-tauri/src/
└── commands/
    └── requests.rs                            ← Tauri command respond_to_request(requestId, decision)
                                                 forwards JSON to supervisor stdin

App~/src/components/requests/
├── RequestCard.tsx                            ← shared base — chrome, markdown body, status states
├── PermissionRequestCard.tsx                  ← Allow / Allow Always / Deny variant
├── QuestionCard.tsx                           ← AskUserQuestion variant — handles 1-N questions
│                                                 with single/multi/free-text response types
└── markdown-renderers.tsx                     ← react-markdown component prop overrides
                                                 (Tailwind classes per element type)

App~/src/ipc/
└── (new exports in commands.ts + events.ts; no new files)
```

**Modified files:**

```
App~/src-tauri/src/claude_supervisor/
└── sdk_entry.js                               ← register canUseTool callback in query() options;
                                                 add pending requests Map; add stdin handler for
                                                 respond_to_request type; emit ask-user-requested
                                                 + permission-requested + request-resolved

App~/src-tauri/src/
├── types.rs                                   ← AgentMessage enum: + AskUserRequested,
│                                                 PermissionRequested, RequestResolved variants;
│                                                 + decision payload structs
├── events.rs                                  ← (no new event channel; still routes via
│                                                 EVT_AGENT_MESSAGE — additive enum payload)
├── commands/mod.rs                            ← register respond_to_request handler
├── commands/conversation.rs                   ← (unchanged — send_message stays the same)
└── lib.rs                                     ← invoke_handler! gets the new command

App~/src/
├── ipc/types.ts                               ← AskUserRequestedPayload + PermissionRequestedPayload
│                                                 stubs replaced with full SDK-shape types;
│                                                 AgentMessage union extended with new variants;
│                                                 DecisionPayload type added; Block union extended
│                                                 with "request" variant
├── ipc/commands.ts                            ← respondToRequest(requestId, decision)
├── ipc/events.ts                              ← (no new wrappers — EVT_AGENT_MESSAGE covers all)
├── stores/conversationStore.ts                ← appendRequestBlock(turnId, request);
│                                                 markRequestAnswered(requestId, answer);
│                                                 markRequestInterrupted(requestId);
│                                                 in-session AllowAlways cache (mirror of supervisor
│                                                 cache, for optimistic UI only)
└── routes/ChatRoute.tsx                       ← BlockView extended with "request" branch routing
                                                 to RequestCard variants; supervisor-status-changed
                                                 listener calls markRequestInterrupted on all pending
                                                 cards when status flips to crashed

App~/package.json                              ← + react-markdown dependency
App~/runtime/package.json                      ← (unchanged — sdk-entry.js doesn't need
                                                 react-markdown; rendering is React-side)
```

**Files deleted:**

```
docs/internal/v2-features/05-permission-system-fix.md         ← superseded by this feature
docs/internal/v2-features/05-permission-system-fix.md.meta    ← Unity meta sibling
docs/internal/v2-features/04-interactive-plan-mode.md         ← replaced by 04-interactive-approvals.md
docs/internal/v2-features/04-interactive-plan-mode.md.meta    ← Unity meta sibling
```

**No changes to:**

- `Editor/` (any C# code) — Unity side untouched.
- `Plugin~/` — agent / skill / knowledge files unchanged.
- `Server~/dist/mcp-proxy.js` — proxy unaffected.
- C# MCP Server — unaffected.
- F02-shipped components: `FirstRunPanel`, `PermissionModeToggle`, `SessionList`, `ToolUseBlock`, `ToolResultBlock`, `ClaudeVersionWarningBanner`, `UpdateBanner`.

## Locked decisions (from parent design doc)

The parent doc (`04-interactive-approvals.md`) enumerates **8 decisions**. Summary here for executable context:

1. **One `canUseTool` callback handles both flows** — single async function in `sdk-entry.js`, dispatches by `toolName`.
2. **Built-in `AskUserQuestion`, not custom tool** — uses Anthropic's shipped tool; no `createSdkMcpServer` registration.
3. **`previewFormat: "markdown"` via `react-markdown`** — small library, no XSS, composes with Tailwind.
4. **Always-available, no plan-mode gating** — Claude decides when to use; mode-gate would cause hallucination/retries.
5. **Subagents inherit built-ins automatically** — verified empirically in Group 4 smoke; fallback options pre-thought.
6. **Single `RequestCard` base + variants** — shared chrome, F09 restyles once.
7. **Auto-resolve on supervisor crash** — pending cards marked "interrupted"; session resume re-fires.
8. **F04 absorbs F05 entirely** — F05 doc deleted; roadmap row removed.

Read the parent doc for rationale per decision.

## `AskUserQuestion` contract

Imported types from `@anthropic-ai/claude-agent-sdk` (TypeScript SDK reference, April 2026):

```typescript
type AskUserQuestionInput = {
  questions: Array<{
    header?: string;          // optional short heading shown above the question
    question: string;         // markdown-formatted prompt body
    multiSelect: boolean;     // true → user can pick more than one option
    options: Array<{
      label: string;          // short clickable label
      description?: string;   // markdown-formatted body shown next to/under label
    }>;
  }>;
};

type AskUserQuestionOutput = {
  answers: Array<{
    selectedOptions: string[];  // empty array means free-text was used
    freeTextResponse?: string;  // present when user typed instead of selecting
  }>;
};
```

The shape `AskUserQuestionInput.questions[].options[]` doesn't carry an explicit "free-text allowed" flag — Claude embeds free-text in `description` of an option labeled e.g. "Other (specify)". Our React renderer detects this convention and shows a text input when an option's label matches `/other/i` or when `description` contains a `_(free text)_` marker (final convention is decided at implementation time per the SDK's actual behavior in real prompts).

When the user submits, our `canUseTool` returns:

```typescript
{
  behavior: "allow",
  updatedInput: {
    answers: [
      { selectedOptions: ["JQuery", "React"] },                        // multi-select
      { selectedOptions: ["Use TypeScript"] },                         // single-select
      { selectedOptions: [], freeTextResponse: "Use Vite for build" }  // free-text
    ]
  } satisfies AskUserQuestionOutput
}
```

`updatedInput` is the standard SDK pattern for `canUseTool` to provide tool inputs. For `AskUserQuestion`, the "input" the SDK sees is effectively the user's answer — the SDK then routes the result to Claude as the tool's output.

## Permission request contract

`canUseTool` signature (from the official TypeScript SDK reference, April 2026):

```typescript
type CanUseTool = (
  toolName: string,
  input: Record<string, unknown>,
  options: {
    signal: AbortSignal;
    suggestions?: PermissionUpdate[];   // optional pre-formed allow/deny suggestions
    blockedPath?: string;               // when relevant: path that would be touched
    decisionReason?: string;            // SDK's reason hint
    toolUseID: string;                  // unique per tool call
    agentID?: string;                   // present when called from a subagent context
  }
) => Promise<PermissionResult>;

type PermissionResult =
  | { behavior: "allow"; updatedInput: Record<string, unknown>; updatedPermissions?: PermissionUpdate[]; toolUseID?: string }
  | { behavior: "deny"; message: string; interrupt?: boolean; toolUseID?: string };
```

We use `toolUseID` as the `requestId` for the React side — guaranteed unique per call, lets us correlate when the user responds. `agentID` is captured for the card header (so the user sees "subagent: unity-shader-specialist" vs "main thread"). `blockedPath` and `decisionReason` go into the markdown body of the card when present.

Our callback returns:

- **Allow** → `{ behavior: "allow", updatedInput: input }` (`updatedInput` is required by the SDK's Zod schema even when we don't modify; passing the original `input` unchanged signals "allow as-is". A bare `{ behavior: "allow" }` raises a ZodError that the SDK surfaces as a `Tool permission request failed` rejection — discovered during F04 task 1.1 validation.)
- **Allow Always** → `{ behavior: "allow", updatedInput: input }` plus inserts `${toolName}:${stableHash(input)}` into the in-session cache; subsequent `canUseTool` calls matching the same key short-circuit with `{ behavior: "allow", updatedInput: input }` without surfacing a card
- **Deny** → `{ behavior: "deny", message: "User denied via Tauri UI", interrupt: false }`

`interrupt: false` lets Claude continue and try a different approach. `interrupt: true` would abort the whole turn; we don't expose that to the user in v2.0 (could be a v2.1 "Deny & Stop" button).

## Wire protocol — additions only

All three additions are JSON lines, consistent with the F02-established protocol. `type` discriminator is kebab-case.

### Outgoing — Node → Tauri (via stdout, parsed by spawn.rs::read_stdout, re-emitted as agent-message)

**`ask-user-requested`** — fired when `canUseTool` receives `toolName === "AskUserQuestion"`:

```json
{
  "type": "ask-user-requested",
  "requestId": "<toolUseID>",
  "turnId": "<current-turn-id>",
  "agentId": "<from canUseTool opts>",
  "input": {
    "questions": [
      { "header": "...", "question": "...", "multiSelect": false, "options": [...] }
    ]
  }
}
```

**`permission-requested`** — fired for any other tool name in default mode:

```json
{
  "type": "permission-requested",
  "requestId": "<toolUseID>",
  "turnId": "<current-turn-id>",
  "agentId": "<from canUseTool opts>",
  "toolName": "<e.g. mcp__game-deck__scene-create>",
  "input": { /* whatever the tool accepts */ },
  "blockedPath": "<optional>",
  "decisionReason": "<optional>"
}
```

**`request-resolved`** — fired immediately after `canUseTool` resolves (either via user response or auto-resolve via Allow Always cache hit). Lets React update card visual to "answered" state:

```json
{
  "type": "request-resolved",
  "requestId": "<toolUseID>",
  "outcome": "allow" | "allow-always" | "deny" | "auto-allowed",
  "answer": { /* the AskUserQuestionOutput, when applicable */ }
}
```

`outcome: "auto-allowed"` fires when the cache short-circuits without UI; React still adds a synthetic `request` block to the message stream for context, marked as auto-allowed.

### Incoming — Tauri → Node (via stdin, written by claude_supervisor::send_input or new respond method)

**`respond_to_request`** — user clicked an action in the card:

```json
{
  "type": "respond-to-request",
  "requestId": "<toolUseID>",
  "decision": {
    "kind": "permission",
    "outcome": "allow" | "allow-always" | "deny"
  }
}
```

```json
{
  "type": "respond-to-request",
  "requestId": "<toolUseID>",
  "decision": {
    "kind": "question",
    "answer": {
      "answers": [
        { "selectedOptions": ["..."], "freeTextResponse": "..." }
      ]
    }
  }
}
```

`sdk-entry.js` looks up `pending.get(requestId)`, resolves the promise with the appropriate `PermissionResult`, removes from `pending`. Unknown `requestId` is logged-and-ignored (defensive against stale Tauri-side state after a supervisor restart).

## React component design

### `RequestCard` base

Visual structure:

```tsx
<div className="rounded border-l-4 border-{accent} bg-slate-800/60 p-3 my-2">
  <div className="flex items-center gap-2 mb-2">
    <Icon variant={variant} />
    <span className="text-xs uppercase tracking-wider text-slate-400">
      {label}  {/* "Permission required" | "Question" */}
    </span>
    {agentId && (
      <span className="ml-auto text-xs text-slate-500">
        from {agentId}
      </span>
    )}
  </div>
  <div className="prose prose-sm prose-invert">
    <ReactMarkdown components={markdownRenderers}>
      {bodyMarkdown}
    </ReactMarkdown>
  </div>
  <div className="mt-3 flex flex-wrap gap-2 justify-end">
    {children /* footer actions */}
  </div>
  {state === "interrupted" && (
    <div className="text-xs text-slate-500 mt-2">
      Conversation interrupted — answer no longer applicable.
    </div>
  )}
</div>
```

State enum: `"pending" | "answered" | "interrupted" | "auto-allowed"`. Visual greys out when not pending; action buttons disable.

### `PermissionRequestCard`

Body markdown formatting (composed by `requests/format-permission.ts`):

```markdown
**`{toolName}`** wants to proceed with these inputs:

```json
{...input...}
```

{optional decisionReason as plain paragraph}
{optional blockedPath as `Blocks path: <path>`}
```

Footer actions:

- `Allow` (primary, blue)
- `Allow Always` (secondary, slate)
- `Deny` (destructive, red outline)

Accent color: yellow (`border-yellow-500`).

### `QuestionCard`

Multi-question vertical stack. Each question rendered as:

```tsx
<div className="my-3 first:mt-0 last:mb-0">
  {q.header && <div className="text-sm font-semibold mb-1">{q.header}</div>}
  <ReactMarkdown components={markdownRenderers}>{q.question}</ReactMarkdown>
  <div className="mt-2 grid grid-cols-1 gap-1.5">
    {q.options.map(opt => (
      <Option ... />  /* checkbox if multiSelect, radio otherwise */
    ))}
    {hasFreeText && (
      <input type="text" placeholder="Or type a custom answer..." />
    )}
  </div>
</div>
```

Footer: single `Submit` button at the bottom of the card; disabled until every question has an answer (selectedOptions.length > 0 OR freeTextResponse non-empty).

Accent color: blue (`border-blue-500`).

### `markdown-renderers.tsx`

Per-element overrides for `react-markdown`:

```typescript
export const markdownRenderers: Components = {
  p: ({ children }) => <p className="text-sm text-slate-200 mb-2 last:mb-0">{children}</p>,
  strong: ({ children }) => <strong className="font-semibold text-slate-100">{children}</strong>,
  code: ({ inline, children }) => inline
    ? <code className="rounded bg-slate-900 px-1 py-0.5 text-xs font-mono text-emerald-300">{children}</code>
    : <pre className="rounded bg-slate-900 p-2 my-2 overflow-x-auto"><code className="text-xs font-mono text-slate-200">{children}</code></pre>,
  ul: ({ children }) => <ul className="list-disc list-inside text-sm text-slate-200 mb-2">{children}</ul>,
  ol: ({ children }) => <ol className="list-decimal list-inside text-sm text-slate-200 mb-2">{children}</ol>,
  li: ({ children }) => <li className="my-0.5">{children}</li>,
  a: ({ children, href }) => <a href={href} className="text-sky-400 hover:text-sky-300 underline">{children}</a>,
  // ...
};
```

Lives in a single file; F09 restyles by editing this file.

## Lifecycle handling

**Card states** (in `conversationStore`):

- `pending` — request is open; user hasn't acted; supervisor is waiting on `canUseTool` promise
- `answered` — user clicked; React optimistically transitions; final confirmation arrives via `request-resolved` event (which idempotently confirms); card stays in stream as historical context
- `interrupted` — supervisor crashed (`supervisor-status-changed: crashed` while card was pending); React listens to that event and walks `messages` flipping every pending request block; visual greys out
- `auto-allowed` — Allow Always cache hit; card synthesized into stream from `request-resolved` event with `outcome: "auto-allowed"`; renders compactly (no actions, just "Auto-allowed: <toolName>")

**Crash flow:**

1. User clicks something during a pending card → `respondToRequest()` fired but supervisor crashes before processing
2. `supervisor-status-changed: crashed` event arrives
3. `ChatRoute` listener calls `conversationStore.markAllPendingRequestsInterrupted()`
4. All pending request blocks transition to `interrupted`; visual updates
5. User clicks Restart Supervisor (existing F02 task 6.2 button) → fresh spawn → no automatic re-emit; if the original tool call was mid-flight, Claude Code's session storage retains it; user resumes via SessionList → fresh `canUseTool` round; new card

**Restart-without-crash flow** (Tauri close + reopen):

1. User closes Tauri (F02 task 6.3 clean shutdown kills supervisor)
2. Reopens Tauri via F07 pin
3. SessionList shows the previous session; user clicks → `resume_session` → supervisor spawns with `resume: sessionId`
4. Claude Code replays the pending tool call → `canUseTool` fires → fresh card

**In-session "Allow Always" cache:**

- Lives in `sdk-entry.js` only (single source of truth)
- `Map<string, true>` keyed by `${toolName}:${stableHash(JSON.stringify(input))}`
- Cleared when supervisor exits (Map dies with the process)
- React store maintains an optimistic mirror for visual hint ("you allowed this once already") — cleared on `supervisor-status-changed: idle | starting | crashed | failed` to stay in sync

## Subagent inheritance

Per Decision #5: `canUseTool` operates at the SDK process level — Claude Code's main thread plus any subagent it spawns route their permission/clarifying-question requests through the same callback. The callback receives `agentID` in its options when the call originates from a subagent (e.g. `agentID: "mcp-game-deck:unity-shader-specialist"`); we display this in the card header so the user knows which agent asked.

Group 4 of the tasks doc validates this empirically:

- Smoke 1: main-thread permission card — `default` mode + prompt that triggers `Read` on a file → permission card surfaces
- Smoke 2: main-thread question card — prompt that's deliberately ambiguous → Claude calls `AskUserQuestion` → question card surfaces
- Smoke 3: subagent permission card — main thread invokes `@agent-mcp-game-deck:unity-shader-specialist` with `default` mode active → subagent triggers tool that needs approval → permission card surfaces with `agentId` populated
- Smoke 4: subagent question card — same agent, deliberately ambiguous question → AskUserQuestion fires from inside the subagent → question card surfaces

If smoke 3 or 4 fails, fallback per Decision #5: add `AskUserQuestion` to specialist frontmatter `tools:` array (small change in `Plugin~/agents/*.md`), then re-test.

## Definition of done

- User toggles `permissionMode` to `default`, sends a prompt that triggers a tool call → permission card appears inline; clicking Allow continues the conversation; clicking Deny aborts the tool call cleanly
- "Allow Always" works within the session: clicking it once on a tool means subsequent calls of the same tool with the same inputs auto-approve without surfacing a card (an `auto-allowed` synthetic block appears instead)
- A prompt that's deliberately ambiguous causes Claude to call `AskUserQuestion`; question card appears with the questions Claude generated; user answers; conversation continues
- Multi-question cards render up to 3 questions stacked; submit is disabled until all answered; Submit fires once with all answers bundled
- Single-select / multi-select / free-text response types all render correctly per question
- Subagent invocations route their permission/question requests through the same UI; cards header includes `from <agent-id>`
- Supervisor crash mid-card flips visible cards to `interrupted` state within 2s; Restart Supervisor + session resume produces a fresh card for the same tool call
- React renders markdown content in card bodies (bold, lists, inline code, code blocks) via `react-markdown`; click handlers attached only to footer actions, never to markdown content
- Existing F02 functionality unaffected: chat streaming, tool-use/tool-result blocks, session list, permission mode dropdown, Shift+Tab cycling, attachments — all work as before
- F05 doc + meta deleted; F04 doc renamed to `04-interactive-approvals.md`; roadmap.md F05 row removed and F04 row updated; `docs/internal/README.md` v2-features list updated
- Smoke test on Windows 11: open app via F07 pin, run a 5-turn conversation that touches default-mode tool calls (3+ permission cards), one ambiguous prompt (1 question card with 2 questions), close cleanly. Repeat 3 times. No regressions, no orphan processes, no zombie pending cards.

## Out of scope reminders

- The C# MCP Server (`Editor/MCP/`) is **NOT** modified.
- Cross-session "Allow Always" persistence is v2.1.
- Hooks-based `PreToolUse` / `PostToolUse` blanket allow/deny is v2.1+.
- HTML rendering (`previewFormat: "html"`) is deferred — markdown handles all current needs.
- Final card visual styling is F09's territory; F04 ships with serviceable Tailwind and the right structure for F09 to override.
- F04 doesn't fix the F02 task 4.2 "the Tauri auto mode toggle is in-memory only, lost on supervisor exit" issue — that's separate state, separate fix, v2.x.

## Open questions deferred to implementation

These are not blockers; decide at task time:

1. **Free-text detection convention in `AskUserQuestionInput.options`** — Claude may emit free-text fallback as an option labeled "Other" with a description hinting at typing, or via some other convention. Decide at task 2.3 time after observing real prompts; document the heuristic in `format-question.ts`.
2. **Optimistic vs strict request-resolved confirmation** — when the user clicks Allow, do we transition the card to `answered` immediately (optimistic) or wait for the `request-resolved` event echo? Optimistic feels snappier; strict avoids visual lag if supervisor is slow to ack. Lean optimistic; document the choice when implementing.
3. **Maximum input preview size in permission cards** — a tool input of 50 KB JSON shouldn't render in full in the card body. Decide on truncation strategy (first ~500 chars + "..." with expand toggle?) at task 2.2 time.
4. **Card focus management after submit** — should the chat scroll past the answered card automatically, or stay visible for a beat? Default: scroll continues with chat as before; the answered card stays in flow.

These notes are recorded so they don't surprise during execution. Don't re-litigate.
