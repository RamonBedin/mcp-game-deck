# Feature 01 — External App (Tauri) — Tasks

> **Companion:** `01-external-app-spec.md` for design rationale.
> **Execution model:** Ramon invokes Claude Code with `Execute task X.Y from this file`. Claude Code reads ONLY the named task block, executes it, reports done. Ramon reviews diff in VS Code, commits, moves to next task.

## How to read this file

Each task has:

- **ID** — `N.M` (group.task)
- **Title** — short imperative
- **Pre-req** — task IDs that must be done first
- **Output** — files created/modified, what's expected to work after
- **Done when** — explicit acceptance criteria
- **Size** — S (≤30min focused work), M (~1h), L (~2-3h)
- **Notes** — technical pointers, gotchas

Tasks are designed atomic: one task = one Claude Code session = one commit (or one logical group of commits) by Ramon.

If a task gets too big mid-execution, Claude Code should stop and report "task is bigger than estimated, recommend splitting" — Ramon decides whether to continue or split.

---

## Group 1 — Scaffolding (foundations)

### 1.1 — Initialize App~/ with Tauri + React + Vite

**Pre-req:** none
**Size:** M
**Output:**
- `App~/package.json`, `App~/pnpm-lock.yaml` (or `package-lock.json` if Ramon prefers npm)
- `App~/vite.config.ts`, `App~/tsconfig.json`, `App~/index.html`
- `App~/src/main.tsx`, `App~/src/App.tsx` (placeholder content)
- `App~/src-tauri/Cargo.toml`, `App~/src-tauri/tauri.conf.json`, `App~/src-tauri/build.rs`
- `App~/src-tauri/src/main.rs`, `App~/src-tauri/src/lib.rs` (minimal `tauri::Builder` running with no commands yet)
- `App~/src-tauri/icons/` placeholder icons (Tauri default icons OK)
- `App~/.gitignore` (ignores `target/`, `dist/`, `node_modules/`)

**Done when:**
- `cd App~ && pnpm install` succeeds
- `cd App~ && pnpm tauri dev` opens an empty window titled "MCP Game Deck"
- Window can be closed cleanly
- No errors in dev console

**Notes:**
- Use Tauri 2.x (not 1.x). Use `pnpm` or `npm`, Ramon's preference.
- Tauri config:
  - Window title: "MCP Game Deck"
  - Default size: 1100x720
  - Min size: 800x600
  - Resizable: true
  - DevTools enabled in dev, disabled in release
- Don't add commands yet — that's later tasks. `lib.rs` should call `tauri::Builder::default().run(tauri::generate_context!())` with no `.invoke_handler()` chain.
- The `App~/.gitignore` is for the App folder specifically. The repo's main `.gitignore` already excludes generic things; this one covers Rust `target/`, Vite `dist/`, `node_modules/`.

---

### 1.2 — Add Tailwind + base styling

**Pre-req:** 1.1
**Size:** S
**Output:**
- `App~/tailwind.config.js`, `App~/postcss.config.js`
- `App~/src/styles/globals.css` (Tailwind directives + dark mode base)
- `App~/src/App.tsx` updated to use Tailwind classes (basic dark theme, full-height layout)

**Done when:**
- `pnpm tauri dev` shows window with dark background applied via Tailwind
- A Tailwind class like `text-blue-500` renders correctly somewhere visible (verify class works)
- No runtime errors

**Notes:**
- Tailwind 3.x (not 4.x — 4 has different config, stick to 3 for stability)
- Dark mode: use `class` strategy (toggleable later) but apply `dark` class to `<html>` by default
- Color palette: stay neutral for now (slate / zinc / gray). Brand colors come later.
- Don't import from `@tailwindcss/forms` or other plugins yet — keep deps minimal

---

### 1.3 — Add React Router with empty routes

**Pre-req:** 1.2
**Size:** S
**Output:**
- `App~/src/routes/ChatRoute.tsx` (h1 "Chat", placeholder content)
- `App~/src/routes/PlansRoute.tsx` (placeholder)
- `App~/src/routes/RulesRoute.tsx` (placeholder)
- `App~/src/routes/SettingsRoute.tsx` (placeholder)
- `App~/src/App.tsx` updated with sidebar nav + `<Outlet />` for routes
- `App~/src/main.tsx` updated to wrap App in `<BrowserRouter>` (or `<MemoryRouter>` — see Notes)

**Done when:**
- Window shows sidebar with 4 tabs: Chat, Plans, Rules, Settings
- Clicking each tab swaps the main area content
- Default route is Chat
- No errors

**Notes:**
- Use **`MemoryRouter`** (not BrowserRouter). Tauri windows don't have meaningful URLs and `BrowserRouter` can interact weirdly with the WebView. Memory router is safer.
- Sidebar layout: fixed-width (~200px) on the left, route content fills remaining space.
- Use `react-router-dom` v6 syntax (`<Routes>`, `<Route>`, `<Outlet>`).
- Sidebar items: text-only for now, no icons (icons come later when we have a visual direction).

---

### 1.4 — Set up Zustand stores (empty shells)

**Pre-req:** 1.3
**Size:** S
**Output:**
- `App~/src/stores/connectionStore.ts` — state shape: `{ unityStatus, nodeSdkStatus }`, no real data yet
- `App~/src/stores/conversationStore.ts` — state shape: `{ messages, currentSessionId, permissionMode }`
- `App~/src/stores/plansStore.ts` — `{ plans, currentPlan }`
- `App~/src/stores/rulesStore.ts` — `{ rules }`
- `App~/src/stores/settingsStore.ts` — `{ settings }`

Each store exports a `useXxxStore` hook with placeholder initial state and a couple of dummy setter actions.

**Done when:**
- Stores compile, export proper TypeScript types
- A test usage in `SettingsRoute.tsx` reads from `useSettingsStore` and renders a value (e.g. a default theme name)
- No errors at runtime

**Notes:**
- Use Zustand v4 (`create((set) => ({ ... }))` pattern with TypeScript generic)
- Don't wire to Tauri yet — that's Group 2
- Type each store explicitly (no `any`). Use `interface` for state shape.
- Don't add persistence middleware yet — settings go through Tauri commands later anyway

---

## Group 2 — Tauri command + event plumbing (no real backend yet, stubs)

### 2.1 — Define shared types in src/ipc/types.ts

**Pre-req:** 1.4
**Size:** S
**Output:**
- `App~/src/ipc/types.ts` with all types referenced in spec's "Tauri commands" and "Tauri events" sections:
  - `ConnectionStatus` (`"connected" | "busy" | "disconnected"`)
  - `PermissionMode` (`"auto" | "ask" | "plan"`)
  - `Message` (id, role, content, timestamp, agent?)
  - `MessageId` (string)
  - `PlanMeta` (name, lastModified)
  - `Plan` (PlanMeta + content + frontmatter)
  - `RuleMeta` (name, enabled)
  - `Rule` (RuleMeta + content)
  - `AppSettings`, `AppSettingsPatch`
  - `AppError` (kind: enum, message: string)

**Done when:**
- File compiles, no `any`, all types exported
- Match the spec exactly — these are the contract

**Notes:**
- Keep types **strict** — every field needed, none extra
- Use string literal unions for enums (TS idiomatic), not actual `enum`
- AppError kind list: `"unity_disconnected"`, `"node_sdk_unavailable"`, `"file_not_found"`, `"permission_denied"`, `"invalid_input"`, `"internal"` for now

---

### 2.2 — Define Rust types matching src/ipc/types.ts

**Pre-req:** 2.1
**Size:** S
**Output:**
- `App~/src-tauri/src/types.rs` with Rust equivalents (using `serde::{Serialize, Deserialize}`)
- All types annotated with `#[derive(Debug, Clone, Serialize, Deserialize)]`
- String enums use `#[serde(rename_all = "kebab-case")]` or explicit `#[serde(rename = "...")]` to match TS literal strings exactly

**Done when:**
- `cargo check` passes
- A round-trip test (manually verify): if Rust serializes a `ConnectionStatus::Connected`, the JSON output matches what TS expects in `ConnectionStatus`

**Notes:**
- Use `serde_json` already pulled by Tauri, no new dep needed
- For `AppError`, use a tagged enum: `#[serde(tag = "kind", content = "message")]` or similar — TS side gets `{ kind: "...", message: "..." }`
- Run `cargo fmt` after writing

---

### 2.3 — Implement stub Tauri commands (return fake data)

**Pre-req:** 2.2
**Size:** M
**Output:**
- `App~/src-tauri/src/commands/mod.rs` — re-exports
- `App~/src-tauri/src/commands/connection.rs` — `get_unity_status`, `get_node_sdk_status`, `reconnect_unity`, `restart_node_sdk` (all return hardcoded fake values for now)
- `App~/src-tauri/src/commands/conversation.rs` — `send_message` (echoes input), `get_conversation_history` (returns empty vec), `clear_conversation`, `set_permission_mode`, `get_permission_mode`
- `App~/src-tauri/src/commands/plans.rs` — `list_plans` (returns empty vec), `read_plan` (returns mock), `write_plan`, `delete_plan` (all noop or fake)
- `App~/src-tauri/src/commands/rules.rs` — same pattern, all stubs
- `App~/src-tauri/src/commands/settings.rs` — `get_settings`, `update_settings`
- `App~/src-tauri/src/lib.rs` updated `tauri::Builder` chain with `.invoke_handler(tauri::generate_handler![...])` registering ALL commands

**Done when:**
- `cargo build` succeeds
- `pnpm tauri dev` launches without errors
- Calling any command from the React side via `invoke()` returns the stub data without panicking

**Notes:**
- `#[tauri::command]` on every function
- Use `Result<T, AppError>` for fallible ones, plain `T` for read-only stubs that can't fail yet
- Don't worry about thread safety / state yet — store nothing, just return fakes
- Stub `send_message` should return a `MessageId` like `"stub-{timestamp}"`

---

### 2.4 — Implement typed JS wrappers in src/ipc/commands.ts

**Pre-req:** 2.3
**Size:** S
**Output:**
- `App~/src/ipc/commands.ts` — exports a function per Tauri command, each correctly typed:
  ```ts
  import { invoke } from "@tauri-apps/api/core";
  import type { ConnectionStatus, ... } from "./types";

  export const getUnityStatus = (): Promise<ConnectionStatus> =>
    invoke("get_unity_status");

  export const sendMessage = (text: string, agent?: string): Promise<string> =>
    invoke("send_message", { text, agent: agent ?? null });

  // ...one per command
  ```

**Done when:**
- All commands from spec wrapped with proper types
- Calling `getUnityStatus()` from anywhere in React returns the stub `"connected"` (or whatever the stub gives)
- TypeScript catches type mismatches at compile time

**Notes:**
- Use `@tauri-apps/api/core` for `invoke` (Tauri 2.x path)
- snake_case → camelCase: Tauri auto-converts argument names; verify by testing one command end-to-end
- DO NOT export `invoke` raw from this module — only typed wrappers, so callers can't bypass types

---

### 2.5 — Wire ConnectionStore to real commands

**Pre-req:** 2.4
**Size:** S
**Output:**
- `App~/src/stores/connectionStore.ts` updated to call `getUnityStatus()` and `getNodeSdkStatus()` on mount
- Polling: poll every 2 seconds while window is open
- `App~/src/routes/SettingsRoute.tsx` shows live connection status (reflects what stubs return)

**Done when:**
- Settings tab shows "Unity: connected" and "Node SDK: connected" (from stubs)
- If you change the stub return value in Rust and rebuild, frontend reflects it within 2s
- No memory leaks — poll cleared on store unmount

**Notes:**
- Use `setInterval` inside the store's setup (Zustand pattern), or use a custom `useEffect` in a top-level component. Either works; keep it simple.
- 2s is fine for v1. Tune later if it feels janky or wasteful.

---

### 2.6 — Implement Tauri events (Rust → React)

**Pre-req:** 2.5
**Size:** M
**Output:**
- `App~/src-tauri/src/events.rs` — helpers: `emit_unity_status_changed`, `emit_node_sdk_status_changed`, `emit_message_received`, etc — each takes the AppHandle and a payload, calls `app.emit("event-name", payload)`
- `App~/src/ipc/events.ts` — typed `listen()` wrappers: `onUnityStatusChanged(handler)`, `onMessageReceived(handler)`, etc, each returns the unsubscribe function from `@tauri-apps/api/event`
- A test trigger in Rust: a Tauri command `dev_emit_test_event()` (only compiled in `#[cfg(debug_assertions)]`) that emits a `unity-status-changed` event with `disconnected`
- Frontend test: a button in SettingsRoute (also dev-only) that calls `dev_emit_test_event` and listens via `onUnityStatusChanged` — when fired, store updates, UI reflects

**Done when:**
- Click the dev test button → connection store gets the new status → UI updates from "connected" to "disconnected" → revert it back via another stub
- No runtime errors
- `listen()` cleanup runs on component unmount (verify with React strict mode double-mount)

**Notes:**
- Tauri 2.x uses `app.emit(event_name, payload)` (no longer `app.emit_all`)
- Use `useEffect` with cleanup return for `listen()` registration
- Hide the dev test button behind `import.meta.env.DEV` check

---

## Group 3 — Node Agent SDK supervision

### 3.1 — Spawn Node SDK as child process

**Pre-req:** 2.6
**Size:** M
**Output:**
- `App~/src-tauri/src/node_supervisor/mod.rs`, `spawn.rs` — module that:
  - On Tauri startup, locates Node executable (`node` from PATH; if missing, log error)
  - Locates Server~/dist entry point (path resolved relative to the unity project — for now, hardcode a path in dev config; production resolution comes later)
  - Spawns `node <entry>` with stdin/stdout piped
  - Stores the child handle in Tauri managed state
  - On Tauri shutdown (window close), kills the child cleanly
- Node SDK side: a minimal stub `Server~/dist/agent-sdk-stub.js` that just reads stdin lines, echoes back as `log` notifications. (This proves the pipe works without depending on Feature 02 being done.)

**Done when:**
- App starts, Node child process spawns (verify in Task Manager / Activity Monitor)
- App closes, Node child process terminates within 2s
- No zombie processes after running open/close 5 times in a row
- Tauri logs the child PID at startup

**Notes:**
- Use `tokio::process::Command` (Tauri's async runtime)
- Pipe stdin/stdout as `Stdio::piped()`, leave stderr inheriting (so errors show in dev console for now)
- Store `Child` in `tauri::State<Mutex<Option<Child>>>` or similar managed state
- Cleanup hook: `app.on_window_event` → on `CloseRequested`, async kill child, then accept close
- The stub `agent-sdk-stub.js` is a temporary file just for this task. Feature 02 replaces it. Add a comment in the file: "STUB — replaced by Feature 02 orchestrator."

---

### 3.2 — Implement JSON-RPC framing over stdio

**Pre-req:** 3.1
**Size:** L
**Output:**
- `App~/src-tauri/src/node_supervisor/jsonrpc.rs` — handles:
  - Reading newline-delimited JSON from child stdout
  - Writing newline-delimited JSON to child stdin
  - Request/response correlation by `id` field
  - Notifications (no `id`) dispatched to event emission
  - Tokio task spawned per direction (read loop, write queue)
  - Pending requests stored in a `HashMap<u64, oneshot::Sender<Value>>`
  - Timeout: requests that don't get response within 30s reject with timeout error
- `App~/src-tauri/src/node_supervisor/protocol.rs` — typed request/notification messages (see spec section "Tauri ↔ Node Agent SDK protocol")
- Update Node SDK stub to handle a few requests for testing:
  - `ping` request returns `{ "pong": true }`
  - `echo` request returns the input
  - Periodically (every 5s) emits a `log` notification

**Done when:**
- A test command in Rust `node_ping()` (#[tauri::command]) calls Node SDK, returns the pong within ~50ms
- Frontend can call `nodePing()` from SettingsRoute, sees the result
- Log notifications from Node show up in app's dev console (route through Tauri events for now)
- Killing Node SDK mid-request: the pending request rejects with a clear error, doesn't hang

**Notes:**
- This is the trickiest task in Feature 01. Allocate the L estimate honestly.
- Use `tokio::io::{BufReader, AsyncBufReadExt, AsyncWriteExt}` for line-based IO
- `serde_json::Value` for arbitrary JSON; deserialize to typed structs only when needed
- ID generator: simple atomic counter is fine
- Errors from Node SDK come back as `{"jsonrpc": "2.0", "id": N, "error": {...}}` — implement that branch
- If Node child stdout closes (process died), all pending requests should reject — important for restart logic in 3.3

---

### 3.3 — Restart and resilience

**Pre-req:** 3.2
**Size:** M
**Output:**
- `restart_node_sdk` Tauri command actually works: kills current child, spawns new one
- If Node child crashes unexpectedly, supervisor detects (read loop ends with EOF) and:
  - Emits `node-sdk-status-changed` event with `crashed`
  - Logs the crash
  - Does NOT auto-restart yet — user clicks restart button (manual control for v1)
- `node-sdk-status-changed` event fired on: `starting`, `running`, `crashed`
- SettingsRoute shows live Node SDK status; restart button calls `restart_node_sdk`

**Done when:**
- Manually killing the Node child (via Task Manager) → status flips to crashed within ~1s
- Clicking restart → status flips to starting → running → green
- Two consecutive restarts in 5s don't leak processes

**Notes:**
- Don't add auto-restart yet — Feature 02 might want different supervision logic (e.g. restart on `npm install` runs)
- "Status: starting" = process spawned but no `ping` response yet. "running" = first ping succeeded. Tauri keeps a small state machine.

---

## Group 4 — Unity client (TCP)

### 4.1 — Connect to Unity MCP Server (TCP)

**Pre-req:** 3.3
**Size:** M
**Output:**
- `App~/src-tauri/src/unity_client/mod.rs`, `connection.rs`, `protocol.rs`
- TCP client that:
  - Connects to `127.0.0.1:8090` on Tauri startup
  - Reconnects with backoff (1s, 2s, 5s, 10s, 30s capped) when connection lost
  - Emits `unity-status-changed` events on transitions
  - Sends a periodic heartbeat (every 5s) — Unity replies, status stays `connected`
- Use existing C# MCP Server protocol unchanged. If protocol details aren't documented, READ `Editor/MCP/` source to extract them.

**Done when:**
- Tauri starts with Unity open → `unity-status-changed: connected` fires within 1s
- Stop Unity → status flips to `disconnected` within heartbeat interval + a bit
- Restart Unity → reconnects automatically, emits `connected` again
- No tight reconnect loops if Unity stays down

**Notes:**
- Existing C# MCP Server already uses `TcpListener` with `ReuseAddress` — that's confirmed working. We just need a client.
- Heartbeat: a no-op MCP request, e.g. `tools/list` if that's cheap. If too expensive, design a `ping` MCP method on the C# side later. For now, `tools/list` is fine in dev.
- Logging: include connection events in app log so we can see reconnect timing

---

### 4.2 — Forward Unity tool calls through the bridge

**Pre-req:** 4.1
**Size:** M
**Output:**
- When Node SDK sends a request that requires a Unity tool call, Tauri forwards it to Unity TCP, awaits response, returns to Node SDK
- For Group 4 scope, this is a manual / RPC path — Node SDK explicitly says "call Unity tool X with params Y", Tauri handles transport
- Add a Tauri command `dev_call_unity_tool(name, params)` for testing without Node SDK orchestration

**Done when:**
- From SettingsRoute, a dev button calls `dev_call_unity_tool("project-info", {})` (or similar simple existing tool)
- Result comes back, displayed in UI
- Tool call latency shown in console (typically <50ms localhost)

**Notes:**
- Don't try to make Node SDK fluently invoke Unity yet — that's Feature 02 territory
- Just prove the wire works end-to-end: React → Tauri → Unity → Tauri → React

---

## Group 5 — Minimal end-to-end chat round trip

### 5.1 — Build minimal Chat UI

**Pre-req:** 4.2
**Size:** M
**Output:**
- `App~/src/routes/ChatRoute.tsx` rewritten:
  - Message list (scrollable, newest at bottom)
  - Input field at bottom, Enter to send (Shift+Enter newline)
  - Each message renders role badge (user / assistant) + content
- `conversationStore` updated:
  - `messages: Message[]`
  - `sendMessage(text)` action: optimistically appends user message, calls `sendMessage` Tauri command, awaits result
  - Listens to `message-received` event, appends assistant messages

**Done when:**
- Type "hello" in chat → press Enter → user message appears immediately
- Echo response from Node SDK stub appears within ~100ms
- Scroll auto-anchors to bottom
- Input clears after send

**Notes:**
- Visual polish minimal — readable monospace, rough Tailwind. We're not designing UI here, just proving the loop.
- Use `useEffect` for the event subscription, cleanup on unmount

---

### 5.2 — Echo stub in Node SDK proves the round trip

**Pre-req:** 5.1
**Size:** S
**Output:**
- Node SDK stub now handles the `conversation/send` JSON-RPC method:
  - Receives `{text, agent, session_id}`
  - Sends back `message/received` notification with `{id: "...", role: "assistant", content: "echo: <text>"}`

**Done when:**
- Send "hello world" in chat → see "echo: hello world" appear as assistant reply
- Send 5 messages rapidly → all 5 echoes show up, in order
- No dropped messages

**Notes:**
- This is the END of Feature 01. From here, Feature 02 replaces the echo with the actual orchestrator agent.
- Add a comment in the stub: "Replaced by Feature 02 orchestrator."

---

## Group 6 — Build pipeline

### 6.1 — Verify production build works on Windows

**Pre-req:** 5.2
**Size:** M
**Output:**
- `pnpm tauri build` produces a Windows `.msi` and `.exe` in `App~/src-tauri/target/release/bundle/`
- Built binary launches and works the same as dev mode (chat echo round-trip)
- Built binary size documented in this file (target: <30MB compressed)

**Done when:**
- The `.msi` installs on a clean Windows path
- After install, app appears in Start menu, launches, echo loop works
- Document final binary size in spec doc under "Build size note" section

**Notes:**
- macOS / Linux builds deferred. Windows-only is fine for v2.0 first ship.
- Codesigning deferred — unsigned `.msi` triggers SmartScreen warning, that's OK for early users
- `.gitignore` should exclude `App~/src-tauri/target/` (Cargo build dir) — verify it's ignored

---

## After Feature 01

The work after this is:

- **Feature 07 (Editor pin)** — provides a way to launch this app from inside Unity. Touches C# Editor code, not the Tauri app.
- **Feature 02 (Orchestrator agent)** — replaces the echo stub in Node SDK with real Claude Agent SDK conversations + subagent delegation.
- **Feature 03+ (rest)** — flesh out plans, rules, slash commands, etc.

Don't start any of those until Feature 01's "Definition of done" (in spec) is fully met.

---

## Status tracking

| Task | Status | Done date | Notes |
|------|--------|-----------|-------|
| 1.1 | ✅ done | 2026-04-25 | Janela vazia abriu, validado em Windows |
| 1.2 | ✅ done | 2026-04-25 | Dark theme aplicado, Tailwind classes verificadas |
| 1.3 | ✅ done | 2026-04-25 | Sidebar nav funcionando, 4 routes alternam |
| 1.4 | ✅ done | 2026-04-25 | 5 stores criados, settings tab renderiza dado |
| 2.1 | ✅ done | 2026-04-25 | Tipos completos, tsc limpo, sem any |
| 2.2 | ✅ done | 2026-04-25 | Tipos Rust + serde + testes round-trip passando |
| 2.3 | ✅ done | 2026-04-25 | 20 commands registrados |
| 2.4 | ✅ done | 2026-04-25 | 20 wrappers tipados, type safety verificado |
| 2.5 | ✅ done | 2026-04-25 | Polling 2s funcionando, status ao vivo na Settings |
| 2.6 | ⏳ pending | — | — |
| 3.1 | ⏳ pending | — | — |
| 3.2 | ⏳ pending | — | — |
| 3.3 | ⏳ pending | — | — |
| 4.1 | ⏳ pending | — | — |
| 4.2 | ⏳ pending | — | — |
| 5.1 | ⏳ pending | — | — |
| 5.2 | ⏳ pending | — | — |
| 6.1 | ⏳ pending | — | — |

Mark tasks as `✅ done` after Ramon commits the corresponding code.
