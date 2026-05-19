# Feature 02 — Claude Code Supervisor — Spec

> **Status:** `agreed` — design decisions locked April 2026; revalidated 2026-04-29 after F07 merged into v2.0.
> **Companion:** `02-claude-code-supervisor-tasks.md` (decomposed work breakdown for Claude Code execution).
> **Parent design doc:** `02-claude-code-supervisor.md` (the seven locked decisions and the 6-group breakdown).
> **Architectural parent:** `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.

## What this is

The work that ADR-001 actually requires: replacing the Feature 01 echo stub with Claude Code itself, spawned by the Tauri Node child via `@anthropic-ai/claude-agent-sdk`.

When this feature ships:

1. The user clicks the Editor pin (F07, already shipped) → Tauri opens
2. First-run flow detects whether `claude` is on PATH and authenticated, and whether `@anthropic-ai/claude-agent-sdk` is installed in the Tauri-managed Node runtime
3. If anything is missing, the React UI surfaces a clear next step (install Claude Code via official docs, log in via `claude /login`, install SDK on first launch)
4. Once everything is ready, the supervisor spawns `claude` as a subprocess with: working directory = Unity project root, the bundled `Plugin~/` (skills + agents) surfaced via the SDK's `plugins` option, MCP config pointing at `Server~/dist/mcp-proxy.js`
5. The user types in chat, Claude Code answers using the 10 Unity specialists (as agents) plus the 22 generic skills plus any user-configured MCPs and Spec-Kit, with tool calls roundtripping through `mcp-proxy.js` → C# MCP Server
6. Attachments (PNG, PDF) are sent as file paths (not base64); permission mode toggles work; sessions persist via Claude Code's own session storage; closing the Tauri window terminates `claude` and Node child cleanly

The pre-existing `Cannot find module 'agent-sdk-stub.js'` errors that F07 left behind in the terminal are **gone** when this feature ships — replaced by real Agent SDK invocation.

## Architecture overview

```
┌─────────────────────────┐    Process.Start    ┌──────────────────────────────┐
│   UNITY EDITOR          │  + env vars         │   TAURI APP (Windows)        │
│   - F07 pin             │ ───────────────────►│  - Rust backend (supervisor) │
│   - C# MCP Server       │  (UNITY_PROJECT_    │  - React frontend            │
│     localhost:8090      │   PATH, UNITY_MCP_  │  - Node child (this feature) │
└──────────▲──────────────┘   HOST/PORT, etc)   └────────┬─────────────────────┘
           │                                              │ Agent SDK message stream
           │ TCP                                          │ (replaces JSON-RPC echo)
           │                                              ▼
           │                                    ┌──────────────────────────────┐
           │                                    │ @anthropic-ai/claude-agent-  │
           │                                    │ sdk (Node, in Tauri's Node   │
           │                                    │ runtime, NOT bundled in MSI) │
           │                                    │  - query() loop              │
           │                                    │  - permission handling       │
           │                                    │  - session management        │
           │                                    │  - attachment encoding       │
           │                                    └────────┬─────────────────────┘
           │                                              │ subprocess
           │                                              ▼
           │                                    ┌──────────────────────────────┐
           │  mcp__game-deck__<tool>            │  claude subprocess           │
           ◄────────────────────────────────────│  (system PATH, NOT bundled)  │
              via Server~/dist/mcp-proxy.js     │  cwd = UNITY_PROJECT_PATH    │
                                                │  plugins: [<Plugin~/>]       │
                                                │  (skills + agents bundled    │
                                                │  via plugin manifest)        │
                                                └──────────────────────────────┘
```

The C# MCP Server (`Editor/MCP/`), the F07 pin, and the launch contract from F07 are unchanged. The Tauri Rust backend's supervisor pattern from F01 is unchanged. **What changes:** the Node child's process target (echo stub → Agent SDK) and the wire format on the Tauri↔Node channel (custom JSON-RPC → Agent SDK's native messages).

## Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| Engine entry | `@anthropic-ai/claude-agent-sdk` (npm, proprietary, Anthropic) | Decision #1. SDK absorbs Windows stdin quirks (ADR-001 validation #4). NOT bundled in MSI — installed on-demand via `npm install` on first launch. |
| Engine subprocess | `claude` (system PATH, proprietary, Anthropic) | NOT bundled in MSI — user installs via official Anthropic docs. Detected via `where.exe claude` on Windows. |
| MCP transport (Tauri ↔ Unity) | Existing `Server~/dist/mcp-proxy.js` (TypeScript, proprietary code in this repo) | Unchanged from F01. Spawned by Claude Code per `mcpServers` config in the SDK's `query()` options. |
| Skills + agents surfacing | `plugins: [{type:"local", path: <package>/Plugin~/}]` | Decision #2 (updated 2026-04-30 after F02 task 3.1 empirical pivot). The SDK's `plugins` option auto-discovers both skills (under `Plugin~/skills/`) and agents (under `Plugin~/agents/`) from the bundled plugin manifest. Replaces the original `--add-dir` + copy-step plan: `additionalDirectories` only grants filesystem read access, not skill discovery. |
| Commands (opt-in user-authored) | `additionalDirectories: ["<unity-project>/ProjectSettings/GameDeck/commands/"]` | Filesystem read access only; commands are a legacy format separate from the discovery-driven plugin mechanism. Skipped silently if directory absent. |
| Auth | Owned by Claude Code (5 methods supported, ADR-001 validation #6) | MCP Game Deck stores zero credentials. Detect-and-redirect only. |
| Session storage | Claude Code's own session machinery | Decision #6. Drops F01's planned `.game-deck-sessions.json`. |
| Permission mode | Surfaced in React UI as a thin wrapper over Claude Code's 5 modes | Decision #5. |

## File layout

**New files:**

```
App~/src-tauri/src/
├── claude_supervisor/                 ← replaces node_supervisor for this feature's scope
│   ├── mod.rs                         ← public surface (spawn, status, shutdown)
│   ├── install_check.rs               ← detect claude + SDK install + auth
│   ├── spawn.rs                       ← Agent SDK spawn with all flags + envs
│   ├── lifecycle.rs                   ← health check, restart, shutdown sequencing
│   └── windows_hygiene.rs             ← Windows-specific subprocess flags (Decision #7)

App~/src/components/
├── FirstRunPanel.tsx                  ← "Claude Code missing / not logged in / SDK installing" surface
└── PermissionModeToggle.tsx           ← (if not already in F01) the 5-mode dropdown

App~/src/ipc/
├── (extends existing commands.ts)     ← + check_claude_install_status,
│                                         start_supervisor, stop_supervisor,
│                                         get_permission_mode, set_permission_mode
└── (extends existing events.ts)       ← + onSupervisorStatusChanged,
                                          onSdkInstallProgress

Server~/                               ← existing mcp-proxy.ts unchanged; rest of Server~ stays deleted

(no new files in Editor/ — F02 is App~ + supervisor only)
```

**Modified files:**

```
App~/src-tauri/src/
├── lib.rs                             ← register claude_supervisor; remove node_supervisor wiring;
│                                         tauri-plugin-shell or process spawn for npm install
├── node_supervisor/                   ← removed (replaced by claude_supervisor)
└── commands/
    ├── connection.rs                  ← rewrite get_node_sdk_status → get_supervisor_status
    └── conversation.rs                ← rewrite send_message to use Agent SDK message stream
                                          (no more JSON-RPC echo path)

App~/src/
├── App.tsx                            ← FirstRunPanel mounts above the layout when supervisor not ready
├── stores/
│   ├── connectionStore.ts             ← rename node_sdk_status → supervisor_status
│   └── conversationStore.ts           ← consume streamed messages (text deltas, tool use blocks,
│                                          tool result blocks) instead of monolithic responses
└── ipc/
    ├── commands.ts                    ← add the new Tauri commands
    └── events.ts                      ← add the new event subscribers

App~/package.json                      ← @anthropic-ai/claude-agent-sdk dep declared but installed
                                          on-demand on first launch (not at build time — see Decision #1)

App~/src-tauri/Cargo.toml               ← (no new deps; spawn uses std::process)
App~/src-tauri/capabilities/default.json ← (review if shell:* permissions are needed for npm install
                                            of the SDK — likely yes for invoking npm via shell)
```

**Files deleted (post-F02):**

```
(none — but the existing references to agent-sdk-stub.js in node_supervisor get removed
naturally when node_supervisor → claude_supervisor swap happens)
```

## Locked decisions (from parent design doc)

The parent doc (`02-claude-code-supervisor.md`) enumerates **7 decisions**. Summary here for executable context:

1. **Engine: `@anthropic-ai/claude-agent-sdk`, both SDK and `claude` as external dependencies.** Not bundled in MSI (Anthropic Commercial Terms don't grant redistribution).
2. **Asset surfacing: skills + agents via SDK `plugins` option, no copy step.** Updated 2026-04-30 after task 3.1 empirical pivot. Both skills and agents live inside `<package>/Plugin~/` (a Claude Code plugin directory with `.claude-plugin/plugin.json` manifest), surfaced to the SDK via `plugins: [{type:"local", path}]`. Replaces the original `--add-dir` + copy-step plan.
3. **10 specialists ship as agents (subagent MCP bug fixed by Anthropic, re-checked 2026-04-29).** Empirical smoke test in Group 3; skills fallback if it fails.
4. **`{{KB_PATH}}`: contextual after plugin pivot.** Original "substitute at copy time" plan obsolete — no copy step. Resolution decided at task 3.2 time when consolidating `Agents~/` into `Plugin~/agents/`.
5. **Permission mode: thin React wrapper over Claude Code's 5 modes.**
6. **Sessions: Claude Code's storage, not ours.** Drops `.game-deck-sessions.json` machinery.
7. **Windows subprocess hygiene: detached process group, UTF-8 encoding, --append-system-prompt for long prompts, normalized paths.**

Read the parent doc for rationale per decision.

## Install-detection contract

The first-run experience needs to know three things, surfaced via a single Tauri command:

```typescript
// In App~/src/ipc/commands.ts
type ClaudeInstallStatus = {
  claudeInstalled: boolean;
  claudeAuthenticated: boolean;  // false when claude /status reports not logged in
  sdkInstalled: boolean;          // @anthropic-ai/claude-agent-sdk in App~/runtime/node_modules/
  claudeVersion: string | null;   // e.g. "2.10.3"; null when not installed
};

export async function checkClaudeInstallStatus(): Promise<ClaudeInstallStatus>;
```

The Rust side (`install_check.rs`) implements:

- **`claudeInstalled`** — `where.exe claude` on Windows; return code 0 → true, exit code 1 → false. Cache per session (refresh on user-triggered action).
- **`claudeAuthenticated`** — spawn `claude /status` non-interactively (or use the SDK's startup probe), parse the output; reports `true` when the SDK can issue a query without auth error.
- **`sdkInstalled`** — `package.json` lookup at `App~/runtime/node_modules/@anthropic-ai/claude-agent-sdk/package.json`. If file exists → true.
- **`claudeVersion`** — `claude --version` parse.

The React `FirstRunPanel.tsx` consumes this and renders one of four states:

1. Everything ready → unmounts; supervisor is started; main UI takes over
2. SDK missing → shows installing progress (driven by `onSdkInstallProgress` event from Rust); auto-advances to state 1 when complete or to state 4 on failure
3. Claude Code missing → shows "Install Claude Code" CTA pointing at `https://docs.claude.com/en/docs/claude-code/setup` (or the canonical install URL)
4. Claude Code installed but not authenticated → shows "Run `claude /login` in a terminal" CTA with copy-button

State 2 → 1 is the happy first-launch path. States 3 and 4 require user action; the panel polls `checkClaudeInstallStatus()` every 5s until the user resolves.

## Spawn contract

When everything checks out, the supervisor spawns the SDK with:

```typescript
// Conceptual; actual Rust-side code marshals these into the SDK's query() options
{
  cwd: process.env.UNITY_PROJECT_PATH,            // from F07's launch contract
  systemPrompt: undefined,                         // F08 overlays via --append-system-prompt later
  mcpServers: {
    "game-deck": {
      command: "node",
      args: [<package>/Server~/dist/mcp-proxy.js],
      env: {
        UNITY_MCP_HOST: process.env.UNITY_MCP_HOST,
        UNITY_MCP_PORT: process.env.UNITY_MCP_PORT,
      }
    }
  },
  permissionMode: <user-selected>,                 // default | acceptEdits | plan | bypassPermissions | auto
  plugins: [
    { type: "local", path: "<package>/Plugin~/" }  // skills + agents bundled, namespaced as mcp-game-deck:<n>
  ],
  additionalDirectories: [
    "ProjectSettings/GameDeck/commands/"           // skipped if directory absent; filesystem read access only
  ],
}
```

The SDK then drives `claude` as a subprocess. Tauri Node child supervises the SDK process, framing/forwarding messages to the React UI via Tauri events.

## Asset surfacing — plugin layout (no copy step)

Decision #2 (updated 2026-04-30 after F02 task 3.1 empirical pivot). The package's `Plugin~/` directory is structured as a Claude Code plugin per the official manifest convention:

```
<package>/Plugin~/
├── .claude-plugin/
│   └── plugin.json          # manifest: name="mcp-game-deck", version, license, etc.
├── skills/
│   ├── architecture-decision/
│   │   └── SKILL.md         # namespaced as mcp-game-deck:architecture-decision
│   ├── code-review/
│   │   └── SKILL.md
│   └── ... (22 skills total)
└── agents/
    ├── unity-shader-specialist.md   # invoked via @agent-mcp-game-deck:unity-shader-specialist
    ├── unity-dots-specialist.md
    └── ... (10 specialists total)
```

The supervisor passes the absolute `Plugin~/` path to the SDK via the `plugins` option (see Spawn contract above). The SDK auto-discovers both skills and agents from the manifest. **No copy step into `<unity-project>/.claude/`. No manifest tracking. No uninstall menu** — when the user removes the UPM package, the plugin and all its skills + agents disappear automatically.

`{{KB_PATH}}` substitution is contextual per Decision #4 — only relevant if an agent text references the Knowledge Base via an absolute path. With the plugin layout, agents can use paths relative to the plugin root or rely on env-var-based resolution at runtime, eliminating the copy-time substitution machinery entirely.

## Wire protocol — Tauri ↔ Node migration

F01 used a custom JSON-RPC 2.0 dialect for the Tauri↔Node channel because the Node side was an echo stub we controlled. F02 replaces the Node side with the Agent SDK, whose own message format is now the wire format on this channel. Specifically:

**Outgoing (Tauri → Node):**
- User prompt → SDK's `query.input(text)` API
- Permission mode change → SDK's `query.setPermissionMode(mode)` API
- Session resumption → SDK's `query.resume(sessionId)` API
- Cancel → SDK's `query.interrupt()` API

**Incoming (Node → Tauri, streamed via stdio + SDK's framing):**
- Text deltas (assistant content)
- Tool use blocks (Claude is calling a tool — pre-permission display)
- Tool result blocks (the tool returned data)
- Permission requests (user needs to approve)
- Session metadata (session id, turn count)
- Errors (auth, network, tool failures)

Tauri marshals these into Tauri events for the React side to consume in `conversationStore`. The React store's existing shape (messages array, current session id, status) stays compatible — only the producer changes.

## Attachment migration

F01's React drag-drop flow built explicit `image` / `document` content blocks with base64. The Agent SDK accepts file paths and handles encoding internally. The migration is mechanical:

1. React continues to capture file paths from drag-drop (already does)
2. Tauri's `send_message` command accepts `(text: string, attachmentPaths: string[])` instead of `(text: string, attachments: AttachmentBlob[])`
3. Tauri forwards paths to the Node child unmodified
4. The Node child passes paths to the SDK
5. The SDK reads, encodes, and sends to `claude`

Decision per ADR-001 validation #7 (needs-local-test): the supervisor normalizes Windows backslashes to forward slashes before passing to the SDK as a defensive measure. Test cases for Group 5: small PNG, multi-MB PDF, Windows path with spaces, Windows path with non-ASCII chars (e.g. `C:\Projetos\maçã\image.png`).

## Lifecycle

The supervisor owns three lifecycle events:

**Startup (Tauri opens):**
1. `install_check.rs` runs `checkClaudeInstallStatus()`
2. If anything missing → React shows `FirstRunPanel`, supervisor stays idle
3. If everything ready → `spawn.rs` invokes the SDK with the spawn contract
4. Health check: send a minimal `query.input("__health__")` after 1.5s; if no response in 5s, surface `supervisor_status: crashed` event
5. On healthy response → emit `supervisor_status: ready`, React unmounts FirstRunPanel

**Restart (user-initiated or after crash):**
1. `lifecycle.rs::shutdown()` — send SIGTERM to Node child + claude subprocess (Windows: `GenerateConsoleCtrlEvent`); wait 2s; SIGKILL if still alive
2. Clear supervisor state in connection store
3. Re-run startup

**Shutdown (Tauri close):**
1. F07's `WindowEvent::CloseRequested` handler from F01 already exists; F02 hooks `lifecycle.rs::shutdown()` into it before `app.exit(0)`
2. Both Node child and claude subprocess terminate cleanly within 2s
3. No zombies (verified via Task Manager post-close on Windows)

## Permission mode UI

Decision #5: thin wrapper. The React component is a dropdown with the 5 modes (`default`, `acceptEdits`, `plan`, `bypassPermissions`, `auto`). On change, fires `setPermissionMode(mode)` Tauri command, which forwards to the SDK. Shift+Tab toggle inside chat is wired via the same code path.

The dropdown reads the current mode via `getPermissionMode()` Tauri command on mount, and listens for `permission_mode_changed` Tauri events (emitted when the SDK reports a mode change from inside `claude`, e.g., user pressed Shift+Tab in the chat input).

## Pre-public-release checks

These are NOT blockers for implementation but must be answered before the first public v2.0 release:

- **License/ToS interpretation** — verify with Anthropic legal that MCP Game Deck's distribution model (no SDK / claude bundling, on-demand npm install at first launch, redirect to Anthropic install docs for Claude Code) is compliant. ADR-001 validation #1 documents the analysis; legal sign-off is a separate step.
- **README + LICENSE update** — add the proprietary-dependency disclosure: "This package requires Claude Code and `@anthropic-ai/claude-agent-sdk` (both proprietary, by Anthropic) installed locally. These are NOT bundled with MCP Game Deck."
- **No Anthropic logos** in marketing without prior approval.

## Definition of done

- User clicks the F07 pin → Tauri opens → if Claude Code or SDK missing/unauthenticated, FirstRunPanel surfaces a clear next step
- Once everything ready, supervisor spawns the SDK pointing at the user's Unity project; smoke prompt round-trips with text response (NOT echo) within 3s
- The 10 Unity specialists are reachable via the `Task` tool with the `mcp-game-deck:` namespace; smoke test (Group 3 task 3.4): invoke `@agent-mcp-game-deck:unity-shader-specialist`, ask it to call an MCP Game Deck tool, confirm response includes the tool result. **If smoke test fails despite the documented Anthropic fix, fall back to skills (provisional plan in tasks)**.
- The 22 generic skills appear in the chat with `mcp-game-deck:` namespace (validated empirically during task 3.1: prompt "List all available skills" returns the 22 + Claude Code built-ins)
- User drags a PNG into chat → image is sent as attachment → Claude Code analyzes it
- User toggles permission mode in the React UI → Claude Code respects it; Shift+Tab inside chat also toggles and the React UI reflects it
- User closes the Tauri window (or Unity Editor with Tauri open) → both child processes terminate cleanly within 2s; no zombies in Task Manager
- The pre-existing `Cannot find module 'agent-sdk-stub.js'` errors that F07 left behind are gone
- README + LICENSE updated with the proprietary-dependency disclosure (or a TODO tracked for the public-release checklist if Ramon wants to defer)
- Smoke test on Windows 11: open app via the F07 pin, run a 3-turn conversation including a tool call and a PNG attachment, close cleanly. Repeat 5 times. No crashes, no auth re-prompts, no orphan processes.

## After Feature 02

The supervisor is the spine for the rest of v2.0:

- **Feature 04 (Interactive Plan Mode)** can register `ask_user` as an in-process tool via the SDK's `@tool` decorator
- **Feature 05 (Permission System Fix)** validates and polishes the surface this feature exposes
- **Feature 06 (Plans CRUD)** can ship `/save-plan` and `/plan-execute` as skills in `Plugin~/skills/`
- **Feature 08 (Rules Page)** can inject rules via `--append-system-prompt`

(The previously-planned "Feature 02b — Specialists as skills" is removed: with the subagent MCP bug fixed by Anthropic, specialists ship as agents directly. If Group 3's smoke test finds the fix incomplete, the rewrite-to-skills work is a normal F02 task, not a separate feature.)

## References

- `02-claude-code-supervisor.md` — parent design doc with the 7 locked decisions
- `02-claude-code-supervisor-tasks.md` — decomposed tasks for execution
- `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md` — architectural parent (with all 7 validations researched)
- `docs/internal/v2-features/01-external-app-spec.md` — predecessor; F02 inherits its supervisor pattern
- `docs/internal/v2-features/07-editor-status-pin-spec.md` — F07 pin contract that F02 consumes
- Claude Agent SDK docs: https://platform.claude.com/docs/en/agent-sdk/overview
- Claude Code subagent docs: https://code.claude.com/docs/en/sub-agents
- Claude Code skills docs: https://code.claude.com/docs/en/skills
- Claude Code authentication docs: https://code.claude.com/docs/en/authentication
