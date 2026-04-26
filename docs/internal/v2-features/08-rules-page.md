# Feature 08 — Rules Page

## Status

`draft` — needs scope clarification with Ramon.

## Problem

The agent today has no consistent way to learn project-specific or developer-specific conventions. Examples of needs:

- "In this project, always use TextMeshPro, never the built-in UI Text"
- "Never modify scripts in `Assets/ThirdParty/`"
- "When creating a new GameObject, always parent it under `_Game/Entities/`"
- "Use ProjectName.Module namespacing convention"

Today, users have to repeat these instructions every conversation, or they get violated when context window pressure causes the agent to forget. Worse: if a teammate joins, they have to tell the agent the same conventions again.

## Proposal

Add a **Rules** tab in the external app where the user defines project-scoped behavior constraints. Rules are loaded into the agent's system prompt automatically every conversation.

Rules are stored as markdown at:

```
ProjectSettings/GameDeck/rules/<rule-name>.md
```

Same convention pattern as plans and commands. Per-project, versioned, writable.

## Scope IN

- **Rules tab in external app**:
  - List of rules (name + summary)
  - Click to open — full rule text + when-to-apply hints
  - Edit / save / delete
  - Toggle on/off per rule (rule stays in file, just inactive)
- **Rule format** — markdown with frontmatter:

```markdown
---
name: prefer-textmeshpro
enabled: true
applies-to: ui-creation
created: 2026-04-17
---

# Always use TextMeshPro

When creating UI text, always use TextMeshPro components, never the built-in UI Text component.

This applies to:
- New UI labels
- Buttons that need text
- Any UI element that displays strings

Exceptions: only the legacy editor scenes in `Assets/Legacy/` keep their existing UI Text — don't migrate them.
```

- **Loading**: at conversation start, the agent loads enabled rules and includes them in its system prompt.
- **Tagging** (v2.0 simple version): rules can have an `applies-to` field. The orchestrator can use this to decide which rules to surface to which subagents (e.g. a `script-writer` subagent gets script-related rules, a `scene-builder` subagent gets scene-related rules).

## Scope OUT (deferred to v2.1)

- Conditional rules (only apply if X) — v2.1
- Rule libraries / sharing across projects
- Rule versioning beyond git
- Rule inheritance from parent rules
- Auto-suggestion of rules ("I noticed you keep correcting me — should I save this as a rule?")
- Rule categories beyond `applies-to`

## Dependencies

- **Feature 01 (External app)** — Rules tab UI lives there
- **Feature 02 (Orchestrator agent)** — orchestrator decides which rules apply to which subagents based on `applies-to` field

## Cost estimate

**Small-medium.**

- File IO for rules (Rust side): ~2 days (almost identical to plans IO)
- React Rules tab UI: ~1 week (similar to Plans tab)
- Loader that reads rules into agent system prompt: ~3 days
- `applies-to` filtering logic in orchestrator: ~2 days

Total: ~2 weeks.

A lot of this code is shared with Feature 06 (Plans). Same file IO patterns, same edit/save/delete UI components. Real new work is the orchestrator integration.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Rules eat too much system prompt budget | high | Limit to N enabled rules at a time, or summarize automatically. Show token count in UI. |
| Conflicting rules ("always use X" vs "never use Y" where they overlap) | medium | Surface conflicts in UI, let user resolve. Don't try to auto-resolve. |
| User writes vague rules that confuse the agent | medium | Provide examples / templates in the UI. "Good rule" vs "vague rule" guidance. |
| Rules drift from reality as project evolves | high | Add "last reviewed" hint. Prompt user to review rules after N conversations. |

## Milestone

v2.0 — basic version (list, edit, toggle, load into system prompt).
v2.1 — power version (conditionals, libraries, auto-suggestion).

## Open questions

1. **Hard cap on enabled rules?** Maybe yes — start with 10. If user enables an 11th, prompt them to disable one. Tokens are real cost.
2. **Where in the system prompt do rules go?** At the end (most recent context wins) or beginning (foundational)? Probably toward the end of the system prompt block, but before the conversation. Document and test.
3. **Should rules apply to slash commands too?** Probably yes — slash commands invoke the agent, which should respect rules.
4. **`applies-to` taxonomy** — what are the valid values? Need a curated list. Probably matches subagent names (e.g. `script-writer`, `scene-builder`, `tool-auditor`). Defined in agent definitions.

## Notes

- Don't over-engineer this. v2.0 is "list, edit, load into prompt". That's it.
- Resist the temptation to make rules a query language ("if file matches *.cs and folder is Assets/Scripts/Player/ then..."). That's v2.1+ territory.
- Rules are markdown for the same reason plans are: editable in any text editor, friendly to git diffs, no proprietary format.
