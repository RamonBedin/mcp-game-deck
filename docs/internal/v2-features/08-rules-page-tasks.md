# Feature 08 — Rules Page — Tasks

> **Companion:** `08-rules-page.md` (design root) + `08-rules-page-spec.md` (executable spec).
> **Branch:** `feature/08-rules-page` — created from `develop/v2.0` after F06 merges.
> **Total:** 15 tasks across 5 groups.

## Status table

| Group | Task | Status | Commit |
|-------|------|--------|--------|
| 1 | 1.1 — Extract `markdown_doc` module + flip plans to ms | ✅ done | — |
| 2 | 2.1 — `list_rules` real implementation | ✅ done | — |
| 2 | 2.2 — `read_rule` real implementation | ✅ done | — |
| 2 | 2.3 — `write_rule` real implementation | ✅ done | — |
| 2 | 2.4 — `delete_rule` real implementation | ✅ done | — |
| 2 | 2.5 — `toggle_rule` surgical frontmatter mutation | ✅ done | — |
| 3 | 3.1 — `rules_bundle::recompose` composition | ✅ done | — |
| 3 | 3.2 — `rules_watcher` notify loop + emit | ✅ done | — |
| 3 | 3.3 — Spawn env var + sdk-entry.js consumer + startup recompose | ✅ done | — |
| 4 | 4.1 — `rulesStore` + `rules-changed` subscription | ⏳ pending | — |
| 4 | 4.2 — `RulesList` component (checkbox + token header) | ⏳ pending | — |
| 4 | 4.3 — `RulePane` + `RuleViewer` + `RuleEditor` + estimator | ⏳ pending | — |
| 4 | 4.4 — Wire `RulesRoute` 2-col + toggle + cap + new-rule flow | ⏳ pending | — |
| 5 | 5.1 — F08 final smoke: 17 DoD scenarios + regression checks | ⏳ pending | — |

---

## Group 1 — Shared `markdown_doc` module refactor

### Task 1.1 — Extract `markdown_doc.rs` + flip `last_modified` to ms

**Size:** medium
**Refs:** spec §"File layout", §"Data shapes" (note on ms units), §"Notes" (refactor rationale); existing `commands/plans.rs` helpers (`parse_frontmatter`, `validate_plan_name`, `ensure_plans_dir`, `next_available_suffix`, atomic tmp-rename in `write_plan`); F06 follow-up on `last_modified` units.

**Output:**
- `App~/src-tauri/src/markdown_doc.rs` — new module exporting:
  - `parse_frontmatter(raw: &str) -> (FrontmatterMap, String)` — verbatim move of the F06 helper. Type alias `pub type FrontmatterMap = serde_json::Map<String, serde_json::Value>` lives here.
  - `validate_kebab_name(name: &str) -> Result<(), AppError>` — generalized rename of `validate_plan_name`. Same regex (`^[a-z0-9][a-z0-9-]*$`, max 64 chars), generic error messages ("Name must be 1-64 characters." etc — drops the `plan` noun).
  - `ensure_dir(dir: &Path) -> std::io::Result<()>` — verbatim move of `ensure_plans_dir`.
  - `atomic_write(path: &Path, content: &[u8]) -> std::io::Result<()>` — pure helper for tmp-then-rename. Takes the final path; computes `<path>.tmp`; writes; renames; cleans up `.tmp` on rename failure. Used by `write_plan`, `write_rule`, `toggle_rule`, `rules_bundle::recompose`.
  - `read_metadata_ms(metadata: &std::fs::Metadata) -> i64` — returns mtime in **milliseconds** (using `Duration::as_millis() as i64`). Both `PlanMeta` and `RuleMeta` consume this.
  - `next_available_suffix(base: &str, exists: impl Fn(&str) -> bool) -> Option<String>` — verbatim move; stays generic (no `plans-` references).
- Move the existing F06 unit tests for these helpers from `commands/plans.rs::tests` into `markdown_doc.rs::tests`. Keep `commands/plans.rs::tests` only for tests that exercise plans-specific behavior (e.g. `read_plan_rejects_invalid_name` stays — it tests the cmd's surface, not the helper).
- `App~/src-tauri/src/commands/plans.rs` — refactor:
  - Remove the moved helper functions and tests.
  - Import from `crate::markdown_doc`.
  - Change `PlanMeta.last_modified` from `as_secs() as i64` to the new ms helper.
  - Verify `list_plans` and `read_plan` work end-to-end after the swap.
- `App~/src-tauri/src/types.rs` — update `PlanMeta` doc-comment to reflect ms. No struct changes needed (the field is already `i64`).
- `App~/src-tauri/src/lib.rs` — register the `markdown_doc` module.
- `App~/src-tauri/src/plans_watcher.rs` — no change required (`PlansWatcher` doesn't touch the time helper directly), but verify it still compiles.
- `App~/src/components/PlansList.tsx` — its `formatRelative` already multiplies by 1000 internally (per F06 follow-up note); remove that multiplication now that the input is already ms. Verify the relative-time strings still render correctly.

**Validation:**
- `cargo test -p game-deck-app --lib` — all moved tests pass under their new home.
- `cargo build --release` — clean, no warnings.
- `pnpm tsc --noEmit` — clean.
- F06 manual smoke: open the Plans tab, list loads, mtime strings still read correctly ("2h ago" etc), edit/save round-trip works.
- Confirm no `as_secs()` references remain in `plans.rs`.

**Commit message:**

```
refactor(markdown): extract markdown_doc shared module + ms units (F08 task 1.1)

Lifts parse_frontmatter, validate_kebab_name, ensure_dir, atomic_write,
read_metadata_ms, and next_available_suffix out of commands/plans.rs
into a new crate::markdown_doc module so F08 rules.rs can share them
without duplication. F06 plans.rs becomes a thin caller.

Also flips PlanMeta.last_modified from seconds to milliseconds (closing
the F06 follow-up). PlansList drops its compensating *1000 in
formatRelative; SessionList and the new RuleMeta both consume the same
unit out of the box.

Refs: 08-rules-page-tasks.md (task 1.1), 08-rules-page-spec.md
```

---

## Group 2 — Rust CRUD

### Task 2.1 — Real `list_rules` implementation

**Size:** small
**Refs:** spec §"Data shapes", §"Wire protocol changes"; existing stub in `App~/src-tauri/src/commands/rules.rs`; task 1.1 (shared helpers).

**Output:**
- `App~/src-tauri/src/commands/rules.rs` — replace the `list_rules` stub:
  - Reads `UNITY_PROJECT_PATH` via `crate::project_root::try_resolve_project_root` (same path resolution as plans).
  - Constructs `<project>/ProjectSettings/GameDeck/rules/` via a local `rules_dir()` helper (mirror `plans_dir()`).
  - Calls `markdown_doc::ensure_dir` (idempotent).
  - Globs `*.md` one level (no recursion).
  - For each, reads the file, parses frontmatter via `markdown_doc::parse_frontmatter`, extracts:
    - `enabled: bool` — defaults to `false` on missing/malformed (conservative, can't auto-inject).
    - `description: Option<String>` — same semantics as plans.
    - `applies_to: Vec<String>` — parses YAML sequence; falls back to `[]` if absent or wrong type.
    - `estimated_tokens: u32` — computed from the **full file content** (frontmatter + body) using `(raw.chars().count() as u32 + 3) / 4` (rounded up).
  - Builds `RuleMeta { name, last_modified (ms), enabled, description, applies_to, estimated_tokens }`.
  - Sorts by `last_modified` descending.
- `App~/src-tauri/src/types.rs` — expand `RuleMeta`:
  ```rust
  pub struct RuleMeta {
      pub name: String,
      pub last_modified: i64,         // ms
      pub enabled: bool,
      pub description: Option<String>,
      pub applies_to: Vec<String>,
      pub estimated_tokens: u32,
  }
  ```
- `App~/src/ipc/types.ts` — mirror the new shape.

**Validation:**
- Empty rules dir → returns `[]`, creates dir on the way.
- With 3 rule files (one enabled with `description` + `applies-to: [ui]`, one disabled with no description, one with malformed YAML) the call returns 3 entries; malformed surfaces with `enabled: false`, empty `description`, empty `applies_to`.
- Manual: drop a rule with `applies-to: ui` (scalar, not array) — falls back to `applies_to: []` (no crash).
- `cargo build` clean.
- `pnpm tsc --noEmit` clean.

**Commit message:**

```
feat(rules): real list_rules implementation (F08 task 2.1)

Replaces the F01-era stub with a real list backed by the on-disk
rules dir under ProjectSettings/GameDeck/rules/. Parses YAML
frontmatter into RuleMeta (enabled, description, applies-to,
estimated_tokens). Conservative defaults: missing/malformed enabled
falls to false so a broken rule can never accidentally activate.

Refs: 08-rules-page-tasks.md (task 2.1), 08-rules-page-spec.md
```

---

### Task 2.2 — Real `read_rule` implementation

**Size:** small
**Refs:** spec §"Data shapes"; task 1.1 (shared helpers); task 2.1.

**Output:**
- `App~/src-tauri/src/commands/rules.rs` — replace the `read_rule` stub:
  - `validate_kebab_name(&name)` upfront. On invalid → `AppError::InvalidInput`.
  - `rules_dir()` resolution; if absent → `AppError::FileNotFound`.
  - Constructs path `<dir>/<name>.md`; reads via `fs::read_to_string`. Maps IO errors per the plans pattern (`NotFound → FileNotFound`, `PermissionDenied → PermissionDenied`, else → `Internal`).
  - Extracts `last_modified` via `markdown_doc::read_metadata_ms`.
  - Splits frontmatter via `markdown_doc::parse_frontmatter`.
  - Computes `estimated_tokens` from the full raw string (same formula as `list_rules`).
  - Returns `Rule { name, last_modified, content (body without delimiters), frontmatter, estimated_tokens }`.
- `App~/src-tauri/src/types.rs` — expand `Rule`:
  ```rust
  pub struct Rule {
      pub name: String,
      pub last_modified: i64,
      pub content: String,
      pub frontmatter: RuleFrontmatter,
      pub estimated_tokens: u32,
  }
  pub type RuleFrontmatter = serde_json::Map<String, serde_json::Value>;
  ```
  - Remove the old fields (`enabled`, single `content`) from the stub.
- `App~/src/ipc/types.ts` — mirror.

**Validation:**
- Valid rule with full frontmatter (enabled, description, applies-to) parses correctly; frontmatter map carries all three keys.
- Valid rule with no frontmatter returns `frontmatter: {}` and full content as body.
- Malformed YAML returns `frontmatter: {}` and body still readable.
- Non-existent name → `AppError::FileNotFound`.
- Invalid name (`"My Rule!"`) → `AppError::InvalidInput`.
- `estimated_tokens` matches `chars/4` round-up for a known-size test file.
- `cargo build`, `pnpm tsc --noEmit` clean.

**Commit message:**

```
feat(rules): real read_rule implementation (F08 task 2.2)

Reads <root>/ProjectSettings/GameDeck/rules/<name>.md, splits YAML
frontmatter from body, exposes the full frontmatter map plus
estimated_tokens (chars/4 heuristic) for the UI to render. Mirrors
read_plan's error mapping and graceful YAML fallback.

Refs: 08-rules-page-tasks.md (task 2.2), 08-rules-page-spec.md
```

---

### Task 2.3 — Real `write_rule` implementation

**Size:** small
**Refs:** spec §"Wire protocol changes"; task 1.1; task 2.2.

**Output:**
- `App~/src-tauri/src/commands/rules.rs` — replace the `write_rule` stub:
  - Validates name, resolves dir, `markdown_doc::ensure_dir`.
  - Atomic write via `markdown_doc::atomic_write` to `<dir>/<name>.md`.
  - Overwrite semantics: existing file is replaced (UI's "+ New rule" flow validates non-collision client-side before calling).
  - Error mapping mirrors `write_plan`.

**Validation:**
- Writing a new rule creates the file with content verbatim.
- Writing the same name overwrites, mtime updates.
- No `.md.tmp` leftover after success.
- Invalid name → `InvalidInput`.
- `cargo build` clean.

**Commit message:**

```
feat(rules): real write_rule implementation (F08 task 2.3)

Atomic write through markdown_doc::atomic_write. Overwrite semantics
match write_plan: collision detection is the UI's job, not the
command's. Used both by the React "+ New rule" flow and by 2.5's
toggle_rule (which writes back surgically-modified content).

Refs: 08-rules-page-tasks.md (task 2.3), 08-rules-page-spec.md
```

---

### Task 2.4 — Real `delete_rule` implementation

**Size:** trivial
**Refs:** task 2.2 (path resolution).

**Output:**
- `App~/src-tauri/src/commands/rules.rs` — replace the `delete_rule` stub:
  - Validates name, resolves dir, `fs::remove_file`.
  - Error mapping mirrors `delete_plan`.

**Validation:**
- Existing rule removed.
- Missing rule → `FileNotFound`.
- Invalid name → `InvalidInput`.

**Commit message:**

```
feat(rules): real delete_rule implementation (F08 task 2.4)

fs::remove_file on the resolved path with typed error mapping. Same
shape as delete_plan; nothing rule-specific beyond the path.

Refs: 08-rules-page-tasks.md (task 2.4), 08-rules-page-spec.md
```

---

### Task 2.5 — Surgical `toggle_rule` frontmatter mutation

**Size:** medium
**Refs:** spec §"Wire protocol changes" → `toggle_rule`, §"Notes" (toggle is surgical, not full rewrite); spec §"Edge cases" (cap enforcement); task 1.1.

**Output:**
- `App~/src-tauri/src/commands/rules.rs` — replace the `toggle_rule` stub:
  - Validates name, resolves path.
  - **Cap enforcement on enable:** if `enabled == true`, count currently-enabled rules in the dir; if already at 10, return `AppError::InvalidInput("Rule cap reached (10 enabled). Disable one first.")`. Skip the count when `enabled == false` (disabling is always allowed).
  - Reads the file raw.
  - Parses frontmatter as `serde_yaml::Value`. If the value is not a mapping (e.g. `null`, sequence, scalar), return `AppError::Internal("Cannot toggle a rule with non-mapping frontmatter; edit the file directly.")` rather than silently rewriting (preserves user intent).
  - Mutates only the `enabled` key in the mapping; preserves all other keys and their ordering.
  - Re-serializes the frontmatter back to YAML.
  - Reconstructs the file: `---\n<new-yaml>---\n<body>`.
  - Atomic write back via `markdown_doc::atomic_write`.
- The "preserve key ordering" requirement: `serde_yaml::Value::Mapping` preserves insertion order; using `Value` (not `serde_json::Map`) for the round-trip keeps user-authored frontmatter visually stable across toggles. Document this choice in a comment.

**Validation:**
- Toggle a rule with `enabled: false` → file's first line block contains `enabled: true`; body unchanged byte-for-byte.
- Frontmatter with multiple keys (description, applies-to, custom-field) → all keys preserved in original order; only `enabled` flipped.
- Toggle on when 10 already enabled → `InvalidInput` error returned; no file write happens.
- Toggle off when 10 enabled → succeeds (count check only on enable).
- Rule with no frontmatter (`enabled` was never set) → toggle ON injects `---\nenabled: true\n---\n` at the top, preserves body. Effectively converts a frontmatter-less rule into a real rule. Document as accepted behavior.
- Rule with non-mapping frontmatter (e.g. YAML sequence) → `AppError::Internal` clear message; user fixes the file manually.
- `cargo test`, `cargo build` clean.

**Commit message:**

```
feat(rules): surgical toggle_rule preserves body + ordering (F08 task 2.5)

Round-trips frontmatter through serde_yaml::Value::Mapping so user-
authored key ordering survives toggles. Cap enforcement (10 enabled
max) on enable; disable is always allowed. Rejects non-mapping
frontmatter with a clear error rather than silently rewriting it.

Refs: 08-rules-page-tasks.md (task 2.5), 08-rules-page-spec.md
```

---

## Group 3 — Rules watcher + bundle compiler

### Task 3.1 — `rules_bundle::recompose` composition logic

**Size:** medium
**Refs:** spec §"Bundle composition algorithm", §"Architecture overview"; task 1.1 (atomic_write, parse_frontmatter); task 2.1 (rules_dir resolution).

**Output:**
- `App~/src-tauri/src/rules_bundle.rs` — new module:
  - `pub fn recompose(project_root: &Path) -> std::io::Result<()>`:
    1. Resolves `<project_root>/ProjectSettings/GameDeck/rules/`. If absent OR unreadable → treat as zero enabled → goto step 6.
    2. Reads all `*.md` in the dir. For each, parses frontmatter via `markdown_doc::parse_frontmatter`; filters where `enabled == Some(Value::Bool(true))`.
    3. Sorts surviving files by filename stem alphabetically (lowercase compare).
    4. Composes the bundle string per spec format (`## Project Rules` heading, intro paragraph, `### <name>\n\n<body>\n\n---\n\n` per rule, no trailing separator after the last rule).
    5. Ensures `<project_root>/Library/MCPGameDeck/` exists.
    6. If the filtered list is empty: `fs::remove_file(<project>/Library/MCPGameDeck/rules-bundle.md)`, ignoring `NotFound`. Return `Ok(())`.
    7. Otherwise: atomic_write the composed bundle to `<project>/Library/MCPGameDeck/rules-bundle.md`.
  - `pub fn bundle_path(project_root: &Path) -> PathBuf` — returns the bundle path; used by spawn.rs to set the env var.
- `App~/src-tauri/src/lib.rs` — register the module.
- Unit tests in `rules_bundle.rs::tests` (filesystem-free via injected helpers OR using `tempfile` crate if already in dev-dependencies; otherwise plain `std::env::temp_dir` + cleanup):
  - Empty input → returns Ok, no file written.
  - Single enabled rule → bundle contains heading + intro + `### name` + body + no trailing separator.
  - Multiple enabled rules → alphabetical order, `---` separators between (not after last).
  - Disabled rules ignored.
  - Malformed frontmatter on a file → treated as disabled (defensive default).

**Validation:**
- Unit tests pass.
- Manual: drop 2 enabled + 1 disabled rule files into a test project's rules dir; call `recompose`; inspect `Library/MCPGameDeck/rules-bundle.md` matches spec format byte-for-byte.
- Disable both rules; `recompose`; verify file is deleted (not zero-byte).
- `cargo build`, `cargo test` clean.

**Commit message:**

```
feat(rules): rules_bundle::recompose composer (F08 task 3.1)

Reads all rules, filters enabled, sorts alphabetically, composes the
appendSystemPromptFile-ready bundle text, atomic-writes to
Library/MCPGameDeck/rules-bundle.md. Deletes the bundle file when
zero rules are enabled (avoids feeding the SDK a zero-byte file).

Refs: 08-rules-page-tasks.md (task 3.1), 08-rules-page-spec.md
```

---

### Task 3.2 — `rules_watcher` notify loop + emit

**Size:** medium
**Refs:** spec §"Architecture overview", §"Wire protocol changes"; existing `plans_watcher.rs` (template); task 3.1.

**Output:**
- `App~/src-tauri/src/rules_watcher.rs` — new module, structurally a copy of `plans_watcher.rs` with two substitutions:
  - Watches `<project>/ProjectSettings/GameDeck/rules/` instead of plans dir.
  - On every classified event (after the `.md` filter and `classify_event` call): **first** call `rules_bundle::recompose(&project_root)` (log + continue on Err — bundle composition failure is not fatal to the watcher); **then** call `emit_rules_changed(app, RulesChangedPayload { kind, name })`.
- `App~/src-tauri/src/events.rs` — add:
  - `pub const EVT_RULES_CHANGED: &str = "rules-changed";`
  - `pub fn emit_rules_changed(app: &AppHandle, payload: RulesChangedPayload) -> tauri::Result<()>`.
- `App~/src-tauri/src/types.rs` — add:
  ```rust
  pub struct RulesChangedPayload { pub kind: RulesChangedKind, pub name: Option<String> }
  pub enum RulesChangedKind { Created, Modified, Deleted }
  ```
  with kebab-case serde rename matching plans.
- `App~/src/ipc/types.ts` — mirror.
- Unit tests for `classify_event` (verbatim copy of the plans pattern, scoped to RulesChangedKind).

**Validation:**
- Create a rule via `write_rule` (or `echo` directly into the dir) → `rules-changed` event fires within ~500ms AND `Library/MCPGameDeck/rules-bundle.md` reflects the change (created if rule enabled, unchanged if disabled, deleted if last enabled rule was removed).
- Modify enabled state via direct file edit → bundle recomposes, event fires.
- Delete a rule file → `kind: Deleted`, bundle recomposes.
- Rapid sequential writes coalesce into one event (250ms debouncer).
- Watcher restart on `restart_supervisor` cleans up old watcher (no zombie thread; mirror plans watcher lifecycle).
- `cargo build`, `cargo test` clean.

**Commit message:**

```
feat(rules): rules_watcher with bundle recompose on every event (F08 task 3.2)

Notify-based watcher on <project>/ProjectSettings/GameDeck/rules/,
debounced at 250ms. Every event: recompose the bundle, then emit
typed RulesChangedPayload via Tauri event. Lifecycle ties into the
supervisor restart sequence like the plans watcher.

Refs: 08-rules-page-tasks.md (task 3.2), 08-rules-page-spec.md
```

---

### Task 3.3 — Spawn env var + sdk-entry.js consumer + startup recompose

**Size:** medium
**Refs:** spec §"Wire protocol changes" → "Env var contract", §"sdk-entry.js integration"; spec §"Notes" (recompose called from three places); existing `spawn.rs` env var setup pattern (`MCP_GAME_DECK_PLUGIN_DIR`, `MCP_GAME_DECK_COMMANDS_DIR`); existing `sdk_entry.js` `handleInput` `queryOptions` construction.

**Output:**
- `App~/src-tauri/src/claude_supervisor/spawn.rs`:
  - When building the spawn command, add `MCP_GAME_DECK_RULES_BUNDLE_PATH=<bundle_path>` where `bundle_path = rules_bundle::bundle_path(&project_root).to_string_lossy()`. Unconditional set; the file at that path may or may not exist (JS handles both).
  - Set alongside the existing `MCP_GAME_DECK_PLUGIN_DIR` / `MCP_GAME_DECK_COMMANDS_DIR` block, same style.
- `App~/src-tauri/src/claude_supervisor/sdk_entry.js`:
  - In `handleInput`'s `queryOptions` block (just after `additionalDirectories` line and before `canUseTool`), insert:
    ```javascript
    const bundlePath = process.env.MCP_GAME_DECK_RULES_BUNDLE_PATH;
    if (bundlePath && bundlePath.length > 0)
    {
      try
      {
        const stats = fsSync.statSync(bundlePath);
        if (stats.isFile() && stats.size > 0)
        {
          queryOptions.appendSystemPromptFile = bundlePath;
        }
      }
      catch (err)
      {
        // ENOENT or read error: omit; bundle absent means zero
        // enabled rules, which is fine.
      }
    }
    ```
  - Add `import fsSync from "node:fs";` at the top alongside the existing `fsp` import.
  - Apply the same check inside `runHealthCheck`'s `query()` options block — health probe also exercises the rule injection path so smoke catches misconfiguration.
  - Add a debug line: `debug("rules bundle:", queryOptions.appendSystemPromptFile ?? "(none)")` inside `handleInput` after the check, so stderr shows the bundle state per turn. Useful for the smoke validation in task 5.1.
  - Remove the existing TODO comment block in `handleInput` that referenced this work (`// NOTE: when F08 (Rules Page) starts injecting system prompts, ...`).
- `App~/src-tauri/src/lib.rs`:
  - In the setup hook (alongside the plans watcher spawn), add: `rules_bundle::recompose(&project_root)` on startup, ignoring `Err` (log only). This ensures the bundle is fresh on first `query()` even if the watcher hasn't fired yet.
  - Spawn the rules watcher (mirror plans watcher spawn).
- `App~/src-tauri/src/commands/connection.rs` (or wherever `restart_supervisor` lives):
  - After project-root change, before spawning the new supervisor, call `rules_bundle::recompose(&new_project_root)`. Same idempotent-on-Err pattern.

**Validation:**
- Launch the Tauri app on a project with one enabled rule → `Library/MCPGameDeck/rules-bundle.md` exists; supervisor stderr log shows `rules bundle: <path>` on the first turn.
- Send any chat message → SDK round-trip succeeds; Claude can reference the rule content if asked.
- Disable the rule via UI toggle → bundle file deleted within ~500ms; next turn's stderr log shows `rules bundle: (none)`; SDK round-trip omits the file.
- Stop/restart the supervisor → recompose runs on startup; bundle reflects current state.
- Manual: delete the bundle file externally between turns → next turn's check fails gracefully (no crash, just omits); next rules-changed event recomposes.
- `cargo build` clean.

**Commit message:**

```
feat(rules): wire bundle env var + sdk-entry.js consumer (F08 task 3.3)

spawn.rs sets MCP_GAME_DECK_RULES_BUNDLE_PATH unconditionally;
sdk-entry.js checks file existence and size on every query() before
adding appendSystemPromptFile. lib.rs recomposes on startup so the
bundle is fresh before the first turn; restart_supervisor recomposes
on project switch. The 32KB Windows CreateProcess ceiling is sidestepped
by feeding the SDK a file path, not an inline string.

Removes the F08-pending TODO comment block from handleInput.

Refs: 08-rules-page-tasks.md (task 3.3), 08-rules-page-spec.md
```

---

## Group 4 — React Rules tab

### Task 4.1 — `rulesStore` + `rules-changed` subscription

**Size:** small
**Refs:** spec §"Data shapes"; existing `plansStore.ts` (template); task 3.2 (event shape).

**Output:**
- `App~/src/stores/rulesStore.ts` — Zustand store mirroring `plansStore` shape:
  - `rules: RuleMeta[]`
  - `selectedName: string | null`
  - `currentRule: Rule | null`
  - `editMode: boolean`
  - `editDraft: string | null`
  - Actions:
    - `loadList()` → `invoke('list_rules')`, updates `rules`.
    - `selectRule(name)` → `invoke('read_rule', {name})`, updates `currentRule`, exits edit.
    - `enterEdit()` / `cancelEdit()` / `setEditDraft(text)` — same shape as plans store.
    - `saveEdit()` → `invoke('write_rule', {name, content: editDraft})`; on success, refreshes `currentRule` via `selectRule(name)`, exits edit.
    - `deleteRule(name)` → `invoke('delete_rule', {name})`; clears `currentRule` if it was the deleted one.
    - `toggleRule(name, enabled)` → `invoke('toggle_rule', {name, enabled})`; on `InvalidInput` (cap reached or non-mapping frontmatter), the action returns `Promise<{ ok: false, error: string }>` so the UI can surface a toast; on success returns `{ ok: true }`. List refresh happens via the watcher event, not the action's return.
    - `createNewRule(name, content)` → wraps `invoke('write_rule', ...)` after a client-side collision check against the cached `rules` list.
- `App~/src/hooks/useRulesSubscription.ts` — registers a Tauri listener for `rules-changed`, calls `rulesStore.loadList()` on every event. Wire alongside `usePlansSubscription` in `App.tsx`.
- Initial load: `loadList()` invoked on `App` mount or on first navigation to the Rules tab.

**Validation:**
- Tab loads list on mount.
- External edit fires `rules-changed` → list re-fetches.
- Toggle action returns shaped result; the UI consumer can distinguish success vs cap-reached.
- `pnpm tsc --noEmit` clean.

**Commit message:**

```
feat(rules): rulesStore with rules-changed subscription (F08 task 4.1)

Zustand store mirrors the on-disk rules dir; subscribes to the Tauri
rules-changed event for live refresh. toggleRule returns a shaped
{ok, error?} result so the UI surfaces the cap-reached toast without
parsing errors.

Refs: 08-rules-page-tasks.md (task 4.1), 08-rules-page-spec.md
```

---

### Task 4.2 — `RulesList` component (checkbox + token header)

**Size:** medium
**Refs:** spec §"UX details" → "Rules tab layout"; existing `PlansList.tsx` (template).

**Output:**
- `App~/src/components/RulesList.tsx`:
  - Props: `{ rules: RuleMeta[], selectedName: string | null, onSelect, onNewRule, onToggle: (name: string, next: boolean) => Promise<{ok: boolean, error?: string}> }`.
  - Header: `"Rules (<enabled-count>/10 enabled, ~<total-tokens> tokens)"` — counts and token sum computed from `rules` array.
  - "+ New rule" button below the header.
  - Each row:
    - Checkbox glyph (`☑` enabled, `☐` disabled) — clickable, fires `onToggle`. Optimistic update: set local state immediately, reconcile on watcher event. Disabled-style for `enabled: false` rules (slightly muted text).
    - Rule name (bold).
    - Description (muted, truncated to one line).
    - Footer line: `~N tokens · <relative-mtime>` plus `· disabled` suffix when applicable.
    - Yellow warning glyph next to token count if `estimated_tokens > 500`.
  - Empty state: per spec.
  - Toast surfacing for `onToggle` error: when `onToggle` resolves with `{ok: false}`, show an inline toast at the bottom of the list (3s auto-dismiss). Reuse F06's toast utility if available; otherwise a small local component.
  - Width ~250px, scrollable, Tailwind consistent with PlansList.

**Validation:**
- 0/few/many rules — all render correctly.
- Checkbox click → optimistic flip → watcher event reconciles within ~500ms.
- Cap behavior: with 10 enabled, clicking ☐ on the 11th shows the toast, rule stays disabled.
- Token count in header matches sum of enabled rules' `estimated_tokens`.
- Warning glyph appears on a rule with body > 2000 chars.

**Commit message:**

```
feat(rules): RulesList with checkbox toggle + token header (F08 task 4.2)

Sidebar list with per-row enable checkbox, header showing
<enabled>/10 + estimated token cost, optimistic toggle reconciled by
the watcher, toast on cap-reached. Yellow warning glyph on rules
estimated above 500 tokens. Mirrors PlansList visual conventions for
cohesion.

Refs: 08-rules-page-tasks.md (task 4.2), 08-rules-page-spec.md
```

---

### Task 4.3 — `RulePane` + `RuleViewer` + `RuleEditor` + estimator

**Size:** medium
**Refs:** spec §"UX details" → "Pane (`RulePane.tsx`)"; existing `PlanPane.tsx` / `PlanViewer.tsx` / `PlanEditor.tsx` (templates).

**Output:**
- `App~/src/lib/tokenEstimate.ts`:
  - `export function estimateTokens(text: string): number { return Math.ceil(text.length / 4); }` — single source of truth. Used by `RuleEditor` to show live token count while editing.
- `App~/src/components/RuleViewer.tsx`:
  - Props: `{ rule: Rule }`.
  - Renders `rule.content` via `react-markdown`.
  - Below body: metadata strip showing `applies_to` as chips. Strip is omitted when `applies_to.length === 0`. Below the chips: small grey caption "v2.0: informational only" — sets user expectation that the field is parsed but not yet used.
- `App~/src/components/RuleEditor.tsx`:
  - Props: `{ value, onChange, onSave, onCancel }`.
  - Monospace textarea, Cmd/Ctrl+S = save, Esc = cancel.
  - Live token estimator displayed below the textarea: `"~<N> tokens"` recalculated from `value` via `estimateTokens`.
- `App~/src/components/RulePane.tsx`:
  - Props: `{ rule: Rule | null, editMode, editDraft, onEnterEdit, onCancelEdit, onSaveEdit, onChangeDraft, onDelete, onToggle: (name, next) => Promise<...> }`.
  - When `rule === null`: empty state per spec.
  - When `rule !== null && !editMode`: header strip with `[Toggle ☑/☐] [Delete] [View | Edit]`. Toggle button reflects current `rule.frontmatter.enabled` (or false fallback) and triggers the same `onToggle` shape as the list checkbox (with toast on cap error). Body is `RuleViewer`.
  - When `rule !== null && editMode`: header strip with `[Save] [Cancel]`. Body is `RuleEditor`.
  - Delete: confirmation modal (reuse F06's `DeleteConfirmModal` if extracted; otherwise inline confirmation).

**Validation:**
- View renders markdown including code blocks, headings.
- `applies-to` chips visible with a populated array; absent when empty.
- Edit textarea live-updates token count.
- Cmd/Ctrl+S triggers save; Esc cancels.
- Toggle button in pane matches list checkbox behavior (cap toast, optimistic update).
- Delete confirmation prevents accidental deletion.

**Commit message:**

```
feat(rules): RulePane + viewer/editor + token estimator (F08 task 4.3)

Right-column pane with markdown viewer (react-markdown), monospace
editor with live token count, action header (Toggle / Delete /
View|Edit), confirmation modal for delete. RuleViewer surfaces
applies-to chips with a "v2.0 informational only" caption so users
don't expect per-subagent filtering yet. tokenEstimate.ts centralizes
the chars/4 heuristic for the list, header, and editor.

Refs: 08-rules-page-tasks.md (task 4.3), 08-rules-page-spec.md
```

---

### Task 4.4 — Wire `RulesRoute` 2-col + toggle + cap + new-rule flow

**Size:** medium
**Refs:** spec §"UX details" → "New rule flow"; tasks 4.1–4.3; existing `routes/PlansRoute.tsx` (template).

**Output:**
- `App~/src/routes/RulesRoute.tsx` — replace placeholder:
  - 2-column flex layout: `RulesList` left, `RulePane` right.
  - Wires `rulesStore` actions to component props.
  - "+ New rule" flow: opens an inline form (or modal) with `name` text input (kebab-case validation client-side via `markdown_doc`-equivalent regex in TS). On Create:
    - Client-side collision check against cached `rules` array.
    - Invokes `createNewRule(name, TEMPLATE)` where TEMPLATE is the markdown shown in spec §"New rule flow".
    - On success: `selectRule(name)` and `enterEdit()` (user can immediately edit the body).
  - Toggle wiring: row checkbox click and pane Toggle button both invoke `rulesStore.toggleRule(name, next)`; on `{ok: false, error}`, show the toast at the route level (positioned above the list).
  - Watcher event reconciliation: optimistic state in the row checkbox is overwritten by the next `loadList()` after `rules-changed`. Document inline as a comment.

**Validation:**
- Rules tab opens, loads list, shows empty state.
- "+ New rule" → form → Create → rule appears in list, opens in Edit mode with template content.
- Edit + Save → list refreshes, View mode shows rendered markdown.
- Toggle via checkbox + via pane both work end-to-end.
- Cap at 10 enabled: 11th toggle blocked with toast; UI stays consistent.
- External edit (VS Code) → list/pane refreshes within ~500ms.
- Re-launch Tauri → rules tab loads existing rules correctly.

**Commit message:**

```
feat(rules): wire RulesRoute with full CRUD + cap + new-rule flow (F08 task 4.4)

Replaces placeholder with 2-col layout. Wires rulesStore to List/Pane
components. New rule flow with collision check and disabled-by-default
template. Toggle from list checkbox AND pane button share the same
action path. Cap toast surfaces on InvalidInput from toggle_rule.

Rules tab is now fully functional end-to-end.

Refs: 08-rules-page-tasks.md (task 4.4), 08-rules-page-spec.md
```

---

## Group 5 — Final smoke

### Task 5.1 — F08 final smoke: 17 DoD scenarios + regression checks

**Size:** small
**Refs:** spec §"Definition of done".

**Output:**
- `.claude/reports/smoke/F08.md` with each of the 17 DoD scenarios checked off + the regression checks (F02 / F04 / F06 / F07).
- Any final fixes for issues caught here, in the same commit.

**Validation:** every scenario in the spec's "Definition of done" section passes against a real Unity 6 project on Windows 11. No regressions in F02 / F04 / F06 / F07 paths.

Pay special attention to:
- **Scenario 5** (verify `appendSystemPromptFile` is actually set on the SDK call): inspect supervisor stderr for the `rules bundle:` debug line added in task 3.3.
- **Scenario 6** (Claude references rule content): write a rule that says something distinctive ("All variables must be prefixed with `q_`"), enable it, ask Claude to write a code snippet. Verify the prefix shows up. If it doesn't, the bundle isn't reaching the model — either the env var isn't set, the file is missing/empty, or the SDK's `appendSystemPromptFile` semantics differ from expectation. Triage from stderr first.
- **Scenario 10** (external edit reflects in UI): keep the Rules tab open in Tauri; edit a rule file in VS Code; save; verify the list row and pane refresh within ~500ms.
- **Scenario 16** (F06 regression after markdown_doc refactor): run F06's smoke checklist (plans CRUD, slash dropdown, `@` picker) end-to-end. Refactor risk is highest here.

**Commit message:**

```
docs(rules): F08 final smoke validated — green (F08 task 5.1)

All 17 DoD scenarios from the spec pass. F02/F04/F06/F07 regression
checks clean. Rules page + appendSystemPromptFile injection shipping
with no known defects.

Refs: 08-rules-page-tasks.md (task 5.1), 08-rules-page-spec.md
```

---

## Notes for execution

- **Branch:** `feature/08-rules-page` from `develop/v2.0`. Create after F06 lands.
- **Commit cadence:** one commit per task. Use the suggested message; adjust the body if implementation details shifted from spec.
- **Validation discipline:** every task lists explicit validation steps. Run them before commit.
- **PR target:** `develop/v2.0`. After 5.1 done, open the PR with the same template style as F06.
- **No git operations from Claude Code** — Ramon owns git per CLAUDE.md.
- **Rust/TS conventions:** apply existing project standards (`///` doc comments on public Rust items, braces always, `cargo fmt` clean, no warnings; TSDoc on exported TS items, named exports preferred, no `any`).
- **Dependency hygiene:** no new crates or npm packages expected. Verify before adding anything — if you find yourself reaching for one, stop and ask. F06 already pulled in everything F08 needs.
- **Cross-platform:** v2.0 is Windows-validated. The bundle file path uses forward-slash construction in spec text but on disk uses the OS's separator via `PathBuf::join`. `notify` works on all three platforms.
- **Order:** Group 1 → 2 → 3 → 4 → 5 is the dependency-clean order. Within Group 2, tasks 2.1 / 2.2 / 2.3 / 2.4 / 2.5 can interleave freely; Group 3 depends on Group 1 (atomic_write) and Group 2 (RuleMeta shape). Group 4 depends on Group 2 + 3. Group 5 depends on everything.
- **Refactor risk in 1.1:** the F06 plans tests are the safety net. If a moved test fails after the lift, the bug is in the extraction — fix in the same commit before moving on. Don't accumulate test debt across tasks.
- **Bundle file is the trust boundary.** The Rust side owns it; the JS side reads it but never writes it. If anything weird happens to the bundle (stale content, missing, etc), the answer is "what did the watcher do? did recompose run?" — never "let's have JS rewrite it."
- **`applies-to` discipline:** the field is parsed, displayed, round-tripped, but the compiler ignores it in v2.0. Resist scope creep here. The UI's "v2.0 informational only" caption is the contract with the user.
- **Cap of 10 enabled rules is intentional friction.** When ten feels too few, that's the signal that rules are doing too much work and some should be merged into project `CLAUDE.md` or removed in favor of clearer agent instructions. v2.1 may add cost analytics to help users decide; v2.0 just enforces the bound.

## Follow-ups discovered during implementation (anticipated)

These are likely surface areas to watch — record actual findings in PR notes as they appear.

- **`appendSystemPromptFile` SDK semantics in practice.** The SDK option name and behavior come from the comment in `sdk_entry.js`; smoke (scenario 5) is the empirical confirmation. If the SDK accepts a different option name (e.g. `systemPromptFile` without the `append` prefix, or requires explicit absolute paths only), update the wire-up and re-smoke. Document the actual name + signature in PR notes.
- **`runHealthCheck` cost when rules are enabled.** Health probe will now include the rules bundle in its system prompt. For a typical bundle (<2K tokens) the cost increase is negligible per probe, but if users grow to 10 rules at ~500 tokens each (5K total), each probe adds ~5K input tokens. Future optimization: skip `appendSystemPromptFile` in `runHealthCheck` (probe doesn't need rules) or use `initializationResult()` streaming-input pattern for zero tokens. Defer until cost shows up.
- **Bundle composition latency for large rule sets.** Worst case: 10 enabled rules at 500 tokens each = 5K tokens = ~20KB. Recompose reads 10 files, sorts, concatenates — all in-memory, sub-millisecond. No issue at v2.0 scale. If a user somehow accumulates hundreds of rule files (cap notwithstanding), recompose stays fast as long as files are small.
- **`toggle_rule` rejecting non-mapping frontmatter is friction.** If users encounter this in the wild (a rule with `enabled: true` as the only frontmatter content somehow parsed as a scalar, etc), they have to edit the file directly. Consider: instead of erroring, normalize the frontmatter to a mapping during toggle. Cheaper than a docs explanation. Defer until reports surface.
