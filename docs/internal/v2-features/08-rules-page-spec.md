# Feature 08 — Rules Page — Spec

> **Status:** `agreed` — design decisions locked May 2026 (see `08-rules-page.md` for full rationale).
> **Companion:** `08-rules-page-tasks.md` (decomposed work breakdown for Claude Code execution).

## What this is

Single deliverable: a **Rules tab** in the Tauri React app for CRUD on per-project behavior rules stored as markdown under `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/rules/<name>.md`, plus a **bundle pipeline** that compiles every `enabled: true` rule into a single file the SDK injects as `appendSystemPromptFile` on every `query()`.

After this feature, the user can codify project-specific conventions ("always use TextMeshPro", "never modify `Assets/ThirdParty/`", "namespace as `ProjectName.Module`") in markdown files that automatically apply to every conversation — no more repeating instructions, no more drift between teammates, no more loss when context window pressure forces the agent to forget.

This is the third Rust+React+watcher feature in v2.0 (after F06 plans and F06 project files) — the third occurrence is the right moment to extract a shared `markdown_doc` helper module rather than triplicate the frontmatter/kebab-validate/atomic-write boilerplate.

## Architecture overview

```
                        ┌──────────────────────────────────────────────────┐
                        │  EXTERNAL APP (Tauri)                            │
                        │                                                   │
   ┌──────────────┐     │  ┌─ React (App~/src/) ─────────────────────────┐ │
   │ User toggles │ ──► │  │ RulesRoute → RulesList | RulePane           │ │
   │ or edits a   │     │  │   ├─ checkbox per row (toggle)              │ │
   │ rule         │     │  │   └─ header: <N>/10 enabled, ~T tokens      │ │
   └──────────────┘     │  │                                              │ │
                        │  │  rulesStore  ◄─────────────                 │ │
                        │  │   ▲                                          │ │
                        │  │   │ rules-changed event                      │ │
                        │  └──────────────┬──────────────────────────────┘ │
                        │                 │ Tauri cmds   │ Tauri events    │
                        │                 ▼              │                 │
                        │  ┌─ Rust (src-tauri/) ─────────┴───────────────┐ │
                        │  │ commands/rules.rs   list/read/write/        │ │
                        │  │                     delete/toggle           │ │
                        │  │ rules_watcher       notify on rules dir     │ │
                        │  │ rules_bundle        compose + write bundle  │ │
                        │  │                     on every rules-changed  │ │
                        │  │ markdown_doc        shared helpers (F06     │ │
                        │  │                     plans + F08 rules)      │ │
                        │  └──────────────────────────────────────────────┘ │
                        │           │ spawn-time env var                  │
                        │           ▼                                     │
                        │  ┌─ Node child (sdk-entry.js) ──────────────────┐ │
                        │  │ MCP_GAME_DECK_RULES_BUNDLE_PATH env var      │ │
                        │  │ Every query() options:                       │ │
                        │  │   if exists && size > 0:                     │ │
                        │  │     appendSystemPromptFile: path             │ │
                        │  │   else: omit                                  │ │
                        │  └──────────────────────────────────────────────┘ │
                        └──────────────────────────────────────────────────┘
                                              │
                                              ▼
                        ┌──────────────────────────────────────────────────┐
                        │  USER UNITY PROJECT (filesystem)                 │
                        │                                                   │
                        │  ProjectSettings/GameDeck/rules/     ◄── source  │
                        │    prefer-textmeshpro.md             of truth    │
                        │    no-thirdparty-modifications.md                │
                        │    ...                                            │
                        │                                                   │
                        │  Library/MCPGameDeck/                ◄── derived │
                        │    rules-bundle.md                   (gitignored)│
                        └──────────────────────────────────────────────────┘
```

Two clean lanes again. The Rules tab and the SDK meet at the bundle file. CRUD on individual rules goes through Rust commands; the SDK never reads individual rule files. The bundle compiler runs Rust-side on every `rules-changed` event so the file is always fresh — the SDK reads it (via the SDK's own file handling for `appendSystemPromptFile`) on every `query()`, picking up changes automatically without any stdin coordination.

## Stack

**Rust crates (existing, no additions):**
- `notify` v6 + `notify-debouncer-mini` — already in deps from F06 plans watcher
- `serde_yaml` v0.9 — already in deps from F06 frontmatter parsing
- `tauri`, `serde`, `serde_json` — already core

**TypeScript / React (existing, no additions):**
- `react-markdown` — already from F04
- `date-fns` — already from F06
- Zustand (or whatever store pattern F06 used) — already

**No new dependencies.** The third Rust+React+watcher feature reuses everything F06 already pulled in.

## File layout

**New files:**

```
App~/src-tauri/src/
├── markdown_doc.rs                  # SHARED: frontmatter parse + kebab-name
│                                    #   validate + ensure_dir + atomic_write
├── rules_bundle.rs                  # compose + write Library/MCPGameDeck/
│                                    #   rules-bundle.md from enabled rules
└── rules_watcher.rs                 # notify-based; mirrors plans_watcher

App~/src/
├── components/
│   ├── RulesList.tsx                # left column, checkbox + token count
│   ├── RulePane.tsx                 # right pane, view/edit toggle
│   ├── RuleViewer.tsx               # react-markdown rendered
│   └── RuleEditor.tsx               # monospace textarea
├── hooks/
│   └── useRulesSubscription.ts      # subscribes to rules-changed
├── lib/
│   └── tokenEstimate.ts             # chars/4 heuristic shared
└── stores/
    └── rulesStore.ts                # list, current selection, edit state
```

**Modified files:**

```
App~/src-tauri/src/
├── commands/rules.rs                # F01-era stubs replaced; uses
│                                    #   markdown_doc helpers
├── commands/plans.rs                # refactored to use markdown_doc
│                                    #   (parse_frontmatter, validate_name,
│                                    #   atomic_write extracted out)
├── commands/mod.rs                  # no change (rules cmds already exported)
├── events.rs                        # add rules-changed emit helper
├── types.rs                         # expand Rule + RuleMeta; add
│                                    #   RulesChangedKind + payload
├── lib.rs                           # spawn rules_watcher; init bundle on
│                                    #   startup; tie MCP_GAME_DECK_RULES_
│                                    #   BUNDLE_PATH env var to spawn.rs
├── plans_watcher.rs                 # consume markdown_doc helpers
├── claude_supervisor/spawn.rs       # set MCP_GAME_DECK_RULES_BUNDLE_PATH
│                                    #   env var before spawning sdk-entry.js
└── claude_supervisor/sdk_entry.js   # check env var + file exists/size on
│                                    #   each query(); add
│                                    #   appendSystemPromptFile when present

App~/src/
├── routes/RulesRoute.tsx            # placeholder replaced
└── ipc/types.ts                     # mirror updated types
```

**Unchanged:** Editor C# code, MCP server, MCP proxy, Plugin~/agents/*, Plugin~/skills/*, Plugin~/knowledge/*, Plugin~/.claude-plugin/plugin.json.

## Data shapes

### `RuleMeta` and `Rule` (expand existing in `types.rs`)

The stub `Rule` today is `{name, enabled, content}`. Expand to:

```rust
pub struct RuleMeta {
    pub name: String,           // filename without .md
    pub last_modified: i64,     // ms (NOT seconds — mtime in milliseconds)
    pub enabled: bool,
    pub description: Option<String>,
    pub applies_to: Vec<String>,    // informational only in v2.0
    pub estimated_tokens: u32,      // chars / 4 heuristic
}

pub struct Rule {
    pub name: String,
    pub last_modified: i64,         // ms
    pub content: String,            // body without frontmatter delimiters
    pub frontmatter: RuleFrontmatter,
    pub estimated_tokens: u32,
}

pub type RuleFrontmatter = serde_json::Map<String, serde_json::Value>;
```

`description` is convenience-extracted in `RuleMeta` so `list_rules` doesn't return full bodies; `read_rule` returns the whole frontmatter map. Writes preserve unknown fields verbatim (same as plans).

`applies_to` is parsed as `Vec<String>` for ergonomic UI rendering (chips/tags), but its value is **informational only** in v2.0 — the bundle compiler ignores it. Documented as v2.1 future work.

**Note on `last_modified` units:** v2.0 normalizes to **milliseconds** for new structs (matches `SessionSummary.last_modified` and `Date.now()` math on the React side). The existing `PlanMeta.last_modified` in seconds is a known follow-up from F06; extracting `markdown_doc` is the natural moment to flip plans to ms too. Done in the same task (1.1) so both consumers move together.

### `RulesChangedPayload` (new event)

```rust
pub struct RulesChangedPayload {
    pub kind: RulesChangedKind,     // Created | Modified | Deleted
    pub name: Option<String>,
}
```

Same kind-synthesis caveat as plans (the debouncer collapses to `Any`; `classify_event` reconstructs via `was_known` + `exists_now`). The React consumer refetches `list_rules` on every event; the kind is informational.

### Bundle file (no Rust struct; just a markdown file)

Path: `<UNITY_PROJECT_PATH>/Library/MCPGameDeck/rules-bundle.md`

Format when one or more rules are enabled:

```markdown
## Project Rules

The user has configured the following rules for this Unity project. Apply them consistently throughout the conversation:

### prefer-textmeshpro

When creating UI text, always use TextMeshPro components, never the built-in UI Text component.

This applies to:
- New UI labels
- Buttons that need text
- Any UI element that displays strings

Exceptions: only the legacy editor scenes in `Assets/Legacy/` keep their existing UI Text — don't migrate them.

---

### namespace-convention

Use the namespacing pattern `ProjectName.Module` for all new C# classes...

---

...
```

Ordering: enabled rules sorted alphabetically by name (deterministic; helps prompt caching). Each rule's body is written verbatim — no transformation, no truncation. The `---` separator between rules is for human readability when inspecting the file; the SDK reads the whole text as the appended system prompt.

When zero rules are enabled: Rust **deletes** the bundle file (`fs::remove_file`, ignore `NotFound`). JS detects absence via `fs.existsSync()` + size check and omits `appendSystemPromptFile` entirely. Avoids feeding the SDK a zero-byte file (unknown SDK behavior on empty content; safer to omit).

## Wire protocol changes

### Tauri commands (Rust → React)

The stubs in `commands/rules.rs` are replaced with real implementations:

- `list_rules() -> Vec<RuleMeta>` — reads dir, parses frontmatter, returns sorted by `last_modified` descending. Creates the dir on first call (idempotent, mirror F06).
- `read_rule(name: String) -> Result<Rule, AppError>` — validates name, reads file, splits frontmatter from body, returns `Rule`. Malformed YAML → empty `frontmatter` map, body still readable.
- `write_rule(name: String, content: String) -> Result<(), AppError>` — validates name, atomic tmp-then-rename write. **Overwrite semantics** (same as F06 `write_plan`). UI's "+ New rule" path validates non-collision before invoking.
- `delete_rule(name: String) -> Result<(), AppError>` — validates name, `fs::remove_file`.
- `toggle_rule(name: String, enabled: bool) -> Result<(), AppError>` — surgical: reads file, modifies only the `enabled:` key in the frontmatter map (preserves body and unknown frontmatter fields), atomic write back. If the user is at 10/10 enabled and tries to enable an 11th, returns `AppError::InvalidInput("Rule cap reached (10 enabled). Disable one first.")` — server-side cap enforcement as defense in depth, even though the React UI also enforces it client-side.

### Tauri events (Rust → React)

- `rules-changed` with `RulesChangedPayload` — emitted by the rules watcher (debounced 250ms, same window as plans).

### Env var contract (Rust → Node spawn)

`spawn.rs` adds one env var when launching `sdk-entry.js`:

- `MCP_GAME_DECK_RULES_BUNDLE_PATH=<absolute path to Library/MCPGameDeck/rules-bundle.md>`

This is set unconditionally (path is deterministic from `UNITY_PROJECT_PATH`); the file at that path may or may not exist depending on whether any rules are enabled. JS checks existence at query-time, not at spawn-time.

### `sdk-entry.js` integration

In `handleInput`'s `queryOptions` construction, after the existing `cwd`, `permissionMode`, `mcpServers`, `plugins`, `additionalDirectories`, `canUseTool`:

```javascript
const bundlePath = process.env.MCP_GAME_DECK_RULES_BUNDLE_PATH;
if (bundlePath && bundlePath.length > 0)
{
  try
  {
    const stats = fsSync.statSync(bundlePath);  // sync OK — boot path
    if (stats.isFile() && stats.size > 0)
    {
      queryOptions.appendSystemPromptFile = bundlePath;
    }
  }
  catch (err)
  {
    // ENOENT or read error: omit; not fatal
  }
}
```

Same check happens inside `runHealthCheck` so the health probe also injects the bundle (smoke verifies real configuration end-to-end). Sync `statSync` is fine on the boot/query path — the file is local, the call is microseconds. No new dep needed.

**No stdin protocol changes.** No new `AgentMessage` variant. No new control message type. The whole Rust↔JS coordination is "Rust writes the file; JS reads its path from env and stats it on every turn."

## UX details

### Rules tab layout

```
┌─ Rules tab ──────────────────────────────────────────────────────────┐
│  ┌─ Rules (3/10 enabled, ~114 tokens) ──┐  ┌─ Prefer TextMeshPro ──┐ │
│  │ + New rule                             │  │ [Toggle] [Delete]    │ │
│  ├────────────────────────────────────────┤  │ [View | Edit]        │ │
│  │ ☑ prefer-textmeshpro                   │  ├──────────────────────┤ │
│  │   Always use TMP, never UI Text...     │  │                      │ │
│  │   ~52 tokens · 2d ago                  │  │  ## Always use Text..│ │
│  ├────────────────────────────────────────┤  │                      │ │
│  │ ☑ no-thirdparty-modifications          │  │  When creating UI ...│ │
│  │   Never modify scripts in Assets/T...  │  │                      │ │
│  │   ~38 tokens · 5d ago                  │  │                      │ │
│  ├────────────────────────────────────────┤  │  applies to: ui      │ │
│  │ ☑ namespace-convention                 │  │                      │ │
│  │   Use ProjectName.Module pattern...    │  │                      │ │
│  │   ~24 tokens · 1w ago                  │  │                      │ │
│  ├────────────────────────────────────────┤  │                      │ │
│  │ ☐ aggressive-pooling                   │  │                      │ │
│  │   Pool everything spawned in gameplay  │  │                      │ │
│  │   ~67 tokens · 3w ago · disabled       │  │                      │ │
│  └────────────────────────────────────────┘  └──────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

Differences from `PlansList`:

- **Checkbox per row** (`☑` / `☐`) toggles `enabled`. Click → `toggle_rule` → file watcher → list refresh. Visual is the checkbox glyph + slightly muted styling when disabled.
- **Header summary**: `Rules (<enabled-count>/<total-count> enabled, ~<total-tokens> tokens)`. Total tokens sums `estimated_tokens` of all enabled rules (computed React-side from `rulesStore`).
- **Per-row info**: `~N tokens · <relative-mtime>` (plus `· disabled` suffix for disabled rules).
- **Cap behavior** (10 enabled): when user clicks `☐` on a disabled rule and `enabled.count === 10`, the toggle attempt is rejected (client-side check first; server `toggle_rule` returns `AppError::InvalidInput` as defense). UI shows an inline toast: "Limit of 10 enabled rules. Disable one first." Toast auto-dismisses after ~3s.

### Pane (`RulePane.tsx`)

Mirrors `PlanPane` structure visually for cohesion. Action header strip:

- `[Toggle]` — same effect as the row checkbox; toggles `enabled` on the currently-open rule.
- `[Delete]` — confirmation modal (reuse F06's `DeleteConfirmModal` if extracted; otherwise inline confirmation in the pane).
- `[View | Edit]` — segmented toggle. View is default.

In View mode: body is `RuleViewer` (`react-markdown`-rendered). Below the rendered body, a small metadata strip shows `applies to: <chip>, <chip>, ...` for each entry in `applies_to`. If empty, the strip is omitted.

In Edit mode: header changes to `[Save] [Cancel]`. Body is `RuleEditor` (monospace textarea, Cmd/Ctrl+S = save, Esc = cancel — same as `PlanEditor`).

Empty state when no rule selected: "Select a rule, or add a new one to give Claude project-specific instructions that persist across conversations."

### New rule flow

`+ New rule` button opens an inline form (same UX shape as F06's new-plan form):

1. Name input — kebab-case validation per `markdown_doc::validate_kebab_name`.
2. On Create: `invoke('write_rule', {name, content: TEMPLATE})` with this template:

```markdown
---
enabled: false
description: ""
applies-to: []
---

# <name>

<rule body — describe what Claude should do, when it applies, and any exceptions>
```

3. On success: select the newly-created rule and enter Edit mode (consistent with F06 new-plan flow).
4. Rule starts **disabled** by default. User writes the content, then explicitly toggles enabled (single deliberate action — avoids accidentally injecting half-written rules into the system prompt).

### Token estimator

Heuristic: `tokens ≈ chars / 4` (rounded up). Documented in the UI's first-time hint and in `tokenEstimate.ts` as an approximation; v2.1 can swap in a real tokenizer if usage signals demand. The estimate counts the full file content (frontmatter + body) since that's what the bundle includes — slight overcount because YAML keys also count, but that's directionally fine for "is this rule getting expensive?"

Visual warning: rules with `estimated_tokens > 500` show a yellow warning glyph in the list row. Not blocking, just informational. (10 rules at 500 tokens = 5K tokens of system prompt, comparable to a small `CLAUDE.md`. Power users can opt in.)

## Bundle composition algorithm

`rules_bundle.rs::recompose(project_root: &Path) -> std::io::Result<()>`:

1. Resolve `<project_root>/ProjectSettings/GameDeck/rules/`. If absent, treat as "zero rules" → delete bundle if exists, return.
2. Read all `*.md` files in the directory. For each, parse frontmatter via `markdown_doc::parse_frontmatter`. Filter to those where `enabled == true`.
3. If filter result is empty: `fs::remove_file(<project_root>/Library/MCPGameDeck/rules-bundle.md)`, ignoring `NotFound`. Return.
4. Sort the filtered list alphabetically by name (rule filename without `.md`).
5. Compose the bundle text using the format documented above (`## Project Rules` heading, intro paragraph, `### <name>` per rule, body verbatim, `---` separator).
6. Ensure `<project_root>/Library/MCPGameDeck/` exists (idempotent).
7. Atomic write to `<project_root>/Library/MCPGameDeck/rules-bundle.md` via tmp-then-rename (using `markdown_doc::atomic_write`).

`recompose` is called from:
- App startup (`lib.rs::setup`): initial composition so the bundle is fresh on first `query()`.
- The rules watcher's event loop (`rules_watcher.rs`): every `rules-changed` event triggers a recompose before emitting to React.
- After `restart_supervisor` (`commands/connection.rs`): so a project switch picks up the new project's rules.

Single function, single source of truth, called from three places. No caching in memory — re-reads from disk every time. Cheap (typically <10 rule files, each <2KB).

## Definition of done

The following 17 scenarios pass on Windows 11 against a real Unity 6 project with MCP Game Deck installed:

1. Open Rules tab on a project with no rules → empty state hint shows; header reads "Rules (0/10 enabled, ~0 tokens)"; bundle file does not exist.
2. Click "+ New rule" → form opens → enter `prefer-textmeshpro` → Create → rule appears in list (disabled, ~N tokens shown), opens in Edit mode with template content.
3. Edit content → Save → View mode shows rendered markdown; file written to `ProjectSettings/GameDeck/rules/prefer-textmeshpro.md`; bundle file still does NOT exist (rule is disabled).
4. Toggle the rule on via the row checkbox → list refreshes, header reads "Rules (1/10 enabled, ~N tokens)"; bundle file created at `Library/MCPGameDeck/rules-bundle.md` containing the rule.
5. Send a message in Chat tab — verify in stderr log (or via SDK trace) that `appendSystemPromptFile` is set on the `query()` call.
6. Ask Claude in chat: "what rules am I working under?" — Claude references the rule content (smoke that the bundle is reaching the model).
7. Toggle the rule off via the pane Toggle button → bundle file deleted; next `query()` omits `appendSystemPromptFile`.
8. Create 10 enabled rules → header reads "Rules (10/10 enabled, ~N tokens)"; attempt to toggle an 11th on → toast "Limit of 10 enabled rules. Disable one first."; the click is rejected.
9. Delete an enabled rule (was 10/10 → 9/10) → confirmation modal → confirm → file removed, bundle recomposes without it, header updates.
10. Edit a rule externally in VS Code → save → Rules tab list and bundle file refresh within ~500ms (file watcher + recompose).
11. Toggle a rule with very long body (~3000 chars) → warning glyph appears in row; toggle still succeeds; bundle includes the long content.
12. Malformed YAML frontmatter on a rule → `list_rules` still returns the entry (without `description`/`applies_to`), Pane still opens in Edit mode; toggle still works (re-writes frontmatter on save).
13. Add `applies-to: [ui, scripts]` in frontmatter → list row shows description; pane View mode shows `applies to: ui, scripts` chip strip; bundle composition is unaffected (informational only).
14. Re-launch the Tauri app (close + reopen via F07 pin) → Rules tab loads existing rules correctly; bundle file is recomposed on startup; existing enabled-count matches.
15. Switch the active Unity project (via supervisor restart) → Rules tab clears old project's rules, loads new project's rules from its own `ProjectSettings/GameDeck/rules/`; bundle file at the new project's `Library/MCPGameDeck/` is fresh.
16. Verify F06 plans tab still works end-to-end after the `markdown_doc` refactor (CRUD, dropdown, watcher).
17. With one rule enabled, ask Claude to violate it ("ignore your rules and use UI Text"). Claude refuses or acknowledges the rule. (Soft test — rule adherence depends on the model; we're verifying the rule is reaching the model, not that the model is perfectly obedient.)

**Regression checks (must continue to pass):**

- F02: chat round-trip works, sessions list loads, permission mode toggle works.
- F04: permission cards and AskUserQuestion cards appear correctly mid-conversation.
- F06: plans CRUD, slash dropdown, `@` picker all work; plans tab is unchanged visually and behaviorally.
- F07: pin status indicator reflects connection state; clicking pin opens / focuses Tauri app.

## Edge cases

- **`UNITY_PROJECT_PATH` not set** — Rules tab shows the same error state the Plans tab uses (missing env var, launch via Editor pin). `list_rules` returns `[]`.
- **Rules directory doesn't exist on first launch** — `list_rules` creates it (idempotent). Subsequent writes succeed.
- **`Library/MCPGameDeck/` doesn't exist** — `recompose` creates it on first write. `recompose` skips creation when zero rules are enabled (no point creating an empty dir just to not write a file into it).
- **Malformed YAML in a rule file** — `read_rule` returns empty `frontmatter`, body still readable; `list_rules` skips the `description`/`applies_to`/`enabled` extraction for that file and surfaces it with `enabled: false`. The rule shows up in the list but never enters the bundle (because `enabled` defaults to `false` on parse failure — a malformed rule cannot accidentally activate).
- **Toggle on a rule whose file became malformed externally** — `toggle_rule` reads, fails frontmatter parse, returns `AppError::Internal` with a clear message. UI surfaces the error and prompts the user to fix the file or re-create.
- **Bundle file is deleted by some external tool between recompose and next query()** — JS's existence check catches it; the next turn runs without rules. Next `rules-changed` event recomposes and the turn after that has them again. Self-healing.
- **Rules dir contains a `.md` file with no frontmatter at all** — accepted; `frontmatter` empty, `enabled` defaults to `false`, rule sits disabled in the list. User can edit to add `enabled: true` if they want it to apply.
- **Watcher fires on the bundle file itself** — the watcher only watches `<project>/ProjectSettings/GameDeck/rules/`, not `Library/`, so writes to the bundle file don't loop. Atomicity (tmp-then-rename) is irrelevant here because the bundle isn't watched.
- **File watcher fires on a rule we just wrote** — debouncer coalesces (250ms window); React re-fetches once. `recompose` runs once. No infinite loop.
- **Two rapid toggles** (☐ → ☑ → ☐ within 250ms) — watcher coalesces into one `Modified` event. Final state reflects whatever the second toggle persisted. UI optimistic update prevents visual flicker (set local state immediately, reconcile on watcher event).
- **User enables 10 rules then disables one externally** — watcher fires, recompose runs, bundle reflects 9 rules. Cap calculation in the React UI also re-reads from `list_rules`, so subsequent enables work correctly up to the new 10/10 ceiling.
- **`applies-to` value is a string instead of an array** in user-edited frontmatter (e.g. `applies-to: ui`) — `read_rule` falls back to `applies_to: []` (informational field; not worth erroring). v2.1 may coerce single-string to single-element array.
- **`enabled` value is not a boolean** (e.g. `enabled: "yes"`) — falls back to `false` (conservative; can't accidentally inject). User must use literal YAML `true` / `false`.
- **Recompose during query()** — race window where Rust is rewriting the bundle while JS is reading it for `appendSystemPromptFile`. Atomic tmp-then-rename means JS either sees the old fully-formed file or the new fully-formed file, never a partial write. Worst case: one turn uses the previous bundle; next turn picks up the new one.
- **Long rule (>2k chars)** — works; UI shows a warning glyph at >500 estimated tokens per rule. Bundle composition is identical (no truncation).
- **Cap was at 10 and user deletes an enabled rule** — falls to 9/10. Subsequent enable on a disabled rule works.
- **All 10 rules are deleted externally while the tab is open** — watcher fires for each (debounced), list goes to empty, bundle file is deleted by recompose. UI returns to empty state cleanly.

## Notes

- **No new dependencies.** Third iteration of the Rust+React+watcher pattern; everything F08 needs is already in `Cargo.toml` and `package.json` from F06.
- **`markdown_doc.rs` is a refactor, not new functionality.** Extract the existing helpers from `commands/plans.rs` (`parse_frontmatter`, `validate_plan_name` → renamed `validate_kebab_name`, `ensure_dir`, `next_available_suffix`, atomic tmp-rename write). F06 tests move alongside. F06 plans.rs imports from `markdown_doc` instead of declaring its own copies. F08 rules.rs imports the same helpers from the start. The refactor is task 1.1 — landing it first means everything downstream uses the shared module from day one, and the F06 unit test suite gates correctness of the extraction.
- **`last_modified` unit normalization** happens during the markdown_doc refactor: the shared `read_metadata_ms` helper returns milliseconds. `PlanMeta` and `RuleMeta` both consume it. F06 follow-up resolved.
- **Bundle file path: `Library/MCPGameDeck/rules-bundle.md`** — Library/ is gitignored by Unity convention; the bundle is a derived artifact, not source. Source of truth is `ProjectSettings/GameDeck/rules/*.md` (versioned by user's git).
- **Toggle is surgical, not full rewrite.** `toggle_rule` parses the frontmatter, updates only the `enabled` key, re-serializes back. Unknown frontmatter fields and the body are preserved verbatim. (Implementation: parse to `serde_yaml::Value`, mutate, re-serialize, splice into the file with body untouched. If round-trip fails — non-mapping frontmatter, etc — fall back to InvalidInput error rather than silently rewriting.)
- **Cap of 10 is server-side too.** `toggle_rule` re-reads the dir and counts currently-enabled rules before applying a `true` toggle. UI's client-side check is the friendly path; the server-side check is the safety net. If two tabs were ever to race (impossible in v2.0 — single-instance — but cheap to defend), the second would land an `InvalidInput` and the UI would surface the toast.
- **`applies-to` is dead weight in v2.0.** The field is parsed, displayed, round-tripped, but the bundle compiler ignores it. Documented in the UI Pane footer ("v2.0 informational only; planned to filter per-subagent in v2.1"). When v2.1 lands subagent-aware injection, the field is already in the data and the migration is one-sided (compiler logic only, file format unchanged).
- **Bundle is composed Rust-side, not JS-side.** Rust owns the disk; JS owns the wire to the SDK. Symmetric with how F02 owns the supervisor lifecycle (Rust) and the SDK options construction (JS).
- **The SDK reads the file at `query()` time.** No caching needed on either side. Mutations propagate at the speed of the next turn — typically <1s after a toggle. v2.1 may add a "rule changed mid-conversation" notice in the chat, but v2.0 just lets the next turn reflect.
- **What v2.0 does NOT do** (deferred to v2.1+, mirror F08 design doc Scope OUT):
  - Conditional rules (only apply if X)
  - Rule libraries / sharing across projects
  - Rule auto-suggestion ("I noticed you keep correcting me — save this as a rule?")
  - Per-subagent filtering via `applies-to`
  - Rule conflict detection
  - Token cost analytics dashboard
  - `/save-rule` chat skill (decided against in design phase)
