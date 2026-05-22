<h1 align="center">MCP Game Deck</h1>

<p align="center">
  <strong>Claude Code, plugged straight into the Unity Editor.</strong>
</p>

<p align="center">
  <code>273 MCP Tools</code> &nbsp;&middot;&nbsp;
  <code>10 Specialist Agents</code> &nbsp;&middot;&nbsp;
  <code>24 Slash Commands</code> &nbsp;&middot;&nbsp;
  <code>16-doc Knowledge Base</code> &nbsp;&middot;&nbsp;
  <code>External Desktop App</code>
</p>

<p align="center">
  v2.0.0 &nbsp;&bull;&nbsp; Unity 6+ &nbsp;&bull;&nbsp; Requires Claude Code &nbsp;&bull;&nbsp; Windows / macOS / Linux
</p>

---

## What is MCP Game Deck?

MCP Game Deck is a Unity Editor extension that turns Claude Code into a first-class collaborator inside your project. It exposes 273 MCP tools that cover everything from scene editing and prefab authoring to builds, profiling, ProBuilder mesh work, lighting, animation, and ScriptableObject data flows — and pairs them with a dedicated desktop chat app, persistent plans, project rules, and 10 Unity-specialist subagents.

**It is not an AI itself.** It is the bridge that lets Claude Code see, create, modify, and inspect your Unity project — and a polished home for that conversation outside the Editor process.

---

## Highlights

### A real chat home outside Unity

The chat moved out of the Editor in v2.0. A native desktop app (built with Tauri) replaces the in-editor window: it survives assembly reloads, stays alive across Unity restarts, and can run in parallel with the Editor without fighting it for focus.

The Unity package launches the app from a toolbar pin. First click downloads a signed binary from the GitHub Release, verifies its SHA-256, and spawns it pointed at your project. After that the pin is just a focus shortcut.

### Persistent plans, persistent rules

Plans and rules are first-class citizens in v2.0, stored as markdown in `ProjectSettings/GameDeck/`. Plans are full markdown documents with structured frontmatter — write one, execute it through the chat, edit and re-run as the project evolves. Rules are short directives that get injected into Claude's system prompt every turn, capped at 10 active at a time so they stay deliberate.

### Per-turn model picker

Switch between Opus, Sonnet, and Haiku without restarting a session. The picker syncs with the active Claude Code installation — whatever the SDK exposes at init time is what you can pick.

### Permission modes you actually want

Four real options: **Ask** (default), **Auto-edit** (file edits flow without prompts), **Plan** (read-only — Claude proposes, never writes), and **Free** (no permission gates). The current mode lives in the HUD strip, switchable mid-session.

### Knowledge base, in the app

Sixteen curated Unity reference documents are bundled with the package and surfaced in a dedicated Library tab — full-text search with match highlights, markdown rendered with GitHub-Flavored Markdown extensions. Cite a doc in chat, open it in a panel, find the section you need.

### Unity-specialist subagents

Ten domain experts ship as Task-spawnable subagents: `unity-specialist`, `unity-ui-specialist`, `unity-shader-specialist`, `unity-dots-specialist`, `unity-addressables-specialist`, `gameplay-programmer`, `systems-designer`, `technical-artist`, `performance-analyst`, `qa-lead`. Claude routes work to them automatically based on context.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Unity Editor (your project)                                    │
│                                                                  │
│    ┌──────────────┐    ┌──────────────────────────────────┐    │
│    │ Toolbar Pin  │──▶ │  C# MCP Server (TCP :8090)       │    │
│    │ (launches    │    │   • 273 tools, 2 resources,       │    │
│    │  external    │    │     6 prompts                     │    │
│    │  app)        │    │   • Main-thread dispatcher        │    │
│    └──────┬───────┘    │   • Token-based auth              │    │
│           │            └────────────────▲─────────────────┘    │
└───────────┼──────────────────────────────┼─────────────────────┘
            │ spawn + env contract         │ HTTP JSON-RPC
            ▼                              │
┌─────────────────────────────────────────────────────────────────┐
│  MCP Game Deck Desktop App  (Tauri, signed .exe / .msi / .dmg) │
│                                                                  │
│    ┌──────────────┐   ┌──────────────────┐   ┌─────────────┐  │
│    │ React + Vite │   │ Rust supervisor  │   │ MCP proxy   │  │
│    │ UI: chat,    │◀─▶│ spawns Claude    │──▶│ (esbuild    │  │
│    │ plans, rules │   │ Code via SDK     │   │  bundled    │  │
│    │ library      │   │                  │   │  .cjs)      │  │
│    └──────────────┘   └────────┬─────────┘   └──────┬──────┘  │
│                                │ AgentMessage         │ STDIO ▲│
│                                ▼  protocol            │      ││
│                         ┌─────────────────────────────┴───┐  ││
│                         │  @anthropic-ai/claude-agent-sdk │  ││
│                         │  → Claude Code CLI              │──┘│
│                         └─────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

The Unity package is the thin connector: a C# MCP server that lives inside the Editor process, a toolbar pin that launches the desktop app, and a `Plugin~/` directory of Claude Code plugins (agents, skills, knowledge). The desktop app is the chat surface and the host for the Claude Code CLI session.

The MCP-standard interface on `:8090` stays open, so any MCP-compatible client (Claude Desktop, Cursor, Cline, etc.) can connect directly without going through the desktop app.

---

## Quick start

1. **Install Claude Code** ([instructions](https://docs.claude.com/en/docs/claude-code/setup)) and authenticate it (`claude /login`).
2. **Install the Unity package** via Package Manager → Add package from git URL:
   ```
   https://github.com/RamonBedin/mcp-game-deck.git
   ```
3. **Open your project in Unity 6**. The MCP server starts automatically. A pin appears on the Editor toolbar.
4. **Click the pin** → "Open Chat". The desktop app downloads on first use (~30 MB) and launches.
5. **Send a message.** "What's in the active scene?" "Create a 10×10 ProBuilder cube and add a Rigidbody." "Run the EditMode tests and summarize failures."

That's it. The pin is your entry point from there on.

---

## Features

### 273 MCP Tools

Programmatic control over the Unity Editor, organized across 41 categories. The chat exposes every tool to Claude — names like `mcp__game-deck__gameobject-create` or `mcp__game-deck__probuilder-extrude-faces` show up as auto-discovered tools at session init.

<details>
<summary><strong>View all tool categories</strong></summary>

| Category | Description |
|----------|-------------|
| **GameObject** | Create, find, get, update, delete, duplicate, select, set parent, look-at, move relative |
| **Component** | Add, get, update, remove, list components on GameObjects |
| **Transform** | Move, rotate, scale, get transform info |
| **Scene** | Create, load, save, unload, delete scenes; get info/hierarchy; list scenes; frame objects |
| **Prefab** | Create, instantiate, open, modify, save, close prefabs; get prefab info |
| **Asset** | Create, copy, move, rename, delete assets; inspect/set import settings; refresh AssetDatabase |
| **Script** | Create, read, update, delete C# scripts; apply targeted edits; validate compilation |
| **Editor** | Play/pause/stop, execute menu items, manage tags/layers, get editor state, preferences, undo |
| **ProBuilder** | Mesh creation, extrusion, face/edge/vertex operations, UV editing, smoothing, merge, weld |
| **Graphics** | Lightmap baking, reflection probes, light probes, URP volumes, post-processing, render stats |
| **Physics** | Raycast, linecast, overlap, shape cast, rigidbody, joints, physics materials, collision matrix |
| **Camera** | Camera creation, Cinemachine configuration (body, aim, noise, blend, lens, target, priority) |
| **Profiler** | Start/stop profiling, memory snapshots, frame debugger, frame timing, performance counters |
| **Package** | List, add, remove, embed, search packages; manage scoped registries; resolve, status |
| **UIToolkit** | Create UXML/USS, attach documents, panel settings, get visual tree, inspect, read/update files |
| **Animation** | Create clips, add keyframes, configure Animator controllers, get animation info |
| **Build** | Build, batch build, get/set build settings, switch platform, manage build scenes |
| **Texture** | Create, inspect, configure textures; apply gradients and procedural patterns |
| **Material** | Create, update, assign materials; get shader properties |
| **Light** | Create, configure, list lights |
| **Shader** | Inspect and list shaders |
| **VFX** | List and inspect particle systems |
| **Audio** | Create and configure audio sources |
| **NavMesh** | Get NavMesh info, bake NavMesh |
| **Terrain** | Create terrains, get terrain data |
| **Screenshot** | Capture Game view, Scene view, camera-specific, and multi-view screenshots |
| **ScriptableObject** | Create, inspect, list, modify ScriptableObjects |
| **Object** | Get object data, modify objects by instance ID |
| **Reflect** | Get type info, search types, call methods, get members, find methods via reflection |
| **Type** | Get type schema for serialized types |
| **PlayerSettings** | Get and set player settings (company, product, scripting backend) |
| **Tests** | Run EditMode/PlayMode tests, get test results |
| **Console** | Get logs, log messages, clear console |
| **Selection** | Get and set editor selection |
| **Meta** | List all tools, enable/disable tools |
| **Helpers** | Cross-domain utilities and shared logic |
| **FindInFile** | File search and grep across the project |
| **BatchExecute** | Run multiple tool calls atomically |
| **UnityDocs** | Get API docs, get manual pages, open documentation |
| **Ping** | Server connectivity check |

</details>

### 10 Unity-Specialist Subagents

| Agent | Focus |
|-------|-------|
| **unity-specialist** | Architecture decisions, Unity APIs, MonoBehaviour vs DOTS, platform builds |
| **unity-ui-specialist** | UI Toolkit (UXML/USS), data binding, runtime UI performance, input handling |
| **unity-shader-specialist** | Shader Graph, custom HLSL, VFX Graph, URP customization, post-processing |
| **unity-dots-specialist** | ECS architecture, Jobs system, Burst compiler, hybrid renderer |
| **unity-addressables-specialist** | Asset groups, async loading, memory management, content catalogs |
| **gameplay-programmer** | Game mechanics, player systems, combat, input, state machines |
| **systems-designer** | Combat formulas, progression curves, crafting recipes, balance math |
| **technical-artist** | Shaders, VFX, rendering optimization, art pipeline |
| **performance-analyst** | Profiling, memory analysis, frame time budgets, bottlenecks |
| **qa-lead** | Test strategy, bug triage, release quality gates, regression planning |

Invoke a specialist explicitly via Claude's `Task` tool, or let Claude route automatically based on the question.

### 24 Slash Commands

Quick-access structured workflows. Type `/` in the chat to autocomplete.

<details>
<summary><strong>View all slash commands</strong></summary>

| Command | Description |
|---------|-------------|
| `/architecture-decision` | Create an Architecture Decision Record (ADR) |
| `/asset-audit` | Audit assets for naming, file sizes, and orphaned references |
| `/balance-check` | Analyze game balance data for outliers and degenerate strategies |
| `/brainstorm` | Guided game concept ideation with structured techniques |
| `/bug-report` | Structured bug reports with reproduction steps and severity |
| `/changelog` | Auto-generate changelog from git history |
| `/code-review` | Architectural and quality code review for Unity C# |
| `/create-command` | Create a new custom slash command from a SKILL.md template |
| `/design-review` | Review game design docs for completeness and balance |
| `/estimate` | Task effort estimation with complexity analysis |
| `/hotfix` | Emergency fix workflow with audit trail |
| `/map-systems` | Decompose a game concept into systems with dependency mapping |
| `/new-feature` | Plan a feature implementation with tasks, architecture, and file list |
| `/new-system` | Generate a complete Unity system (MonoBehaviour + ScriptableObject config) |
| `/perf-profile` | Structured performance profiling with budgets |
| `/plan-execute` | Execute a saved plan step-by-step with checkpoints |
| `/playtest-report` | Generate or analyze structured playtest reports |
| `/prototype` | Rapid prototyping — validate a mechanic with throwaway code |
| `/refactor` | Analyze and refactor a file or system with before/after comparison |
| `/reverse-document` | Generate design/architecture docs from existing code |
| `/save-plan` | Persist a generated plan to `ProjectSettings/GameDeck/plans/` |
| `/scope-check` | Analyze a feature for scope creep |
| `/sprint-plan` | Generate or update a sprint plan from the project backlog |
| `/tech-debt` | Scan codebase for technical debt (TODO, FIXME, HACK) |

</details>

You can ship your own commands too. Drop a `SKILL.md` into `ProjectSettings/GameDeck/commands/` and it appears in the picker.

### 16-Document Knowledge Base

A curated Unity reference library bundled with the package. Surfaced in the Library tab of the desktop app with full-text search and match highlighting.

<details>
<summary><strong>View all topics</strong></summary>

1. **Project Architecture** — Assembly definitions, 4-layer pattern, bootstrapper
2. **ScriptableObjects** — Event channels, runtime sets, data-driven design
3. **Design Patterns** — Observer, Command, State Machine, Object Pool, Singleton, Factory
4. **DOTS/ECS** — Entity Component System, Burst, Jobs, decision frameworks
5. **Architecture by Genre** — RPG, 2D platformer, multiplayer, roguelike patterns
6. **Mobile Optimization** — Profiling, pooling, NonAlloc APIs, compression
7. **Core Gameplay Systems** — Combat, progression, economy, meta-progression
8. **UI/UX** — UI Stack, MVP/MVVM, Canvas splitting, virtualized lists
9. **Dependency Injection & Testing** — VContainer vs Zenject, NUnit, testing pyramid
10. **AI-Assisted Workflow** — MCP patterns, known limitations, best practices
11. **Asset Pipeline & Addressables** — Sprite Atlas, texture/audio compression
12. **Procedural Content & Balancing** — Generation algorithms, mathematical balancing
13. **Audio & VFX** — Audio design, Wwise, VFX Graph best practices
14. **Save System & Meta-Progression** — Serialization, progression tracking
15. **Publishing & Live Ops** — Store requirements, analytics, monetization
16. **Case Studies** — 12 real games analyzed, 10 recurring architectural patterns

</details>

### Plans CRUD

Plans are markdown documents stored in `ProjectSettings/GameDeck/plans/`. They show up in a dedicated tab in the desktop app: create, edit, delete, execute. The `/save-plan` command persists a Claude-generated plan to disk; `/plan-execute` runs one with checkpoints.

### Rules

Short directives that get injected into Claude's system prompt every turn. Stored in `ProjectSettings/GameDeck/rules/`, composed into `Library/MCPGameDeck/rules-bundle.md` at runtime, and forwarded via the SDK's `systemPrompt.append` channel. Cap of 10 active rules to keep them deliberate.

### 2 MCP Resources, 6 MCP Prompts

| Resource | Description |
|----------|-------------|
| **Assets** | List project assets filtered by type (Prefab, Material, ScriptableObject, etc.) |
| **Scenes Hierarchy** | Complete hierarchy of all loaded scenes |

| Prompt | Description |
|--------|-------------|
| **build-pipeline** | Configure and execute single or multi-platform builds |
| **gameobject-handling-strategy** | Best practices for creating and organizing GameObjects |
| **prefab-workflow** | Prefab creation, editing, and management |
| **scene-setup** | Scene organization with proper lighting, camera, and structure |
| **ui-toolkit-workflow** | UI Toolkit patterns with UXML, USS, and C# backing |
| **debug-and-profile** | Structured profiling and debugging workflow |

---

## Requirements

| Requirement | Version | Notes |
|-------------|---------|-------|
| **Claude Code** | ≥ 2.1.0, < 3.0.0 | Install via `npm install -g @anthropic-ai/claude-code` |
| **Unity** | 6000.0+ | Uses Unity 6 APIs (`EntityId`, `EditorUtility.EntityIdToObject`) |
| **Node.js** | 18+ | Required by Claude Code itself; the desktop app spawns a Node child |
| **OS** | Windows 10/11, macOS 12+, Ubuntu 20.04+ | Tauri binaries shipped per-OS via GitHub Releases |
| **Anthropic API** | Active subscription or API key | Set up through Claude Code's `/login` flow |

---

## Installation

### Step 1 — Install Claude Code

```bash
npm install -g @anthropic-ai/claude-code
claude /login
```

Confirm with `claude /status`.

### Step 2 — Install the Unity package

Open Unity 6, then **Window → Package Manager → + → Add package from git URL**:

```
https://github.com/RamonBedin/mcp-game-deck.git
```

For a specific version, append `#v2.0.0`.

For local development:

```
https://github.com/RamonBedin/mcp-game-deck.git
# then point to a file: reference if you're iterating on the package itself
```

### Step 3 — Open the chat

Click the **MCP Game Deck pin** on the Editor toolbar → **Open Chat**.

On first launch the package downloads the signed Tauri binary from the matching GitHub Release into `%APPDATA%\MCPGameDeck\bin\<version>\` (Windows) — or the OS-equivalent user-data directory on macOS/Linux. SHA-256 is verified before promotion. If the download fails the dialog explains how to install manually.

The desktop app's first-run wizard then walks through the three checks (Claude Code installed, authenticated, Agent SDK present) and provisions the Node runtime on demand.

After that: just chat.

---

## Configuration

Project settings live at `ProjectSettings/GameDeckSettings.json` and are editable via **Project Settings → MCP Game Deck**.

| Setting | Default | Description |
|---------|---------|-------------|
| **MCP Server Port** | `8090` | Port for the C# MCP TCP server. Changing requires a Unity restart. |
| **Host** | `localhost` | Hostname the MCP server binds to |
| **Request Timeout** | `30s` | Max time for a single tool execution on the main thread |

The settings panel also exposes a copy-paste MCP config block for **Claude Desktop**, **Claude Code CLI**, **Cursor**, **Cline**, and other MCP-aware clients that want to connect to the C# server directly (bypassing the desktop app).

---

## Using the C# MCP server with other clients

The desktop app is one consumer of the C# MCP server — not the only one. Any MCP-compatible client can connect to `localhost:8090` directly. The settings panel generates the JSON config block ready to paste:

```json
{
  "mcpServers": {
    "mcp-game-deck": {
      "command": "node",
      "args": ["<package-path>/Server~/dist/mcp-proxy.js"],
      "env": {
        "UNITY_MCP_PORT": "8090",
        "UNITY_MCP_HOST": "localhost"
      }
    }
  }
}
```

For external-client use you'll need to `npm install && npm run build` inside `Server~/` once to compile the proxy. The desktop app ships a self-contained bundled version inside its installer, so its users never need to do this themselves.

---

## Project structure

```
mcp-game-deck/
├── package.json              Unity package manifest (v2.0.0)
├── README.md                 You are here
├── LICENSE                   MIT
│
├── Editor/                   C#, Editor-only
│   ├── MCP/                  Custom MCP framework (attributes, discovery, server)
│   ├── Tools/                273 MCP tools across 41 categories
│   ├── Pin/                  Toolbar pin + binary download/launch lifecycle
│   ├── Settings/             Project settings UI and persistence
│   ├── Prompts/              MCP prompt templates
│   └── Resources/            Pin icons, etc.
│
├── Plugin~/                  Claude Code plugin bundled with the package
│   ├── .claude-plugin/       plugin.json + plugin metadata
│   ├── agents/               10 Unity-specialist agents
│   ├── skills/               24 slash commands (one SKILL.md per command)
│   └── knowledge/            16 reference documents
│
├── Server~/                  TypeScript MCP proxy
│   ├── src/                  Source (Claude Code ↔ Unity HTTP bridge)
│   └── package.json          Dev dependencies + bundle script
│
└── App~/                     Tauri desktop chat app (v1.0.0)
    ├── src/                  React + Vite frontend
    ├── src-tauri/            Rust supervisor (spawns Claude Code SDK, owns MCP proxy resource)
    ├── scripts/              Build orchestration (Server~ → bundled proxy → Tauri resource)
    └── package.json          App version, build scripts
```

The `~` suffix on `Plugin~/`, `Server~/`, and `App~/` is a Unity convention — those folders are invisible to the asset pipeline. They ship with the package via UPM git install but never appear in your project's Asset Database.

---

## Architectural notes

A few decisions worth knowing if you're going to dig in:

- **TCP, not HTTPListener.** The Unity-side MCP server uses `TcpListener` with `SO_REUSEADDR` to avoid `EADDRINUSE` after assembly reloads. The HTTP layer is hand-rolled on top.
- **Main-thread dispatcher.** Every tool call that touches Unity APIs is marshalled onto the main thread via an internal dispatcher. Tools never assume they're on the right thread.
- **Resource-bundled proxy.** The MCP proxy ships as a self-contained CommonJS bundle (esbuild) inside the Tauri app's resource dir — no separate `npm install` for end users. Source lives in `Server~/src/mcp-proxy.ts`.
- **Token-based auth.** A per-project auth token at `Library/GameDeck/auth-token` gates the MCP server. The desktop app reads it transparently; external MCP clients can opt in via the `UNITY_MCP_AUTH_TOKEN` env var.
- **Permission contract via env vars.** The pin passes `MCP_GAME_DECK_PACKAGE_ROOT` (resolved Unity package path) and `MCP_GAME_DECK_RUNTIME_DIR` (per-version writable runtime tree) to the Tauri process at spawn time, decoupling the desktop binary from compile-time path assumptions.

For deeper architectural context, see `docs/internal/` (committed, internal-facing).

---

## Third-party dependencies

MCP Game Deck integrates with proprietary software that is **not bundled** with this package and is governed by separate terms:

- **[Claude Code](https://docs.claude.com/en/docs/claude-code)** — Anthropic's CLI agent. Required at runtime. Subject to [Anthropic's Usage Policy](https://www.anthropic.com/legal/aup) and [Commercial Terms of Service](https://www.anthropic.com/legal/commercial-terms).
- **[@anthropic-ai/claude-agent-sdk](https://www.npmjs.com/package/@anthropic-ai/claude-agent-sdk)** — Drives Claude Code from the desktop app's Node runtime. Same Anthropic terms apply.
- **[@modelcontextprotocol/sdk](https://www.npmjs.com/package/@modelcontextprotocol/sdk)** — MCP protocol implementation for the proxy. MIT.
- **[Tauri](https://v2.tauri.app)** — Desktop app framework. MIT / Apache 2.0.

The MIT License of this repository covers **only** the source code authored here. Users are responsible for installing Claude Code separately, maintaining their own Anthropic API authentication, and complying with Anthropic's terms.

---

## License

[MIT](LICENSE) © Ramon Bedin

---

## Author

**Ramon Bedin** — [GitHub](https://github.com/RamonBedin)

If you ship a Unity project with MCP Game Deck, I'd love to hear about it. Open an issue or drop a line.

---

<p align="center">
  <strong>MCP Game Deck v2.0.0</strong>
  <br>
  Built for Unity 6 &nbsp;&bull;&nbsp; Powered by the Model Context Protocol &nbsp;&bull;&nbsp; Designed by Ramon Bedin
</p>
