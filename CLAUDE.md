# MCP Game Deck — Claude Code Context

> **Purpose:** This file is loaded by Claude Code at the start of every session. It contains project conventions, coding standards, and hard constraints. Keep it focused — details belong in referenced files, not here.

---

## Project Overview

**MCP Game Deck** is an open-source Unity Editor package (MIT) that bridges Claude AI to Unity via MCP (Model Context Protocol).

- Repo: https://github.com/RamonBedin/mcp-game-deck
- Current version: **v1.1.0** (see `package.json`)
- Unity target: **6000.0+** (uses Unity 6000.3 APIs in places — see gotchas)
- Test project: Jurassic Survivors (2D URP roguelike) — not in this repo

**Components:**
- C# MCP Server (`TcpListener` with `ReuseAddress`) — Unity Editor side
- TypeScript MCP Proxy — bridges stdio ↔ TCP (`Server~/`)
- Agent SDK Server — manages Claude conversations (`Server~/`)
- Embedded Chat UI (UIToolkit) — inside Unity Editor (being phased out in v2.0 — see `docs/internal/`)
- **268 MCP tools** across ~38 domains under `Editor/Tools/`
- Curated Unity knowledge layer (`Editor/Tools/UnityDocs/`, `Editor/Tools/UIToolkit/`)

---

## 🎯 Current Focus: v2.0

**v2.0 (external app + orchestrator architecture) is the priority deliverable.** Tool consolidation work is paused and resumes in v2.1.x once v2.0 ships.

Roadmap: `docs/internal/roadmap.md`. v2.0 features documented per-feature in `docs/internal/v2-features/`.

---

## 🚫 HARD CONSTRAINTS

**Ramon owns all git operations.** He uses VS Code's Source Control panel for staging, diff review, and committing. **No agent (subagent or main session) runs any git command.** Even `git status` is off-limits — Ramon's IDE shows that already.

**Bash is allowed for build / validation only:**
- `dotnet build`, `dotnet test`, `dotnet --version`
- `npm run build`, `npm test`, `tsc`, `npx tsc`, `node --version`

Anything else with Bash (especially `git`, `gh`, `rm -rf`) is denied via `.claude/settings.json`. Do not attempt to bypass.

**Subagent toolsets** (defined per agent in `.claude/agents/*.md` frontmatter) are stricter than the global allow list:
- `tool-auditor`: Read, Grep, Glob, Write
- `auto-reviewer`: Read, Grep, Glob, Write
- `consolidation-planner`: Read, Grep, Glob, Write
- `tool-consolidator`: Read, Grep, Glob, Edit, Write (no Bash at all)
- `build-validator`: Read, Grep, Glob, Bash, Write (Bash for `dotnet`/`tsc` only)
- `audit-batch-runner`: Read, Grep, Glob, Write, Edit, Task (orchestrator — runs as main session, not subagent)

---

## Directory Structure

```
.
├── CLAUDE.md                 ← you are here
├── docs/internal/            ← roadmap, v2 architecture, feature docs
├── .claude/
│   ├── settings.json         ← permission deny/allow list (versioned)
│   ├── settings.local.json   ← local overrides (gitignored)
│   ├── agents/               ← specialized subagents + audit-batch-runner orchestrator
│   ├── templates/            ← review template (auto-reviewer reads this for output format)
│   ├── state/                ← batch progress files (audit-batch-progress.json)
│   └── reports/              ← audits/reviews/plans/validations (paused; resumes v2.1.x)
├── Editor/
│   └── Tools/                ← ~38 domains, 268 tools (current state, no consolidation yet)
├── Server~/                  ← TypeScript MCP Proxy + Agent SDK Server
├── App~/                     ← (NEW in v2.0) Tauri app: src-tauri/, src/, dist/
├── Agents~/                  ← agent definitions (runtime, not .claude/)
├── KnowledgeBase~/
├── Skills~/
├── package.json              ← Unity package manifest
└── README.md
```

**Note:** Folders ending in `~` are hidden from Unity's asset pipeline by convention. Do not rename them.

---

## Refactor Pipeline (PAUSED — resumes in v2.1.x)

> ⏸ **The tool refactor pipeline is fully built but paused.** v2.0 is the priority. Tool consolidation will run in the new v2.0 home (external Tauri app), where each refactor can be validated against its real production context. The 41 audits already in `.claude/reports/audits/` are preserved as input for v2.1.x cycles.

When v2.1.x starts, the pipeline below picks up where it left off:

```
1. tool-auditor          → .claude/reports/audits/audit-[domain]-[YYYYMMDD].md
       ↓ (Ramon skims audit; re-run if code drifted significantly during v2.0)
2. auto-reviewer         → .claude/reports/reviews/review-[domain]-[YYYYMMDD].md
       ↓ (Ramon answers escalations directly in the file)
       ↓ (re-invoke auto-reviewer to finalize → Status: READY FOR PLANNING)
3. consolidation-planner → .claude/reports/plans/plan-[domain]-[YYYYMMDD].md
       ↓ (Ramon reviews plan, marks READY FOR EXECUTION)
4. tool-consolidator     → edits .cs files in Editor/Tools/
       ↓ (file edits land on disk)
5. build-validator       → .claude/reports/validations/validation-[domain]-[YYYYMMDD].md
       ↓ (Ramon reviews validation; if FAILED, decide: re-run consolidator or fix manually)
6. Ramon reviews diff in VS Code Source Control → commits via VS Code
```

**Existing artifacts to pick up when v2.1.x starts:**

- 41 audits in `.claude/reports/audits/`
- Animation review draft (Ramon-written, pre-auto-reviewer) in `.claude/reports/reviews/`
- GameObject review draft (auto-reviewer + Ramon's escalation answers in chat history) — needs to be merged and finalized
- Sentinel convention decision: nullable string `"true" | "false" | ""` for "leave unchanged" booleans

The pipeline is generic — same agents work on every domain. When work resumes, prioritize Tier 1 domains (GameObject, Component, Prefab, Scene, Asset, Script, Editor, Selection) per the April 2026 batch summary.

---

## C# Coding Standards (STRICTLY ENFORCED)

These are non-negotiable. Violations must be fixed before any PR.

### XML Documentation
- Public methods require XML doc comments
- `<param>` tags required for every parameter
- `<returns>` tag required when method returns a value
- **Partial classes:** XML doc summary goes on EXACTLY ONE file — the one containing `[McpToolType]`. All other partial files have summaries removed (no duplication).

### Braces
- `if` blocks **always** use braces, even for single-line returns. No exceptions.

```csharp
// ✅ correct
if (clip == null)
{
    return ToolResponse.Error("Clip not found.");
}

// ❌ wrong
if (clip == null) return ToolResponse.Error("Clip not found.");
```

### Error Handling
- Empty `catch` blocks are forbidden — must log the error
- Use `McpLogger.Error(...)` or `Debug.LogWarning(...)`
- **`McpLogger` only has `Info` and `Error`** — there is NO `Warning` method. Use `Debug.LogWarning` for warnings.

### Language Features (Unity 6000.3 / C# 9.0)
- **Null-conditional assignment is ILLEGAL:** `obj?.prop = value` does NOT compile. Use explicit null check.
- Use `EntityIdToObject(...)` — **not** deprecated `InstanceIDToObject(...)`
- Unity 6000.3 supports implicit `int` → `EntityId` cast; leverage it
- Pattern matching preferred: `is not T variable` over `as T` casts

```csharp
// ✅ correct
if (asset is not AnimationClip clip)
{
    return ToolResponse.Error("Asset is not an AnimationClip.");
}

// ❌ avoid
var clip = asset as AnimationClip;
if (clip == null) { ... }
```

---

## MCP Tool Conventions

### File Layout
- One file per tool action: `Tool_[Domain].[Action].cs`
- Partial classes within a domain
- Example: `Editor/Tools/Animation/Tool_Animation.CreateClip.cs`

### Attribute Usage

```csharp
[McpTool("domain-action-name", Title = "Domain / Action Title")]
[Description("One-line summary. Use imperative voice. Include disambiguation if other tools are similar.")]
public ToolResponse MyAction(
    [Description("What this param is. Example value if non-obvious.")] string paramName,
    [Description("Optional param. Defaults to 'X'.")] string optional = "X"
)
```

**Critical:** Tool descriptions must come from `[System.ComponentModel.Description]` attribute on the method. The `toolAttr.Description` property is always empty — do not use it.

### Sentinel Convention (decided April 2026, applies to all v2.1.x consolidations)

For "leave unchanged" parameters, use **nullable string sentinel**: `"true" | "false" | ""` for booleans, `""` for strings, `-1` for ints where natural range excludes -1. Do NOT use int tri-state for booleans (`1/0/-1`) — that's the legacy organic pattern being replaced.

### Response Pattern
- Return `ToolResponse.Text(...)` for success with message
- Return `ToolResponse.Error(...)` for failures
- Wrap Unity API calls in `MainThreadDispatcher.Execute(() => { ... })`

### Marking Read-Only Tools
- Add `ReadOnlyHint = true` to `[McpTool(...)]` for inspection-only tools

---

## Known Gotchas (DO NOT RE-LITIGATE)

These were debugged and fixed. Do not re-open.

- **Proxy ESM temporal dead zone:** `mcp-proxy.js` `ALLOWED_HOSTS` must be declared before use. Fixed in v1.0.1.
- **Auto permission mode:** Uses `bypassPermissions`, NOT `acceptEdits`.
- **Session Cost locale:** Uses `CultureInfo.InvariantCulture` for decimal formatting. Fixed in v1.0.3.
- **HttpListener replaced:** Use `TcpListener` with `ReuseAddress` to avoid `EADDRINUSE :8090` on assembly reload.
- **Assembly reload:** Must `LockReloadAssemblies()` during AI generation. Use `beforeAssemblyReload` → stop server; `afterAssemblyReload` → restart; `EditorApplication.quitting` → final cleanup. (Note: in v2.0 this becomes obsolete since chat lives outside Unity.)
- **Event subscriptions:** Always `-=` before `+=` to avoid double-subscription after reload.
- **`dist/` in `.gitignore`:** Compiled TypeScript is not versioned. Don't commit it.
- **Filesystem MCP tool unreliable on paths containing `@`** (Unity PackageCache). Work against source repo via `file:` reference instead.

---

## Specialized Agents (`.claude/agents/`)

Subagents for focused, repeatable tasks. Each has a narrow mission and restricted toolset. **All currently paused — used in v2.1.x.**

| Agent | Role | Status | Purpose |
|-------|------|--------|---------|
| `tool-auditor` | subagent | ✅ ready, validated on Animation, used in batch April 2026 | Analyze a domain, produce diagnostic report. **Never modifies code.** |
| `auto-reviewer` | subagent | ✅ ready, partially tested on GameObject | Draft review file: auto-decide mechanical findings, escalate strategic ones. **Never modifies code.** |
| `consolidation-planner` | subagent | ✅ ready, not yet tested | Take audit + finalized review, propose refactor plan with concrete signatures. **Never modifies code.** |
| `tool-consolidator` | subagent | ✅ ready, not yet tested | Execute approved plan: edit code. **No Bash, no git.** |
| `build-validator` | subagent | ✅ ready, not yet tested | Static checks + `dotnet build` (when csproj exists) + `tsc --noEmit`. **No git.** |
| `audit-batch-runner` | **orchestrator** | ✅ ready, used April 2026 (41 audits) | Run `tool-auditor` across many domains. Resumable, state-tracked. Runs as main session, not subagent. |

See `.claude/agents/README.md` for invocation and pipeline notes.

---

## v2.0 Architecture

External Tauri app (`App~/`) replaces the in-Unity chat window. Per-feature docs in `docs/internal/v2-features/` (9 features).

**v2.0 entry points (start order):**

1. Feature 07 (Editor Status Pin) — small, replaces ChatWindow as the in-Unity surface
2. Feature 01 (External App scaffolding) — Tauri/React skeleton, IPC stubs
3. Feature 02 (Orchestrator agent) — single-chat multi-subagent routing
4. Then 03 (slash commands), 04 (interactive plan), 05 (permission fix), 06 (plans CRUD), 08 (rules page), 09 (UI polish)

ADRs in `docs/internal/decisions/` capture cross-cutting choices (e.g. Tauri over Electron).

---

## Communication Preferences (Ramon)

- Language: Portuguese (PT-BR) or English — match user's language
- Format for changes: **diffs** for modifications, **clean code blocks** (no `+` markers) for new additions
- **Diagnose before fixing.** When something breaks, ask for terminal/console logs before proposing a fix. Do not guess.
- **Do not make unsolicited changes.** If asked a question, answer it — don't silently apply a code change instead.
- Dry-run previews before applying file edits. Wait for explicit approval ("sim", "pode") before writing.
- **Ramon owns git.** Do not run git commands proactively.
- **Ramon is a senior dev.** When he makes strategic/scope decisions (priority, milestone ordering, what to defer), execute them — don't relitigate. Push back only on technical issues (API doesn't exist, race condition, code that won't compile), not on scope.
