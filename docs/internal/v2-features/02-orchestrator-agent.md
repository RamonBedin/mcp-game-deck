# Feature 02 — Orchestrator Agent (Single Chat, Multiple Subagents)

## Status

`agreed` — pattern decided, design pending.

## Problem

Today, switching agents requires creating a new chat. Conversation context is lost. The user has to manually decide which agent to use for each task, and re-establish context every time.

This is friction. Most real workflows mix tasks: "look at this scene, refactor that prefab, then write a script". Different agents are best for different parts, but starting a new chat per agent destroys flow.

Reference: Claude Code uses an orchestrator pattern — one main agent receives the user's message, delegates to specialized subagents via a `Task` tool, integrates results, replies in the same chat. The user sees a single conversation; routing happens transparently.

## Proposal

Build the same pattern in MCP Game Deck:

1. **Main agent** (default, always present) receives every user message.
2. Main agent has a `delegate` tool that spawns a subagent for a focused task.
3. Subagents are defined in `Agents~/` (already exists) — one Markdown file per subagent with `name`, `description`, allowed tools, system prompt.
4. Main agent reads agent descriptions and decides who to delegate to. User can also force routing with `/agent <name>` if they want.
5. Subagent runs to completion (with its own tool budget), returns result to main agent.
6. Main agent presents the result in the chat as if it had done the work itself.

The user sees one chat. Switching is invisible unless they look. This is what `/agents` autocomplete does in Claude Code.

## Scope IN

- Orchestrator main agent system prompt (knows how to route)
- `delegate` tool exposed to main agent (spawns a subagent with a task)
- Subagent definition format reuses existing `Agents~/` convention
- `/agent <name>` slash command for explicit routing
- Visual indicator in chat UI: "delegating to <agent>..." when subagent is running
- Conversation history shows the delegation tree (collapsed by default, expandable)
- Tool budget per subagent — if a subagent goes over budget, it returns control to main

## Scope OUT (deferred)

- Subagent-to-subagent delegation (only main can delegate, for now)
- Parallel subagent execution (sequential only in v2.0)
- Subagent memory / persistence across sessions
- User-defined subagents in UI (still file-based for v2.0; UI editor in v2.1)

## Dependencies

- **Feature 01 (External app)** — orchestrator runs in the Node Agent SDK, but the UX (showing delegation tree, "delegating..." indicator) is in the React frontend. Both halves needed.
- **Feature 03 (Slash commands)** — `/agent <name>` is implemented as a slash command.
- **Feature 05 (Permission system fix)** — subagents must inherit or override permissions cleanly. Current bug makes this nondeterministic.

## Cost estimate

**Medium.**

- Main agent system prompt design — careful work but not large code (~3-5 days)
- `delegate` tool implementation in Agent SDK Server (~1 week)
- Subagent loader (read `Agents~/`, expose to main as routable options) (~3 days)
- React UI for delegation tree + "delegating..." indicator (~1 week)
- Tool budget enforcement (~3 days)

Total: ~3 weeks of focused work.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Main agent picks wrong subagent | high | Good agent descriptions are critical. Reuse the writing standards from `.claude/agents/`. |
| Subagent goes over budget and main loses context | medium | Hard budget cap. Subagent always returns SOMETHING (even partial result). |
| Delegation tree gets confusing in long sessions | medium | Collapse by default. Show only the "owning" agent's reply unless user expands. |
| User forces `/agent X` for tasks main would have routed correctly | low | Trust the user. If they explicitly route, respect it. |

## Milestone

v2.0.

## Open questions

1. **Where does main agent live structurally?** Same `Agents~/` folder or a special slot? Probably special — there's exactly one main, it has a different role.
2. **Subagent permissions** — does a subagent inherit main's permission mode (auto/plan/ask), or have its own? Probably inherit, with explicit override option per subagent.
3. **What happens if user changes permission mode mid-conversation?** New subagents pick up the change. In-flight subagents finish with their original mode. Document this clearly.
4. **Agent description format** — reuse the format we already have in `.claude/agents/*.md`? Probably yes — same convention everywhere.

## Notes

- This is the feature that makes v2.0 "feel like" a different product. The single-chat illusion is the killer UX win.
- Pair with Feature 03 (slash commands) — power users want to override routing sometimes.
