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

### KI-011 — Tauri binary has `package_root()` hardcoded at compile time

- **Priority:** P0 (blocks v2.0 release rehearsal — does not block today's dev flow)
- **Scope:** M
- **Status:** open
- **Discovered:** Ramon code-read during triage of Nicollas's bug, 2026-05-20

**Symptom.** Any machine running the Tauri binary **built on a different machine** (official release downloaded via the Unity pin) will have `MCP_PROXY_PATH` unset, MCP silently disabled, ToolSearch empty. Works "by accident" on Ramon's machine because the source lives in `C:\Projects\mcp-game-deck\` — the exact path baked into `CARGO_MANIFEST_DIR` at compile time.

**Diagnosis (confirmed in code).** [paths.rs:65-72](../../App~/src-tauri/src/claude_supervisor/paths.rs#L65) uses `env!("CARGO_MANIFEST_DIR")` to resolve `package_root()`. That macro is evaluated at **compile-time** and freezes an absolute literal into the binary (`C:\Projects\mcp-game-deck\App~\src-tauri` when built by Ramon). In [spawn.rs:30-33](../../App~/src-tauri/src/claude_supervisor/spawn.rs#L30), `mcp_proxy_script().is_file()` returns `false` on any machine where that path doesn't exist. The comment at [paths.rs:60-64](../../App~/src-tauri/src/claude_supervisor/paths.rs#L60) already documents the limitation:

> *"CARGO_MANIFEST_DIR is a compile-time anchor that points at the source tree even after Tauri bundles the binary into an MSI. Production builds need a different resolution (e.g., walking up from `current_exe()` or asset-side embedding). For dev/preview, this is correct."*

Same limitation hits `plugin_dir()` ([paths.rs:92-94](../../App~/src-tauri/src/claude_supervisor/paths.rs#L92)) — `Plugin~/` referenced by agents and skills.

**Fix direction.** Three approaches under discussion:

1. **Tauri resource bundle.** `mcp-proxy.js` and `Plugin~/` shipped as resources, resolved via `app.path().resource_dir()`. Cleanest long-term; idiomatic Tauri approach.
2. **`std::env::current_exe()`** + relative path. Simple but ties to the install layout.
3. **Env var injected via PinLauncher.** Add `MCP_GAME_DECK_PACKAGE_ROOT` to [PinLauncher.cs:221-238](../../Editor/Pin/PinLauncher.cs#L221) alongside the existing vars. Rust uses this env as primary anchor with `CARGO_MANIFEST_DIR` fallback for dev. Reuses the Unity → Tauri env channel already established.

#3 is the cheapest and unblocks release rehearsal immediately. #1 is the correct long-term destination. Decision pending when attacking this KI.

**Why P0 with caveat.** Doesn't block the current dev flow (everyone on the team clones the repo). Blocks v2.0 release rehearsal, which `roadmap.md:249` already lists as "immediate next". The first real install via official release will break for every external user.

---

### KI-012 — Tauri loses Unity connection after tool-triggered recompile (does not reconnect; pin stays green)

- **Priority:** P0 (does not auto-recover — full app restart required)
- **Scope:** S-M (needs instrumentation first)
- **Status:** open
- **Discovered:** Ramon, during KI-008 fix validation in `firepot-roulette`, 2026-05-20

**Symptom.** When a tool invocation triggers a C# recompile in Unity (creating/editing a script, etc. — not arbitrary assembly reload), Unity dutifully recompiles, **but after the reload Tauri flips its connection-status indicator to red and never reconnects**. The Unity-side pin status stays green — Unity itself reports the C# MCP server as healthy. Tauri's `unity-client` poll logs:

```
[unity-client] status → connected
...
[unity-client] status → disconnected
```

while nothing on the Unity side surfaces an error. Recovery requires restarting the Tauri app — Unity reload alone doesn't bring it back.

**Specificity:** the trigger is **tool-driven recompile** (a Claude tool wrote/edited a C# script), not user-initiated reloads. Manual Unity recompiles may or may not reproduce — not yet tested.

**Preliminary diagnosis.** Not yet investigated in code. The "doesn't reconnect" detail strongly suggests state cached past the reload boundary, not a transient bind-window issue. Possibilities:

- **Auth-token rotated post-reload.** Unity may regenerate the `auth-token` file at `<UNITY_PROJECT_PATH>/Library/GameDeck/` on assembly reload. Tauri reads the token once at supervisor spawn ([sdk_entry.js:977-990](../../App~/src-tauri/src/claude_supervisor/sdk_entry.js#L977)) and doesn't reload — every subsequent poll authed with the stale token would be rejected. **Most likely** given the "never reconnects" detail.
- **TCP listener / endpoint reference cached.** `unity-client` may hold a stale connection or endpoint object across the reload. Even if the C# server is back on the same port, the Rust poll never retries cleanly.
- **Hung HTTP connection.** Long-lived HTTP connection from poll cycle was open when Unity tore down the listener; the connection is now in a half-broken state that polls can't recover from.

**Investigation scope.**

- Add timestamped logging to `unity-client.rs` poll path: which step transitions to disconnected (TCP refused, HTTP 401, parse error, etc.).
- Read `Editor/MCP/Server/` C# side to see what `LockReloadAssemblies` actually does to the listener.
- Compare connection lifecycle across Unity reload boundaries.

**Fix direction.** Depends on which hypothesis lands:

- If auth-token rotated: re-read token on every poll cycle (cheap — local file), or invalidate cached token on first `disconnected` and retry.
- If state cache: explicit re-creation of HTTP client / connection state on `disconnected → connected` transition.
- If hung HTTP: set aggressive timeouts and tear down the client on each `disconnected` detection, recreating on next poll.

May correlate with [[KI-001a]] (long-session tool disappearance) — both point at the same "Tauri loses sight of Unity" surface. Investigating KI-012 may surface root cause of KI-001a as a side effect.

---

## Resolved

### KI-013 — Auto-resume race wipes the first user message and kills WorkingStrip

- **Priority:** P0
- **Scope:** XS (one effect, one guard)
- **Status:** resolved 2026-05-20
- **Discovered:** Ramon, during validation of [[KI-008]] fix, 2026-05-20
- **Fixed in:** [App~/src/components/SessionList.tsx](../../App~/src/components/SessionList.tsx) — guard added to the auto-resume `useEffect`.

**Symptom.** Surfaced immediately after the KI-008 fix landed. Sending the first message in a fresh app session caused: (a) the user's just-sent message to vanish from the chat, (b) the WorkingStrip to disappear mid-stream, and (c) Claude's reply to appear "out of nowhere" with no user prompt above it. The reply itself was correct — only the visual chat history was corrupted.

**Root cause.** Auto-resume race in [SessionList.tsx](../../App~/src/components/SessionList.tsx). On boot, `SessionList` mounts, fetches the session list async, and once it resolves auto-resumes the most-recent session (intentional UX — open the app, land in your last conversation). The effect only checked `currentSessionId === null` before resuming — it did not check whether a turn was already in flight or whether the user had already typed into a fresh chat.

Sequence when the user types quickly:

1. App opens, `currentSessionId = null`, `sessions = []`, `getSessions()` in flight.
2. User sends a message. `conversationStore.sendMessage` appends `userMsg` locally and sets `inFlight = true`. Supervisor receives it and begins streaming.
3. `getSessions()` resolves, populates `sessions`. The auto-resume `useEffect` fires.
4. `currentSessionId` is still `null` (no listener exists to set the id of the newly-created session during the first turn — see "Adjacent fragility" below), so the effect proceeds.
5. `handleResume(mostRecent.id)` calls `loadHistory(history)` → `set({ messages: history, inFlight: false })`. The user's local message is overwritten, and `inFlight: false` kills the WorkingStrip.
6. Incoming `text-delta` events for the in-flight turn hit the `appendDelta` fallback in [conversationStore.ts:167-172](../../App~/src/stores/conversationStore.ts#L167) (no message with that turnId exists anymore), which creates a fresh assistant message — explaining why the reply appears with no user prompt above it.

The bug was latent before the KI-008 fix because the listener teardown on `ChatRoute` unmount was hiding it (events dropped on the floor either way). Hoisting the listener exposed it.

**Resolution.** Added an early-return guard at the top of the auto-resume `useEffect`:

```tsx
const conversation = useConversationStore.getState();
if (conversation.inFlight || conversation.messages.length > 0)
{
  autoResumedRef.current = true;
  return;
}
```

If a turn is in flight or the user has already typed into a fresh chat, the auto-resume gives up (sets `autoResumedRef.current = true` so it won't try again later in the same boot). All other paths that call `loadHistory` (clicking a session row, deleting the active session, New chat) are user-initiated and intentional — left untouched.

**Adjacent fragility (not fixed, anchored for later).** No frontend listener exists for "session-created" emitted by the supervisor when the first turn opens a brand-new Claude session. `currentSessionId` therefore stays `null` for the entire duration of the first turn. The autoResumedRef guard prevents the race today, but if any future code path resets that ref while a turn is in flight, the bug returns. Proper fix is to wire `setCurrentSessionId(...)` from a `session-created` (or equivalent) agent-message variant inside [useConversationSubscription.ts](../../App~/src/hooks/useConversationSubscription.ts). Not scoped here to avoid bloat.

---

### KI-008 — Stream events lost during route navigation

- **Priority:** P0
- **Scope:** S
- **Status:** resolved 2026-05-20
- **Discovered:** Ramon observation during triage, May 2026
- **Fixed in:** [App~/src/hooks/useConversationSubscription.ts](../../App~/src/hooks/useConversationSubscription.ts) (new), [App~/src/App.tsx](../../App~/src/App.tsx), [App~/src/routes/ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx)

**Symptom (historical).** Loading indicator disappeared when the user navigated away from the Chat route while a turn was streaming. On return, chat showed inconsistent state — `inFlight` either stuck `true` (loader stuck) or `false` (loader gone but messages still streaming). Events emitted while the user was on Plans/Rules/Library/Settings were dropped: `text-delta` missing, `assistant-turn-complete` not flipping `inFlight`, `tool-use` blocks not appearing.

**Root cause.** The `onAgentMessage` listener was registered inside `ChatRoute`'s `useEffect` with cleanup `unlisten?.()`. Navigating to any other route unmounted `ChatRoute` and tore down the subscription; Tauri events do not buffer, so anything emitted while no listener existed fell on the floor.

**Resolution.** Extracted the listener into a new `useConversationSubscription` hook mounted at the root in `App.tsx` alongside the existing `usePlansSubscription` / `useRulesSubscription` / `useCatalogSubscription` hooks. The subscription now lives for the lifetime of the app, surviving every route navigation and re-render. `conversationStore` is global (zustand) so no other plumbing changed. `ChatRoute` no longer owns the subscription; it just renders `messages` and handles user input.

The hook uses `useConversationStore.getState()` inside the callback (not selector-bound actions), so the effect has zero deps and the listener registers exactly once. This also fixes the boot-time path of [[KI-010]]: `App.tsx` mounts before any route's `Outlet` renders, so the listener exists by the time the first supervisor `emit_agent_message` fires (a micro-race against the very first paint remains, but is no longer a practical concern).

**Side effects of the fix.** Subsumed [[KI-002]] (WorkingStrip vanishing after QuestionCard answer), [[KI-003]] (plan execution stalls silently), and the practical-case window of [[KI-010]] (boot-time supervisor errors lost).

---

### KI-002 — WorkingStrip disappears mid-stream after answering a QuestionCard

- **Priority:** P0
- **Scope:** S
- **Status:** resolved 2026-05-20 (subsumed by [[KI-008]])
- **Discovered:** Nicollas dogfood, May 2026

**Symptom (historical).** After the user answered a clarifying question (`AskUserQuestion`), the WorkingStrip loader disappeared even though Claude was still working. Chat appeared frozen until the next text delta arrived. Example mid-stream outputs with no visible loader:

> *"Quick targeted lookup before I write the plan — I need to understand `PhysicsRouletteController`…"*
> *"Now I have enough to write the plan. Let me draft it to the plan file."*

**Resolution.** Subsumed by [[KI-008]]. The frozen-loader symptom was the user-visible face of dropped `text-delta` and `assistant-turn-complete` events — same root cause as KI-008, fixed by the same hoist. Confirm in dogfood: WorkingStrip should now pulse continuously across QuestionCard answer → next delta.

---

### KI-003 — Plan execution hangs without feedback

- **Priority:** P0
- **Scope:** S
- **Status:** resolved 2026-05-20 (subsumed by [[KI-008]])
- **Discovered:** Nicollas dogfood, May 2026

**Symptom (historical).** When the user ran a plan, execution stalled silently — no loader, no error, no terminal output, nothing in the `claude` CLI process either.

**Resolution.** Subsumed by [[KI-008]]. Plan execution emits the same `text-delta` / `tool-use` / `assistant-turn-complete` event stream as a regular turn; if the listener was torn down between the user clicking Run Plan and the first delta arriving, the chat appeared stuck. Same root cause as KI-008, fixed by the same hoist. Confirm in dogfood: triggering plan execution should now produce visible WorkingStrip + streaming output end-to-end.

---

### KI-010 — Boot-time soft-warn errors from the supervisor are lost by React

- **Priority:** P1
- **Scope:** S
- **Status:** resolved 2026-05-20 (subsumed by [[KI-008]])
- **Discovered:** Ramon code-read during triage of Nicollas's "tools disappeared" report, 2026-05-20

**Symptom (historical).** When the Rust supervisor detected a boot-time problem (e.g. `Server~/dist/mcp-proxy.js` missing) and emitted `AgentMessage::Error` via `emit_agent_message`, the message never appeared in the React chat. App opened, chat worked for plain prompts, but any MCP-backed request silently failed.

**Resolution.** Subsumed by [[KI-008]]. The listener now lives in `App.tsx`, which mounts before any route's `Outlet` renders the `ChatRoute`. By the time the supervisor spawns its Node child and emits soft warnings, the listener is already attached. A theoretical micro-race remains between the very first Rust emit and React's first `useEffect`, but in practice the supervisor's spawn happens well after React hydration; if that ever surfaces, the documented mitigation is a Rust-side buffer flushed via a `ready()` Tauri command on App's first effect.

---

### KI-001a — MCP `game-deck` tools intermittently disappear in long sessions

- **Priority:** P0
- **Scope:** M
- **Status:** resolved 2026-05-20
- **Discovered:** Nicollas dogfood, May 2026
- **Confirmed by:** Ramon dogfood after KI-001b fix landed — tools no longer vanish across extended sessions.

**Symptom (historical).** All MCP Game Deck tools available immediately after spawn. After variable uptime they "disappeared": Claude no longer saw them, dropped to generic-Claude mode, and suggested the user open a terminal and run `claude` there. Restarting the Tauri app restored the tools.

**Resolution.** No standalone fix landed for (a) — the symptom stopped reproducing after the KI-001b fix (JSON-invalid `-Infinity` sentinel in the `tools/list` response) and the side-effect changes in [App~/runtime/sdk-entry.js](../../App~/runtime/sdk-entry.js) (forwarding `UNITY_MCP_AUTH_TOKEN`, `PROJECT_CWD`, and setting `alwaysLoad: true`). Hypothesis in retrospect: what looked like "tools vanishing after long uptime" was actually intermittent `tools/list` re-fetches by the CLI hitting the same parse failure as (b), just on a different cadence. With the schema now JSON-valid and `alwaysLoad: true` keeping descriptions resident, no re-fetch can lose them. Closing here; reopen as a fresh KI if the symptom returns.

---

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
