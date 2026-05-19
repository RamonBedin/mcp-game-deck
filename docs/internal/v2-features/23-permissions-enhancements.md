# Feature 23 — Permission System Enhancements

## Status

`proposed` — design pending Ramon approval. Picks up F04 (Interactive Approvals) deferrals. Companion specs (`23-permissions-enhancements-spec.md` + `23-permissions-enhancements-tasks.md`) will follow when execution starts.

## Problem

F04 (Interactive Approvals) shipped a permission system: when Claude wants to call a tool, the user sees a card asking `Allow / Always / Deny`. "Always" remembers the choice for the rest of the session. Three deferrals from F04 limit how useful the system is over time:

**(a) No cross-session persistence.** "Always" only lasts the current session. The next time the user opens the app, every tool prompts again until they re-train it. Power users with stable workflows re-grant the same dozen permissions every day.

**(b) No shareable rule library.** A studio with 5 developers each individually trains their own permission set. There's no way to define "the rules for this project" and have new team members inherit them. Each onboarding starts from a blank permission slate.

**(c) No hooks for extension.** Permission decisions are hard-wired in the supervisor's `canUseTool` callback. There's no plugin point where a project-specific rule (e.g., "never let any tool touch `/Assets/ThirdParty/`") can intercept and deny without changing the supervisor source.

## Proposal

Three coordinated enhancements.

**(a) Cross-session persistence.** Add `Library/MCPGameDeck/permissions.json` per-project storage. "Always" choices write to this file. On supervisor boot, the `canUseTool` callback consults the file first. UI surface: a Permissions page in Settings shows the current per-project list with per-row revoke buttons and a "Clear all" affordance.

Storage shape (versioned for future migration):
```json
{
  "version": 1,
  "rules": [
    {"tool": "mcp__game-deck__asset-find", "decision": "allow", "scope": "project"},
    {"tool": "mcp__game-deck__build-player", "decision": "deny", "scope": "project"}
  ]
}
```

**(b) Rule library / project profiles.** Permissions live in `ProjectSettings/GameDeck/permissions/` (versioned in git, shared across team) for project-wide rules, plus `Library/MCPGameDeck/permissions.json` for per-user overrides (local). Resolution order: per-user > per-project > built-in defaults. A `permission_profile` field in the project profile JSON allows naming a baseline ("strict", "permissive", "default") that the user-level rules amend.

UI: Settings → Permissions page has two sections: "Project rules (from git)" and "Your overrides (local)". Editing project rules generates a diff in `ProjectSettings/`, normal commit flow handles sharing.

**(c) Hook system.** Plugin points in the supervisor `canUseTool` callback:
- `beforePermissionCheck(toolName, args, context) → MaybePolicy` — plugin returns a decision (allow / deny / continue) or skips
- `afterPermissionDecision(toolName, args, decision, context)` — logging / audit / observability

Hooks are registered in `Server~/src/hooks/` directory; each hook is a `.ts` file exporting a default function. They run in registration order; first decisive return wins. v2.1 ships with one built-in hook example: `pathBoundaryHook` that denies any tool argument referencing paths in a project-configured forbidden list (e.g., `/Assets/ThirdParty/`).

## Scope IN

- **Per-project permission storage:**
  - `ProjectSettings/GameDeck/permissions/default.json` (versioned, team-shared)
  - `Library/MCPGameDeck/permissions.json` (local, per-user)
  - Resolution: user > project > built-in
  - Migration from any old in-memory-only "Always" rules
- **Settings → Permissions page:**
  - Tabbed: "Project" (synced via git) vs "Local" (user-only)
  - List of rules with tool, decision, source
  - Per-row revoke button
  - "Clear all" with confirmation
  - "Reset to project defaults" (clears local overrides)
- **Hook system:**
  - `Server~/src/hooks/` directory loaded at supervisor boot
  - `beforePermissionCheck` and `afterPermissionDecision` hook types
  - First-decisive-return resolution for `beforePermissionCheck`
  - All hooks run for `afterPermissionDecision` (for logging)
  - Built-in `pathBoundaryHook` as example + practical defense
  - Hot reload of hooks on supervisor restart
- **Profile selection:**
  - `permission_profile: "strict" | "permissive" | "default"` field in project profile
  - Baselines compose with explicit rules

## Scope OUT (deferred to v2.3+)

- **GUI for editing project rules** — v2.1 edits via Settings page; bulk import/export is text-editing the JSON. v2.3 if rule count grows.
- **Per-conversation permission overrides** — a session always inherits project + local rules; can't temporarily relax for a single chat.
- **Permission templates marketplace** — sharing rule libraries beyond project boundaries (e.g., a shared Unity-typical permission profile). F31 (Rule libraries) territory.
- **Risk-tier-based default policies** — auto-allow all `Tier=Read`, auto-deny all `Tier=Destructive` from the catalog. Tempting, but explicit user intent is the better default. v2.3 if users ask.
- **Per-subagent permission scoping** — every subagent inherits the parent permissions. F25 + F26 might revisit.
- **Audit log UI** — `afterPermissionDecision` writes to a log file but doesn't yet have a viewer. v2.3 if needed.

## Dependencies

- **F19 (Destructive sweep)** — recommended. The built-in `pathBoundaryHook` benefits from knowing which tools are destructive (annotations) to apply stricter checks; non-destructive tools can skip the hook for performance.

## Risks

- **Git-tracked permission files leaking secrets** — if a user names a rule "deny prod_api_key tool" or similar with sensitive context, it ends up in git. Mitigation: rules reference tool names only, not secrets; arguments aren't stored (only the decision, not the trigger).
- **Hook performance** — every tool call runs through every hook. For 30+ tool calls per minute, 50ms of hook overhead = noticeable. Mitigation: profile during spec phase; budget hooks to <5ms each.
- **Resolution complexity** — "user vs project vs built-in" priority can be confusing when a tool is allowed in user but denied in project. UI must show the *effective* decision and its source clearly.
- **Hot reload of hooks at supervisor restart, not at file change** — risk of stale hook behavior if user edits a hook file. Acceptable trade-off; live reload of `.ts` files is fragile.

## Open questions

1. **Should "Always allow" decisions auto-promote to project rules after N consecutive sessions?**
   - Recommendation: no. Surprising side effect — user sees something allowed they don't remember training. Manual promotion only via Settings page.
2. **Should rules support glob/regex patterns (e.g., `mcp__game-deck__asset-*`)?**
   - Recommendation: not in v2.1. Single-tool rules only. Pattern support is a v2.3 follow-up when rule count justifies it.
3. **What happens to in-flight "Always" rules from v2.0 sessions on first v2.1 launch?**
   - Recommendation: migration prompt: "Move your X session rules to the project profile?" with Skip/Save options. Default is Skip (in-memory rules expire, user re-trains on next use).

## Related notes

F04 (Interactive Approvals) shipped the core "ask the user" mechanic with session-local memory. F23 makes it useful long-term (persistence, sharing, extension).

Implementation touches `Server~/src/permissions/` (new), Settings UI page, project profile JSON schema, and a few hooks in `canUseTool`. Estimated 5–7 days.
