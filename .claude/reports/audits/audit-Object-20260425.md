# Audit Report — Object

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Object/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/Object/Tool_Object.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- All absence claims below are made over a fully-covered domain (2/2 files analyzed). Cross-domain claims (e.g. "no `EntityIdToObject` in Object domain") are also fully verified via Grep over the entire `Editor/Tools/` tree.

**Reviewer guidance:**
- This domain is intentionally generic — its tools target any `UnityEngine.Object` by `instanceId`. The most consequential findings are: (1) overlap with `scriptableobject-*` and `component-*` domains, which already do the same thing scoped to specific subclasses; (2) use of the deprecated `InstanceIDToObject` API in violation of project standards; and (3) capability gaps for value types Unity exposes via the inspector but the domain does not handle (e.g. Quaternion writes, array length writes, sub-asset resolution for object refs).
- The two tools form a tightly-paired read/write pair (`object-get-data` / `object-modify`). Treat them as a unit for any consolidation decision.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `object-get-data` | Object / Get Data | `Tool_Object.GetData.cs` | 1 (`instanceId:int`) | yes |
| `object-modify` | Object / Modify | `Tool_Object.Modify.cs` | 2 (`instanceId:int`, `propertiesJson:string`) | no |

**Internal Unity API surface used:**
- `EditorUtility.InstanceIDToObject(int)` — deprecated in Unity 6000.3 (project standard mandates `EntityIdToObject`)
- `SerializedObject` / `SerializedProperty` (read & write iteration)
- `Undo.RecordObject` (Modify only)
- `EditorUtility.SetDirty`
- `AssetDatabase.LoadAssetAtPath<Object>` (for object-reference resolution in Modify)

**Param description quality (raw):**
- `object-get-data.instanceId`: "Instance ID of the Unity object to inspect (e.g. from a GameObject or Component)." (good — has hint about source)
- `object-modify.instanceId`: "Instance ID of the Unity object to modify." (terse)
- `object-modify.propertiesJson`: includes inline example `{"damage":25,"label":"Boss","tint":"1,0,0,1"}` (good)

---

## 2. Redundancy Clusters

### Cluster R1 — Generic vs. typed property inspection
**Members:** `object-get-data` (Object), `scriptableobject-inspect` (ScriptableObject), `component-get` (Component)
**Overlap:** All three iterate `SerializedObject.GetIterator()` and pretty-print each visible `SerializedProperty`'s path/type/value. The implementations of the per-property formatting helper (`GetPropertyValueString`) are near-duplicates between `Tool_Object.GetData.cs` (lines 76-130) and `Tool_ScriptableObject.Inspect.cs` (lines 83-100); `component-get` does the same thing inline with a slightly different format. The only behavioural differences are: (a) how the target object is resolved (instance id vs. asset path vs. GameObject+component type), (b) whether `m_Script` is filtered (ScriptableObject filters; Object does not).
**Impact:** When the LLM has an instance id of a ScriptableObject or a Component, three tools become candidates for "show me its properties." Tool selection becomes guess-driven. Maintenance burden: any improvement to the formatter (e.g. truncating long strings, adding LayerMask support) must be made in three places.
**Confidence:** high

### Cluster R2 — Generic vs. typed property modification
**Members:** `object-modify` (Object), `scriptableobject-modify` (ScriptableObject), `component-update` (Component)
**Overlap:** All three resolve a target, find a SerializedProperty by path, parse a string value into a typed value, call `ApplyModifiedProperties`, and `SetDirty`. `object-modify` and `component-update` both accept a flat-JSON property map (and both ship their own JSON parser); `scriptableobject-modify` accepts a single `propertyPath`/`value` pair. The supported value-type matrices are nearly identical (int/float/bool/string/vector2/vector3/color/object-ref) but each implementation is slightly different — e.g. `object-modify` supports enums by name OR index (lines 139-154), `scriptableobject-modify` does not (per the description). Only `scriptableobject-modify` calls `AssetDatabase.SaveAssets()` after applying.
**Impact:** Heavy ambiguity — for any ScriptableObject or Component, two tools work. The LLM has no signal about which to prefer. Subtle behaviour differences (single-property vs. batch, enum-by-name support, asset-save side effect) are not documented in any of the three descriptions.
**Confidence:** high

### Cluster R3 — Internal duplication: flat-JSON parser
**Members:** `Tool_Object.Modify.cs` `ParseFlatJson` (lines 217-314), `Tool_Component.Update.cs` (separate copy — Grep confirms only these two files in `Editor/Tools/` define such a parser)
**Overlap:** Two hand-rolled flat-JSON parsers in the codebase, doing the same job. Not visible to the LLM (so no tool-selection ambiguity), but it is a redundancy that affects how a refactor of either domain should proceed.
**Impact:** Low for the LLM; medium for maintenance. Worth flagging because consolidation of R2 would naturally absorb this too.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — Description does not disambiguate `object-get-data` from `scriptableobject-inspect` / `component-get`
**Location:** `object-get-data` — `Tool_Object.GetData.cs` lines 26-27
**Issue:** No "use this when X, not Y" clause. A reader (LLM) cannot tell whether to prefer this generic tool or the typed alternatives when the instance is known to be a Component or a ScriptableObject.
**Evidence:** `[Description("Resolves a Unity object by instance ID and returns all visible serialized properties with their paths, types, and current values. Works with any UnityEngine.Object subclass.")]` — the closing phrase "any UnityEngine.Object subclass" actively invites overlap rather than narrowing intent.
**Confidence:** high

### A2 — Description does not disambiguate `object-modify` from `scriptableobject-modify` / `component-update`
**Location:** `object-modify` — `Tool_Object.Modify.cs` line 30
**Issue:** Same as A1: no positioning relative to the typed siblings. Also does not document the side-effect difference (does NOT call `AssetDatabase.SaveAssets`, unlike `scriptableobject-modify`).
**Evidence:** `[Description("Modifies multiple serialized properties on any Unity object identified by instance ID. propertiesJson is a flat JSON object mapping property paths to string-encoded values. Supports int, float, bool, string, enum (by index or name), Vector2/3, Color, and object references (by asset path).")]`
**Confidence:** high

### A3 — `propertiesJson` value-encoding rules are listed but not exhaustively defined
**Location:** `object-modify` param `propertiesJson` — line 33
**Issue:** Description lists supported types but does not explain (a) that values must be strings (the example `{"damage":25,...}` suggests numeric literals are allowed, and the parser does accept them — but the description says "string-encoded"); (b) the comma-separated format for vectors/colors; (c) that object references must be asset paths (so scene-only references are not addressable); (d) Vector4 / Quaternion / Bounds / Rect / LayerMask / AnimationCurve are silently unsupported.
**Evidence:** Param description: "JSON object mapping serialized property paths to new string-encoded values. Example: {\"damage\":25,\"label\":\"Boss\",\"tint\":\"1,0,0,1\"}" — example mixes a number literal `25` with a string `"1,0,0,1"`, which is internally consistent with the parser but not explained.
**Confidence:** high

### A4 — `instanceId` description does not explain how to obtain one
**Location:** `object-modify` param `instanceId` — line 32
**Issue:** Description is "Instance ID of the Unity object to modify." — no hint that the LLM should call `object-get-data`, `component-get`, `gameobject-find`, or similar to acquire one. The companion description in `object-get-data` is slightly better ("e.g. from a GameObject or Component") but still vague.
**Evidence:** Cited above.
**Confidence:** medium

### A5 — `object-get-data` does not warn that very large objects produce huge listings
**Location:** `object-get-data` — `Tool_Object.GetData.cs` line 27
**Issue:** Iterates all visible serialized properties of any object with no truncation, depth limit, or filter (compare with `scriptableobject-inspect` which at least skips `m_Script`). For complex assets (e.g. a Mesh, AnimatorController, or large ScriptableObject), the response can be very long. Description does not warn the LLM, nor offer a filter.
**Evidence:** Lines 52-61: `while (iterator.NextVisible(enterChildren)) { ... sb.AppendLine(...); }` with no size guard, no `m_Script` skip, and no path-prefix filter parameter.
**Confidence:** high

### A6 — `object-get-data` does not skip `m_Script` (cosmetic noise)
**Location:** `Tool_Object.GetData.cs` lines 52-61
**Issue:** Every Unity object inspection includes `m_Script` as the first row, which is rarely useful. `scriptableobject-inspect` filters it (line 60 of that file). Inconsistency between sibling tools.
**Evidence:** No filter clause in the iteration loop.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `object-modify.propertiesJson` is required but declared without default
**Location:** `object-modify` param `propertiesJson`
**Issue:** Declared as a positional `string` with no default. By project convention (compare `component-update`, which sets `propertiesJson = ""`), required-by-intent strings often still get a `""` default and runtime validation. Minor consistency issue. Not a bug — body has a `string.IsNullOrWhiteSpace(propertiesJson)` check.
**Current:** `[Description(...)] string propertiesJson` (no default)
**Suggested direction:** Make signature consistent with `component-update` style if Ramon prefers default-then-validate; otherwise, leave alone — it is technically required.
**Confidence:** low

### D2 — `instanceId` has no sensible default and no fallback path
**Location:** `object-get-data` and `object-modify` params `instanceId`
**Issue:** Object domain has no `objectPath` fallback (compare `component-get` / `component-update` which accept `instanceId=0` then resolve via `objectPath`). For scene objects this is fine because the LLM normally resolves by other means, but for asset objects (e.g. ScriptableObjects, Materials), there is no analogue — the LLM must know the instance id ahead of time.
**Current:** `int instanceId` (required, no default).
**Suggested direction:** Consider an `assetPath` fallback parameter so an LLM can address an asset directly. (Not actionable here — flagged for the planner.)
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Use of deprecated `InstanceIDToObject` (project-standard violation)
**Workflow:** Resolve a Unity object from an instance id.
**Current coverage:** Both Object tools call `EditorUtility.InstanceIDToObject(instanceId)`.
**Missing:** Project convention (`CLAUDE.md` C# Coding Standards, "Language Features (Unity 6000.3 / C# 9.0)") explicitly mandates `EntityIdToObject`. Across `Editor/Tools/`, 6 files already use `EntityIdToObject`; only 3 still call the deprecated form, and 2 of those 3 are in this domain.
**Evidence:**
- `Tool_Object.GetData.cs` line 39: `var obj = EditorUtility.InstanceIDToObject(instanceId);`
- `Tool_Object.Modify.cs` line 48: `var obj = EditorUtility.InstanceIDToObject(instanceId);`
- `#pragma warning disable CS0618` at the top of both files (lines 1) — the deprecation is being suppressed rather than fixed.
**Confidence:** high

### G2 — `object-modify` cannot write Vector4, Quaternion, Rect, Bounds, LayerMask, AnimationCurve, ArraySize
**Workflow:** Set a Vector4 (e.g. shader uniform), Quaternion (e.g. cached rotation field), LayerMask (e.g. raycast mask on a Component), or resize a serialized array (e.g. add an entry to `Renderer.sharedMaterials`).
**Current coverage:** `ApplyPropertyValue` (lines 111-208) handles Integer / Float / Boolean / String / Enum / Vector2 / Vector3 / Color / ObjectReference. All other `SerializedPropertyType` values fall to `default: return false;` (line 205-206).
**Missing:** Branches for `Vector4`, `Vector2Int`, `Vector3Int`, `Quaternion`, `Rect`, `RectInt`, `Bounds`, `BoundsInt`, `LayerMask` (handled as `SerializedPropertyType.LayerMask`), `ArraySize` (which would let the LLM resize arrays before writing entries), `AnimationCurve`. The reader (`GetPropertyValueString`, lines 76-130) DOES expose these types — meaning the LLM can SEE them but cannot WRITE them, which is a confusing partial-coverage gap.
**Evidence:**
- Reader, line 99-119: handles Vector2/3/4, Rect, Color, Bounds, Quaternion, Vector2Int, Vector3Int, RectInt, BoundsInt, ArraySize.
- Writer, line 113-207: only Integer, Float, Boolean, String, Enum, Vector2, Vector3, Color, ObjectReference.
**Confidence:** high

### G3 — `object-modify` ObjectReference resolution only handles main asset, not sub-assets
**Workflow:** Set a property to point at a sub-asset, e.g. a sprite inside a multi-sprite texture, a sub-mesh of an FBX, an AnimationClip nested inside an FBX, a sub-asset ScriptableObject.
**Current coverage:** Line 196: `var referenced = AssetDatabase.LoadAssetAtPath<Object>(value);` — `LoadAssetAtPath<Object>` returns the **main** asset at that path. Sub-assets cannot be resolved this way.
**Missing:** No mechanism to address sub-assets. Unity's typical approach: `LoadAllAssetsAtPath` then filter by `name` and/or `type`, or accept `path::name` syntax.
**Evidence:** Line 196 quoted above. The `[Description]` says "object references (by asset path)" with no mention of the limitation.
**Confidence:** high

### G4 — Cannot target objects in scenes that are not loaded, nor sub-objects (Components, child GameObjects) of a loaded scene without already knowing their instance id
**Workflow:** "Modify property X on the Mesh inside FBX Y" or "modify property X on the Component of type Z on the GameObject at hierarchy path /Player".
**Current coverage:** `object-modify` requires `instanceId` only — no way to reach into an asset/scene by name/path/type and obtain the right object.
**Missing:** A resolver-style fallback (asset path, asset path + sub-name, or hierarchy path + component type). Sibling domains (Component, ScriptableObject) handle this for their own scopes. The generic Object domain does not.
**Evidence:** `Tool_Object.Modify.cs` lines 31-34: parameters are `instanceId` and `propertiesJson` only.
**Confidence:** high

### G5 — No batch read / no path-prefix filter on `object-get-data`
**Workflow:** "Show me only the `material.*` properties on this Renderer" or "show me the contents of the `stats` sub-object on this ScriptableObject."
**Current coverage:** None — `object-get-data` takes only `instanceId` and emits the full property tree.
**Missing:** Optional `pathPrefix`, `maxDepth`, or `paths: string[]` parameter. Without one, the LLM must consume the entire property tree even when it only needs one branch — wasting context budget on large assets.
**Evidence:** `Tool_Object.GetData.cs` line 28-30: only `instanceId` is accepted.
**Confidence:** medium (workflow is plausible but not unambiguously needed; flagged for human judgement)

### G6 — `object-modify` does not register Undo for nested asset modifications consistently with `scriptableobject-modify`
**Workflow:** Modify a ScriptableObject via `object-modify`, then expect the asset to be saved to disk so a subsequent build/test sees the change.
**Current coverage:** `object-modify` calls `Undo.RecordObject` + `ApplyModifiedProperties` + `EditorUtility.SetDirty`. It does NOT call `AssetDatabase.SaveAssets()`. By contrast, `scriptableobject-modify` does (per its file). For scene objects this is correct; for asset objects (ScriptableObjects, Materials, AnimationClips, etc.) the change persists only until next domain reload unless something else triggers a save.
**Missing:** Either an explicit save-on-asset branch, or an optional `save: bool` parameter, or clear documentation of the behaviour difference.
**Evidence:** `Tool_Object.Modify.cs` lines 92-93: `serializedObj.ApplyModifiedProperties(); EditorUtility.SetDirty(obj);` — no `SaveAssets`.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort)

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability/Standards | 5 | 1 | 25 | high | Replace `InstanceIDToObject` with `EntityIdToObject`; remove `#pragma warning disable CS0618` |
| 2 | R2 | Redundancy | 5 | 4 | 10 | high | `object-modify`, `scriptableobject-modify`, `component-update` overlap heavily — consolidate or clearly disambiguate |
| 3 | G2 | Capability Gap | 4 | 2 | 16 | high | Add Vector4/Quaternion/LayerMask/Rect/Bounds/Vector*Int/ArraySize branches to the writer |
| 4 | A2 | Ambiguity | 4 | 1 | 20 | high | Add "use this when X, not Y" disambiguation to `object-modify` description |
| 5 | A1 | Ambiguity | 4 | 1 | 20 | high | Add "use this when X, not Y" disambiguation to `object-get-data` description |
| 6 | G6 | Capability Gap | 4 | 2 | 16 | high | Document/handle the missing `SaveAssets` for asset-typed targets |
| 7 | R1 | Redundancy | 3 | 3 | 9 | high | `object-get-data`, `scriptableobject-inspect`, `component-get` triplicate inspection logic |
| 8 | G3 | Capability Gap | 3 | 2 | 12 | high | Sub-asset references (sprites, sub-meshes) cannot be set by asset path alone |
| 9 | A3 | Ambiguity | 3 | 1 | 15 | high | `propertiesJson` encoding rules are partial; document each type's expected string form |
| 10 | A5 | Ambiguity | 3 | 1 | 15 | high | Warn LLM that `object-get-data` is unbounded; offer filter parameters |
| 11 | A6 | Ambiguity/Defect | 2 | 1 | 10 | high | Skip `m_Script` in `object-get-data` to match `scriptableobject-inspect` |
| 12 | R3 | Redundancy (internal) | 2 | 3 | 6 | high | Two flat-JSON parsers in `Object/Modify` and `Component/Update`; share via Helpers |
| 13 | G4 | Capability Gap | 3 | 4 | 6 | medium | No path/type resolver — must already have `instanceId` |
| 14 | G5 | Capability Gap | 2 | 2 | 8 | medium | No path-prefix or depth filter on `object-get-data` |
| 15 | A4 | Ambiguity | 2 | 1 | 10 | medium | `instanceId` description does not point the LLM at how to obtain one |
| 16 | D2 | Default | 2 | 3 | 6 | medium | No `assetPath` fallback for asset-typed targets |
| 17 | D1 | Default | 1 | 1 | 5 | low | Required-by-intent `propertiesJson` lacks a `""` default+validate pattern (consistency only) |

---

## 7. Notes

**Cross-domain dependencies noticed:**
- This domain is conceptually a generalisation of `Component` (`component-get` / `component-update`) and `ScriptableObject` (`scriptableobject-inspect` / `scriptableobject-modify`). Any consolidation decision for the Object domain is necessarily a decision about all three. The reviewer should consider whether the Object domain is meant to be the single canonical entry point (with the typed siblings deprecated) or a fallback (in which case its description must say so explicitly — see A1, A2).
- The `Reflect` domain (`Tool_Reflect.CallMethod.cs`, line 143) also resolves objects from instance ids — same `EntityIdToObject` pattern — and could share a tiny resolver helper. Flagged but not in scope.
- `Tool_Component.Update.cs` and `Tool_Object.Modify.cs` both ship a hand-rolled flat-JSON parser. If R2 is consolidated, R3 is naturally fixed; if not, the parser should still be hoisted to `Editor/Tools/Helpers/`.

**Workflows intentionally deferred:**
- I did not test runtime behaviour. Findings are based on static reading only. In particular, claims about `LoadAssetAtPath<Object>` not resolving sub-assets (G3) and about persistence requiring `SaveAssets` (G6) follow Unity API documentation — they are stated with high confidence but not verified by execution in this audit.

**Open questions for the reviewer:**
1. Is the Object domain meant to be the canonical generic fallback, or is it the user-facing entry point that should subsume `component-update` and `scriptableobject-modify`? The answer determines whether R1/R2 are "fix the descriptions" (Tier 1) or "merge the tools" (Tier 4) work.
2. For asset-typed targets, should `object-modify` opportunistically call `AssetDatabase.SaveAssets()`, or expose a `save:bool=true` parameter? (G6)
3. Is the deprecated `InstanceIDToObject` use intentional (some compatibility reason) or just stale? `#pragma warning disable CS0618` at line 1 of both files suggests it was a deliberate suppression at some point. (G1)

**Honest limits of this audit:**
- I am confident in coverage (2/2 files in domain, plus targeted reads of all overlapping siblings). I am confident in the redundancy and ambiguity findings. The capability-gap findings G2-G6 are plausible from API knowledge but the priorities depend on Ramon's view of how often each workflow comes up in practice.
