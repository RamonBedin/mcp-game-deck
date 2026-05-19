# v2.0 Architecture

> Cross-cutting decisions for v2.0. Individual features in `v2-features/` reference this doc rather than re-deriving these decisions.

> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`. The third process below was originally a custom Node Agent SDK Server. Under ADR-001 it is replaced by `@anthropic-ai/claude-agent-sdk` supervising a `claude` (Claude Code) subprocess. The three-process layout, the TCP path to Unity, and the Tauri shell are unchanged; the brain inside the third process changed brand. Sections below are updated where the change is concrete; references to "Node Agent SDK Server" and "orchestrator agent" are kept where they are still informative as historical context.

## Status

`agreed` — Ramon + Claude alignment session, Apr 2026. Engine target updated by ADR-001 (Apr 28, 2026).

## Big picture

```
┌──────────────────────────────┐         ┌────────────────────────────┐
│        UNITY EDITOR          │         │   EXTERNAL APP (Tauri)     │
│                              │         │                            │
│  ┌────────────────────────┐  │         │  ┌──────────────────────┐  │
│  │  Editor Pin (status)   │◄─┼────────►│  │   React UI           │  │
│  │  + "Open Chat" button  │  │  IPC    │  │   - chat             │  │
│  └────────────────────────┘  │         │  │   - plans tab        │  │
│                              │         │  │   - rules tab        │  │
│  ┌────────────────────────┐  │         │  │   - settings         │  │
│  │  C# MCP Server         │◄─┼────────►│  └──────────────────────┘  │
│  │  (TcpListener)         │  │  TCP    │  ┌──────────────────────┐  │
│  └────────────────────────┘  │         │  │   Rust backend       │  │
│                              │         │  │   - process mgmt     │  │
│  ┌────────────────────────┐  │         │  │   - file IO          │  │
│  │  268 MCP Tools         │  │         │  │   - bridge           │  │
│  └────────────────────────┘  │         │  └──────────────────────┘  │
│                              │         │             │              │
│                              │         │             ▼              │
│                              │         │  ┌──────────────────────┐  │
│                              │         │  │ Claude Agent SDK     │  │
│                              │         │  │ (Node child process) │  │
│                              │         │  │   spawns:            │  │
│                              │         │  │   ┌────────────────┐ │  │
│                              │         │  │   │ claude (CLI)   │ │  │
│                              │         │  │   │ subprocess     │ │  │
│                              │         │  │   │ - agent loop   │ │  │
│                              │         │  │   │ - subagents    │ │  │
│                              │         │  │   │ - permissions  │ │  │
│                              │         │  │   │ - .claude/     │ │  │
│                              │         │  │   │ - CLAUDE.md    │ │  │
│                              │         │  │   │ - multi-MCP    │ │  │
│                              │         │  │   └────────────────┘ │  │
│                              │         │  └──────────────────────┘  │
└──────────────────────────────┘         └────────────────────────────┘

      ProjectSettings/GameDeck/plans/*.md      ◄── plans live here
                                                   (per Unity project,
                                                    versioned by user)
```

## Process layout

Three OS processes:

1. **Unity Editor** — runs C# MCP Server on TCP. Hosts the Editor pin and "Open Chat" entry point.
2. **External App (Tauri)** — Rust backend + WebView UI. User-facing surface for chat, plans, rules, settings.
3. **Claude Agent SDK + Claude Code subprocess (Node)** — spawned by the Tauri app at startup via `@anthropic-ai/claude-agent-sdk`. The SDK in turn spawns a `claude` subprocess that owns the agent loop, orchestration, slash commands, skills, permissions, memory, and multi-MCP composition. The SDK talks to Tauri over stdio (Agent SDK protocol) and to the C# MCP Server via the existing `mcp-proxy.js` bridge.

The external app spawns and supervises the Node child. Unity is just the MCP server provider — it doesn't know or care about the app.

**Why a child Node process and not Rust-hosted?** The Claude Agent SDK is a Node library (`@anthropic-ai/claude-agent-sdk`). Rust can't host it directly. Cleanest split: Rust does file IO + IPC + windowing, Node hosts the SDK and supervises the `claude` CLI, Unity does Unity stuff.

**Historical note:** the original v2.0 plan had a custom Agent SDK Server (`Server~/src/index.ts`) here — a WebSocket server that hosted its own `query()` loop, agent loader, session storage, and permission flow. Under ADR-001 that custom server is removed; its responsibilities are absorbed by Claude Code itself, which already implements all of them.

## Why Tauri (not Electron)

Decision recorded in `decisions/001-tauri-over-electron.md`.

Short version:
- **Bundle size** — Tauri ~10MB vs Electron ~150MB. Tauri can ship inside the Unity package (`App~/dist/`) without ballooning install size. Electron can't.
- **Performance** — native WebView, lower memory baseline.
- **Cost** — Rust learning curve real but bounded. Tauri commands are simple boilerplate.

## Package layout

```
mcp-game-deck/
├── package.json                ← Unity package manifest
├── CLAUDE.md
├── docs/internal/              ← these design docs
├── .claude/                    ← agent definitions
├── Editor/                     ← C# Unity Editor code
│   ├── Pin/                    ← NEW: small status pin (replaces ChatWindow)
│   └── Tools/                  ← 268 tools, unchanged in scope
├── Server~/                    ← TypeScript MCP Proxy + Agent SDK Server (existing)
├── App~/                       ← NEW: Tauri app source + bundled binaries
│   ├── src-tauri/              ← Rust backend
│   ├── src/                    ← React frontend
│   ├── package.json
│   └── dist/                   ← built binaries per platform (gitignored or via LFS)
├── Agents~/                    ← agent definitions for runtime (existing)
├── KnowledgeBase~/             ← existing
└── Skills~/                    ← existing
```

`App~/` follows the `~`-suffix convention so Unity ignores it. Same as `Server~/`.

## Plans storage

`ProjectSettings/GameDeck/plans/<plan-name>.md`

Reasoning:
- `ProjectSettings/` is per-Unity-project (plans for game A ≠ plans for game B — correct semantics)
- Versioned by user's git (team can share plans naturally)
- Writable regardless of how the package is installed (PackageCache is read-only; `ProjectSettings/` is always writable)
- Same convention as `create-command` skill, which already writes to `ProjectSettings/GameDeck/commands/<name>/`

## Communication channels

| From | To | Channel | Purpose |
|------|----|---------|---------| 
| Unity Editor (Pin) | Tauri App | Process spawn / OS-level deeplink | "Open Chat" launches or focuses the app |
| Tauri App | C# MCP Server (Unity) | TCP localhost (existing port 8090) | Tool calls, status |
| Tauri App | Claude Agent SDK (Node child) | stdio (Agent SDK protocol) | Conversation, prompt streaming, permission UI surface |
| Claude Agent SDK / `claude` subprocess | C# MCP Server | stdio MCP via `mcp-proxy.js` (which proxies to TCP localhost:8090) | Tool calls from agent to Unity |
| Tauri App | Filesystem | Direct (Rust) | Plans, rules, settings |

The C# MCP Server is unchanged — same `TcpListener` and protocol it has today. The big shift is **the chat client moves out of Unity into the Tauri app**, and the brain of the chat client is Claude Code itself (per ADR-001) rather than a custom server.

## What survives Unity lifecycle disruptions

Today's pain: assembly reload nukes the chat. Play mode entry breaks the connection. Unity restart loses everything.

In v2.0:
- The Tauri app is a **separate OS process**. Unity assembly reload, play mode, even Unity crash do not affect it.
- The MCP Server inside Unity DOES restart on reload (same as today). The Tauri app's role is to **detect disconnect, hold context, reconnect cleanly** when the server is back.
- Conversation state lives inside Claude Code (sessions, memory). Tauri supervises the SDK + `claude` subprocess; the conversation survives Unity restarts because Claude Code is a separate process tree from Unity.

The Editor pin reflects connection state visually:
- 🟢 green — connected to running Unity
- 🟡 yellow — Unity busy (compiling, entering play mode)
- 🔴 red — Unity disconnected
- ⚫ gray — Unity not running / no connection

The pin polls TCP on port 8090. Pin state never blocks the app — chat can run, queue actions, replay when Unity reconnects.

## Security and trust boundary

- The Tauri app only talks to **localhost** (Unity port 8090, Claude Agent SDK over stdio in-process). No external network calls except those Claude Code itself makes (Anthropic API, web search if enabled).
- Filesystem access from Rust scoped to project paths and `ProjectSettings/GameDeck/`.
- No code from Unity ever runs in the Tauri app process. No code from the Tauri app ever runs in the Unity process.
- **Claude Code authentication is external to MCP Game Deck.** The user's Claude credentials are owned by Claude Code's own credential storage (`~/.claude/` or equivalent). The Tauri app never sees, stores, or proxies the API key.

## Open architectural questions

These are partially resolved by ADR-001. Status of each below.

1. **First-run experience** — *partially resolved.* Under ADR-001, the user must have Claude Code installed and authenticated. Tauri's first-run flow now includes detecting whether `claude` is on PATH and whether the user is logged in, then surfacing a clear "install / log in to Claude" call to action. Binary extraction of the Tauri app itself (resolved by Feature 07) is unchanged.
2. **Auto-update of the app** — *partially resolved.* Claude Code has its own update mechanism. The Tauri shell still updates via Unity Package Manager (package version bump). Two layers, two cadences — documented in release notes.
3. **Multiple Unity projects open simultaneously** — *resolved.* Feature 07's project-scoped Tauri instance ID (decision #3) handles this; each Unity project gets its own Tauri process, each with its own Claude Code subprocess.
4. **Handling Claude API key** — *resolved.* Under ADR-001, the API key is owned by Claude Code's own credential storage. The Tauri app and the package settings no longer store or proxy it. The legacy `_defaultModel` field in `GameDeckSettings.cs` becomes informational only.

These don't block starting v2.0 work, but each will surface during implementation.
