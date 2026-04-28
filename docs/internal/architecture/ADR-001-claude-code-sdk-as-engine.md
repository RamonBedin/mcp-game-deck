# ADR-001 — Claude Agent SDK as the Tauri engine (Caminho B)

> **Status:** `accepted` — 2026-04-28
> **Supersedes:** original v2.0 architecture for Features 01–08 (in-package custom Agent SDK Server as the chat engine)
> **Companion:** Unity Package distribution remains the only delivery channel (Distribution A — see "Distribution" section)

## Context

The MCP Game Deck v1.x ships an in-Editor Chat UI that runs a custom Agent SDK Server (`Server~/agent-sdk-stub.js` + `Server~/src/`) under the hood. The chat is self-contained: agents in `Agents~/`, skills in `Skills~/`, slash commands, and prompts all live behind a custom orchestrator that you control end-to-end.

The original v2.0 plan (Features 01–08 as written in `docs/internal/v2-features/`) preserves this model. The chat UI moves out of the Editor into a Tauri desktop app, but the engine stays the same: Tauri spawns the same custom Agent SDK Server as a child process, talks to it over stdio + JSON-RPC 2.0, and that server is the brain that orchestrates agents, parses slash commands, manages permissions, and dispatches MCP tool calls to the C# server inside Unity.

External feedback received in late April 2026 surfaced the limitation of this design clearly:

> "The Game Deck MCP server is well-built. The limit is the embedded Chat UI: it acts as its own MCP client with the agents, commands and context that the package decides. There's no way to extend it from the outside. It can't be combined with the user's other MCPs (GitHub, Atlassian, filesystem) and Spec-Kit doesn't run on top of it because it doesn't read the commands and templates Spec-Kit generates in the project. When the client is open — like Claude Code pointing at the Unity MCP — everything unlocks: same agent, same session, multiple MCPs stacked, the user's own slash commands, Spec-Kit, and the project's `CLAUDE.md` as context."

The criticism is not about the MCP server. The 268 tools, the resources, the prompts, the C# side — all of that is solid and is exactly what the user wanted. The criticism is about the **chat client** being a closed system. Moving that closed system from the Editor to a Tauri window does not fix it; it just relocates the silo.

## Decision

The Tauri app will not host a custom Agent SDK Server. It will embed Anthropic's official `@anthropic-ai/claude-agent-sdk` package and use it to spawn Claude Code as a subprocess. Tauri owns the UI and the IPC plumbing to Unity. Claude Code owns the agent loop, the orchestration, the slash commands, the skills system, the permission machinery, the memory, and the multi-MCP composition.

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
┌─ UNITY EDITOR (existing) ────────────────────────────────────┐
│  C# MCP Server — 268 tools, 7 resources, 5 prompts            │
│  Toolbar pin (Feature 07)                                     │
│  Project settings, update checker, etc.                       │
└──────────────────────────────────────────────────────────────┘
```

The C# MCP Server inside Unity does not change. The toolbar pin does not change. The communication between Tauri and Unity does not change. What changes is the brain inside the Tauri side — it goes from "our custom code" to "Claude Code itself".

## Consequences

### Positive

- **The feedback is resolved at the architectural level.** The user gets a UI dedicated to Unity workflows (Tauri opens via the pin, focused experience) AND gets every capability Claude Code has — multi-MCP composition, custom slash commands, custom skills, sub-agents, plugins, hooks, the user's `CLAUDE.md`, Spec-Kit, web search, code execution, file system tools, memory across sessions. Nothing from the Anthropic ecosystem is lost.
- **The product positioning is honest.** "MCP Game Deck is the best MCP server for Unity, plus a Unity-focused desktop app that gives you Claude Code with all your other tooling already wired in" is a coherent pitch. The previous pitch ("we have our own chat client that does everything you need for Unity") is what the user just rejected.
- **Implementation effort drops.** Feature 02 (orchestrator) becomes zero work — Claude Code already orchestrates. Feature 03 (slash commands) becomes zero work for built-ins and zero work for user commands — Claude Code reads `.claude/commands/` and `.claude/skills/` natively. The custom `Server~/` stops being a maintenance burden.
- **Updates from Anthropic come for free.** When Claude Code ships a new feature (better permission modes, new built-in skills, agent improvements), the Tauri app gets it automatically on the next subprocess version bump.
- **The 10 agents in `Agents~/` and the skills in `Skills~/` keep their value.** Same Markdown format, same role. They just stop being consumed by a custom orchestrator and start being consumed by the same kind of orchestrator that Claude Code runs everywhere else.

### Negative

- **Hard dependency on Claude Code's CLI behavior.** Output format changes, breaking releases, or platform-specific bugs will land on you. The community has reported Windows-specific issues with stdin piping (Issue #7263) and Windows subprocess invocation (the `dstreefkerk` post from January 2026). Mitigation: pin a tested Claude Code version in the package, validate upgrades before bumping, document recovery if the user's local Claude Code drifts.
- **The user must have a Claude account.** Claude Code requires authentication via Anthropic (API key or login). MCP Game Deck cannot work without a logged-in `claude` instance available on the user's machine. This is a real hurdle for first-time users — needs to be handled by clear onboarding, not silent failure.
- **The user must have Node.js installed.** This was already true for the proxy (`Server~/mcp-proxy.js`). The bar does not rise; it just stays.
- **Some MCP-tool-from-subagent edge cases are documented as buggy.** Claude Code Issue #25200 reports custom agents not always picking up MCP tools declared in their frontmatter. Issue #34935 is a feature request for better subagent MCP support. For our 10 agents this likely doesn't bite (single MCP server, simple frontmatter), but it needs validation in real use.
- **Licensing of the Agent SDK for embedded distribution must be confirmed before public release.** The MIT license on MCP Game Deck does not automatically extend coverage to bundled npm packages. Specifically need to verify: (a) the license of `@anthropic-ai/claude-agent-sdk`, (b) Anthropic's Terms of Service for distributing software that embeds and re-exposes Claude Code as a subprocess, (c) whether end-user authentication mechanisms have any product-bundling restrictions. Treat this as a release blocker, not a planning blocker.

### Neutral

- The C# MCP Server in `Editor/MCP/` is unchanged.
- The toolbar pin (Feature 07) is unchanged.
- `Server~/mcp-proxy.js` (the STDIO ↔ HTTP bridge for external clients like Claude Desktop pointing at the Unity MCP) is unchanged. External clients keep working exactly as they do today.
- The Unity Package as a delivery mechanism is unchanged.
- The user's Unity project is not modified by installing MCP Game Deck. The user's `.claude/` directory in their project (if they have one) is read but never written.

## Distribution: Unity Package only (Distribution A)

The MCP Game Deck package ships as a Unity Package via Unity Package Manager (the existing channel). The Claude-side assets travel inside the package:

- `Agents~/<agent>.md` — 10 specialist agents
- `Skills~/<skill-name>/SKILL.md` — skills
- `Server~/` — Node-side scripts (proxy, sdk supervisor)

When Tauri spawns Claude Code as a subprocess, it passes `--add-dir <package-path>/Agents~/` and `--add-dir <package-path>/Skills~/` so Claude Code discovers our agents and skills without writing anything to the user's `.claude/` directory.

### Loading mechanism

`--add-dir` officially supports `.claude/skills/` discovery in any added directory. For agents, the documented behavior is less explicit. The implementation must handle both cases:

**Primary path:** `--add-dir <package>/Agents~/` and `--add-dir <package>/Skills~/`. If Claude Code picks up agents the same way it picks up skills, this is the only mechanism needed.

**Fallback path:** if `--add-dir` does not surface `.claude/agents/` automatically, the Tauri app copies the agents to `<unity-project>/.claude/agents/gamedeck-<original-name>.md` on first launch (or on a user-triggered "install agents" action), removes them on uninstall, and refuses to overwrite a user file with the same name. The `gamedeck-` prefix avoids namespace collision with user agents.

The decision between primary and fallback is made at implementation time by validating against the installed Claude Code version. The ADR does not lock this choice — it locks the constraint that **agents and skills ship inside the Unity Package and never require the user to install a separate Claude Code plugin or marketplace entry**.

### Why not a Claude Code plugin

Distributing as a Claude Code plugin via marketplace was considered. It was rejected because the C# MCP Server is an Editor-side artifact (Editor scripts, .meta files, package.json, Editor assemblies) that cannot be delivered through the Claude Code plugin mechanism. Splitting installation in two halves (Unity package for the Editor side + Claude Code plugin for the agents/skills) doubles the onboarding friction with no compensating gain. Single-package distribution is the simpler path.

## What changes in the codebase

### Removed or repurposed

- `Server~/agent-sdk-stub.js` — was the placeholder for the custom Agent SDK Server; not needed.
- `Server~/src/index.js` (the custom WebSocket-based Agent SDK Server) — removed. The Tauri app no longer talks to a custom Node server we wrote.
- The `node_supervisor/` module under `App~/src-tauri/src/` (planned in Feature 01 spec) — its target shifts. Instead of supervising our custom server, it supervises the `@anthropic-ai/claude-agent-sdk` Node child that hosts Claude Code. The supervision pattern (spawn / monitor / restart / clean shutdown) stays. The protocol it speaks across stdio shifts from our custom JSON-RPC 2.0 to whatever the Agent SDK exposes.

### Unchanged

- `Editor/` — the entire Unity Editor side (C# MCP Server, pin, settings, update checker, all 268 tools).
- `Server~/mcp-proxy.js` — the STDIO ↔ HTTP bridge used by external MCP clients (Claude Desktop, Cursor, etc.) to point at the C# server. Independent of the Tauri app.
- `App~/src/` — the React frontend scaffold. Routing, stores, IPC bindings, layout, Tailwind, components — all reusable.
- `App~/src-tauri/src/unity_client/` — TCP client to the C# server. Unchanged protocol, unchanged code.
- `App~/src-tauri/src/commands/` — Tauri command surface for the React side. Mostly unchanged contracts; some commands may simplify because Claude Code handles parts of what the custom server would have done.
- `Agents~/` — same 10 markdown files, same format. Distributed inside the package and surfaced to Claude Code via `--add-dir` (or copy fallback).
- `Skills~/` — same. Surfaced via `--add-dir`.
- `KnowledgeBase~/` — unchanged. Continues to be exposed via MCP resources from the C# server, or referenced from agent system prompts.

### Needs revision

- `App~/src-tauri/src/node_supervisor/protocol.rs` and `jsonrpc.rs` — currently planned to speak our custom JSON-RPC dialect to a custom server. Will speak the Agent SDK protocol instead.
- `App~/src/ipc/types.ts` — message and event types may shift to mirror Agent SDK message types more directly, instead of our custom types.

## Mapping to existing v2.0 features

The existing `docs/internal/v2-features/` documents were written before this ADR. Each is now in one of three states. The mapping below is the source of truth; the per-feature spec headers should be updated to point at this ADR.

| # | Feature | State after ADR-001 | Reason |
|---|---|---|---|
| 01 | External App (Tauri) | **Revise (small)** | Stack stays. Layout stays. The diagram's third process changes name (custom Agent SDK Server → Claude Agent SDK + Claude Code subprocess). The "Tauri ↔ Node SDK protocol" section is rewritten to reference the Agent SDK protocol. Definition-of-done item 5 (Node SDK stub echo test) and item 9 (Feature 02 plug-in point) need rewording. |
| 02 | Orchestrator Agent | **Superseded** | Claude Code orchestrates natively. The custom orchestrator described in this spec is not needed. The 10 agents in `Agents~/` are still used — they are discovered by Claude Code as subagents (see Distribution section). The slash command `/agent <name>` becomes whatever Claude Code uses (`/agents` interactive picker). Spec is archived as historical reference. |
| 03 | Slash Commands | **Superseded** | Built-in commands (`/plan`, `/clear`, etc.) ship with Claude Code. User commands in `.claude/commands/` are loaded natively. Autocomplete is built-in. The product gets all of this for free. The `create-command` skill in `Skills~/` continues to work — it writes files in the format Claude Code already reads. Spec is archived. |
| 04 | Interactive Plan Mode | **Revise (medium)** | The UX target stays — agent generates a plan, user reviews step-by-step, executes. The implementation pivots from a custom plan-mode protocol to using Claude Code's existing plan permission mode plus hooks if needed. Needs investigation of Claude Code hook capabilities before re-spec. |
| 05 | Permission System Fix | **Investigate** | The bug being fixed was in our custom permission machinery. Claude Code has its own permission system (default / acceptEdits / plan / bypassPermissions / auto). The replacement question is: does Claude Code's permission system cover the scenarios the original bug fix targeted? Needs investigation before re-spec. |
| 06 | Plans CRUD | **Revise (small)** | Storage location stays (`ProjectSettings/GameDeck/plans/*.md`). Plans tab UI stays. The "re-execute" mechanism shifts from invoking the custom orchestrator to feeding the plan content into a Claude Code session as user input or via a slash command. `/save-plan` still makes sense, just as a Claude Code skill or command we ship in `Skills~/`. |
| 07 | Editor Status Pin | **Unchanged** | Unity Editor side. Independent of the chat engine. Continues exactly as the current spec describes. Feature is in flight at the time of this ADR (Group 2 complete). |
| 08 | Rules Page | **Revise (medium)** | Conceptually, "rules" map naturally to per-project `CLAUDE.md` content and `.claude/rules/*.md` patterns that Claude Code already supports. The Rules tab in the Tauri app becomes a UI for editing these files, rather than maintaining a custom rule registry on our side. |
| 09 | Design Handoff | **Unchanged** | Pure design / asset work. Independent of engine choice. |

## Validations pending before implementation

These are not blocking the ADR. They are blockers for the first feature that depends on a confirmed answer.

1. **License of `@anthropic-ai/claude-agent-sdk` and Anthropic ToS for embedded distribution.** Read both before publishing a release that bundles the SDK in `App~/`. If restricted, evaluate alternatives (the user installs Claude Code separately and Tauri spawns it).
2. **`--add-dir` behavior for `.claude/agents/`.** Test with a real Claude Code install: drop a test agent in a directory, run `claude --add-dir <that-path>`, run `/agents`, see if the test agent shows up. If yes, primary path. If no, fallback path (copy on first launch).
3. **Windows stdin piping bugs.** Reference: dstreefkerk's January 2026 post and Issue #7263. Validate the SDK on Windows with a non-trivial prompt size before committing to it; if bugs persist, switch to direct `claude -p` subprocess invocation.
4. **Custom subagent MCP tool resolution.** Issue #25200. Validate that our 10 agents in `Agents~/`, when surfaced to Claude Code, can actually call MCP Game Deck tools. If they can't, document the workaround (probably: agents reference the tools by full MCP namespace path).
5. **Authentication onboarding flow.** First-run UX: detect missing Claude Code login, surface a clear "log in to Claude" call to action in the Tauri UI before showing chat. Specifies in Feature 01 revision.

## References

- External feedback (April 2026, paraphrased) — the prompt that triggered this rewrite. Original conversation in chat history.
- Claude Code subagents documentation: https://code.claude.com/docs/en/sub-agents
- Claude Code skills documentation: https://code.claude.com/docs/en/skills
- Claude Agent SDK headless mode: https://code.claude.com/docs/en/headless
- `--add-dir` flag behavior: https://code.claude.com/docs/en/skills (skills exception paragraph)
- Issue #25200 — custom agents and MCP tools: https://github.com/anthropics/claude-code/issues/25200
- Issue #34935 — feature request, MCP in subagents: https://github.com/anthropics/claude-code/issues/34935
- Issue #7263 — Windows stdin piping bug: https://github.com/anthropics/claude-code/issues/7263
- dstreefkerk on Windows + Claude Code subprocess: https://dstreefkerk.github.io/2026-01-running-claude-code-from-windows-cli/
