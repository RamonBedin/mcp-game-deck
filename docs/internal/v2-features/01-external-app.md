> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.
> **Status post-ADR:** `delivered — engine target updated.` The Tauri scaffold (React + Vite + Tailwind + Zustand + Router + Rust supervisor + TCP client + JSON-RPC framing + chat round-trip) shipped April 2026 (MSI 2.93 MB). The Node child process target has shifted under ADR-001: Tauri now spawns Claude Code via `@anthropic-ai/claude-agent-sdk` instead of a custom Agent SDK Server (`Server~/dist/index.js`). The supervisor pattern, the TCP path to Unity, and the React shell stay; only the Node-side payload changes. Mentions of "Node Agent SDK Server" below should be read as "Claude Agent SDK + Claude Code subprocess" — the third process is still there, the brand changed.

# Feature 01 — External App (Tauri)

## Status

`agreed` — architecture decided, no code yet.

## Problem

The chat UI lives inside Unity Editor as a UI Toolkit panel. Unity's lifecycle constantly disrupts it:

- Assembly reload (every C# file save) destroys panel state. Conversation history, agent selection, in-flight tool calls — gone.
- Entering Play Mode reloads scripts. Same effect.
- Switching scenes can trigger reloads.
- Unity Editor restart loses everything.

Workarounds attempted: `LockReloadAssemblies()` during AI generation, `EditorPrefs` persistence, etc. They patch symptoms but not the cause. The cause is structural: a chat window doesn't belong inside a process that constantly reloads itself.

Reference product solving this correctly: the Benzi.ai Unity assistant (separate desktop app, Unity has only a small status pin).

## Proposal

Move the chat UI to a dedicated desktop app built with Tauri (Rust + React). The app runs as a separate OS process. Unity becomes a tool provider — the C# MCP Server stays where it is, the chat client moves out.

The app lives inside the Unity package at `App~/`. Users install one package and get both halves.

## Scope IN

- Tauri scaffolding in `App~/`
  - `App~/src-tauri/` — Rust backend (process supervision, file IO, IPC)
  - `App~/src/` — React frontend (chat, plans tab, rules tab, settings)
  - Build pipeline producing per-platform binaries to `App~/dist/`
- IPC between Tauri app and:
  - Unity C# MCP Server (existing TCP on port 8090)
  - Node Agent SDK Server (spawned and supervised by Tauri)
- Connection state management (detect Unity disconnect, hold context, reconnect)
- App launches from "Open Chat" button on the Editor pin (Feature 07)
- Conversation persists across Unity restarts (state in Node SDK process, supervised by Tauri)

## Scope OUT (deferred)

- Auto-update of the app binary (use package update flow for now — open architectural question in `v2-architecture.md`)
- Theming beyond defaults
- Multi-Unity-project handling (one Tauri app per Unity project at first; cross-project switching deferred)
- Mobile / web versions

## Dependencies

- **Feature 07 (Editor pin)** — provides the "Open Chat" entry point. Pin can ship before app, but app needs pin to be useful from inside Unity.
- ~~**Feature 02 (Orchestrator agent)** — the new chat UX is built on top of single-chat-with-subagents. App without orchestrator would be a worse version of today's chat. Build them together.~~ **Superseded by ADR-001.** Feature 02 was canceled; orchestration is owned by Claude Code natively. The 10 agents in `Agents~/` are surfaced to Claude Code via `--add-dir`.

## Cost estimate

**Large.**

Real work involved:
- Tauri scaffolding and Rust process supervision (~1 week of focus)
- React UI rewrite of the chat (current UI Toolkit code can't be reused) (~2 weeks)
- IPC protocol design and implementation between Tauri/Rust ↔ Node SDK ↔ Unity C# (~1-2 weeks)
- Build pipeline producing reproducible binaries on three platforms (~1 week, often more)
- First-run experience (binary extraction, dependency check) (~1 week)
- Edge cases (Unity offline at app start, app crash recovery, port conflicts) (~1 week)

That's a 6-9 week feature for a focused solo dev who knows the stack. Adjust upward for any unfamiliarity.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Rust learning curve eats more time than expected | medium | Keep Rust code minimal — boilerplate Tauri commands, no async complexity if avoidable |
| WebView CSS inconsistencies across platforms | medium | Test on all three early. Stick to flexbox + CSS Grid. Avoid bleeding-edge CSS. |
| Build pipeline fragility (codesigning, notarization on macOS) | high | Ship Windows-first. macOS notarization is its own project. Linux is easy. |
| User distrust of "another desktop app to install" | medium | Bundle inside package. No separate installer. |
| Node SDK supervision bugs (zombies, orphans) | medium | Use Tauri's process management primitives. Test process tree on all three platforms. |
| Performance regression vs current in-Unity panel | low | WebView is fast enough for chat. Render concerns are nonexistent at chat throughput. |

## Milestone

v2.0.

## Open questions

1. **First-run UX** — when user clicks "Open Chat" for the first time after installing the package, the binary needs to extract from `App~/dist/` to a writable location. Where? `%APPDATA%/MCPGameDeck/`? Per-Unity-project?
2. **Port conflicts** — if user runs two Unity projects with mcp-game-deck installed in both, both try to bind port 8090. How does the app know which Unity it's talking to? Likely answer: Unity assigns dynamic port, Editor pin reports it, app reads it.
3. **Crash recovery** — if Tauri app crashes mid-conversation, Node SDK process is orphaned. Do we kill it on next start, or try to reattach? Reattach is harder but better UX.
4. **Codesigning costs** — macOS notarization requires Apple Developer account ($99/yr). Worth it for v2.0, or ship unsigned with "right-click → Open" workaround initially?

## Notes

- Reuse of existing C# MCP Server is total. Server changes for v2.0: zero or near zero.
- Reuse of existing TypeScript Agent SDK is high — main change is removing UI Toolkit specifics, exposing same logic over IPC instead of direct calls.
- 268 tools unchanged. v2.0 is about the client, not the tool catalog. (Tool consolidation is the v1.2 thread.)
