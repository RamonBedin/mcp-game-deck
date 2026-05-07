# Feature 04 — Interactive Approvals & Clarifying Questions — Tasks

> **Companion to:** `04-interactive-approvals-spec.md`. Read that first.
> **Parent design doc:** `04-interactive-approvals.md` (the 8 locked decisions).
> **Execution model:** one task per Claude Code session. Ramon validates per checks below, commits via VS Code, returns to chat for next task.

## How to read this doc

- **S** = Small (~30 min – 1 h focused work). **M** = Medium (1–3 h). **L** = Large (3+ h, consider splitting if it grows).
- **Status column** updated by Ramon as tasks complete: ✅ done / 🔄 in progress / ⏳ pending.
- **Refs** point at the spec section that motivates the task.

---

## Status table

| # | Task | Size | Status | Date | Notes |
|---|------|------|--------|------|-------|
| 1.1 | `canUseTool` callback skeleton in sdk-entry.js — pending Map + dispatch | S | ✅ | 2026-05-06 | Schema fix: `updatedInput` required on `behavior:"allow"` |
| 1.2 | Emit branches — ask-user-requested + permission-requested + request-resolved | S | ✅ | 2026-05-06 | Used existing `emit()` helper (matches F02 envelope `{"message":{...}}`) |
| 1.3 | `respond-to-request` stdin handler — resolve awaited promises | S | ✅ | 2026-05-06 | + deadlock fix: `handleInput` serialized via `inputQueue` Promise chain (was blocking `for await` loop, preventing `respond-to-request` from being read while `canUseTool` awaited resolution) |
| 1.4 | In-session "Allow Always" cache — keyed by toolName + stable input hash | S | ✅ | 2026-05-06 | + wire extension: `RequestResolved` variant gains `toolName`/`turnId` (Rust + TS pulled forward from 2.3) for auto-allowed synthetic blocks. **Group 1 closed (4/4).** |
| 2.1 | Rust types — AgentMessage variants + DecisionPayload + Block "request" variant | S | ✅ | 2026-05-06 | + match arm extension in `spawn.rs::read_stdout` (compile fix) |
| 2.2 | Rust command — `respond_to_request(requestId, decision)` writes to stdin | S | ✅ | 2026-05-06 | + `withGlobalTauri: true` enabled in `tauri.conf.json` for DevTools probing; `write_stdin_line` extracted as shared helper on `ClaudeSupervisor` |
| 2.3 | TS bindings — replace stub types, add respondToRequest, extend Block union | S | ✅ | 2026-05-06 | F01 stubs (`AskUserType`, old payloads) replaced; events.ts F01 dead code left for post-F04 housekeeping. **Group 2 closed (3/3).** |
| 3.1 | react-markdown dep + markdown-renderers.tsx with Tailwind component overrides | S | ✅ | 2026-05-06 | v9 API adaptation: code renderer split into `pre` + `code` overrides (v9 removed the `inline` prop). Inline detection via `language-*` className regex + newline heuristic. Edge case (fenced single-line without language hint renders as inline pill) accepted — doesn't occur with Claude's actual output. |
| 3.2 | RequestCard base — chrome, markdown body, state visuals (pending/answered/interrupted/auto-allowed) | M | ✅ | 2026-05-06 | Wrapper plain `<div>` (prose classes removed — redundant without @tailwindcss/typography); auto-allowed early-return before chrome (defensive duplicate of BlockView 3.5 inline render); emoji icons with aria-hidden until F09 swaps to lucide |
| 3.3 | PermissionRequestCard variant — Allow / Allow Always / Deny actions | S | ✅ | 2026-05-06 | + `outcome?: PermissionOutcome` prop added (spec was internally inconsistent on this); `OUTCOME_LABEL` Record for caption text; tool name wrapped in inline code in markdown body to protect underscores from emphasis interpretation |
| 3.4 | QuestionCard variant — 1-N stacked questions with single/multi/free-text response types | M | ✅ | 2026-05-06 | A2 architectural change: RequestCard's `bodyMarkdown: string` generalized to `body: ReactNode` (in-flight refactor of 3.2 + 3.3) so QuestionCard can pass interactive JSX. Free-text precedence bug in spec literal fixed via `isFreeTextActive` gate. State reset relies on `key={requestId}` in 3.5 (forward dependency). |
| 3.5 | Block integration — conversationStore "request" branch + ChatRoute BlockView routing | S | ⏳ | | |
| 4.1 | Lifecycle — supervisor crash → mark all pending requests interrupted | S | ⏳ | | |
| 4.2 | Smoke — main thread permission card round-trip | S | ⏳ | | |
| 4.3 | Smoke — main thread question card (multi-question + multi-select + free-text) | S | ⏳ | | |
| 4.4 | Smoke — subagent permission + question cards (Decision #5 validation) | M | ⏳ | | |
| 4.5 | Cleanup — delete F05 docs, update roadmap.md, update v2-features README.md | S | ⏳ | | |

17 tasks total. Group 1 + 2 are critical path — once they work, the protocol round-trips end-to-end. Group 3 is the visible UX. Group 4 is lifecycle + validation + cleanup.

---

## Group 1 — `canUseTool` in sdk-entry.js

> Goal: a `canUseTool` callback registered in `query()` options that intercepts both kinds of requests, dispatches to React via stdout events, and resolves on stdin response. After this group, the Node side is ready — UI comes in Group 3.

### Task 1.1 — `canUseTool` callback skeleton in sdk-entry.js

**Size:** S
**Refs:** spec "What this is", spec "Architecture overview", parent design doc Decision #1

**Context:** F02 task 2.2 established `query()` invocation in `sdk-entry.js` with `mcpServers` + `plugins` + `permissionMode` options. F04 adds `canUseTool` to the same options object. This task wires the skeleton — registration, type imports, dispatch shell — without yet emitting events or resolving promises (those come in 1.2 / 1.3). Compile clean and supervisor still spawns successfully.

**Output:**

- `App~/src-tauri/src/claude_supervisor/sdk_entry.js` (template fonte; runtime mirror regenerates per F02 bug B4 workflow):
  - Import `CanUseTool` and `PermissionResult` types from `@anthropic-ai/claude-agent-sdk` (TS-style import in JSDoc since file is `.js`)
  - Module-level `pending = new Map()` (key: `requestId`, value: `{ resolve, reject, requestType }`)
  - Function `canUseToolCallback(toolName, input, opts)`:
    - If `toolName === "AskUserQuestion"` → console.error stub: `[canUseTool] AskUserQuestion intercepted (1.2 will emit)` + return `{ behavior: "allow", updatedInput: input }` placeholder so flow doesn't stall during dev
    - Else → console.error stub: `[canUseTool] permission for ${toolName} (1.2 will emit)` + return `{ behavior: "allow", updatedInput: input }` placeholder (the SDK's Zod schema requires `updatedInput` on `behavior: "allow"` even when we don't modify; passing `input` unchanged signals "allow as-is")
  - Add `canUseTool: canUseToolCallback` to the `queryOptions` object passed to `query()` in `handleInput`
- No new exports; this is internal supervisor scaffolding
- Apply same change to runtime mirror manually after build (per F02 bug B4 — delete `runtime/sdk-entry.js` before testing)

**Validation:**

1. `cargo check` clean (Rust unchanged in this task).
2. `pnpm tsc --noEmit` clean (TS unchanged).
3. Open Tauri via F07 pin → supervisor reaches `Ready` as before (~7-9s).
4. Send any prompt that doesn't trigger tool calls ("hi") → response streams back, no regressions.
5. Switch to `default` permission mode + send "list scenes in unity" (triggers MCP tool) → in DevTools terminal output you should see `[canUseTool] permission for mcp__game-deck__scene-list (1.2 will emit)`; tool call proceeds (because of placeholder allow).
6. **NOTA**: this task intentionally leaves the placeholder allowing everything. Don't set `bypassPermissions` mode — verify the callback fires. The actual user prompt UX comes in Group 3.

**Commit:**

```
feat(v2): F04 task 1.1 — canUseTool callback skeleton in sdk-entry.js

Registers a canUseTool function in query() options that dispatches
by toolName: AskUserQuestion vs permission request. Both branches
log to stderr and return placeholder allow — Group 1.2 / 1.3 wire
the actual emit + stdin response loop.

Module-level pending Map sized for the upcoming requestId → resolver
table.

Refs: 04-interactive-approvals-tasks.md (task 1.1)
```

---

### Task 1.2 — Emit branches — ask-user-requested, permission-requested, request-resolved

**Size:** S
**Refs:** spec "Wire protocol — additions only" (outgoing events)

**Context:** With the callback registered, this task replaces the placeholder `console.error` + immediate-allow with real event emission to Tauri stdout. The promises returned by `canUseToolCallback` now block until 1.3's stdin handler resolves them. Without 1.3, the conversation would stall — but compile-clean and the events should be observable in DevTools to validate the wire format.

**Output:**

- `sdk-entry.js`:
  - Helper `emitAskUserRequested(requestId, turnId, agentId, input)` — `console.log(JSON.stringify({ type: "ask-user-requested", requestId, turnId, agentId, input }))`
  - Helper `emitPermissionRequested(requestId, turnId, agentId, toolName, input, blockedPath, decisionReason)` — same pattern
  - Helper `emitRequestResolved(requestId, outcome, answer)` — fires after the awaited promise resolves
  - `canUseToolCallback` rewritten:
    ```js
    async function canUseToolCallback(toolName, input, opts) {
      const requestId = opts.toolUseID;
      const turnId = currentTurnId;          // module-level, set by handleInput
      const agentId = opts.agentID || null;
      
      if (toolName === "AskUserQuestion") {
        emitAskUserRequested(requestId, turnId, agentId, input);
        const answer = await new Promise((resolve, reject) => {
          pending.set(requestId, { resolve, reject, requestType: "question" });
        });
        emitRequestResolved(requestId, "allow", answer);
        return { behavior: "allow", updatedInput: answer };
      }
      
      emitPermissionRequested(requestId, turnId, agentId, toolName, input, opts.blockedPath, opts.decisionReason);
      const decision = await new Promise((resolve, reject) => {
        pending.set(requestId, { resolve, reject, requestType: "permission" });
      });
      emitRequestResolved(requestId, decision.outcome);
      
      if (decision.outcome === "deny") {
        return { behavior: "deny", message: "User denied via Tauri UI", interrupt: false };
      }
      return { behavior: "allow", updatedInput: input };
    }
    ```
  - `currentTurnId` module variable set at start of `handleInput` (before `query()` call) so emit helpers can access it
- Runtime mirror updated per F02 bug B4 workflow

**Validation:**

1. `pnpm tauri dev` → supervisor `Ready` as before.
2. Switch to `default` mode + send "list scenes in unity" → conversation **STALLS** (expected, no resolver wired yet); DevTools console should show a `permission-requested` event arrive on `agent-message` channel with the right payload (toolName, input, requestId).
3. Open ChatRoute's `onAgentMessage` listener path — payload arrives but no case in the existing switch matches → silently dropped (expected, Group 3 adds the case).
4. Restart Supervisor (F02 4.x button) → recovers cleanly; pending Map gets cleared on respawn (Map dies with the process).
5. Switch to `bypassPermissions` mode + send same prompt → tool call proceeds without invoking `canUseTool` (SDK auto-approves under bypass); no event emitted. Verifies the callback only fires when SDK delegates to it.

**Commit:**

```
feat(v2): F04 task 1.2 — canUseTool emits ask-user-requested + permission-requested

canUseToolCallback now emits typed events on stdout for both branches
and awaits a Promise stored in pending Map. Resolution happens in 1.3
when stdin handler arrives; meanwhile conversations using default mode
will stall, which is expected — the round-trip is half-built.

emitRequestResolved fires after the promise resolves (or after auto-
allow short-circuit lands in 1.4).

Refs: 04-interactive-approvals-tasks.md (task 1.2)
```

---

### Task 1.3 — `respond-to-request` stdin handler

**Size:** S
**Refs:** spec "Wire protocol — additions only" (incoming), spec "Permission request contract"

**Context:** Tauri-side response to a card click writes a JSON line to supervisor's stdin. `sdk-entry.js`'s existing readline loop on stdin needs a new branch that looks up the resolver in `pending` and resolves it with the appropriate shape. After this task, the round-trip closes end-to-end at the wire level — UI in Group 3 is what surfaces it visually.

**Output:**

- `sdk-entry.js` stdin loop:
  - New branch on `parsed.type === "respond-to-request"`:
    ```js
    if (parsed.type === "respond-to-request") {
      const entry = pending.get(parsed.requestId);
      if (!entry) {
        debug(`[canUseTool] received response for unknown requestId ${parsed.requestId} — ignoring`);
        continue;
      }
      pending.delete(parsed.requestId);
      
      if (entry.requestType === "question") {
        // parsed.decision: { kind: "question", answer: AskUserQuestionOutput }
        entry.resolve(parsed.decision.answer);
      } else {
        // parsed.decision: { kind: "permission", outcome: "allow" | "allow-always" | "deny" }
        entry.resolve({ outcome: parsed.decision.outcome });
      }
      continue;
    }
    ```
  - Defensive: validate `parsed.decision.kind` matches `entry.requestType` — mismatched type → log + reject with explanatory error (treats as denial for permission, as dismissed for question)
- No changes to existing branches (`input`, `setPermissionMode`, `setResumeSession`, `clearResumeSession`, `healthCheck`)
- Runtime mirror updated

**Validation:**

1. `pnpm tauri dev` → supervisor Ready.
2. Manual test via DevTools console: send a permission-triggering prompt in default mode, wait for the stalled state, then in DevTools:
   ```javascript
   const { invoke } = await import('@tauri-apps/api/core');
   // grab requestId from the most recent permission-requested event in console
   await invoke('respond_to_request', {
     requestId: '<copied requestId>',
     decision: { kind: 'permission', outcome: 'allow' }
   });
   ```
   — wait, this requires the Rust command to exist (task 2.2). For this task's validation, manually write a JSON line directly via supervisor stdin if possible, OR defer the round-trip smoke to 2.2 and validate this task by:
   - Starting supervisor, mocking the stdin write via a test harness (skip if too much overhead — task 2.2 will exercise this path immediately)
   - Inspecting the new branch via `node -e` syntax-check
3. Confirm no regression: existing `setPermissionMode`, `setResumeSession`, `healthCheck` stdin branches still work (toggle permission mode dropdown → mode change propagates).

**Note:** This task is intentionally testable in isolation only with extra harness; the realistic smoke comes after task 2.2 lands the Rust command. That's fine — tasks 1.3 and 2.2 are paired logical units, just split across the wire boundary.

**Commit:**

```
feat(v2): F04 task 1.3 — respond-to-request stdin handler resolves canUseTool promises

sdk-entry.js stdin loop gains a respond-to-request branch that looks
up the requestId in pending Map and resolves the awaited promise with
the appropriate shape (AskUserQuestionOutput for questions, decision
object for permissions).

Defensive type-mismatch handling: kind in payload must match
requestType on resolver entry, else log + reject.

After 2.2 lands the Tauri command, the round-trip closes end-to-end.

Refs: 04-interactive-approvals-tasks.md (task 1.3)
```

---

### Task 1.4 — In-session "Allow Always" cache

**Size:** S
**Refs:** spec "Stack" (in-session "Allow Always" cache row), parent design doc scope IN

**Context:** When the user clicks "Allow Always" on a permission card, subsequent identical requests should auto-resolve without surfacing UI. Cache lives in `sdk-entry.js` (single source of truth), keyed by `toolName + stable hash of input`. The `request-resolved` event fires with `outcome: "auto-allowed"` so the React side can render a synthetic compact block in the message stream for context.

**Output:**

- `sdk-entry.js`:
  - Module-level `allowAlwaysCache = new Set()` (string keys)
  - Helper `cacheKey(toolName, input)` returns `${toolName}:${stableHash(input)}` — `stableHash` is a simple deterministic JSON serialization helper (sort keys, then SHA-256 first 16 hex chars; OR for v2.0 simplicity, just `JSON.stringify` on a sorted-keys clone — collision space is huge and the cache is in-memory)
  - In `canUseToolCallback`'s permission branch, before emitting:
    ```js
    const key = cacheKey(toolName, input);
    if (allowAlwaysCache.has(key)) {
      emitRequestResolved(requestId, "auto-allowed");
      return { behavior: "allow", updatedInput: input };
    }
    ```
  - In stdin's `respond-to-request` branch, when `outcome === "allow-always"`, also add to cache:
    ```js
    if (parsed.decision.outcome === "allow-always") {
      const key = cacheKey(entry.toolName, entry.input);  // entry needs these fields stored at canUseTool time
      allowAlwaysCache.add(key);
    }
    ```
  - To make the above work, expand the `pending` Map entry to include `toolName` and `input`:
    ```js
    pending.set(requestId, { resolve, reject, requestType: "permission", toolName, input });
    ```
- Runtime mirror updated

**Validation:**

1. `pnpm tauri dev` + the rest of Group 1.
2. Smoke (after 2.2 lands the Tauri command, before Group 3 UI is real — manual stdin probe):
   - Send a permission-triggering prompt
   - Manually invoke `respond_to_request` with `outcome: "allow-always"` for the surfaced requestId
   - Send the **same** prompt again that triggers an identical tool call
   - Expected: no `permission-requested` event emitted; instead, immediate `request-resolved` with `outcome: "auto-allowed"`. Tool call proceeds.
3. Send a prompt with **different** input (same tool, different params) → cache miss → permission-requested fires normally.
4. Restart Supervisor → cache cleared (Map dies); same request re-prompts.

**Commit:**

```
feat(v2): F04 task 1.4 — in-session Allow Always cache for permission requests

allowAlwaysCache Set keyed by toolName:stableHash(input). On
allow-always outcome, key is added; subsequent canUseTool calls hit
the cache and auto-resolve with outcome: auto-allowed (single
request-resolved event emitted; no permission-requested).

In-memory only (Decision #7 of design doc). Cleared on supervisor
exit. Cross-session persistence is v2.1+.

Refs: 04-interactive-approvals-tasks.md (task 1.4)
```

---

## Group 2 — Tauri Rust types + commands

> Goal: types.rs grows the new AgentMessage variants and decision payloads; a new Tauri command writes the response to stdin. After this group, the protocol round-trip is observable end-to-end via DevTools (UI is still Group 3).

### Task 2.1 — Rust types — AgentMessage variants + DecisionPayload + Block "request" variant

**Size:** S
**Refs:** spec "File layout" (modified files), spec "Wire protocol"

**Output:**

- `App~/src-tauri/src/types.rs` extended:
  - `AgentMessage` enum gains 3 new variants (kebab-case via existing `#[serde(rename_all = "kebab-case")]`):
    ```rust
    AskUserRequested {
      request_id: String,
      turn_id: String,
      agent_id: Option<String>,
      input: serde_json::Value,
    },
    PermissionRequested {
      request_id: String,
      turn_id: String,
      agent_id: Option<String>,
      tool_name: String,
      input: serde_json::Value,
      blocked_path: Option<String>,
      decision_reason: Option<String>,
    },
    RequestResolved {
      request_id: String,
      outcome: String,                              // "allow" | "allow-always" | "deny" | "auto-allowed"
      answer: Option<serde_json::Value>,            // populated for question outcomes
    },
    ```
  - New struct `DecisionPayload` for the Tauri command input (camelCase via attribute):
    ```rust
    #[derive(Debug, Deserialize, Serialize)]
    #[serde(tag = "kind", rename_all = "camelCase")]
    pub enum DecisionPayload {
      Permission { outcome: PermissionOutcome },
      Question { answer: serde_json::Value },     // raw AskUserQuestionOutput; we don't validate here
    }
    
    #[derive(Debug, Deserialize, Serialize)]
    #[serde(rename_all = "kebab-case")]
    pub enum PermissionOutcome {
      Allow,
      AllowAlways,
      Deny,
    }
    ```
- No changes to `events.rs` — all 3 new agent message variants ride on the existing `EVT_AGENT_MESSAGE` channel via the `AgentMessage` enum
- `cargo check` clean

**Validation:**

1. `cargo check` clean.
2. `cargo test` runs all pre-existing tests (including the 12 from `version_check.rs`) without regressions.
3. Manually verify serde round-trip via `cargo test` adding 1 small test:
   ```rust
   #[test]
   fn ask_user_requested_serializes_kebab() {
     let m = AgentMessage::AskUserRequested {
       request_id: "tu_123".into(),
       turn_id: "t_1".into(),
       agent_id: None,
       input: serde_json::json!({ "questions": [] }),
     };
     let json = serde_json::to_string(&m).unwrap();
     assert!(json.contains("\"type\":\"ask-user-requested\""));
     assert!(json.contains("\"requestId\":\"tu_123\""));
   }
   ```

**Commit:**

```
feat(v2): F04 task 2.1 — Rust types for canUseTool wire protocol

types.rs extends AgentMessage with AskUserRequested, PermissionRequested,
and RequestResolved variants — kebab-case discriminator consistent with
existing variants. DecisionPayload + PermissionOutcome types added for
the upcoming respond_to_request command (task 2.2).

Block enum extended later in 3.5 with the "request" variant for
in-stream rendering.

Refs: 04-interactive-approvals-tasks.md (task 2.1)
```

---

### Task 2.2 — Rust command `respond_to_request`

**Size:** S
**Refs:** spec "Wire protocol — additions only" (incoming)

**Output:**

- New file `App~/src-tauri/src/commands/requests.rs`:
  ```rust
  use crate::claude_supervisor::ClaudeSupervisor;
  use crate::types::DecisionPayload;
  use serde_json::json;
  use tauri::State;
  
  #[tauri::command]
  pub async fn respond_to_request(
    request_id: String,
    decision: DecisionPayload,
    supervisor: State<'_, ClaudeSupervisor>,
  ) -> Result<(), String> {
    let payload = json!({
      "type": "respond-to-request",
      "requestId": request_id,
      "decision": decision,
    });
    supervisor
      .write_stdin_line(&payload.to_string())
      .await
      .map_err(|e| format!("respond_to_request: {}", e))
  }
  ```
- `App~/src-tauri/src/claude_supervisor/mod.rs` exposes `write_stdin_line(line: &str) -> Result<(), Error>` if not already public — wraps the existing stdin sender used by `send_input` and `set_permission_mode`. If those existing methods are the only paths, refactor to a shared private `write_stdin_line` and have the public methods call it
- `App~/src-tauri/src/commands/mod.rs` adds `pub mod requests;`
- `App~/src-tauri/src/lib.rs` `invoke_handler!` macro gets `requests::respond_to_request`

**Validation:**

1. `cargo check` clean.
2. `pnpm tauri dev` + DevTools console manual test — validate end-to-end with task 1.3:
   - Switch to `default` mode + send "list scenes in unity"
   - Wait for `permission-requested` event in DevTools console (note the requestId)
   - Run:
     ```javascript
     const { invoke } = await import('@tauri-apps/api/core');
     await invoke('respond_to_request', {
       requestId: '<the requestId>',
       decision: { kind: 'permission', outcome: 'allow' }
     });
     ```
   - Expected: tool call resumes, response streams back, `request-resolved` event in DevTools with `outcome: "allow"`. Conversation completes.
3. Repeat with `outcome: "deny"` → tool call returns deny, Claude responds explaining can't proceed.
4. Repeat with `outcome: "allow-always"` → tool proceeds; send the **same** prompt again → no `permission-requested` event, just `request-resolved` with `outcome: "auto-allowed"` (validates 1.4's cache hit path).
5. Send invalid requestId → supervisor stderr shows the "unknown requestId" debug message (1.3 defensive); Tauri command itself returns Ok (we don't error on unknown — sdk-entry handles).

**Commit:**

```
feat(v2): F04 task 2.2 — respond_to_request Tauri command

commands/requests.rs writes a respond-to-request JSON line to the
supervisor's stdin via shared write_stdin_line method on
ClaudeSupervisor. Round-trip validated end-to-end with task 1.3:
permission card decision → Tauri → stdin → sdk-entry resolves canUseTool
promise → query() resumes.

Allow Always cache from 1.4 also exercised: second identical request
short-circuits via auto-allowed.

Refs: 04-interactive-approvals-tasks.md (task 2.2)
```

---

### Task 2.3 — TS bindings — replace stub types, add respondToRequest, extend Block union

**Size:** S
**Refs:** spec "File layout" (`App~/src/ipc/`)

**Context:** F01 left stub types `AskUserRequestedPayload` and `PermissionRequestedPayload` in `ipc/types.ts` without a producer. F04 replaces these with the full SDK-shape versions and extends the `AgentMessage` discriminated union and the `Block` discriminated union to carry the new types.

**Output:**

- `App~/src/ipc/types.ts`:
  - `AskUserType` removed — superseded by SDK schema
  - `AskUserRequestedPayload` rewritten:
    ```typescript
    export interface AskUserQuestion {
      header?: string;
      question: string;
      multiSelect: boolean;
      options: Array<{ label: string; description?: string }>;
    }
    
    export interface AskUserRequestedPayload {
      requestId: string;
      turnId: string;
      agentId: string | null;
      input: { questions: AskUserQuestion[] };
    }
    ```
  - `PermissionRequestedPayload` rewritten:
    ```typescript
    export interface PermissionRequestedPayload {
      requestId: string;
      turnId: string;
      agentId: string | null;
      toolName: string;
      input: unknown;
      blockedPath: string | null;
      decisionReason: string | null;
    }
    ```
  - New `RequestResolvedPayload`:
    ```typescript
    export interface RequestResolvedPayload {
      requestId: string;
      outcome: "allow" | "allow-always" | "deny" | "auto-allowed";
      answer?: AskUserQuestionOutput;
    }
    
    export interface AskUserQuestionAnswer {
      selectedOptions: string[];
      freeTextResponse?: string;
    }
    
    export interface AskUserQuestionOutput {
      answers: AskUserQuestionAnswer[];
    }
    ```
  - New `DecisionPayload` (matches Rust enum):
    ```typescript
    export type DecisionPayload =
      | { kind: "permission"; outcome: "allow" | "allow-always" | "deny" }
      | { kind: "question"; answer: AskUserQuestionOutput };
    ```
  - `AgentMessage` union extended with 3 new variants:
    ```typescript
    | { type: "ask-user-requested"; requestId: string; turnId: string; agentId: string | null;
        input: { questions: AskUserQuestion[] } }
    | { type: "permission-requested"; requestId: string; turnId: string; agentId: string | null;
        toolName: string; input: unknown; blockedPath: string | null; decisionReason: string | null }
    | { type: "request-resolved"; requestId: string; outcome: "allow" | "allow-always" | "deny" | "auto-allowed";
        answer?: AskUserQuestionOutput }
    ```
  - `Block` union extended with `request` variant:
    ```typescript
    | { type: "request"; requestId: string; subtype: "permission" | "question"; payload: PermissionRequestedPayload | AskUserRequestedPayload; state: "pending" | "answered" | "interrupted" | "auto-allowed"; answer?: AskUserQuestionOutput; outcome?: "allow" | "allow-always" | "deny" | "auto-allowed" }
    ```
- `App~/src/ipc/commands.ts` adds:
  ```typescript
  import { invoke } from "@tauri-apps/api/core";
  import type { DecisionPayload } from "./types";
  
  export const respondToRequest = (requestId: string, decision: DecisionPayload): Promise<void> =>
    invoke("respond_to_request", { requestId, decision });
  ```
- No changes to `ipc/events.ts` — new agent messages route through the existing `onAgentMessage` subscriber (the discriminated union expansion is enough for the consumer to switch on)

**Validation:**

1. `pnpm tsc --noEmit` clean.
2. `pnpm tauri dev` runs without React errors.
3. ChatRoute's existing `onAgentMessage` switch still works (new variants fall to the default no-op since Group 3 hasn't added cases yet — F02 task 4.3 already established this pattern of additive variants without producer being safe).
4. Stub types removed cleanly — grep `AskUserType` returns zero matches anywhere in `App~/src/`.

**Commit:**

```
feat(v2): F04 task 2.3 — TS bindings for canUseTool wire protocol

App~/src/ipc/types.ts replaces the F01-era stub types
AskUserRequestedPayload / PermissionRequestedPayload (which had no
producer) with full SDK-shape versions matching AskUserQuestionInput
and CanUseTool's options struct. Adds RequestResolvedPayload,
AskUserQuestionOutput, DecisionPayload.

AgentMessage union extended with ask-user-requested, permission-
requested, request-resolved variants (kebab-case wire). Block union
extended with "request" variant for in-stream card rendering (Group
3.5 wires it).

ipc/commands.ts gains respondToRequest(requestId, decision) wrapper
over the Tauri command from 2.2.

Refs: 04-interactive-approvals-tasks.md (task 2.3)
```

---

## Group 3 — React components

> Goal: cards render in-stream, route user clicks back through `respondToRequest`, stay in chat as historical context after answered. After this group, F04 is feature-complete from a UX perspective; Group 4 is lifecycle + smoke.

### Task 3.1 — react-markdown dep + markdown-renderers.tsx

**Size:** S
**Refs:** spec "Stack" (Markdown rendering), spec "React component design" (`markdown-renderers.tsx`)

**Output:**

- `App~/package.json` add dependency: `"react-markdown": "^9.0.0"` (or current stable at task time)
- Run `pnpm install` (Ramon does this on PC)
- New file `App~/src/components/requests/markdown-renderers.tsx`:
  ```typescript
  import type { Components } from "react-markdown";
  
  /**
   * Tailwind-styled component overrides for react-markdown.
   * 
   * Used by RequestCard to render markdown content in permission
   * and question card bodies. Styled to match the chat's existing
   * slate palette; F09 (Design Handoff) restyles in-place.
   */
  export const markdownRenderers: Components = {
    p: ({ children }) => (
      <p className="text-sm text-slate-200 mb-2 last:mb-0 leading-relaxed">
        {children}
      </p>
    ),
    strong: ({ children }) => (
      <strong className="font-semibold text-slate-100">{children}</strong>
    ),
    em: ({ children }) => (
      <em className="italic text-slate-300">{children}</em>
    ),
    code: ({ inline, children }) =>
      inline ? (
        <code className="rounded bg-slate-900 px-1 py-0.5 text-xs font-mono text-emerald-300">
          {children}
        </code>
      ) : (
        <pre className="rounded bg-slate-900 p-2 my-2 overflow-x-auto">
          <code className="text-xs font-mono text-slate-200">{children}</code>
        </pre>
      ),
    ul: ({ children }) => (
      <ul className="list-disc list-inside text-sm text-slate-200 mb-2 space-y-0.5">
        {children}
      </ul>
    ),
    ol: ({ children }) => (
      <ol className="list-decimal list-inside text-sm text-slate-200 mb-2 space-y-0.5">
        {children}
      </ol>
    ),
    li: ({ children }) => <li className="leading-relaxed">{children}</li>,
    a: ({ children, href }) => (
      <a
        href={href}
        className="text-sky-400 hover:text-sky-300 underline"
        target="_blank"
        rel="noopener noreferrer"
      >
        {children}
      </a>
    ),
    h1: ({ children }) => (
      <h1 className="text-base font-semibold text-slate-100 mb-2 mt-1">{children}</h1>
    ),
    h2: ({ children }) => (
      <h2 className="text-sm font-semibold text-slate-100 mb-1.5 mt-2">{children}</h2>
    ),
    h3: ({ children }) => (
      <h3 className="text-sm font-semibold text-slate-200 mb-1 mt-1.5">{children}</h3>
    ),
    blockquote: ({ children }) => (
      <blockquote className="border-l-2 border-slate-600 pl-3 my-2 text-slate-300 italic">
        {children}
      </blockquote>
    ),
  };
  ```

**Validation:**

1. `pnpm tsc --noEmit` clean (after install).
2. `pnpm tauri dev` runs.
3. Sanity render: temporarily mount `<ReactMarkdown components={markdownRenderers}>{"# H1\n\n**bold** and `code` and a [link](https://example.com)\n\n- item\n- item"}</ReactMarkdown>` in a corner of `ChatRoute` and verify rendering matches Tailwind classes; revert before commit.
4. No regressions in existing chat rendering (markdown is opt-in at usage site).

**Commit:**

```
feat(v2): F04 task 3.1 — react-markdown dep + Tailwind component overrides

Adds react-markdown ^9 to App~/package.json.

App~/src/components/requests/markdown-renderers.tsx exports a Components
override map matching the chat's slate palette: p / strong / em / code
(inline + block) / ul / ol / li / a / h1-h3 / blockquote. F09 restyles
by editing this single file.

Used by RequestCard (3.2) and its variants (3.3 / 3.4) for body content
rendering.

Refs: 04-interactive-approvals-tasks.md (task 3.1)
```

---

### Task 3.2 — RequestCard base component

**Size:** M
**Refs:** spec "React component design" (`RequestCard` base)

**Output:**

- New file `App~/src/components/requests/RequestCard.tsx`:
  - Props:
    ```typescript
    interface RequestCardProps {
      variant: "permission" | "question";
      label: string;                         // "Permission required" | "Clarifying questions"
      bodyMarkdown: string;
      agentId: string | null;
      state: "pending" | "answered" | "interrupted" | "auto-allowed";
      footer: ReactNode;                     // variant-specific actions
    }
    ```
  - Renders:
    - Border-left accent: `border-yellow-500` for permission, `border-blue-500` for question
    - Header strip: variant icon (use lucide-react if available, or simple emoji `🛡️` / `❓`) + label uppercase tracking + agentId if present (`from {agentId}`)
    - Body: `<ReactMarkdown components={markdownRenderers}>{bodyMarkdown}</ReactMarkdown>` inside `<div className="prose prose-sm prose-invert">` wrapper
    - Footer: {footer} children rendered right-aligned
    - Visual states:
      - `pending` — full color, footer interactive
      - `answered` — slight opacity reduction (`opacity-70`), footer disabled
      - `interrupted` — `opacity-50`, footer disabled, "Conversation interrupted" caption underneath
      - `auto-allowed` — compact rendering: just `<div className="text-xs text-slate-500 italic">Auto-allowed: {toolName}</div>` (no body, no footer)
  - Tailwind classes consistent with rest of chat (slate-800/60 bg, slate-100 text, etc.)

**Validation:**

1. `pnpm tsc --noEmit` clean.
2. Storybook-equivalent sanity test: mount in a temporary route or alongside ChatRoute:
   ```tsx
   <RequestCard
     variant="permission"
     label="Permission required"
     bodyMarkdown="**`mcp__game-deck__scene-create`** wants to proceed with these inputs:\n\n```json\n{\"name\": \"NewScene\"}\n```"
     agentId={null}
     state="pending"
     footer={<><button>Allow</button><button>Deny</button></>}
   />
   ```
3. Verify all 4 states render correctly by toggling the prop manually.
4. Verify `auto-allowed` state renders compactly (no body, just one-line).
5. Verify with long markdown body that the card scrolls / wraps appropriately.
6. Revert sanity test mount before commit.

**Commit:**

```
feat(v2): F04 task 3.2 — RequestCard base component

App~/src/components/requests/RequestCard.tsx renders the shared chrome
for both permission and question variants: accent border (yellow vs
blue), header strip with icon + label + optional agentId, markdown body
via react-markdown + 3.1 renderers, footer slot for variant actions.

Four states:
- pending: full color, footer interactive
- answered: dimmed, footer disabled
- interrupted: heavily dimmed + "Conversation interrupted" caption
- auto-allowed: compact one-line rendering (used when 1.4 cache hits)

F09 restyles in-place; structure stays.

Refs: 04-interactive-approvals-tasks.md (task 3.2)
```

---

### Task 3.3 — PermissionRequestCard variant

**Size:** S
**Refs:** spec "React component design" (`PermissionRequestCard`)

**Output:**

- New file `App~/src/components/requests/PermissionRequestCard.tsx`:
  - Props:
    ```typescript
    interface PermissionRequestCardProps {
      payload: PermissionRequestedPayload;
      state: "pending" | "answered" | "interrupted" | "auto-allowed";
      onDecision: (outcome: "allow" | "allow-always" | "deny") => void;
    }
    ```
  - Helper `formatPermissionBody(payload): string` builds markdown:
    ```typescript
    function formatPermissionBody(p: PermissionRequestedPayload): string {
      const inputJson = JSON.stringify(p.input, null, 2);
      const truncated = inputJson.length > 800 ? inputJson.slice(0, 800) + "\n... (truncated)" : inputJson;
      const lines = [
        `**\`${p.toolName}\`** wants to proceed with these inputs:`,
        ``,
        "```json",
        truncated,
        "```",
      ];
      if (p.decisionReason) lines.push("", p.decisionReason);
      if (p.blockedPath) lines.push("", `_Blocks path:_ \`${p.blockedPath}\``);
      return lines.join("\n");
    }
    ```
  - Renders `<RequestCard variant="permission" label="Permission required" bodyMarkdown={...} agentId={p.agentId} state={state} footer={...} />`
  - Footer for `pending`:
    ```tsx
    <>
      <button onClick={() => onDecision("deny")} className="rounded border border-red-700 px-3 py-1 text-xs text-red-400 hover:bg-red-950">Deny</button>
      <button onClick={() => onDecision("allow-always")} className="rounded border border-slate-600 px-3 py-1 text-xs text-slate-300 hover:bg-slate-800">Allow Always</button>
      <button onClick={() => onDecision("allow")} className="rounded bg-sky-700 px-3 py-1 text-xs text-sky-50 hover:bg-sky-600">Allow</button>
    </>
    ```
  - Footer for non-pending states: same buttons but `disabled` (`disabled:opacity-50 disabled:cursor-not-allowed`); show outcome label as caption when answered (e.g. "Allowed" / "Allowed always" / "Denied")

**Validation:**

1. `pnpm tsc --noEmit` clean.
2. Manual mount sanity-test (revert before commit) — verify all 3 buttons render, click handlers fire correctly via console.log.
3. Manually trigger payload variants — short input, long input (truncation kicks in), input with blockedPath set, input with decisionReason — all render readable.

**Commit:**

```
feat(v2): F04 task 3.3 — PermissionRequestCard variant

App~/src/components/requests/PermissionRequestCard.tsx composes
RequestCard with permission-specific body formatting (toolName + JSON
input preview truncated at 800 chars + optional decisionReason +
blockedPath) and a footer with Allow / Allow Always / Deny actions.

Buttons disabled in non-pending states with caption showing the
chosen outcome.

Refs: 04-interactive-approvals-tasks.md (task 3.3)
```

---

### Task 3.4 — QuestionCard variant

**Size:** M
**Refs:** spec "React component design" (`QuestionCard`)

**Output:**

- New file `App~/src/components/requests/QuestionCard.tsx`:
  - Props:
    ```typescript
    interface QuestionCardProps {
      payload: AskUserRequestedPayload;
      state: "pending" | "answered" | "interrupted" | "auto-allowed";
      onSubmit: (answer: AskUserQuestionOutput) => void;
      previousAnswer?: AskUserQuestionOutput;   // for answered-state display
    }
    ```
  - Per-question local state: `selectedOptions: string[][]` (per question index) + `freeTextResponse: string[]` (per question index)
  - Free-text detection heuristic: if `option.label.match(/^other\b/i)` OR `option.description?.includes("free text")` → treat as free-text fallback for that question; show text input alongside options
  - Render each question stacked vertically:
    ```tsx
    payload.input.questions.map((q, idx) => (
      <div key={idx} className="my-3 first:mt-0 last:mb-0 pb-3 border-b border-slate-700 last:border-b-0">
        {q.header && <div className="text-sm font-semibold mb-1 text-slate-200">{q.header}</div>}
        <ReactMarkdown components={markdownRenderers}>{q.question}</ReactMarkdown>
        <div className="mt-2 grid grid-cols-1 gap-1.5">
          {q.options.map(opt => {
            const isFreeText = isFreeTextOption(opt);
            const isSelected = selectedOptions[idx].includes(opt.label);
            return (
              <label className="flex items-start gap-2 rounded p-1.5 hover:bg-slate-800/40 cursor-pointer">
                <input
                  type={q.multiSelect ? "checkbox" : "radio"}
                  name={`question-${idx}`}
                  checked={isSelected}
                  onChange={() => toggleOption(idx, opt.label, q.multiSelect)}
                  disabled={state !== "pending"}
                />
                <div className="flex-1 min-w-0">
                  <div className="text-sm text-slate-200">{opt.label}</div>
                  {opt.description && <ReactMarkdown components={markdownRenderers}>{opt.description}</ReactMarkdown>}
                </div>
              </label>
            );
          })}
          {hasFreeTextOption(q) && selectedOptions[idx].some(isFreeTextLabel) && (
            <input
              type="text"
              value={freeTextResponse[idx]}
              onChange={e => setFreeText(idx, e.target.value)}
              placeholder="Type your custom answer..."
              disabled={state !== "pending"}
              className="rounded border border-slate-700 bg-slate-800 px-2 py-1 text-sm text-slate-100"
            />
          )}
        </div>
      </div>
    ))
    ```
  - Footer: single Submit button at the bottom; disabled until every question has either `selectedOptions.length > 0` OR `freeTextResponse non-empty`:
    ```tsx
    <button
      onClick={handleSubmit}
      disabled={!allAnswered || state !== "pending"}
      className="rounded bg-sky-700 px-4 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:opacity-50"
    >
      {state === "answered" ? "Answered" : "Submit"}
    </button>
    ```
  - `handleSubmit` builds `AskUserQuestionOutput` and calls `onSubmit`:
    ```typescript
    const answer: AskUserQuestionOutput = {
      answers: payload.input.questions.map((_, idx) => {
        const free = freeTextResponse[idx]?.trim();
        if (free) {
          return { selectedOptions: [], freeTextResponse: free };
        }
        return { selectedOptions: selectedOptions[idx] };
      })
    };
    onSubmit(answer);
    ```
  - Answered-state: show `previousAnswer` rendered read-only

**Validation:**

1. `pnpm tsc --noEmit` clean.
2. Manual mount sanity-test (revert before commit) with 3 fixtures:
   - 1-question single-select: 4 options, no free-text → radio buttons, Submit disabled until selection
   - 2-question multi-select: 3 options each, free-text fallback → checkboxes, free-text input appears when "Other" selected, Submit disabled until both questions answered
   - 1-question free-text only: 1 option labeled "Other (specify)" → text input shown, Submit enabled when text non-empty
3. Verify `state="answered"` mode displays previousAnswer read-only (selected options shown, text shown, all controls disabled).

**Commit:**

```
feat(v2): F04 task 3.4 — QuestionCard variant for AskUserQuestion

App~/src/components/requests/QuestionCard.tsx composes RequestCard with
multi-question vertical stack. Each question rendered with single-select
(radio), multi-select (checkbox), or free-text fallback (detected
heuristically from option label/description) per the SDK's
AskUserQuestionInput schema.

Single Submit button at the bottom; disabled until all questions are
answered. handleSubmit builds AskUserQuestionOutput in the schema
format, calls onSubmit which routes to respondToRequest in 3.5.

Answered-state shows previousAnswer read-only.

Refs: 04-interactive-approvals-tasks.md (task 3.4)
```

---

### Task 3.5 — Block integration in conversationStore + ChatRoute

**Size:** S
**Refs:** spec "File layout" (`conversationStore.ts`, `ChatRoute.tsx`)

**Output:**

- `App~/src/stores/conversationStore.ts`:
  - New actions:
    - `appendRequestBlock(turnId, block)` — pushes a `request` block to the turn (uses existing `pushBlockToTurn` helper)
    - `markRequestAnswered(requestId, answer?, outcome?)` — walks `messages`, finds the block with matching `requestId`, transitions `state: "answered"` and stores `answer` / `outcome`
    - `markRequestInterrupted(requestId)` — same lookup, transitions to `"interrupted"`
    - `markAllPendingRequestsInterrupted()` — walks all messages and flips every `pending` request block to `interrupted`
    - `appendAutoAllowedBlock(turnId, requestId, toolName)` — synthesizes a compact `request` block with `state: "auto-allowed"` for cache-hit visibility
- `App~/src/routes/ChatRoute.tsx`:
  - `BlockView` extended:
    ```typescript
    case "request": {
      if (block.state === "auto-allowed") {
        return (
          <div className="text-xs text-slate-500 italic">
            Auto-allowed: {(block.payload as PermissionRequestedPayload).toolName}
          </div>
        );
      }
      
      const handleDecision = (outcome: "allow" | "allow-always" | "deny") => {
        markRequestAnswered(block.requestId, undefined, outcome);
        void respondToRequest(block.requestId, { kind: "permission", outcome });
      };
      
      const handleQuestionSubmit = (answer: AskUserQuestionOutput) => {
        markRequestAnswered(block.requestId, answer);
        void respondToRequest(block.requestId, { kind: "question", answer });
      };
      
      if (block.subtype === "permission") {
        return (
          <PermissionRequestCard
            payload={block.payload as PermissionRequestedPayload}
            state={block.state}
            onDecision={handleDecision}
          />
        );
      }
      return (
        <QuestionCard
          payload={block.payload as AskUserRequestedPayload}
          state={block.state}
          onSubmit={handleQuestionSubmit}
          previousAnswer={block.answer}
        />
      );
    }
    ```
  - `onAgentMessage` switch extended:
    ```typescript
    case "permission-requested":
      appendRequestBlock(m.turnId, {
        type: "request",
        requestId: m.requestId,
        subtype: "permission",
        payload: { requestId: m.requestId, turnId: m.turnId, agentId: m.agentId,
                   toolName: m.toolName, input: m.input, blockedPath: m.blockedPath,
                   decisionReason: m.decisionReason },
        state: "pending",
      });
      break;
    case "ask-user-requested":
      appendRequestBlock(m.turnId, {
        type: "request",
        requestId: m.requestId,
        subtype: "question",
        payload: { requestId: m.requestId, turnId: m.turnId, agentId: m.agentId, input: m.input },
        state: "pending",
      });
      break;
    case "request-resolved":
      if (m.outcome === "auto-allowed") {
        // Synthesize a compact block — we never saw a permission-requested for it
        // (cache short-circuited), but user should see history.
        // Note: we don't have toolName here; emit it from sdk-entry's
        // emitRequestResolved when outcome === "auto-allowed". Adjust 1.4 emit
        // to include toolName + turnId in the payload for that outcome only.
        appendAutoAllowedBlock(m.turnId, m.requestId, m.toolName);
      } else {
        // Idempotent confirmation — block was already marked answered optimistically
        markRequestAnswered(m.requestId, m.answer, m.outcome);
      }
      break;
    ```
  - **Note for 1.4 follow-up**: `request-resolved` payload for `auto-allowed` outcome needs to carry `toolName` and `turnId` so React can synthesize the block. Adjust `emitRequestResolved` signature when 1.4 is implemented OR via small follow-up edit; document the dependency

**Validation:**

1. `pnpm tsc --noEmit` clean.
2. End-to-end sanity (still without Group 4's lifecycle handling): switch to `default` mode, send "list scenes in unity" → permission card appears in the chat → click Allow → tool call resumes → response streams in → card stays in stream as historical "answered" block.
3. Click Allow Always on a fresh permission card → next identical request → compact "Auto-allowed: ..." block appears in the chat (synthesized from `request-resolved` event with `outcome: auto-allowed`).
4. Click Deny → tool call returns deny → Claude responds explaining can't proceed; card stays as answered (Denied caption).
5. Send a deliberately ambiguous prompt that triggers `AskUserQuestion` → question card appears → fill in answers → Submit → conversation continues; card stays as answered.
6. No regressions in existing chat: text streaming, tool-use/tool-result blocks, attachments — all work as before.

**Commit:**

```
feat(v2): F04 task 3.5 — request blocks integrated into conversationStore + ChatRoute

conversationStore gains appendRequestBlock, markRequestAnswered,
markRequestInterrupted, markAllPendingRequestsInterrupted,
appendAutoAllowedBlock actions.

ChatRoute's BlockView routes the new "request" Block variant to
PermissionRequestCard (3.3) or QuestionCard (3.4) based on subtype.
onAgentMessage switch extended with three new variants:
permission-requested → append pending block; ask-user-requested →
append pending block; request-resolved → mark answered (idempotent
optimistic confirmation) or synthesize auto-allowed block.

End-to-end UX validated: default mode tool calls now surface inline
permission cards instead of stalling; user clicks Allow / Allow
Always / Deny and the conversation continues.

Refs: 04-interactive-approvals-tasks.md (task 3.5)
```

---

## Group 4 — Lifecycle, smoke, cleanup

> Goal: validate end-to-end including failure cases; close out the feature with doc maintenance.

### Task 4.1 — Lifecycle: supervisor crash → mark interrupted

**Size:** S
**Refs:** parent design doc Decision #7, spec "Lifecycle handling"

**Output:**

- `App~/src/routes/ChatRoute.tsx` (or `App~/src/App.tsx` — wherever the supervisor-status-changed listener already lives from F02):
  - Add subscriber that listens to `supervisor-status-changed` events
  - On `status === "crashed"` OR `status === "failed"` → call `conversationStore.getState().markAllPendingRequestsInterrupted()`
  - On `status === "ready"` → no-op (no automatic re-emit; resume happens via SessionList click)
- `conversationStore.markAllPendingRequestsInterrupted` walks all messages, all blocks, finds `type === "request" && state === "pending"`, transitions to `"interrupted"`

**Validation:**

1. `pnpm tauri dev` → supervisor Ready.
2. Switch to `default` mode + send a tool-call-triggering prompt → permission card appears (pending).
3. **DON'T click any button.** Open Task Manager → kill the `node.exe` child of the Tauri process (the one running `sdk-entry.js`).
4. Within ~2s, supervisor status flips to `crashed` (F02 task 6.2) → permission card transitions to `interrupted` state visually (greyed, "Conversation interrupted" caption appears, buttons disabled).
5. Click Restart Supervisor (F02 task 6.2 button) → supervisor respawns → status `ready` → card stays interrupted (no automatic re-emit, by design).
6. Click the most recent session in SessionList → `resume_session` triggers → supervisor reconnects to that session → Claude Code's session storage replays the pending tool call → fresh `permission-requested` event arrives → new pending card appears in the chat (the old interrupted card stays as historical context).
7. Click Allow on the fresh card → tool call resumes; conversation continues normally.

**Commit:**

```
feat(v2): F04 task 4.1 — supervisor crash flips pending requests to interrupted

ChatRoute subscribes to supervisor-status-changed; on crashed/failed,
calls markAllPendingRequestsInterrupted on the conversation store.
All pending request blocks transition to "interrupted" state with
greyed visual + "Conversation interrupted" caption.

Restart Supervisor + session resume produces fresh canUseTool round
when Claude Code replays the pending tool call from session storage
— validated empirically.

Refs: 04-interactive-approvals-tasks.md (task 4.1)
```

---

### Task 4.2 — Smoke: main thread permission card

**Size:** S
**Refs:** spec "Subagent inheritance" (Smoke 1)

**Context:** Validation task — no code changes (or only minor adjustments if smoke reveals issues). Document outcome in this tasks doc.

**Output:**

- Manually exercise the main-thread permission flow with a variety of tool calls
- Document any issues or rough edges observed; fix in-place if minor, escalate to a follow-up task if not
- Update this task's row in the status table with the outcome paragraph

**Validation flow:**

1. `pnpm tauri dev` → supervisor Ready.
2. Switch to `default` mode.
3. Test prompt 1: "Read the README.md in this Unity project" → triggers `Read` tool → permission card appears with `Read` toolName + path in input → click Allow → response with file contents streams in → card stays as Allowed.
4. Test prompt 2 (same kind of tool, similar input): "Read package.json" → permission card appears (cache miss because exact input differs) → click Allow Always → response streams → next "Read" with similar input auto-allowed (compact synthetic block visible).
5. Test prompt 3: "Run a bash command to list files in the current directory" → triggers `Bash` tool → permission card → click Deny → Claude responds explaining can't continue without that.
6. Test prompt 4: "Create a new scene called 'TestF04' in Unity" → triggers `mcp__game-deck__scene-create` MCP tool → permission card appears with MCP tool input preview → click Allow → scene created in Unity, response streams.
7. Test prompt 5: long tool input (e.g. "Write a 1000-line config to scratchpad.json") → permission card body shows truncated input + "... (truncated)" marker → readable.
8. Verify all cards remain in chat history with correct answered states; sessions can resume and chat reload preserves them.

**Commit:**

```
chore(v2): F04 task 4.2 — main thread permission card smoke validation

Validated 5 permission flows in default mode:
- Read tool call → Allow
- Read tool call (different input) → Allow Always (Allow Always cache
  hit on subsequent identical call)
- Bash tool call → Deny (Claude explains)
- MCP Game Deck tool call → Allow
- Long input tool call → truncated preview readable

[Outcome: PASS — all 5 flows behaved as designed; no rough edges.]
[OR: outcome details + follow-ups]

Refs: 04-interactive-approvals-tasks.md (task 4.2)
```

---

### Task 4.3 — Smoke: main thread question card

**Size:** S
**Refs:** spec "Subagent inheritance" (Smoke 2), parent design doc Decision #2 + #3

**Context:** Validation task. Provoke `AskUserQuestion` with a deliberately ambiguous prompt and verify the question card UX across response types.

**Output:**

- Manually exercise question card flows with single-select, multi-select, free-text variants
- Confirm the free-text detection heuristic (3.4) actually triggers on Claude's real prompts; if not, adjust the heuristic
- Document outcome

**Validation flow:**

1. `pnpm tauri dev` → supervisor Ready.
2. Test prompt 1 (single question, single-select): "Set up a new C# script for a player controller. Don't ask if it should be MonoBehaviour or DOTS — pick one and explain why." (the deliberate negation may sometimes trigger `AskUserQuestion` despite — or use a more provoking framing if needed). Alternative provocation: "Help me name my new game. Pick a genre first and then propose names for that genre." (vague enough to potentially trigger AskUserQuestion). If neither triggers, test prompt 1 becomes: "I want to start a new project. What should I do?" with explicit "Use AskUserQuestion to clarify" hint added.
3. When question card appears: verify single-select renders as radio buttons; verify Submit is disabled until selected; click an option, click Submit → response continues.
4. Test prompt 2 (multi-question, mixed types): use a hint prompt that makes Claude bundle 2+ questions: "I want to scaffold a multiplayer game. You'll need to ask me about networking framework choice, art style, and target platforms — use AskUserQuestion with multi-select for platforms."
5. Multi-question card appears with 3 questions stacked → answer each (single-select for networking, single-select for art style, multi-select for platforms with checkboxes) → Submit when all done → response continues.
6. Test prompt 3 (free-text): if Claude generates an "Other" option, select it and verify the text input appears; type custom answer; Submit → answer received correctly.
7. Verify card history: after answer, card stays in stream with answers visible (read-only).

**Commit:**

```
chore(v2): F04 task 4.3 — main thread question card smoke validation

Validated AskUserQuestion flows:
- Single question + single-select → radio + Submit
- Multi-question (3) with mixed types → stacked, all required answered
  before Submit enables, multi-select platforms via checkboxes
- Free-text fallback → "Other" option + text input

Free-text detection heuristic (3.4) triggered correctly on Claude's
actual emitted "Other (specify)" options; no adjustment needed.

[Outcome: PASS — UX as designed]
[OR: outcome details + follow-ups]

Refs: 04-interactive-approvals-tasks.md (task 4.3)
```

---

### Task 4.4 — Smoke: subagent permission + question cards

**Size:** M
**Refs:** parent design doc Decision #5, spec "Subagent inheritance"

**Context:** Validate the SDK's documented behavior that subagents inherit `canUseTool` and built-in `AskUserQuestion`. Two smokes (permission + question), each with a subagent invocation. If they fail, fall back to the pre-thought options in Decision #5.

**Output:**

- Validation task; possibly small follow-up code if subagent inheritance is broken
- Document outcome and any fallback applied

**Validation flow:**

1. `pnpm tauri dev` → supervisor Ready, `default` permission mode.
2. **Smoke 3 (subagent permission):**
   - Prompt: "Use the unity-shader-specialist agent to find shaders in this project that use Light2D, and read 2 of their files for inspection."
   - Expected sequence: main thread invokes `Agent` (Task) tool with `subagent_type: "mcp-game-deck:unity-shader-specialist"` → subagent runs → subagent invokes `Read` tool on a shader file → `canUseTool` fires WITH `agentID: "mcp-game-deck:unity-shader-specialist"` → permission card appears with `from mcp-game-deck:unity-shader-specialist` in the header → click Allow → subagent receives Allow → continues → eventually returns summary to main thread.
   - **PASS:** card surfaces with agentId populated; click Allow continues subagent.
   - **FAIL:** no card appears; subagent stalls or times out. Fall back per Decision #5: add `Read, Glob, Grep` MCP tools to specialist's `tools:` frontmatter explicitly (these are built-ins; if the issue is built-in inheritance failing, this won't help — escalate to a different fallback). Try fallback (b): document limitation, ship without subagent support; main thread asks user, then delegates with answer.
3. **Smoke 4 (subagent question):**
   - Prompt: "Use the unity-shader-specialist agent. Tell it to ask me to clarify which URP version I'm using before suggesting any shader. Use AskUserQuestion."
   - Expected: subagent invoked → subagent calls `AskUserQuestion` → `canUseTool` fires with `toolName: "AskUserQuestion"` AND `agentID: "mcp-game-deck:unity-shader-specialist"` → question card appears → answer → subagent continues with the answer.
   - **PASS:** card surfaces with agentId; answer reaches subagent.
   - **FAIL:** Anthropic's subagent-MCP-fix doesn't extend to AskUserQuestion. Fall back: ship without subagent question support; main thread can still ask via `AskUserQuestion` and pass the answer down to the subagent invocation prompt manually.
4. Document the outcome in this tasks file row + the parent design doc Decision #5.

**Commit:**

```
chore(v2): F04 task 4.4 — subagent permission + question card smoke validation

Smoke 3 (subagent permission card): [PASS / FAIL details]
Smoke 4 (subagent question card): [PASS / FAIL details]

[If PASS:] Decision #5 holds — subagents inherit canUseTool and
AskUserQuestion as built-ins. Specialists in Plugin~/agents/ work
end-to-end with the new card UI without frontmatter changes.

[If FAIL:] Fallback applied — [details]. Limitation documented in
parent design doc Decision #5 and CLAUDE.md.

Refs: 04-interactive-approvals-tasks.md (task 4.4)
```

---

### Task 4.5 — Cleanup: delete F05 docs, update roadmap, update v2-features README

**Size:** S
**Refs:** parent design doc Decision #8

**Output:**

- **Delete (manual via PowerShell or VS Code):**
  ```powershell
  Remove-Item docs\internal\v2-features\05-permission-system-fix.md
  Remove-Item docs\internal\v2-features\05-permission-system-fix.md.meta
  Remove-Item docs\internal\v2-features\04-interactive-plan-mode.md
  Remove-Item docs\internal\v2-features\04-interactive-plan-mode.md.meta
  ```
- **Updated already** (during this feature's preparation):
  - `docs/internal/v2-features/04-interactive-approvals.md` (new design root, replaces 04-interactive-plan-mode.md)
  - `docs/internal/v2-features/04-interactive-approvals-spec.md` (new)
  - `docs/internal/v2-features/04-interactive-approvals-tasks.md` (new — this file)
  - `docs/internal/roadmap.md` (F05 row removed; F04 row updated)
  - `docs/internal/README.md` (v2-features listing updated)
- Verify after deletion:
  - `Get-ChildItem docs\internal\v2-features\05*` returns nothing
  - `Get-ChildItem docs\internal\v2-features\04-interactive-plan*` returns nothing
  - `Get-ChildItem docs\internal\v2-features\04-interactive-approvals*` returns the 3 expected files (md, -spec.md, -tasks.md)
  - Grep `permission-system-fix` returns zero matches across the repo (except possibly historical commit messages, which is fine)
  - Grep `interactive-plan-mode` returns zero matches across the repo

**Validation:**

1. Run the deletion commands; confirm 4 files gone.
2. Open `docs/internal/roadmap.md` — F05 row absent from v2.0 table; F04 row reads "Interactive Approvals & Clarifying Questions" with "✅ done" status.
3. Open `docs/internal/README.md` — v2-features listing reflects current files (no `02-orchestrator-agent.md` mentioned anymore from earlier ADR-001 cleanup; F04 listed as `04-interactive-approvals.md`; F05 not listed).
4. Open `04-interactive-approvals.md` — design root reads cleanly, banner notes the F04+F05 unification at the top.
5. Smoke: open Tauri via F07 pin, run a 5-turn conversation (mix of permission + question + tool calls + attachments), close cleanly. Repeat 3 times. No regressions, no zombie processes.

**Commit:**

```
chore(v2): F04 task 4.5 — F05 docs deleted; roadmap + README updated

Deleted superseded docs:
- 05-permission-system-fix.md (+ .meta) — F04 absorbs entirely
- 04-interactive-plan-mode.md (+ .meta) — replaced by 04-interactive-approvals.md

Updated:
- roadmap.md: removed F05 row from v2.0 table; renamed F04 to
  "Interactive Approvals & Clarifying Questions" with ✅ done
- docs/internal/README.md: updated v2-features listing

🎯 FEATURE 04 (INTERACTIVE APPROVALS & CLARIFYING QUESTIONS) FECHADA
INTEGRALMENTE — 17/17 TASKS DONE.

Default permission mode now usable in the Tauri app (was functionally
broken before). AskUserQuestion clarifying questions surface inline
in the chat with appropriate response-type UIs. Subagents inherit
both flows. F05 collapsed in.

Refs: 04-interactive-approvals-tasks.md (task 4.5)
```

---

17 tasks total. Estimated total time: 8–14h focused work depending on validation depth and unknowns hit during Group 3 (free-text detection heuristic) and Group 4 (subagent inheritance — Decision #5 fallback if smoke fails).

When all tasks ✅, F04 is feature-complete. v2.0 advances substantially — only F06 (Plans CRUD), F08 (Rules), F09 (Design Handoff) remaining.
