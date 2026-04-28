> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.
> **Status post-ADR:** `mostly superseded — small surface remaining.` The bug being fixed lived in the custom server's permission resolver. Under ADR-001 the custom server is removed; the bug goes with it. Claude Code's permission system (`default` / `acceptEdits` / `plan` / `bypassPermissions` / `auto`) covers all five test cases listed in the spec. What remains is much smaller: surface Claude Code's mode in the Tauri React UI and validate end-to-end that the five behaviors work. Original ~2-2.5 weeks estimate drops to ~2-3 days. **Read the ADR before executing this feature.**

# Feature 05 — Permission System Fix

## Status

`agreed` — bug + clean refactor, scope clear.

## Problem

The permission system has three modes (`plan`, `auto`, `ask`) but they're not respected consistently:

1. In **plan mode**, the agent ignores `auto` and asks for confirmation on every action — even though plan mode shouldn't be executing anything that needs permission, since it's planning.
2. When the user runs a slash command, the system seems to fall back to "ask" regardless of the current mode setting.
3. Switching modes mid-conversation doesn't always take effect — sometimes the next agent action still uses the old mode.

Symptom: user sets `auto`, mode shows `auto` in UI, but the next action prompts for confirmation. Frustrating, breaks flow.

## Proposal

Refactor the permission resolver to be a single source of truth:

1. **One PermissionState object** held by the Agent SDK Server, mutable, observable.
2. **Every code path that needs permission resolution reads from this single object.** No code path bypasses it.
3. **Mode changes propagate immediately.** When user sets `auto`, the next tool call check sees `auto`. No caching, no stale state.
4. **Tools declare their permission requirements explicitly.** The check is: "given this tool, this current state, this current mode — should we proceed, ask, or block?"
5. **Plan mode never asks for permission to execute** — it's not executing. Plan mode only triggers `ask_user` (Feature 04) when the agent needs information, which is a different concept.

## Scope IN

- Audit current permission code — find every code path that decides "ask user or proceed". Document them.
- Refactor to single resolver class.
- Permission state propagates via event/observer to UI (UI shows current mode, no stale display).
- Mode change applies to in-flight actions where safe (queued tool calls re-check before executing).
- Plan mode does NOT trigger permission prompts (only `ask_user` if agent needs info).
- Slash commands respect the current mode — `/plan` switches mode, doesn't reset other state.
- Test cases:
  - Set `auto` → tool call → no prompt
  - Set `ask` → tool call → prompt
  - Set `plan` → agent generates plan → no permission prompts during planning
  - Switch `ask` → `auto` mid-message → next action no prompt
  - `/auto` slash command → mode changes → next action no prompt

## Scope OUT (deferred)

- Per-tool permission overrides (e.g. "always allow this specific tool even in ask mode") — deferred to v2.1
- Permission scopes / categories — v2.1
- Allowlist / denylist persistence across sessions — v2.1

## Dependencies

- **Feature 01 (External app)** — new UI surface where the mode switcher lives
- **Feature 02 (Orchestrator agent)** — subagents need to inherit/override permission cleanly (cross-references this feature)

## Cost estimate

**Medium-small.**

- Audit + documentation of current code paths: ~3 days
- Refactor to single resolver: ~1 week
- UI integration (mode switcher live, no stale state): ~3 days
- Test suite covering the cases above: ~3 days

Total: ~2-2.5 weeks.

The actual code change is contained, but the existing code is tangled (multiple places make permission decisions independently), so the audit phase matters.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Refactor breaks existing flows that secretly depend on the buggy behavior | medium | Comprehensive test cases written FIRST, run against both old and new implementation |
| Race condition during mid-message mode change | low | Mode read happens at tool-call site, not at message-receive site. No queuing means no race. |
| User confused by "what does plan mode actually do then" | medium | Update `plan` mode UX to clearly indicate it's planning, not executing. Action buttons "Execute Plan" appear after plan is done. |
| Subagent inheritance interaction with Feature 02 | medium | Document inheritance rule explicitly. Test with subagent flows. |

## Milestone

v2.0. (Originally considered for v1.2 since it's a bug, but Ramon confirmed v2.0 — agility on releases is more valuable than fixing one bug now that's a few weeks away from disappearing along with the chat window.)

## Open questions

1. **Should `/plan` reset conversation context or just switch mode?** Probably just switch mode — clearing context is `/clear`.
2. **What happens to in-flight tool calls when mode changes from `auto` to `ask`?** Probably finish in-flight, apply `ask` to subsequent. Document this.
3. **Visual indicator when mode changes?** Toast notification? Status bar update? Chat message? Probably status bar (always visible) + brief toast on change.

## Notes

- Resist the temptation to add features in this refactor. Goal is parity with current modes, but reliable. Per-tool overrides, scopes, etc go to v2.1.
- This bug is high-frequency (hits every session) but low-severity per occurrence (annoying, not destructive). Prioritize to ship with v2.0 release, not before — see milestone note.
