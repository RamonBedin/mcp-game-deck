# AI-Assisted Unity Development: State of the Art 2025–2026

> Practical guide for solo developers — tools, real limitations, and workflows that actually work.
> Last updated: April 2026

---

## Table of Contents

1. [Unity MCP — Connecting LLMs to the Editor](#1-unity-mcp--connecting-llms-to-the-editor)
2. [Claude Code for Gamedev](#2-claude-code-for-gamedev)
3. [Native Unity AI (Muse → Unity AI 6.2+)](#3-native-unity-ai)
4. [Known Limitations](#4-known-limitations)
5. [Practical Strategies](#5-practical-strategies)
6. [Alternatives: Cursor, Windsurf, Copilot](#6-alternatives-cursor-windsurf-copilot)
7. [Case Study: Epic Survivors — MonoGame for AI Transparency](#7-case-study-epic-survivors--monogame-for-ai-transparency)
8. [Recommendations for Solo Developers](#8-recommendations-for-solo-developers)
9. [Sources](#9-sources)

---

## 1. Unity MCP — Connecting LLMs to the Editor

The **Model Context Protocol (MCP)** is an open standard that allows LLMs (Claude, GPT, etc.) to call "tools" on external systems. For Unity, this means an AI can manipulate the Editor directly — creating GameObjects, editing scripts, running tests, capturing screenshots — all via structured calls.

### 1.1 CoplayDev/unity-mcp (the most mature)

**What it is:** An HTTP bridge between AI assistants and the Unity Editor. Runs a local server at `localhost:8080` inside the Editor via a Unity Package.

**Architecture:**

```
┌─────────────┐     MCP/HTTP      ┌──────────────────┐     C# Scripting    ┌─────────────┐
│  AI Client   │ ──────────────── │  MCP Server       │ ──────────────────  │ Unity Editor │
│ (Claude,     │   localhost:8080  │  (MCPForUnity     │    EditorApplication│ (scenes,     │
│  Cursor...)  │                   │   package)        │    API calls        │  assets...)  │
└─────────────┘                    └──────────────────┘                      └─────────────┘
```

**Available tools (~40+):**

| Category | Tools | What they do |
|---|---|---|
| Assets & Project | `manage_asset`, `manage_prefabs`, `manage_shader`, `manage_texture`, `manage_material` | Asset CRUD, importing, configuration |
| Scene & GameObjects | `manage_gameobject`, `manage_scene`, `find_gameobjects` | Create/move/delete objects, switch scenes |
| Scripts | `create_script`, `delete_script`, `manage_script`, `validate_script`, `script_apply_edits`, `apply_text_edits` | Generate and edit C# with validation |
| Editor & Workflow | `manage_editor`, `execute_menu_item`, `manage_tools`, `refresh_unity` | Control the Editor, execute menu items |
| Advanced | `manage_animation`, `manage_camera`, `manage_graphics`, `manage_physics`, `manage_profiler`, `manage_build`, `manage_packages`, `manage_vfx`, `manage_ui`, `manage_probuilder` | Specialized Unity systems |
| Utilities | `batch_execute`, `read_console`, `run_tests`, `find_in_file`, `unity_docs`, `unity_reflect` | Batch automation, debugging, documentation |

**Setup:**

```bash
# 1. In Unity Package Manager:
#    Window > Package Manager > + > Add package from git URL:
#    https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main

# 2. In Unity:
#    Window > MCP for Unity > Start Server

# 3. In claude_desktop_config.json (Claude Desktop):
{
  "mcpServers": {
    "unity": {
      "url": "http://localhost:8080/mcp"
    }
  }
}

# For Claude Code, auto-configure usually works.
# For Cursor/Windsurf, enable the MCP toggle in settings.
```

**Honest assessment:** This is the most complete and actively maintained option (v9.6.3 as of April 2026, with profiler tools). The `batch_execute` command is genuinely useful (10-100x faster than individual calls). However, operations involving binary serialization (complex scenes, nested prefabs) can still fail silently. Security is loopback-only by default. MIT license; the "Coplay" premium product is separate and more integrated.

**Supported clients:** Claude Desktop, Claude Code, Cursor, VS Code Copilot, Windsurf, GitHub Copilot CLI.

### 1.2 YetAnotherUnityMcp (WebSockets, real-time)

**What it is:** An alternative that uses **WebSockets** instead of HTTP, enabling bidirectional real-time communication between AI and Unity.

**Different architecture:**

- Unity runs a **WebSocket server** (C#/.NET plugin)
- A **Python client** (FastMCP) receives requests from AI agents
- Bidirectional communication: Unity can *notify* the AI of changes, not just respond

**Capabilities:**
- Execute arbitrary C# in the Editor
- Query Editor state in real time
- Capture screenshots
- Modify GameObject properties
- Navigate the scene hierarchy

**Honest assessment:** A more elegant architecture for interactive scenarios (the AI "sees" changes in real time). However, fewer pre-built tools than CoplayDev, and arbitrary C# execution is powerful but risky. Smaller project with less guaranteed maintenance.

### 1.3 IvanMurzak/Unity-MCP (CLI-first, token-efficient)

**What it is:** A framework with **52 tools, 48 prompts, a dedicated CLI**, and a focus on token efficiency.

**Differentiators:**
- Cross-platform CLI for setup (create project, install plugin, configure MCP, open Unity — all via terminal)
- Any C# method becomes a tool with a single line of code
- Focus on a "full AI develop and test loop"
- Free, works with Claude Code, Gemini, Copilot, Cursor

**Honest assessment:** A more programmatic and extensible approach. The CLI is useful for automation. The "efficient token usage" claim is relevant — well-defined tools consume less context than raw text dumps. Good for developers who want to customize tools specifically for their project.

### 1.4 Official Unity: com.unity.ai.assistant MCP

Unity itself has released an **official MCP package** (`com.unity.ai.assistant@2.0`) that exposes the Editor via MCP. Still in preview, but it signals that Unity recognizes MCP as the standard for AI integration.

---

## 2. Claude Code for Gamedev

### 2.1 Claude Code Game Studios — Reality vs. Marketing

**What it really is:** A **collection of prompt templates** (markdown + YAML frontmatter) that organize Claude Code into a simulated game studio hierarchy. It is not software, not an Anthropic product — it is a community open-source repository.

**Structure (3 tiers, 48 agents):**

| Tier | Examples | Suggested model |
|---|---|---|
| Directors (3) | creative-director, technical-director, producer | Claude Opus |
| Leads (8) | game-designer, lead-programmer, art-director, qa-lead | Claude Sonnet |
| Specialists (37) | gameplay-programmer, engine-specialist, AI-programmer, UI-artist, sound-designer | Sonnet/Haiku |

**37 slash commands** cover: `/start` (guided onboarding), sprint workflows, code review, asset audit, release.

**8 hooks** automate: commit validation, gap detection, asset security checks.

**What works:**
- Good mental framework for organizing complex tasks
- Slash commands for repetitive workflows are genuinely useful
- Validation hooks catch common errors

**What doesn't work (yet):**
- 48 simultaneous agents is unrealistic with current context windows
- It is fundamentally "organized prompt engineering", not autonomous agents
- No evidence of completed games built with the system
- Unity support is superficial (mentions "DOTS/ECS, Shaders, Addressables" without elaborating)
- The user still makes all the decisions

**Verdict:** Useful as an **organizational framework** and starting point for creating your own workflows. Not the silver bullet the marketing implies.

### 2.2 Subagents, Slash Commands, and Hooks (what really matters)

Regardless of Game Studios, Claude Code's core features are powerful for gamedev:

**Subagents** — Delegate isolated tasks without polluting the main context:
```
# Example: audit performance while working on a feature
"Spawn a subagent to analyze all Update() methods in Scripts/
 for performance anti-patterns. Report findings but don't modify files."
```

**Slash Commands** — Repetitive workflows with a single command:
```markdown
# .claude/commands/unity-component.md
Create a new Unity MonoBehaviour component:
- Name: $ARGUMENTS
- Add [RequireComponent] where appropriate
- Use SerializeField for inspector fields
- Add summary XML docs
- Follow our naming convention: PascalCase, suffix with purpose (e.g., PlayerMovement, EnemySpawner)
```

**Hooks** — Automation on Claude Code events:
```json
// .claude/settings.json
{
  "hooks": {
    "PreToolUse": [{
      "matcher": "Edit",
      "command": "python scripts/validate_unity_meta.py"
    }]
  }
}
```

### 2.3 hcg-workflows and Multi-Phase Workflows

The emerging pattern in the community is to divide development into phases with **clean context between each one**:

**Most common pattern (4-5 phases):**

1. **Research** → Analyze the existing codebase, understand patterns, identify dependencies
2. **Design/Plan** → Architect the solution, define interfaces, plan tests
3. **Implement** → Code, using subagents for parallel tasks
4. **Verify** → Tests, code review via subagent, build validation
5. **Document** → Update docs, CLAUDE.md, comments

**Why separate phases?** LLM context degrades with size. A clean phase = consistent quality.

Projects like `claude-code-workflows` (shinpr) and `claude-code-spec-workflow` (Pimzino) implement variations of this pattern with dedicated slash commands for each phase.

### 2.4 CLAUDE.md — The Secret Weapon for Unity

The `CLAUDE.md` file at the root (and in subfolders!) is where you teach Claude about your project:

```markdown
# CLAUDE.md (Unity project root)

## Project
Survivor roguelike 2D, Unity 6.1 LTS, URP pipeline.

## Architecture
- Scripts/Core/ → Singletons, managers (GameManager, AudioManager)
- Scripts/Gameplay/ → Gameplay MonoBehaviours
- Scripts/Data/ → ScriptableObjects (configs, balance)
- Scripts/UI/ → UI Toolkit components

## Conventions
- Naming: PascalCase for classes, _camelCase for private fields with [SerializeField]
- Configs via ScriptableObject, NEVER magic numbers in code
- Events via ScriptableObject channels (SO-based event system)
- New systems: create interface first, then implementation

## What NOT to do
- DO NOT edit .meta files directly
- DO NOT edit .unity or .prefab files (serialized YAML)
- DO NOT use Find() or GetComponent() in Update()
- DO NOT create new singletons without approval

## How to test
- Play mode tests in Tests/PlayMode/
- Edit mode tests in Tests/EditMode/
- Run: Unity > Window > General > Test Runner
```

---

## 3. Native Unity AI

### 3.1 From Muse to Unity AI (6.2+)

**Timeline:**
- 2023-2024: Unity Muse (chat, sprite gen, texture gen) — experimental, mediocre results
- August 2025: Unity 6.2 launches **Unity AI**, completely replacing Muse
- January 2026: Unity AI Beta 2026 — improved agentic capabilities

**Three components of Unity AI:**

**Assistant** (replaces Muse Chat):
- "Project-aware" chat inside the Editor
- Answers documentation queries
- Batch rename assets
- Writes and executes C# code
- Runs on GPT (Azure OpenAI) + Meta Llama
- *Assessment:* Useful for quick questions and simple tasks. For complex code, Claude/Cursor are still superior.

**Generators** (replaces Muse Generate):
- Generates sprites, textures, materials, animations, and sounds
- Uses third-party models (not only Unity's)
- *Assessment:* Textures and sprites are "ok for placeholder". Does not replace artists for final assets. Animations are basic. Sounds are surprisingly useful for prototypes.

**Inference Engine** (rebrand of Sentis):
- Local execution of ML models (ONNX format, opset 7-15)
- Runs on CPU or GPU
- Does not send data to the cloud
- Future ML-Agents support
- *Assessment:* A real, functional tool for running neural networks in-game (NPC behavior, procedural generation, style transfer). Not for code generation — it is for ML runtime.

### 3.2 Sentis / Inference Engine — When to Use

**Real use cases:**
- NPC decision-making with trained models
- Procedural content generation via neural nets
- Image recognition for AR/VR
- Local voice/gesture recognition
- Runtime style transfer on textures

**Do not confuse with:** AI that writes code. Sentis runs trained models inside the game; it has nothing to do with coding assistants.

---

## 4. Known Limitations

### 4.1 Unity Metadata (.meta files)

Every asset in Unity generates a `.meta` file with a unique GUID. That GUID is how Unity references assets internally.

**The problem for AI:**
- If the AI creates a new script, it **cannot generate the .meta** — Unity needs to generate the GUID
- If the AI deletes a file without deleting the .meta, references break
- If the AI moves files, the .meta files must move with them
- GUIDs are the invisible glue of the project; AI does not "see" these connections

**Mitigation:** Use MCP tools (`manage_asset`, `create_script`) that delegate creation to Unity, rather than having the AI write files directly.

### 4.2 YAML Serialization (Scenes and Prefabs)

Unity scenes (`.unity`) and prefabs (`.prefab`) are serialized YAML with:
- Numeric fileIDs for each object
- GUIDs referencing scripts and assets
- Encoded transform hierarchies
- Hundreds of lines for simple scenes

**The problem for AI:**
```yaml
--- !u!1 &674301646
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 674301647}
  - component: {fileID: 674301649}
  m_Layer: 0
  m_Name: Enemy
```

No current LLM reliably generates valid Unity YAML. The fileIDs and GUIDs must be internally consistent, and any error results in a corrupted scene.

**Golden rule:** AI should never edit .unity, .prefab, or .asset files directly. Use MCP tools or manipulate via C# code (Editor scripts).

### 4.3 Prefabs and Scene Instances

Prefabs with overrides, nested prefabs, and prefab variants create layers of serialization that are effectively impossible for AI to generate:
- Override values that reference the base prefab
- Nested prefabs with their own fileIDs
- Variant chains where changes cascade

**What AI can do:** Generate the *C# script* that will be added to the prefab. The human (or MCP tool) handles the wiring in the Editor.

### 4.4 Context Window vs. Real Projects

| Scenario | Estimated size | Fits in context? |
|---|---|---|
| A simple script | 50-200 lines | Yes |
| A system (5-10 scripts) | 500-2000 lines | Yes, with care |
| Full module (20+ scripts) | 5000+ lines | Partially |
| Entire project (100+ scripts) | 50,000+ lines | No |

**Strategies:**
- CLAUDE.md with a project map (AI knows where to look without reading everything)
- Subagents to research specific parts
- Development phases with clean context
- Convention-over-configuration (AI infers patterns from naming)

### 4.5 Other Problematic Areas

- **Shaders:** HLSL/ShaderLab is niche; LLMs have little training data, especially for custom URP/HDRP shaders
- **Animation:** Animator controllers, blend trees, and animation clips are complex binary/YAML assets
- **Physics:** Configuring physics materials, layers, and collision matrices requires understanding the Editor
- **Addressables/Asset Bundles:** Complex configuration that depends on the Editor UI
- **UI Toolkit (USS/UXML):** AI generates reasonable results, but the C# binding frequently has bugs

---

## 5. Practical Strategies

### 5.1 Prompt Structure for Unity

**Bad prompt:**
> "Create an inventory system"

**Good prompt:**
> "Create an InventorySystem for our survivor roguelike 2D in Unity 6 (URP). Requirements:
> - MonoBehaviour singleton accessible via InventorySystem.Instance
> - Items defined as ScriptableObject (ItemData) with: name, icon (Sprite), stackable (bool), maxStack (int)
> - InventorySlot struct with: ItemData reference, currentStack int
> - Events via C# Action<> for: OnItemAdded, OnItemRemoved, OnInventoryChanged
> - Public methods: AddItem(ItemData, int), RemoveItem(ItemData, int), HasItem(ItemData, int) → bool
> - Serialize with [SerializeField] private List<InventorySlot> for Inspector debug
> - DO NOT use Find() or Resources.Load()
> - Follow convention: _camelCase for private fields, PascalCase for public"

### 5.2 Conventions That Make AI More Effective

**Predictable folder structure:**
```
Assets/
├── Scripts/
│   ├── Core/           # Managers, singletons, utils
│   ├── Gameplay/       # Game MonoBehaviours
│   ├── Data/           # ScriptableObjects
│   ├── UI/             # UI scripts
│   └── Editor/         # Custom editors, tools
├── ScriptableObjects/  # .asset files (SO instances)
├── Prefabs/
├── Scenes/
├── Art/
├── Audio/
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

**Clear naming conventions:**
- Classes: `PlayerMovement`, `EnemySpawner`, `WeaponData`
- Interfaces: `IDamageable`, `IInteractable`
- ScriptableObjects: `*Data` (config), `*Channel` (events), `*Set` (runtime sets)
- Suffixes by purpose: `*Manager`, `*Controller`, `*Handler`, `*Factory`

**ScriptableObject-based config:**
```csharp
// AI understands and generates this perfectly
[CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Stats")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private int _damage = 10;

    [Header("Visuals")]
    [SerializeField] private Sprite _sprite;
    [SerializeField] private RuntimeAnimatorController _animator;

    // Public read-only properties
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public int Damage => _damage;
    public Sprite Sprite => _sprite;
}
```

**SO-based event system (AI-friendly):**
```csharp
[CreateAssetMenu(menuName = "Events/Void Event Channel")]
public class VoidEventChannel : ScriptableObject
{
    private Action _onEventRaised;

    public void RaiseEvent() => _onEventRaised?.Invoke();
    public void Subscribe(Action listener) => _onEventRaised += listener;
    public void Unsubscribe(Action listener) => _onEventRaised -= listener;
}
```

This is transparent to AI: everything is pure C#, with no dependency on Editor wizards.

### 5.3 Code Review of AI Output

**Checklist for reviewing AI-generated Unity code:**

1. **Correct lifecycle methods?** — `Awake()` for self-initialization, `Start()` for external references, `OnEnable()`/`OnDisable()` for events
2. **Performance in loops?** — No `GetComponent<>()`, `Find()`, or `Camera.main` in `Update()`
3. **Null checks?** — Unity overloads the `==` operator; `?.` on `UnityEngine.Object` can mask destroyed objects
4. **Correct serialization?** — `[SerializeField]` on private fields, not exposed public fields
5. **Memory leaks?** — Unsubscribe from events in `OnDisable()`, not just in `OnDestroy()`
6. **Thread safety?** — Unity API only works on the main thread; AI sometimes suggests async patterns that break this
7. **Coroutines vs async/await?** — Unity 6 supports Awaitable, but AI may mix patterns

### 5.4 When AI Helps vs. When It Hurts

**AI excels at:**
- MonoBehaviour, ScriptableObject, and Editor script boilerplate
- Implementing known patterns (state machine, object pool, observer)
- Writing unit tests for isolated logic
- Refactoring existing code following defined patterns
- Generating utilities (extensions, helpers, serialization)
- Documentation and XML comments

**AI is mediocre at:**
- Integration between existing systems (requires a lot of context)
- UI Toolkit layouts (generates UXML/USS but binding frequently fails)
- Custom shader code (little training data for URP/HDRP)
- Performance optimization (needs real profiling data, not intuition)

**AI gets in the way when:**
- It tries to edit scenes/prefabs directly
- It generates "over-engineered" architectures for simple projects
- It suggests patterns that don't match the project's existing ones
- It makes "confident bullshit" claims about Unity APIs that changed between versions
- It tries to solve visual/art problems without seeing the result

---

## 6. Alternatives: Cursor, Windsurf, Copilot

### 6.1 Comparison for Unity/C#

| Aspect | Claude Code | Cursor | Windsurf | GitHub Copilot |
|---|---|---|---|---|
| **Base** | Terminal (CLI) | VS Code fork | VS Code fork | Multi-IDE extension |
| **Price** | Anthropic plans | $20/month (Pro) | $15/month (Pro) | $10/month (Individual) |
| **MCP Support** | Native | Yes | Yes | Limited |
| **Unity MCP** | Yes (all) | Yes (CoplayDev) | Yes (CoplayDev) | Partial |
| **C# quality** | Very good | Very good | Good | Good |
| **Agentic mode** | Subagents | Composer + Subagents | Cascade | Copilot Agent |
| **Context handling** | CLAUDE.md | .cursorrules + codebase indexing | Similar to Cursor | Codebase indexing |
| **Best for** | Complex workflows, automation | Multi-file refactoring | Autonomous tasks with less steering | Broad IDE integration |

### 6.2 Honest Assessment by Tool

**Claude Code:**
- Pros: Powerful subagents, granular CLAUDE.md, hooks for automation, best for architecture
- Cons: Terminal-only (no visual), learning curve, usage-based cost

**Cursor:**
- Pros: Excellent codebase indexing, intuitive UI, `.cursorrules` for context, good for Unity since it's VS Code-based
- Cons: Higher monthly cost, can be overly opinionated with suggestions

**Windsurf:**
- Pros: Cascade is the best autonomous mode, competitive pricing, great for "vibe coding"
- Cons: Smaller ecosystem, less community documentation for Unity

**GitHub Copilot:**
- Pros: Works in Visual Studio (official Unity IDE), affordable pricing, native GitHub integration
- Cons: More limited MCP support, less agentic, completion-focused rather than agent-focused

### 6.3 Practical Recommendation

For **solo Unity dev in 2026**, the most productive combination is:

- **Main editor:** Cursor or VS Code + Copilot (for daily autocomplete)
- **Complex tasks:** Claude Code (for architecture, refactoring, multi-file changes)
- **Editor bridge:** CoplayDev/unity-mcp (for direct Editor manipulation)
- **Rapid prototyping:** Windsurf Cascade (for "vibe coding" isolated features)

---

## 7. Case Study: Epic Survivors — MonoGame for AI Transparency

### 7.1 Context

Epic Survivors is a survivor roguelike developed over 14 months using AI as the primary development tool (started with Cursor 3.5). The crucial decision was **abandoning Unity in favor of MonoGame** for reasons of AI transparency.

### 7.2 Why MonoGame?

**The problem with Unity for AI:**
- `.meta` files, serialized YAML, and prefabs are **opaque** to LLMs
- The Unity Editor is a black box — much logic lives in wizards and inspectors
- GUID-based references are invisible to AI
- Context window wasted on serialization, not on logic

**The MonoGame advantage:**
- **100% code-first**: No visual editor, everything is pure C#
- **No binary serialization**: Configs in human-readable YAML/JSON
- **No .meta files**: References are direct paths in code
- **Total transparency**: AI reads and understands the entire project

### 7.3 Three-Layer Architecture

```
┌──────────────────────────────────────┐
│           Game Layer                  │  ← YAML configs, game-specific logic
├──────────────────────────────────────┤
│     Custom Game Engine               │  ← Engine for survivor roguelikes
├──────────────────────────────────────┤
│         MonoGame Framework           │  ← Rendering, input, audio
└──────────────────────────────────────┘
```

**Data-driven design:** The entire game can change without touching C# — just by swapping YAML files. For custom behavior, a `.cs` file following naming conventions is auto-discovered by the system.

### 7.4 Notable AI Practices

- **Nested CLAUDE.md files**: Context files in each folder (e.g., an enemy config folder has its own CLAUDE.md explaining the schema)
- **AI autoplay for testing**: Instead of traditional unit tests, the game has an autoplay mode that AI can observe for balance testing, performance testing, and regression testing
- **Separation blurred over time**: The clean separation between engine and scripts ended up mixing in practice — an important lesson about architectural ambition vs. reality

### 7.5 Lessons for Those Staying on Unity

Even when staying on Unity, these lessons apply:
- **Maximize pure code**: ScriptableObjects, C# events, config files
- **Minimize Editor dependency**: Avoid logic in inspectors, prefer code-driven setup
- **CLAUDE.md per folder**: Give AI local context
- **Data-driven where possible**: YAML/JSON configs that AI can read and generate

---

## 8. Recommendations for Solo Developers

### 8.1 Recommended Stack (2026)

```
Unity 6.x LTS
+ CoplayDev/unity-mcp (AI ↔ Editor bridge)
+ Claude Code (complex tasks, architecture)
+ Cursor or VS Code + Copilot (daily autocomplete)
+ CLAUDE.md in each project folder
+ ScriptableObject-based architecture
+ SO event channels for communication
+ Git + proper .gitignore for Unity
```

### 8.2 Daily Workflow

1. **Start of day**: Review CLAUDE.md, update if necessary
2. **New feature**: Claude Code → Phase: Research → Plan → Implement → Verify
3. **Bug fix**: Cursor/Copilot (local context, fast)
4. **Refactoring**: Claude Code with subagents for broad analysis
5. **Asset setup**: MCP tools via Claude to create/configure in the Editor
6. **Playtesting**: Manual + autoplay scripts where possible
7. **End of day**: Commit with a clear description, update CLAUDE.md if architecture changed

### 8.3 Golden Rules

1. **AI generates C# code, human does wiring in the Editor** — Never let AI edit .unity/.prefab files
2. **Context is king** — A well-written CLAUDE.md is worth more than any sophisticated tool
3. **Phases with clean context** — Don't ask AI to "do everything at once"
4. **Review everything** — AI generates code that compiles but can have subtle logic bugs
5. **Convention-over-configuration** — Predictable naming and folder structure = more accurate AI
6. **ScriptableObjects are your friends** — Data separated from logic = AI understands both better
7. **Start simple** — Don't install 48 agents. A good CLAUDE.md + Claude Code already goes a long way

---

## 9. Sources

### Unity MCP
- [CoplayDev/unity-mcp — GitHub](https://github.com/CoplayDev/unity-mcp)
- [CoplayDev/unity-mcp Wiki & Roadmap](https://github.com/CoplayDev/unity-mcp/wiki/Project-Roadmap)
- [YetAnotherUnityMcp — GitHub](https://github.com/Azreal42/YetAnotherUnityMcp)
- [IvanMurzak/Unity-MCP — GitHub](https://github.com/IvanMurzak/Unity-MCP)
- [Unity MCP Overview — Unity Docs](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.0/manual/unity-mcp-overview.html)
- [Unity MCP | Awesome MCP Servers](https://mcpservers.org/servers/github-com-coplaydev-unity-mcp)

### Claude Code for Gamedev
- [Claude Code Game Studios — GitHub](https://github.com/Donchitos/Claude-Code-Game-Studios)
- [Claude Code Customization Guide (CLAUDE.md, Skills, Subagents)](https://alexop.dev/posts/claude-code-customization-guide-claudemd-skills-subagents/)
- [Understanding Claude Code's Full Stack: MCP, Skills, Subagents, Hooks](https://alexop.dev/posts/understanding-claude-code-full-stack/)
- [Claude Code Hooks Guide — Official Docs](https://code.claude.com/docs/en/hooks-guide)
- [Claude Code Common Workflows — Official Docs](https://code.claude.com/docs/en/common-workflows)
- [claude-code-spec-workflow — GitHub](https://github.com/Pimzino/claude-code-spec-workflow)
- [awesome-claude-code — GitHub](https://github.com/hesreallyhim/awesome-claude-code)

### Native Unity AI
- [Unity AI in Unity 6.2 — CG Channel](https://www.cgchannel.com/2025/08/unity-rolls-out-unity-ai-in-unity-6-2/)
- [Unity AI Beta 2026 — Unity Discussions](https://discussions.unity.com/t/unity-ai-beta-2026-is-here/1703625)
- [Unity Goes All-In on Gen AI — 80.lv](https://80.lv/articles/unity-goes-all-in-on-generative-ai-introducing-a-bunch-of-ai-features-in-6-2-update)
- [Sentis / Inference Engine — Unity Docs](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.4/manual/index.html)
- [Unity AI Features Overview](https://unity.com/features/ai)

### Limitations and Serialization
- [Understanding Unity's Serialization Language, YAML — Unity Blog](https://unity.com/blog/engine-platform/understanding-unitys-serialization-language-yaml)
- [Why AI Writes Better Game Code in Godot Than in Unity — DEV](https://dev.to/mistyhx/why-ai-writes-better-game-code-in-godot-than-in-unity-10hf)

### Alternatives (Cursor, Windsurf, Copilot)
- [Cursor vs Windsurf vs Copilot: Best AI IDE 2026](https://www.buildmvpfast.com/blog/cursor-vs-windsurf-vs-copilot-best-ai-ide-2026)
- [AI Coding Assistants in 2026 — DEV](https://dev.to/kainorden/ai-coding-assistants-in-2026-cursor-vs-github-copilot-vs-windsurf-2mm9)
- [Cursor vs GitHub Copilot vs Windsurf — builder.io](https://www.builder.io/blog/cursor-vs-windsurf-vs-github-copilot)

### Case Study: Epic Survivors
- [Epic Survivors: 14 Months of Development](https://web3dev1337.github.io/epic-survivors-architecture/)

### Practices and Architecture
- [ScriptableObject-based Runtime Sets — Unity](https://unity.com/how-to/scriptableobject-based-runtime-set)
- [Modular Game Architecture with ScriptableObjects — Unity](https://assets2.brandfolder.io/bf-boulder-prod/3fctx42nqsmx4f9jj7gmjn3q/v/1131033164/original/create-modular-game-architecture-in-unity-with-scriptableobjects.pdf)
- [Using Claude AI in Game Development — Kevuru Games](https://kevurugames.com/blog/using-claude-ai-in-game-development-tools-use-cases-and-industry-statistics/)
