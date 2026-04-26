# Audit Report — Prefab

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Prefab/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 7 (via Glob `Editor/Tools/Prefab/Tool_Prefab.*.cs`)
- `files_read`: 7
- `files_analyzed`: 7

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Coverage is balanced, so absence claims about the Prefab domain are made directly. Cross-domain absence claims (e.g. "no override/apply/revert tool exists anywhere") were verified by `Grep` over `Editor/Tools/` for `ApplyPrefab|RevertPrefab|PrefabOverride|GetPropertyModifications` (zero matches) and `PrefabVariant|CreateVariant` (only `Tool_Prefab.Create.cs` and `Tool_Prefab.ModifyContents.cs` matched, and only on `SaveAsPrefabAsset` — no variant-specific code).

**Reviewer guidance:**
- The Prefab domain is small (7 tools) but covers a deeply stateful Unity workflow: scene-time edits vs. prefab-stage edits vs. headless asset edits. The biggest finding is not internal inconsistency but a few major capability gaps (variants, overrides, nested-prefab navigation) that block common production workflows.
- One important cross-domain overlap: `add-asset-to-scene` (in `Editor/Tools/AddAssetToScene/`) duplicates much of `prefab-instantiate`. This is documented in cluster R1 — Ramon should weigh whether the consolidation belongs in the Prefab domain or in a Scene/Asset domain.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `prefab-create` | Prefab / Create | `Tool_Prefab.Create.cs` | 4 | no |
| `prefab-get-info` | Prefab / Get Info | `Tool_Prefab.GetInfo.cs` | 1 | yes |
| `prefab-instantiate` | Prefab / Instantiate | `Tool_Prefab.Instantiate.cs` | 6 | no |
| `prefab-open` | Prefab / Open | `Tool_Prefab.Open.cs` | 1 | no |
| `prefab-save` | Prefab / Save | `Tool_Prefab.Save.cs` | 0 | no |
| `prefab-close` | Prefab / Close | `Tool_Prefab.Close.cs` | 0 | no |
| `prefab-modify-contents` | Prefab / Modify Contents | `Tool_Prefab.ModifyContents.cs` | 9 | no |

**Internal Unity API surface used:**
- `PrefabUtility.SaveAsPrefabAsset`, `PrefabUtility.SaveAsPrefabAssetAndConnect` (Create)
- `PrefabUtility.GetPrefabAssetType` (GetInfo)
- `PrefabUtility.InstantiatePrefab` (Instantiate)
- `AssetDatabase.OpenAsset`, `AssetDatabase.LoadAssetAtPath<GameObject>` (Open, GetInfo, Instantiate)
- `PrefabStageUtility.GetCurrentPrefabStage` (Save, Close)
- `PrefabUtility.SavePrefabAsset` (Save)
- `StageUtility.GoToMainStage` (Close)
- `PrefabUtility.LoadPrefabContents`, `PrefabUtility.UnloadPrefabContents`, `PrefabUtility.SaveAsPrefabAsset` (ModifyContents)
- `Tool_Transform.FindGameObject` (Create — cross-domain helper)

---

## 2. Redundancy Clusters

### Cluster R1 — Prefab Instantiation in Two Domains
**Members:** `prefab-instantiate` (Prefab), `add-asset-to-scene` (AddAssetToScene)
**Overlap:** Both tools load a prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`, call `PrefabUtility.InstantiatePrefab`, optionally rename, optionally parent, register undo, and select. `add-asset-to-scene` adds a `rotY` parameter and accepts non-prefab GameObject assets (falls back to `Object.Instantiate`); `prefab-instantiate` requires a prefab and accepts a `parentPath` (hierarchy path) instead of `parentName` (top-level lookup). 80%+ parameter overlap. An LLM asked "instantiate this prefab into the scene at (1,2,3)" has no clear basis to pick one.
**Impact:** High — both tools are visible in the same tool list and the descriptions do not disambiguate. Likely cause of frequent wrong-tool selection.
**Confidence:** high

### Cluster R2 — Two Ways to Edit Prefab Contents
**Members:** `prefab-open` + `prefab-modify-via-component-add/etc.` (stage-based workflow), `prefab-modify-contents` (headless workflow)
**Overlap:** Strictly speaking these are not redundant — the stage-based path exposes the prefab to all scene tools (Component, Transform, GameObject), while `prefab-modify-contents` is self-contained. But for the common five operations (`set-position`, `add-component`, `remove-component`, `delete-child`, `set-active`), there are now two valid paths and the LLM must choose. The descriptions of `prefab-open` and `prefab-modify-contents` do not state when one is preferred.
**Impact:** Medium — won't usually cause failure (both paths work), but it's a coin-flip on every prefab edit. The stage-based path is correct for complex multi-step edits; the headless path is correct for one-shot edits. Neither tool documents this.
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `prefab-modify-contents` action enum drift
**Location:** `prefab-modify-contents` — `Tool_Prefab.ModifyContents.cs`
**Issue:** The method-level `[Description]` lists 5 actions. The `action` parameter `[Description]` repeats the same 5. But there is no documentation of *what each action requires* in the parameter descriptions — e.g. `componentType` says "Component type name for add-component or remove-component" but does not say it is *ignored* by the other 3 actions, and `isActive` defaults to `-1` which is an in-band sentinel that needs explanation in the method description, not just the param description.
**Evidence:** Method `[Description]` line ~40: `"Modifies the contents of a Prefab asset without entering Prefab Mode. Actions: set-position, add-component, remove-component, delete-child, set-active. Changes are saved back to disk immediately."` — gives the action list but no per-action parameter map.
**Confidence:** high

### A2 — `prefab-create` ambiguous source object semantics
**Location:** `prefab-create` — `Tool_Prefab.Create.cs`
**Issue:** Both `instanceId` and `objectPath` are optional with sentinel values (`0` and `""`), and the method-level `[Description]` does not state that one of them is required. An LLM calling this tool with neither will get a generic "GameObject not found." error.
**Evidence:** `[Description("Creates a Prefab asset from a scene GameObject and saves it to the project.")]` — no mention that the source must be specified, and no "use instanceId when you have it from a recent tool call, otherwise use objectPath" disambiguation clause.
**Confidence:** high

### A3 — `prefab-save` precondition not in description
**Location:** `prefab-save` — `Tool_Prefab.Save.cs`
**Issue:** The description says `"No-ops and returns an error when no Prefab Edit Mode stage is active."` but doesn't explain that the LLM must call `prefab-open` first. The error path is correct, but a description-level "Call after `prefab-open` and your modifications" would prevent the LLM from calling `prefab-save` speculatively after `prefab-modify-contents` (which does not use the stage).
**Evidence:** No "Use after `prefab-open`, not after `prefab-modify-contents`" clause anywhere.
**Confidence:** high

### A4 — `prefab-instantiate` `parentPath` vs `add-asset-to-scene` `parentName` naming inconsistency
**Location:** `prefab-instantiate` param `parentPath` vs `add-asset-to-scene` param `parentName`
**Issue:** Cross-domain inconsistency: `prefab-instantiate` accepts `parentPath` and uses `GameObject.Find(parentPath)` (which Unity treats as a hierarchy path when it contains `/`); `add-asset-to-scene` accepts `parentName` and uses the same `GameObject.Find` API. Same backend, different surface naming. The LLM cannot tell whether `parentName` accepts a path or only a name without reading both files.
**Evidence:** `Tool_Prefab.Instantiate.cs` line 60: `var parentGo = GameObject.Find(parentPath);`. `Tool_AddAssetToScene.cs` line 80: `var parent = GameObject.Find(parentName);`. Same call, different param name and description.
**Confidence:** high

### A5 — `prefab-modify-contents` `targetChild` and `deleteChild` collision risk
**Location:** `prefab-modify-contents` — params `targetChild` and `deleteChild`
**Issue:** Two child-path parameters with overlapping semantics. `targetChild` selects which sub-transform to operate on for `set-position`/`add-component`/`remove-component`/`set-active`. `deleteChild` selects which child to destroy for `delete-child`. The descriptions do not state that for `delete-child` action, `targetChild` is ignored. An LLM might naturally try `action=delete-child, targetChild='Body/Head'` and be silently surprised when `deleteChild` (empty) errors out.
**Evidence:** `Tool_Prefab.ModifyContents.cs` lines 161-165: `delete-child` action checks `deleteChild`, not `targetChild`. The XML doc does not call out the asymmetry; the `[Description]` on `targetChild` does not say it's unused for `delete-child`.
**Confidence:** high

### A6 — `prefab-modify-contents` `isActive` integer-as-tristate
**Location:** `prefab-modify-contents` — param `isActive`
**Issue:** `isActive` uses `int` with `-1` as "skip", `0` as false, `1` as true. The description does explain this, but using a tristate int instead of a `bool?` (or splitting into a bool with required-when-action-is-set-active validation) is non-obvious. It is the only param of this shape in the domain.
**Evidence:** `Tool_Prefab.ModifyContents.cs` line 50: `[Description("Active state for set-active: 1=active, 0=inactive, -1=skip. Default -1.")] int isActive = -1` — works but is the only sentinel-int in the file.
**Confidence:** medium

### A7 — `prefab-get-info` does not state output format / depth limit
**Location:** `prefab-get-info` — `Tool_Prefab.GetInfo.cs`
**Issue:** The description says "returns its type, full hierarchy, and all components on each GameObject" but doesn't tell the caller the format is plain text (not JSON) or that hierarchy traversal is unbounded. For deeply-nested prefabs this could produce a very large response. No `maxDepth` parameter exists.
**Evidence:** `AppendHierarchy` (line 73) recurses unconditionally via `t.GetChild(ci)` and `depth + 1` with no upper bound.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `prefab-create` `savePath` default is implicit and undocumented
**Location:** `prefab-create` param `savePath`
**Issue:** `savePath` defaults to `""`, then internally becomes `Assets/{go.name}.prefab`. The `[Description]` mentions the path format but not the implicit default. The LLM has no way to know that omitting `savePath` writes to `Assets/` root rather than e.g. `Assets/Prefabs/`.
**Current:** `string savePath = ""`
**Suggested direction:** Either document the implicit default in the description, or change the default to a literal placeholder like `"Assets/Prefabs/{name}.prefab"` and document the substitution.
**Confidence:** high

### D2 — `prefab-modify-contents` `action` default of `set-position`
**Location:** `prefab-modify-contents` param `action`
**Issue:** Defaulting `action` to `"set-position"` is arbitrary. If the LLM forgets to pass `action`, the tool will silently move the target to (0,0,0). A more defensive default would be required-no-default, or a sentinel like `""` with a "must specify action" error.
**Current:** `string action = "set-position"`
**Suggested direction:** Make `action` required (no default), or default to a sentinel that errors with a list of valid actions.
**Confidence:** high

### D3 — `prefab-instantiate` no rotation parameter
**Location:** `prefab-instantiate`
**Issue:** Tool accepts `posX/posY/posZ` but no rotation. `add-asset-to-scene` accepts `rotY`. For most game-dev use-cases (placing enemies facing a direction) the LLM will instantiate, then call `transform-rotate` separately. Not a default issue per se, but a missing-default that creates a 2-call workflow.
**Current:** `posX, posY, posZ` only
**Suggested direction:** Add `rotX/rotY/rotZ` defaults of 0 (matching `add-asset-to-scene` shape).
**Confidence:** high

### D4 — `prefab-create` `keepConnection = true` default is correct but not stated as the recommendation
**Location:** `prefab-create` param `keepConnection`
**Issue:** Default `true` is the right choice, but the description is just `"Keep prefab connection on the scene object. Default true."` — it doesn't explain *why* this is the recommended default (so subsequent edits to the prefab asset propagate to the scene instance).
**Current:** `bool keepConnection = true`
**Suggested direction:** Document the rationale — "Default true. Set false only when you want a one-shot snapshot with no link back."
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Prefab Variants
**Workflow:** Create a prefab variant from an existing prefab. E.g. `Enemy_Boss.prefab` is a variant of `Enemy_Base.prefab` with different stats and a different sprite. This is a core Unity prefab feature.
**Current coverage:** None.
**Missing:** No tool calls `PrefabUtility.SaveAsPrefabAsset(GameObject, string, ...)` against an *instance of an existing prefab* to create a variant, nor `PrefabUtility.IsPartOfVariantPrefab`, `PrefabUtility.GetCorrespondingObjectFromSource`. `prefab-create` calls `SaveAsPrefabAssetAndConnect` on a *scene GameObject*, which produces a regular prefab, not a variant.
**Evidence:** `Tool_Prefab.Create.cs` lines 67-72: only handles the regular-prefab path. `Grep` over `Editor/Tools/` for `Variant`, `CreateVariant`, `IsPartOfVariantPrefab` returns zero matches outside the two `SaveAsPrefabAsset` callsites in `Create.cs` and `ModifyContents.cs` (which are the regular-prefab API).
**Confidence:** high

### G2 — Prefab Override Management (Apply / Revert / Inspect)
**Workflow:** A scene instance of `Player.prefab` has had its `maxHealth` field overridden from 100 to 150 in the Inspector. The user wants to (a) list the overrides, (b) apply them back to the prefab asset, or (c) revert them. This is one of the most common day-to-day prefab operations.
**Current coverage:** None.
**Missing:** No tool wraps `PrefabUtility.GetPropertyModifications`, `PrefabUtility.ApplyPropertyOverride`, `PrefabUtility.ApplyPrefabInstance`, `PrefabUtility.RevertPrefabInstance`, `PrefabUtility.RevertPropertyOverride`, `PrefabUtility.GetAddedComponents`, `PrefabUtility.GetRemovedComponents`.
**Evidence:** `Grep` over `Editor/Tools/` for `ApplyPrefab|RevertPrefab|PrefabOverride|GetPropertyModifications` returned zero matches (verified at audit time).
**Confidence:** high

### G3 — Nested Prefab Editing & Inspection
**Workflow:** `Level.prefab` contains a nested `Enemy.prefab`. The user wants to (a) detect the nesting, (b) navigate into the nested prefab, or (c) determine which game objects in a hierarchy are nested-prefab roots vs. plain children.
**Current coverage:** Partial. `prefab-get-info` walks the transform hierarchy but does not flag nested-prefab boundaries.
**Missing:** `prefab-get-info` does not call `PrefabUtility.IsAnyPrefabInstanceRoot` or `PrefabUtility.GetNearestPrefabInstanceRoot` while traversing, so the output does not distinguish a nested-prefab root from a plain child GameObject. There is also no tool to "open into" a nested prefab from the parent's stage.
**Evidence:** `Tool_Prefab.GetInfo.cs` lines 73-100 (`AppendHierarchy`): only outputs `[name] active=... components=[...]` — no nested-prefab annotation.
**Confidence:** high

### G4 — Setting Component Field Values Inside a Prefab
**Workflow:** Set `Player.prefab` `Rigidbody.mass = 5.0` directly on the prefab asset, without entering Prefab Mode and without instantiating to the scene first.
**Current coverage:** Partial. `prefab-modify-contents` can `add-component` or `remove-component`, but cannot configure component fields. The user must `prefab-open` → `component-update` → `prefab-save` (3-call workflow), or instantiate, edit, apply (also multi-step and risks creating overrides).
**Missing:** No `prefab-modify-contents` action like `set-component-field`. The headless `LoadPrefabContents`/`SaveAsPrefabAsset` flow already used by `ModifyContents.cs` would support this — it just doesn't expose a `set-field` action.
**Evidence:** `Tool_Prefab.ModifyContents.cs` switch on `actionNorm` (lines 99-195) has 5 cases, none of them touch `SerializedObject` / field setting. No equivalent tool elsewhere.
**Confidence:** high

### G5 — Listing Prefab Assets in the Project
**Workflow:** "List all prefabs in `Assets/Prefabs/Enemies/`." This is the natural first step before instantiating, modifying, or auditing.
**Current coverage:** Out-of-domain. The Asset domain has `Tool_Asset.Find.cs` (verified via Grep), which can presumably do this with a `t:Prefab` filter. But the Prefab domain itself has no enumeration tool, and the LLM may not realize the Asset domain handles it.
**Missing:** Either a `prefab-list` tool, or a cross-reference in `prefab-get-info`'s description pointing the LLM at `asset-find`.
**Evidence:** `Glob` `Editor/Tools/Prefab/Tool_Prefab.*.cs` — no `List` or `Find` file. `Grep` confirmed `Editor/Tools/Asset/Tool_Asset.Find.cs` exists.
**Confidence:** medium (the capability technically exists in another domain; the gap is discoverability)

### G6 — Disconnect / Unpack Prefab Instance
**Workflow:** "Unpack this scene prefab instance so I can edit it freely without affecting the prefab asset." Common Unity workflow when a designer wants to use a prefab as a starting template only.
**Current coverage:** None.
**Missing:** No tool wraps `PrefabUtility.UnpackPrefabInstance` or `PrefabUtility.UnpackPrefabInstanceAndReturnNewOutermostRoots`.
**Evidence:** `Grep` over `Editor/Tools/` for `Unpack` returned no matches in the tool layer.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort).

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G2 | Capability Gap | 5 | 3 | 15 | high | No override apply/revert/inspect tools — blocks most production prefab workflows. |
| 2 | R1 | Redundancy | 5 | 2 | 20 | high | `prefab-instantiate` and `add-asset-to-scene` overlap 80%+; LLM picks at random. |
| 3 | G4 | Capability Gap | 4 | 2 | 16 | high | `prefab-modify-contents` cannot set component field values; forces stage round-trip. |
| 4 | G1 | Capability Gap | 4 | 3 | 12 | high | No prefab variant creation — blocks core Unity workflow. |
| 5 | A1 | Ambiguity | 4 | 1 | 20 | high | `prefab-modify-contents` doesn't document which params each action uses. |
| 6 | D2 | Default | 4 | 1 | 20 | high | `action="set-position"` default silently moves prefab to origin if action omitted. |
| 7 | A4 | Ambiguity | 3 | 1 | 15 | high | `parentPath` vs `parentName` naming inconsistency between `prefab-instantiate` and `add-asset-to-scene`. |
| 8 | G6 | Capability Gap | 3 | 2 | 12 | high | No `unpack-prefab-instance` tool. |
| 9 | A2 | Ambiguity | 3 | 1 | 15 | high | `prefab-create` doesn't document that one of `instanceId`/`objectPath` is required. |
| 10 | G3 | Capability Gap | 3 | 2 | 12 | high | `prefab-get-info` doesn't annotate nested-prefab roots in the hierarchy output. |

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `Tool_Prefab.Create.cs` line 37 calls `Tool_Transform.FindGameObject(instanceId, objectPath)` — direct dependency on the Transform domain's static helper. This is fine, but worth noting that any rename or refactor of `Tool_Transform.FindGameObject` will break the Prefab domain.
- `add-asset-to-scene` lives in its own `AddAssetToScene` domain but does prefab work. The redundancy in R1 is half a Prefab-domain issue and half a question about whether `AddAssetToScene` should exist as its own domain at all. Recommend Ramon decide whether to deprecate `add-asset-to-scene` in favor of an enriched `prefab-instantiate` + a (possibly new) generic asset-instantiation tool.

**Open questions for the reviewer:**
- Should the audit-driven cleanup of the Prefab domain include changing `prefab-modify-contents` (which is monolithic) into separate per-action tools, or keep it unified and lean on the planner to fix the parameter docs? `Tool_Animation.ConfigureController.cs` is referenced by the audit prompt as an example of *good* `action`-dispatched consolidation, suggesting the unified approach is preferred. The findings above assume unified.
- Whether to add explicit `prefab-list` / `prefab-find-by-tag` tools in this domain, or to cross-link the Asset domain in descriptions (G5).

**Workflows intentionally deferred:**
- I did not exhaustively audit the `BatchExecute` domain even though `Grep` flagged it for the word "prefab" — its role is generic batching and any prefab-specific ergonomics are out of scope for this audit.

**Limits of the audit:**
- No runtime validation. All findings are static analysis of source. Behavior under prefab-stage edge cases (e.g. opening a prefab while another is already open, calling `prefab-modify-contents` during Prefab Mode) is not exercised.
- I did not search ProBuilder, Terrain, or other domains for prefab-adjacent tools beyond what the keyword search surfaced.
