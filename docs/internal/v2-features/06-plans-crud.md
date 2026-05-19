# Feature 06 — Plans CRUD

## Status

`agreed` — all design decisions locked. Companion: `06-plans-crud-spec.md` (executable spec) + `06-plans-crud-tasks.md` (decomposed work breakdown).

## Problem

Two distinct gaps surface after F02/F04/F07 shipped:

**1. Plans generated in chat are ephemeral.** Today, when Claude exits plan mode (`ExitPlanMode` tool fires, user approves), the plan exists only inside the conversation transcript. If the user wants to refer back to it later, edit a step before re-running, save it as a template for similar future tasks, or share it with a teammate, they have to scroll back, copy-paste, or screenshot. This friction pushes users away from plan mode.

**2. Slash commands and agents are undiscoverable.** The Tauri chat input has no autocomplete dropdown when the user types `/` or `@`. The Claude Code CLI surfaces both natively; the Tauri shell does not. The result: 22 skills already shipped via `Plugin~/skills/` (and the new ones this feature adds — `save-plan`, `plan-execute`) are invisible unless the user already knows the exact name. Same problem for the 10 specialists in `Plugin~/agents/` and any user-installed plugins, commands, or `.claude/agents/` files in their Unity project.

These two gaps stay coupled in this feature: shipping `/save-plan` and `/plan-execute` without a dropdown that surfaces them is half a feature. Discoverability earns its keep across F06 and every skill landed before/after.

## Proposal

Two pieces ship together:

**Plans tab** — new tab in the Tauri React app showing all plans saved for the current Unity project. Lists plans, opens for view/edit, deletes, and triggers re-execute. Plans are stored as markdown at:

```
ProjectSettings/GameDeck/plans/<plan-name>.md
```

Same convention pattern as `ProjectSettings/GameDeck/commands/<n>/SKILL.md` (used by the `create-command` skill) and `ProjectSettings/GameDeck/rules/<rule-name>.md` (Feature 08). Per-Unity-project, versioned by user's git, writable regardless of how the package is installed (`PackageCache` is read-only; `ProjectSettings/` is always writable).

**Slash + `@` autocomplete dropdown** in the chat input. Triggered by `/` for slash commands (built-ins, plugin commands, user `.claude/commands/`, user `.claude/skills/`, and any third-party plugin commands) and by `@` for a unified picker mixing agents (from `Plugin~/agents/`, user `.claude/agents/`, third-party plugin agents) and project files (rooted at `UNITY_PROJECT_PATH`, with sensible exclusions). All catalog data comes from the SDK's `system/init` event for commands+agents; file index is a small Rust scan on supervisor startup, refreshed via the existing file watcher.

The two skills that the Plans tab interoperates with — `/save-plan` and `/plan-execute` — ship as plugin skills under `Plugin~/skills/`. They use Claude Code's own Read/Write tools to manipulate plan files; the Plans tab uses Rust commands to do the same CRUD without going through the agent. Both paths land at the same disk location; a Rust file watcher emits `plans-changed` so the React tab refreshes whenever the directory changes (skill writes, user edits in VS Code, external tool, anything).

## Scope IN

**Plans CRUD (Rust + React)**

- Rust commands: `list_plans`, `read_plan`, `write_plan`, `delete_plan` — real implementations replacing the F01-era stubs
- Rust file watcher on `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/`, debounced, emits `plans-changed` Tauri event
- Frontmatter parsing (YAML) — only `description` field is consumed by the UI; everything else passes through as free-form `Map<String, Value>` (see decision #2)
- Plan name validation: kebab-case lowercase, max 64 chars, auto-suffix `-2`, `-3`, ... on collision (matches `create-command`)
- React Plans tab: 2-column layout (list left ~250px, viewer/editor right), View/Edit toggle on right pane, Delete button with confirmation modal, Re-execute button, empty state when no plans exist
- React Plans store subscribed to `plans-changed` event for live refresh

**Skills (Plugin~/skills/)**

- `save-plan/SKILL.md` — argument-hint `[plan-name]`, optional argument; if absent, asks user via `AskUserQuestion`. Reads the most recent plan from conversation context, formats as markdown with `description` frontmatter, writes to disk
- `plan-execute/SKILL.md` — argument-hint `<plan-name>`, required argument; reads file, prepends "Execute the following plan step-by-step:" to the body, continues conversation with that as the agent's working brief

**Slash + `@` autocomplete (the Big Bundle)**

- `sdk-entry.js` captures the `system/init` event from the SDK and emits a new `CatalogReady` agent-message variant carrying `{commands: [...], agents: [...]}`. Re-emitted on resume so React can rebuild its catalog cache after supervisor restart
- React `catalogStore` consumes `CatalogReady`, exposes `useCommands()` and `useAgents()` hooks
- Rust command `list_project_files` returns a flat list of project-relevant files under `UNITY_PROJECT_PATH`, with exclusions: `Library/`, `Temp/`, `obj/`, `Logs/`, `.vs/`, `.git/`, `node_modules/`, `dist/`, hidden dirs starting with `.` (except `.claude/`). Cached on first call; refresh on a Rust file watcher rooted at the project root (separate from the plans watcher)
- React `useProjectFiles()` hook with the file index
- Slash dropdown — `/` at start of word triggers, filters by typed query, shows command name + description + argument-hint, keyboard nav (ArrowUp/Down/Enter/Tab), Esc/click-outside closes, click-to-select, insert behavior is "name + space" (argument-hint shown but not auto-inserted)
- `@` unified dropdown — same interaction model as `/`, two visible sections (Agents above, Files below) with section headers; filter logic shared; insertion writes `@agent-<n>` for agents and `@<relative-path>` for files
- Both dropdowns are portal-anchored above the textarea (or below, if not enough space above)

## Scope OUT (deferred)

- **`last-run` timestamp tracking on plans** — would require writing back to the plan file on every re-execute, which means the skill mid-conversation has to perform a Write. Can land as v2.1 polish if user demand surfaces
- **Plan templates** (parametrized plans with `<placeholder>` syntax) — v2.1
- **Plan versioning beyond git** — v2.1+
- **Cross-project plan library** — v2.1+
- **Plan import/export beyond markdown** — not planned
- **Plan execution dry-run mode** (preview without running) — v2.1+
- **Plan branching / conditionals** — v2.1+
- **Auto-save plans when user generates one in plan mode** — explicit save only (decision implicit in original F06 doc, kept). Auto-save would clutter
- **MRU / pinned commands / favorites in the slash dropdown** — v2.1
- **Subagent invocation prefix beyond `@agent-`** — Claude Code uses `@agent-<plugin>:<n>` natively; the dropdown surfaces these directly, no special parsing
- **Recently-used files at top of `@` picker** — v2.1; v2.0 sorts files alphabetically within the section
- **File-content preview on hover in `@` picker** — v2.1
- **Fuzzy search inside dropdowns** — v2.0 uses simple prefix/substring match; fuzzy is v2.1
- **Multi-character invocation triggers** beyond `/` and `@` (e.g. `#tag`, `~snippet`) — not planned

## Dependencies

- **Feature 02 (Claude Code Supervisor)** — done. F06 reads the SDK's `system/init` event; that wiring runs in `sdk-entry.js`, which F02 owns. The skills file location (`Plugin~/skills/`) and surfacing mechanism (SDK `plugins` option) are the F02-locked decisions #2 and #3. F06 only adds two skills under that directory; no plugin-loader changes needed.
- **Feature 04 (Interactive Approvals)** — done. The `/save-plan` skill calls `AskUserQuestion` (the SDK built-in) when invoked without an argument; the question card UI that surfaces the prompt is the F04 implementation.
- **Feature 07 (Editor Status Pin)** — done. F07's pin sets `UNITY_PROJECT_PATH` env var when launching the Tauri app; F06 consumes this to resolve `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/` and the file index roots.
- **Feature 01 (External App)** — done. F06 lives entirely inside the Tauri React app and the Rust supervisor — no Unity-side changes, no MCP tool changes.

This feature does not depend on F08 (Rules Page) or F09 (Design Handoff). F08 may visually align its tab with the Plans tab once F09 lands.

## Locked decisions

### Decision #1 — Split file IO: Rust direct for the tab, Claude Code Read/Write for the skills

The Plans tab UI never goes through the agent for CRUD. Rust commands act directly on `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/*.md` so list/read/write/delete are instantaneous and work even when the chat isn't open. The skills (`/save-plan`, `/plan-execute`) run inside the `claude` subprocess and use Claude Code's own Read/Write tools — they share the same disk location but go through the SDK rather than calling Tauri commands.

A Rust file watcher on the plans directory bridges the two paths: any change emits `plans-changed`, React re-fetches the list. No duplicate state, no race.

Alternatives considered: routing all CRUD through the skill (rejected — tab list would block on agent latency, would require the chat to be running for the tab to load); routing skill writes through Tauri commands (rejected — skills run inside `claude`, they cannot reach Tauri's command surface).

### Decision #2 — Frontmatter: minimal + free-form

Only `description` (single-line string, optional) is consumed by the React UI and the skills. Anything else the user adds is parsed into the existing `frontmatter: Map<String, Value>` field on the `Plan` struct (already `Map` in `types.rs`) and round-tripped untouched. No required fields; no `name` (filename is the source of truth); no `created` / `last-run` (filesystem mtime/ctime cover what we need).

`last-run` tracking is genuinely useful but adding it means the `/plan-execute` skill has to write back to the plan file mid-conversation, which is a Write tool call mid-task. Defer to v2.1 with real usage signal.

### Decision #3 — `/save-plan` argument is optional

`/save-plan` (no arg) prompts the user for a name via `AskUserQuestion`. `/save-plan foo` runs directly with `foo` as the name. Same pattern as `create-command`, which establishes the precedent. Both paths run the same name-validation + collision-check logic (see decision #6).

### Decision #4 — `/plan-execute` argument is required

`/plan-execute <name>` requires the name argument; the skill returns an error if absent (and lists available plans via `Glob` to help the user retry). v2.0 keeps the skill simple — listing + AskUserQuestion-driven picker is v2.1 polish if needed.

The Plans tab's "Re-execute" button takes the same path: it sends the literal string `/plan-execute <name>` into the chat as if the user typed it. Symmetrical with manual invocation; no separate code path.

### Decision #5 — Plans tab layout: 2-column, View/Edit toggle on right pane

Left column (~250px): plan list, sorted by mtime descending, showing `<name>` + `<description>` (truncated to one line) + relative mtime ("2h ago"). Click selects.

Right pane: header with action buttons (Re-execute, Delete, View/Edit toggle). Body switches between View (markdown rendered via `react-markdown` — already a dependency from F04) and Edit (monospace textarea). Edit mode adds Save / Cancel buttons. View mode is the default.

Matches the SessionList sidebar pattern from F02 visually. F09 may refine but the structure stands.

### Decision #6 — Plan name validation

Strict kebab-case: `^[a-z0-9][a-z0-9-]*$`, max 64 characters. Same as `create-command`. Enforced both in Rust (`write_plan`) and in React (input field validates as user types in the New Plan flow and in the `/save-plan` skill's argument).

Auto-suffix on collision: if `<name>.md` exists when `/save-plan name` runs, try `<name>-2.md`, `<name>-3.md`, ... up to `-99` before erroring. The Plans tab "rename" flow (v2.1) will use the same logic.

### Decision #7 — Concurrency: last-write-wins

No file locks. The race window (user editing in tab while skill writes from chat) is a corner case; mitigation is the file watcher firing `plans-changed` after every disk write, which causes the tab to reload from disk if the user hasn't pressed Save yet. If the user has unsaved edits, the tab keeps them in memory; pressing Save overwrites whatever the skill wrote. Documented as a known v2.0 limitation; v2.1 may add mtime-based conflict detection if reports surface.

### Decision #8 — Bundle the slash + `@` dropdown into F06

The original v2.0 plan tracked F06 as small (CRUD + 2 skills, ~10 tasks). After F07 cleanup deleted the v1.1.0 in-Editor `Editor/ChatUI/` autocomplete, the Tauri React chat input was left without any slash discovery. The first feature to add user-discoverable slash commands is F06 itself (`/save-plan`, `/plan-execute`). Shipping the skills without the dropdown would mean shipping F06 with both new commands invisible unless the user already knew the names.

The dropdown's data source is free — the SDK already emits the catalog via `system/init` (per ADR-001 mapping table). The work is the React UI surface plus a small Rust file index for `@`. Bundling is small (~12 added tasks; F06 grows from ~10 to ~22, comparable to F02's 24).

### Decision #9 — `@` is a unified picker, not split

`@` opens one dropdown with two sections: Agents above, Files below. Section headers visible. Filter logic shared. Insertion produces the appropriate string per row type (`@agent-<plugin>:<n>` for agents, `@<relative-path>` for files).

The alternative (separate `@agent-` and `@file/` triggers, or splitting `@` into a context-sensitive single-section view) was rejected because it adds parser complexity for marginal UX gain — Claude Code's own CLI behavior is unified, so users expect the unified picker.

### Decision #10 — File index exclusions

The `list_project_files` Rust command excludes (gitignore-style) the following directories at any depth: `Library/`, `Temp/`, `obj/`, `Logs/`, `.vs/`, `.git/`, `node_modules/`, `dist/`, plus any directory whose name starts with `.` except `.claude/` (so the user's `.claude/agents/` and similar are still visible if they want to `@` them). All file types included — no extension filtering. v2.1 may add user-configurable exclusions if needed.

## Cost estimate

**Medium.** Bigger than the original "small-medium" estimate because of the bundled dropdown. Smaller than F02 (24 tasks) but in the same ballpark.

- Rust CRUD + file watcher (Group 1): ~1.5 days
- React Plans tab (Group 2): ~2 days
- Skills (Group 3): ~0.5 day
- system/init catalog capture (Group 4): ~0.5 day
- Slash dropdown (Group 5): ~1.5 days
- `@` unified picker (Group 6): ~2 days
- Final smoke + cleanup (Group 7): ~0.5 day

Total: ~8 days focused work, ~22 tasks across 7 groups.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| `system/init` payload shape changes between SDK versions | low | Pin SDK version in `App~/runtime/package.json` (already done by F02 task 6.5); subscribe to `agent-message` permissively (unknown variants ignored, not crashed on) |
| File watcher misses events on Windows network drives or junctions | medium | Document Windows-only validation; users on exotic mount setups can fall back to manual refresh button (added in task 2.4 as escape hatch) |
| Project file index is huge for big Unity projects | medium | Exclusions cover the worst offenders (`Library/`, `Temp/`, `node_modules/`); index is in-memory with no token cost; if a project exceeds ~50k files, dropdown filtering stays fast (substring search on a Vec). v2.1 may add lazy/streaming index if reports surface |
| User edits plan in tab while skill writes simultaneously | low | Last-write-wins; documented in decision #7. File watcher reload covers most cases |
| Skill writes invalid frontmatter / tab can't parse it | low | `read_plan` falls back to empty frontmatter on YAML parse error; raw content is still readable |
| Slash dropdown hijacks `/` in user's regular text (e.g. URLs, paths) | medium | Only triggers when `/` is at the start of a word AND not preceded by an alphanumeric char; URLs typed mid-message don't trigger. Validated in task 5.5 smoke |
| `@` dropdown shows too many files at once | low | List virtualization (react-window or similar) if needed; for v2.0, plain `<ul>` with limit of ~200 visible (filtered) entries — fine for typical projects |
| Plans dir doesn't exist on first launch | low | `list_plans` creates it on demand (idempotent); writing a plan creates parents |

## Milestone

v2.0.

## Open questions (deferred to implementation)

1. **Relative time formatting library** — likely `date-fns` (already a transitive dep via something) or a tiny `formatDistanceToNow` helper. Decided at task 2.2.
2. **Dropdown anchoring strategy** — fixed positioning relative to textarea cursor vs absolute relative to chat input container. Decided at task 5.2 / 6.3 when wired against real DOM.
3. **Empty-state copy for the Plans tab** — what does it say? Probably "No plans yet. Save a plan from chat with `/save-plan` after Claude generates one in plan mode." Decided at task 2.4.
4. **Re-execute confirmation** — does clicking Re-execute confirm before sending? Probably not (the user clicked the button, intent is clear); skipping confirmation matches Send-message semantics. Confirm at task 2.4.

## Notes

- Don't over-engineer the markdown editor in the tab. Users with strong feelings will edit the file directly in VS Code (the file is on disk in `ProjectSettings/`); the tab is for quick tweaks.
- The slash dropdown's value scales with the number of installed plugins. Today (v2.0 launch) the user sees `mcp-game-deck:*` plus built-ins. Tomorrow (after they install other Claude Code plugins) the same dropdown surfaces those too — no code changes on our side. Same for `@agent-`.
- `Plugin~/skills/save-plan/` and `Plugin~/skills/plan-execute/` are the only new files added under `Plugin~/`; the plugin manifest at `Plugin~/.claude-plugin/plugin.json` doesn't need updating since it lists the directory, not individual skills.
- Rules (Feature 08) will follow the same Rust+React+file-watcher pattern as F06 plans — most of the file watcher and frontmatter parsing code can be factored into a shared module during F08 if reuse opportunities emerge during implementation.

## References

- `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md` — engine decision; F06's "re-execute via skill" path follows from the ADR's mapping table
- `docs/internal/v2-architecture.md` — process layout, plans storage location convention
- `docs/internal/v2-features/02-claude-code-supervisor.md` — F02 locked the `Plugin~/` plugin layout that F06 extends with two new skills
- `docs/internal/v2-features/04-interactive-approvals.md` — F04 supplies the `AskUserQuestion` UI that `/save-plan` invokes when run without an argument
- `docs/internal/v2-features/06-plans-crud-spec.md` — companion executable spec
- `docs/internal/v2-features/06-plans-crud-tasks.md` — companion task breakdown
- Claude Agent SDK system/init event: https://platform.claude.com/docs/en/agent-sdk/streaming
- Claude Code skills documentation: https://code.claude.com/docs/en/skills
- `Plugin~/skills/create-command/SKILL.md` — pattern reference for `save-plan`
