# Review — Prefab

**Audit reviewed:** `.claude/reports/audits/audit-Prefab-20260425.md`
**Reviewer:** Ramon (drafted by auto-reviewer agent on 2026-05-18; escalations answered by Ramon; finalized 2026-05-18)
**Date:** 2026-05-18

---

## 0. Auto-Decision Summary

This review was drafted by the auto-reviewer agent and finalized after Ramon answered the 7 escalation questions in Section 5. The Prefab audit has **19 findings** across 6 categories (2 redundancy clusters R1–R2, 7 ambiguity findings A1–A7, 4 default issues D1–D4, 6 capability gaps G1–G6).

**Findings auto-decided by agent:** 9 (9 accepted, 0 rejected, 0 deferred)
**Findings resolved via Ramon's escalation answers:** 10 (bundled into 7 escalation questions E1–E7; all answered and folded into Section 1)

**Auto-decision rationale:**

The pipeline is paused for v2.0 but Ramon is treating Prefab as a second one-off exception after the GameObject cycle that shipped 2026-05-18. The GameObject precedent supplies several hard conventions that make some Prefab findings mechanical:

- **String-sentinel for nullable bools** (`"true" | "false" | ""`) is now the project convention — A6's int tri-state `isActive` migration is no longer a design choice; it's a mandated alignment.
- **Description-only fixes are quick wins** — A1, A2, A3, A5, A7 (description portion), D4 fit this pattern and the audit gives explicit wording recommendations or single-direction guidance.
- **Required-no-default with empty sentinel + error** is the safe pattern when a `string action` parameter has no defensible default — D2 falls under this (changing `action = "set-position"` to `action = ""` with an error listing valid actions).
- **`savePath` implicit default (D1)** — auto-accepted as description-only ("default empty resolves to `Assets/{name}.prefab`"); the more elaborate placeholder-substitution alternative is not chosen because it introduces parsing code with no clear ergonomic benefit over a documented implicit default.

The remaining 10 findings were escalated and have all been resolved by Ramon (Section 5). Summary of his decisions:

- **E1 (R1 + A4 + D3):** Delete `add-asset-to-scene`; fold features into `prefab-instantiate` (rotX/rotY/rotZ + non-prefab fallback).
- **E2 (R2 + Open Q #1):** Keep `prefab-modify-contents` unified; commit to action-dispatch growth.
- **E3 (A7 + G3):** Add `maxDepth` param + nested-prefab annotation + `isVariant` output flag.
- **E4 (G1):** Add `prefab-create-variant`; drop separate is-variant tool (folded into get-info).
- **E5 (G2):** Action-dispatched `prefab-override` tool with 3 actions (list, apply-instance, revert-instance). Per-property deferred.
- **E6 (G4 + G6):** Add both. `set-component-field` as 6th action on `prefab-modify-contents` (scoped to primitives + ObjectReference, with FUTURE CONSOLIDATION note). New `prefab-unpack-instance` tool.
- **E7 (G5 + Open Q #2):** Description-only cross-link to `asset-find`.

**Backward compatibility:** Confirmed box 3 ("may break tool names freely"). The `add-asset-to-scene` deletion (E1), A6 sentinel migration, and D2 default removal are all clean breaks per the GameObject precedent.

---

## 1. Decisions Per Finding

| Finding ID | Decision | Notes |
|-----------|----------|-------|
| R1 | accept | Per E1: delete `add-asset-to-scene` entirely; fold its features into `prefab-instantiate` (non-prefab fallback via `PrefabUtility.GetPrefabAssetType` → `Object.Instantiate`). Clean break per GameObject precedent. |
| R2 | accept with modification | Per E2: keep `prefab-modify-contents` unified; commit to action-dispatch growth (sets up E6/G4 as 6th action). Description-only disambiguation between stage and headless paths included. |
| A1 | accept | Auto-decided: description-only fix recommended directly by audit (high confidence). Expand method-level `[Description]` on `prefab-modify-contents` to include per-action required-param map (e.g. "set-position uses posX/Y/Z; add-component/remove-component use componentType; delete-child uses deleteChild; set-active uses isActive"). |
| A2 | accept | Auto-decided: description-only fix recommended directly by audit (high confidence). Update `prefab-create` `[Description]` to state "One of `instanceId` or `objectPath` is required. Prefer `instanceId` if known from a recent tool call; otherwise use `objectPath`." |
| A3 | accept | Auto-decided: description-only fix recommended directly by audit (high confidence). Add to `prefab-save` `[Description]`: "Call after `prefab-open` and your modifications. Do NOT call after `prefab-modify-contents` (which saves on its own)." |
| A4 | accept | Per E1: resolved by deleting `add-asset-to-scene`. `prefab-instantiate` keeps `parentPath` (the more accurate name — supports both top-level names and hierarchy paths via `GameObject.Find`). Naming inconsistency disappears with the deletion. |
| A5 | accept | Auto-decided: description-only fix recommended directly by audit (high confidence). Tighten `targetChild` `[Description]` to state "Ignored for `delete-child` action (use `deleteChild` instead)." Symmetrically tighten `deleteChild` `[Description]` to state "Only used for `delete-child` action." |
| A6 | accept | Auto-decided: CLAUDE.md sentinel convention. Migrate `isActive` from `int -1/0/1` to nullable string `"true" \| "false" \| ""` per the project-wide decision shipped in the GameObject cycle. This IS a signature break — see Section 3 backward-compat box. The "no value" path produces an error only when `action = "set-active"` (preserving current behavior); for other actions the empty sentinel is silently ignored. |
| A7 | accept | Per E3: add `int maxDepth = -1` param (-1 = unlimited, preserves current behavior) and document the plain-text output format in `[Description]`. Bundled with G3. |
| D1 | accept | Auto-decided: description-only fix is the safe direction. Update `savePath` `[Description]` to state explicitly "Default empty: resolves to `Assets/{go.name}.prefab` at the project root." The placeholder-substitution alternative (`"Assets/Prefabs/{name}.prefab"` with literal template parsing) is rejected as it introduces parsing code with no clear ergonomic benefit. |
| D2 | accept | Auto-decided: audit recommends "Make `action` required (no default), or default to a sentinel that errors with a list of valid actions." The sentinel approach (`action = ""` + error listing valid actions) is the established pattern in the codebase. This IS a signature break on `prefab-modify-contents` — see Section 3 backward-compat box. |
| D3 | accept | Per E1: add `rotX/rotY/rotZ` (defaults 0) to `prefab-instantiate` for full 3D rotation parity. Lands as part of the `add-asset-to-scene` consolidation. |
| D4 | accept | Auto-decided: description-only rationale tweak recommended directly by audit (medium confidence but cheap). Update `keepConnection` `[Description]` to "Keep prefab connection on the scene object so future edits to the prefab asset propagate. Default true. Set false only when you want a one-shot snapshot with no link back." |
| G1 | accept with modification | Per E4: add `prefab-create-variant(instanceId, objectPath, savePath)` as a new tool (~30 lines). DROP the separate `prefab-is-variant` read tool — variant query is folded into `prefab-get-info`'s output as `isVariant: true/false` (see E3). |
| G2 | accept with modification | Per E5: action-dispatched `prefab-override` single tool. Start with 3 actions this cycle (`list`, `apply-instance`, `revert-instance`). Per-property actions (`apply-property`, `revert-property`) and added/removed components/gameobjects deferred until a v2.x workflow needs them. |
| G3 | accept | Per E3: in `AppendHierarchy`, per-node call `PrefabUtility.IsAnyPrefabInstanceRoot(node)` and prefix rows with `[nested-prefab]` where true. Lives entirely inside `prefab-get-info`. |
| G4 | accept with modification | Per E6: add `set-component-field` as the 6th action on `prefab-modify-contents` (per E2's growth commitment). Scoped narrowly: primitives only (`int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Quaternion`, `enum`) + `ObjectReference`. Reject other types with a clear error. Plan MUST include a FUTURE CONSOLIDATION note re: post-v2.0 Object/SO/Component triangle (see Section 3). |
| G5 | accept | Per E7: description-only cross-link. Append to both `prefab-get-info` and `prefab-instantiate` descriptions: "To enumerate or search for prefabs by path/filter, use `asset-find` with `t:Prefab`." No new tool, no Asset-domain edits. |
| G6 | accept | Per E6: new `prefab-unpack-instance(instanceId, objectPath, unpackMode)` tool. `unpackMode` is `"outermost"` or `"completely"` (mapping to `PrefabUnpackMode.OutermostRoot=0` / `PrefabUnpackMode.Completely=1`). Wraps `PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction)`. ~30 lines, fully isolated. |

---

## 2. Open Questions Answered

The audit's Section 7 lists two open questions for the reviewer.

| Question | Answer |
|----------|--------|
| Should the audit-driven cleanup of the Prefab domain include changing `prefab-modify-contents` (which is monolithic) into separate per-action tools, or keep it unified and lean on the planner to fix the parameter docs? | **Keep unified** — per E2. The tool is genuinely action-dispatched today (5 different verbs sharing the `LoadPrefabContents` → mutate → `SaveAsPrefabAsset` → `UnloadPrefabContents` plumbing), matching the `Tool_Animation.ConfigureController.cs` precedent. Commit to action-dispatch growth: E6/G4 (`set-component-field`) lands as the 6th action. Description-only disambiguation between stage path (`prefab-open`) and headless path (`prefab-modify-contents`) is included. |
| Whether to add explicit `prefab-list` / `prefab-find-by-tag` tools in this domain, or to cross-link the Asset domain in descriptions (G5). | **Cross-link only** — per E7. The enumeration capability already exists at `asset-find` with `t:Prefab`. The gap is discoverability, not capability. Append to both `prefab-get-info` and `prefab-instantiate` `[Description]`: "To enumerate or search for prefabs by path/filter, use `asset-find` with `t:Prefab`." Respects domain boundaries; avoids duplicated enumeration code. Asset-domain audit owns the broader enumeration story. |

---

## 3. Constraints For The Plan

### Backward Compatibility

- [ ] Must preserve all existing tool names (no breaking renames)
- [ ] May rename tools but must add deprecation aliases
- [x] **May break tool names freely (this is internal refactor)**

**Resolution:** Box 3 confirmed. Three signature/surface breaks are part of this cycle:
- **A6** (sentinel migration on `isActive`): `int` → `string`. Existing callers passing `1`/`0`/`-1` will fail.
- **D2** (removing default on `action`): callers relying on the implicit `set-position` default will get a validation error.
- **E1 / R1** (`add-asset-to-scene` deletion): the entire `add-asset-to-scene` tool is removed; callers must migrate to `prefab-instantiate` (which now accepts non-prefab GameObject assets via fallback and has `rotX/rotY/rotZ` rotation parity).

All three follow the GameObject cycle's "may break freely" stance — clean breaks, no shims. Per the GameObject precedent (no deprecation aliases for `gameobject-select`), this Prefab cycle deletes outright rather than aliasing.

### Code Style

- Follow existing CLAUDE.md C# standards strictly: braces on all `if`, no empty catches, `EntityIdToObject` not `InstanceIDToObject`, no `obj?.prop = x` null-conditional assignment, partial-class single-summary rule (the `[McpToolType]` file owns the class-level summary; other partial files have no class-level summary).
- All new tool descriptions must come from `[System.ComponentModel.Description]` attribute on the method (NOT `toolAttr.Description`, which is always empty — see CLAUDE.md "Critical").
- **`McpLogger` has only `Info` and `Error`** — use `Debug.LogWarning` for warnings if any are added (e.g. inside `PrefabUtility.LoadPrefabContents` failure paths, or inside override-suite tools added under E5).
- **String-sentinel convention for nullable booleans:** `"true" | "false" | ""`. Project-wide standard established in the GameObject cycle. A6 migrates `isActive` to this form. Do NOT introduce new int tri-state booleans in any new tools added under E2/E4/E5/E6.
- Prefer action-dispatched consolidation when a tool grows beyond ~5 params with sentinels — see `Tool_Animation.ConfigureController.cs` as exemplar. The GameObject cycle established that single-verb tools should stay single-verb; reserve action-dispatch for genuinely different verbs. `prefab-modify-contents` (5→6 actions per E2/E6) and `prefab-override` (3 actions per E5) both qualify as genuinely action-dispatched.

### Scope Limits

- **Do NOT touch the Asset domain.** Even though G5 / E7 cross-link to `asset-find`, the Asset domain has its own audit pending. Cross-references in descriptions (cite-only) are fine; no Asset-domain code edits.
- **Do NOT touch the Component domain.** G4 / E6 (set-component-field) reads as a Prefab-domain feature delivered via the headless `LoadPrefabContents` flow, not a Component-domain change. The new action lands inside `prefab-modify-contents` only.
- **FUTURE CONSOLIDATION note required on G4 (set-component-field):** The plan must include a comment in the generated code citing the post-v2.0 backlog's "Object ↔ ScriptableObject ↔ Component generic-modifier triangle" cross-cutting decision. When that triangle is resolved in v2.1.x, this action's implementation should be revisited to align with the canonical pattern (or extract a shared helper). Field-type scope this cycle: primitives only (`int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Quaternion`, `enum`) + `ObjectReference`. Reject anything else (custom struct, generic List/array of complex types, AnimationCurve, etc.) with a clear error message. Use `SerializedObject` + `FindProperty(fieldName)` + type-specific setter (`.intValue`, `.objectReferenceValue`, etc.) + `ApplyModifiedPropertiesWithoutUndo` (we're inside `LoadPrefabContents` scope — undo is N/A).
- **Do NOT touch the Script, Scene, or Transform domains.** The Prefab domain depends on `Tool_Transform.FindGameObject` (cited in audit Section 7) — this dependency stays as-is. Do NOT migrate `Tool_Transform.FindGameObject` off `EditorUtility.InstanceIDToObject` here; that's flagged for the Transform audit.
- **Do NOT pull v2.0 features into this cycle** — chat UI, orchestrator, plans tab, etc. are out of scope per `docs/internal/roadmap.md`.
- **The `AddAssetToScene` domain (1 file) is in-scope for deletion** per E1. `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` is removed. Verify no other code-paths reference it before deleting.

### Preferences

- **Description-only fixes are quick wins.** A1, A2, A3, A5, D1, D4 are pure documentation edits with no behavioral impact. Group them into a single PR (Group A below).
- **Maintain symmetry across the domain:** every tool that targets an existing GameObject or prefab asset should accept the established lookup patterns (`instanceId` + `objectPath` for scene objects, `prefabPath` for asset path lookups). New tools added under E4/E5/E6 follow this — `prefab-create-variant` and `prefab-unpack-instance` both use `(instanceId, objectPath, ...)`.
- **Macro tools over chained micro-tools** where workflow ergonomics matter (per GameObject E2/E4 precedent). `prefab-override` lands as a single action-dispatched tool (3 actions) rather than 3 separate single-verb tools, since the verbs share target (prefab instance root) and plumbing.
- **Defer aggressively.** This is a one-off exception in a v2.0-priority window. Per-property override actions (`apply-property` / `revert-property`) and added/removed components/gameobjects are deferred to v2.1.x. The G4 `set-component-field` FUTURE CONSOLIDATION note is the placeholder for the post-v2.0 generic-modifier triangle.

---

## 4. Priority Override

Use audit ranking as-is.

The audit's top-10 ranking by `Impact × (6 - Effort)`:

1. R1 / D2 / A1 (score 20) — top-tier wins
2. G2 / A4 / A2 (score 15+) — high impact
3. G4 / G1 / G6 / G3 (score 12) — capability gaps

Section 6 sequences the work groups low-risk-first (description polish) to highest-scope-last (overrides, capability gaps). The planner may re-sequence if it finds a better order, but the audit's ranking should be the default tie-breaker.

---

## 5. Escalation Block — Ramon's answers (preserved as rationale of record)

These decisions were Ramon's. Preserved in full so future-Ramon and the planner can read the reasoning behind each accept / accept-with-modification / defer decision in Section 1.

---

### E1 — `prefab-instantiate` vs `add-asset-to-scene` cross-domain overlap (R1 + A4 + D3)

**Audit context:** R1 (80%+ parameter overlap between `prefab-instantiate` and `add-asset-to-scene`), A4 (`parentPath` vs `parentName` naming inconsistency on the same backend call), D3 (`prefab-instantiate` lacks rotation params; `add-asset-to-scene` has `rotY`).

The `AddAssetToScene` domain is one file (`Tool_AddAssetToScene.cs`) and effectively a parallel implementation of `prefab-instantiate` with two differences: (a) accepts non-prefab GameObject assets via fallback to `Object.Instantiate`, (b) has `rotY`. The two tools live in the same tool list with no disambiguation.

**Question:** How should the overlap be resolved?

**Options:**
1. **Delete `add-asset-to-scene` entirely.** Fold its features into `prefab-instantiate`: add `rotX/rotY/rotZ` (defaults 0), accept non-prefab GameObject assets as a fallback, rename `parentPath` to stay (it's the more accurate name — supports both top-level names and hierarchy paths via `GameObject.Find`). Clean break, follows GameObject precedent (deleted `gameobject-select` outright). Smallest surface.
2. **Keep both, rename `add-asset-to-scene`'s `parentName` to `parentPath`** for consistency, add rotation parity to `prefab-instantiate`. Both tools survive with clear disambiguation in descriptions ("Use `prefab-instantiate` for prefab assets; use `add-asset-to-scene` for non-prefab GameObject assets / model imports / FBX"). Two breaks: rename on `add-asset-to-scene`, additive expansion on `prefab-instantiate`.
3. **Rename `add-asset-to-scene` to `asset-instantiate`** and reshape it as the generic-asset-instantiation tool (since prefab is just one asset type). `prefab-instantiate` becomes the prefab-specific specialization. This is a bigger refactor and arguably bleeds into the Asset domain (out of scope per Section 3).
4. **Leave both as-is**, only update descriptions to disambiguate. Pure description-only resolution. Smallest change, but doesn't address the param-name mismatch.

**My recommendation:** **Option 1** (delete `add-asset-to-scene`, fold its features into `prefab-instantiate`). The GameObject precedent set the "delete redundant tools rather than shim" pattern; the `add-asset-to-scene` codepath is small (1 file) and adding a non-prefab fallback to `prefab-instantiate` is trivial (check `PrefabUtility.GetPrefabAssetType` on the loaded asset; if not a prefab, fall back to `Object.Instantiate`). The "this is for non-prefab assets" niche of `add-asset-to-scene` doesn't justify a separate tool. Rotation params land as `rotX/rotY/rotZ` for full 3D support (Jurassic Survivors is 2D but the package is general-purpose).

**Your answer:** Option 1 — delete `add-asset-to-scene`, fold its features into `prefab-instantiate`.

Rationale: matches the GameObject precedent (delete-without-shim), resolves the post-v2.0 backlog's flagged cross-cutting decision ("AddAssetToScene → folded into Prefab?"), and the non-prefab fallback is a one-line `PrefabUtility.GetPrefabAssetType` branch into `Object.Instantiate`. Add `rotX/rotY/rotZ` (defaults 0) for full 3D rotation parity. Keep `parentPath` (the more accurate name — works for both top-level and hierarchy paths via `GameObject.Find`)
---

### E2 — Stage vs headless prefab editing paths (R2 + audit Open Question #1)

**Audit context:** R2 (two valid paths to edit prefab contents — `prefab-open` + stage-scoped tools vs `prefab-modify-contents` headless). Audit Open Question #1 asks whether to split `prefab-modify-contents` into separate per-action tools or keep it unified with better descriptions.

`prefab-modify-contents` currently dispatches 5 actions (`set-position`, `add-component`, `remove-component`, `delete-child`, `set-active`) over a `LoadPrefabContents` → mutate → `SaveAsPrefabAsset` → `UnloadPrefabContents` flow. It's monolithic but the workflow it implements is genuinely "headless one-shot edit" — different from the stage-based `prefab-open` path which is for multi-step interactive edits.

**Question:** What's the structural direction for `prefab-modify-contents` and the stage-vs-headless choice?

**Options:**
1. **Keep `prefab-modify-contents` unified, add description-only disambiguation.** Both paths survive; descriptions on `prefab-open` and `prefab-modify-contents` explain the trade-off ("use `prefab-open` for multi-step edits where you'll use Component / Transform / GameObject tools between open and save; use `prefab-modify-contents` for one-shot scripted edits"). Add the per-action param map (A1, already auto-accepted). Smallest change.
2. **Split `prefab-modify-contents` into per-action tools** (`prefab-set-position-headless`, `prefab-add-component-headless`, etc.). Wide surface (5+ new tools). Each tool has a clean signature. But this contradicts the action-dispatch precedent in `Tool_Animation.ConfigureController.cs` and produces 5 small tools where 1 unified tool currently exists.
3. **Keep unified AND extend with new actions** (anticipating E6: `set-component-field` could be a new action). Same as Option 1 but commits to action-dispatch as the growth direction.
4. **Deprecate `prefab-modify-contents` entirely** in favor of always using the stage-based path. Pushes the LLM to `prefab-open` → tool calls → `prefab-save`. Cleanest model but a 3-call workflow for what's currently 1 call.

**My recommendation:** **Option 3** (keep unified, commit to action-dispatch growth). The action-dispatch pattern is the project precedent (`Tool_Animation.ConfigureController.cs`) and `prefab-modify-contents` is genuinely action-dispatched today — five different verbs over a shared headless plumbing. Adding `set-component-field` (E6) as a sixth action fits cleanly. Description-only disambiguation between stage and headless paths (Option 1's content) lands as part of this.

**Your answer:** Option 3 — keep `prefab-modify-contents` unified, commit to action-dispatch growth.

Rationale: matches project precedent (`Tool_Animation.ConfigureController.cs`), and the tool is genuinely action-dispatched today (5 different verbs sharing the `LoadPrefabContents` → mutate → `SaveAsPrefabAsset` → `UnloadPrefabContents` plumbing). Option 1's description-only disambiguation between stage (`prefab-open`) and headless (`prefab-modify-contents`) paths lands as part of this. Sets up E6/G4 as the 6th action cleanly.

---

### E3 — `prefab-get-info` enrichment scope (A7 + G3)

**Audit context:** A7 (no depth limit, hierarchy could be very large for deeply-nested prefabs, output format not documented), G3 (no nested-prefab annotation — `AppendHierarchy` doesn't flag where a nested prefab boundary sits).

Both findings are about `prefab-get-info` and both involve enriching its output.

**Question:** How much to enrich `prefab-get-info` in this cycle?

**Options:**
1. **Description-only.** Document the unbounded traversal and plain-text format in `[Description]`. Leave G3 unaddressed this cycle. Smallest change.
2. **Description + `maxDepth` param.** Add `int maxDepth = -1` (unlimited) param to `prefab-get-info`. Address A7 fully, still leave G3 unaddressed.
3. **Description + `maxDepth` + nested-prefab annotation.** Add `maxDepth` param. Also update `AppendHierarchy` to call `PrefabUtility.IsAnyPrefabInstanceRoot` / `GetNearestPrefabInstanceRoot` per node and prefix output rows with `[nested-prefab]` where applicable. Addresses both A7 and G3.
4. **Option 3 plus a new `prefab-navigate-nested` tool** to "open into" a nested prefab from the parent stage. Bigger scope; touches the stage-management area.

**My recommendation:** **Option 3.** `maxDepth` is a small additive param and the nested-prefab annotation is purely an `AppendHierarchy` extension — both stay inside `prefab-get-info` with no surface beyond it. Option 4 (the navigate-nested tool) is the kind of workflow tool best deferred to the v2.1.x batch since it interacts with the broader stage-management story that may evolve in v2.0.

**Your answer:** Option 3 — description + `maxDepth` + nested-prefab annotation, plus `isVariant` field in output (fold from E4).

Rationale: addresses A7 (unbounded traversal, undocumented format) and G3 (no nested-prefab annotation) in one tool, no surface beyond `prefab-get-info`.

Implementation notes:
- Add `int maxDepth = -1` param (-1 = unlimited, preserves current behavior).
- Document the plain-text output format in `[Description]`.
- In `AppendHierarchy`, per-node call `PrefabUtility.IsAnyPrefabInstanceRoot(node)` and prefix rows with `[nested-prefab]` where true.
- Add `isVariant: true/false` to the top-level output via `PrefabUtility.GetPrefabAssetType(loadedAsset) == PrefabAssetType.Variant` — this covers the read side that E4 originally split into a separate `prefab-is-variant` tool.

---

### E4 — Prefab Variants (G1)

**Audit context:** G1 — no tool creates a prefab variant. `prefab-create` only produces regular prefabs via `SaveAsPrefabAssetAndConnect` on a scene GameObject. Creating a variant requires calling `PrefabUtility.SaveAsPrefabAsset(GameObject, string, ...)` against an *instance of an existing prefab*.

Variants are a core Unity prefab feature (e.g. `Enemy_Boss` as a variant of `Enemy_Base`), but they're also a workflow that production teams either use heavily or barely at all depending on architecture.

**Question:** Add prefab variant support this cycle?

**Options:**
1. **Add `prefab-create-variant(scenePrefabInstance, savePath)`** as a new tool. Single-verb new tool, ~50 lines. Includes the "scene object must be a prefab instance" validation. Add `prefab-is-variant(prefabPath)` as a read-only companion (calls `PrefabUtility.IsPartOfVariantPrefab`).
2. **Add only `prefab-create-variant`** (skip the is-variant query — `prefab-get-info` can carry it as part of E3's enrichment).
3. **Defer to v2.1.x batch.** Cite the gap; ship without it. Variants are not in any current v2.x workflow demonstrated to date.
4. **Add variant support as a new action on `prefab-create`** (e.g. `kind = "regular" | "variant"`). Action-dispatched but reuses the create surface.

**My recommendation:** **Option 1** if you have a production reason to need variants in v2.x (the Jurassic Survivors use case isn't visible in the docs I have). **Option 3** if you don't — defer. Lean toward Option 3 absent a clear use case, since each new tool in this paused-pipeline window should pay for itself in v2.x workflows.

**Your answer:** Option 1 (modified) — add `prefab-create-variant` as a new tool. DROP the separate `prefab-is-variant` read tool — the variant query is folded into `prefab-get-info`'s output as `isVariant: true/false` (see E3).

Rationale: variant creation is a core Unity prefab feature and the package is general-purpose (not Jurassic-Survivors-specific). `PrefabUtility.SaveAsPrefabAsset(instance, path, out success)` automatically creates a Variant when the input is a prefab instance root — implementation is essentially: validate that `scenePrefabInstance` is a prefab instance root (`PrefabUtility.IsAnyPrefabInstanceRoot`), then call `SaveAsPrefabAsset`. ~30 lines. The read side gets a free ride on the E3 enrichment, avoiding a second tool.

Signature: `prefab-create-variant(instanceId, objectPath, savePath)` — symmetric with the rest of the domain via the `Tool_Transform.FindGameObject` helper.

---

### E5 — Prefab Override Management suite (G2)

**Audit context:** G2 — no tools wrap `PrefabUtility.GetPropertyModifications`, `ApplyPropertyOverride`, `ApplyPrefabInstance`, `RevertPrefabInstance`, `RevertPropertyOverride`, `GetAddedComponents`, `GetRemovedComponents`. This is one of the most common day-to-day prefab operations. The audit ranks G2 as priority #1 (score 15, Impact 5).

This is the largest scope decision in the Prefab audit. A minimal viable override suite is 3 tools (list-overrides, apply-instance, revert-instance); a maximal suite is 5-6 tools covering per-property granularity, added/removed components, and inspection.

**Question:** Add override management this cycle, and at what depth?

**Options:**
1. **Minimal suite (3 tools):** `prefab-list-overrides`, `prefab-apply-instance`, `prefab-revert-instance`. Covers ~80% of common workflows. Per-property granularity deferred.
2. **Action-dispatched suite (1 tool):** `prefab-override` with actions `list`, `apply-instance`, `revert-instance`, `apply-property`, `revert-property`. Single tool, 5 actions, consolidated surface. Follows the GameObject E5 precedent (single tool with sentinel action when verbs are genuinely different).
3. **Maximal suite (5+ tools):** all of the above as separate single-verb tools. Larger surface, clearer signatures, but contradicts the action-dispatch precedent.
4. **Defer to v2.1.x batch.** Cite the gap; ship without it. Largest single piece of work in the audit; deferring keeps this Prefab cycle modest.

**My recommendation:** **Option 2** (action-dispatched single tool). Matches the `Tool_Animation.ConfigureController.cs` pattern, matches the `prefab-modify-contents` precedent in this very domain, and overrides have genuinely different verbs (list / apply / revert / per-property apply / per-property revert) — exactly the case where action-dispatch shines. Add per-property actions if Ramon confirms they're needed; otherwise start with `list` + `apply-instance` + `revert-instance` and grow the action set later. If your v2.x backlog doesn't include override management, **Option 4** (defer) is also defensible.

**Your answer:** Option 2 — action-dispatched single tool `prefab-override`.

Rationale: matches the project precedent (Animation.ConfigureController) and the in-domain precedent (`prefab-modify-contents`); the override verbs are genuinely different (list/apply-instance/revert-instance/apply-property/revert-property) but share the same target (a prefab instance) and similar plumbing.

Scope this cycle: start with 3 actions — `list`, `apply-instance`, `revert-instance`. Per-property (`apply-property`, `revert-property`) and added/removed components/gameobjects deferred until a v2.x workflow actually needs them. Grow the action set on demand.

Implementation notes for the planner:
- `list` → `PrefabUtility.GetObjectOverrides(instanceRoot, includeDefaultOverrides = false)`. Cheap pre-check via `HasPrefabInstanceAnyOverrides` before doing the work.
- `apply-instance` → `PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction)`.
- `revert-instance` → `PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction)`.
- Note: when per-property actions land later, `ApplyObjectOverride` requires an `assetPath` argument because of nested-prefab ambiguity (which prefab in the nesting chain to apply to). That's a planner heads-up, not a v2.x scope decision.
- All actions need to validate the target is a prefab instance root (`PrefabUtility.IsAnyPrefabInstanceRoot`) and return a clear error otherwise.

---

### E6 — Extend headless prefab workflow (G4 + G6)

**Audit context:** G4 (`prefab-modify-contents` cannot set component field values — forces stage round-trip or instantiate-edit-apply workflow). G6 (no `prefab-unpack-instance` tool wrapping `PrefabUtility.UnpackPrefabInstance`). Both are "extend the headless prefab workflow" capability gaps.

**Question:** Add these two capabilities this cycle?

**Options:**
1. **Add both.** G4 → new `set-component-field` action on `prefab-modify-contents` (action-dispatched, fits the unified-tool direction from E2). G6 → new `prefab-unpack-instance(instanceId, objectPath, unpackMode)` tool. Both contained, ~80 lines combined.
2. **Add G4 only.** `set-component-field` action — closes the most-cited gap, defers unpack as YAGNI for v2.x.
3. **Add G6 only.** Unpack tool — small, isolated, low risk. Defer set-component-field as it interacts with serialization (and `SerializedObject` flows can be finicky).
4. **Defer both.** Keep this cycle to description fixes + the auto-decided sentinel/default migrations.

**My recommendation:** **Option 1** (add both). G4 is high-impact (priority #3 in the audit) and fits cleanly into the unified `prefab-modify-contents` tool as a new action. G6 is small and isolated (a wrapper over `PrefabUtility.UnpackPrefabInstance` with a mode enum). Combined cost is low and both close common Unity workflow gaps. **Option 2** (G4 only) is the conservative version if E5 (overrides) is also accepted as a big-scope add — keep total cycle scope manageable.

**Your answer:** Option 1 (with implementation constraint on G4) — add both G4 and G6.

G6 (`prefab-unpack-instance`): new tool, signature `prefab-unpack-instance(instanceId, objectPath, unpackMode)` where `unpackMode` is `"outermost"` or `"completely"` (mapping to `PrefabUnpackMode.OutermostRoot=0` and `PrefabUnpackMode.Completely=1`). Wraps `PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction)`. ~30 lines, fully isolated.

G4 (`set-component-field` action on `prefab-modify-contents`): added as the 6th action per E2's growth commitment, BUT scoped narrowly to avoid pre-empting the post-v2.0 backlog's "Object ↔ ScriptableObject ↔ Component generic-modifier triangle" cross-cutting decision.

Constraint for the planner on G4:
- Supported field types this cycle: primitives only (`int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Quaternion`, `enum`) + `ObjectReference` (other Unity Object). Reject anything else (custom struct, generic List/array of complex types, AnimationCurve, etc.) with a clear error message.
- Use `SerializedObject` + `FindProperty(fieldName)` + the type-specific setter (`.intValue`, `.objectReferenceValue`, etc.) + `ApplyModifiedPropertiesWithoutUndo` (we're inside `LoadPrefabContents` scope, undo is N/A here).
- Plan must include a NOTE FOR FUTURE CONSOLIDATION: when the Object/SO/Component triangle is resolved in v2.1.x, this action's implementation should be revisited to align with the canonical pattern (or extract a shared helper). Cite the post-v2.0 backlog item.

This way G4 closes the gap without committing to a position on the triangle prematurely.

---

### E7 — Prefab listing / discoverability (G5 + audit Open Question #2)

**Audit context:** G5 — no `prefab-list` tool in this domain. The capability technically exists in the Asset domain (`asset-find` with `t:Prefab` filter — file confirmed at `Editor/Tools/Asset/Tool_Asset.Find.cs`) but the LLM may not realize it. Audit Open Question #2 asks whether to add a Prefab-domain enumeration tool or cross-link `asset-find` in descriptions.

**Question:** How to address prefab discoverability?

**Options:**
1. **Description-only cross-link.** Add to `prefab-get-info` and `prefab-instantiate` `[Description]`: "To find prefabs by path or tag, use `asset-find` with `t:Prefab`." No new tool. Zero scope creep into Asset domain.
2. **New `prefab-list(folderPath, recursive)` tool.** Lightweight wrapper around `AssetDatabase.FindAssets("t:Prefab")`. Lives in Prefab domain (no Asset-domain edits). Slight duplication with `asset-find`.
3. **Defer to Asset-domain audit.** Treat this as "Asset domain owns enumeration." Cite the gap; no edit this cycle.

**My recommendation:** **Option 1** (description-only cross-link). The capability exists; the gap is discoverability. A cross-reference in `prefab-get-info` and `prefab-instantiate` descriptions is the cheapest and most-honest fix — it respects domain boundaries and doesn't create duplicated enumeration code. Option 2 is reasonable if you want zero cross-domain dependency in the LLM's mental model, but at the cost of two paths to the same answer.

**Your answer:** Option 1 — description-only cross-link.

Rationale: the enumeration capability already exists at `asset-find` with `t:Prefab`. The gap is discoverability for the LLM, not capability. A cross-reference in `prefab-get-info` and `prefab-instantiate` `[Description]` is the cheapest fix, respects domain boundaries, and avoids duplicated enumeration code. Asset-domain audit owns the broader enumeration story.

Concrete description nudge: append to both `prefab-get-info` and `prefab-instantiate` descriptions: "To enumerate or search for prefabs by path/filter, use `asset-find` with `t:Prefab`."

---

## 6. Change Group Hints

The planner can use this grouping as a starting point. Order Groups A→B→C→D→E→F→G→H represents low-risk-first to highest-scope-last; the planner may re-sequence if it finds a better order.

### Group A — Description polish (ship first)

- **Findings:** A1, A2, A3, A5, D1, D4
- **Scope:** Pure `[Description]` text edits on existing tools.
  - A1: expand `prefab-modify-contents` method-level `[Description]` with per-action required-param map.
  - A2: tighten `prefab-create` `[Description]` re: `instanceId` vs `objectPath` precedence.
  - A3: tighten `prefab-save` `[Description]` re: do-not-call-after-`prefab-modify-contents`.
  - A5: tighten `targetChild` / `deleteChild` `[Description]` on `prefab-modify-contents`.
  - D1: tighten `savePath` `[Description]` re: `Assets/{name}.prefab` implicit default.
  - D4: tighten `keepConnection` `[Description]` rationale.
- **Risk:** zero — no signature or behavior change. Pure documentation.

### Group B — Sentinel + default migrations on `prefab-modify-contents`

- **Findings:** A6, D2
- **Scope:**
  - A6: migrate `isActive` from `int -1/0/1` to nullable string `"true" | "false" | ""`. Error only when `action = "set-active"` and value is `""`; otherwise the empty sentinel is silently ignored for non-set-active actions.
  - D2: change `action = "set-position"` default → `action = ""` + error listing valid actions.
- **Risk:** signature breaks on `prefab-modify-contents`. Clean breaks per GameObject precedent (Section 3 box 3).

### Group C — Cross-domain consolidation: delete `add-asset-to-scene`

- **Findings:** R1, A4, D3 (per E1)
- **Scope:**
  - Delete `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` and the `AddAssetToScene` folder (verify no other code references it first).
  - Add `rotX/rotY/rotZ` params (defaults 0) to `prefab-instantiate`.
  - Add non-prefab GameObject asset fallback to `prefab-instantiate`: check `PrefabUtility.GetPrefabAssetType(loadedAsset)`; if not a prefab, fall back to `Object.Instantiate`.
  - Keep `parentPath` (do not rename).
- **Risk:** surface break — `add-asset-to-scene` tool name disappears. Clean break per GameObject precedent.

### Group D — `prefab-get-info` enrichment

- **Findings:** A7, G3, plus the `isVariant` output field folded in from E4
- **Scope:**
  - Add `int maxDepth = -1` param (-1 = unlimited, preserves current behavior).
  - Document plain-text output format in `[Description]`.
  - In `AppendHierarchy`, per-node call `PrefabUtility.IsAnyPrefabInstanceRoot(node)` and prefix output rows with `[nested-prefab]` where true.
  - Add `isVariant: true/false` to the top-level output via `PrefabUtility.GetPrefabAssetType(loadedAsset) == PrefabAssetType.Variant`.
- **Risk:** additive — new param has a back-compat default, output gains a field but doesn't remove any.

### Group E — Headless workflow extensions

- **Findings:** G4, G6 (per E6)
- **Scope:**
  - **G4:** add `set-component-field` as the 6th action on `prefab-modify-contents`. Field-type scope: primitives only (`int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Quaternion`, `enum`) + `ObjectReference`. Reject other types with clear error. Use `SerializedObject` + `FindProperty(fieldName)` + type-specific setter + `ApplyModifiedPropertiesWithoutUndo`. **Plan MUST include a FUTURE CONSOLIDATION comment in the generated code** citing the post-v2.0 Object/SO/Component generic-modifier triangle for v2.1.x revisit.
  - **G6:** new `prefab-unpack-instance(instanceId, objectPath, unpackMode)` tool. `unpackMode` is `"outermost"` or `"completely"`. Wraps `PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction)`. ~30 lines, fully isolated.
- **Risk:** additive — G4 adds an action (existing actions untouched); G6 is a brand-new tool.

### Group F — Variants

- **Findings:** G1 (per E4)
- **Scope:**
  - New `prefab-create-variant(instanceId, objectPath, savePath)` tool. Validate target is a prefab instance root via `PrefabUtility.IsAnyPrefabInstanceRoot`; then call `PrefabUtility.SaveAsPrefabAsset(instance, path, out success)` which automatically creates a Variant when the input is a prefab instance root. ~30 lines.
  - The companion `prefab-is-variant` read tool is **dropped** — read side handled in Group D via the `isVariant` flag in `prefab-get-info` output.
- **Risk:** additive — new tool, no existing surface change.

### Group G — Overrides

- **Findings:** G2 (per E5)
- **Scope:**
  - New `prefab-override` action-dispatched tool with 3 actions this cycle: `list`, `apply-instance`, `revert-instance`. Per-property actions (`apply-property`, `revert-property`) and added/removed components/gameobjects deferred to v2.1.x.
  - `list` → `PrefabUtility.GetObjectOverrides(instanceRoot, includeDefaultOverrides = false)` with cheap pre-check via `HasPrefabInstanceAnyOverrides`.
  - `apply-instance` → `PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.AutomatedAction)`.
  - `revert-instance` → `PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction)`.
  - All actions validate target is a prefab instance root via `PrefabUtility.IsAnyPrefabInstanceRoot` and return a clear error otherwise.
  - Use `Debug.LogWarning` (NOT `McpLogger.Warning` — it doesn't exist) for any warning paths.
- **Risk:** additive — new tool, no existing surface change.

### Group H — Discoverability cross-link

- **Findings:** G5 (per E7)
- **Scope:**
  - Append to `prefab-get-info` `[Description]`: "To enumerate or search for prefabs by path/filter, use `asset-find` with `t:Prefab`."
  - Append the same line to `prefab-instantiate` `[Description]`.
- **Risk:** zero — description-only edits.

---

## 7. Notes For The Planner

**Cross-domain dependencies (carry forward, do NOT edit here):**

- `Tool_Prefab.Create.cs` line 37 calls `Tool_Transform.FindGameObject(instanceId, objectPath)`. This dependency stays as-is. Do NOT migrate `Tool_Transform.FindGameObject` off `EditorUtility.InstanceIDToObject` in this cycle — flag for Transform audit (same heads-up as the GameObject review).
- `add-asset-to-scene` (`Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs`) is **deleted this cycle** per E1 / Group C. Verify no other code references it before deletion.
- `asset-find` (Asset domain) is cite-only per Section 3. Group H description cross-links it; no Asset-domain code edits.

**GameObject precedent applied directly:**

- **A6 sentinel migration** follows the GameObject E3 resolution. `isActive: int` → `isActive: string` with `"true" | "false" | ""` semantics. Same migration shape as `gameobject-update`'s `isActive` / `isStatic`.
- **D2 required-action** follows the principle that arbitrary defaults on `string action` parameters silently corrupt state when the LLM omits the param. `action = ""` + error listing valid actions is the established pattern.
- **Clean breaks over shims** — GameObject deleted `gameobject-select` outright. Same approach for `add-asset-to-scene` (Group C).

**FUTURE CONSOLIDATION note (required on G4 / Group E):**

The `set-component-field` action's implementation MUST include a code comment citing the post-v2.0 backlog's "Object ↔ ScriptableObject ↔ Component generic-modifier triangle" cross-cutting decision. When that triangle is resolved in v2.1.x, this action should be revisited to align with the canonical pattern (or extract a shared helper). Example placement: a block comment immediately above the `SerializedObject` + `FindProperty` block, marked `// TODO(v2.1.x): align with Object/SO/Component generic-modifier triangle — see post-v2.0 backlog.`

**Out-of-scope reminders (from CLAUDE.md):**

- Pipeline officially paused for v2.0. Prefab is a second one-off exception after GameObject. Do NOT pull in scope from Asset, Component, Script, Scene, or Transform domains.
- Ramon owns all git operations. The consolidator agent edits files only.
- `McpLogger` has only `Info` and `Error` — use `Debug.LogWarning` for warnings in any new error paths (e.g. inside override-suite tools added under E5/Group G).
- Filesystem MCP unreliable on `@` paths (PackageCache). Reference repo paths under `Editor/Tools/Prefab/` and `Editor/Tools/AddAssetToScene/`.

**Pattern reuse:**

- Headless prefab edit flow: `PrefabUtility.LoadPrefabContents` → mutate → `PrefabUtility.SaveAsPrefabAsset` → `PrefabUtility.UnloadPrefabContents`. Current canonical implementation in `Tool_Prefab.ModifyContents.cs`. The new `set-component-field` action (Group E / G4) extends this same skeleton with a 6th action branch. Reuse the `try`/finally `UnloadPrefabContents` cleanup discipline.
- Stage-based prefab edit flow: `prefab-open` → tools acting on the prefab stage → `prefab-save` → `prefab-close`. Current canonical implementation in `Tool_Prefab.Open.cs` / `Save.cs` / `Close.cs`. New override-suite tools (Group G / E5) operate on scene instances directly without entering a stage — they don't need this flow.
- Symmetric lookup: `prefab-create-variant` (Group F) and `prefab-unpack-instance` (Group E / G6) both use `(instanceId, objectPath, ...)` per the established domain convention via the `Tool_Transform.FindGameObject` helper.

---

## 8. Approval

**Status:** READY FOR PLANNING
