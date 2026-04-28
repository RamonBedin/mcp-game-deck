# v2.0 Architecture

> Cross-cutting decisions for v2.0. Individual features in `v2-features/` reference this doc rather than re-deriving these decisions.

## Status

`agreed` — Ramon + Claude alignment session, Apr 2026.

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
│                              │         │  │ Agent SDK Server     │  │
│                              │         │  │ (TypeScript / Node)  │  │
│                              │         │  │ - orchestrator agent │  │
│                              │         │  │ - subagents          │  │
│                              │         │  │ - permission resolver│  │
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
3. **Agent SDK Server (Node)** — same as today, lives in `Server~/`. Spawned by the Tauri app at startup. Talks to the C# MCP Server via TCP and to the Tauri app via stdio or local socket.

The external app spawns and supervises the Node process. Unity is just the MCP server provider — it doesn't know or care about the app.

**Why not collapse Node into Tauri's Rust process?** The Agent SDK is a Node library (`@anthropic-ai/claude-agent-sdk`). Rust can't host it directly. Cleanest split: Rust does file IO + IPC + windowing, Node does Claude SDK conversation, Unity does Unity stuff.

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
| Tauri App | Node Agent SDK Server | stdio or local socket | Conversation, agent routing, permission |
| Node Agent SDK | C# MCP Server | TCP localhost (existing) | Same path as today |
| Tauri App | Filesystem | Direct (Rust) | Plans, rules, settings |

The C# MCP Server is unchanged — same `TcpListener` and protocol it has today. The big shift is **the chat client moves out of Unity into the Tauri app**.

## What survives Unity lifecycle disruptions

Today's pain: assembly reload nukes the chat. Play mode entry breaks the connection. Unity restart loses everything.

In v2.0:
- The Tauri app is a **separate OS process**. Unity assembly reload, play mode, even Unity crash do not affect it.
- The MCP Server inside Unity DOES restart on reload (same as today). The Tauri app's role is to **detect disconnect, hold context, reconnect cleanly** when the server is back.
- Conversation state lives in the Node Agent SDK process, which is supervised by Tauri — it survives Unity restarts.

The Editor pin reflects connection state visually:
- 🟢 green — connected to running Unity
- 🟡 yellow — Unity busy (compiling, entering play mode)
- 🔴 red — Unity disconnected
- ⚫ gray — Unity not running / no connection

The pin polls TCP on port 8090. Pin state never blocks the app — chat can run, queue actions, replay when Unity reconnects.

## Security and trust boundary

- The Tauri app only talks to **localhost** (Unity port 8090, Node SDK on a chosen port). No external network calls except Claude API (handled by Node SDK).
- Filesystem access from Rust scoped to project paths and `ProjectSettings/GameDeck/`.
- No code from Unity ever runs in the Tauri app process. No code from the Tauri app ever runs in the Unity process.

## Open architectural questions

These are unresolved as of writing. Each will get a doc in `decisions/` when settled.

1. **First-run experience** — when user installs package and clicks "Open Chat" the first time, what happens? Tauri binary needs to extract from `App~/dist/`, Node deps need install. UX of this matters.
2. **Auto-update of the app** — Tauri has built-in updater. Use it, or rely on package version updates? Package update flow is heavier (user has to re-import package), but simpler.
3. **Multiple Unity projects open simultaneously** — does the user run one Tauri app per project, or one app that switches between projects? Affects port allocation and pin design.
4. **Handling Claude API key** — currently in Project Settings. In v2.0 should it move to Tauri app settings (cross-project) or stay per-project?

These don't block starting v2.0 work, but each will surface during implementation.
