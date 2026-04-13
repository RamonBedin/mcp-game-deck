# CLAUDE.md — MCP Game Deck

You are assisting a game developer working on a **Unity 6** project that has the **MCP Game Deck** package installed.
This package gives you direct access to the Unity Editor through **268 MCP tools**, **7 resources**, **5 prompts**, **10 specialized agents**, and **18 slash commands**.

Use these tools to help the developer build, debug, optimize, and ship their game.

---

## Quick Start

- The MCP server **auto-starts** when the Unity Editor loads (default port `8090`).
- Settings are at **Project Settings > MCP Game Deck**.
- Use `tools/list` to discover all available tools at runtime.
- Use `resources/list` and `prompts/list` to discover resources and prompts.

---

## MCP Tools (268 total)

Tools are organized by Unity subsystem. Call them via `tools/call` with the tool ID.

| Category | Count | Description |
|----------|------:|-------------|
| ProBuilder | 35 | Mesh creation, extrusion, face/edge/vertex operations, UV editing, smoothing, merge, weld |
| Graphics | 26 | Lightmap baking, reflection probes, light probes, URP volumes, post-processing, render stats, quality settings |
| Physics | 25 | Raycast, linecast, overlap, shape cast, rigidbody, joints, physics materials, collision matrix, simulation |
| Camera | 20 | Camera creation, listing, Cinemachine configuration (body, aim, noise, blend, lens, target, priority, brain) |
| Profiler | 16 | Start/stop profiling, memory snapshots, frame debugger, frame timing, performance counters, memory comparison |
| Editor | 13 | Play/pause/stop, execute menu items, manage tags/layers, get editor state, preferences, undo |
| Package | 13 | List, add, remove, embed, search packages; manage scoped registries; resolve, status |
| Asset | 11 | Create, copy, move, rename, delete assets; inspect/set import settings; create folders; refresh AssetDatabase |
| GameObject | 10 | Create, find, get, update, delete, duplicate, select, set parent, look-at, move relative |
| Scene | 10 | Create, load, save, unload, delete scenes; get info/hierarchy; list scenes; frame objects in Scene view |
| UIToolkit | 9 | Create UXML/USS, attach documents, create panel settings, get visual tree, inspect, list, read/update files |
| Prefab | 8 | Create, instantiate, open, modify, save, close prefabs; get prefab info |
| Script | 8 | Create, read, update, delete C# scripts; apply targeted edits; validate compilation |
| Texture | 6 | Create, inspect, configure textures; apply gradients and procedural patterns |
| Build | 6 | Build, batch build, get/set build settings, switch platform, manage build scenes |
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
| PlayerSettings | 3 | Get and set player settings (company, product, icons, splash, scripting) |
| Selection | 3 | Get and set editor selection |
| Shader | 3 | Inspect and list shaders |
| Terrain | 3 | Create terrains, get terrain data |
| Tests | 3 | Run EditMode/PlayMode tests, get test results |
| Audio | 2 | Create and configure audio sources |
| Ping | 2 | Server connectivity ping |
| Type | 2 | Get type schema for serialized types |
| VFX | 2 | List and inspect particle systems |
| Other | 4 | Add asset to scene, batch execute, find in file, recompile scripts |

---

## MCP Resources (7 total)

Resources provide read-only access to project state. Call them via `resources/read` with the URI.

| URI | Description |
|-----|-------------|
| `mcp-game-deck://assets/{filter}` | List project assets, filtered by type (`t:Prefab`, `t:Material`, `t:ScriptableObject`, etc.). Max 200 results. |
| `mcp-game-deck://console-logs/{logType}` | Recent console entries filtered by type (`error`, `warning`, `log`). Includes timestamps and stack traces. |
| `mcp-game-deck://gameobject/{name}` | Detailed info about a GameObject — transform, components, serialized properties, layer, tag, children. |
| `mcp-game-deck://menu-items/{prefix}` | Available Editor menu items, filterable by prefix (`File`, `Edit`, `Assets`, etc.). |
| `mcp-game-deck://packages` | All installed Unity packages with name, version, source, and description. |
| `mcp-game-deck://scenes-hierarchy` | Complete hierarchy of all loaded scenes — GameObjects, components, nesting, active state. |
| `mcp-game-deck://tests` | Available test assemblies and test scripts (EditMode and PlayMode). |

---

## MCP Prompts (5 total)

Prompts are structured workflow templates. Call them via `prompts/get` with the prompt name.

| Prompt | Description | Key Arguments |
|--------|-------------|---------------|
| `build-pipeline` | Step-by-step build workflow for single or multi-platform builds | `targetPlatform` (windows64, android, ios, webgl, all) |
| `gameobject-handling-strategy` | Best practices for creating, modifying, and organizing GameObjects | `gameObjectName` (name or path) |
| `prefab-workflow` | Prefab creation, editing, and management guidance | `prefabName` (name or asset path) |
| `scene-setup` | Scene organization with proper lighting, camera, and structure | `sceneType` (gameplay, menu, loading, test, empty) |
| `ui-toolkit-workflow` | UI Toolkit patterns — UXML layout, USS styles, C# backing code | `uiName` (e.g., MainMenu, InventoryPanel, HUD) |

---

## Specialized Agents (10 total)

Delegate to these agents for domain-specific tasks. Each has deep knowledge of its area plus access to MCP tools.

| Agent | Use For |
|-------|---------|
| **unity-specialist** | Architecture decisions, Unity API guidance, MonoBehaviour vs DOTS, platform builds |
| **unity-ui-specialist** | UI Toolkit (UXML/USS), data binding, runtime UI performance, input handling |
| **unity-shader-specialist** | Shader Graph, custom HLSL, VFX Graph, URP customization, post-processing |
| **unity-dots-specialist** | ECS architecture, Jobs system, Burst compiler, hybrid renderer, data-oriented design |
| **unity-addressables-specialist** | Asset groups, async loading, memory management, content catalogs, remote delivery |
| **gameplay-programmer** | Game mechanics, player systems, combat, input, state machines, interactive features |
| **systems-designer** | Combat formulas, progression curves, crafting recipes, status effects, balance math |
| **technical-artist** | Shaders, VFX, rendering optimization, art pipeline, visual profiling |
| **performance-analyst** | Profiling, memory analysis, frame time budgets, bottleneck identification |
| **qa-lead** | Test strategy, bug triage, release quality gates, regression planning |

---

## Slash Commands (18 total)

Users can invoke these with `/<command>`. Each runs a structured workflow.

| Command | Description |
|---------|-------------|
| `/architecture-decision` | Create an Architecture Decision Record (ADR) |
| `/asset-audit` | Audit assets for naming conventions, file sizes, and orphaned references |
| `/balance-check` | Analyze game balance data for outliers and degenerate strategies |
| `/brainstorm` | Guided game concept ideation with structured creative techniques |
| `/bug-report` | Create structured bug reports with reproduction steps and severity |
| `/changelog` | Auto-generate changelog from git history |
| `/code-review` | Architectural and quality code review for Unity C# |
| `/design-review` | Review game design documents for completeness and balance |
| `/estimate` | Estimate task effort with complexity analysis and confidence levels |
| `/hotfix` | Emergency fix workflow with audit trail for critical bugs |
| `/map-systems` | Decompose a game concept into systems with dependency mapping |
| `/perf-profile` | Structured performance profiling with budgets and recommendations |
| `/playtest-report` | Generate or analyze structured playtest reports |
| `/prototype` | Rapid prototyping workflow — validate a mechanic with throwaway code |
| `/reverse-document` | Generate design or architecture docs from existing code |
| `/scope-check` | Analyze a feature for scope creep against the original plan |
| `/sprint-plan` | Generate or update a sprint plan from the project backlog |
| `/tech-debt` | Scan codebase for technical debt (TODO, FIXME, HACK, complexity) |

---

## Knowledge Base (16 reference docs)

The `knowledge-base/` directory contains comprehensive Unity reference documentation. Consult these when you need deep domain knowledge.

| Doc | Topic |
|-----|-------|
| `01-unity-project-architecture.md` | Project architecture, assembly definitions, 4-layer pattern, bootstrapper |
| `02-scriptableobjects-data-driven.md` | ScriptableObjects, event channels, runtime sets, data-driven design |
| `03-unity-design-patterns.md` | Observer, Command, State Machine, Object Pool, Singleton, Factory, Decorator |
| `04-ecs-dots-performance.md` | DOTS/ECS, Burst, Jobs system, decision frameworks |
| `05-architecture-by-genre.md` | Genre-specific patterns (RPG, 2D platformer, multiplayer, roguelike) |
| `06-mobile-optimization.md` | Mobile profiling, pooling, NonAlloc APIs, compression, Adaptive Performance |
| `07-core-gameplay-systems.md` | Combat, progression, economy, meta-progression systems |
| `08-unity-ui-ux.md` | UI Stack, MVP/MVVM, Canvas splitting, virtualized lists, safe areas |
| `09-dependency-injection-testing.md` | VContainer vs Zenject, NUnit, testing pyramid |
| `10-ai-assisted-unity-workflow.md` | AI-assisted development patterns, MCP usage, known limitations |
| `11-asset-pipeline-addressables.md` | Addressables, Sprite Atlas, texture/audio compression |
| `12-procedural-content-balancing.md` | Procedural generation, mathematical balancing |
| `13-audio-vfx-systems.md` | Audio design, Wwise integration, VFX best practices |
| `14-save-system-meta-progression.md` | Save/load patterns, serialization, progression tracking |
| `15-publishing-live-ops.md` | Store requirements, live ops, analytics, monetization |
| `16-unity-project-case-studies.md` | 12 real games analyzed, 10 recurring architectural patterns |

---

## Configuration

Settings are stored in `ProjectSettings/GameDeckSettings.json` and editable via **Project Settings > MCP Game Deck**.

| Setting | Default | Description |
|---------|---------|-------------|
| MCP Port | `8090` | Port for the MCP HTTP server |
| Agent Port | `9100` | Port for the Agent SDK WebSocket server |
| Host | `localhost` | Hostname the MCP server binds to |
| Request Timeout | `30s` | Timeout for MCP tool execution |
| Auto Start | `true` | Auto-start servers when the Chat window opens |
| Default Model | `claude-sonnet-4-6` | Claude model used by the Agent SDK |

---

## How It Works

```
Unity Editor
├── MCP Server (C# HTTP, port 8090)
│   ├── 268 Tools — manipulate editor state
│   ├── 7 Resources — read-only project data
│   └── 5 Prompts — structured workflow templates
│
├── Chat UI (EditorWindow, UI Toolkit)
│   └── WebSocket client → Agent SDK Server
│
└── Agent SDK Server (Node.js, port 9100)
    ├── Claude API integration
    ├── MCP proxy → forwards tool calls to C# server
    └── Session management
```

The MCP server runs inside the Unity Editor process. It starts automatically on Editor load and stops during assembly reload or play mode transitions. All tool calls that touch Unity APIs execute on the main thread via an internal dispatcher.

---

## Best Practices

- **Read before writing**: Use resources (`scenes-hierarchy`, `gameobject/{name}`, `assets/{filter}`) to understand the current project state before making changes.
- **Use prompts for workflows**: Instead of figuring out multi-step operations from scratch, use the built-in prompts (`scene-setup`, `prefab-workflow`, `build-pipeline`).
- **Delegate to specialists**: For domain-heavy tasks (shaders, DOTS, UI Toolkit, game balance), delegate to the appropriate agent — they have deeper context.
- **Check console after changes**: Use `console-get-logs` or the `console-logs` resource to verify no errors were introduced.
- **Use screenshots for visual feedback**: The `screenshot-game-view`, `screenshot-scene-view`, and `screenshot-camera` tools let you see what the user sees.
- **Navigate the Scene view freely**: You have full control over the Scene view camera. Use `scene-view-frame` to frame objects, `camera-align-to-view` to reposition, and transform tools to move around. Don't limit yourself to the user's current viewpoint — move to where you need to be to place, inspect, or verify objects.
- **Consult the knowledge base**: Before recommending architecture or patterns, check if a relevant doc exists in `knowledge-base/`.
