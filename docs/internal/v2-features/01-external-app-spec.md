> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.
> **Status post-ADR:** `delivered — engine target updated.` This spec was executed as written and shipped April 2026 (see `01-external-app-tasks.md` for per-task status). Under ADR-001 the **target** of the Node supervisor changed: instead of spawning the custom Agent SDK Server (`Server~/dist/index.js`), Tauri now spawns Claude Code via `@anthropic-ai/claude-agent-sdk`. The supervisor pattern, the diagram's three-process layout, the React shell, and the TCP path to Unity all stand. Specific deltas:
>
> 1. Diagram's third box — "NODE AGENT SDK SERVER (existing Server~/) — child process of Tauri" — read as "@anthropic-ai/claude-agent-sdk (Node) supervising a `claude` subprocess." The TCP path to Unity (port 8090) stays exactly as drawn.
> 2. Section "Tauri ⇔ Node Agent SDK protocol" — the custom JSON-RPC 2.0 dialect described there is replaced by the Agent SDK's own message protocol. Tauri owns supervision and stdio framing; the wire format becomes the SDK's.
> 3. Definition of done item 5 ("Send Message in Chat tab successfully round-trips: types → Tauri command → Node SDK stub → echo back → renders") was met with the echo stub. Under ADR-001 the equivalent target is "types → Tauri command → Agent SDK → Claude Code → streamed response."
> 4. Definition of done item 9 ("Feature 02 orchestrator can plug into the Node SDK side without re-architecting Tauri") is **dropped** — Feature 02 was superseded by ADR-001.
> 5. "What this feature does NOT include" still applies; just substitute "the orchestrator agent logic itself (Feature 02)" with "the Claude Code subprocess wiring under ADR-001."
>
> The MSI 2.93 MB / NSIS 1.94 MB / exe 8.99 MB measurements stay valid. The production runtime caveats (Node SDK path resolution via `env!("CARGO_MANIFEST_DIR")`, `UNITY_PROJECT_PATH` env var) stay valid — they remain Feature 07's responsibility.

# Feature 01 — External App (Tauri) — Spec

> **Status:** `agreed` — design decisions locked April 2026.
> **Companion:** `01-external-app-tasks.md` (decomposed work breakdown for Claude Code execution).

## What this is

A standalone desktop app, bundled inside the Unity package as `App~/`, that hosts the chat UI and project surfaces (plans, rules, settings). Replaces the in-Unity UI Toolkit chat window.

Three OS processes total at runtime:

```
┌─────────────────────────┐    TCP localhost:8090    ┌──────────────────────────┐
│   UNITY EDITOR          │◄──────────────────────►│   TAURI APP              │
│   (C# MCP Server)       │    (existing protocol)  │   - Rust backend         │
└─────────────────────────┘                          │   - React frontend       │
                                                     └────────┬─────────────────┘
                                                              │ stdio + JSON-RPC
                                                              ▼
                                                     ┌──────────────────────────┐
                                                     │  NODE AGENT SDK SERVER   │
                                                     │  (existing Server~/)     │
                                                     │  child process of Tauri  │
                                                     └──────────────────────────┘
```

## Stack decisions (locked)

| Layer | Choice | Why |
|-------|--------|-----|
| Desktop shell | **Tauri 2.x** | ADR 001 — bundle size |
| Backend lang | **Rust** | Tauri default; Ramon will write minimal commands + supervisor |
| Frontend lang | **TypeScript** | Type safety across IPC boundaries matters |
| UI lib | **React 18** | Familiar, component model fits |
| Build tool | **Vite** | Tauri default; fast dev loop |
| Styling | **Tailwind CSS 3.x** | Utility-first, no CSS files to manage |
| State mgmt | **Zustand** | ~3KB, hooks-based, no boilerplate |
| Routing | **React Router** | Tabs (chat/plans/rules/settings) need URL state for back-button etc |
| Tauri ↔ React IPC | **Commands + Events** (Tauri native) | Commands for user-initiated actions; Events for push updates |
| Tauri ↔ Node SDK IPC | **stdio + JSON-RPC 2.0** | Simple, supervisable, no port conflicts |
| Tauri ↔ Unity IPC | **TCP localhost:8090** | Reuses existing C# MCP Server protocol unchanged |

## File layout

```
App~/
├── package.json                  ← npm metadata for the React side
├── pnpm-lock.yaml or package-lock.json
├── vite.config.ts
├── tsconfig.json
├── tailwind.config.js
├── postcss.config.js
├── index.html                    ← Vite entry
├── src/                          ← React frontend
│   ├── main.tsx                  ← React mount point
│   ├── App.tsx                   ← top-level layout, router
│   ├── routes/
│   │   ├── ChatRoute.tsx
│   │   ├── PlansRoute.tsx
│   │   ├── RulesRoute.tsx
│   │   └── SettingsRoute.tsx
│   ├── components/               ← reusable UI
│   ├── stores/                   ← Zustand stores
│   │   ├── connectionStore.ts    ← Unity + Node SDK connection state
│   │   ├── conversationStore.ts  ← messages, current session
│   │   ├── plansStore.ts         ← plans list + open plan
│   │   ├── rulesStore.ts         ← rules list + enabled set
│   │   └── settingsStore.ts      ← user prefs
│   ├── ipc/                      ← Tauri command/event bindings
│   │   ├── commands.ts           ← typed wrappers around invoke()
│   │   ├── events.ts             ← typed listen() helpers
│   │   └── types.ts              ← shared types between Rust and TS
│   ├── styles/
│   │   └── globals.css           ← Tailwind directives + base resets
│   └── utils/
├── src-tauri/                    ← Rust backend
│   ├── Cargo.toml
│   ├── tauri.conf.json
│   ├── build.rs
│   ├── icons/                    ← app icons (placeholder)
│   └── src/
│       ├── main.rs               ← entry point
│       ├── lib.rs                ← run() function called by main
│       ├── commands/             ← Tauri command handlers
│       │   ├── mod.rs
│       │   ├── connection.rs     ← unity status, node sdk status
│       │   ├── conversation.rs   ← send message, get history
│       │   ├── plans.rs          ← CRUD against ProjectSettings/GameDeck/plans/
│       │   ├── rules.rs          ← CRUD against ProjectSettings/GameDeck/rules/
│       │   └── settings.rs       ← read/write app settings
│       ├── node_supervisor/      ← Node SDK child process management
│       │   ├── mod.rs
│       │   ├── spawn.rs          ← spawn / monitor / restart
│       │   ├── jsonrpc.rs        ← framing, request/response correlation
│       │   └── protocol.rs       ← request/response types
│       ├── unity_client/         ← TCP client to Unity MCP Server
│       │   ├── mod.rs
│       │   ├── connection.rs     ← connect, heartbeat, reconnect
│       │   └── protocol.rs       ← MCP message framing (existing)
│       ├── events.rs             ← centralized event emission to frontend
│       └── error.rs              ← unified error type
└── dist/                         ← compiled binaries per platform (gitignored;
                                      released as GitHub Release assets)
```

## Tauri commands (Rust → exposed to JS)

Naming convention: snake_case in Rust, camelCase auto-mapped in JS by Tauri.

```rust
// connection.rs
get_unity_status()              -> ConnectionStatus
get_node_sdk_status()           -> ConnectionStatus
reconnect_unity()               -> Result<(), AppError>
restart_node_sdk()              -> Result<(), AppError>

// conversation.rs
send_message(text: String, agent: Option<String>) -> Result<MessageId, AppError>
get_conversation_history(session_id: String, limit: usize) -> Vec<Message>
clear_conversation(session_id: String) -> Result<(), AppError>
set_permission_mode(mode: PermissionMode) -> Result<(), AppError>
get_permission_mode() -> PermissionMode

// plans.rs
list_plans() -> Vec<PlanMeta>
read_plan(name: String) -> Result<Plan, AppError>
write_plan(name: String, content: String) -> Result<(), AppError>
delete_plan(name: String) -> Result<(), AppError>

// rules.rs
list_rules() -> Vec<RuleMeta>
read_rule(name: String) -> Result<Rule, AppError>
write_rule(name: String, content: String) -> Result<(), AppError>
delete_rule(name: String) -> Result<(), AppError>
toggle_rule(name: String, enabled: bool) -> Result<(), AppError>

// settings.rs
get_settings() -> AppSettings
update_settings(patch: AppSettingsPatch) -> Result<(), AppError>
```

## Tauri events (Rust → emitted to JS)

```rust
// emitted from unity_client when status changes
"unity-status-changed" -> { status: "connected" | "busy" | "disconnected", reason?: String }

// emitted from node_supervisor when status changes
"node-sdk-status-changed" -> { status: "running" | "starting" | "crashed", pid?: u32 }

// emitted when a message arrives from Node SDK (assistant reply, tool result, etc)
"message-received" -> Message

// emitted during streaming reply
"message-stream-chunk" -> { message_id: String, chunk: String }
"message-stream-complete" -> { message_id: String }

// emitted when agent invokes ask_user (interactive plan mode)
"ask-user-requested" -> { question_id: String, question: String, options: Option<Vec<String>>, type: "single" | "multi" | "free-text" }

// emitted when Node SDK requests permission (ask mode)
"permission-requested" -> { request_id: String, tool: String, params: Value }
```

## Tauri ↔ Node Agent SDK protocol

JSON-RPC 2.0 over Node child process stdio. One JSON object per line (`\n`-delimited), UTF-8.

**Tauri → Node SDK requests:**

```json
{"jsonrpc": "2.0", "id": 1, "method": "conversation/send", "params": {"text": "...", "agent": null, "session_id": "..."}}
{"jsonrpc": "2.0", "id": 2, "method": "conversation/clear", "params": {"session_id": "..."}}
{"jsonrpc": "2.0", "id": 3, "method": "permission/set_mode", "params": {"mode": "auto"}}
{"jsonrpc": "2.0", "id": 4, "method": "ask_user/respond", "params": {"question_id": "...", "answer": "..."}}
{"jsonrpc": "2.0", "id": 5, "method": "permission/respond", "params": {"request_id": "...", "decision": "allow" | "deny"}}
```

**Node SDK → Tauri notifications (no id, push):**

```json
{"jsonrpc": "2.0", "method": "message/received", "params": {...Message}}
{"jsonrpc": "2.0", "method": "message/stream/chunk", "params": {"message_id": "...", "chunk": "..."}}
{"jsonrpc": "2.0", "method": "message/stream/complete", "params": {"message_id": "..."}}
{"jsonrpc": "2.0", "method": "ask_user/requested", "params": {...}}
{"jsonrpc": "2.0", "method": "permission/requested", "params": {...}}
{"jsonrpc": "2.0", "method": "log", "params": {"level": "info", "text": "..."}}
```

## Permission flow (high level)

The permission system fix from Feature 05 is implemented in the Node SDK. Tauri is a transport — it forwards user choices and displays prompts. The Rust side does NOT decide permissions. Single source of truth: Node SDK's `PermissionState`.

## File system scopes (Rust)

Tauri's allowlist limits filesystem access. Scopes:

- `$APPCONFIG/MCPGameDeck/` — app settings (read/write)
- `<unity_project>/ProjectSettings/GameDeck/plans/**` — plans (read/write)
- `<unity_project>/ProjectSettings/GameDeck/rules/**` — rules (read/write)
- `<unity_project>/Library/MCPGameDeck/logs/**` — runtime logs (write only)

Unity project path is provided by the C# Editor side via the MCP server (when Unity connects, it announces its `Application.dataPath` parent). Tauri persists the most recently connected project so it can re-scope on next launch.

## What this feature does NOT include

- The orchestrator agent logic itself (Feature 02) — Node SDK side
- The interactive plan UI (Feature 04) — uses the `ask-user-requested` event from this feature, but the UI itself is its own feature doc
- Slash command parsing (Feature 03) — frontend only, comes after this scaffolding works
- Plans CRUD UI (Feature 06) — uses commands defined here but the UI is its own feature
- Rules UI (Feature 08) — same
- The Editor pin (Feature 07) — separate feature, separate spec
- Auto-update (deferred to v2.x)
- Codesigning / notarization (deferred until ship-time)

## Out of scope reminders (from CLAUDE.md and roadmap)

- Don't pull v1.2 tool refactor into this work — paused until v2.1.x
- Ramon owns git
- Sentinel convention `"true" | "false" | ""` applies to any new C# / Node-side params (TS/Rust use proper Optional types, no sentinels needed there)

## Definition of done for Feature 01

The feature is "done" when ALL of these hold:

1. `App~/` exists with the file layout above
2. `pnpm install && pnpm tauri dev` (or npm equivalent) launches a window
3. Window shows a layout with Chat / Plans / Rules / Settings tabs (empty content is OK)
4. Settings tab shows current connection status to Unity (live, polled)
5. Send Message in Chat tab successfully round-trips: types → Tauri command → Node SDK stub → echo back → renders
6. Closing window cleanly terminates the Node SDK child process (no orphans)
7. App can be built into a per-platform binary via `pnpm tauri build` (Windows .msi at minimum; macOS / Linux deferred to ship-time)
8. No git operations performed by the build process — Ramon owns git
9. Feature 02 (orchestrator) can plug into the Node SDK side without re-architecting Tauri

That's the bar. Anything beyond (real chat, plans, rules) is later features.

## Open questions deferred to implementation

1. **Where exactly does the Tauri binary extract to on first run?** Resolved during Feature 07 (pin) integration — pin spawns the binary, decides path.
2. **Multiple Unity projects open simultaneously** — handled in Feature 07 design.
3. **Auto-update vs package update** — deferred to v2.x.
4. **macOS notarization cost** — addressed when first macOS release is needed; not blocking dev.

These are noted in `docs/internal/v2-architecture.md` already; do not re-litigate during Feature 01 implementation.

## Build size note

Measured on Windows after `pnpm tauri build` (April 2026, Tauri 2.10.x).

| Artifact | Path (under `App~/src-tauri/target/release/`) | Size |
|---|---|---|
| Windows installer (Wix MSI) | `bundle/msi/MCP Game Deck_0.1.0_x64_en-US.msi` | **2.93 MB** |
| Windows installer (NSIS) | `bundle/nsis/MCP Game Deck_0.1.0_x64-setup.exe` | **1.94 MB** |
| Standalone executable | `mcp-game-deck-app.exe` | **8.99 MB** |

Target was <30 MB compressed for the `.msi`. Tauri 2.x with the system WebView2 runtime (pre-installed on Win10/11) ships extremely lean — final installer is ~10× under target.

### Production runtime caveats (resolved by Feature 07, not 6.1)

- **Node SDK path** is resolved via `env!("CARGO_MANIFEST_DIR")` — embeds the developer's machine path at compile time. The release binary works only on a machine where the repo lives at the same path. Distribution-ready resolution (resource bundling or env var injection) lands with Feature 07's pin.
- **Auth token** requires `UNITY_PROJECT_PATH` env var to be set before launch. Feature 07's pin sets this when spawning the app.

### macOS / Linux

Deferred. The `targets: "all"` config in `src-tauri/tauri.conf.json` will build them when run on those OSes, but neither is shipped for v2.0.
