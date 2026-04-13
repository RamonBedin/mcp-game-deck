<h1 align="center">MCP Game Deck</h1>

<p align="center">
  <strong>Give Claude Code full control over the Unity Editor.</strong>
</p>

<p align="center">
  <code>268 MCP Tools</code> &nbsp;&middot;&nbsp;
  <code>7 Resources</code> &nbsp;&middot;&nbsp;
  <code>5 Prompts</code> &nbsp;&middot;&nbsp;
  <code>10 Agents</code> &nbsp;&middot;&nbsp;
  <code>18 Slash Commands</code> &nbsp;&middot;&nbsp;
  <code>16-doc Knowledge Base</code>
</p>

<p align="center">
  v1.0.1 &nbsp;&bull;&nbsp; Unity 6+ &nbsp;&bull;&nbsp; MCP Protocol &nbsp;&bull;&nbsp; Requires Claude Code &amp; Node.js
</p>

---

## What is MCP Game Deck?

MCP Game Deck is a Unity Editor package that exposes your entire project to [Claude Code](https://docs.anthropic.com/en/docs/claude-code) through the [Model Context Protocol (MCP)](https://modelcontextprotocol.io). It is **not** an AI itself — it is the bridge that lets Claude Code see, create, modify, and inspect everything in your Unity Editor: scenes, GameObjects, prefabs, materials, scripts, builds, and more.

**How it works:** The package runs a custom MCP server inside the Unity Editor. Claude Code connects to it and gains access to 268 tools that control the editor. You talk to Claude Code, Claude Code talks to your Unity project.

**What's included:**

- **MCP Server** — A custom C# HTTP server with 268 tools, 7 resources, and 5 prompts. Runs inside the Editor process. No external MCP libraries.
- **Intelligence Layer** — 10 specialized agents, 18 slash commands, and a 16-document knowledge base that give Claude Code deep Unity domain expertise.
- **Chat UI** — An optional EditorWindow (powered by Claude Agent SDK + Node.js) for chatting with Claude directly inside Unity.

---

## Features

### 268 MCP Tools

Full programmatic control over the Unity Editor, organized into 37 categories:

<details>
<summary><strong>View all tool categories</strong></summary>

| Category | Count | Description |
|----------|------:|-------------|
| ProBuilder | 35 | Mesh creation, extrusion, face/edge/vertex operations, UV editing, smoothing, merge, weld |
| Graphics | 26 | Lightmap baking, reflection probes, light probes, URP volumes, post-processing, render stats |
| Physics | 25 | Raycast, linecast, overlap, shape cast, rigidbody, joints, physics materials, collision matrix |
| Camera | 20 | Camera creation, Cinemachine configuration (body, aim, noise, blend, lens, target, priority) |
| Profiler | 16 | Start/stop profiling, memory snapshots, frame debugger, frame timing, performance counters |
| Editor | 13 | Play/pause/stop, execute menu items, manage tags/layers, get editor state, preferences, undo |
| Package | 13 | List, add, remove, embed, search packages; manage scoped registries; resolve, status |
| Asset | 11 | Create, copy, move, rename, delete assets; inspect/set import settings; refresh AssetDatabase |
| GameObject | 10 | Create, find, get, update, delete, duplicate, select, set parent, look-at, move relative |
| Scene | 10 | Create, load, save, unload, delete scenes; get info/hierarchy; list scenes; frame objects |
| UIToolkit | 9 | Create UXML/USS, attach documents, panel settings, get visual tree, inspect, read/update files |
| Prefab | 8 | Create, instantiate, open, modify, save, close prefabs; get prefab info |
| Script | 8 | Create, read, update, delete C# scripts; apply targeted edits; validate compilation |
| Build | 6 | Build, batch build, get/set build settings, switch platform, manage build scenes |
| Texture | 6 | Create, inspect, configure textures; apply gradients and procedural patterns |
| Component | 5 | Add, get, update, remove, list components on GameObjects |
| Reflect | 5 | Get type info, search types, call methods, get members, find methods via reflection |
| ScriptableObject | 5 | Create, inspect, list, modify ScriptableObjects |
| Screenshot | 4 | Capture Game view, Scene view, camera-specific, and multi-view screenshots |
| Animation | 4 | Create clips, add keyframes, configure Animator controllers, get animation info |
| Material | 4 | Create, update, assign materials; get shader properties |
| Light | 4 | Create, configure, list lights |
| Transform | 4 | Move, rotate, scale, get transform info |
| UnityDocs | 4 | Get API docs, get manual pages, open documentation |
| Console | 3 | Get logs, log messages, clear console |
| Meta | 3 | List all tools, enable/disable tools |
| NavMesh | 3 | Get NavMesh info, bake NavMesh |
| Object | 3 | Get object data, modify objects by instance ID |
| PlayerSettings | 3 | Get and set player settings (company, product, scripting backend) |
| Selection | 3 | Get and set editor selection |
| Shader | 3 | Inspect and list shaders |
| Terrain | 3 | Create terrains, get terrain data |
| Tests | 3 | Run EditMode/PlayMode tests, get test results |
| Audio | 2 | Create and configure audio sources |
| Ping | 2 | Server connectivity check |
| Type | 2 | Get type schema for serialized types |
| VFX | 2 | List and inspect particle systems |

</details>

### 7 MCP Resources

Read-only access to live project state:

| Resource | Description |
|----------|-------------|
| **Assets** | List project assets filtered by type (Prefab, Material, ScriptableObject, etc.) |
| **Console Logs** | Recent console entries with timestamps and stack traces |
| **GameObject** | Full detail on any GameObject — transform, components, properties, children |
| **Menu Items** | Available Editor menu items |
| **Packages** | All installed Unity packages with metadata |
| **Scenes Hierarchy** | Complete hierarchy of all loaded scenes |
| **Tests** | Available test assemblies and scripts |

### 5 Workflow Prompts

Structured templates for common multi-step workflows:

- **build-pipeline** — Configure and execute single or multi-platform builds
- **gameobject-handling-strategy** — Best practices for creating and organizing GameObjects
- **prefab-workflow** — Prefab creation, editing, and management
- **scene-setup** — Scene organization with proper lighting, camera, and structure
- **ui-toolkit-workflow** — UI Toolkit patterns with UXML, USS, and C# backing

### 10 Specialized AI Agents

Domain experts with deep knowledge and access to all MCP tools:

| Agent | Expertise |
|-------|-----------|
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

### 18 Slash Commands

Quick-access structured workflows:

| Command | Description |
|---------|-------------|
| `/architecture-decision` | Create an Architecture Decision Record (ADR) |
| `/asset-audit` | Audit assets for naming, file sizes, and orphaned references |
| `/balance-check` | Analyze game balance data for outliers and degenerate strategies |
| `/brainstorm` | Guided game concept ideation with structured techniques |
| `/bug-report` | Structured bug reports with reproduction steps and severity |
| `/changelog` | Auto-generate changelog from git history |
| `/code-review` | Architectural and quality code review for Unity C# |
| `/design-review` | Review game design docs for completeness and balance |
| `/estimate` | Task effort estimation with complexity analysis |
| `/hotfix` | Emergency fix workflow with audit trail |
| `/map-systems` | Decompose a game concept into systems with dependency mapping |
| `/perf-profile` | Structured performance profiling with budgets |
| `/playtest-report` | Generate or analyze structured playtest reports |
| `/prototype` | Rapid prototyping — validate a mechanic with throwaway code |
| `/reverse-document` | Generate design/architecture docs from existing code |
| `/scope-check` | Analyze a feature for scope creep |
| `/sprint-plan` | Generate or update a sprint plan from the project backlog |
| `/tech-debt` | Scan codebase for technical debt (TODO, FIXME, HACK) |

### 16-Document Knowledge Base

Comprehensive Unity reference documentation covering:

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

---

## Getting Started

### Prerequisites

- **Claude Code** — This package is designed to work with Claude Code. [Install Claude Code](https://docs.anthropic.com/en/docs/claude-code)
- **Unity 6** (6000.0 or later)
- **Node.js 18+** (required for the Agent SDK Server and Chat UI)
- **Claude API key** (for Claude Code and the integrated Chat UI)

### Installation

1. Clone or download this repository

2. In Unity, go to **Window > Package Manager**

3. Click **+** > **Add package from disk...**

4. Select the `package.json` at the root of this repository

5. Build the Agent SDK Server:
   ```bash
   cd Server~
   npm install
   npm run build
   ```

6. On first import, the package automatically copies `.claude/`, `knowledge-base/`, and `CLAUDE.md` to your project root

### First Launch

The MCP server starts automatically when the Unity Editor loads. To use the integrated chat:

1. Open **Window > MCP Game Deck Chat**
2. The Agent SDK server starts automatically (or run `npm start` in `Server~/`)
3. Start chatting — Claude now has full access to your Unity project

To use with **Claude Code CLI** instead:

1. Navigate to your Unity project root
2. Run `claude` — the CLAUDE.md instructs Claude on available tools
3. Claude will connect to the MCP server on port `8090`

---

## How It Works

```
Unity Editor
 |
 |-- MCP Server (C# HTTP, port 8090)
 |    |-- 268 Tools ......... create, modify, inspect editor state
 |    |-- 7 Resources ....... read-only project data
 |    '-- 5 Prompts ......... structured workflow templates
 |
 |-- Chat UI (EditorWindow, UI Toolkit)
 |    '-- WebSocket client -> Agent SDK Server
 |
 '-- Agent SDK Server (Node.js, port 9100)
      |-- Claude API integration
      |-- MCP proxy -> forwards tool calls to C# server
      '-- Session management & cost tracking
```

The MCP server runs inside the Unity Editor process with zero external dependencies. All tool calls that touch Unity APIs execute on the main thread via an internal dispatcher. The server automatically stops during assembly reload and play mode transitions.

---

## Configuration

Settings are stored in `ProjectSettings/GameDeckSettings.json` and editable via **Project Settings > MCP Game Deck**.

| Setting | Default | Description |
|---------|---------|-------------|
| MCP Port | `8090` | Port for the C# MCP HTTP server |
| Agent Port | `9100` | Port for the Agent SDK WebSocket server |
| Host | `localhost` | Hostname the MCP server binds to |
| Request Timeout | `30s` | Timeout for tool execution |
| Auto Start | `true` | Auto-start servers when the Chat window opens |
| Default Model | `claude-sonnet-4-6` | Claude model used by the Agent SDK |

---

## Project Structure

```
mcp-game-deck/
|-- package.json              Unity package manifest
|-- CLAUDE.md                 AI context file (copied to user project)
|-- README.md                 This file
|
|-- Editor/                   C# editor-only code
|    |-- MCP/                 Custom MCP framework
|    |    |-- Attributes/     [McpTool], [McpToolType], [McpResource], [McpPrompt]
|    |    |-- Models/         ToolResponse, ResourceResponse, McpToolInfo
|    |    |-- Discovery/      Reflection-based tool/resource/prompt discovery
|    |    |-- Registry/       In-memory registries for tools, resources, prompts
|    |    |-- Server/         TCP/HTTP server, JSON-RPC 2.0 handler
|    |    '-- Utils/          MainThreadDispatcher, JsonHelper, McpLogger
|    |-- Tools/               268 MCP tools (37 categories)
|    |-- Resources/           7 MCP resources
|    |-- Prompts/             5 MCP prompts
|    |-- ChatUI/              Integrated chat EditorWindow
|    |-- Settings/            Project settings UI and persistence
|    '-- Utils/               PackageSetup, helpers
|
|-- Server~/                  Agent SDK Server (TypeScript, ~ = ignored by Unity)
|    |-- src/                 Source files
|    |-- dist/                Compiled output
|    '-- package.json         Node.js dependencies
|
|-- .claude/                  AI intelligence layer
|    |-- agents/              10 specialized agent definitions
|    '-- skills/              18 slash command definitions
|
'-- knowledge-base/           16 Unity reference documents
```

---

## Requirements

| Requirement | Version |
|-------------|---------|
| Claude Code | Latest |
| Unity | 6000.0+ (Unity 6) |
| Node.js | 18+ |
| npm | 9+ |
| Platform | Windows, macOS, Linux |

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Author

**Ramon Bedin** — [GitHub](https://github.com/RamonBedin)

---

<p align="center">
  <strong>MCP Game Deck v1.0</strong>
  <br>
  Built for Unity 6 &nbsp;&bull;&nbsp; Powered by MCP &nbsp;&bull;&nbsp; Designed by Ramon Bedin
</p>