# Audit Report — BatchExecute

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/BatchExecute/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via Glob `Editor/Tools/BatchExecute/Tool_BatchExecute.*.cs` returned 0; broader Glob `Editor/Tools/BatchExecute/**/*.cs` returned 1)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** balanced (1 = 1 = 1)

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Naming-convention observation (not an error):**
The standard pattern `Tool_[Domain].[Action].cs` did not match this domain. The single file is `Tool_BatchExecute.cs`, holding both tool methods plus a private helper in one partial class. The initial Glob correctly returned 0 because the dot-action segment is absent. Using a recursive glob found `Tool_BatchExecute.cs`. This is a naming inconsistency relative to the rest of the codebase (e.g. `Tool_Asset.Copy.cs`), but does not affect coverage.

**Absence claims in this report:**
Accounting is balanced. Absence claims are permitted and clearly grounded; cross-domain claims are backed by Grep evidence cited inline.

**Reviewer guidance:**
- BatchExecute is a *meta-domain*: every operation it performs already exists as a single-call tool elsewhere (asset-refresh, asset-delete, asset-copy, selection-set, editor-execute-menu). Treat it as an orchestration/macro layer rather than a primitive layer. The audit findings reflect that lens.
- A constant `_blockedMenuPrefixes` is duplicated verbatim between `Tool_BatchExecute.cs` (lines 22-29) and `Tool_Editor.ExecuteMenu.cs` (lines 21-28). Worth flagging to consolidation-planner as a code-hygiene item even though it is not a tool-shape concern.
- The `delete` action in `batch-execute-api` permanently deletes (no `moveToTrash` option) and is therefore *more dangerous* than the standalone `asset-delete` tool, which defaults to trash. This is a behavioral inconsistency with safety implications.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `batch-execute-menu` | Batch Execute / Menu Commands | `Tool_BatchExecute.cs` | 3 (`commands` string, `stopOnError` bool=true, `atomic` bool=false) | no |
| `batch-execute-api` | Batch Execute / API Calls | `Tool_BatchExecute.cs` | 2 (`operations` string, `stopOnError` bool=true) | no |

**Internal Unity API surface used:**
- `EditorApplication.ExecuteMenuItem`
- `Undo.IncrementCurrentGroup`, `Undo.GetCurrentGroup`, `Undo.SetCurrentGroupName`, `Undo.RevertAllDownToGroup`, `Undo.CollapseUndoOperations`
- `AssetDatabase.LoadAssetAtPath`, `AssetDatabase.Refresh`, `AssetDatabase.SaveAssets`, `AssetDatabase.ImportAsset`, `AssetDatabase.DeleteAsset`, `AssetDatabase.CopyAsset`, `AssetDatabase.GenerateUniqueAssetPath`
- `EditorGUIUtility.PingObject`
- `Selection.activeObject`

**Helper (private, not a tool):** `ExecuteApiCall(string action, string arg)` — dispatch table covering `select`, `ping`, `refresh`, `save`, `import`, `delete`, `duplicate`.

---

## 2. Redundancy Clusters

### Cluster R1 — Menu execution duplicates `editor-execute-menu`
**Members:** `batch-execute-menu`, `editor-execute-menu` (cross-domain, in `Editor/Tools/Editor/Tool_Editor.ExecuteMenu.cs`)
**Overlap:** Both wrap `EditorApplication.ExecuteMenuItem` and apply the same `_blockedMenuPrefixes` security filter (the constant array is duplicated verbatim — lines 22-29 in BatchExecute vs lines 21-28 in Editor.ExecuteMenu). `batch-execute-menu` adds looping, `stopOnError`, and atomic-Undo grouping on top, but for any single-command call it is functionally identical to `editor-execute-menu`.
**Impact:** The LLM has two indistinguishable choices for "execute this menu item." It will pick inconsistently, and updates to the security blocklist must happen in two places.
**Confidence:** high

### Cluster R2 — `batch-execute-api` actions reimplement standalone tools
**Members:** `batch-execute-api` actions vs the dedicated single-action tools they shadow:
- `select:<path>` vs `selection-set` (`Tool_Selection.Set.cs`) — though `selection-set` works on hierarchy paths/instance IDs of GameObjects, while `batch-execute-api select` works on asset paths. Partial overlap.
- `refresh` vs `asset-refresh` (`Tool_Asset.Refresh.cs`)
- `delete:<path>` vs `asset-delete` (`Tool_Asset.Delete.cs`) — note `asset-delete` defaults to trash; `batch-execute-api delete` permanently deletes (see Section 5, G3).
- `duplicate:<path>` vs `asset-copy` (`Tool_Asset.Copy.cs`) with auto-generated unique name. No standalone "duplicate" exists, so this one fills a small gap.
- `import:<path>` — no standalone equivalent identified.
- `save` (`AssetDatabase.SaveAssets()`) — no standalone single-call save tool identified; many tools call `SaveAssets` internally.
- `ping:<path>` — no standalone equivalent identified.

**Overlap:** When the LLM wants to do one of: select an asset, refresh AssetDatabase, or delete an asset, it has two access paths — the canonical typed tool with full parameter validation/descriptions, or the `batch-execute-api` magic-string mini-DSL. These are semantically the same operations.
**Impact:** Tool selection ambiguity. Worse, the batch path has weaker validation (e.g. delete bypasses the trash default) and weaker per-action descriptions.
**Confidence:** high

### Cluster R3 — `_blockedMenuPrefixes` constant duplication
**Members:** Same array of 5 strings at `Tool_BatchExecute.cs` 22-29 and `Tool_Editor.ExecuteMenu.cs` 21-28.
**Overlap:** Verbatim copy. Code-hygiene concern — diverging blocklists is a security failure mode.
**Impact:** Indirect — affects maintainability, not direct LLM ambiguity. Flagged for consolidation-planner attention.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — `batch-execute-api` action enum buried in description
**Location:** `batch-execute-api` — `Tool_BatchExecute.cs` line 146
**Issue:** The `operations` parameter accepts a custom mini-DSL ("action:arg"). The supported actions are listed in the *method-level* `[Description]` but not in the *parameter-level* `[Description]`. A model focusing on the parameter description sees only the example, not the enumeration. Additionally, no per-action argument shape is documented (e.g. does `save` take an arg? does `refresh`? — the code says no, but the description doesn't).
**Evidence:** Method desc (line 146) lists "select, ping, refresh, save, import, delete, duplicate" with terse parenthetical hints. Param desc (line 148): `"Comma-separated operations in 'action:arg' format (e.g. 'select:Assets/Prefabs/Player.prefab,ping:Assets/Prefabs/Player.prefab,save').",` — only one example, no enumeration, no per-action argument requirements.
**Confidence:** high

### A2 — No disambiguation between `batch-execute-menu` and `editor-execute-menu`
**Location:** `batch-execute-menu` — `Tool_BatchExecute.cs` line 43
**Issue:** Description does not contain a "use this when X, not Y" clause distinguishing it from the single-call `editor-execute-menu`. The LLM has no signal indicating "use batch only when you have 2+ commands AND want atomic undo".
**Evidence:** Line 43 description: `"Executes multiple Unity Editor menu commands in sequence. Supports stop-on-error and atomic mode (all changes grouped in a single undo operation). Use this to automate multi-step Editor workflows."` — says "multiple" but doesn't forbid single-command use, and doesn't reference the cheaper `editor-execute-menu` alternative.
**Confidence:** high

### A3 — No disambiguation between `batch-execute-api` and the standalone Asset/Selection tools
**Location:** `batch-execute-api` — `Tool_BatchExecute.cs` line 146
**Issue:** Description does not advise the model to prefer `asset-refresh`, `asset-delete`, `asset-copy`, `selection-set` for single operations. Without that nudge, an LLM may default to the batch tool because of its broader-sounding name.
**Evidence:** Line 146 description does not reference the typed alternatives. Cluster R2 lists the redundant pairs.
**Confidence:** high

### A4 — `commands` and `operations` parameter format relies on comma-splitting with no escape mechanism
**Location:** Both tools — params `commands` (line 45) and `operations` (line 148)
**Issue:** Comma is the separator, but Unity menu paths and asset paths can in principle contain commas (asset filenames especially). The format documentation does not warn the caller, and the implementation provides no escape syntax. This is a *correctness ambiguity* — the LLM cannot know whether to use a different tool when an asset path contains commas.
**Evidence:** Line 57: `var menuItems = commands.Split(',');`. Line 159: `var ops = operations.Split(',');`. No quoting/escaping. Param descs do not warn against commas in arguments.
**Confidence:** medium (commas in Unity asset filenames are uncommon but legal)

### A5 — Mixed brace style violates repo convention
**Location:** `Tool_BatchExecute.cs` line 122-123
**Issue:** `if (atomic && undoGroup >= 0)` followed by single-line body `Undo.RevertAllDownToGroup(undoGroup);` with no braces. CLAUDE.md mandates braces "always, even for single-line returns. No exceptions."
**Evidence:**
```csharp
if (atomic && undoGroup >= 0)
    Undo.RevertAllDownToGroup(undoGroup);
```
This is a code-style violation, not a tool-shape ambiguity, but worth noting since the auditor reads the file.
**Confidence:** high

### A6 — Region heading typo
**Location:** `Tool_BatchExecute.cs` line 201
**Issue:** `#region HELPER METHODOS` — should be `METHODS`.
**Confidence:** high (cosmetic)

### A7 — Atomic-failure UX is silently inconsistent
**Location:** `batch-execute-menu` — `Tool_BatchExecute.cs` lines 129-132
**Issue:** When `atomic = true` and any command fails, the code reverts down to the undo group on first failure (line 99 / 123) but does NOT report that revert in the response text. Conversely, when all succeed, it collapses operations (line 131). The user-facing description ("atomic mode (all changes grouped in a single undo operation)") doesn't mention revert-on-failure semantics. The behavior is therefore: "atomic = revert if any step fails when stopOnError=true; otherwise just collapse." Not stated.
**Evidence:** Description line 43 vs implementation lines 99, 123, 129-132.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `atomic` default is `false`, but the tool's main selling point is atomic Undo
**Location:** `batch-execute-menu` param `atomic` — line 47
**Issue:** The primary differentiator over `editor-execute-menu` is atomic Undo grouping. Defaulting to `false` means a model that uses this tool without thinking about `atomic` gets the SAME behavior as N calls to `editor-execute-menu`, except slightly more efficient. This makes the redundancy in Cluster R1 even worse: the default form of `batch-execute-menu` collapses to N independent `editor-execute-menu` calls.
**Current:** `bool atomic = false`
**Suggested direction:** Reconsider whether the default should match the tool's intent (atomic = true). Or split: keep batch only when atomic is required.
**Confidence:** medium (this is partly a design choice; flagging for review)

### D2 — `stopOnError` default of `true` is sensible but not aligned across both tools' docs
**Location:** Both tools — param `stopOnError` (lines 46, 149)
**Issue:** Default is fine. But the param description does not discuss the trade-off ("set false to attempt all operations and collect partial-success results"). For an LLM weighing whether to flip the default, the description provides no guidance.
**Current:** `bool stopOnError = true`
**Suggested direction:** Expand description to explain when to set false (e.g. "Set false when caller wants a full audit of which menu items are missing.").
**Confidence:** medium

### D3 — `batch-execute-api` actions silently differ in argument requirements
**Location:** `batch-execute-api` param `operations` — implicit per-action defaults
**Issue:** `refresh` and `save` ignore any argument; `select`, `ping`, `import`, `delete`, `duplicate` require an asset path. There is no formal default — the code just no-ops the arg. From a tool-shape perspective the param is a single string, but the *semantic* per-action argument is undocumented. A model passing `refresh:Assets/Foo.prefab` will get a successful refresh and no warning that the path was ignored.
**Evidence:** `Tool_BatchExecute.cs` lines 250-256 (`refresh` and `save` do not reference `arg`).
**Suggested direction:** Either document that args are ignored for those actions, or warn/error when an arg is supplied where none is expected.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — No "execute arbitrary Unity API call" tool
**Workflow:** A user wants to run a Unity Editor call that isn't covered by an existing typed tool — e.g. `EditorWindow.GetWindow<SceneView>().Repaint()`, or `Lightmapping.Bake()`.
**Current coverage:** `batch-execute-api` covers seven hand-picked actions only (lines 213, 232-279). `Tool_Reflect.CallMethod.cs` (cited by Grep, in `Editor/Tools/Reflect/`) likely covers reflective invocation; not in scope for this audit.
**Missing:** The "API" in `batch-execute-api`'s name implies general Unity API access, but the tool is hard-coded to a tiny dispatch table. The name overpromises.
**Evidence:** `Tool_BatchExecute.cs` lines 211-280 — `ExecuteApiCall` switch statement has only seven cases.
**Confidence:** high (the name vs implementation mismatch is unambiguous)

### G2 — No transactional/atomic mode for `batch-execute-api`
**Workflow:** Run a sequence of asset operations (delete + create + import + refresh) and roll back if any step fails.
**Current coverage:** `batch-execute-menu` has `atomic` for menu commands. `batch-execute-api` does not.
**Missing:** `batch-execute-api` has no `atomic` parameter. Asset-DB operations are partially undoable through `AssetDatabase.StartAssetEditing()` / `StopAssetEditing()` and `Undo.RecordObject` for some asset types, but the tool exposes none of this. Failures mid-sequence leave the project in a partial state with no rollback.
**Evidence:** `Tool_BatchExecute.cs` line 147 — `ExecuteApiCalls` signature has no `atomic` param. Lines 152-197 contain no `StartAssetEditing` / `StopAssetEditing` wrapping.
**Confidence:** high

### G3 — `batch-execute-api delete` permanently deletes; standalone `asset-delete` defaults to trash
**Workflow:** Delete an asset as part of a batch; expect the same safe default the standalone tool provides.
**Current coverage:** `asset-delete` (in Asset domain) has `moveToTrash = true` default. `batch-execute-api delete` calls `AssetDatabase.DeleteAsset(arg)` — permanent deletion, no recovery option exposed.
**Missing:** Either parity with the safer default, or an explicit `delete-permanent` action label so the model sees the destructive semantics. Currently the model can't distinguish.
**Evidence:** `Tool_BatchExecute.cs` lines 262-267 vs `Tool_Asset.Delete.cs` lines 41-48.
**Confidence:** high

### G4 — No way to reorder, retry, or branch on batch results
**Workflow:** "Run these N operations; if step 3 fails, run a fallback step instead of stopping."
**Current coverage:** `stopOnError` is binary — stop or continue past failures.
**Missing:** No conditional / branching support. This is intentional (batch tools shouldn't be programming languages), but worth noting because the only "branch" available is "stop or skip-and-continue". A workflow that wants "skip if asset missing, otherwise process" must pre-validate with separate tool calls.
**Evidence:** `Tool_BatchExecute.cs` lines 70-127 (menu loop) — only `break` on error or `continue` past.
**Confidence:** medium (this may be a deliberate scope decision, not a gap; flagging for reviewer)

### G5 — No dry-run / preview mode
**Workflow:** "Show me what would run if I executed this batch, without actually changing anything."
**Current coverage:** None. There is no `preview` or `dryRun` flag.
**Missing:** A read-only mode that validates menu paths, checks asset paths exist, and reports a plan, without mutating state. This is particularly valuable for `batch-execute-api delete`/`duplicate`/`import` where mistakes are destructive.
**Evidence:** `Tool_BatchExecute.cs` — neither tool exposes a dry-run flag; neither is marked `ReadOnlyHint = true`. Confirmed by reading the entire file.
**Confidence:** high

### G6 — Comma-separated string format is fragile vs an array param
**Workflow:** Pass a list of menu paths or operations from an LLM that wants structured input.
**Current coverage:** Both tools accept a single comma-joined string.
**Missing:** Native array params (e.g. `string[] commands`). MCP supports arrays, and the rest of the codebase uses comma-separated strings as an apparent convention (see `selection-set` for similar pattern), so this may be deliberate. But the cost is: no way to embed a comma in any element. An array param would be more robust.
**Evidence:** `Tool_BatchExecute.cs` line 44 (`string commands`), line 147 (`string operations`). `Tool_Selection.Set.cs` line 38-39 also uses comma-separated strings. Convention is consistent across repo, so flagging as informational.
**Confidence:** medium

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | R2 | Redundancy | 5 | 3 | 15 | high | `batch-execute-api` actions duplicate standalone Asset/Selection tools with weaker validation. |
| 2 | G3 | Capability/Safety Gap | 5 | 1 | 25 | high | `batch-execute-api delete` permanently deletes; inconsistent with `asset-delete`'s trash-by-default. |
| 3 | R1 | Redundancy | 4 | 2 | 16 | high | `batch-execute-menu` overlaps `editor-execute-menu` (verbatim duplicate of blocklist constant). |
| 4 | A1 | Ambiguity | 4 | 1 | 20 | high | `operations` param description omits action enumeration and per-action arg shape. |
| 5 | A2 | Ambiguity | 4 | 1 | 20 | high | No "use when X, not Y" disambiguation between `batch-execute-menu` and `editor-execute-menu`. |
| 6 | G2 | Capability Gap | 4 | 3 | 12 | high | `batch-execute-api` has no atomic / rollback mode despite being a batch tool. |
| 7 | G5 | Capability Gap | 3 | 2 | 12 | high | No dry-run / preview mode for either batch tool. |
| 8 | A3 | Ambiguity | 3 | 1 | 15 | high | No nudge toward standalone Asset tools; LLM may prefer batch tool by default. |
| 9 | D1 | Default Issue | 3 | 1 | 15 | medium | `atomic` defaults to `false` despite being the tool's main differentiator. |
| 10 | A7 | Ambiguity | 3 | 2 | 12 | high | Atomic revert-on-failure semantics not described in tool documentation. |
| 11 | R3 | Redundancy/Hygiene | 2 | 2 | 8 | high | `_blockedMenuPrefixes` constant duplicated verbatim in two files. |
| 12 | G1 | Capability Gap | 3 | 4 | 6 | high | Tool name promises "API calls" but only seven hardcoded actions are supported. |

(Priority = Impact × (6 − Effort). Higher score = higher value. Top items mix high impact with low effort.)

---

## 7. Notes

- **Cross-domain dependencies noticed:**
  - Strong overlap with `Editor/Tools/Editor/Tool_Editor.ExecuteMenu.cs` (menu execution + identical blocklist constant).
  - Action-level overlap with `Editor/Tools/Asset/Tool_Asset.{Refresh,Delete,Copy}.cs` and `Editor/Tools/Selection/Tool_Selection.Set.cs`.
  - `Editor/Tools/Reflect/Tool_Reflect.CallMethod.cs` was not read but appears (via Grep) to be the canonical "execute arbitrary API" tool, which makes `batch-execute-api`'s name even more misleading.

- **Open questions for the reviewer:**
  1. Is BatchExecute meant to remain as a *macro* layer over single-call tools, or should it be folded into the per-domain tools as optional `actions` arrays / atomic flags? The current half-and-half design produces the redundancy in R1/R2.
  2. Should `batch-execute-api` be renamed to something narrower (e.g. `batch-execute-asset-ops`) since the action set is asset-DB-focused?
  3. Should the security blocklist be promoted to a shared helper (e.g. `Editor/Tools/Helpers/MenuSecurity.cs`) rather than duplicated?
  4. Is the comma-separated string convention (R2 / G6) a hard repo convention, or open to revisit toward array params for batch-style tools?

- **Workflows intentionally deferred:**
  - Did not enumerate the full reflection-based capability surface in `Tool_Reflect.CallMethod.cs` (out of scope for BatchExecute audit).
  - Did not compare against `Tool_Build.BatchBuild.cs` (Build domain) — different concept of "batch", not relevant to this domain's design.

- **Honest limits of this audit:**
  - Single-file domain, 283 lines, fully read. Confidence in coverage is high.
  - All absence claims ("no atomic mode for API", "no dry-run", "no permanent-delete distinction") are verified against the entire file. Cross-domain absence claims (e.g. "no standalone save tool") were spot-checked via Grep but not exhaustively verified across all 39 domains; treat those as informational and verify before relying on them.
