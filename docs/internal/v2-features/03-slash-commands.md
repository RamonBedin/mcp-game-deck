# Feature 03 — Slash Commands

## Status

`agreed` — pattern partly exists already (`create-command` skill), needs runtime integration.

## Problem

Power users want fast access to specific actions or agents without typing prose every time. Today there's no slash command surface in the chat. The `create-command` skill exists in `Skills~/` and writes commands to `ProjectSettings/GameDeck/commands/<n>/SKILL.md`, but those commands aren't currently invokable from the chat UI.

## Proposal

Implement a slash command runtime in the v2.0 external app:

1. **Built-in commands** (provided by the package):
   - `/agent <n>` — force route to a specific subagent (Feature 02)
   - `/plan` — switch permission mode to plan
   - `/auto` — switch permission mode to auto
   - `/ask` — switch permission mode to ask
   - `/clear` — clear conversation
   - `/save-plan` — save current plan to plans tab (Feature 06)
   - `/help` — show all available commands

2. **User commands** — read from `ProjectSettings/GameDeck/commands/<n>/SKILL.md` (already populated by `create-command` skill). Each becomes invokable as `/<n>`.

3. **Autocomplete** — when user types `/`, dropdown shows matching commands with descriptions.

4. **Argument parsing** — commands declare `argument-hint` in their SKILL.md (already convention). UI shows the hint inline.

## Scope IN

- Command registry in Node Agent SDK Server
- Built-in commands listed above
- Loader that watches `ProjectSettings/GameDeck/commands/` for changes (live reload, no restart)
- Autocomplete dropdown in React UI
- Argument hint rendering
- Command execution dispatched as a system message that the main agent processes

## Scope OUT (deferred)

- Command shortcuts / aliases (no `/p` for `/plan` in v2.0)
- Cross-project command library (commands are per-Unity-project for now)
- UI for editing commands (still file-based; UI editor is v2.1)
- Command history / favorites

## Dependencies

- **Feature 01 (External app)** — autocomplete UI lives there.
- **Feature 02 (Orchestrator agent)** — `/agent <n>` requires orchestrator routing.
- **Feature 05 (Permission system fix)** — `/plan`, `/auto`, `/ask` rely on permission mode actually working at runtime.

## Cost estimate

**Small.**

- Command registry + dispatcher: ~2-3 days
- Loader watching `ProjectSettings/GameDeck/commands/`: ~1 day
- Built-in commands listed above: ~2 days
- Autocomplete UI: ~2 days

Total: ~1.5 weeks.

The `create-command` skill already produces files in the right format, so the writing-side convention is settled. This feature is mostly about reading those files at runtime.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Slash inside prose triggers autocomplete annoyingly | medium | Only trigger when `/` is the first non-whitespace char on a line |
| User-defined command has malformed SKILL.md | medium | Fail soft — log error, skip the command, keep loading others |
| Command name collisions (built-in vs user) | low | Built-in wins, log a warning |
| Live reload breaks during command edit | low | Debounce filesystem watcher, retry on parse error |

## Milestone

v2.0.

## Open questions

1. **Is `/agent <n>` the right invocation, or should agent routing have its own marker (e.g. `@<n>`)?** Claude Code uses `/agents` for menu, no per-message marker. Probably follow that pattern: dropdown to pick, no `@`.
2. **Should commands be runnable by the orchestrator agent itself, or only by the user?** Probably user-only at first — agent-invoked commands could create loops. Revisit if a real use case emerges.
3. **Where do built-in commands live in code?** Likely a single `BuiltinCommands.ts` module in the Agent SDK Server. They're not in `ProjectSettings/` because they ship with the package.

## Notes

- The `create-command` skill is unchanged. It produces the right output already. v2.0 just makes that output runnable.
- This feature is a quick win once Feature 01 and Feature 02 land. Build it third in the v2.0 sequence.
