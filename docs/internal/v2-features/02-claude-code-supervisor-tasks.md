# Feature 02 — Claude Code Supervisor — Tasks

> **Companion to:** `02-claude-code-supervisor-spec.md`. Read that first.
> **Parent design doc:** `02-claude-code-supervisor.md` (the 7 locked decisions).
> **Execution model:** one task per Claude Code session. Ramon validates per checks below, commits via VS Code, returns to chat for next task.

## How to read this doc

- **S** = Small (~30 min – 1 h focused work). **M** = Medium (1–3 h). **L** = Large (3+ h, consider splitting if it grows).
- **Status column** updated by Ramon as tasks complete: ✅ done / 🔄 in progress / ⏳ pending.
- **Refs** point at the spec section that motivates the task.

---

## Status table

| # | Task | Size | Status | Date | Notes |
|---|------|------|--------|------|-------|
| 1.1 | Tauri command `check_claude_install_status()` + Rust install_check.rs | M | ✅ | 2026-04-29 | New `App~/src-tauri/src/claude_supervisor/install_check.rs` (sibling to existing `node_supervisor/`, no swap — that's task 2.1). 4-field `ClaudeInstallStatus` struct serialized via serde. 4 detection branches running in parallel via `tokio::join!` (required adding `"macros"` feature to tokio in Cargo.toml — was missing): `where.exe claude` exit code, `claude --version` parse, `claude /status` parse, fs.exists check on `App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/package.json`. New `commands/install.rs` (own file — install domain will grow with 1.3 + 4.4) registers `check_claude_install_status` in invoke_handler. No `.manage()` / setup / close hook (function pure, no state). TS binding in `App~/src/ipc/commands.ts`. Validated via DevTools console invoke across 4 forced states (claude renamed, logged out, sdk node_modules deleted) — all 4 fields toggle correctly within ~1s, no panics, errors silenced as defaults. Pre-existing F07 `Cannot find module 'agent-sdk-stub.js'` errors continue (expected — F02 task 2.1 swaps the spawn target). |
| 1.2 | React `FirstRunPanel.tsx` — 4 states UI (ready / installing-sdk / claude-missing / not-authenticated) | M | ✅ | 2026-04-30 | New `App~/src/components/FirstRunPanel.tsx` — 4 states + 1 internal `checking` (centered "Checking installation..." text on slate-900). Gate completo: App.tsx renderiza `<FirstRunPanel />` cobrindo viewport quando não-ready, `<UpdateBanner /> + <SidebarLayout />` quando ready (UpdateBanner some sob FirstRunPanel intencionalmente — first-run > update info). Polling 5s via setInterval em useEffect com cleanup. Tailwind seguindo padrão (slate-900 bg, slate-100 text, blue-700 accents). Listener defensivo `onSdkInstallProgress` registrado em IPC layer (App~/src/ipc/events.ts) com payload `{ percent: number \| null; message?: string }` — emissor real chega na 1.3. State 3 button "Open install docs" usa openUrl com URL `https://docs.claude.com/en/docs/claude-code/setup`; capability `default.json` estendido com scope `https://docs.claude.com/*` (lição da F07 5.3 aplicada preventivamente). State 4 button "Copy command" copia `claude /login` via `navigator.clipboard.writeText`. Validado: state 2 (panel travado em installing — esperado pois 1.3 ainda não emite progress; manualmente instalado SDK em App~/runtime/ confirma transição pra state 1 e desaparecimento do panel). Estados 3 e 4 e polling 5s validados via teardown manual (rename claude.exe + claude /logout). Pre-existente: F07 `Cannot find module 'agent-sdk-stub.js'` continua até task 2.1. |
| 1.3 | On-demand `npm install` of @anthropic-ai/claude-agent-sdk on first launch | M | ✅ | 2026-04-30 | New `App~/src-tauri/src/claude_supervisor/sdk_install.rs` spawns `npm install @anthropic-ai/claude-agent-sdk` no path `App~/runtime/` (mesmo anchor que install_check.rs usa: `CARGO_MANIFEST_DIR/.. + "runtime"`). Cria pasta + minimal package.json se ausente, depois roda npm install. Idempotência server-side: se SDK já existe, retorna Ok early sem spawn. Stderr ecoa via eprintln! pra terminal (debug do dev); última linha do stdout npm vira o `message` do event `sdk-install-progress` (payload `{ percent: null, message: "..." }`). Sem parse de % (npm output é caótico, indeterminate sempre). Falha → emit event de erro com últimas 5 linhas do stderr; sucesso → `sdk-install-completed` event. Tauri command `start_sdk_install()` registrado em `commands/install.rs`. FirstRunPanel state 2 (installing-sdk) chama no useEffect mount. Validado: install fresh (state 2 → installing → state 1 ready em ~30-90s), idempotência (reabrir Tauri pula install completo, vai direto pra ready), failure path (com Wi-Fi off, npm falha em ~30s, panel mostra erro com botão Retry). Pré-existente: F07 erro do agent-sdk-stub continua até task 2.1. App~/.gitignore já cobre runtime/. |
| 2.1 | Replace node_supervisor with claude_supervisor module skeleton (no logic yet, compiles clean) | S | ✅ | 2026-04-30 | New `App~/src-tauri/src/claude_supervisor/mod.rs` exposing `ClaudeSupervisor` with public surface (new/spawn/shutdown/status). Bodies são TODOs: `spawn` retorna `Err(SpawnError::NotImplemented)` com Display impl honesto ("claude_supervisor::spawn not yet implemented (task 2.2)"); `shutdown` no-op; `status` retorna Idle. SupervisorStatus enum com 5 variants (idle/starting/ready/crashed/failed) serializados snake_case (consistente com NodeSdkStatus existente). lib.rs: `.manage(ClaudeSupervisor::new())` substituiu `.manage(NodeSupervisor::new())`; setup spawn dentro de `tauri::async_runtime::spawn` aponta pro novo supervisor (segue pattern A — chama `spawn()` que retorna Err + eprintln! "[claude-supervisor] not yet implemented (task 2.2)"); CloseRequested aponta pra ClaudeSupervisor::shutdown. Event renomeado: `node-sdk-status-changed` → `supervisor-status-changed` (const novo, payload novo, ts binding novo). `commands/connection.rs::get_node_sdk_status` reescrito como `get_supervisor_status`. React: `connectionStore.ts` renomeou `nodeSdkStatus` → `supervisorStatus`; consumers atualizados. node_supervisor/ files intactos no tree (sai em 7.3); cargo warnings esperados em código morto (opção B aprovada — sinal autêntico). Restart Supervisor + Ping Node SDK ficam visíveis com erros honestos (Restart: "Supervisor not yet implemented (task 2.2)"; Ping: "state not managed"). Cleanup junto em 7.3. Bug fix incluído: `[object Object]` rendering nos 3 handlers de erro (handlePing/handleRestart/handleCallUnityTool) substituído por helper local `formatError` que cobre Error instances, strings raw, e fallback JSON.stringify. **CRITÉRIO PRINCIPAL VALIDADO: terminal limpo — `Cannot find module 'agent-sdk-stub.js'` stack trace some, substituído por linha clean do eprintln! da SpawnError::NotImplemented.** UI status indicator mostra "failed" (esperado pois spawn retorna Err). F07 single-instance + F02 1.x (FirstRunPanel + checkInstallStatus) sem regressão. |
| 2.2 | Spawn Agent SDK with cwd + envs from F07 launch contract; smoke round-trip | M | ✅ | 2026-04-30 | Real spawn substituiu o NotImplemented da 2.1. Stack: novo `claude_supervisor::runtime_setup::ensure_entry_script()` gera `App~/runtime/sdk-entry.js` (idempotent + adiciona `"type": "module"` no package.json se ausente). `claude_supervisor::spawn::spawn_node_child(project_path)` spawna Node child com env vars F07 EXPLÍCITAS via `cmd.env()` (UNITY_PROJECT_PATH, UNITY_MCP_HOST, UNITY_MCP_PORT) — confiabilidade vs. inheritance default. sdk-entry.js usa `import { query } from "@anthropic-ai/claude-agent-sdk"`, itera `for await`, filtra blocks `type === "text"`, agrega monoliticamente em `AssistantText` (streaming é 2.3). AgentMessage enum tipado em types.rs com discriminator kebab-case (Ready/AssistantText/AssistantTurnComplete/Error). Lifecycle Idle → Starting → Ready via signal `{type:"ready"}` no stdout do Node child; 5s timeout → Failed. send_message Tauri command (minimal, só text — attachments na 5.1) escreve JSON line no stdin do child. ChatRoute existente consome via emit_message_received (shape F01-era preservado pra evitar refactor; 2.3 migra pro canal agent-message novo). Dev fallback: quando UNITY_PROJECT_PATH ausente, supervisor cai pra `App~/runtime/` como cwd com log claro — permite `pnpm tauri dev` smoke sem Unity, mas caminho real é via F07 pin. Bug fix incluído: conversationStore.ts tinha `[object Object]` rendering idêntico ao SettingsRoute da 2.1; aplicado mesmo helper formatError. **CRITÉRIO PRINCIPAL VALIDADO: smoke real do chat funcionando end-to-end via F07 pin com Unity aberto** — "say hello in pirate" produz resposta real do Claude Code (não echo). Confirma: env contract F07 propaga corretamente, SDK query() funciona, Claude subprocess sobe limpo, response stream agrega e renderiza. Heads-up dev workflow: `pnpm tauri dev` standalone (sem Unity) hoje funciona via fallback mas é menos confiável; produção sempre via pin. |
| 2.3 | Agent SDK message stream → React conversationStore (text deltas only) | M | ✅ | 2026-04-30 | Streaming char-by-char substituiu `AssistantText` monolítico da 2.2. sdk-entry.js: `query()` options ganha `includePartialMessages: true` (confirmado via doc oficial Anthropic, não palpite); loop `for await` discrimina `msg.type === "stream_event"` (filtra `event.type === "content_block_delta"` + `event.delta.type === "text_delta"` → `emitTextDelta(turnId, text)`) e `msg.type === "result"` (→ `emitTurnComplete(turnId)`). turnId gerado em `makeTurnId()` no início de cada `handleInput` (produtor é dono do id, multi-block turns acumulam no mesmo id). types.rs: `AgentMessage` enum extendido com `TextDelta { turnId: String, text: String }` (kebab-case discriminator); `AssistantText` mantido como variant legacy aceita pelo wire shape (sem produtor 2.3+, ChatRoute case retorna no-op). spawn.rs read_stdout: canal único via `emit_agent_message` para todos os AgentMessage variants, deprecated path `emit_message_received` removido do hot path (function continua em events.rs com `#[allow(dead_code)]` porque jsonrpc.rs do node_supervisor legacy ainda referencia). conversationStore.ts: `appendMessage` genérico removido, substituído por API específica `appendDelta(turnId, text)` (lookup por turnId no array, append no content existente OR cria nova message), `completeTurn(turnId)` (no-op placeholder pra futuro cursor blink), `appendErrorMessage(text)` (mensagem system com prefixo "error:"). ChatRoute.tsx: listener trocado de `onMessageReceived` pra `onAgentMessage` com switch sobre `m.type` discriminator (text-delta/assistant-turn-complete/error/ready/assistant-text). MessageId type intocado (legacy, sai em 7.3). **CRITÉRIO PRINCIPAL VALIDADO: streaming visual char-by-char no chat funcionando** — prompt "hi" produziu 2 text-delta events seguidos de assistant-turn-complete; resposta apareceu progressivamente no chat. DevTools console confirmado emitindo `text-delta` no canal `agent-message`, zero events `message-received` ou `assistant-text` (canal único funcionando). Bug encontrado durante validação: `runtime_setup.rs::ensure_entry_script()` cache idempotente não sobrescreveu `App~/runtime/sdk-entry.js` quando o template fonte mudou na 2.3 — workaround manual: renomear runtime/sdk-entry.js antes de testar tasks que mudam o template. **TODO sugerido pra task 7.3 ou item próprio**: cache invalidation por hash do template OU sempre sobrescrever em `cfg(debug_assertions)` (idempotência só em release). Pré-existente: F07 erro do agent-sdk-stub continua. |
| 2.4 | Agent SDK message stream — tool use blocks + tool result blocks | M | ✅ | 2026-04-30 | Spawn contract estendido: `mcpServers.game-deck` configurado em sdk_entry.js via `buildMcpServers()` lendo `MCP_PROXY_PATH` (resolvido em spawn.rs via `paths::mcp_proxy_script()` apontando pro Server~/dist/mcp-proxy.js). Quando proxy ausente, supervisor emite AgentMessage::Error com warning honesto e spawna sem mcpServers (não-tool prompts continuam funcionando). sdk-entry.js itera dois novos paths no for-await: (1) `stream_event` com `content_block_start` tipo tool_use → acumula input via `input_json_delta`, emite tool-use no `content_block_stop`; (2) mensagens `user` com `tool_result` blocks → emite tool-result com is_error flag. AgentMessage enum estendido com ToolUse { turnId, toolUseId, name, input } e ToolResult { turnId, toolUseId, content, isError }, ambos kebab-case discriminator. ChatRoute.tsx switch estendido com cases tool-use e tool-result; conversationStore com appendToolUse/appendToolResult agrupando blocks no mesmo turn via turnId. Validado end-to-end com Unity aberto + C# MCP server na 8090: prompt "List all GameObjects in the current Unity scene" → Claude descobriu tool certa via ToolSearch (`mcp__game-deck__scene-get-hierarchy`) → chamou tool com args válidos → MCP proxy roteou pro C# server → Tool call + Tool result blocks renderizaram corretamente no chat. **Tool wiring 100% funcional.** Bloqueio pelo permission system do Claude Code (default mode pede approval) é esperado e será resolvido na task 4.2 (PermissionMode UI com dropdown dos 5 modes); não é problema da 2.4. Multi-tool turn (ToolSearch → scene-get-hierarchy) confirmou que turnId mantém continuidade entre tool calls dentro do mesmo turn. Pre-existente: F07 erro do agent-sdk-stub continua até 7.3. |
| 3.1 | Asset surfacing — `--add-dir` Skills~/ wired into spawn options | S | ⏳ | | |
| 3.2 | Asset surfacing — copy Agents~/ → .claude/agents/gamedeck-*.md with {{KB_PATH}} resolved | M | ⏳ | | |
| 3.3 | Manifest tracking (`Library/GameDeck/installed-agents.json`) + uninstall menu action | S | ⏳ | | |
| 3.4 | Empirical smoke test — `gamedeck-unity-shader-specialist` calls an MCP Game Deck tool successfully | S | ⏳ | | |
| 4.1 | `send_message` Tauri command rewritten to drive Agent SDK input | S | ⏳ | | |
| 4.2 | Permission mode UI — dropdown + Tauri commands `get_permission_mode` / `set_permission_mode` | M | ⏳ | | |
| 4.3 | Permission mode events — Shift+Tab inside chat propagates back to React UI | S | ⏳ | | |
| 4.4 | Session list — `get_sessions` Tauri command reading from Claude Code's storage; sidebar UI | M | ⏳ | | |
| 5.1 | Attachment migration — paths instead of base64 on the wire | M | ⏳ | | |
| 5.2 | Attachment cross-platform path test — Windows backslashes / spaces / non-ASCII | S | ⏳ | | |
| 6.1 | Health check on supervisor startup — minimal query, 5s timeout, surface clean failure | S | ⏳ | | |
| 6.2 | Crash detection — `node-sdk-status-changed: crashed` event wiring | S | ⏳ | | |
| 6.3 | Clean shutdown — both Node child and claude subprocess terminate within 2s on Tauri close | M | ⏳ | | |
| 6.4 | Windows hygiene — CREATE_NEW_PROCESS_GROUP equivalent + UTF-8 stdio | S | ⏳ | | |
| 6.5 | Pin known-good Claude Code version range; warn-but-don't-block on drift | S | ⏳ | | |
| 7.1 | E2E smoke test — F07 pin → Tauri → 3-turn conversation w/ tool call + attachment, repeat 5x | M | ⏳ | | |
| 7.2 | README + LICENSE proprietary-dependency disclosure (or track as TODO) | S | ⏳ | | |
| 7.3 | Cleanup — remove dead JSON-RPC types from F01 era | S | ⏳ | | |

24 tasks total. Groups 1+2 are critical path — once they work, the user can have a real conversation. Groups 3+4 are polish. Groups 5+6 are reliability. Group 7 closes the feature.

---

## Group 1 — Auth and install detection

> Goal: first-run experience that detects whether `claude` is on PATH + authenticated and whether the SDK is installed in the Tauri-managed Node runtime; surfaces the right next step in React. Supervisor stays idle until everything checks out.

### Task 1.1 — Tauri command `check_claude_install_status()` + Rust install_check.rs

**Size:** M
**Refs:** spec "Install-detection contract"

**Context:** four pieces of information drive the FirstRunPanel UX. They live in Rust because they involve subprocess spawning + filesystem checks. Single Tauri command bundles them into one IPC roundtrip so React can render the panel atomically.

**Output:**

- New file `App~/src-tauri/src/claude_supervisor/install_check.rs`
- Public function `check_install_status() -> ClaudeInstallStatus` (struct serializes to JSON with the 4 fields from spec)
- Detection logic:
  - `claudeInstalled`: `where.exe claude` on Windows (use `std::process::Command`); exit code 0 → true
  - `claudeVersion`: if installed, run `claude --version`, parse output (e.g. "Claude Code 2.10.3" → `"2.10.3"`); null on failure
  - `claudeAuthenticated`: spawn `claude /status` non-interactively (`--no-interactive` or equivalent CLI flag — verify in docs at task time); parse output for "logged in" or similar; false on failure
  - `sdkInstalled`: check `App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/package.json` exists. The `runtime/` path is decided here — document the choice and stick with it
- Errors during detection (e.g., `where.exe` missing) → return `false` for that field, don't propagate
- New Tauri command `check_claude_install_status` in `commands/connection.rs` (or new `commands/install.rs` if you prefer; one place owns all install-related commands)
- Add to `invoke_handler` in `lib.rs`
- TypeScript binding: `App~/src/ipc/commands.ts` exports `checkClaudeInstallStatus()`

**Validation:**

1. Compile clean (Rust + TS).
2. Open Tauri DevTools console, run:
   ```javascript
   const { invoke } = await import('@tauri-apps/api/core');
   await invoke('check_claude_install_status');
   ```
3. With Claude Code installed + logged in + SDK present → all four fields true / non-null
4. Uninstall Claude Code (or rename it temporarily) → `claudeInstalled: false`, others false/null
5. Logout (`claude /logout`) → `claudeAuthenticated: false`
6. Delete `App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/` → `sdkInstalled: false`
7. Re-run command in each scenario; results match expectations within ~1s

**Commit:**

```
feat(v2): F02 task 1.1 — Tauri command + Rust install detection

App~/src-tauri/src/claude_supervisor/install_check.rs detects:
- claudeInstalled — `where.exe claude` exit code
- claudeVersion — parsed from `claude --version`
- claudeAuthenticated — parsed from `claude /status`
- sdkInstalled — App~/runtime/node_modules/.../package.json exists

Tauri command check_claude_install_status returns the 4-field
ClaudeInstallStatus struct in one IPC roundtrip. TS binding
exported in App~/src/ipc/commands.ts.

Refs: 02-claude-code-supervisor-tasks.md (task 1.1)
```

---

### Task 1.2 — React `FirstRunPanel.tsx` — 4 states UI

**Size:** M
**Refs:** spec "Install-detection contract" (states 1-4)

**Output:**

- New file `App~/src/components/FirstRunPanel.tsx`
- 4 states, switched on `ClaudeInstallStatus`:
  1. **All ready** → component returns `null` (parent layout takes over)
  2. **SDK installing** → progress strip ("Installing @anthropic-ai/claude-agent-sdk... 12%") driven by `onSdkInstallProgress` event from Rust (event wiring in 1.3)
  3. **Claude Code missing** → centered card with title "Install Claude Code", short copy, "Open install docs" button → `openUrl("https://docs.claude.com/en/docs/claude-code/setup")`
  4. **Not authenticated** → centered card with title "Log in to Claude Code", short copy, "Copy command" button → copies `claude /login` to clipboard
- Polls `checkClaudeInstallStatus()` every 5s while mounted; auto-advances when state changes
- App.tsx mount: `<FirstRunPanel />` above the existing layout (returns null when ready, so layout fills the window)
- Tailwind styling consistent with the rest of the app (slate-900 bg, slate-100 text, blue-700 accents)

**Validation:**

1. Force each state by manipulating `claude` install / auth / SDK presence (same as task 1.1 validation)
2. Each state renders the correct copy + CTA
3. State 3 (Claude Code missing) → click "Open install docs" → browser opens at the URL (uses opener plugin from F07)
4. State 4 (Not authenticated) → click "Copy command" → paste in Notepad → `claude /login`
5. With nothing installed: panel stays at state 3 until you install Claude Code (5s poll detects), advances to state 4 until you `claude /login`, advances to state 1 (or state 2 if SDK still installing) — verify the polling-driven progression
6. Layout fills window normally when panel returns null

**Commit:**

```
feat(v2): F02 task 1.2 — FirstRunPanel React component

FirstRunPanel shows the right next step based on
checkClaudeInstallStatus(). Four states: ready (renders null),
SDK installing (progress driven by onSdkInstallProgress event),
Claude Code missing (Open install docs button), not authenticated
(Copy command button for `claude /login`).

5s polling auto-advances when state changes. Mounted above the
main layout in App.tsx.

Refs: 02-claude-code-supervisor-tasks.md (task 1.2)
```

---

### Task 1.3 — On-demand `npm install` of @anthropic-ai/claude-agent-sdk

**Size:** M
**Refs:** spec "Install-detection contract" + Decision #1 (SDK not bundled)

**Context:** The SDK is proprietary and we can't bundle it (ADR-001 validation #1). Tauri must `npm install` it on the first launch into `App~/runtime/node_modules/`. Subsequent launches skip the install (sdkInstalled === true).

**Output:**

- New file `App~/src-tauri/src/claude_supervisor/sdk_install.rs`
- Public function `install_sdk_async(app: AppHandle) -> Result<(), Error>` that:
  - Creates `App~/runtime/` if missing
  - Writes a minimal `package.json` if missing (with `"@anthropic-ai/claude-agent-sdk": "^X.Y.Z"` dependency at the version pinned by spec/ADR)
  - Spawns `npm install` with cwd = `App~/runtime/`
  - Streams stdout to `onSdkInstallProgress` Tauri event (parse `npm` output for percent — best-effort; if parse fails, emit indeterminate progress every 500ms)
  - On exit code 0 → emit `sdk-install-completed` event
  - On non-zero → emit `sdk-install-failed` event with error message
- Tauri command `start_sdk_install()` (idempotent — no-op if already installed)
- React side: `FirstRunPanel.tsx` calls `start_sdk_install()` on mount when state is "Claude Code installed + auth OK + SDK missing"; subscribes to progress + completed + failed events
- Capability: review `App~/src-tauri/capabilities/default.json` — running `npm install` may require shell:allow-execute or similar; verify and grant minimum needed
- The Node child supervisor (next group) reads from `App~/runtime/node_modules/` when spawning the SDK

**Validation:**

1. Delete `App~/runtime/node_modules/` entirely.
2. Open Tauri (via `pnpm tauri dev` or via the F07 pin if testing real launch). FirstRunPanel renders state 2 (SDK installing).
3. Progress events arrive — DevTools console shows them via `listen('sdk-install-progress', ...)` (or rely on the panel's own progress bar).
4. After ~30s-2min (depends on machine + network), `sdk-install-completed` fires; panel advances to state 1 (all ready).
5. `App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/` exists.
6. Re-launch Tauri — install does NOT re-run (idempotent), panel goes straight to state 1.
7. Failure path: temporarily set npm registry to invalid URL (`npm config set registry http://invalid`), re-run; install fails, panel surfaces error with retry option. Restore registry.

**Commit:**

```
feat(v2): F02 task 1.3 — on-demand SDK install via npm

App~/src-tauri/src/claude_supervisor/sdk_install.rs spawns npm
install for @anthropic-ai/claude-agent-sdk into App~/runtime/
on first launch. SDK is NOT bundled in the MSI (per ADR-001
validation #1 — Anthropic Commercial Terms don't grant
redistribution rights).

Progress streamed to React via onSdkInstallProgress event;
completion via sdk-install-completed; failures via
sdk-install-failed with message. FirstRunPanel state 2
consumes these.

Idempotent: subsequent launches skip install when
App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/
package.json exists.

Refs: 02-claude-code-supervisor-tasks.md (task 1.3)
```

---

## Group 2 — Engine spawn

> Goal: replace the F01 echo stub with a real Agent SDK invocation. After this group, typing in chat produces actual Claude Code output streamed back to the React UI.

### Task 2.1 — Replace node_supervisor with claude_supervisor skeleton

**Size:** S
**Refs:** spec "File layout"

**Context:** F01 created `App~/src-tauri/src/node_supervisor/` to spawn the echo stub. This task creates the parallel `claude_supervisor/` module with the same public surface (spawn, status, shutdown) but wired to spawn nothing yet — that's task 2.2. Compile must stay clean throughout the swap.

**Output:**

- New folder `App~/src-tauri/src/claude_supervisor/`
- New file `claude_supervisor/mod.rs` exposing:
  - `pub struct ClaudeSupervisor` — owns the supervisor state (process handles, status enum, etc.)
  - `pub fn new() -> Self`
  - `pub async fn spawn(&self, app: AppHandle) -> Result<u32, SpawnError>` — TODO body (returns `Err(SpawnError::NotImplemented)` for now)
  - `pub async fn shutdown(&self)` — TODO body (no-op for now)
  - `pub fn status(&self) -> SupervisorStatus` — returns `Idle` for now
- Keep existing `node_supervisor/` files in tree but **stop wiring them in `lib.rs`**
- `lib.rs`: `.manage(ClaudeSupervisor::new())` replaces `.manage(NodeSupervisor::new())`
- `lib.rs`: setup block's `tauri::async_runtime::spawn(async move { ... supervisor.spawn() ... })` wires to the new ClaudeSupervisor (which will return NotImplemented but that's fine for this task)
- `lib.rs`: WindowEvent::CloseRequested handler points at ClaudeSupervisor::shutdown
- Existing `commands/connection.rs::get_node_sdk_status` rewritten as `get_supervisor_status` — returns the new `SupervisorStatus` enum (Idle / Starting / Ready / Crashed / Failed)
- React side: `connectionStore.ts` renames `nodeSdkStatus` → `supervisorStatus`; consumers (status indicators, etc.) updated
- Existing `node_supervisor/` files **left in tree, unused** — cleanup in task 7.3 once we're sure the swap is stable

**Validation:**

1. Compile clean (Rust + TS).
2. `pnpm tauri dev` → Tauri opens. Status indicator in the UI shows "Idle" (or whatever the new enum reports as initial state).
3. **No more `Cannot find module 'agent-sdk-stub.js'` errors in the terminal** — the spawn path no longer reaches the deleted stub.
4. The "Cannot find module" error from F07 is replaced by silence (or by a clean log line "[claude-supervisor] not yet implemented" if you add one).
5. Existing F07 tests still pass: pin click opens Tauri, single-instance + route still work, FirstRunPanel renders correctly (assuming Claude Code + SDK installed from Group 1).

**Commit:**

```
feat(v2): F02 task 2.1 — claude_supervisor skeleton replaces node_supervisor

Adds App~/src-tauri/src/claude_supervisor/mod.rs as the new
managed-state owner for the supervisor. Public surface:
new / spawn / shutdown / status. Bodies are TODOs that compile
clean and return Idle / NotImplemented; real wiring lands in 2.2.

lib.rs swaps .manage() target, setup spawn target, and
CloseRequested shutdown target. Existing node_supervisor/ stays
in the tree (unused) for one feature cycle; task 7.3 deletes it.

Side benefit: the F07-era "Cannot find module agent-sdk-stub.js"
error is gone — spawn path no longer reaches the deleted stub.

Refs: 02-claude-code-supervisor-tasks.md (task 2.1)
```

---

### Task 2.2 — Spawn Agent SDK with cwd + envs from F07; smoke round-trip

**Size:** M
**Refs:** spec "Spawn contract"

**Output:**

- New file `claude_supervisor/spawn.rs`
- `ClaudeSupervisor::spawn` body:
  - Reads `UNITY_PROJECT_PATH`, `UNITY_MCP_HOST`, `UNITY_MCP_PORT` from process env (set by F07 pin)
  - Spawns Node child with cwd = `App~/runtime/`, env vars passed through, command = `node` + path to a small entry script
- New file `App~/runtime/sdk-entry.js` (created at runtime by Rust if missing — written by `sdk_install.rs` extension or by a separate `runtime_setup.rs`):
  - Imports `@anthropic-ai/claude-agent-sdk`
  - Reads cwd + env vars from `process.env`
  - Calls `query()` with the spawn contract from spec
  - For this task: hard-codes a single roundtrip — accepts a JSON line from stdin (`{ "type": "input", "text": "..." }`), forwards to SDK, streams response messages back as JSON lines on stdout
  - This is replaced by full bidirectional streaming in 2.3 / 2.4 — for now, focus on proving end-to-end works
- Tauri side captures stdout, forwards each JSON line as a Tauri event (`agent-message`)
- Smoke prompt: when the supervisor reports `Ready`, the React side's existing send-message flow (via `send_message` Tauri command from F01) sends "echo test" via stdin; the supervisor should get a real Claude Code response (NOT the echo stub's mirror)

**Validation:**

1. Pre-req: Group 1 fully working (Claude Code + SDK installed + authenticated).
2. `pnpm tauri dev` → supervisor spawns; status indicator → "Starting" → "Ready" within ~3-5s.
3. In the chat tab, type "say hello in pirate" and send.
4. Response streams back from real Claude Code, NOT the echo stub. Expected: actual pirate-speak text from the model.
5. Terminal logs from the Node child appear (via `agent-message` events) in DevTools console.
6. **No more `Cannot find module` errors.**
7. Smoke also confirms the env-var contract from F07 works: `UNITY_PROJECT_PATH` becomes the SDK's cwd; `UNITY_MCP_HOST/PORT` will be used by the MCP proxy in 2.4 (not yet wired here, just confirm they reach the Node side).

**Commit:**

```
feat(v2): F02 task 2.2 — Agent SDK spawned with F07 launch contract

ClaudeSupervisor::spawn launches a Node child running
App~/runtime/sdk-entry.js, which imports
@anthropic-ai/claude-agent-sdk and calls query() with cwd from
UNITY_PROJECT_PATH and the rest of the F07 env contract.

Single-roundtrip smoke confirmed: typing in chat produces real
Claude Code output, not echo-stub mirroring. Streaming +
multi-turn comes in 2.3 / 2.4. Tool-use blocks come in 2.4.

The F07-era error stack is replaced by clean SDK output.

Refs: 02-claude-code-supervisor-tasks.md (task 2.2)
```

---

### Task 2.3 — Agent SDK message stream → React conversationStore (text deltas)

**Size:** M
**Refs:** spec "Wire protocol — Tauri ↔ Node migration"

**Output:**

- `sdk-entry.js` extended to forward all SDK message types as JSON lines on stdout (one message per line, with a `type` discriminator field)
- For this task, focus on TWO message types:
  - `text-delta` (streaming assistant content) — accumulates into the current assistant message
  - `assistant-turn-complete` (final marker) — closes the current assistant message
- Tauri Rust side: `claude_supervisor::lifecycle::on_stdout_line()` parses the JSON, emits typed Tauri events
- React side: `App~/src/ipc/events.ts` adds `onAgentMessage(callback)` subscriber
- `conversationStore.ts`:
  - Replaces the F01 monolithic-response handler with a streaming handler
  - `appendTextDelta(messageId, delta)` accumulates deltas
  - `completeAssistantTurn(messageId)` marks the message done
  - The chat UI renders the partial text as it streams (existing component should already handle this case if F01 designed correctly; verify and adapt)
- Stale F01 paths (the old monolithic JSON-RPC response handler) deleted

**Validation:**

1. Type a longer prompt that produces a multi-sentence response ("write a haiku about Unity").
2. Watch the chat UI: assistant text appears character-by-character (or token-by-token), not as a single blob at the end.
3. Multi-turn conversation works: send another prompt, response streams again, both messages persist in scroll.
4. Cancel mid-response (UI cancel button if existing, else type `/clear` if Claude Code supports it) — partial message stays in history; next prompt starts fresh.

**Commit:**

```
feat(v2): F02 task 2.3 — streaming text deltas from SDK to React

App~/runtime/sdk-entry.js forwards SDK messages as JSON lines on
stdout. Tauri Rust parses + emits as Tauri events. React's
conversationStore consumes via onAgentMessage subscriber.

For this task: text-delta and assistant-turn-complete only.
Tool use blocks land in 2.4. Multi-sentence responses stream
character-by-character in the chat UI. Multi-turn persists.

Refs: 02-claude-code-supervisor-tasks.md (task 2.3)
```

---

### Task 2.4 — Agent SDK message stream — tool use + tool result blocks

**Size:** M
**Refs:** spec "Wire protocol — Tauri ↔ Node migration", spec "Spawn contract" (mcpServers wiring)

**Output:**

- Spawn contract extended: `mcpServers.game-deck` configured with `command: "node"`, `args: [<package>/Server~/dist/mcp-proxy.js]`, `env: { UNITY_MCP_HOST, UNITY_MCP_PORT }` from the F07 env vars
- `sdk-entry.js` forwards two more message types:
  - `tool-use` (Claude is calling an MCP tool — pre-permission display, e.g., "Calling mcp__game-deck__get_scene_objects(...)")
  - `tool-result` (the tool returned data; truncate large payloads in display, full payload retained in store)
- React's `conversationStore` adds tool-use + tool-result entries as separate "blocks" within an assistant message (chat UI shows them as collapsible details inline with the assistant text)
- New chat UI component if not already in F01: `<ToolUseBlock />`, `<ToolResultBlock />` (collapsible, syntax-highlighted JSON for params and results)
- Verify with: a prompt that triggers a tool call, e.g. "List the scenes in this Unity project" → Claude Code → mcp__game-deck__list_scenes call → roundtrip through `mcp-proxy.js` → C# MCP Server → reply

**Validation:**

1. C# MCP Server running in Unity (port 8090).
2. Pre-req: Group 1 + tasks 2.1-2.3 working.
3. Send: "What scenes are in this Unity project?"
4. Chat UI shows:
   - Assistant text: "Let me check..." (streamed)
   - Tool use block: `mcp__game-deck__list_scenes({})` (collapsed by default, expandable)
   - Tool result block: JSON with the scenes (collapsed by default)
   - Assistant text continues: "I found 3 scenes: ..."
5. The tool call actually went through — verify by checking C# MCP Server logs in Unity Console (should show the incoming request).
6. Multi-tool turn: "Find all GameObjects named 'Player' and print their components" → triggers multiple tool calls; each one renders as its own block.

**Commit:**

```
feat(v2): F02 task 2.4 — tool use + tool result blocks via MCP

Spawn contract now configures mcpServers.game-deck pointing at
Server~/dist/mcp-proxy.js with UNITY_MCP_HOST/PORT from F07.
sdk-entry.js forwards tool-use and tool-result SDK messages as
typed Tauri events. React renders them as collapsible blocks
inline with assistant text.

End-to-end MCP roundtrip confirmed: prompt → SDK → claude → MCP
proxy → C# Server → Unity → reply, all streamed and rendered.

Refs: 02-claude-code-supervisor-tasks.md (task 2.4)
```

---

## Group 3 — Asset surfacing

> Goal: 22 generic skills + 10 Unity specialists are usable from `claude` after this group. Skills via `--add-dir`; specialists via copy-to-`.claude/agents/` with `{{KB_PATH}}` resolved at copy time. Empirical smoke confirms specialists can call MCP Game Deck tools.

### Task 3.1 — `--add-dir` Skills~/ in spawn options

**Size:** S
**Refs:** spec "Spawn contract" (additionalDirectories), Decision #2

**Output:**

- `claude_supervisor/spawn.rs` extended:
  - Resolves `<package>/Skills~/` absolute path (the package root is reachable via `UNITY_PROJECT_PATH` + walking up to find the package, OR via a separate env var the F07 pin sets — verify and document)
  - Adds it to the SDK's `additionalDirectories` array
- Optional second entry: `ProjectSettings/GameDeck/commands/` (if directory exists; skipped silently if not)
- Verify in `claude` interactively (open the Tauri-spawned `claude` via session resume or via direct `claude` in the same project): `/skills` lists 22 skills

**Validation:**

1. After spawn, in the chat tab: `/skills` (or the equivalent — Claude Code may auto-list) shows 22 generic skills surfaced from `Skills~/`.
2. User skills (in `<unity-project>/.claude/skills/`) also appear, alongside ours.
3. Sample test: trigger a skill via natural prompt that matches its description; verify Claude invokes it.

**Commit:**

```
feat(v2): F02 task 3.1 — Skills~/ surfaced via --add-dir

Spawn contract resolves <package>/Skills~/ absolute path and adds
to SDK additionalDirectories. ProjectSettings/GameDeck/commands/
also added if present (skipped if absent).

Verified: /skills lists all 22 generic skills + any user skills
from <unity-project>/.claude/skills/. Sample skill invocation via
natural prompt works.

Refs: 02-claude-code-supervisor-tasks.md (task 3.1)
```

---

### Task 3.2 — Copy Agents~/ → .claude/agents/gamedeck-*.md with {{KB_PATH}}

**Size:** M
**Refs:** spec "Asset surfacing — copy step", Decision #2 + #4

**Output:**

- New file `claude_supervisor/asset_install.rs`
- Public function `install_agents(unity_project_root: &Path, package_root: &Path) -> Result<Manifest, Error>`:
  - For each `<package>/Agents~/<n>.md`:
    - Read content
    - `content.replace("{{KB_PATH}}", &package_kb_absolute_path)`
    - Write to `<unity-project>/.claude/agents/gamedeck-<n>.md`
    - **Refuse** to overwrite if target file exists AND name doesn't have `gamedeck-` prefix
  - Return the list of files written
- Called from `claude_supervisor::spawn` BEFORE the SDK spawn — agents must be on disk before `claude` reads `.claude/agents/`
- Idempotency: if `gamedeck-<n>.md` already exists with current package version (manifest check in 3.3), skip the copy — saves I/O on every launch

**Validation:**

1. Delete `<unity-project>/.claude/agents/` entirely.
2. `pnpm tauri dev` → spawn runs → directory recreated with 10 `gamedeck-*.md` files.
3. Open one (e.g. `gamedeck-unity-shader-specialist.md`) in editor → verify `{{KB_PATH}}` is replaced with absolute path to `<package>/KnowledgeBase~/`.
4. In `claude` chat: `/agents` → 10 specialists appear with `gamedeck-` prefix.
5. Manually create `<unity-project>/.claude/agents/my-custom-agent.md` (no prefix). Re-run spawn. File untouched (we don't overwrite).
6. Manually create `<unity-project>/.claude/agents/gamedeck-fake.md` (with prefix, but we didn't write it). Re-run spawn. File overwritten if name matches one we manage; left alone if name doesn't match.

**Commit:**

```
feat(v2): F02 task 3.2 — copy Agents~/ to .claude/agents/ with {{KB_PATH}}

App~/src-tauri/src/claude_supervisor/asset_install.rs walks
<package>/Agents~/, substitutes {{KB_PATH}} with the package's
KnowledgeBase~/ absolute path, and writes
<unity-project>/.claude/agents/gamedeck-<n>.md for each of the
10 specialists.

Refuses to overwrite files without the gamedeck- prefix (= user
files). Idempotent on subsequent launches via package-version
check in 3.3.

Refs: 02-claude-code-supervisor-tasks.md (task 3.2)
```

---

### Task 3.3 — Manifest tracking + uninstall menu action

**Size:** S
**Refs:** spec "Asset surfacing — copy step" (manifest section)

**Output:**

- `asset_install.rs` extended:
  - Reads/writes `<unity-project>/Library/GameDeck/installed-agents.json` (manifest format from spec)
  - Tracks: `packageVersion`, `claudeVersion`, `files: [{ name, writtenAt }]`
  - Idempotency check: skip the copy step if manifest's `packageVersion === current package version` AND all listed files still exist on disk
- New Tauri command `uninstall_managed_agents()`:
  - Reads manifest, deletes each `files[i].name` from `<unity-project>/.claude/agents/`, deletes manifest
  - Returns count of files removed
- Optional menu action in Tauri (Settings tab or a Tauri menu bar): "Uninstall MCP Game Deck managed agents"
- Confirmation dialog before deletion ("This will remove 10 files from `.claude/agents/`. User-created agents are NOT affected. Continue?")

**Validation:**

1. After 3.2 ran, manifest exists at `<unity-project>/Library/GameDeck/installed-agents.json` with all 10 entries.
2. Re-run spawn — copy step skipped (verify via debug log or by checking writtenAt timestamps unchanged).
3. Manually delete one of the `gamedeck-*.md` files. Re-run spawn — manifest's "all files exist" check fails → copy re-runs → all 10 files restored.
4. Settings panel (or menu) "Uninstall managed agents" → confirmation dialog → 10 files removed → manifest deleted.
5. User-created `my-custom-agent.md` (from 3.2 step 5) untouched after uninstall.

**Commit:**

```
feat(v2): F02 task 3.3 — manifest + uninstall menu action

Library/GameDeck/installed-agents.json tracks the 10 agents we
write, with package + claude version stamps. Idempotency: spawn
skips copy when manifest matches current state.

Tauri command uninstall_managed_agents removes only the files we
wrote; user files untouched. Settings UI surfaces a confirmation
dialog before deletion.

Refs: 02-claude-code-supervisor-tasks.md (task 3.3)
```

---

### Task 3.4 — Empirical smoke test: specialist calls MCP tool

**Size:** S
**Refs:** spec "Definition of done" (Group 3 smoke), Decision #3

**Output:**

This is a validation task, not a code task. The output is a paragraph in the F02 design doc + the tasks doc confirming Decision #3 holds (or noting the fallback was needed).

The test:

1. Open Tauri (with all of Group 1 + 2 + 3.1 + 3.2 + 3.3 working).
2. Send: "Use the gamedeck-unity-shader-specialist to look up which scenes use a custom shader called 'WaterShader'."
3. Expected: Claude main thread invokes `Task` with `subagent_type: "gamedeck-unity-shader-specialist"`. The subagent runs in its own context; uses `mcp__game-deck__<some-tool>` to query Unity; returns a summary.
4. Check the response: did the subagent successfully call the MCP tool? Look for tool-use blocks WITHIN the subagent's transcript (Claude Code displays subagent activity nested in the main response).
5. **If yes:** Decision #3 holds. Write a paragraph in the parent design doc + this tasks file confirming "smoke test 2026-XX-XX confirmed specialists can call MCP tools as agents; original Decision #3 stands."
6. **If no:** the bug isn't fully fixed. Add a follow-up task block here for "rewrite specialists as skills" — same content as the original ADR-001 fallback plan. Don't block the rest of F02 on this; the specialists are nice-to-have, the supervisor itself doesn't depend on them.

**Validation:**

The validation IS the task. Document the result.

**Commit:**

```
chore(v2): F02 task 3.4 — empirical smoke test for specialists + MCP

Tested gamedeck-unity-shader-specialist invocation via Task tool;
[result: PASS — subagent successfully called mcp__game-deck__<tool>
and returned a summary, OR result: FAIL — subagent could not call
MCP tools, falling back to skills rewrite per appendix in tasks doc].

Decision #3 [holds / changes to skills fallback].

Refs: 02-claude-code-supervisor-tasks.md (task 3.4)
```

---

## Group 4 — Permission mode + sessions

> Goal: React UI surfaces permission modes + session list. Both are thin wrappers over Claude Code's own machinery (Decision #5 + #6).

### Task 4.1 — `send_message` rewritten to drive Agent SDK input

**Size:** S
**Refs:** spec "Wire protocol — Tauri ↔ Node migration" (outgoing)

**Output:**

- `commands/conversation.rs::send_message` rewritten:
  - Accepts `(text: String, attachment_paths: Vec<String>)` (attachments wired in Group 5; for now empty array)
  - Forwards to `claude_supervisor` via internal channel
  - Supervisor writes JSON line to `sdk-entry.js`'s stdin: `{ "type": "input", "text": "...", "attachments": [...] }`
  - `sdk-entry.js` calls `query.input(text)` with attachment handling
- Drop the F01 conversation-history command — Claude Code's own session storage is the source of truth (Decision #6); React queries via task 4.4
- Drop `clear_conversation` — Claude Code provides `/clear` in chat naturally

**Validation:**

1. Send a prompt — confirm round-trip works (already validated in 2.2-2.4; this task formalizes the contract).
2. The old `clear_conversation` and `get_conversation_history` Tauri commands are gone OR redirected to no-op stubs that React doesn't call anymore.
3. No regression in chat UX.

**Commit:**

```
feat(v2): F02 task 4.1 — send_message drives SDK input directly

commands/conversation.rs::send_message accepts text + attachment
paths and forwards through claude_supervisor to sdk-entry.js's
stdin (JSON line, type: input). SDK calls query.input internally.

Drops F01 monolithic conversation history + clear commands —
Claude Code session storage and /clear are the sources of truth
(Decision #6).

Refs: 02-claude-code-supervisor-tasks.md (task 4.1)
```

---

### Task 4.2 — Permission mode UI

**Size:** M
**Refs:** spec "Permission mode UI", Decision #5

**Output:**

- New file `App~/src/components/PermissionModeToggle.tsx` (or extend existing F01 component)
- Dropdown with 5 modes: `default`, `acceptEdits`, `plan`, `bypassPermissions`, `auto`
- Tauri commands:
  - `get_permission_mode()` → `String` (current mode from supervisor state)
  - `set_permission_mode(mode: String)` → forwards to `sdk-entry.js` as a control message; SDK updates `query.setPermissionMode(mode)`
- Mounted in the chat input toolbar (above the textarea, right-aligned)
- Reads current mode on mount; subscribes to mode-change events for the next task

**Validation:**

1. Dropdown shows all 5 modes; current selection reflects supervisor state.
2. Change to `plan` → next prompt enters plan mode (Claude Code's plan-mode banner appears in chat, and Claude doesn't take destructive actions without a plan).
3. Change to `acceptEdits` → next prompt edits files without per-edit prompting.
4. Change to `bypassPermissions` → all permissions bypassed (use cautiously in testing).

**Commit:**

```
feat(v2): F02 task 4.2 — permission mode UI dropdown

PermissionModeToggle dropdown surfaces Claude Code's 5 modes
(default / acceptEdits / plan / bypassPermissions / auto).
Tauri commands get_permission_mode and set_permission_mode
forward to sdk-entry.js's stdin as control messages.

Mounted in the chat input toolbar.

Refs: 02-claude-code-supervisor-tasks.md (task 4.2)
```

---

### Task 4.3 — Permission mode events (Shift+Tab)

**Size:** S
**Refs:** spec "Permission mode UI"

**Output:**

- `sdk-entry.js` forwards SDK's `permission-mode-changed` events as JSON lines on stdout (when user pressed Shift+Tab inside the chat input rendered by Claude Code)
- Tauri Rust emits `permission-mode-changed` event
- React's `PermissionModeToggle.tsx` subscribes; updates dropdown selection on event arrival (no dropdown action triggers a Tauri command — passive sync)

**Validation:**

1. Chat input has focus; press Shift+Tab → mode cycles in Claude Code; React dropdown reflects the new mode.
2. Cycle through all 5 modes via Shift+Tab; dropdown stays in sync.
3. Reverse: change via dropdown → `claude` mode updates correctly (this should already work from 4.2; verify no regression).

**Commit:**

```
feat(v2): F02 task 4.3 — Shift+Tab permission mode sync

sdk-entry.js forwards SDK's permission-mode-changed event;
Tauri Rust re-emits; PermissionModeToggle subscribes and updates
its selection passively. Cycle via Shift+Tab in chat input now
reflects in the dropdown.

Refs: 02-claude-code-supervisor-tasks.md (task 4.3)
```

---

### Task 4.4 — Session list

**Size:** M
**Refs:** spec "Stack" (session storage), Decision #6

**Output:**

- New Tauri command `get_sessions()` → `Vec<SessionSummary>` (id, title, last_modified, message_count)
- Implementation: spawn `claude --list-sessions --json` (or equivalent — verify the right command at task time); parse output
- Alternative: read Claude Code's session storage directory directly (typically `~/.claude/projects/<hash>/sessions/`); verify path on Windows
- Tauri command `resume_session(session_id: String)` → forwards to `sdk-entry.js`; SDK calls `query()` with `resume: sessionId`
- React: new sidebar component `SessionList.tsx` mounted on the Chat route; clicking a session resumes it; visual indicator for the active session
- New Session button creates a fresh session (current behavior)

**Validation:**

1. After several conversations, sidebar shows them as a list (titles auto-derived by Claude Code from first prompt).
2. Click an old session → chat reloads with that session's history.
3. New Session → fresh empty chat.
4. Sessions persist across Tauri restarts (since storage is Claude Code's, not ours).
5. Removing the F01 `.game-deck-sessions.json` machinery — file no longer created; old file (if exists) ignored.

**Commit:**

```
feat(v2): F02 task 4.4 — session list from Claude Code's storage

get_sessions Tauri command reads from Claude Code's session
storage (Decision #6: their storage, not ours). resume_session
forwards to query.resume.

SessionList sidebar in Chat route shows list, supports click-to-
resume, marks active. New Session creates fresh.

The F01-era .game-deck-sessions.json machinery is dead — file
no longer written, old file ignored.

Refs: 02-claude-code-supervisor-tasks.md (task 4.4)
```

---

## Group 5 — Attachment migration

> Goal: file paths replace base64 on the wire. Cross-platform path edge cases tested on Windows.

### Task 5.1 — Paths instead of base64

**Size:** M
**Refs:** spec "Attachment migration"

**Output:**

- React drag-drop already captures file paths (verify); update if not
- `send_message` Tauri command's `attachment_paths: Vec<String>` parameter (already declared in 4.1) is now used: paths forwarded to `sdk-entry.js`'s stdin as part of the input JSON line
- `sdk-entry.js` passes paths directly to `query.input(text, { attachments: paths })` — SDK handles encoding internally
- Drop F01's base64 encoding code (likely in `App~/src/ipc/` or a similar path); cleanup in 7.3

**Validation:**

1. Drag a small PNG (say, 200KB) into the chat → "what's in this image?" → Claude Code analyzes correctly.
2. Drag a multi-MB PDF (say, 5MB) → "summarize this PDF" → Claude Code reads and summarizes.
3. The wire protocol no longer carries base64 (verify via Tauri DevTools network tab or Rust-side logging — payloads are paths, not blobs).
4. F01's base64 encoder code path is unused (mark for deletion in 7.3).

**Commit:**

```
feat(v2): F02 task 5.1 — attachments via paths, not base64

send_message attachment_paths now flows as paths through to the
SDK, which handles encoding internally (per Decision #1 — SDK
absorbs platform quirks). Drops F01's base64 encoder from the
hot path.

Verified with PNG (200KB) and PDF (5MB) — Claude Code reads
both correctly via path.

Refs: 02-claude-code-supervisor-tasks.md (task 5.1)
```

---

### Task 5.2 — Cross-platform path edge cases

**Size:** S
**Refs:** spec "Attachment migration", ADR-001 validation #7

**Output:**

- Validation task — exercise the edge cases
- If any fail, add a path normalization step in `claude_supervisor::spawn` or in `sdk-entry.js`:
  - Windows backslashes → forward slashes
  - URI-encode spaces? (unlikely needed; SDK should handle)
- Document findings in this tasks doc

**Validation:**

1. PNG at `C:\Users\Ramon\Desktop\test.png` → works (forward slashes vs backslashes).
2. PNG at `C:\Users\Ramon\Desktop\my pictures\test.png` (spaces) → works.
3. PNG at `C:\Users\Ramon\Desktop\maçã.png` (non-ASCII) → works.
4. PNG at `C:\Projetos\Jurassic\Assets\Images\dinossauro.png` (deep nested + accent) → works.

**Commit:**

```
chore(v2): F02 task 5.2 — attachment path edge cases on Windows

Validated four edge cases: backslashes (default Windows form),
spaces in path, non-ASCII characters, deep nested paths. All
pass through to the SDK without normalization needed.
[OR if normalization needed: added forward-slash normalization
in spawn.rs before forwarding to sdk-entry.js.]

Refs: 02-claude-code-supervisor-tasks.md (task 5.2)
```

---

## Group 6 — Lifecycle and resilience

### Task 6.1 — Health check on supervisor startup

**Size:** S
**Refs:** spec "Lifecycle" (Startup)

**Output:**

- `claude_supervisor::lifecycle::health_check()` — spawn a minimal `query.input("__health__")` 1.5s after spawn completes
- 5s timeout for response; on timeout → emit `supervisor_status: crashed` + log
- On healthy response → emit `supervisor_status: ready`
- React's FirstRunPanel + main UI consume the state changes (FirstRunPanel unmounts on `ready`, surfaces error on `crashed`)

**Validation:**

1. Healthy launch: status flow Idle → Starting → Ready within ~5s.
2. Force a crash: kill the Node child manually mid-launch (Task Manager) → supervisor detects within 5s and reports crashed.
3. After crashed, restart button (in Settings or as part of the error UI) re-runs spawn cleanly.

**Commit:**

```
feat(v2): F02 task 6.1 — health check on spawn

health_check() sends a minimal query 1.5s after spawn; 5s
timeout. Healthy → supervisor_status: ready. Timeout → crashed.
React UI consumes state transitions and surfaces crash with
restart action.

Refs: 02-claude-code-supervisor-tasks.md (task 6.1)
```

---

### Task 6.2 — Crash detection event wiring

**Size:** S
**Refs:** spec "Lifecycle"

**Output:**

- Watch the Node child's exit code; non-zero exit at any point during runtime → emit `supervisor_status: crashed`
- React UI shows a "Supervisor crashed — Restart" surface (toast or inline banner in chat) on this event
- Manual restart button calls `restart_supervisor` Tauri command

**Validation:**

1. Spawn working; kill `claude` subprocess mid-conversation via Task Manager.
2. Tauri detects within ~2s (process exit propagates from `claude` → SDK → Node child → Rust).
3. UI surfaces the crash banner with Restart button.
4. Click Restart → fresh spawn → new `Ready` state, conversation history preserved (Claude Code session storage).

**Commit:**

```
feat(v2): F02 task 6.2 — crash detection + restart

Node child exit watcher emits supervisor_status: crashed on
non-zero exit at any point. React surfaces crash banner with
Restart. Restart re-runs spawn; session history preserved via
Claude Code's storage.

Refs: 02-claude-code-supervisor-tasks.md (task 6.2)
```

---

### Task 6.3 — Clean shutdown on Tauri close

**Size:** M
**Refs:** spec "Lifecycle" (Shutdown)

**Output:**

- F01's `WindowEvent::CloseRequested` handler already calls supervisor shutdown (was wired to `node_supervisor`; task 2.1 swapped it to `claude_supervisor`)
- `claude_supervisor::lifecycle::shutdown()`:
  - Send termination signal to Node child (Windows: `GenerateConsoleCtrlEvent` for the process group, or send a `{type: "shutdown"}` JSON line first as a graceful nudge)
  - Wait 2s
  - If Node child still alive → SIGKILL equivalent on Windows (`TerminateProcess`)
  - Node child should propagate to `claude` subprocess (claude is its child); verify both die
- After shutdown, `app.exit(0)` runs as before

**Validation:**

1. Open Tauri, run a 2-turn conversation.
2. Close window via X button.
3. In Task Manager, no `node.exe` or `claude.exe` processes spawned by Tauri remain (give it 5s for cleanup).
4. Repeat 5 times — no zombie accumulation.
5. Edge: kill the Tauri process itself (Task Manager) — Node + claude become orphans (parent dead). This is acceptable but document it: orphan cleanup is a v2.1+ concern.

**Commit:**

```
feat(v2): F02 task 6.3 — clean shutdown of all 3 processes

claude_supervisor::lifecycle::shutdown sends graceful signal to
Node child, waits 2s, force-kills if needed. Node child
propagates to claude subprocess. No zombies in Task Manager
after 5 close cycles.

Edge: killing Tauri process itself leaves orphans (parent dead);
documented as v2.1+ concern.

Refs: 02-claude-code-supervisor-tasks.md (task 6.3)
```

---

### Task 6.4 — Windows hygiene

**Size:** S
**Refs:** spec "Spawn contract", Decision #7, ADR-001 validation #4

**Output:**

- `claude_supervisor::windows_hygiene` module:
  - Node child spawned with `creation_flags: CREATE_NEW_PROCESS_GROUP` (Windows-specific; Rust's `std::os::windows::process::CommandExt::creation_flags`)
  - `stdio: 'utf-8'` set on all streams (Node side)
  - Long system prompts (if F08 ever sets them) passed via `--append-system-prompt` file path, not CLI arg
- Apply only on `cfg!(windows)`; macOS / Linux noop

**Validation:**

1. Spawn from a PowerShell window. Press Ctrl+C in the PowerShell — Node child + claude + Tauri all survive (CREATE_NEW_PROCESS_GROUP isolates from parent's signal).
2. Send a prompt with non-ASCII output (e.g. Chinese characters in a code comment); verify text streams correctly without `?` placeholders or mojibake (UTF-8 stdio working).

**Commit:**

```
feat(v2): F02 task 6.4 — Windows subprocess hygiene

Node child spawned with CREATE_NEW_PROCESS_GROUP — isolates from
parent's Ctrl+C. stdio explicitly UTF-8. Long system prompts
go via --append-system-prompt file path (not CLI arg) per ADR-001
validation #4 workarounds.

Verified: Ctrl+C in PowerShell doesn't kill Tauri's children;
non-ASCII text in responses streams correctly.

Refs: 02-claude-code-supervisor-tasks.md (task 6.4)
```

---

### Task 6.5 — Pin Claude Code version range

**Size:** S
**Refs:** spec "Stack" (engine subprocess)

**Output:**

- Document the known-good Claude Code version range in `package.json` (e.g., `"claudeCode": ">=2.10.0 <3.0.0"`) — not a real npm dep, just a metadata field for our own use
- On supervisor startup, read `claudeVersion` from install detection; compare against the range
- If outside range → emit a warning event consumed by React (small banner: "Claude Code version 2.5.0 detected; tested with 2.10+. Some features may behave differently."); don't block

**Validation:**

1. Confirm the range in package.json reflects what we actually tested with.
2. Force a low version: temporarily replace `claude --version` output via test mode → warning banner appears, app still works.
3. Real version: no banner.

**Commit:**

```
feat(v2): F02 task 6.5 — Claude Code version range warning

package.json's claudeCode field documents the known-good range.
Supervisor startup compares current version; outside range emits
a warning consumed by React (non-blocking banner).

Verified at boundaries: in-range silent, out-of-range banner.

Refs: 02-claude-code-supervisor-tasks.md (task 6.5)
```

---

## Group 7 — Smoke + cleanup

### Task 7.1 — End-to-end smoke test

**Size:** M
**Refs:** spec "Definition of done"

**Output:**

This is a validation task. Run the full E2E flow 5 times back-to-back; document results.

The flow per iteration:

1. Open Unity (with C# MCP Server running)
2. Click the F07 pin → Tauri opens
3. FirstRunPanel: ready (assumes prior runs installed everything)
4. Type prompt 1: "What's in this Unity project?" → tool call → result → response
5. Type prompt 2: "Drag a screenshot of the Game view in here and describe it" + drag PNG → analysis
6. Type prompt 3: "Use the gamedeck-unity-shader-specialist to find shader issues"
7. Toggle permission mode to `plan` → next prompt → plan mode response
8. Close Tauri → verify clean shutdown (Task Manager check)

Repeat 5x. Note any failures.

**Validation:**

The validation IS the task. Document per-iteration results. Ideal: 5/5 pass.

**Commit:**

```
chore(v2): F02 task 7.1 — E2E smoke test (5 iterations)

Ran the full pin-to-shutdown flow 5 times: open Unity, click
F07 pin, send 3 prompts (tool call, attachment, specialist
delegation), toggle plan mode, close cleanly.

Result: [N/5 passed; failures: ...].

Refs: 02-claude-code-supervisor-tasks.md (task 7.1)
```

---

### Task 7.2 — README + LICENSE proprietary-dependency disclosure

**Size:** S
**Refs:** spec "Pre-public-release checks"

**Output:**

- `README.md` — add a "Requirements" section with the proprietary-dependency disclosure verbatim from spec
- `LICENSE` — add a note at the bottom: "MCP Game Deck is MIT-licensed. It depends on Claude Code and `@anthropic-ai/claude-agent-sdk` (proprietary, by Anthropic, governed by the Anthropic Commercial Terms). These are NOT bundled."
- Or, if Ramon prefers to defer: track as a TODO in `docs/internal/release-checklist.md` (create if doesn't exist) for the public-release pass

**Validation:**

1. README renders correctly on GitHub.
2. LICENSE note doesn't conflict with MIT.
3. (If deferred): release checklist file exists with the TODO.

**Commit:**

```
docs(v2): F02 task 7.2 — README + LICENSE proprietary-dep disclosure

Per ADR-001 validation #1 + F02 spec "Pre-public-release checks":
Claude Code and @anthropic-ai/claude-agent-sdk are proprietary
external deps, NOT bundled. README "Requirements" section and
LICENSE note both reflect this.

Refs: 02-claude-code-supervisor-tasks.md (task 7.2)
```

---

### Task 7.3 — Cleanup: remove dead F01-era code

**Size:** S
**Refs:** spec "File layout" (post-F02 deletions)

**Output:**

- Delete `App~/src-tauri/src/node_supervisor/` entirely (replaced by `claude_supervisor/`)
- Delete `App~/src-tauri/src/commands/` modules that became no-ops in 4.1 (`get_conversation_history`, `clear_conversation` — if their bodies are stubs, remove the commands and their TS bindings)
- Delete F01's base64 attachment encoder (replaced by paths in 5.1)
- Delete the F01-era JSON-RPC type definitions in `App~/src-tauri/src/jsonrpc/` (or wherever) IF the SDK message stream fully replaced them
- Delete `App~/runtime/sdk-entry.js`'s "single roundtrip" path from 2.2 if 2.3+ replaced it
- Verify Tauri compiles clean after all deletions; smoke test still passes

**Validation:**

1. Compile clean.
2. Tauri opens, smoke flow from 7.1 still works.
3. No imports left pointing to deleted modules.

**Commit:**

```
chore(v2): F02 task 7.3 — cleanup F01-era dead code

Removed:
- App~/src-tauri/src/node_supervisor/ (replaced by claude_supervisor/)
- F01 base64 attachment encoder (replaced by paths in 5.1)
- F01 JSON-RPC type defs (replaced by SDK message stream)
- get_conversation_history + clear_conversation Tauri commands
  (no-ops since 4.1)
- sdk-entry.js single-roundtrip path (replaced in 2.3+)

Tauri compiles clean. Smoke test from 7.1 passes post-cleanup.

Refs: 02-claude-code-supervisor-tasks.md (task 7.3)
```

---

24 tasks total. Estimated total time: 25-40h focused work depending on validation depth and unknowns hit during Group 1 (npm install pathing) and Group 2 (SDK message format edge cases).

When all tasks ✅, F02 is feature-complete. The supervisor is the spine for F04 / F05 / F06 / F08 — they all consume it.
