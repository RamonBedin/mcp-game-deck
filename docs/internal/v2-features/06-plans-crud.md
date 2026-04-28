# Feature 06 — Plans CRUD

## Status

`agreed` — storage location confirmed, UX pending.

## Problem

Today, when the agent generates a plan in plan mode, the plan exists only in the chat. If the user wants to:

- Refer back to it later
- Edit a step before executing
- Save it as a template for similar future tasks
- Share it with a teammate

…they have to scroll back, copy-paste, or screenshot. This is friction that pushes users away from plan mode.

## Proposal

Add a **Plans** tab in the external app (Feature 01). The tab shows all plans saved for the current Unity project, lets the user open, edit, and re-execute them.

Plans are stored as markdown files at:

```
ProjectSettings/GameDeck/plans/<plan-name>.md
```

Same convention as `ProjectSettings/GameDeck/commands/<n>/SKILL.md` (used by the `create-command` skill). Confirmed:

- Per-Unity-project (correct semantics — plan for game A ≠ plan for game B)
- Versioned by user's git (team can share plans naturally)
- Writable regardless of how the package is installed (PackageCache is read-only; `ProjectSettings/` is always writable)

## Scope IN

- **`/save-plan` slash command** — saves the current plan from chat to a markdown file, prompts for name
- **Plans tab in external app**:
  - List of plans in the current project (sorted by modified time)
  - Click to open — shows plan content
  - Edit in place (markdown editor — basic, no toolbar; just textarea with monospace + render preview)
  - Save / cancel
  - Delete (with confirmation)
- **"Re-execute" button** on an open plan — sends the plan as a system message to a fresh chat with auto mode, agent walks the steps
- **Markdown format** — simple. Header with `name`, `created`, `last-run`. Body with the plan steps. Example:

```markdown
---
name: setup-2d-roguelike-scene
created: 2026-04-17T14:32:00Z
last-run: 2026-04-18T09:15:00Z
---

## Plan: Setup 2D Roguelike Scene

1. Create empty scene "MainGame"
2. Add 2D camera with orthographic projection, size 5
3. Create "Player" prefab from existing PlayerRun animation
4. Add "GameManager" GameObject with GameManager.cs script
5. ...
```

## Scope OUT (deferred to v2.1)

- Plan templates (parametrized plans with `<placeholder>` syntax)
- Plan versioning beyond git
- Cross-project plan library
- Plan import/export beyond markdown
- Plan execution dry-run mode (preview without running)
- Plan branching / conditionals

## Dependencies

- **Feature 01 (External app)** — Plans tab is in the React UI
- **Feature 03 (Slash commands)** — `/save-plan` is implemented as a slash command
- **Feature 04 (Interactive plan mode)** — plans saved are products of plan mode, so plan mode must work first
- **Feature 02 (Orchestrator agent)** — re-executing a plan invokes the orchestrator with the plan as context

## Cost estimate

**Small-medium.**

- File IO for plans (Rust side, scoped to `ProjectSettings/GameDeck/plans/`): ~2 days
- React Plans tab UI (list, open, edit, save, delete): ~1 week
- `/save-plan` command + naming flow: ~2 days
- Re-execute integration with orchestrator: ~3 days

Total: ~2 weeks.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| User edits plan into something that fails on re-execute | medium | Re-execution is best-effort; agent can ask follow-ups via Feature 04 if plan is incomplete |
| Plan filename collision when user names two plans the same | low | Auto-append `-2`, `-3` etc. Or prompt to overwrite. |
| Plans tab gets cluttered with experimental plans | medium | Add archive concept later (v2.1). For v2.0, encourage delete. |
| Markdown editor in-app is poor (no syntax help) | low | Acceptable for v2.0 — most users will edit in their actual editor (VS Code, etc) since the file is on disk |

## Milestone

v2.0.

## Open questions

1. **What does "re-execute" actually do?** Pass the plan to the orchestrator as the user message? Spawn a subagent with the plan as system prompt? Probably the first — keeps it conversational.
2. **Auto-save plans when user generates one in plan mode?** Or only save when user explicitly clicks/commands? Auto-save would clutter; explicit save is cleaner. Go with explicit.
3. **Frontmatter schema** — how strict? Probably loose: required `name`, optional `created`, `last-run`, `tags`. Anything else passes through.

## Notes

- Keep the editor simple. Sophisticated markdown editing belongs to the user's actual code editor — they can open the file directly in VS Code if they want.
- Plans on disk is a feature, not a bug. Users who want git-tracked plans get them for free.
