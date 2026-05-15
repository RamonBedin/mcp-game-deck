# Feature 08 — Rules Page

## Status

`agreed` — design decisions locked May 2026. Companion: `08-rules-page-spec.md` (executable spec) + `08-rules-page-tasks.md` (decomposed work breakdown).

## Problem

The agent today has no consistent way to learn project-specific or developer-specific conventions. Examples:

- "In this project, always use TextMeshPro, never the built-in UI Text"
- "Never modify scripts in `Assets/ThirdParty/`"
- "When creating a new GameObject, always parent it under `_Game/Entities/`"
- "Use `ProjectName.Module` namespacing convention"

Today, users repeat these instructions every conversation, or watch them get violated when context window pressure causes the agent to forget. Worse: a teammate joining the project has to tell the agent the same conventions from scratch.

## Proposal

Add a **Rules** tab in the Tauri app where the user defines project-scoped behavior constraints. Rules are stored as markdown at:

```
ProjectSettings/GameDeck/rules/<rule-name>.md
```

Same convention pattern as plans (F06) and commands (`create-command` skill): per-project, versioned by user's git, writable regardless of how the package is installed.

Enabled rules are compiled by Rust into a single bundle file at `Library/MCPGameDeck/rules-bundle.md` (gitignored — derived artifact). On every `query()`, `sdk-entry.js` reads the file's contents and forwards them to the SDK via `systemPrompt.append` (preset: `"claude_code"`). The SDK handles the Windows ~32KB `CreateProcess` ceiling internally via tempfile + `--append-system-prompt-file` to the CLI, so the host just hands over the string.

## Scope IN

- **Rules tab in the Tauri app:**
  - List of rules with per-row checkbox toggle (`☑` enabled / `☐` disabled)
  - Header summary: `<enabled-count>/10 enabled, ~<total-tokens> tokens`
  - Pane with View/Edit toggle, Toggle / Delete actions, `applies-to` chip strip
  - "+ New rule" inline form with kebab-case name validation
  - Empty state with onboarding hint
  - Toast on cap-reached when trying to enable the 11th rule
- **Frontmatter:** `enabled` (boolean), `description` (optional one-liner), `applies-to` (array of free-form strings; **informational only in v2.0**)
- **Bundle pipeline:** Rust composes `Library/MCPGameDeck/rules-bundle.md` from all enabled rules on every `rules-changed` event (and on startup, and on supervisor restart)
- **Token estimator:** `chars / 4` heuristic shown per rule and summed in the header
- **Cap of 10 enabled rules** — client-side and server-side enforcement
- **Surgical toggle:** `toggle_rule` mutates only the `enabled` frontmatter key, preserves body and unknown keys verbatim
- **Shared refactor:** extract `markdown_doc` Rust module from F06 plans for `parse_frontmatter`, `validate_kebab_name`, `ensure_dir`, `atomic_write`, `read_metadata_ms`, `next_available_suffix`. Plans migrates to ms units in the same task (closes F06 follow-up)

## Scope OUT (deferred to v2.1+)

- **Functional `applies-to` per-subagent filtering** — Claude Code spawns subagents with fixed toolsets at spawn time, no clean hook to inject per-agent system prompts. v2.0 parses + displays + round-trips the field but the bundle compiler ignores it
- **Conditional rules** (only apply if X) — query-language territory, deliberately avoided
- **Rule libraries / cross-project sharing**
- **Rule versioning beyond git**
- **Auto-suggestion** ("I noticed you keep correcting me — save as a rule?")
- **Rule conflict detection**
- **Real tokenizer** (e.g. `tiktoken`) — `chars/4` is enough signal at v2.0 scale
- **`/save-rule` chat skill** — captured normative instructions are too fuzzy to detect reliably; UI covers the primary case; revisit in v2.1 if signal appears
- **Multi-line `applies-to` editing UI** — frontmatter is hand-edited in v2.0

## Dependencies

- **F01 (External App)** — done. Rules tab lives inside the Tauri React app
- **F02 (Claude Code Supervisor)** — done. Bundle injection rides on the existing `sdk-entry.js` query loop
- **F06 (Plans CRUD)** — done. `markdown_doc` extraction reuses F06's plans helpers; Rust+React+watcher pattern proven by F06
- **F07 (Editor Status Pin)** — done. F07 sets `UNITY_PROJECT_PATH` env var when launching Tauri; F08 consumes for both rules dir and bundle path

This feature does not depend on F09 (Design Handoff). F09 may visually polish the Rules tab once it lands.

## Locked decisions (May 2026)

These were the nine open questions raised before spec work; resolutions captured here so the spec/tasks can stand on settled ground.

### #1 — Injection mechanism: `systemPrompt.append` (resolved during 3.3)

Originally locked as `appendSystemPromptFile` based on a forward-looking TODO in `sdk_entry.js`. Empirical smoke during task 3.3 (a 🦖-prefix rule) revealed the option was silently ignored: the bundle composed correctly and stderr logged the path, but Claude responded without the prefix. The Claude Agent SDK TypeScript reference + the "Modifying System Prompts" doc resolved the real shape: `systemPrompt?: string | { type: "preset"; preset: "claude_code"; append?: string }` — `append` takes a string, not a path. The host reads the bundle file's contents inline and forwards them through `append`; the SDK handles the Windows ~32KB `CreateProcess` ceiling internally via tempfile + `--append-system-prompt-file` to the CLI. Lesson: verify SDK options against type definitions, not against forward-looking code comments.

### #2 — `applies-to` is informational only in v2.0

Claude Code spawns subagents with fixed toolsets at spawn time; there's no hook to inject per-agent system prompts dynamically. The field stays in the frontmatter (parsed, displayed, round-tripped) so v2.1 can flip the switch without a schema migration, but v2.0's bundle compiler ignores it. UI surfaces a "v2.0 informational only" caption to set expectations.

### #3 — No `/save-rule` skill in v2.0

F06's `/save-plan` had a clean marker (`ExitPlanMode`); rules have no equivalent — the heuristic "what's the recent normative instruction?" is too fuzzy. The Rules tab's "+ New rule" button covers the primary case (rules are usually defined ex ante, not captured ad hoc). Revisit in v2.1 if demand signal appears.

### #4 — Cap of 10 enabled rules

When ten feels too few, that's the signal rules are doing too much work and some should consolidate into project `CLAUDE.md` or clearer agent instructions. UI blocks the 11th with a toast.

### #5 — Bundle position: end of system prompt

`systemPrompt.append` (with preset `"claude_code"`) naturally appends after the SDK's own system prompt — recent context wins, which is what we want for "rules the user explicitly set."

### #6 — Frontmatter shape

```yaml
---
enabled: true
description: "Always use TextMeshPro for new UI text."
applies-to: [ui]
---
```

Filename is source of truth (no `name` key); `created` cuts (mtime covers); `applies-to` is an array of free-form strings.

### #7 — Bundle refresh: watcher-driven, read fresh by SDK each turn

Rust watcher recomposes `Library/MCPGameDeck/rules-bundle.md` on every `rules-changed` event. SDK reads the file on every `query()`. No stdin protocol changes, no in-memory state to sync.

### #8 — No conflict detection in v2.0

Two contradictory rules ("always X" / "never X") are the user's problem. Token visibility helps users notice when their rule set is getting expensive; conflict detection is v2.1 if reports surface.

### #9 — Shared `markdown_doc` module factored out of F06

Third occurrence of the Rust+watcher+frontmatter pattern is the right moment to extract. Single task does the extraction + flips `PlanMeta.last_modified` from seconds to milliseconds (closing F06's follow-up).

## Cost estimate

**Medium.** Smaller than F02 / F06 because there's no autocomplete dropdown, no skills, no SDK protocol additions — just CRUD, a watcher, a bundle compiler, and a tab UI.

- Group 1 (shared refactor): ~1 day
- Group 2 (Rust CRUD): ~1.5 days
- Group 3 (watcher + bundle + spawn wire): ~1.5 days
- Group 4 (React Rules tab): ~2.5 days
- Group 5 (smoke): ~0.5 day

Total: ~7 days focused work, 15 tasks across 5 groups.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| SDK option semantics differ from initial assumption (`appendSystemPromptFile` turned out non-existent; canonical option is `systemPrompt.append`) | realized | Smoke scenarios 5/6 caught it via 🦖-prefix rule; fix landed in task 3.3 (read bundle content + forward via `systemPrompt.append`); resolution captured in Decision #1 and tasks.md follow-ups |
| Rules eat too much of the model's context budget | medium | Cap of 10; per-rule token estimate; >500 tokens shows warning glyph |
| Conflicting rules confuse the model | medium | Surface tokens; no auto-resolution. v2.1 may add conflict detection |
| Vague rules degrade model output | medium | Editor includes hint copy in the template; live token count discourages bloat |
| Rules drift from project reality | high | `last_modified` is visible per row; v2.1 may add "stale rule" prompts |
| Refactoring F06 plans breaks the plans tab | medium | F06 test suite moves with the helpers; CI runs both old plans paths and new rules paths; smoke scenario 16 is the regression gate |
| User edits frontmatter externally with non-mapping YAML | low | `toggle_rule` returns clear error; UI surfaces it; user fixes the file |

## Milestone

v2.0.

## Notes

- Bundle file `Library/MCPGameDeck/rules-bundle.md` is the trust boundary between Rust (writes) and JS (reads). If anything weird happens to it, the answer is "what did the watcher do? did `recompose` run?" — never "let's have JS rewrite it"
- The 10-rule cap is intentional friction. v2.0's design philosophy: discoverability + intentionality > unbounded capacity
- `applies-to`'s parsed-but-ignored status is deliberate. Shipping the schema today means v2.1 only needs compiler changes, no file migrations
- Don't over-engineer the markdown editor. Users with strong feelings will edit rule files directly in VS Code — the tab is for quick toggles, light edits, and discovery
- The shared `markdown_doc` module is the natural payoff of writing the third feature in this pattern. F08 plans the lift; F09+ benefit if any future feature reuses

## References

- `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md` — engine decision; F08's `systemPrompt.append` injection follows from the ADR
- `docs/internal/v2-architecture.md` — process layout, storage location convention
- `docs/internal/v2-features/06-plans-crud.md` — established the Rust+React+watcher pattern that F08 extends
- `docs/internal/v2-features/08-rules-page-spec.md` — companion executable spec
- `docs/internal/v2-features/08-rules-page-tasks.md` — companion task breakdown
- Claude Agent SDK "Modifying System Prompts": https://platform.claude.com/docs/en/agent-sdk/modifying-system-prompts — Options interface defines `systemPrompt?: string | { type: "preset"; preset: "claude_code"; append?: string }`. Verified empirically during F08 task 3.3.
