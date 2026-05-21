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

### KI-007 — No visible confirmation after Allow / Deny

- **Priority:** P1
- **Scope:** XS
- **Status:** open
- **Discovered:** Nicollas dogfood, May 2026

**Symptom.** After the user clicks Allow or Deny on a `PermissionRequestCard`, the card just dims (opacity). There's no positive confirmation that the action was registered — no checkmark for Allow, no X for Deny.

**Diagnosis.** Store updates `state: "answered"` with `outcome` correctly. The card receives both props. Likely the component only changes opacity and doesn't render an outcome icon.

**Fix direction.** In `PermissionRequestCard.tsx`, when `state === "answered"`, render a small ✓ (token `--ok`) for `allow` / `allow-always` and ✗ (token `--bad`) for `deny`, replacing the action buttons. Confirm component shape when attacking this KI.

---


## Resolved

### KI-011 — Tauri binary had `package_root()` + `runtime_dir()` hardcoded at compile time, and `mcp-proxy.js` was not shipped self-contained

- **Priority:** P0 (blocked v2.0 release rehearsal)
- **Scope:** M (grew during execution as two adjacent problems surfaced)
- **Status:** resolved 2026-05-21
- **Discovered:** Ramon code-read during triage of Nicollas's bug, 2026-05-20
- **Fixed in:** [App~/src-tauri/src/claude_supervisor/paths.rs](../../App~/src-tauri/src/claude_supervisor/paths.rs), [spawn.rs](../../App~/src-tauri/src/claude_supervisor/spawn.rs), [install_check.rs](../../App~/src-tauri/src/claude_supervisor/install_check.rs), [Editor/Pin/PinLauncher.cs](../../Editor/Pin/PinLauncher.cs), [Editor/Pin/PinPaths.cs](../../Editor/Pin/PinPaths.cs), [App~/package.json](../../App~/package.json), [App~/scripts/build-proxy.mjs](../../App~/scripts/build-proxy.mjs) (new), [App~/src-tauri/tauri.conf.json](../../App~/src-tauri/tauri.conf.json), [Server~/package.json](../../Server~/package.json), [.gitignore](../../.gitignore)

**Symptom (historical).** Any machine running the Tauri binary built on a different machine would have `MCP_PROXY_PATH` unset, MCP silently disabled, ToolSearch empty. Worked "by accident" on Ramon's machine because the source lived in `C:\Projects\mcp-game-deck\` — the exact path baked into `CARGO_MANIFEST_DIR` at compile time. First real install via official release would break for every external user.

**Diagnosis (confirmed in code).** `paths::package_root()` and `paths::runtime_dir()` both used `env!("CARGO_MANIFEST_DIR")` — a compile-time macro that freezes an absolute literal into the binary. `install_check.rs::detect_sdk_installed` had its own inline copy of the same calculation. Three downstream consumers — `mcp_proxy_script()`, `plugin_dir()`, `sdk_package_json()` — all silently broke once the binary moved off Ramon's source tree.

**Scope expansion during work.** While reading the code, two adjacent problems surfaced that any partial fix would have left as latent KI-012 / KI-013:

1. **`runtime_dir()` cannot live inside the UPM package tree.** When a user installs via UPM git or tarball, the package lives in `Library/PackageCache/com.mcp-game-deck@<hash>/` — read-only. `sdk_install`'s `npm install` would fail at first launch. Runtime needed to move to the OS user-data tree, versioned per package (mirroring the existing `<InstallRoot>/bin/<version>/` pattern).
2. **The Tauri binary cannot derive package_root via `current_exe()` walk-up.** The Tauri binary lives under `<InstallRoot>/bin/<version>/` — far away from the Unity package. Only Unity (via `PackageInfo.resolvedPath`) knows where the package actually is.

**Resolution — three concurrent fixes.**

1. **Env-var pin contract** ([PinLauncher.cs](../../Editor/Pin/PinLauncher.cs)). Two new env vars set by Unity before spawning the Tauri process: `MCP_GAME_DECK_PACKAGE_ROOT` (= `PackageInfo.resolvedPath`, works regardless of install method) and `MCP_GAME_DECK_RUNTIME_DIR` (= `<InstallRoot>/runtime/<version>/`, always writable, version-isolated). New helper `PinPaths.RuntimeFolder(version)` mirrors `BinFolder(version)`.

2. **Rust paths.rs reads env-var first, dev fallback second.** `package_root()` and `runtime_dir()` both read their respective env var; fall back to `CARGO_MANIFEST_DIR` walk-up (with `eprintln!` warning) only when running `cargo tauri dev` directly. `install_check.rs::detect_sdk_installed` adopts the `paths::sdk_package_json()` helper, killing the inline duplicate.

3. **`mcp_proxy_script` resolves via `BaseDirectory::Resource`, not via `package_root`.** The proxy is no longer derived from the Unity package path — it's a Tauri-bundled resource, declared in `tauri.conf.json` `bundle.resources`. New `App~/scripts/build-proxy.mjs` chained via `beforeBuildCommand` builds `Server~/` and stages the compiled proxy at `App~/src-tauri/proxy-bundle/mcp-proxy.js` for the Tauri bundler to pick up. `normalize_resource_path` strips the Windows `\\?\` extended-length prefix that Tauri's resolver returns (see [tauri-apps/tauri#5096](https://github.com/tauri-apps/tauri/issues/5096), closed not-planned), so the path can be passed cleanly to the Node child via `MCP_PROXY_PATH`.

**Surprise during smoke test (added to scope).** After the three fixes above landed and the NSIS installer was tested end-to-end, the proxy still failed: `ERR_MODULE_NOT_FOUND: Cannot find package '@modelcontextprotocol/sdk'`. Root cause: the proxy was shipped as a single `.js` file via Tauri Resource bundling, but its imports (`@modelcontextprotocol/sdk` and friends) live in `Server~/node_modules/` — which doesn't ship alongside the file. In v1.0 / dev mode this never surfaced because the proxy ran from `Server~/dist/mcp-proxy.js` where Node could walk up to `Server~/node_modules/`. Resolution: **esbuild bundles the proxy as a self-contained CJS file** (single 700KB output that inlines all JS deps). New `bundle:proxy` script in `Server~/package.json` runs `esbuild --bundle --platform=node --format=cjs --target=node20`. Format is CJS (not ESM) because the deployed file lives at a path with no parent `package.json` — Node defaults to CJS in that case, no `"type": "module"` shim needed alongside.

**Validated end-to-end on 2026-05-21.** Node-side env-state dump confirms `MCP_PROXY_PATH` resolves correctly, file exists at the resolved path, and the bundled proxy connects to Unity + responds to `initialize` with the real `serverInfo`. Chat in the Tauri app surfaces all `mcp__game-deck__*` tools.

**Sentinel decision (carried forward).** Whenever a similar binary-vs-package-path mismatch surfaces in future code, the established pattern is: PinLauncher passes the authoritative path via env var (Unity is the only source of truth for where the package lives), Rust reads the env var first with a `CARGO_MANIFEST_DIR` dev fallback that `eprintln!`s a warning when active.

---

### KI-006 — User avatar initials hardcoded to "RB"

- **Priority:** P1
- **Scope:** XS
- **Status:** resolved 2026-05-21
- **Discovered:** Nicollas dogfood, May 2026
- **Fixed in:** [App~/src-tauri/src/commands/env.rs](../../App~/src-tauri/src/commands/env.rs), [App~/src-tauri/src/lib.rs](../../App~/src-tauri/src/lib.rs), [App~/src/ipc/commands.ts](../../App~/src/ipc/commands.ts), [App~/src/utils/initials.ts](../../App~/src/utils/initials.ts) (new), [App~/src/hooks/useUserInitials.ts](../../App~/src/hooks/useUserInitials.ts) (new), [App~/src/routes/ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx)

**Symptom (historical).** Every user message showed "RB" as the avatar initials regardless of OS user — Ramon's initials baked into the JSX at [ChatRoute.tsx:282](../../App~/src/routes/ChatRoute.tsx#L282) as a literal string. Nicollas saw "RB" in his sessions.

**Resolution.** New Tauri command `get_os_username` returns `USERNAME` (Windows) or `USER` (Unix), filtering empty strings to `None`. Frontend wraps it via `getOsUsername()`, derives 2-letter initials with a new `deriveInitials` util (splits on whitespace / `.` / `_` / `-`: 2+ tokens → first letter of first + last, single token → first 2 chars, else `"??"`), and exposes the result through a `useUserInitials` hook with module-level caching — one Tauri roundtrip per app lifetime, shared across components. `ChatRoute` calls the hook once and passes the initials down to `MessageView` as a prop.

Skipped Win32 `GetUserNameExW` for the OS display name — the login-name env var is sufficient for the avatar use case and avoids platform-specific FFI for one cosmetic surface. Initial render shows `"??"` until the fetch resolves (sub-frame on Tauri's IPC), no layout shift since the Avatar box is fixed-size.

---

### KI-009 — Built-in commands don't render in the Tauri app

- **Priority:** P1
- **Scope:** S-M
- **Status:** resolved 2026-05-21
- **Discovered:** Ramon observation, May 2026
- **Fixed in:** [App~/src-tauri/src/claude_supervisor/sdk_entry.js](../../App~/src-tauri/src/claude_supervisor/sdk_entry.js), [App~/src-tauri/src/types.rs](../../App~/src-tauri/src/types.rs), [App~/src/ipc/types.ts](../../App~/src/ipc/types.ts), [App~/src/hooks/useConversationSubscription.ts](../../App~/src/hooks/useConversationSubscription.ts), [App~/src/stores/conversationStore.ts](../../App~/src/stores/conversationStore.ts), [App~/src/routes/ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx), [App~/src/components/chat/SystemMessageBlock.tsx](../../App~/src/components/chat/SystemMessageBlock.tsx) (new)

**Symptom (historical).** Built-in Claude Code commands like `/help` and `/cost` produced no visible output in the Tauri chat. The catalog recognized them (autocomplete listed them as `built-in`), but invoking them silently failed to render anything in the conversation.

**Root cause (confirmed via instrumented probe).** Added a temporary `[probe-ki009]` catch-all to `sdk-entry.js`'s `for await` loop and to `handleStreamEvent`, then exercised `/help`, `/cost`, `/clear`, and a Shift+Tab plan flow. The probe captured:

- **Synthetic CLI assistant messages** (`msg.type === "assistant"` with `model: "<synthetic>"`) — `/help` returned `"/help isn't available in this environment."` and `/cost` returned subscription info as a single text content block. Both were silently dropped because the discriminator only handled `system/init`, `stream_event`, `user`, and `result`.
- **`/clear` produces nothing** — consumed locally by the CLI, never reaches the SDK. Out of scope for this KI; would need frontend interception of the input string.
- **Stale `BUILTIN_COMMANDS` set** — listed 9 commands (`help`, `cost`, `permissions`, `agents`, `login`, `logout`, `model`, `status`, `exit`) that the SDK does NOT surface in `system/init.slash_commands`. Confirmed against SDK 2.1.126: only `clear`, `compact`, `context`, `heapdump`, `init`, `review`, `security-review`, `extra-usage`, `usage`, `insights`, `team-onboarding` are real built-ins in the SDK environment.

**Bonus discovery (also addressed).** The probe revealed `system/task_started`, `system/task_progress`, and `system/task_notification` events — rich subagent telemetry (per-step description, cumulative tokens / tool count / duration, completion summary) that was completely invisible to the user. A Task subagent could fire 20+ progress events during a single planning turn and the chat would show only "Finished Agent" at the end. Same probe → same fix cycle.

**Resolution.** Four new wire envelopes routed end-to-end (supervisor → Rust enum → TS mirror → store mutator → React component):

1. `system-message` — synthetic CLI text rendered as a terminal-style block ([`SystemMessageBlock`](../../App~/src/components/chat/SystemMessageBlock.tsx)).
2. `subagent-status` — Task telemetry rendered as a live mini-panel ([`SubagentStatusPanel`](../../App~/src/components/chat/SubagentStatusPanel.tsx)), upserted by `taskId` so progress events update one block in place.
3. `usage-update` — forwarded from `result.usage` for the new context-usage ring in the HUD ([`ContextRing`](../../App~/src/components/shell/ContextRing.tsx)).
4. `plan-summary` — see [[KI-004]] (same patch landed both).

Stale `BUILTIN_COMMANDS` entries trimmed. The 3 `[probe-ki009]` catch-alls were removed after the fix landed.

---

### KI-005 — Markdown tables not rendered (root cause: assistant chat text wasn't going through `react-markdown` at all)

- **Priority:** P1
- **Scope:** XS
- **Status:** resolved 2026-05-21
- **Discovered:** Nicollas dogfood, May 2026
- **Fixed in:** [App~/package.json](../../App~/package.json), [App~/src/components/requests/markdown-renderers.tsx](../../App~/src/components/requests/markdown-renderers.tsx), [App~/src/routes/ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx), [App~/src/components/PlanViewer.tsx](../../App~/src/components/PlanViewer.tsx), [App~/src/components/RuleViewer.tsx](../../App~/src/components/RuleViewer.tsx), [App~/src/components/library/KnowledgeReader.tsx](../../App~/src/components/library/KnowledgeReader.tsx), [App~/src/components/chat/SystemMessageBlock.tsx](../../App~/src/components/chat/SystemMessageBlock.tsx), [App~/src/components/requests/PlanSummaryCard.tsx](../../App~/src/components/requests/PlanSummaryCard.tsx)

**Symptom (historical).** Markdown tables in assistant messages rendered as literal pipe-separated text instead of styled tables. Strikethrough, task-lists and other GFM features would also have shown as literals — only tables were loud enough for the dogfooder to flag.

**Root cause (corrected diagnosis).** The KI's preliminary note ("`markdown-renderers.tsx` missing `remark-gfm`") was incomplete. Two separate gaps compounded:

1. **Assistant chat text never passed through `react-markdown` at all.** [ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx) rendered the `text` block of an assistant message as plain text inside a `<div className="whitespace-pre-wrap">`. Tables showed as pipes because no markdown was being rendered — full stop. `**bold**`, `## headings`, `- lists` would have shown as literals too if a user had thought to check.
2. **The 5 callsites that *did* use `react-markdown`** (PlanViewer, RuleViewer, KnowledgeReader, SystemMessageBlock, PlanSummaryCard) did not pass `remarkPlugins`, so GFM features were off even where markdown rendering existed.

**Resolution.** Added `remark-gfm` as a dependency. Wrapped the assistant text block in [ChatRoute.tsx](../../App~/src/routes/ChatRoute.tsx) in `<ReactMarkdown remarkPlugins={[remarkGfm]} components={markdownRenderers}>` and removed the now-conflicting `whitespace-pre-wrap` from the wrapper (react-markdown's `<p>` controls spacing). Added `remarkPlugins={[remarkGfm]}` to all 5 existing callsites. Extended both renderer maps (the shared [markdownRenderers](../../App~/src/components/requests/markdown-renderers.tsx) and KnowledgeReader's own [buildRenderers](../../App~/src/components/library/KnowledgeReader.tsx)) with `table` / `thead` / `tbody` / `tr` / `th` / `td` / `del` overrides — slate tokens in the shared map, `txt-*` / `bg-*` / `line-*` tokens in the reader (which also wraps cell content with its search-highlight helper).

**Lesson.** A KI's "preliminary diagnosis" pointing at a single file needs to be confirmed end-to-end. The renderer module was a real gap, but the louder bug was that the chat surface had no markdown rendering at all — indistinguishable from the reporter's perspective ("tables aren't styled" vs "tables aren't rendered" both show pipes).

---

### KI-004 — ExitPlanMode renders as a generic Allow card

- **Priority:** P1
- **Scope:** S
- **Status:** resolved 2026-05-21
- **Discovered:** Nicollas dogfood, May 2026
- **Fixed in:** [App~/src-tauri/src/claude_supervisor/sdk_entry.js](../../App~/src-tauri/src/claude_supervisor/sdk_entry.js) — new `ExitPlanMode` branch in `canUseToolCallback`. [App~/src/components/requests/PlanSummaryCard.tsx](../../App~/src/components/requests/PlanSummaryCard.tsx) (new) — dedicated Accept / Reject card.

**Symptom (historical).** When the user entered Claude Code's native plan mode (Shift+Tab) and Claude finished drafting the plan, the SDK fired the built-in `ExitPlanMode` tool. The Tauri app rendered it as a regular permission request card ("Claude wants to use ExitPlanMode") instead of showing the plan summary with explicit Accept / Reject — the actual plan body was hidden behind a "View raw inputs" accordion.

**Resolution.** Landed together with [[KI-009]] in one patch cycle. `canUseToolCallback` in `sdk-entry.js` now branches on `toolName === "ExitPlanMode"` before falling through to the generic permission path: it emits a new `plan-summary` envelope carrying the markdown plan, then awaits the user's decision via the same `respond-to-request` plumbing already used for permission cards. `PlanSummaryCard` renders the plan markdown as the primary content with explicit Accept / Reject buttons (no `allow-always` — every plan is unique, caching the decision makes no sense). React routes `allow` to the supervisor as `behavior: "allow"` (Claude starts coding) and `deny` to `behavior: "deny"` with a rejection message (Claude replies but does not implement).

The wire path reuses the existing permission outcome plumbing so the round-trip is symmetric with `PermissionRequestCard` — no new IPC surface was needed.

---



- **Priority:** P0
- **Scope:** L (architectural)
- **Status:** resolved 2026-05-21 (closed without in-process fix; escalated to [[37-mcp-server-out-of-process]])
- **Discovered:** Ramon, during KI-008 fix validation in `firepot-roulette`, 2026-05-20

**Symptom (historical).** After an assembly reload in Unity (typically tool-triggered: Claude edits a .cs file → Unity recompiles), Tauri's `unity-client` flips to disconnected and **never reconnects**. The Unity-side pin status stays green (Unity considers the MCP server healthy). Restarting Tauri does not help. Recovery requires restarting Unity itself (full process kill).

**Root cause (confirmed via netstat + C# lifecycle instrumentation).** After a reload, `netstat -ano | findstr :8090` shows **two LISTEN sockets coexisting** on the same Unity PID, both bound to `127.0.0.1:8090` via `SO_REUSEADDR`. The old listener is an orphan — no `AcceptLoop` thread reading from it. New TCP connects from Tauri are routed non-deterministically by the Windows kernel; on the affected machine they consistently land on the orphan, where they sit in the accept queue forever and Tauri times out.

The orphan exists because `.NET`'s `SafeHandle` keeps a refcount from in-flight overlapped accept I/O. `listener.Stop()` schedules `closesocket()` but defers the actual OS-level release until the IOCP cancellation unwinds. On Ramon's machine (Win11 Pro 25H2, build 26200.8457), the unwind takes long enough that the new AppDomain's `StartServer` binds a fresh listener before the old socket is released. With `SO_REUSEADDR`, both binds succeed and the orphan persists.

**Machine specificity confirmed.** A second developer on the same project + same Windows version did not reproduce. Difference is timing — the race window in the IOCP unwind is wider on Ramon's hardware. Not fixable from managed C#.

**Investigation summary** (all attempts reverted, see git log of `Editor/MCP/Server/McpServer.cs` and `App~/src-tauri/src/unity_client/`):

- Removed `SO_REUSEADDR` + retry on `EADDRINUSE` with 10×200ms backoff: orphan persisted >2s on affected machine, all retries failed.
- Async accept loop with `CancellationToken` + bounded `task.Wait`: lifecycle ran cleanly per `[KI012]` Info logs (drain confirmed, task completed), orphan still appeared.
- Tracked accepted clients in `ConcurrentDictionary`, force-closed all on `StopServer`: drain confirmed, orphan still appeared.
- HTTP keep-alive in Tauri client to stick to the live listener after first connect: first connect still routed to orphan on affected machine.
- Diagnostic timestamp instrumentation: ThreadPool dispatch was fast (1-2ms) and `activeHandlers` count never exceeded 4. Disproved earlier "ThreadPool starvation" hypothesis.

**Resolution.** Closed without in-process fix. The architectural problem (in-process TCP server in a runtime that reloads its AppDomain) is the actual root cause; the orphan listener is one symptom among several. Even on machines where the orphan doesn't accumulate, every Unity recompile causes brief disconnections, in-flight tool call abandonment, and proxy reconnect overhead (~157KB `tools/list` re-fetch).

Properly tracked by **Feature 37 — MCP Server Out-of-Process** ([37-mcp-server-out-of-process.md](v2-features/37-mcp-server-out-of-process.md)). F37 introduces a sidecar process that owns the public :8090 listener and survives Unity reloads, bridging requests to Unity over reload-tolerant IPC. External clients never see the reload churn.

**Workaround until F37 ships.** Restart Unity Editor (full process kill, not just script reload) when the connection gets stuck. Restarting Tauri alone does not help — the orphan socket lives in Unity's process and persists until that process exits.

---



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
