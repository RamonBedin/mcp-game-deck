# Feature 06 — Plans CRUD — Spec

> **Status:** `agreed` — design decisions locked April 2026 (see `06-plans-crud.md` for full rationale).
> **Companion:** `06-plans-crud-tasks.md` (decomposed work breakdown for Claude Code execution).

## What this is

Two coupled deliverables:

1. **Plans tab** — Tauri-side CRUD UI for plan markdown files stored at `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/<n>.md`. Lists, opens, edits, deletes, re-executes. Real Rust backend replacing the F01 stubs in `commands/plans.rs`.
2. **Slash + `@` autocomplete dropdown** — chat input feature that surfaces all available slash commands (built-ins, plugin commands, user `.claude/commands/` and `.claude/skills/`, third-party plugins) and `@`-mention targets (agents from any source + project files indexed by Rust). Triggered by `/` and `@` at the start of a word.

After this feature, MCP Game Deck has v2.0-ready discoverability: the user types `/`, sees every command available; types `@`, sees every agent + every project file. Plus a real Plans tab that round-trips between chat (`/save-plan`, `/plan-execute`) and the React UI.

## Architecture overview

```
                        ┌──────────────────────────────────────────────────┐
                        │  EXTERNAL APP (Tauri)                            │
                        │                                                   │
   ┌──────────────┐     │  ┌─ React (App~/src/) ─────────────────────────┐ │
   │ User types   │ ──► │  │ ChatRoute → ChatInput                       │ │
   │ "/" or "@"   │     │  │   ├─ useSlashAutocomplete  ──┐              │ │
   └──────────────┘     │  │   └─ useAtAutocomplete     ──┤              │ │
                        │  │                              │              │ │
                        │  │  catalogStore  ◄─────────────┘  (commands,  │ │
                        │  │   ▲                              agents)    │ │
                        │  │   │ CatalogReady event                      │ │
                        │  │   │                                          │ │
                        │  │  PlansRoute → PlansList | PlanPane          │ │
                        │  │   ▲                                          │ │
                        │  │   │ plans-changed event                      │ │
                        │  │   │                                          │ │
                        │  │  plansStore  (list, current selection)      │ │
                        │  └──────────────┬──────────────┬───────────────┘ │
                        │                 │ Tauri cmds   │ Tauri events    │
                        │                 ▼              │                 │
                        │  ┌─ Rust (src-tauri/) ─────────┴───────────────┐ │
                        │  │ commands/plans.rs   list/read/write/delete  │ │
                        │  │ commands/files.rs   list_project_files      │ │
                        │  │                                              │ │
                        │  │ plans_watcher       notify on plans dir     │ │
                        │  │ files_watcher       notify on project root  │ │
                        │  │                                              │ │
                        │  │ events.rs           plans-changed,          │ │
                        │  │                     project-files-changed   │ │
                        │  └──────────────────────────────────────────────┘ │
                        │                                                   │
                        │  ┌─ Node child (App~/runtime/sdk-entry.js) ────┐ │
                        │  │ system/init capture → CatalogReady          │ │
                        │  │ Skills run in claude subprocess:            │ │
                        │  │   /save-plan   → Read context, Write file   │ │
                        │  │   /plan-execute <name> → Read file, run     │ │
                        │  └──────────────────────────────────────────────┘ │
                        └──────────────────────────────────────────────────┘
                                              │
                                              ▼
                        ┌──────────────────────────────────────────────────┐
                        │  USER UNITY PROJECT (filesystem)                 │
                        │                                                   │
                        │  ProjectSettings/GameDeck/plans/                 │
                        │    setup-2d-roguelike-scene.md                   │
                        │    refactor-spawn-system.md                      │
                        │    ...                                            │
                        └──────────────────────────────────────────────────┘
```

The Plans tab and the skills meet at the disk. The dropdown reads the catalog from the SDK and the file index from Rust. No path bridges the agent for tab CRUD; no path goes through Rust for skill execution. Two clean lanes.

## Stack

**Rust (added crates):**
- `notify` v6 + `notify-debouncer-mini` v0.4 — file watcher for plans dir + project root
- `serde_yaml` v0.9 — frontmatter parsing
- `walkdir` v2 — file index walk (already in dep tree from F02; verify before adding)

**Rust (existing crates used):**
- `tauri` (commands, events, state) — already core
- `serde` / `serde_json` — already core

**TypeScript / React (added deps):**
- `date-fns` v3 — relative time formatting (verify if already present from F02; if so, no add)

**TypeScript / React (existing deps used):**
- `react-markdown` — Plan viewer rendering (came in via F04)
- React + Tauri IPC bindings — F01 baseline
- `lucide-react` for icons — F01 baseline

**Skills:** plain markdown with YAML frontmatter, written under `Plugin~/skills/<n>/SKILL.md`. No code, no deps; pattern follows `Plugin~/skills/create-command/SKILL.md`.

## File layout

**New files:**

```
App~/src-tauri/src/
├── commands/
│   └── files.rs                     # list_project_files Tauri command
├── plans_watcher.rs                 # notify-based watcher → emits plans-changed
└── files_watcher.rs                 # notify-based watcher → emits project-files-changed

App~/src/
├── components/
│   ├── PlansList.tsx                # Left column of Plans tab
│   ├── PlanPane.tsx                 # Right column container, view/edit toggle
│   ├── PlanViewer.tsx               # react-markdown rendered view
│   ├── PlanEditor.tsx               # monospace textarea with save/cancel
│   ├── SlashDropdown.tsx            # / autocomplete UI
│   ├── AtDropdown.tsx               # @ unified picker UI
│   └── DeleteConfirmModal.tsx       # reusable confirm modal (or extend existing)
├── hooks/
│   ├── useSlashAutocomplete.ts      # / detection + state machine
│   ├── useAtAutocomplete.ts         # @ detection + state machine
│   └── useProjectFiles.ts           # subscribes to project-files-changed, calls list_project_files
└── stores/
    ├── plansStore.ts                # list, current selection, edit state
    └── catalogStore.ts              # commands + agents from system/init

Plugin~/skills/
├── save-plan/
│   └── SKILL.md
└── plan-execute/
    └── SKILL.md
```

**Modified files:**

```
App~/src-tauri/src/
├── commands/plans.rs                # stubs replaced with real implementations
├── commands/mod.rs                  # register list_project_files
├── events.rs                        # PlansChangedPayload, ProjectFilesChangedPayload
├── types.rs                         # FileIndexEntry; AgentMessage::CatalogReady variant
├── lib.rs                           # spawn watchers in setup hook; register file index state
└── main.rs                          # if any cmd registration changes propagate

App~/runtime/
└── sdk-entry.js                     # system/init capture → emit CatalogReady on stdout

App~/src/
├── routes/PlansRoute.tsx            # placeholder replaced with real 2-col layout
├── routes/ChatRoute.tsx             # integrate SlashDropdown + AtDropdown into chat input
└── ipc/types.ts                     # CatalogReady, PlansChangedPayload, FileIndexEntry, etc.
```

**Unchanged:** Editor C# code, MCP server, MCP proxy, Plugin~/agents/*, Plugin~/.claude-plugin/plugin.json, Plugin~/knowledge/*.

## Data shapes

### `Plan` and `PlanMeta` (already in `types.rs`, refined here)

```rust
pub struct PlanMeta {
    pub name: String,           // filename without .md extension
    pub last_modified: i64,     // mtime as unix seconds
    pub description: Option<String>,  // ← ADDED in this feature
}

pub struct Plan {
    pub name: String,
    pub last_modified: i64,
    pub content: String,        // body without frontmatter delimiters
    pub frontmatter: PlanFrontmatter,  // Map<String, Value> — free-form
}

pub type PlanFrontmatter = serde_json::Map<String, serde_json::Value>;
```

`description` is convenience-extracted in `PlanMeta` so the list view doesn't have to read every plan's full body. `read_plan` returns the whole `frontmatter` map. Writes preserve unknown fields verbatim.

### `FileIndexEntry` (new)

```rust
pub struct FileIndexEntry {
    pub path: String,           // relative to UNITY_PROJECT_PATH, forward slashes
    pub kind: FileKind,         // file | dir
}

pub enum FileKind {
    File,
    Directory,
}
```

Returned by `list_project_files`. Directories included so the user can `@SomeFolder/` to reference a folder.

### `CatalogReady` agent-message variant (new on the wire)

```rust
// In types.rs AgentMessage enum
CatalogReady {
    commands: Vec<CatalogCommand>,
    agents: Vec<CatalogAgent>,
}

pub struct CatalogCommand {
    pub name: String,           // "save-plan", "mcp-game-deck:save-plan", "/clear"
    pub description: Option<String>,
    pub argument_hint: Option<String>,
    pub source: CommandSource,  // built-in | plugin | user-command | user-skill | third-party
}

pub struct CatalogAgent {
    pub name: String,           // "mcp-game-deck:unity-shader-specialist"
    pub description: Option<String>,
    pub source: AgentSource,    // plugin | user | third-party
}
```

`source` is informational — used by the dropdown to optionally render a small badge per row. Source classification is a best-effort categorization done in `sdk-entry.js` based on the namespace prefix the SDK reports.

### `PlansChangedPayload` (new event)

```rust
pub struct PlansChangedPayload {
    pub kind: PlansChangedKind,  // created | modified | deleted
    pub name: Option<String>,    // None if the watcher couldn't determine a single file (e.g. dir rescanned)
}
```

React's `plansStore` simply re-fetches the full list on any of these — payload is informational.

### `ProjectFilesChangedPayload` (new event)

```rust
pub struct ProjectFilesChangedPayload {
    pub debounced: bool,  // true if at least one event was coalesced
}
```

React's `useProjectFiles()` re-fetches the index on this event. Debouncing happens Rust-side (notify-debouncer-mini at 250ms) to avoid thrashing during large operations like Library re-import.

## UX details

### Plans tab layout

```
┌─ Plans tab ──────────────────────────────────────────────────────────┐
│  ┌─ Plans (12) ─────────────┐  ┌─ Setup 2D roguelike scene ────────┐ │
│  │ + New plan               │  │ [Re-execute] [Delete] [View|Edit] │ │
│  ├──────────────────────────┤  ├────────────────────────────────────┤ │
│  │ ▶ Setup 2D roguelike    │  │                                    │ │
│  │   Scaffold the main...   │  │  ## Plan: Setup 2D roguelike      │ │
│  │   2h ago                 │  │                                    │ │
│  ├──────────────────────────┤  │  1. Create empty scene "MainGame" │ │
│  │   Refactor spawn system  │  │  2. Add 2D camera, ortho size 5   │ │
│  │   Replace static spawn... │  │  3. Create Player prefab from... │ │
│  │   yesterday              │  │  ...                               │ │
│  ├──────────────────────────┤  │                                    │ │
│  │   Add wave config        │  │                                    │ │
│  │   Generate JSON-driven   │  │                                    │ │
│  │   3 days ago             │  │                                    │ │
│  └──────────────────────────┘  └────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────┘
```

- Left column ~250px fixed, scrollable. Selected row highlighted.
- Right pane: header strip with action buttons; body fills the rest.
- View/Edit toggle is a segmented control or two-tab UI. View is default.
- Edit mode: monospace textarea, replaces View body. Adds Save (primary) and Cancel buttons in the header strip; replaces View|Edit toggle with Save|Cancel|View while editing.
- Empty state (no plans): centered hint text — *"No plans yet. After Claude generates a plan in plan mode, run `/save-plan` to capture it."*
- "+ New plan" button at the top of the list opens an empty editor with name input first.
- Delete button shows confirmation modal (reuses pattern from any existing modal in App~/src/components or adds DeleteConfirmModal as a new shared component).
- Re-execute button: sends the literal string `/plan-execute <name>` into the chat input + auto-submit, then auto-navigates to the Chat route.

### Slash dropdown behavior

| Event | Behavior |
|---|---|
| User types `/` at start of word (no preceding alphanumeric) | Dropdown opens with full command list |
| User types more after `/` | Dropdown filters by substring (case-insensitive) on `name` and `description` |
| User types space | Dropdown closes (assumed to be done with command name, typing args now) |
| User presses ArrowDown / ArrowUp | Move selection within visible filtered list |
| User presses Enter or Tab on selection | Insert `/<command-name> ` (with trailing space) replacing the typed prefix; close dropdown |
| User presses Esc | Close dropdown without insertion; original text retained |
| User clicks a row | Same as Enter |
| User clicks outside the dropdown | Close without insertion |
| Dropdown is open and supervisor emits new `CatalogReady` | Refresh list silently (rare; happens on resume) |

Anchor: positioned above the textarea by default, aligned to the cursor's horizontal position. If insufficient space above (top of viewport), flip below the textarea. Width: 320–480px depending on longest visible item (capped). Max height: ~280px with internal scroll if more items than fit.

Each row renders: `<icon> <name> <argument-hint?>` on the left, `<description>` truncated on the right, optional small `<source-badge>` at the far right (e.g. "user", "plugin"). Selected row is highlighted; hovered row gets a faint hover state.

Trigger detection logic:
- `/` is at index 0 of the textarea, OR preceded by whitespace (`/^\s/` to the left)
- The character to the left of `/` is not a letter, digit, or `/` itself
- Block triggers inside URLs by checking if the leftmost token contains `://`

### `@` unified picker behavior

Same interaction model as the slash dropdown, with these differences:

- Trigger: `@` at start of word (same word-boundary rules as `/`)
- Dropdown shows two sections: **Agents** (header, then rows) and **Files** (header, then rows). Agents section first.
- Filter applies to both sections simultaneously by substring (case-insensitive). Empty sections are hidden (no "no agents" placeholder if filter excludes all agents — just the Files section shows).
- Insertion produces `@agent-<name>` for agent rows and `@<relative-path>` for file rows. Both insert with a trailing space.
- File rows show `<icon-file-or-folder> <basename>` on the left and `<parent-path>` truncated on the right.
- Agent rows show `<icon-agent> <name>` on the left and `<description>` truncated on the right.

Initial sort: Agents alphabetical by name, Files alphabetical by relative path. (v2.1 may add MRU.)

### Skills behavior

**`/save-plan` (or `/save-plan <name>`)**

```
1. Parse argument from invocation. If none, ask the user via AskUserQuestion:
     "What should this plan be called?" (free-text)
2. Validate name: lowercase kebab-case, max 64 chars. Reject and ask again on
   invalid input.
3. Read the conversation context for the most recent plan content. The plan is
   typically the body of an ExitPlanMode tool call, OR the assistant's last
   long-form structured output. Format as the body of a markdown file.
4. Compose frontmatter: a single `description:` line summarizing the plan in
   ≤80 chars (Claude generates the description from the body).
5. Resolve target path: <UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/<name>.md
   - UNITY_PROJECT_PATH is the working directory of this Claude Code session.
6. If the file exists, auto-suffix: try <name>-2.md, <name>-3.md, ... up to -99.
   If still colliding, error with a clear message.
7. Write the file with the Write tool.
8. Confirm to the user: "Plan saved to ProjectSettings/GameDeck/plans/<name>.md"
   The Plans tab updates automatically (file watcher).
```

**`/plan-execute <name>`**

```
1. Require name argument. If missing, error with a list of available plans:
     "Usage: /plan-execute <name>. Available plans: ..."
   (Use Glob to enumerate ProjectSettings/GameDeck/plans/*.md.)
2. Read the file at <UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/<name>.md
   with the Read tool. If not found, error with the same available-plans hint.
3. Strip the frontmatter delimiters and announce: "Executing plan: <name>".
4. Begin executing the plan step-by-step. Treat the plan body as the user's
   working brief; check off steps as you complete them, ask AskUserQuestion if
   the plan needs information that isn't in it.
```

Both skills set `allowed-tools` minimally: `save-plan` needs `Read, Write, Glob`; `plan-execute` needs `Read, Glob` plus whatever the plan content requires (which the agent's main thread already has — no restriction needed inside the skill since the skill only loads the plan and continues; subsequent tool calls happen in the parent context).

## Wire protocol changes

**New `agent-message` variant** (Node → React via existing `agent-message` event):

```
{ "type": "catalog-ready",
  "commands": [ { "name": "...", "description": "...", "argumentHint": "...", "source": "..." }, ... ],
  "agents": [ { "name": "...", "description": "...", "source": "..." }, ... ] }
```

Emitted on every supervisor `system/init` reception. React's `catalogStore` replaces its cached state on receipt.

**New Tauri events** (Rust → React):

- `plans-changed` with `PlansChangedPayload` — fired by the plans watcher (debounced 250ms)
- `project-files-changed` with `ProjectFilesChangedPayload` — fired by the files watcher (debounced 250ms)

**New Tauri commands** (React → Rust):

- `list_project_files() -> Result<Vec<FileIndexEntry>, AppError>` — returns the cached file index. Triggers a rebuild if cache is empty or stale (>5 minutes since last refresh and no recent watcher event).

**Modified Tauri commands:**

- `list_plans() -> Vec<PlanMeta>` — real implementation
- `read_plan(name: String) -> Result<Plan, AppError>` — real implementation
- `write_plan(name: String, content: String) -> Result<(), AppError>` — real implementation, with collision auto-suffix only when invoked through a separate `create_plan` flow (the standard `write_plan` overwrites; collision suffix logic lives in the skill, see decision #6 nuance below)
- `delete_plan(name: String) -> Result<(), AppError>` — real implementation

**Note on collision suffix:** the skill's `/save-plan` flow handles auto-suffix internally before calling Write. The Tauri `write_plan` command does NOT auto-suffix — it overwrites. Reason: when the React Plans tab Save button writes, the user is editing an existing file; auto-suffix would create unintended copies. New plans created from the tab via "+ New plan" button must validate non-collision before calling `write_plan`; if collision, the React UI surfaces an error and asks for a different name. Auto-suffix lives in the skill (chat path), validation-and-reject lives in the tab (UI path).

## Definition of done

The following 17 scenarios pass on Windows 11 against a real Unity 6 project with MCP Game Deck installed:

1. Open Plans tab → empty state shows hint when no plans exist
2. Run a plan-mode conversation → invoke `/save-plan setup-2d-roguelike` after Claude generates a plan → file appears in `ProjectSettings/GameDeck/plans/setup-2d-roguelike.md` → Plans tab list refreshes within 500ms
3. Click `setup-2d-roguelike` in the list → right pane shows the plan content rendered as markdown (View tab is default)
4. Toggle to Edit → textarea is editable → modify a step → Save → mtime updates, list re-sorts, View tab now shows the edited content
5. Toggle to Edit → modify → Cancel → original content restored, no disk write
6. Click Delete → confirmation modal appears → confirm → file removed from disk → list updates → right pane goes to empty/no-selection state
7. Click Re-execute → chat input populated with `/plan-execute setup-2d-roguelike`, message auto-submits, navigation switches to Chat tab, Claude reads the plan and starts execution
8. Run `/save-plan` (no args) → AskUserQuestion card appears asking for a name → enter `my-test` → file written
9. Run `/save-plan my-test` again (same name) → file written as `my-test-2.md`; both visible in list
10. Type `/` in chat input → dropdown opens showing all commands: built-ins (`/clear`, `/help`, etc), `mcp-game-deck:save-plan`, `mcp-game-deck:plan-execute`, all 22 plugin skills, plus any user-installed commands/plugins
11. Type `/sa` → list filters to entries containing "sa": `mcp-game-deck:save-plan`, `mcp-game-deck:asset-audit`, etc
12. ArrowDown selects next, ArrowUp selects previous, Enter or Tab inserts `/<command-name> ` (replacing the typed prefix), Esc closes without insertion, click outside closes
13. Type `@` → unified dropdown opens with two sections: Agents (10 specialists like `mcp-game-deck:unity-specialist`) and Files (Unity project files)
14. Type `@unity` → both sections filter; Agents section shows `mcp-game-deck:unity-*` entries; Files section shows files containing "unity" in name or path
15. Type `@Assets/Scripts/` → Agents section empty (filter excludes), Files section shows files under that path; selection inserts `@Assets/Scripts/<file>` correctly
16. Edit a plan externally (VS Code) → save → Plans tab list and right pane refresh within 500ms (file watcher catches the change)
17. Re-launch the Tauri app (close + reopen via the F07 pin) → Plans tab loads existing plans correctly; chat dropdown still works after a fresh `system/init`

**Regression checks (must continue to pass):**

- F02: chat round-trip works, sessions list loads, permission mode toggle works
- F04: permission cards and AskUserQuestion cards appear correctly mid-conversation
- F07: pin status indicator reflects connection state; clicking pin opens / focuses Tauri app
- F02: `Cannot find module 'agent-sdk-stub.js'` error remains gone

## Edge cases

- **`UNITY_PROJECT_PATH` not set** — Plans tab shows an error state explaining that the env var is missing and that the user should launch the app via the Editor pin. Same for the `@` files picker (file index empty + hint).
- **Plans directory doesn't exist on first launch** — `list_plans` creates it (idempotent). Subsequent writes succeed.
- **Plan file with malformed YAML frontmatter** — `read_plan` falls back to empty `frontmatter` map; raw content is still readable. The Plan list shows the entry (filename + mtime, no description).
- **Plan file with no frontmatter at all (just markdown)** — accepted; `frontmatter` empty, `description` None.
- **File watcher fires on a file we wrote ourselves (echo)** — debouncer coalesces; React re-fetches once. No infinite loops.
- **`@` triggered inside a code block in the message** — still triggers the dropdown. (Distinguishing context inside textarea is impractical for v2.0; if user wants literal `@`, they can dismiss with Esc.)
- **Slash dropdown flickers on rapid typing** — debouncer on the filter (≤16ms / one frame) keeps it visually steady. State updates synchronous; only render is throttled.
- **User invokes `/save-plan` with no plan in conversation** — skill body has a fallback: if no recent plan-shaped content, ask the user "I don't see a recent plan in our conversation. What plan should I save?" via AskUserQuestion (free-text), then write that as the body.
- **Project file index is empty (project root unreadable)** — `list_project_files` returns `[]`; `@` dropdown shows only Agents section.
- **Plan name with edge-case characters in user input (`/save-plan My Plan!`)** — skill validates kebab-case; if user provides invalid name, skill asks again with the rule explained.

## Notes

- **No new dependencies if avoidable.** Verify before adding `notify`, `serde_yaml`, `walkdir`, `date-fns` whether they're already transitively in the dep tree from F02 work. The minimal-add philosophy is the right default.
- **The plans dir is the single source of truth.** Both the tab and the skills write to the same files; both rely on the file watcher to maintain consistency. No in-memory cache that survives a refresh — `list_plans` always re-reads.
- **The catalog cache is single-source-of-truth too.** `catalogStore` only updates on `CatalogReady` events; nothing else mutates it. This means the dropdown is always in sync with the supervisor's view of the world.
- **Don't reinvent the catalog.** Consume what the SDK gives. Source classification is a best-effort label; it's OK if some commands show up with `source: "unknown"`.
- **The Plans tab does not need to be ECS-fast.** Re-reading 50 plan files on every refresh is fine. Optimize only if reports surface.
- **Subagent invocation via `@agent-...` happens at the agent level inside Claude Code.** The dropdown only inserts the literal string. Claude Code parses it natively when the message is submitted; we don't intercept.
