# ADR-001 — Claude Agent SDK as the Tauri engine (Caminho B)

> **Status:** `accepted` — 2026-04-28
> **Supersedes:** original v2.0 architecture for Features 01–08 (custom WebSocket-based Agent SDK Server as the chat engine)
> **Companion:** Distribution A — Unity Package only (see "Distribution" section)
> **Note on numbering:** the file `decisions/001-tauri-over-electron.md` is an unrelated earlier ADR about framework choice. This document lives in `architecture/` and is the canonical "ADR-001" for the engine decision; the two coexist without conflict.

## Context

MCP Game Deck v1.x ships an in-Editor Chat UI (`Editor/ChatUI/`) backed by a custom WebSocket-based Agent SDK Server (`Server~/src/index.ts` + helpers). The server is non-trivial: it owns the agent loop via `@anthropic-ai/claude-agent-sdk` `query()`, manages permission flow with timeout-based callbacks, persists sessions to `.game-deck-sessions.json`, loads agents from `Agents~/` and skills from `Skills~/`, supports multi-modal attachments (images + PDFs), wires the C# MCP server in via `mcp-proxy.js` (stdio MCP server), and resolves a `{{KB_PATH}}` placeholder against `KnowledgeBase~/` at runtime.

The original v2.0 plan (Features 01–08 as written) preserves this architecture and moves the chat UI from inside Unity to a Tauri desktop app. The custom Agent SDK Server stays — Tauri spawns it as a child process and talks to it over stdio + JSON-RPC 2.0, replacing the WebSocket transport. Features 02 (Orchestrator), 03 (Slash Commands), 04 (Interactive Plan Mode), 05 (Permission System Fix) are all implementations on top of this custom server.

External feedback received in late April 2026 surfaced the limitation of this design clearly:

> "The Game Deck MCP server is well-built. The limit is the embedded Chat UI: it acts as its own MCP client with the agents, commands and context that the package decides. There's no way to extend it from the outside. It can't be combined with the user's other MCPs (GitHub, Atlassian, filesystem) and Spec-Kit doesn't run on top of it because it doesn't read the commands and templates Spec-Kit generates in the project. When the client is open — like Claude Code pointing at the Unity MCP — everything unlocks: same agent, same session, multiple MCPs stacked, the user's own slash commands, Spec-Kit, and the project's `CLAUDE.md` as context."

The criticism is not about the MCP server. The 268 tools, the resources, the prompts, the C# side are exactly what the user wanted. The criticism is about the **chat client** being a closed system. Moving that closed system from the Editor to a Tauri window does not fix it; it just relocates the silo.

Two pieces of pre-existing evidence support the direction this ADR takes:

1. The Project Settings page (`Editor/Settings/GameDeckSettingsProvider.cs`) already exposes a "Copy MCP Config to Clipboard" button that generates a Claude Desktop / Claude Code MCP configuration block pointing at `Server~/dist/mcp-proxy.js`. The product already acknowledged "the C# MCP server is independent of the Chat UI; you can plug your own client into it."
2. The roadmap (`docs/internal/roadmap.md`) explicitly justifies v2.0's Claude-only scope by saying "the `@anthropic-ai/claude-agent-sdk` already implements the turn loop, tool-use management, and streaming. Reusing it shaves weeks." That argument generalizes: Claude Code already implements *everything* the custom server is reimplementing, plus orchestration, slash commands, skills discovery, hooks, and multi-MCP composition.

## Decision

The Tauri app does not host a custom Agent SDK Server. It embeds Anthropic's official `@anthropic-ai/claude-agent-sdk` and uses it to spawn Claude Code as a subprocess. Tauri owns the UI, the IPC plumbing to Unity, and the file-system surfaces for plans/rules. Claude Code owns the agent loop, the orchestration, the slash commands, the skills system, the permission machinery, the memory, and the multi-MCP composition.

```
┌─ TAURI APP (App~/) ──────────────────────────────────────────┐
│  React UI (chat rendering, plans panel, settings, pin link)  │
│  Rust backend (Tauri commands, events, file scopes)          │
│                                                               │
│  ┌─ @anthropic-ai/claude-agent-sdk (Node child process) ───┐ │
│  │  spawns and supervises Claude Code in headless mode     │ │
│  │  ┌─ claude (subprocess) ─────────────────────────────┐  │ │
│  │  │  • reads CLAUDE.md from the user's Unity project  │  │ │
│  │  │  • loads .claude/agents/ from --add-dir paths     │  │ │
│  │  │  • loads .claude/skills/ from --add-dir paths     │  │ │
│  │  │  • connects to multiple MCP servers in parallel:  │  │ │
│  │  │      - MCP Game Deck (this product, port 8090)    │  │ │
│  │  │      - any other MCP the user already configured  │  │ │
│  │  │  • runs the user's custom slash commands          │  │ │
│  │  │  • runs Spec-Kit, plugins, hooks, sub-agents      │  │ │
│  │  └────────────────────────────────────────────────────┘  │ │
│  └──────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
              │ TCP localhost:8090 (existing protocol)
              ▼
┌─ UNITY EDITOR (existing, unchanged) ─────────────────────────┐
│  C# MCP Server — 268 tools, 7 resources, 5 prompts            │
│  Toolbar pin (Feature 07)                                     │
│  Project settings, update checker, etc.                       │
└──────────────────────────────────────────────────────────────┘
```

The C# MCP Server inside Unity does not change. The toolbar pin does not change. The TCP communication between Tauri and Unity does not change. The `mcp-proxy.js` (stdio ↔ HTTP bridge) does not change — Claude Code uses it the same way the custom server uses it. What changes is the brain inside the Tauri side — it goes from `Server~/src/index.ts` (custom WebSocket server hosting Agent SDK queries) to Claude Code itself.

## Consequences

### Positive

- **The feedback is resolved at the architectural level.** The user gets a UI dedicated to Unity workflows (Tauri opens via the pin, focused experience) AND gets every capability Claude Code has — multi-MCP composition, custom slash commands, custom skills, sub-agents, plugins, hooks, the user's `CLAUDE.md`, Spec-Kit, web search, code execution, file system tools, memory across sessions. Nothing from the Anthropic ecosystem is lost.
- **Existing assets are preserved without rework.** The 10 agents in `Agents~/` already use the YAML frontmatter Claude Code expects (`name`, `description`, `tools`, `model`, `maxTurns`). The `unity-specialist` even uses `Agent` in its tools list to delegate to the four Unity-specialist subagents (DOTS, Shader, Addressables, UI). The 22 skills in `Skills~/` already use Claude Code's `SKILL.md` convention with `user-invocable` and `allowed-tools`. The `create-command` skill already writes new commands to `ProjectSettings/GameDeck/commands/<n>/SKILL.md` — this output format is exactly what Claude Code consumes from `.claude/commands/` or `.claude/skills/`. No format conversion is needed.
- **Implementation effort drops sharply on Features 02 and 03.** Feature 02 (Orchestrator Agent) becomes zero work — Claude Code orchestrates natively via the same pattern Feature 02 was reinventing. Feature 03 (Slash Commands) also becomes zero work for built-ins (`/plan`, `/clear`, etc) and for user commands; Claude Code reads `.claude/commands/` and `.claude/skills/` natively, with autocomplete built in. The 22 skills already in `Skills~/` become 22 working slash commands the day the package installs.
- **Updates from Anthropic come for free.** New permission modes, better skills, agent improvements ship with each Claude Code release — the Tauri app picks them up on the next bumped version.
- **Strategy is internally consistent with existing product decisions.** The Project Settings "Copy MCP Config" button has been a tacit endorsement of "use any MCP-capable Claude client with this server." This ADR makes Tauri itself one of those clients. The roadmap's argument for reusing Anthropic's SDK applies one level up: reuse Claude Code, not just its SDK.

### Negative

- **Hard dependency on Claude Code's CLI behavior.** Output format changes, breaking releases, or platform-specific bugs land on you. Two specific known issues: Issue #7263 (Windows stdin piping with large inputs) and the Jan 2026 dstreefkerk post documenting subprocess invocation quirks on Windows. Mitigation: pin a tested Claude Code version in package metadata, validate upgrades before bumping, document recovery for users whose local Claude Code drifts.
- **Authentication onboarding becomes an external concern.** Claude Code needs a Claude account (API key or login). MCP Game Deck cannot work without an authenticated `claude` install on the user's machine. First-run UX must surface this clearly, not fail silently.
- **The `{{KB_PATH}}` resolution mechanism breaks under naive `--add-dir` distribution.** The 10 agents in `Agents~/` reference `{{KB_PATH}}/01-unity-project-architecture.md`, `{{KB_PATH}}/04-ecs-dots-performance.md`, etc. Today this is resolved by `Server~/src/system-prompt.ts` and `Server~/src/agents.ts` — `getAgentPrompt()` calls `replaceAll("{{KB_PATH}}", getKnowledgeBasePath())` before returning the body. When the agents are surfaced to Claude Code via `--add-dir`, Claude Code reads the raw file with `{{KB_PATH}}` literal and never resolves it. This needs an explicit fix (see "Validations pending"). Options: pre-process at install time (Tauri rewrites `{{KB_PATH}}` to absolute path on first launch and writes resolved copies to a runtime location), or change the schema to use paths relative to the agent file itself.
- **Some MCP-tool-from-subagent edge cases are documented as buggy in Claude Code.** Issue #25200 reports custom agents not always picking up MCP tools declared in their frontmatter. Issue #34935 is a feature request for better subagent MCP support. For our 10 agents this likely doesn't bite (single MCP server, simple frontmatter), but needs validation in real use, especially because `unity-specialist` delegates to the four other Unity specialists.
- **Licensing of the Agent SDK and Claude Code for embedded distribution must be confirmed before public release.** The MCP Game Deck MIT license does not automatically extend coverage to bundled npm packages. Verify before release: (a) license of `@anthropic-ai/claude-agent-sdk`, (b) Anthropic's Terms of Service for distributing software that embeds Claude Code as a subprocess, (c) any product-bundling restrictions on end-user authentication mechanisms. Treat this as a release blocker, not a planning blocker.
- **The custom server's investment in multi-modal attachments doesn't transfer 1:1 as code.** `index.ts` builds `image` / `document` content blocks with explicit base64 encoding. Claude Code handles attachments differently (paths, drag-drop). Behavior parity is achievable, but the Tauri side will need to send file references the way Claude Code expects rather than reusing the custom server's wire format.

### Neutral

- The C# MCP Server in `Editor/MCP/` is unchanged.
- The toolbar pin (Feature 07) is unchanged.
- `Server~/src/mcp-proxy.ts` (the STDIO ↔ HTTP bridge for external clients) is unchanged. External clients (Claude Desktop, Cursor, Cline) keep working as today via the "Copy MCP Config" button in Project Settings.
- The Unity Package as a delivery mechanism is unchanged.
- The user's Unity project is not modified by installing MCP Game Deck. The user's `.claude/` directory in their project (if they have one) is read but never written.
- Node.js remains a dependency, as it is today for `mcp-proxy.js`. The bar does not rise — it stays.

## Distribution: Unity Package only (Distribution A)

The MCP Game Deck package ships as a Unity Package via Unity Package Manager (the existing channel). The Claude-side assets travel inside the package:

- `Agents~/<agent>.md` — 10 specialist agents (after `{{KB_PATH}}` resolution — see below)
- `Skills~/<skill-name>/SKILL.md` — 22 skills (`architecture-decision`, `bug-report`, `code-review`, `create-command`, `new-feature`, `refactor`, `tech-debt`, `sprint-plan`, and 14 others)
- `Server~/mcp-proxy.js` — the stdio ↔ HTTP bridge to the C# server (unchanged role)
- `KnowledgeBase~/` — 16 reference docs that agents quote via `{{KB_PATH}}` placeholders

When Tauri spawns Claude Code as a subprocess, it passes `--add-dir <package-path>/Agents~/` and `--add-dir <package-path>/Skills~/` so Claude Code discovers our agents and skills without writing anything to the user's `.claude/` directory.

### Loading mechanism

`--add-dir` officially supports `.claude/skills/` discovery in any added directory (per Claude Code docs). For agents, the documented behavior is less explicit. The implementation must handle both cases:

**Primary path:** `--add-dir <package>/Agents~/` and `--add-dir <package>/Skills~/`. If Claude Code picks up agents the same way it picks up skills, this is the only mechanism needed.

**Fallback path:** if `--add-dir` does not surface `.claude/agents/` automatically, the Tauri app copies the agents to `<unity-project>/.claude/agents/gamedeck-<original-name>.md` on first launch (or on a user-triggered "install agents" action), removes them on uninstall, and refuses to overwrite a user file with the same name. The `gamedeck-` prefix avoids namespace collision with user agents.

The decision between primary and fallback is made at implementation time by validating against the installed Claude Code version. This ADR locks the constraint that **agents and skills ship inside the Unity Package and never require the user to install a separate Claude Code plugin or marketplace entry**.

### Why not a Claude Code plugin

Distributing as a Claude Code plugin via marketplace was considered. It was rejected because the C# MCP Server is an Editor-side artifact (Editor scripts, .meta files, package.json, Editor assemblies) that cannot be delivered through the Claude Code plugin mechanism. Splitting installation into two halves (Unity package for the Editor side + Claude Code plugin for the agents/skills) doubles onboarding friction with no compensating gain. Single-package distribution is the simpler path.

## Mapping `Server~/src/index.ts` to Claude Code equivalents

The custom server is non-trivial. This table tracks what each capability becomes in the new architecture.

| Capability in `index.ts` | Equivalent in Claude Code | Migration cost |
|---|---|---|
| `query()` agent loop with `mcp-proxy.js` as MCP server | Identical — Claude Code spawns the same MCP server via its `--mcp-config` or settings file | Zero (configuration only) |
| `createCanUseTool()` permission callback with `permission_request` ↔ `permission_response` over WebSocket | Claude Code's permission system: `default` / `acceptEdits` / `plan` / `bypassPermissions` / `auto` (v2.1.85+). User toggles via Shift+Tab or programmatic mode. | Tauri UI surfaces the mode; permission decisions happen inside Claude Code |
| `getAgentPrompt(AGENTS, agentName)` with `--- name --- ` frontmatter parser | Claude Code reads `.claude/agents/<n>.md` natively using the same frontmatter | Zero (with `{{KB_PATH}}` caveat below) |
| `getSkillPrompt(skills, command)` reads `SKILL.md` from `Skills~/` and `ProjectSettings/GameDeck/commands/` | Claude Code reads `.claude/skills/<n>/SKILL.md` and `.claude/commands/<n>.md` natively. User commands (from `create-command` skill) already land in `ProjectSettings/GameDeck/commands/` — Tauri can pass that as `--add-dir` too. | Zero |
| Multi-modal attachments (`image` / `document` content blocks with base64) | Claude Code accepts file paths and handles attachments internally | Tauri sends paths, not base64 wire payloads |
| Session persistence (`.game-deck-sessions.json`, MAX_SESSIONS=100, 30-day TTL) | Claude Code has session resumption (`resume: sessionId`) and its own memory machinery; sessions live in Claude Code's storage | Custom session list UI in Tauri can either read from Claude Code's session API or be dropped if not central to UX |
| `cancelQuery(ws)` with `query.interrupt()` | `query.interrupt()` exists in the SDK identically | Zero |
| System prompt with `{{KB_PATH}}` resolution via `getSystemPrompt()` + `getKnowledgeBasePath()` | **No equivalent** — Claude Code reads the raw file | Needs explicit pre-processing step (see Validations) |
| `loadAgents(cwd)` searching package root + `process.env.PACKAGE_DIR` | Replaced by `--add-dir` (or fallback copy) | Zero — discovery is Claude Code's job |
| `isMcpReachable()` health check on `http://localhost:8090` | Claude Code surfaces MCP server status via `/mcp` slash command | Tauri pin already polls TCP for color-coded status; Claude Code's own status is a secondary signal |
| WebSocket protocol (`messages.ts`: `PromptAction`, `AgentsMessage`, etc.) | Replaced by Agent SDK's typed message stream | Whole `messages.ts` file is dropped |
| `agentsToInfo()` / `skillsToCommands()` wire-format converters | Replaced by Claude Code's `system/init` event which lists available commands and agents | Whole feature dropped |

What remains in `Server~/` after the migration:
- `mcp-proxy.js` (or the TS source compiled to it) — keeps its role as the bridge between Claude Code's stdio MCP transport and the C# HTTP server.
- `prompts/core-system-prompt.md` — kept, but its loading moves from `system-prompt.ts` to wherever Tauri injects extra system prompt content into Claude Code (via `--append-system-prompt` flag or equivalent).
- `agents.ts`, `sessions.ts`, `messages.ts`, `system-prompt.ts`, `index.ts`, `config.ts`, `constants.ts`: all of these become unused for the chat path. They may stay in the repo as historical reference until the v2.0 cleanup pass, then go away.

## What changes in the codebase

### Removed (no longer used)

- `Editor/ChatUI/` — entire folder. Already scheduled for deletion by Feature 07 cleanup (decision #2). The pin replaces it.
- `Server~/src/index.ts` — the custom WebSocket server.
- `Server~/src/messages.ts` — WebSocket protocol types.
- `Server~/src/agents.ts` — agent loader (Claude Code does this).
- `Server~/src/sessions.ts` — session persistence (Claude Code does this).
- `Server~/src/system-prompt.ts` — `{{KB_PATH}}` resolution via the chat path. (Pre-processing for `--add-dir` distribution lives elsewhere — see Validations.)
- `Server~/agent-sdk-stub.js` — the placeholder that referenced the WebSocket server.

### Kept

- `Editor/MCP/` — C# MCP Server, 268 tools. Unchanged.
- `Editor/Pin/` — Feature 07 work in progress. Unchanged.
- `Editor/Resources/`, `Editor/Tools/`, `Editor/Prompts/` — unchanged.
- `Editor/Utils/UpdateChecker.cs` — kept; banner removal already scheduled by Feature 07 cleanup.
- `Server~/src/mcp-proxy.ts` (compiled to `mcp-proxy.js`) — kept; it's the bridge that Claude Code uses to reach the C# server, same as it bridges Claude Desktop today.
- `Server~/prompts/core-system-prompt.md` — kept; loaded by Tauri at Claude Code spawn time.
- `Agents~/<10 markdowns>` — kept; surfaced via `--add-dir` (with `{{KB_PATH}}` pre-processing).
- `Skills~/<22 skills>` — kept; surfaced via `--add-dir`.
- `KnowledgeBase~/<16 docs>` — kept; referenced by agents.
- `App~/src/` — React frontend scaffold. Routing, stores, IPC bindings, layout, Tailwind, components — all reusable.
- `App~/src-tauri/src/unity_client/` — TCP client to the C# server. Unchanged protocol, unchanged code.
- `App~/src-tauri/src/commands/` — Tauri command surface for the React side. Mostly unchanged contracts.

### Needs revision (existing files)

- `Editor/Settings/GameDeckSettings.cs` — field `_agentPort = 9100` becomes obsolete (no Agent SDK Server listening on a port anymore). Remove or repurpose.
- `Editor/Settings/GameDeckSettingsProvider.cs` — `Agent Server Port` field disappears alongside the underlying setting. The `Copy MCP Config to Clipboard` button stays (still useful for users who want to plug Claude Desktop or Cursor into the same server). The `UpdateChecker` banner is already scheduled for removal by Feature 07.
- `App~/src-tauri/src/node_supervisor/` (planned in Feature 01 spec) — its target shifts from "spawn `Server~/dist/index.js`" to "spawn Claude Code via the Agent SDK." Supervision pattern stays (spawn / monitor / restart / clean shutdown). The protocol it speaks across stdio shifts from custom JSON-RPC 2.0 to whatever the Agent SDK exposes.
- `App~/src/ipc/types.ts` — message and event types may shift to mirror Agent SDK message types more directly, instead of mirroring the custom WebSocket protocol.

### Needs revision (`docs/internal/`)

- `docs/internal/v2-architecture.md` — the diagram and "Process layout" section show the third process as "Agent SDK Server (TypeScript / Node) — orchestrator agent / subagents / permission resolver." It's now Claude Code itself. The four open architectural questions in v2-architecture also need revisiting:
  - First-run experience — now intersects with Claude Code authentication.
  - Auto-update of the app — Claude Code has its own update path; the Tauri shell still updates via package version.
  - Multi-Unity — already addressed by Feature 07's project-scoped Tauri instance ID.
  - Claude API key — handled by Claude Code's own credential storage; the Tauri app does not store API keys.
- `docs/internal/roadmap.md` — Feature 01 status row should note that the echo-stub design has been superseded by ADR-001 (the implementation done in April 2026 is still useful as Tauri scaffold validation, but the Node side it pointed at is being replaced).
- The feature mapping table below replaces the implicit dependency graph in the `v2-features/` headers.

## Mapping to existing v2.0 features

Each existing feature spec is in one of three states relative to ADR-001. The classifications below are based on having read the full content of every spec, not on inference.

| # | Feature | State after ADR-001 | What changes |
|---|---|---|---|
| 01 | External App (Tauri) | **Revise (small)** | Stack stays. Layout stays. The diagram's third process changes name (custom Node Agent SDK Server → Claude Agent SDK + Claude Code subprocess). The "Tauri ↔ Node Agent SDK protocol" section is rewritten to reference the Agent SDK protocol. Definition-of-done item 5 (Node SDK echo test) is reframed as a Claude Code spawn smoke test. Item 9 (Feature 02 plug-in point) is dropped. The MSI 2.93 MB ships as scaffold; the engine swap is the next implementation slice. |
| 02 | Orchestrator Agent | **Superseded** | Claude Code orchestrates natively. The 10 agents in `Agents~/` keep their role — `unity-specialist` already declares `Agent` in its tools list and delegates to the four other Unity specialists, which is exactly Claude Code's `Task` / `Agent` pattern. The custom `delegate` tool, the React UI for delegation tree, the `/agent <n>` slash command, the tool budget machinery — all unnecessary. Spec is archived as historical reference. Feature itself is closed without execution. |
| 03 | Slash Commands | **Superseded** | Built-in commands (`/plan`, `/clear`, etc.) ship with Claude Code. User commands in `.claude/commands/` and `.claude/skills/` are loaded natively. Autocomplete is built-in. The 22 skills in `Skills~/` become 22 working slash commands the day the package installs. The `create-command` skill keeps writing to `ProjectSettings/GameDeck/commands/`, which Tauri also exposes via `--add-dir`. Spec is archived. |
| 04 | Interactive Plan Mode | **Revise (medium)** | Claude Code's plan mode (Shift+Tab cycle, `permissionMode: "plan"`) covers the "stop and wait" half. The `ask_user` tool with three response types (single-select / multi-select / free-text) is the original contribution of this feature. Implementation pivots: instead of adding `ask_user` to a custom server, register it as an in-process tool via the Agent SDK's `@tool` decorator (or its TS equivalent) when Tauri configures the SDK. Question card UI in React is unchanged work. |
| 05 | Permission System Fix | **Mostly superseded** | The bug being fixed lived inside the custom server's permission resolver. Claude Code's permission system (default / acceptEdits / plan / bypassPermissions / auto) covers all five test cases listed in the spec ("set auto → no prompt", "set ask → prompt", "set plan → no permission prompts during planning", "switch ask → auto mid-message", "/auto slash command"). The mode switcher in the React UI becomes a thin wrapper over Claude Code's mode. The original audit-and-refactor plan against the custom server is dropped. What remains: surface Claude Code's mode in the Tauri UI and verify the five behaviors work end-to-end. |
| 06 | Plans CRUD | **Revise (small)** | Storage location (`ProjectSettings/GameDeck/plans/*.md`) stays. Plans tab UI stays. The "re-execute" mechanism shifts from invoking the custom orchestrator to feeding the plan content into Claude Code (e.g., `/plan-execute <name>` slash command shipped in `Skills~/` that reads the file and submits it as the user message). `/save-plan` becomes a skill we ship alongside the others, mirroring the `create-command` pattern. |
| 07 | Editor Status Pin | **Unchanged** | Unity Editor side. Independent of the chat engine. The seven locked decisions (binary distribution, cleanup scope, single-instance via tauri-plugin-single-instance, multi-project port collision, toolbar reflection mount, right-click menu, orphan Tauri behavior) all stand. Feature is in flight at the time of this ADR (Group 2 complete; pin in progress). |
| 08 | Rules Page | **Revise (medium)** | Storage location (`ProjectSettings/GameDeck/rules/*.md`) stays. The Rules tab in Tauri stays. The mechanism for loading enabled rules into the agent's system prompt shifts: instead of the custom server reading rules and injecting them into its own system prompt, Tauri injects rule content into Claude Code via `--append-system-prompt` (CLI flag) or by writing them as a project-level `CLAUDE.md` overlay that Claude Code reads naturally. The `applies-to` field's per-subagent filtering may need rethinking under Claude Code's subagent model. |
| 09 | Design Handoff | **Unchanged** | Pure design / asset work. Independent of engine choice. |

## Validations: status (research done 2026-04-28)

The seven validations listed below were researched via Anthropic documentation and GitHub issue tracking on April 28, 2026. Each entry now carries a status: **resolved** (confirmed by docs/issues), **decision-required** (research done, choose between options), or **needs-local-test** (only a hands-on test on a real machine can confirm).

### 1. License of `@anthropic-ai/claude-agent-sdk` and Anthropic ToS — **resolved (with constraints)**

Researched 2026-04-28 reading npm package metadata, GitHub LICENSE files, and Anthropic Commercial Terms of Service.

**License findings:**

- `@anthropic-ai/claude-agent-sdk`: license field is `"SEE LICENSE IN README.md"`. README states: *"Use of this SDK is governed by Anthropic's Commercial Terms of Service."* This is a **proprietary license**, not MIT/Apache.
- `@anthropic-ai/claude-code`: license field is `"SEE LICENSE IN README.md"`. LICENSE.md states literally: *"Use is subject to Anthropic's Commercial Terms of Service. © Anthropic PBC. All rights reserved."* Also proprietary. Anthropic is deprecating npm install in favor of `curl install.sh` / Homebrew / WinGet.
- `@anthropic-ai/sdk` (the plain HTTP client, not Agent SDK): MIT. Not what we use.

**Anthropic Commercial Terms findings (key clauses):**

- **A.1 (Overview):** Permission to *"power products and services Customer makes available to its own customers and end users."* → Building MCP Game Deck on top of Claude Code is permitted in principle.
- **D.4 (Use Restrictions):** Cannot *"build a competing product or service ... or resell the Services."* → MCP Game Deck is neither a competitor to Claude Code nor a resale wrapper. It's a Unity-specific orchestration layer that uses Claude Code. Permitted.
- **Agent SDK docs explicit prohibition:** *"Unless previously approved, Anthropic does not allow third party developers to offer claude.ai login or rate limits for their products."* → We don't authenticate users; Claude Code does. We're in the permitted case.
- **Supported Regions Policy:** Brazil is on the list. Confirmed via official Anthropic supported-countries page.

**Distribution constraints (the actually consequential finding):**

Bundling either `@anthropic-ai/claude-agent-sdk` or the `claude` binary inside the MSI is redistribution of proprietary Anthropic software. The Commercial ToS does not explicitly grant redistribution rights. Three distribution options were evaluated:

- (a) Bundle the SDK in `App~/node_modules` inside the MSI — redistribution. **Avoid.**
- (b) Bundle the `claude` binary in the MSI — redistribution + Anthropic is deprecating npm install. **Avoid.**
- (c) Both as external dependencies; install on-demand via npm on first launch; detect Claude Code on PATH and redirect to install docs if missing. **Recommended and locked.**

**Implication for the architecture:**

- The Tauri app **does not bundle** the Agent SDK or Claude Code
- On first launch, the supervisor checks for the `@anthropic-ai/claude-agent-sdk` package in its Node runtime; if missing, runs `npm install @anthropic-ai/claude-agent-sdk` to a managed location
- Claude Code is detected on PATH; if missing, the React UI shows a clear "install Claude Code first" CTA pointing at Anthropic's installation docs
- README and LICENSE for MCP Game Deck must state: this package requires Claude Code and `@anthropic-ai/claude-agent-sdk` (both proprietary, by Anthropic) installed locally; these are NOT bundled
- Marketing materials may say "powered by Claude Code" but cannot use Anthropic's logos or imply official partnership without prior approval

**Pre-public-release checklist:**

- [ ] Verify the above interpretation with Anthropic legal or via the support channel before first public release (cheap reassurance)
- [ ] README and LICENSE updated with the proprietary-dependency disclosure
- [ ] No Anthropic logos in marketing materials unless approval obtained
- [ ] Brazil residency for the `Customer` (you) confirmed compliant under Anthropic, PBC (US) terms (not the EU/UK Anthropic Ireland Limited)

### 2. `--add-dir` behavior for `.claude/agents/` — **resolved (NEGATIVE)**

Claude Code's official subagent docs are explicit:

> "Project subagents are discovered by walking up from the current working directory. **Directories added with --add-dir grant file access only and are not scanned for subagents.**"

The skills doc confirms the same exception:

> "The --add-dir flag grants file access rather than configuration discovery, but **skills are an exception**: .claude/skills/ within an added directory is loaded automatically. Other .claude/ configuration such as **subagents, commands, and output styles is not loaded from additional directories**."

**Conclusion:** the "primary path" via `--add-dir` does NOT work for agents. The fallback path (copying agents into `<unity-project>/.claude/agents/gamedeck-<n>.md` on first launch) is mandatory, not optional. Skills work via `--add-dir` as written. Slash commands also need the fallback path.

**Implication for the architecture:** the "Loading mechanism" section above is updated:

- **Skills:** `--add-dir <package>/Skills~/` works. Use it.
- **Agents:** copy on first launch to `<unity-project>/.claude/agents/gamedeck-<n>.md`. Track which files were written so they can be removed on uninstall, and refuse to overwrite a user file with the same name.
- **Commands:** if Tauri ever needs to surface user commands from `ProjectSettings/GameDeck/commands/`, also copy them into `.claude/commands/` (with the same `gamedeck-` prefix discipline).

### 3. `{{KB_PATH}}` resolution under the new distribution — *decision-required*

Three options listed previously, plus a fourth surfaced by the answer to #2:

- (a) Tauri writes resolved copies of all 10 agents to a runtime location on first launch and points the install at that location.
- (b) Rewrite the agents to use paths relative to the agent file (e.g., `../KnowledgeBase~/01-...md`).
- (c) Inject `KB_PATH` via environment variable that all agents pick up uniformly. **Risky:** Claude Code reads the agent body raw; unless we pre-process, env var substitution doesn't happen automatically.
- (d) **NEW:** since #2 forces a copy step anyway (agents must land in `<unity-project>/.claude/agents/`), do `{{KB_PATH}}` substitution as part of that same copy step. Single pass: read template from package, substitute `{{KB_PATH}}` to the package's `KnowledgeBase~/` absolute path, write to `.claude/agents/gamedeck-<n>.md`. No new mechanism, just one more step in the same loop.

**Recommendation:** option (d). It piggybacks on the copy step that #2 already requires, doesn't introduce new schemas, and keeps the package's source-of-truth `Agents~/<n>.md` files unchanged.

### 4. Windows stdin piping bugs — **resolved (workarounds known)**

Four relevant issues found; all open or unresolved in current Claude Code as of late April 2026:

- **Issue #7263** — `claude -p` returns empty output for stdin input >7000 chars. Linux platform tag, but stdin/buffering issue likely cross-platform.
- **Issue #36156** — On Windows, hooks receive `process.stdin.isTTY === true` instead of a pipe; tool input never received. Specific to hook commands.
- **Issue #46601** — Windows Stop hook receives empty stdin; same root cause.
- **Issue #5925** — "Raw mode is not supported" when piping (Linux). Ink (the React renderer) requires raw mode.

**dstreefkerk's January 2026 documentation** of Python-based subprocess invocation on Windows confirms the workarounds:

- Use `CREATE_NEW_PROCESS_GROUP` flag on Windows (`subprocess.CREATE_NEW_PROCESS_GROUP`).
- Pass user prompts via stdin (avoids command-line argument length limits).
- Set `encoding="utf-8"` (default cp1252 breaks on emoji/non-ASCII output).
- Long system prompts via `--system-prompt` flag are unstable; pass them as part of the user prompt body or via `--append-system-prompt` instead.
- Don't nest agents inside `claude -p` runs invoked from a parent script (sub-agents hang on permission prompts when parent has `--dangerously-skip-permissions`).

**Implication for the architecture:** the Tauri Node supervisor talks to the Agent SDK (not directly to `claude -p`), so most of these issues don't bite us directly. But we should:

- Pin a known-good Claude Code version in package metadata.
- Add a startup health check that runs a small `query()` round trip and surfaces failure clearly to the user.
- For attachment paths (Windows backslashes), normalize before sending.
- Document a recovery procedure in the README for users whose local Claude Code drifts to a broken version.

### 5. Custom subagent MCP tool resolution — **resolved (KNOWN BUG, not yet fixed)**

Multiple open issues confirm a pattern: **custom subagents in `.claude/agents/` cannot reliably call MCP tools**. Built-in subagents (e.g., `general-purpose` invoked via `Task`) and globally-configured MCP servers DO work. The failure modes:

- **Issue #25200** (Feb 2026, open) — MCP tools declared in agent frontmatter (`tools:`, `mcpServers:`) are not injected into the subagent's tool inventory at all. Agent never attempts to call them.
- **Issue #13898** (Dec 2025) — Custom subagents cannot call project-scoped MCP servers (`.mcp.json` local). They hallucinate plausible-looking results instead of erroring.
- **Issue #13605** (Dec 2025) — Custom plugin subagents cannot access MCP tools; built-in agents can.
- **Issue #13254** (Dec 2025) — Background subagents (`run_in_background: true`) cannot access MCP tools. Setting `run_in_background: false` works.
- **Issue #6915** (Aug 2025) — Feature request for inverse problem (limiting MCP tools to subagents only).

**Common workaround that works:** built-in `general-purpose` subagent invoked via `Task` tool with explicit tool names in the prompt body (`"Use mcp__game-deck__<tool> to ..."`). The 10 specialized agents in `Agents~/` won't reliably reach MCP Game Deck tools through the broken path.

**Implication for the architecture:** this is a serious risk. Three options to evaluate during implementation:

- (a) Wait for Anthropic to fix the bug; ship with a single "Unity Specialist" general-purpose-style agent for v2.0; bring back the 10 specialists once subagent MCP works.
- (b) Rewrite the 10 specialists as **skills** instead of agents — skills don't have the same MCP injection bug because they're surfaced via the main thread's tool inventory. Lose context isolation; gain reliability.
- (c) Ship the 10 specialists as agents anyway, accept that they won't always reach MCP tools, and document the limitation. Power users invoke them with explicit tool names in prompts.

**Recommendation (provisional):** option (b). Skills are more reliable today and align with how `Skills~/` already ships 22 working skills. The 10 specialists become 10 more skills. Subagent capability returns when the bug is fixed; until then, no degraded UX.

### 6. Authentication onboarding flow — **resolved (no auth code in our app)**

Claude Code authentication is fully owned by Claude Code and supports five methods:

- **Claude.ai subscription OAuth** (default for Pro/Max/Team/Enterprise users; `/login` interactive flow).
- **`ANTHROPIC_API_KEY` env var** (Console-issued API key; pay-as-you-go).
- **`ANTHROPIC_AUTH_TOKEN` env var** (Bearer token, e.g. for LLM gateways).
- **`apiKeyHelper` script** (dynamic/rotating credentials).
- **`CLAUDE_CODE_OAUTH_TOKEN` env var** (long-lived OAuth, e.g. CI; generated via `claude setup-token`).
- **Cloud provider auth** (Bedrock, Vertex AI, Microsoft Foundry; set vendor env vars).

**Implication for the architecture:** MCP Game Deck **stores and proxies zero credentials**. The Tauri app's first-run onboarding only needs to:

- Detect whether `claude` is installed (`which claude` / `where claude` / probe PATH).
- Detect whether the user is authenticated (run `claude --version` then `claude /status`, or rely on the SDK's startup signal).
- If not authenticated, surface a clear "please log in to Claude Code first" call to action. Open a docs link or terminal hint, but never collect credentials in our UI.
- Do not interact with `ANTHROPIC_API_KEY` / `ANTHROPIC_AUTH_TOKEN` / OAuth flow ourselves.

Feature 01's existing first-run experience needs to add this detect-and-redirect step.

### 7. Multi-modal attachment migration — *needs-local-test*

The existing custom server builds explicit `image` / `document` content blocks with base64 encoding. The Claude Agent SDK accepts file paths and handles attachment encoding internally. The migration is mechanical — replace base64 wire payloads with file path references in messages — but two practical questions remain:

- Does the SDK accept Windows-style paths (`C:\path\to\file.png`) or do we need to normalize to forward slashes?
- Are there size limits on attachments before the SDK falls back to a different transport?

**Action:** when implementing the Tauri attachment flow (Feature 02 = Claude Code Supervisor, see roadmap), test with: a small PNG, a multi-MB PDF, a Windows path with spaces, a Windows path with non-ASCII characters. Document findings in the supervisor module.

## Cross-cutting impacts

- `Editor/Settings/GameDeckSettings.cs` `_agentPort` field becomes dead and is removed in the v2.0 cleanup pass.
- `Editor/Settings/GameDeckSettingsProvider.cs` "Agent Server Port" UI row removed alongside.
- `Server~/src/constants.ts` constants `DEFAULT_AGENT_PORT`, `MAX_RETRIES`, `RETRY_BASE_DELAY_MS`, etc. become obsolete (the entire retry/backoff machinery was for the WebSocket connection between Unity Chat UI and the custom server, which doesn't exist anymore). MCP-proxy-related constants (`MCP_SERVER_NAME`, `MCP_SERVER_VERSION`, `MCP_SERVER_ID`, `AUTH_TOKEN_FILE`) stay.
- The `_defaultModel = "claude-sonnet-4-6"` field in `GameDeckSettings.cs` becomes informational only. Claude Code's own model selection takes over (configured by the user, or set per-agent in frontmatter).

## References

- External feedback (April 2026, paraphrased) — the message that triggered this rewrite.
- `docs/internal/v2-architecture.md` — predecessor cross-cutting doc; needs update to align with this ADR.
- `docs/internal/roadmap.md` — milestone structure; Feature 01 status note needs update.
- `docs/internal/v2-features/01-external-app-spec.md` and `01-external-app.md` — predecessor specs; require small revision per table above.
- `docs/internal/v2-features/02-orchestrator-agent.md` — superseded.
- `docs/internal/v2-features/03-slash-commands.md` — superseded.
- `docs/internal/v2-features/04-interactive-plan-mode.md` — needs medium revision.
- `docs/internal/v2-features/05-permission-system-fix.md` — mostly superseded; small surface remaining.
- `docs/internal/v2-features/06-plans-crud.md` — needs small revision.
- `docs/internal/v2-features/07-editor-status-pin.md` and companion spec/tasks — unchanged by this ADR.
- `docs/internal/v2-features/08-rules-page.md` — needs medium revision.
- `docs/internal/v2-features/09-design-handoff.md` — unchanged.
- `docs/internal/decisions/001-tauri-over-electron.md` — separate, earlier ADR; coexists.
- `Server~/src/index.ts`, `agents.ts`, `sessions.ts`, `messages.ts`, `system-prompt.ts`, `config.ts`, `constants.ts` — codebase audited for this ADR.
- `Editor/Settings/GameDeckSettings.cs`, `GameDeckSettingsProvider.cs` — codebase audited for this ADR.
- `Editor/Utils/UpdateChecker.cs` — codebase audited for this ADR.
- `Agents~/unity-specialist.md`, `gameplay-programmer.md`, `unity-shader-specialist.md` — sampled for format and pattern; remaining 7 follow same convention.
- `Skills~/create-command/SKILL.md`, `new-feature/SKILL.md` — sampled; 22 skills total in same format.
- Claude Code subagents documentation: https://code.claude.com/docs/en/sub-agents
- Claude Code skills documentation: https://code.claude.com/docs/en/skills
- Claude Agent SDK headless mode: https://code.claude.com/docs/en/headless
- Issue #25200 — custom agents and MCP tools: https://github.com/anthropics/claude-code/issues/25200
- Issue #34935 — feature request, MCP in subagents: https://github.com/anthropics/claude-code/issues/34935
- Issue #7263 — Windows stdin piping bug: https://github.com/anthropics/claude-code/issues/7263
- dstreefkerk on Windows + Claude Code subprocess: https://dstreefkerk.github.io/2026-01-running-claude-code-from-windows-cli/
