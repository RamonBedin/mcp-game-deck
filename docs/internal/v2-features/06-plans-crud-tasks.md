# Feature 06 — Plans CRUD — Tasks

> **Companion:** `06-plans-crud.md` (design root) + `06-plans-crud-spec.md` (executable spec).
> **Branch:** `feature/06-plans-crud` — created from `develop/v2.0` after F04 merges.
> **Total:** 22 tasks across 7 groups.

## Status table

| Group | Task | Status | Commit |
|-------|------|--------|--------|
| 1 | 1.1 — `list_plans` real implementation | ⏳ pending | — |
| 1 | 1.2 — `read_plan` real implementation | ⏳ pending | — |
| 1 | 1.3 — `write_plan` real implementation | ⏳ pending | — |
| 1 | 1.4 — `delete_plan` real implementation | ⏳ pending | — |
| 1 | 1.5 — Plans dir file watcher → `plans-changed` event | ⏳ pending | — |
| 2 | 2.1 — `plansStore` + `plans-changed` subscription | ⏳ pending | — |
| 2 | 2.2 — `PlansList` component (left column) | ⏳ pending | — |
| 2 | 2.3 — `PlanPane` + `PlanViewer` + `PlanEditor` | ⏳ pending | — |
| 2 | 2.4 — Wire `PlansRoute` 2-col layout + actions | ⏳ pending | — |
| 3 | 3.1 — `Plugin~/skills/save-plan/SKILL.md` | ⏳ pending | — |
| 3 | 3.2 — `Plugin~/skills/plan-execute/SKILL.md` | ⏳ pending | — |
| 4 | 4.1 — `system/init` capture in `sdk-entry.js` → `CatalogReady` | ⏳ pending | — |
| 4 | 4.2 — React `catalogStore` + `useCommands` / `useAgents` hooks | ⏳ pending | — |
| 5 | 5.1 — `useSlashAutocomplete` hook | ⏳ pending | — |
| 5 | 5.2 — `SlashDropdown` component | ⏳ pending | — |
| 5 | 5.3 — Wire slash dropdown into ChatRoute input | ⏳ pending | — |
| 5 | 5.4 — Slash dropdown smoke validation | ⏳ pending | — |
| 6 | 6.1 — `list_project_files` Tauri command + files watcher | ⏳ pending | — |
| 6 | 6.2 — `useProjectFiles` + `useAtAutocomplete` hooks | ⏳ pending | — |
| 6 | 6.3 — `AtDropdown` unified component | ⏳ pending | — |
| 6 | 6.4 — Wire `@` picker into ChatRoute input + smoke | ⏳ pending | — |
| 7 | 7.1 — Final F06 smoke: 17 DoD scenarios + regression checks | ⏳ pending | — |

---

## Group 1 — Rust CRUD + file watcher

### Task 1.1 — Real `list_plans` implementation

**Size:** small
**Refs:** spec §"Data shapes", §"Wire protocol changes"; existing stubs in `App~/src-tauri/src/commands/plans.rs`.

**Output:**
- `App~/src-tauri/src/commands/plans.rs` — replace the `list_plans` stub with a real implementation that:
  - Reads `UNITY_PROJECT_PATH` from env (use the same resolution `claude_supervisor` uses; fall back to the `AppSettings.unity_project_path` if not set, or return `Vec::new()` if neither resolves)
  - Constructs path `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/`
  - Creates the directory if it doesn't exist (idempotent — log info on creation, suppress error if already exists)
  - Globs `*.md` in that directory (one level only, no recursion)
  - For each, reads the file, parses YAML frontmatter (use `serde_yaml`), extracts `description: Option<String>` if present
  - Builds a `PlanMeta { name (filename without `.md`), last_modified (mtime as unix seconds), description }`
  - Sorts by `last_modified` descending
  - Returns the Vec
- `App~/src-tauri/src/types.rs` — add `description: Option<String>` field to `PlanMeta`. Update existing serde derives.
- `App~/src-tauri/Cargo.toml` — add `serde_yaml = "0.9"` if not already present.
- `App~/src/ipc/types.ts` — mirror the new field on the TS-side `PlanMeta` shape.

**Validation:**
- With an empty plans dir, `invoke('list_plans')` returns `[]` and the dir gets created
- With three plan files (one with `description`, one without, one with malformed frontmatter), the call returns three entries sorted by mtime; description is populated where valid; the malformed one returns `description: None` without erroring
- `cargo build` clean, no new warnings
- `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(plans): real list_plans implementation (F06 task 1.1)

Replaces F01-era stub with a real list backed by the on-disk plans dir
under ProjectSettings/GameDeck/plans/. Parses YAML frontmatter to
surface description in PlanMeta, falls back to None on malformed YAML.
Creates the dir on first call (idempotent).

Refs: 06-plans-crud-tasks.md (task 1.1), 06-plans-crud-spec.md
```

---

### Task 1.2 — Real `read_plan` implementation

**Size:** small
**Refs:** spec §"Data shapes"; task 1.1.

**Output:**
- `App~/src-tauri/src/commands/plans.rs` — replace the `read_plan` stub:
  - Validates the `name` argument (kebab-case regex `^[a-z0-9][a-z0-9-]*$`, max 64 chars). On invalid, return `AppError::InvalidInput`.
  - Resolves `UNITY_PROJECT_PATH` (same as 1.1).
  - Constructs path `<root>/ProjectSettings/GameDeck/plans/<name>.md`.
  - On file-not-found, returns `AppError::FileNotFound`.
  - Reads file, splits frontmatter from body using a small helper (regex or hand-coded split on `^---\n.*?\n---\n`).
  - Parses frontmatter as `serde_yaml::Value`, converts to `serde_json::Map<String, Value>` (the `PlanFrontmatter` type alias). On malformed YAML, returns empty map (do not fail the read).
  - Returns `Plan { name, last_modified, content (body without delimiters), frontmatter }`.
- Helper function `parse_frontmatter(raw: &str) -> (PlanFrontmatter, String)` extracted (will be reused by 1.3).

**Validation:**
- Valid plan with frontmatter parses correctly
- Valid plan without frontmatter returns `frontmatter: {}` and full content
- Malformed YAML returns `frontmatter: {}` and the body after the `---` block (graceful)
- Non-existent name returns `AppError::FileNotFound`
- Invalid name (e.g. `"My Plan!"`) returns `AppError::InvalidInput`
- `cargo build`, `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(plans): real read_plan implementation (F06 task 1.2)

Reads <root>/ProjectSettings/GameDeck/plans/<name>.md, parses YAML
frontmatter into a free-form map, returns body separately. Falls back
to an empty frontmatter map on malformed YAML so editing in the tab
remains possible. Validates name format up front.

Refs: 06-plans-crud-tasks.md (task 1.2), 06-plans-crud-spec.md
```

---

### Task 1.3 — Real `write_plan` implementation

**Size:** small
**Refs:** spec §"Wire protocol changes" (note on collision suffix), task 1.2 (frontmatter helper).

**Output:**
- `App~/src-tauri/src/commands/plans.rs`:
  - `write_plan(name, content)` validates name, resolves path, writes content to disk atomically (write to `<name>.md.tmp`, rename to `<name>.md` — handles partial-write crashes).
  - **Overwrite semantics:** does NOT auto-suffix. Overwrites the existing file. Auto-suffix is the skill's job (chat path).
  - Creates the plans directory if missing (idempotent, mirror 1.1).
  - Returns `AppError::PermissionDenied` if filesystem rejects the write; `AppError::Internal` on unexpected IO error.
- A separate helper `find_available_name(base: &str) -> Option<String>` that returns the next free `<base>-N.md` slot up to N=99. **Not exposed as a Tauri command** — used internally if/when the React tab adds a "new plan" path that needs collision avoidance (it does — see task 2.4). Helper can be called from the same file.

**Validation:**
- Writing a new plan creates the file with content verbatim
- Writing the same name again overwrites the existing file (mtime updates, content replaces)
- Atomic rename verified by inspecting that no `<name>.md.tmp` files remain after a successful write
- Invalid name returns `InvalidInput`
- `find_available_name` returns the next free slot when 1, 2, 3 are taken; returns `None` after 99
- `cargo build` clean

**Commit message:**

```
feat(plans): real write_plan implementation with atomic write (F06 task 1.3)

write_plan overwrites by design (UI Save semantics). Auto-suffix lives
in the /save-plan skill, not here. Adds find_available_name helper for
the React tab's "+ New plan" collision check (task 2.4 will consume).
Atomic write via tmp-then-rename to handle partial-write crashes.

Refs: 06-plans-crud-tasks.md (task 1.3), 06-plans-crud-spec.md
```

---

### Task 1.4 — Real `delete_plan` implementation

**Size:** trivial
**Refs:** task 1.2 (path resolution).

**Output:**
- `App~/src-tauri/src/commands/plans.rs`: `delete_plan(name)` validates name, resolves path, calls `std::fs::remove_file`. Returns `FileNotFound` if missing, `PermissionDenied` on access denied, `Internal` on other IO errors.

**Validation:**
- Deleting an existing plan removes the file
- Deleting a missing plan returns `FileNotFound`
- Invalid name returns `InvalidInput`

**Commit message:**

```
feat(plans): real delete_plan implementation (F06 task 1.4)

Validates name, removes the file from disk, surfaces filesystem errors
as typed AppError variants for the React side.

Refs: 06-plans-crud-tasks.md (task 1.4), 06-plans-crud-spec.md
```

---

### Task 1.5 — Plans dir file watcher → `plans-changed` event

**Size:** medium
**Refs:** spec §"Architecture overview", §"Wire protocol changes"; the existing watcher patterns in F02 (if any). If no precedent, this is the first watcher in the codebase.

**Output:**
- `App~/src-tauri/Cargo.toml` — add `notify = "6"` and `notify-debouncer-mini = "0.4"` (verify versions current).
- `App~/src-tauri/src/plans_watcher.rs` — new module:
  - `start_plans_watcher(app_handle, project_root) -> JoinHandle` spawns a background task.
  - Watches `<project_root>/ProjectSettings/GameDeck/plans/` recursively=false.
  - Debounces events at 250ms.
  - On any event (create / modify / remove / rename), emits Tauri event `plans-changed` with payload `{ kind, name }` where `kind` is `"created" | "modified" | "deleted"` and `name` is best-effort filename without `.md` (None if can't determine).
  - Auto-recreates the watcher if the watched dir is removed and recreated.
  - Survives `UNITY_PROJECT_PATH` change: when the supervisor is reconfigured (rare; happens if user changes Unity project), the old watcher stops and a new one starts. v2.0: hook into the existing supervisor restart sequence.
- `App~/src-tauri/src/events.rs` — add `PlansChangedKind` enum + `PlansChangedPayload` struct + emit helper.
- `App~/src-tauri/src/types.rs` — mirror the payload shape.
- `App~/src-tauri/src/lib.rs` (or wherever the supervisor setup is) — start the watcher as part of the setup hook, store the JoinHandle on `AppState` so it can be cancelled on shutdown.
- `App~/src/ipc/types.ts` — add `PlansChangedPayload` and `PlansChangedKind` types.

**Validation:**
- Create a plan via `write_plan` (or directly on disk) → `plans-changed` event fires within ~500ms
- Modify the file → fires `kind: "modified"`
- Delete → fires `kind: "deleted"`
- Rapid sequential writes (5 in 100ms) coalesce into one event due to debouncer
- Supervisor restart cleans up the old watcher (no zombie thread)
- Tauri app close terminates the watcher cleanly (verify no zombie processes after Tauri exit)

**Commit message:**

```
feat(plans): file watcher emits plans-changed events (F06 task 1.5)

Adds notify-based watcher on <project>/ProjectSettings/GameDeck/plans/,
debounced at 250ms, emits typed PlansChangedPayload via Tauri event.
Lifecycle tied to the supervisor: starts on setup, dies on shutdown.
Auto-recreates if the watched dir is deleted.

Refs: 06-plans-crud-tasks.md (task 1.5), 06-plans-crud-spec.md
```

---

## Group 2 — React Plans tab

### Task 2.1 — `plansStore` + `plans-changed` subscription

**Size:** small
**Refs:** spec §"Data shapes"; task 1.1 (Tauri command shape); existing store patterns under `App~/src/stores/`.

**Output:**
- `App~/src/stores/plansStore.ts` — Zustand store (or whatever pattern existing stores use) with:
  - `plans: PlanMeta[]`
  - `selectedName: string | null`
  - `currentPlan: Plan | null`
  - `editMode: boolean`
  - `editDraft: string | null` (textarea content while editing, separate from `currentPlan.content`)
  - `actions`:
    - `loadList()` → calls `invoke('list_plans')`, updates `plans`
    - `selectPlan(name)` → calls `invoke('read_plan', {name})`, updates `currentPlan`, resets `editMode = false`
    - `enterEdit()` → sets `editMode = true`, copies `currentPlan.content` into `editDraft`
    - `cancelEdit()` → sets `editMode = false`, clears `editDraft`
    - `saveEdit()` → calls `invoke('write_plan', {name, content: editDraft})`, on success re-loads `currentPlan` and exits edit mode; on error, surfaces error state for UI to display
    - `deletePlan(name)` → calls `invoke('delete_plan', {name})`, on success clears `currentPlan` if it was the deleted one
    - `createNewPlan(name, content)` → calls `find_available_name` semantics via Rust (reusing `write_plan` after collision check the React side does — see task 2.4)
- `App~/src/hooks/usePlansSubscription.ts` (or inline in `App.tsx`) — registers a Tauri event listener for `plans-changed`, calls `plansStore.loadList()` on every event.
- Initial load: call `loadList()` once at app mount or when entering the Plans tab.

**Validation:**
- Tab loads list on mount
- Modifying a plan elsewhere fires `plans-changed` → list re-fetches automatically
- Selecting a plan loads its content
- `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(plans): plansStore with plans-changed subscription (F06 task 2.1)

Zustand store mirrors the on-disk plans dir; subscribes to the Tauri
plans-changed event for live refresh on external edits (skill writes,
VS Code edits, anything).

Refs: 06-plans-crud-tasks.md (task 2.1), 06-plans-crud-spec.md
```

---

### Task 2.2 — `PlansList` component

**Size:** small
**Refs:** spec §"UX details" → "Plans tab layout"; existing `SessionList.tsx` if present (mirror its visual conventions).

**Output:**
- `App~/src/components/PlansList.tsx`:
  - Props: `{ plans: PlanMeta[], selectedName: string | null, onSelect: (name: string) => void, onNewPlan: () => void }`
  - Layout: header strip with "+ New plan" button, list below.
  - Each row: name (bold), description (muted, truncated), relative mtime ("2h ago"). Selected row highlighted.
  - Empty state: centered hint text per spec.
  - Relative time: use `date-fns` `formatDistanceToNow` (verify if installed; if not, add to `App~/package.json`).
  - Width: ~250px fixed, full-height scrollable.
  - Use Tailwind classes consistent with existing components.

**Validation:**
- With 0/1/multiple plans, list renders correctly
- Empty state visible when `plans.length === 0`
- Click on a row triggers `onSelect`
- Selected row has visible highlight
- Long descriptions truncate with ellipsis
- Looks visually consistent with existing sidebar components

**Commit message:**

```
feat(plans): PlansList sidebar component (F06 task 2.2)

Left-column list of plans with name/description/relative-mtime per row,
empty state, "+ New plan" header button. Mirrors the visual conventions
of the existing session list.

Refs: 06-plans-crud-tasks.md (task 2.2), 06-plans-crud-spec.md
```

---

### Task 2.3 — `PlanPane` + `PlanViewer` + `PlanEditor`

**Size:** medium
**Refs:** spec §"UX details" → "Plans tab layout"; `react-markdown` already a dependency from F04.

**Output:**
- `App~/src/components/PlanViewer.tsx`:
  - Props: `{ plan: Plan }`
  - Renders `plan.content` via `react-markdown`. Use the same `components` overrides as F04's `RequestCard` if any (consistency).
- `App~/src/components/PlanEditor.tsx`:
  - Props: `{ value: string, onChange: (next: string) => void, onSave: () => void, onCancel: () => void }`
  - Monospace textarea, full-pane height. Uses Tailwind `font-mono`.
  - Keyboard: Cmd/Ctrl+S triggers `onSave`; Esc triggers `onCancel`.
- `App~/src/components/PlanPane.tsx`:
  - Props: `{ plan: Plan | null, editMode: boolean, editDraft: string | null, onEnterEdit, onCancelEdit, onSaveEdit, onChangeDraft, onDelete, onReExecute }`
  - When `plan === null`: shows empty/no-selection state ("Select a plan or create a new one").
  - When `plan !== null && !editMode`: header strip with `[Re-execute] [Delete] [View | Edit]`; body is `PlanViewer`.
  - When `plan !== null && editMode`: header strip with `[Save] [Cancel]`; body is `PlanEditor`.
  - Delete button opens a confirmation modal — reuse existing modal pattern or add a small inline confirmation; do NOT add a heavy modal lib.

**Validation:**
- View renders markdown correctly (headings, lists, code blocks)
- Edit textarea accepts input, fires `onChange`
- Cmd/Ctrl+S saves, Esc cancels
- Delete shows confirmation, only deletes on confirm
- Re-execute is wired to a callback (actual chat-send wiring is task 2.4)
- Empty state visible when no plan selected

**Commit message:**

```
feat(plans): PlanPane with viewer + editor + actions (F06 task 2.3)

Right-column pane combining markdown viewer (react-markdown), monospace
editor with Cmd+S/Esc shortcuts, action header (Re-execute / Delete /
View|Edit toggle), confirmation modal for Delete, empty state for no
selection. Re-execute callback hooked but not wired to chat yet.

Refs: 06-plans-crud-tasks.md (task 2.3), 06-plans-crud-spec.md
```

---

### Task 2.4 — Wire `PlansRoute` 2-col layout + Re-execute wiring

**Size:** medium
**Refs:** spec §"UX details" → "Plans tab layout"; existing chat send mechanism in `ChatRoute.tsx` and conversationStore.

**Output:**
- `App~/src/routes/PlansRoute.tsx` — replace placeholder:
  - 2-column flex layout: `PlansList` left, `PlanPane` right.
  - Wires `plansStore` actions to component props.
  - "+ New plan" flow: opens an inline form (or modal) with `name` text input (kebab-case validation), then a `Create` button that:
    - Validates name format
    - Calls `invoke('list_plans')` to check collision (or read the cached store state); on collision, surfaces an error inline ("A plan with this name already exists. Try a different name.")
    - On success, calls `invoke('write_plan', {name, content: '---\ndescription: \n---\n\n# New plan\n\n'})` (template starting content)
    - Selects the newly-created plan and enters Edit mode automatically
  - Re-execute wiring: `onReExecute` calls a helper that:
    - Switches the active route to `/chat` (using whatever router the app uses)
    - Calls `conversationStore.sendMessage(`/plan-execute ${name}`)` (or whatever the existing send API is)
- `App~/src/App.tsx` (or wherever the route mount lives) — ensure `PlansRoute` is mounted.

**Validation:**
- Plans tab opens, loads list, shows empty state on first run
- Selecting a plan shows its content
- Editing + Save persists and refreshes
- Editing + Cancel discards
- Delete + confirm removes the file
- Re-execute switches to Chat tab and submits `/plan-execute <name>`
- "+ New plan" creates with collision check; new plan appears in list and is auto-selected in Edit mode
- File watcher updates the list when an external write happens (e.g. invoke `write_plan` from another path)

**Commit message:**

```
feat(plans): wire PlansRoute with full CRUD + re-execute (F06 task 2.4)

Replaces placeholder with 2-col layout. Wires plansStore to the
List/Pane components. New plan flow with collision check. Re-execute
button switches to Chat and submits /plan-execute <name>. Plans tab is
now fully functional end-to-end on the React side.

Refs: 06-plans-crud-tasks.md (task 2.4), 06-plans-crud-spec.md
```

---

## Group 3 — Skills

### Task 3.1 — `Plugin~/skills/save-plan/SKILL.md`

**Size:** small
**Refs:** spec §"UX details" → "Skills behavior"; pattern reference `Plugin~/skills/create-command/SKILL.md`.

**Output:**
- `Plugin~/skills/save-plan/SKILL.md` with frontmatter:
  ```
  ---
  name: save-plan
  description: "Save the current plan from this conversation to ProjectSettings/GameDeck/plans/."
  argument-hint: "[plan-name]"
  user-invocable: true
  allowed-tools: Read, Write, Glob
  ---
  ```
- Body following the spec's "Skills behavior" pseudocode for `/save-plan`. Steps include:
  1. Parse argument from invocation; if missing, ask via `AskUserQuestion` ("What should this plan be called?", free-text)
  2. Validate name format; reject and re-ask on invalid
  3. Locate the most recent plan content in conversation. Strategy: look first for the most recent `ExitPlanMode` tool call; if none, look for the most recent assistant message with structured numbered steps; if neither, ask the user "I don't see a recent plan. What plan should I save?" via AskUserQuestion (free-text)
  4. Compose a one-line `description` (≤80 chars) summarizing the plan
  5. Resolve target path `<UNITY_PROJECT_PATH>/ProjectSettings/GameDeck/plans/<name>.md` — note that `UNITY_PROJECT_PATH` is the cwd of this Claude Code session, so a relative path `ProjectSettings/GameDeck/plans/<name>.md` works
  6. Auto-suffix loop using Glob to check existence: try `<name>.md`, `<name>-2.md`, ..., up to `-99.md`; pick the first free slot
  7. Write the file with body:
     ```
     ---
     description: <one-line summary>
     ---

     <plan body, untouched>
     ```
  8. Confirm to user with the actual filename used (handles auto-suffix transparency)

**Validation:**
- Manual: in a real Tauri chat, run plan mode, exit plan, run `/save-plan test`. File appears at `ProjectSettings/GameDeck/plans/test.md` with correct frontmatter and body. Plans tab list updates.
- Run `/save-plan test` again — file written as `test-2.md`; user notified of the suffix
- Run `/save-plan` (no arg) — AskUserQuestion card surfaces; provide name; flow completes
- Run `/save-plan Bad Name!` — skill rejects format and asks again

**Commit message:**

```
feat(plans): save-plan skill in Plugin~/skills/ (F06 task 3.1)

New plugin skill: captures the most recent plan in conversation and
writes it to ProjectSettings/GameDeck/plans/<name>.md. Optional
argument; falls back to AskUserQuestion. Auto-suffixes on name
collision. Generates a one-line description from the plan body.

Refs: 06-plans-crud-tasks.md (task 3.1), 06-plans-crud-spec.md
```

---

### Task 3.2 — `Plugin~/skills/plan-execute/SKILL.md`

**Size:** small
**Refs:** spec §"UX details" → "Skills behavior"; task 3.1.

**Output:**
- `Plugin~/skills/plan-execute/SKILL.md` with frontmatter:
  ```
  ---
  name: plan-execute
  description: "Load a saved plan from ProjectSettings/GameDeck/plans/ and execute it step-by-step."
  argument-hint: "<plan-name>"
  user-invocable: true
  allowed-tools: Read, Glob
  ---
  ```
- Body per spec pseudocode:
  1. Require name argument. If missing, run Glob on `ProjectSettings/GameDeck/plans/*.md`, list available plans, error message: "Usage: /plan-execute <name>. Available plans: ..."
  2. Read `ProjectSettings/GameDeck/plans/<name>.md` via Read tool
  3. If not found, same hint as above
  4. Strip frontmatter delimiters from the loaded content
  5. Announce: "Executing plan: **<name>**"
  6. Begin executing the plan body step-by-step. Treat it as the user's working brief. Use `AskUserQuestion` if the plan has gaps. Subsequent tool calls (Edit, Bash, MCP tools) happen in the parent agent context — `allowed-tools` here only restricts what the *skill itself* needs to load the plan

**Validation:**
- Manual: with a saved plan, run `/plan-execute setup-2d-roguelike` → Claude reads, announces, starts executing
- Run `/plan-execute nonexistent` → error with available-plans hint
- Run `/plan-execute` (no arg) → same error hint
- Re-execute button in Plans tab triggers same flow correctly

**Commit message:**

```
feat(plans): plan-execute skill in Plugin~/skills/ (F06 task 3.2)

New plugin skill: reads a saved plan and executes it step-by-step in
the parent agent context. Required name argument; helpful error with
available-plans listing on missing or unknown name. Powers both the
chat invocation and the Plans tab Re-execute button.

Refs: 06-plans-crud-tasks.md (task 3.2), 06-plans-crud-spec.md
```

---

## Group 4 — Catalog capture from `system/init`

### Task 4.1 — `system/init` capture in `sdk-entry.js` → `CatalogReady`

**Size:** medium
**Refs:** spec §"Wire protocol changes" → "New `agent-message` variant"; existing `sdk-entry.js` from F02 task 2.x; `types.rs` `AgentMessage` enum.

**Output:**
- `App~/runtime/sdk-entry.js`:
  - In the SDK message stream loop, recognize `system/init` events from the SDK. The exact event shape comes from the SDK's docs; expect a payload listing available commands, skills, and agents with their metadata.
  - Transform the payload into the `CatalogReady` agent-message shape: `{ type: 'catalog-ready', commands: [...], agents: [...] }` with each command/agent normalized to `{ name, description, argumentHint?, source }`.
  - Source classification heuristic:
    - If name starts with `mcp-game-deck:` → `source: 'plugin'`
    - If name has no namespace prefix → `source: 'built-in'` for known built-ins (`/clear`, `/help`, `/cost`, `/permissions`, etc), `source: 'user-command'` otherwise
    - If name has any other namespace prefix (e.g. `someplugin:foo`) → `source: 'third-party'`
  - Emit the JSON line via stdout (existing wire convention).
  - Cache the last `CatalogReady` payload in supervisor state; re-emit on resume / reconnection so React can rebuild after restart.
- `App~/src-tauri/src/types.rs`:
  - Add `CatalogReady { commands: Vec<CatalogCommand>, agents: Vec<CatalogAgent> }` variant to `AgentMessage`
  - Add `CatalogCommand { name, description, argument_hint, source }` and `CatalogAgent { name, description, source }` structs
  - Add `CommandSource` and `AgentSource` enums with kebab-case serde rename
- `App~/src/ipc/types.ts` — mirror all of the above on the TS side.

**Validation:**
- Launch supervisor; verify a single `catalog-ready` agent-message is emitted shortly after `Ready` (within ~2s)
- Payload contains both `commands` and `agents` arrays, each non-empty
- Restart supervisor → new `catalog-ready` emitted
- The 22 `Plugin~/skills/*` are all in the commands array with `source: 'plugin'`
- The 10 `Plugin~/agents/*` are all in the agents array with `source: 'plugin'`
- `cargo build`, `pnpm tsc --noEmit` clean
- Unit test on `types.rs`: serializing a `CatalogReady` produces `"type":"catalog-ready"` per the wire convention

**Commit message:**

```
feat(catalog): capture system/init from SDK as CatalogReady (F06 task 4.1)

sdk-entry.js subscribes to system/init events from the agent SDK,
normalizes the command/skill/agent catalog into a tagged AgentMessage
variant, emits over stdout. Source classification by namespace prefix.
Cached in supervisor for re-emit on reconnect.

Refs: 06-plans-crud-tasks.md (task 4.1), 06-plans-crud-spec.md
```

---

### Task 4.2 — React `catalogStore` + hooks

**Size:** small
**Refs:** task 4.1.

**Output:**
- `App~/src/stores/catalogStore.ts`:
  - State: `{ commands: CatalogCommand[], agents: CatalogAgent[], ready: boolean }`
  - Action `setCatalog(commands, agents)` replaces both arrays atomically and sets `ready = true`
  - Subscribe to `agent-message` Tauri event; on `CatalogReady`, call `setCatalog`. Wire this in the existing `agentMessageRouter` if present, or in a new module-level hook.
- `App~/src/hooks/useCommands.ts` — returns `commands` from `catalogStore`. Optional filter argument: `useCommands(filter?: (c: CatalogCommand) => boolean)`.
- `App~/src/hooks/useAgents.ts` — same shape for agents.

**Validation:**
- Launch app, verify catalog populates within ~2s of supervisor `Ready` event
- React DevTools shows `commands.length > 0` and `agents.length > 0`
- Manually trigger a supervisor restart → catalog clears and re-populates correctly
- `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(catalog): React catalogStore from CatalogReady events (F06 task 4.2)

Zustand store consumes the CatalogReady agent-message variant from the
supervisor. Exposes useCommands and useAgents hooks for the dropdown
work in groups 5 and 6. Cleared on supervisor restart, repopulated on
reconnect.

Refs: 06-plans-crud-tasks.md (task 4.2), 06-plans-crud-spec.md
```

---

## Group 5 — Slash dropdown

### Task 5.1 — `useSlashAutocomplete` hook

**Size:** medium
**Refs:** spec §"UX details" → "Slash dropdown behavior".

**Output:**
- `App~/src/hooks/useSlashAutocomplete.ts`:
  - Inputs: `{ value: string, cursorPosition: number, commands: CatalogCommand[] }`
  - Outputs: `{ active: boolean, query: string, candidates: CatalogCommand[], selectedIndex: number, range: [number, number] | null }` plus methods `next()`, `prev()`, `select(index?)`, `cancel()`, `applySelection(): { newValue: string, newCursor: number } | null`
  - Trigger detection logic per spec (slash at start of word, not preceded by alphanumeric, not inside a URL `://`).
  - Filter logic: substring case-insensitive on `name` and `description`. Sort: exact prefix matches first, then substring matches. Keep `mcp-game-deck:` prefix items above unprefixed user commands when score-equal (mild bias — tunable).
  - Reset state when `active` flips false.
- Unit tests in `App~/src/hooks/useSlashAutocomplete.test.ts` covering:
  - Trigger / no-trigger cases (cursor at start vs middle, after letter vs space)
  - Filter behavior with empty query, partial query, exact match
  - URL detection: `https://example.com/foo` does not trigger after the `/`
  - `applySelection` returns correct `newValue` (replaces from `range[0]` to `cursorPosition` with `/<command-name> `)

**Validation:**
- All unit tests pass
- `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(autocomplete): useSlashAutocomplete hook with state machine (F06 task 5.1)

Pure logic hook for / autocomplete: trigger detection (with URL guard),
substring filter, prefix-match priority, navigation methods, and
applySelection that returns the new textarea state. Covered by unit
tests.

Refs: 06-plans-crud-tasks.md (task 5.1), 06-plans-crud-spec.md
```

---

### Task 5.2 — `SlashDropdown` component

**Size:** medium
**Refs:** spec §"UX details" → "Slash dropdown behavior"; task 5.1.

**Output:**
- `App~/src/components/SlashDropdown.tsx`:
  - Props: `{ candidates: CatalogCommand[], selectedIndex: number, anchor: { top: number, left: number }, onSelect: (index: number) => void, onClose: () => void }`
  - Renders a portal-anchored panel above the textarea (or below if not enough space — measure on mount).
  - Each row: small icon (lucide-react `Command` or `Slash`), command name (with namespace prefix if any), argument-hint inline (muted), description on the right (truncated), small source badge at far right.
  - Selected row visually highlighted; hover styles for non-selected.
  - Click handler on each row → `onSelect(i)`.
  - Click outside (overlay or document listener) → `onClose`.
  - Width: clamps between 320 and 480px based on longest visible name; max-height 280px with internal scroll.
  - Tailwind for styling, consistent with existing components.

**Validation:**
- Storybook (if used) or a manual test harness: passes a list of 30 fake commands and verifies the panel renders, scrolls, highlights, etc
- Click outside fires `onClose`
- Click on row fires `onSelect`
- Long names truncate; long descriptions truncate
- No visual flicker on selection change

**Commit message:**

```
feat(autocomplete): SlashDropdown panel component (F06 task 5.2)

Portal-anchored dropdown with name + argument-hint + description per
row, selection highlight, hover state, click-to-select, click-outside
to close. Clamped width, scrollable max-height, source badges.

Refs: 06-plans-crud-tasks.md (task 5.2), 06-plans-crud-spec.md
```

---

### Task 5.3 — Wire slash dropdown into ChatRoute input

**Size:** medium
**Refs:** spec §"UX details" → "Slash dropdown behavior"; tasks 5.1, 5.2; existing chat input in `ChatRoute.tsx`.

**Output:**
- Refactor `ChatRoute.tsx` (or extract a `ChatInput.tsx`) to:
  - Track textarea `value` and `cursorPosition` (controlled).
  - Pull commands via `useCommands()`.
  - Drive `useSlashAutocomplete` with current value/cursor/commands.
  - Render `SlashDropdown` when `active === true`.
  - Compute anchor position from the cursor: use a hidden mirror div technique OR a simple "anchor below the textarea" approximation (v2.0 acceptable — refine in F09 if visually off).
  - Keyboard handlers on the textarea:
    - When dropdown is active: ArrowDown/Up navigate, Enter and Tab apply selection (preventDefault!), Esc closes
    - When dropdown is inactive: existing behaviors (Enter sends, Shift+Enter newline) unchanged
  - Apply selection: receives `{newValue, newCursor}` from the hook, updates state and refocuses textarea with the cursor at the new position.

**Validation:**
- Type `/` → dropdown opens within 1 frame
- Filter as you type
- ArrowDown / ArrowUp / Enter / Tab / Esc all work as spec'd
- Enter while dropdown is open does NOT submit the message; Enter while inactive still submits
- Click outside closes
- No regressions: send a normal message, send a multi-line message via Shift+Enter, etc

**Commit message:**

```
feat(autocomplete): wire slash dropdown into ChatRoute input (F06 task 5.3)

Integrates useSlashAutocomplete + SlashDropdown into the chat textarea.
Keyboard nav, click-outside, and selection insertion all wired. Enter
semantics correctly diverge based on dropdown active state.

Refs: 06-plans-crud-tasks.md (task 5.3), 06-plans-crud-spec.md
```

---

### Task 5.4 — Slash dropdown smoke validation

**Size:** small
**Refs:** spec §"Definition of done" scenarios 10–12.

**Output:**
- Manual smoke checklist in `.claude/reports/smoke/F06-slash-dropdown.md` with results.
- Address any issues found in tasks 5.1–5.3 in the same commit.

**Validation:** all of the following pass:
- Type `/` → dropdown shows full command list
- Type `/sa` → filters correctly
- Built-ins, plugin commands, and (if user has any) user commands all visible
- Arrow keys navigate, Enter inserts and closes, Tab inserts and closes, Esc closes
- Click outside closes
- Insertion produces `/<name> ` with trailing space
- URL test: `https://example.com/foo` does not trigger
- Mid-message test: typing ` /clear` mid-message triggers correctly (after space)
- No-trigger test: typing `a/b` (after letter) does not trigger
- Rapid typing does not cause flicker or double-render

**Commit message:**

```
docs(plans): F06 slash dropdown smoke validated (F06 task 5.4)

Manual smoke checklist run against tasks 5.1-5.3. All scenarios pass.
Edge cases (URLs, mid-message, no-trigger after letter) validated.

Refs: 06-plans-crud-tasks.md (task 5.4), 06-plans-crud-spec.md
```

---

## Group 6 — `@` unified picker

### Task 6.1 — `list_project_files` Tauri command + files watcher

**Size:** medium
**Refs:** spec §"Wire protocol changes" → "New Tauri commands"; task 1.5 (watcher pattern).

**Output:**
- `App~/src-tauri/src/commands/files.rs` (new):
  - `list_project_files() -> Result<Vec<FileIndexEntry>, AppError>`
  - Resolves `UNITY_PROJECT_PATH`. If absent, returns `Vec::new()`.
  - Uses `walkdir` (verify if already in deps; add if not) to walk the project root, filtering:
    - Excluded directories at any depth: `Library/`, `Temp/`, `obj/`, `Logs/`, `.vs/`, `.git/`, `node_modules/`, `dist/`, plus any directory whose name starts with `.` except `.claude/`
    - All file types included; no extension filtering
  - Returns `Vec<FileIndexEntry { path, kind }>` with relative paths (forward slashes), directories included so users can `@SomeFolder/`
  - Caches the result in `AppState` (with a timestamp); subsequent calls within 5 minutes use the cache. Cache invalidated by the watcher (see below).
- `App~/src-tauri/src/files_watcher.rs` (new):
  - Watches the project root recursively=true, debounced 250ms.
  - On any event, invalidates the file index cache and emits `project-files-changed` Tauri event with `ProjectFilesChangedPayload { debounced }`.
- `App~/src-tauri/src/types.rs` — add `FileIndexEntry`, `FileKind` enum, `ProjectFilesChangedPayload`.
- `App~/src-tauri/src/commands/mod.rs` — register `list_project_files`.
- `App~/src-tauri/src/lib.rs` — start the files watcher alongside the plans watcher.
- `App~/src/ipc/types.ts` — mirror types.

**Validation:**
- On a real Unity project with thousands of files: `list_project_files` returns within ~1s on first call (cold), <50ms on cache hit
- Excluded dirs (`Library/`, etc) are absent from results
- `.claude/` content IS included if the user has any
- Creating a file under `Assets/` triggers `project-files-changed` within 500ms
- Creating a file under `Library/` does NOT trigger (excluded from watch — or watched but discarded; OK either way as long as it doesn't pollute the index)
- Cache invalidates on watcher event; next `list_project_files` call rebuilds

**Commit message:**

```
feat(files): list_project_files Tauri command + files watcher (F06 task 6.1)

walkdir-based project file index with sensible exclusions for the @
picker. notify watcher invalidates cache and emits
project-files-changed for live React sync. Cached for 5 minutes
between rebuilds.

Refs: 06-plans-crud-tasks.md (task 6.1), 06-plans-crud-spec.md
```

---

### Task 6.2 — `useProjectFiles` + `useAtAutocomplete` hooks

**Size:** medium
**Refs:** spec §"UX details" → "@ unified picker behavior"; tasks 4.2, 5.1, 6.1.

**Output:**
- `App~/src/hooks/useProjectFiles.ts`:
  - Calls `invoke('list_project_files')` on mount
  - Subscribes to `project-files-changed` event; re-fetches on signal
  - Returns `{ files: FileIndexEntry[], loading: boolean, error?: AppError }`
- `App~/src/hooks/useAtAutocomplete.ts`:
  - Same input/output shape as `useSlashAutocomplete` but for `@`
  - Trigger: `@` at start of word (same word-boundary rules)
  - Combines `useAgents()` and `useProjectFiles()` into a unified candidate list with section markers (or two separate filtered arrays surfaced together — let the component decide rendering)
  - Filter: substring case-insensitive on agent `name`/`description` AND on file `path`
  - Sort: agents alphabetical, files alphabetical (per section)
  - `applySelection(candidate)` returns `newValue, newCursor`:
    - For an agent: inserts `@agent-<name> ` (with trailing space)
    - For a file: inserts `@<path> `
  - Same navigation semantics: `next`, `prev`, `cancel`
- Unit tests for `useAtAutocomplete` covering:
  - Trigger detection
  - Filter behavior across both sections
  - `applySelection` insertion shape for agents vs files

**Validation:**
- Unit tests pass
- `pnpm tsc --noEmit` clean

**Commit message:**

```
feat(autocomplete): useAtAutocomplete + useProjectFiles hooks (F06 task 6.2)

useProjectFiles wraps the Tauri command + watcher subscription.
useAtAutocomplete combines agents and files into a unified picker
state machine, with section-aware filtering and per-row insertion
semantics.

Refs: 06-plans-crud-tasks.md (task 6.2), 06-plans-crud-spec.md
```

---

### Task 6.3 — `AtDropdown` unified component

**Size:** medium
**Refs:** spec §"UX details" → "@ unified picker behavior"; task 5.2 (visual conventions to reuse).

**Output:**
- `App~/src/components/AtDropdown.tsx`:
  - Props mirror `SlashDropdown` but with section structure: `{ agents: CatalogAgent[], files: FileIndexEntry[], selectedIndex: number, anchor, onSelect, onClose }`
  - Renders two sections: "Agents" header + agent rows, "Files" header + file rows. Hide a section if its filtered list is empty.
  - Agent row: agent icon + `@agent-<name>` + description (truncated)
  - File row: file/folder icon (different per `kind`) + `<basename>` + `<parent-path>` (truncated)
  - Selection cycles across both sections (selectedIndex is a flat index into the concatenated list).
  - Visual treatment otherwise consistent with `SlashDropdown` for cohesion.

**Validation:**
- Renders with mixed agents+files
- Renders with agents-only filter
- Renders with files-only filter
- Sections show correct headers
- Selection navigates across sections seamlessly

**Commit message:**

```
feat(autocomplete): AtDropdown unified picker component (F06 task 6.3)

Two-section dropdown (Agents + Files) with shared filter, headers per
section, type-specific row rendering, flat selection across sections.
Visually consistent with SlashDropdown for cohesion.

Refs: 06-plans-crud-tasks.md (task 6.3), 06-plans-crud-spec.md
```

---

### Task 6.4 — Wire `@` picker into ChatRoute + smoke

**Size:** medium
**Refs:** task 5.3 (slash wiring pattern); tasks 6.1–6.3.

**Output:**
- Extend the chat input wiring from task 5.3:
  - Run `useAtAutocomplete` alongside `useSlashAutocomplete` on the same value/cursor
  - At any moment, at most one is active (the trigger characters are mutually exclusive in practice; if both somehow active, prefer `/`)
  - Render whichever dropdown is active
  - Keyboard handlers route to the active hook
  - Selection from `@` dropdown inserts `@agent-<name> ` or `@<path> ` accordingly
- Manual smoke checklist in `.claude/reports/smoke/F06-at-picker.md`:
  - Type `@` → dropdown shows agents + files
  - Type `@unity` → both filtered correctly
  - Type `@agent-` → only agents (files all filtered out by literal-substring on `path`)
  - Type `@Assets/Sc` → only files
  - Insertion: `@agent-<name>` for agent rows, `@<path>` for file rows (both with trailing space)
  - File index loads on app launch; refresh on external file create within ~500ms
  - Address any issues in 6.1–6.3 in the same commit.

**Validation:** all smoke scenarios pass. F06 DoD scenarios 13–15 covered here.

**Commit message:**

```
feat(autocomplete): wire @ picker into ChatRoute + smoke (F06 task 6.4)

Both / and @ dropdowns coexist on the same chat input; at most one
active at a time. Selection inserts the right shape per row type.
Smoke checklist run; F06 DoD scenarios 13-15 pass.

Refs: 06-plans-crud-tasks.md (task 6.4), 06-plans-crud-spec.md
```

---

## Group 7 — Final smoke

### Task 7.1 — F06 final smoke: 17 DoD scenarios + regression checks

**Size:** small
**Refs:** spec §"Definition of done".

**Output:**
- `.claude/reports/smoke/F06.md` with each of the 17 DoD scenarios checked off + the regression checks (F02 / F04 / F07).
- Any final fixes for issues caught here, in the same commit.

**Validation:** every scenario in the spec's "Definition of done" section passes against a real Unity 6 project on Windows 11. No regressions in F02 / F04 / F07 paths.

**Commit message:**

```
docs(plans): F06 final smoke validated — green (F06 task 7.1)

All 17 DoD scenarios from the spec pass. F02/F04/F07 regression checks
clean. Plans CRUD + slash dropdown + @ picker shipping with no known
defects.

Refs: 06-plans-crud-tasks.md (task 7.1), 06-plans-crud-spec.md
```

---

## Notes for execution

- **Branch:** `feature/06-plans-crud` from `develop/v2.0`. Create after F04 lands.
- **Commit cadence:** one commit per task. Use the suggested message; adjust the body if implementation details shifted from spec.
- **Validation discipline:** every task lists explicit validation steps. Run them before commit. If a step fails, fix in the same task — don't move on hoping the next task fixes it.
- **PR target:** `develop/v2.0`. After 7.1 done, open the PR with the same template style as Feature 02's PR.
- **No git operations from Claude Code** — Ramon owns git per CLAUDE.md.
- **C# coding standards** don't apply here — this feature is Rust + TypeScript + markdown skills. Apply the project's Rust conventions (`///` doc comments on public items, braces always, `cargo fmt` clean, no warnings) and TypeScript conventions (TSDoc-style on exported items, named exports preferred, no `any`).
- **Dependency hygiene:** verify before adding any new crate or npm package. `notify`, `notify-debouncer-mini`, `serde_yaml`, `walkdir`, `date-fns` may already be in transitive deps from F02 / F04 work — check `Cargo.lock` and `pnpm-lock.yaml` first.
- **Cross-platform:** v2.0 is Windows-validated. `notify` works on macOS/Linux out of the box; smoke comes when first non-Windows user reports.
- **Order:** Group 1 → 2 → 3 → 4 → 5 → 6 → 7 is the dependency-clean order. Within Group 1, tasks 1.1 / 1.2 / 1.3 / 1.4 / 1.5 can interleave freely; Group 2 depends on Group 1 fully landed; Group 4 must land before 5/6.
- **Skill iteration:** the two skills in Group 3 will likely need refinement after seeing them used. Treat 3.1 and 3.2 as "first cut" — fixes during Group 7 smoke are expected and welcome.
