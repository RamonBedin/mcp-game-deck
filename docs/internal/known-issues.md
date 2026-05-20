# Known Issues

Internal tracker for bugs and small UX debt in MCP Game Deck. Used in place of GitHub Issues — keeps tracking versioned in the repo and visible alongside the code.

Entries are append-only by ID. When an issue is resolved, move its entry from **Open** to **Resolved** (don't delete). Resolved entries stay useful as historical context.

## Conventions

- **ID:** `KI-NNN`, monotonically increasing, three digits.
- **Priority:** `P0` (breaks core flow), `P1` (degrades UX, workaround possible), `P2` (polish / debt).
- **Scope:** rough effort estimate — `XS` / `S` / `M` / `L`.
- **Status:** `open` / `investigating` / `fix-in-progress` / `resolved`.

---

## Open

### KI-001 — MCP `game-deck` tools intermittently disappear in long sessions

- **Priority:** P0
- **Scope:** M (only variant a remaining)
- **Status:** open (variant a only; variant b resolved 2026-05-19)
- **Discovered:** Nicollas dogfood, May 2026

**Symptom (variant a, only one still open).** All MCP Game Deck tools available immediately after spawn — Claude lists them in its catalog and calls them successfully. After variable uptime they "disappear": Claude no longer sees them, drops to generic-Claude mode, explicitly suggests the user open a terminal and run `claude` there. Restarting the Tauri app restores the tools.

**Not yet diagnosed.** Variant (b) — "never connects on home PC" — turned out to be a different bug entirely (see Resolved section below for KI-001b). Whether the fix for (b) accidentally also fixes (a) is unknown until Nicollas confirms with extended dogfood; the failure mode of (a) (tools appear then vanish) is distinct from (b) (tools never appear).

**Surviving hypotheses for (a).**

1. Proxy STDIO pipe drops mid-session (assembly reload edges, transient TCP issue on the proxy → Unity hop) and the CLI does not reconnect.
2. KI-008 (lost listener on route navigation) masking what looks like tool disappearance — when the listener is dead, no `tool-use` envelopes are emitted even though the server is fine.
3. CLI-side context window pressure — tool descriptions evicted from prompt as conversation grows (mitigated by `alwaysLoad: true` we now pass, but unconfirmed).

**Fix direction.** Re-evaluate after KI-008 lands. If degradation persists, instrument `Server~/src/mcp-proxy.ts` lifecycle (stdio close, TCP reconnect — the file-log diagnostic pattern used in the KI-001b investigation works well, see the Resolved entry for the approach).

---

### KI-002 — WorkingStrip disappears mid-stream after answering a QuestionCard

- **Priority:** P0
- **Scope:** S (likely subsumed by KI-008)
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** After the user answers a clarifying question (`AskUserQuestion`), the WorkingStrip loader disappears even though Claude is still working. The chat appears frozen until the next text delta arrives — sometimes seconds, sometimes longer. Example outputs observed mid-streaming with no visible loading:

> *"Quick targeted lookup before I write the plan — I need to understand `PhysicsRouletteController`…"*
> *"Now I have enough to write the plan. Let me draft it to the plan file."*

**Preliminary diagnosis.** Likely tied to KI-008: the `onAgentMessage` listener lives inside `ChatRoute`'s `useEffect`. If the user navigates away (or the chat re-renders for any reason) between answering the question and the next delta, events queued in the meantime never reach the store. Alternative path: the SDK `query()` stream ends without emitting a `result` message (claude binary crashes, pipe closes), so `assistant-turn-complete` is never dispatched and `inFlight` stays stuck.

**Open question for tester.** Did the WorkingStrip *disappear from the DOM*, or did it remain visible but without the pulse animation? Different bugs.

**Fix direction.** Revisit after KI-008 lands.

---

### KI-003 — Plan execution hangs without feedback

- **Priority:** P0
- **Scope:** S (likely subsumed by KI-008 / KI-002)
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** When the user runs a plan, execution stalls silently — no loader, no error, no terminal output. Nothing in the underlying `claude` CLI process either.

**Preliminary diagnosis.** Likely same root as KI-002 — listener loss or stream-without-result. Verify together once KI-008 is fixed.

---

### KI-004 — ExitPlanMode renders as a generic Allow card

- **Priority:** P1
- **Scope:** S
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** When the user enters Claude Code's native plan mode (Shift+Tab) and Claude finishes drafting the plan, the SDK fires the built-in `ExitPlanMode` tool. The Tauri app renders it as a regular permission request card ("Claude wants to use ExitPlanMode") instead of showing the plan summary with explicit Accept / Reject.

**Diagnosis (confirmed in code).** `canUseToolCallback` in `App~/runtime/sdk-entry.js` only special-cases `AskUserQuestion`. Every other tool — including `ExitPlanMode` — falls through to the generic `emitPermissionRequested` path.

**Fix direction.** Add a branch for `ExitPlanMode`: either emit a dedicated `plan-summary` envelope and render with Accept / Reject in the chat, or auto-allow and render the `plan` field from the tool input as a markdown block inline. Decision needed when attacking this KI.

---

### KI-005 — Markdown tables not rendered

- **Priority:** P1
- **Scope:** XS
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** Markdown tables in assistant messages render as literal pipe-separated text instead of a styled table.

**Preliminary diagnosis.** `App~/src/components/requests/markdown-renderers.tsx` likely uses `react-markdown` without `remark-gfm` (GitHub-Flavored Markdown). Tables, strikethrough, task-lists, and autolinks are GFM features.

**Fix direction.** Add `remark-gfm` to the renderer's `remarkPlugins`. Confirm by reading the file before fixing.

---

### KI-006 — User avatar initials hardcoded to "RB"

- **Priority:** P1
- **Scope:** XS
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** Every user message shows "RB" as the avatar initials regardless of OS user. Nicollas sees "RB" in his sessions — Ramon's initials baked into the JSX.

**Diagnosis (confirmed in code).** `App~/src/routes/ChatRoute.tsx` MessageView for user role:

```tsx
<Avatar variant="user" initials="RB" size={28} />
```

Literal string hardcoded.

**Fix direction.** Add a Tauri command that returns the OS user's display name (or fall back to `USERNAME` / `USER` env), generate initials from first + last token, expose via a small hook or a one-shot at app boot stored in `settingsStore`. Fallback to first two chars of the username if the name has only one token.

---

### KI-007 — No visible confirmation after Allow / Deny

- **Priority:** P1
- **Scope:** XS
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** After the user clicks Allow or Deny on a `PermissionRequestCard`, the card just dims (opacity). There's no positive confirmation that the action was registered — no checkmark for Allow, no X for Deny.

**Diagnosis.** Store updates `state: "answered"` with `outcome` correctly. The card receives both props. Likely the component only changes opacity and doesn't render an outcome icon.

**Fix direction.** In `PermissionRequestCard.tsx`, when `state === "answered"`, render a small ✓ (token `--ok`) for `allow` / `allow-always` and ✗ (token `--bad`) for `deny`, replacing the action buttons. Confirm component shape when attacking this KI.

---

### KI-008 — Stream events lost during route navigation

- **Priority:** P0
- **Scope:** S
- **Status:** open
- **Discovered:** Ramon observation during triage, May 2026

**Symptom.** Loading indicator disappears when the user navigates away from the Chat route while a turn is streaming, or when they trigger a session refresh path. When they return, the chat shows inconsistent state — `inFlight` may still be `true` (loader stuck) or `false` (loader gone but messages still streaming).

The "refresh kills loading" report likely conflates two paths: the `↻` button alone is benign (only re-fetches the session list), but **New chat** (`clearMessages()` → `inFlight: false`) and clicking another session (`loadHistory()` → `inFlight: false`) both force-reset the flag even when a turn is still in flight.

**Diagnosis (confirmed in code).** The `onAgentMessage` listener is registered inside `ChatRoute`'s `useEffect` with cleanup `unlisten?.()`. Navigating to any other route (Plans, Rules, Library, Settings) unmounts `ChatRoute` and the cleanup unsubscribes — events emitted while the user is elsewhere are dropped on the floor. This explains:

- `text-delta` not appearing in store while user is on another tab
- `assistant-turn-complete` not flipping `inFlight` to `false`
- `tool-use` blocks missing on return

**Fix direction.** Hoist the listener to `App.tsx` (or a dedicated `ConversationListenerProvider` mounted once at root) so it survives route navigation. The store is already global; only the subscription needs to live longer.

This is the architectural fix that likely subsumes KI-002 and KI-003.

---

### KI-009 — Built-in commands don't render in the Tauri app

- **Priority:** P1
- **Scope:** S-M
- **Status:** open
- **Discovered:** Ramon observation, May 2026

**Symptom.** Built-in Claude Code commands like `/help`, `/clear`, `/cost`, `/permissions`, `/agents`, `/status`, etc. don't produce visible output in the Tauri chat. The catalog recognizes them (autocomplete lists them as `built-in`), but invoking them silently fails to render anything in the conversation.

**Preliminary diagnosis.** Not yet investigated in code. Possibilities:

- Built-in commands return special SDK message types that `sdk-entry.js` doesn't translate (the main `for await` loop in `handleInput` only handles `system/init`, `stream_event`, `user`, and `result` — any other shape is silently ignored).
- The output is emitted as a content block the frontend doesn't know how to render.
- The CLI handles the command locally and doesn't surface anything to the SDK.

**Investigation scope.**

- Trace what message types appear when `/help`, `/cost`, `/clear` are sent through `query()`.
- Compare against the discriminator in `sdk-entry.js`'s main `for await (const msg of q)` loop — any unhandled `msg.type` is a candidate.
- Check whether the SDK has a `command-result` or similar envelope not currently parsed.

**Fix direction.** Add handling for the missing message types in `sdk-entry.js` and emit a corresponding envelope to the frontend; add a render case in `ChatRoute` for the new envelope. Likely a new `system-message` or `command-output` envelope with markdown body.

---

## Resolved

### KI-001b — `game-deck` MCP server "connected" but exposes zero tools (root cause: JSON-invalid float sentinel)

- **Priority:** P0
- **Scope:** S (1-method fix)
- **Status:** resolved 2026-05-19
- **Discovered:** Ramon home-PC investigation, 2026-05-19
- **Fixed in:** [Editor/MCP/Utils/JsonHelper.cs](../../Editor/MCP/Utils/JsonHelper.cs) — new `IsValidJsonDefault` guard in `AppendInputSchema`

**Symptom.** On Ramon's home PC the `game-deck` MCP server showed `status: "connected"` in the SDK's `system/init.mcp_servers` list, but **zero tools were enumerated** — `init.tools[]` had no `mcp__game-deck__*` entries despite the C# server hosting 274 tools. The LLM in the chat said it didn't have any `scene-*`/`prefab-*` tools, only the `mcp-game-deck:*` skills from the bundled plugin.

**Root cause.** [Tool_Prefab.ModifyContents.cs:61](../../Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs#L61) declared a parameter `float fieldValueFloat = float.NegativeInfinity` as a "not provided" sentinel. The C# JSON serializer in `JsonHelper.AppendDefaultValue` happily wrote that as `"default":-Infinity` — a **JSON-invalid token** (RFC 8259 §6 admits only finite numeric literals).

The proxy → CLI path then unfolded like this:

1. `claude` CLI called `tools/list` on the proxy.
2. Proxy fetched the response from the C# server (274 tools, ~157KB JSON).
3. MCP SDK's strict JSON parser rejected the response at byte ~100905 with `"No number after minus sign in JSON"`.
4. CLI received an error for `tools/list`, but the **`initialize` handshake had already succeeded** — so the server status stayed `connected` rather than flipping to `failed`.
5. CLI proceeded with **zero tools** registered for that server. No surface error to the user.

Why direct POST tests passed: `Invoke-RestMethod` (and PowerShell's JSON parser in general) tolerates `-Infinity`/`Infinity`/`NaN`. The MCP SDK uses a strict parser. Manual probing always succeeded, masking the bug.

**Fix.** Skip the `"default":` field in the JSON schema when the default value is not representable as a JSON number (NaN or ±Infinity). The C# code that consumes the sentinel (`float.IsNegativeInfinity(fieldValueFloat)` checks) is unchanged — the sentinel still means "not provided" at the C# layer; only the schema-published default goes silent. The parameter description already documents the convention to the LLM.

**Side effects of the investigation (also landed, kept):**

- `App~/runtime/sdk-entry.js` (and source `App~/src-tauri/src/claude_supervisor/sdk_entry.js`) — `buildMcpServers()` now (a) reads `auth-token` from `<UNITY_PROJECT_PATH>/Library/GameDeck/` and forwards it via `UNITY_MCP_AUTH_TOKEN`, (b) forwards `PROJECT_CWD` so the proxy's filesystem fallback resolves correctly, and (c) sets `alwaysLoad: true` on the MCP config to keep tools out of `ToolSearch` deferral.
- All debug instrumentation added during diagnosis was reverted after the fix landed.

**Lessons for future audits.** See the "JSON-invalid sentinels" note in `.claude/agents/tool-auditor.md`.
