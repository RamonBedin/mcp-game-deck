# Audit Report — ScriptableObject

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/ScriptableObject/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 4 (via `Glob Editor/Tools/ScriptableObject/Tool_ScriptableObject.*.cs`)
- `files_read`: 4
- `files_analyzed`: 4

**Balance:** balanced

**Errors encountered during audit:** None.

**Files not analyzed:** None.

**Cross-domain reads (for context only, not audited here):**
- `Editor/Tools/Object/Tool_Object.GetData.cs` — overlaps with SO.Inspect
- `Editor/Tools/Object/Tool_Object.Modify.cs` — overlaps with SO.Modify
- `Editor/Tools/Asset/Tool_Asset.Find.cs` — overlaps with SO.List
- `Editor/Tools/Asset/Tool_Asset.Create.cs` — confirmed it does NOT cover ScriptableObject types (so SO.Create is justified)
- `Editor/Tools/Asset/Tool_Asset.GetInfo.cs`, `Tool_Asset.Delete.cs`, `Tool_Asset.Move.cs`, `Tool_Asset.Rename.cs` — present, cover lifecycle for any asset

**Absence claims in this report:**
- All four files were read; absence claims regarding the SO domain are made with full coverage.

**Reviewer guidance:**
- The single biggest finding is structural overlap between this domain and the `Object` domain. The `Object.Modify` / `Object.GetData` tools accept any `UnityEngine.Object` instance ID and operate on `SerializedObject` — which means SO.Inspect and SO.Modify are largely redundant generic wrappers limited to a single asset path lookup. Because asset-path → instanceId requires a separate hop, there is still some user-experience value in keeping SO-specific tools, but they add concept duplication and divergent property-type coverage that the LLM has to reason about.
- The `Object.Modify` tool is a strict superset of `SO.Modify` in batch capability (multi-property JSON map vs. one-property-per-call) but uses identical `SerializedObject` plumbing. The two `ApplyPropertyValue`/`SetPropertyValue` helpers are nearly line-for-line duplicates with subtle differences (e.g. `Object.Modify` accepts `"1"` for booleans; `SO.Modify` does not).
- Property-type coverage in both modify/inspect helpers is limited: no `Vector4`, `Quaternion`, `Rect`, `Bounds`, `Vector2Int`, `Vector3Int`, `LayerMask`, `AnimationCurve`, `Gradient`, `ManagedReference`, or array element insertion/deletion. This is a real capability gap because typical ScriptableObject configs (GameplayConfig with curves, balance tables with arrays, config trees with managed references) cannot be authored end-to-end without these.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `scriptableobject-create` | ScriptableObject / Create | `Tool_ScriptableObject.Create.cs` | 4 | no |
| `scriptableobject-inspect` | ScriptableObject / Inspect | `Tool_ScriptableObject.Inspect.cs` | 1 | no (should be yes) |
| `scriptableobject-list` | ScriptableObject / List | `Tool_ScriptableObject.List.cs` | 3 | no (should be yes) |
| `scriptableobject-modify` | ScriptableObject / Modify Property | `Tool_ScriptableObject.Modify.cs` | 3 | no |

**Tool method details:**

- **`Create`** — params: `typeName: string`, `folderPath: string`, `assetName: string`, `overwrite: bool = false`. Unity APIs: `AppDomain.CurrentDomain.GetAssemblies()`, `Type.GetType`, `AssetDatabase.IsValidFolder/CreateFolder/CreateAsset/SaveAssets/Refresh`, `ScriptableObject.CreateInstance(Type)`.
- **`Inspect`** — params: `assetPath: string`. Unity APIs: `AssetDatabase.LoadAssetAtPath<ScriptableObject>`, `SerializedObject.GetIterator`, `SerializedProperty.NextVisible`. Skips `m_Script`. Helper `GetPropertyValueString` covers Integer, Float, Boolean, String, Enum, Vector2/3/4, Color, ObjectReference, ArraySize. Unrecognised types print `<PropertyType>`.
- **`List`** — params: `typeName: string = ""`, `folderPath: string = ""`, `maxResults: int = 50`. Unity APIs: `AssetDatabase.FindAssets("t:ScriptableObject", ...)`, `AssetDatabase.LoadAssetAtPath<ScriptableObject>`. Type filter is post-filter (case-insensitive `Contains` on `GetType().Name`).
- **`Modify`** — params: `assetPath: string`, `propertyPath: string`, `value: string`. Unity APIs: `AssetDatabase.LoadAssetAtPath<ScriptableObject>`, `SerializedObject.FindProperty/ApplyModifiedProperties`, `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`. Helper `SetPropertyValue` covers Integer, Float, Boolean, String, Enum, Vector2, Vector3, Color, ObjectReference. **No `Undo.RecordObject`** call (Object.Modify includes one).

`ReadOnlyHint`: zero of four tools have it set. (Verified via `Grep ReadOnlyHint` over the domain — zero matches.)

---

## 2. Redundancy Clusters

### Cluster R1 — Inspect / Modify duplicate the generic Object tools
**Members:** `scriptableobject-inspect`, `scriptableobject-modify`, plus cross-domain `object-get-data`, `object-modify`
**Overlap:** SO.Inspect and Object.GetData both iterate visible serialized properties via `SerializedObject` and print path/type/value. SO.Modify and Object.Modify both apply string-encoded values via `SerializedProperty` using nearly identical `SetPropertyValue` / `ApplyPropertyValue` switches. The only structural difference is:
- SO tools take `assetPath` and `LoadAssetAtPath<ScriptableObject>`
- Object tools take `instanceId` and `EditorUtility.InstanceIDToObject`
Object.Modify additionally accepts a JSON map of multiple property assignments per call; SO.Modify is one-property-per-call.
**Impact:** The LLM has two near-identical tools to reason about. For a typical "edit a SO field" intent, either tool works; it must guess which. Worse, the two implementations diverge in subtle ways:
- `Object.Modify` accepts `"1"` as `true`; `SO.Modify` requires literal `"true"`/`"false"`.
- `Object.Modify` calls `Undo.RecordObject(...)`; `SO.Modify` does not (asymmetric undo behaviour for the same operation depending on which tool is chosen).
- Both helpers have to be maintained in lockstep when a new property type is added.
**Confidence:** high (verified by reading both files)

### Cluster R2 — List partially duplicates Asset/Find
**Members:** `scriptableobject-list`, plus cross-domain `asset-find`
**Overlap:** `asset-find` accepts Unity search filter syntax (`t:WeaponConfig`, `t:Texture sky`, `l:label`). For listing all SOs of a given type, `asset-find` with `searchFilter = "t:WeaponConfig"` produces the same result as `scriptableobject-list typeName:WeaponConfig`. SO.List adds: case-insensitive partial type matching, and listing across all SO subclasses (`t:ScriptableObject`).
**Impact:** Lower than R1 — the partial-match and "all SO subclasses" behaviour is genuinely different from Asset.Find's exact-type filter. But for the common case ("list all WeaponConfig assets") the two tools collide. The LLM may pick Asset.Find and never discover SO.List.
**Confidence:** medium (the partial-match behaviour is a meaningful differentiator; consolidation may not be desirable)

---

## 3. Ambiguity Findings

### A1 — Modify value encoding is undocumented for several edge cases
**Location:** `scriptableobject-modify` — `Tool_ScriptableObject.Modify.cs` line 39
**Issue:** The `value` parameter description enumerates `numbers / true|false / x,y,z / r,g,b,a / asset path` but does not mention enums (which the implementation supports both by index and by case-insensitive name). It also omits ArraySize (which is `Integer`-typed at the SerializedProperty level but reads as `ArraySize` in Inspect output, creating a confusing asymmetry). And it does not mention that boolean parsing is strict (`"1"` does NOT work, unlike the Object/Modify counterpart).
**Evidence:** Description string at line 39: `"Value to set. For numbers use the number, for bools use 'true'/'false', for vectors use 'x,y,z', for colors use 'r,g,b,a' (0-1 range), for object references use the asset path."`
**Confidence:** high

### A2 — Modify silently restricts supported property types
**Location:** `scriptableobject-modify` — `Tool_ScriptableObject.Modify.cs` lines 19-21, 35
**Issue:** The summary says "Supports int, float, string, bool, Vector2, Vector3, Color, and object reference fields" — but the implementation also supports Enum (lines 124-139). Conversely, the description gives no signal that Vector4/Quaternion/Rect/Bounds/AnimationCurve/Gradient/LayerMask/ManagedReference will fail with a generic `Could not set property` error. The LLM has no way to know which property types fail without trial-and-error.
**Evidence:** Description (line 35) lists 8 types; implementation switch covers 9 types; absent types fall into `default: return false` (line 190-191) producing only `"Could not set property '...' (type: X) to '...'."`.
**Confidence:** high

### A3 — Inspect description does not mention the m_Script skip
**Location:** `scriptableobject-inspect` — `Tool_ScriptableObject.Inspect.cs` line 26
**Issue:** Inspect skips the `m_Script` property silently (line 60-63). This is correct behaviour but invisible to the caller. If the LLM asks "what's in this SO?" and gets back a list missing `m_Script`, it has no way to know whether the asset has its script reference or not (e.g. a corrupted SO whose `m_Script` is null would look identical to a healthy one).
**Evidence:** Skip at line 60-63; description at line 26 makes no mention.
**Confidence:** medium (low-impact in practice, but a clean-up worth noting)

### A4 — List typeName param is ambiguous between fully-qualified and short name
**Location:** `scriptableobject-list` — `Tool_ScriptableObject.List.cs` line 37
**Issue:** Create requires fully-qualified type name (`MyGame.WeaponConfig`); List uses short type name (`WeaponConfig`) and partial match. The two tools have inconsistent semantics for the same conceptual parameter. The List description does not flag this — it just says `(e.g. 'WeaponConfig')`.
**Evidence:** Create line 28: `Fully qualified ScriptableObject type name (e.g. 'MyGame.WeaponConfig')`. List line 37: `Filter by ScriptableObject type name (e.g. 'WeaponConfig'). Partial match supported.` Implementation at line 71 calls `actualTypeName.Contains(typeName, ...)` against `GetType().Name` (short name only — namespace is stripped).
**Confidence:** high

### A5 — Inspect and List should be marked ReadOnlyHint
**Location:** `scriptableobject-inspect`, `scriptableobject-list` — `Tool_ScriptableObject.Inspect.cs` line 25, `Tool_ScriptableObject.List.cs` line 34
**Issue:** Both tools only read project state; neither writes. Per the `ReadOnlyHint` convention used in `asset-find`, `asset-get-info`, `object-get-data`, these should declare `ReadOnlyHint = true`.
**Evidence:** Grep over the domain: zero matches for `ReadOnlyHint`. Cross-domain comparison: `Tool_Object.GetData.cs` line 26 sets `ReadOnlyHint = true`.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — Create has no default folderPath, but most SOs land in Assets/Data or Assets/ScriptableObjects
**Location:** `scriptableobject-create` param `folderPath`
**Issue:** No default. Caller must always supply. For exploratory or one-off SO creation, a sensible default (e.g. `"Assets"` or `"Assets/Data"`) would reduce friction.
**Current:** `string folderPath` (required)
**Suggested direction:** Make optional with a sensible default such as `"Assets"`. The Create tool already creates intermediate folders (lines 99-115), so a simple default is safe.
**Confidence:** medium (defaults are an opinion call — a planner agent should weigh)

### D2 — Create has no default assetName
**Location:** `scriptableobject-create` param `assetName`
**Issue:** No default. Most callers will name the asset after the type (e.g. `WeaponConfig` -> `WeaponConfig.asset`) or use a unique name. Consider defaulting to the short type name when omitted, with `AssetDatabase.GenerateUniqueAssetPath` to avoid collision (`Tool_Asset.Create.cs` line 60 already does this for the generic Create flow).
**Current:** `string assetName` (required)
**Suggested direction:** Optional with default derived from the short type name; collision handled via `GenerateUniqueAssetPath`. A planner agent should decide whether this is desirable behaviour or not.
**Confidence:** low (this is a UX preference)

### D3 — List default of 50 may truncate large projects silently
**Location:** `scriptableobject-list` param `maxResults`
**Issue:** Default 50 is fine, but the truncation message `, showing first 50` only fires when `count >= maxResults`. Critically, the `count` in that comparison is the post-filter count, not the total guid count — so if 200 SO assets exist but only 30 match `typeName`, the message says `30 found` with no indication that more SOs exist beyond the limit. Conversely, if 500 SOs exist and the loop hits 50 matches early, the user sees `50 found, showing first 50` but does not know the true total.
**Current:** `int maxResults = 50`
**Suggested direction:** Surface both the total guid count (pre-filter) and the matched count. This is more of a behaviour fix than a default change, but worth flagging.
**Confidence:** high (verified by reading lines 54-87 of List.cs)

### D4 — Modify lacks an Undo.RecordObject call (no parameter, but worth flagging here)
**Location:** `scriptableobject-modify` — implementation gap, not a default issue per se
**Issue:** Object.Modify (cross-domain) calls `Undo.RecordObject(obj, "Object / Modify");` at line 62. SO.Modify does not. This means edits via SO.Modify cannot be undone in the Editor, while the equivalent edit via Object.Modify can. Inconsistent UX for the same conceptual action.
**Suggested direction:** Add Undo support, matching Object.Modify. Treat this as a parity fix.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot author array fields (add/remove/reorder elements)
**Workflow:** A typical ScriptableObject config holds arrays — e.g. `WeaponConfig.upgradeTiers : UpgradeTier[]`, `EnemyTable.enemies : EnemyEntry[]`. A developer expects to add a new element, set its fields, and optionally reorder.
**Current coverage:** SO.Modify can set `items.Array.data[0]` for primitive types if the array element already exists at that index. SO.Inspect lists `ArraySize` rows but no helper exposes a way to grow/shrink the array.
**Missing:** No tool calls `SerializedProperty.InsertArrayElementAtIndex` or `DeleteArrayElementAtIndex` for SO assets. A consumer who wants to append a new element must either (a) hand-edit the YAML, or (b) loop calling Modify many times if the array is already sized — but cannot grow it. The `Editor/Tags` domain does call these APIs (verified at `Tool_Editor.Tags.cs` lines 52-53, 145), but that's tag-list-specific, not a generic SO array tool.
**Evidence:** `Tool_ScriptableObject.Modify.cs` line 98 switches on `prop.propertyType` — has no `ArraySize` case (the integer set via `Integer` would change the count but Unity requires `arraySize` mutation specifically), and no element-insert/delete logic. `Tool_ScriptableObject.Inspect.cs` line 65 prints `ArraySize` rows but the Modify tool cannot set them meaningfully.
**Confidence:** high (full SO domain coverage; verified that Object.Modify also lacks array growth — same gap)

### G2 — Cannot set Vector4, Quaternion, Rect, Bounds, LayerMask, Vector2Int, Vector3Int, Hash128
**Workflow:** Set a `Bounds` (e.g. spawn area), a `Rect` (e.g. UI region), a `LayerMask` (e.g. enemy layers), or `Vector2Int/Vector3Int` (e.g. grid coordinate) field on a config asset. Inspect a Vector4 shader-uniform default.
**Current coverage:** None.
**Missing:** SO.Modify's `SetPropertyValue` (line 96) covers Integer, Float, Boolean, String, Enum, Vector2, Vector3, Color, ObjectReference. SO.Inspect's `GetPropertyValueString` (line 83) reads Vector4 (line 94) but Modify cannot set it. No support for `vector4Value`, `quaternionValue`, `rectValue`, `boundsValue`, `intValue` on `LayerMask` props (they need explicit handling), `vector2IntValue`, `vector3IntValue`, `hash128Value`.
**Evidence:** Direct read of switch in `Tool_ScriptableObject.Modify.cs` lines 98-192. The default case at line 190 returns false; user gets a generic "Could not set property" error.
**Confidence:** high

### G3 — Cannot set AnimationCurve, Gradient, or ManagedReference
**Workflow:** ScriptableObject configs commonly hold `AnimationCurve` (e.g. damage falloff, ease curves), `Gradient` (e.g. health bar colour ramp), or `[SerializeReference]` polymorphic fields (modern Unity pattern for behaviour graphs / config trees).
**Current coverage:** None — Inspect renders these as `<AnimationCurve>` / `<Gradient>` placeholders (line 98 default branch); Modify rejects them.
**Missing:** No tool exposes `animationCurveValue` (set via keyframe array), `gradientValue` (set via colour-key + alpha-key arrays), or managed-reference assignment (`SerializedProperty.managedReferenceValue` with type lookup). `Grep SerializedPropertyType.(AnimationCurve|Gradient|ManagedReference)` over the entire `Editor/Tools` tree returned only Object.GetData and Component.Get (read-only sites), no write sites anywhere in the codebase.
**Evidence:** Cross-codebase Grep `SerializedPropertyType\.(AnimationCurve|Gradient|LayerMask|...)` — 2 files match, both read-only listings, neither writes.
**Confidence:** high (verified across the entire `Editor/Tools` tree, not just SO domain)

### G4 — Cannot duplicate or clone an existing ScriptableObject
**Workflow:** "Make a Goblin variant from the existing Skeleton enemy config." A developer would typically duplicate the source asset, rename it, then tweak a few fields.
**Current coverage:** Asset.Copy (`Tool_Asset.Copy.cs` exists per Glob) handles generic asset duplication. SO.Modify can edit the copy.
**Missing:** Nothing strictly missing — Asset.Copy + SO.Modify covers the workflow. Flagged here only as a discoverability concern: a user who searched the SO domain for "duplicate" would not find it. A pointer in SO.Create's description (or a thin SO.Duplicate alias) could improve discoverability. **Downgrade: not a true gap.**
**Confidence:** low (this is more an ergonomics suggestion than a gap)

### G5 — Cannot create a SO via type discovery (`CreateAssetMenu` attribute)
**Workflow:** Unity scripts annotated with `[CreateAssetMenu(menuName = "Game/WeaponConfig")]` provide a menu-driven creation path. A developer asking "what kinds of SOs can I create here?" expects a way to list available `CreateAssetMenu`-decorated types.
**Current coverage:** None — Create requires the caller to already know the fully-qualified type name. There is no list-types tool, no introspection of `CreateAssetMenuAttribute`.
**Missing:** A discovery tool that scans loaded assemblies for `Type.IsSubclassOf(typeof(ScriptableObject))` and reports each, optionally with its `CreateAssetMenuAttribute.menuName`. Without this, the LLM has to know the exact type name in advance — which it usually doesn't unless the user spells it out.
**Evidence:** Grep `CreateAssetMenu` across `Editor/Tools` returns zero matches. No SO type discovery anywhere.
**Confidence:** high (verified across full Tools tree)

### G6 — Cannot create with initial property values in one call
**Workflow:** Create `WeaponConfig` and immediately set `damage: 25, range: 10`. Two-step is fine for the LLM but error-prone (must remember the exact path it just created).
**Current coverage:** SO.Create + N calls to SO.Modify. Or SO.Create + Object.Modify (which does take a JSON map but requires resolving the new asset's instance ID first).
**Missing:** A `propertiesJson` parameter on SO.Create, mirroring Asset.Create's pattern (which has the param but marks it `reserved for future use`). Would let the LLM author + populate in a single tool call.
**Evidence:** `Tool_ScriptableObject.Create.cs` line 47-52 — no propertiesJson param. `Tool_Asset.Create.cs` line 37 — has propertiesJson, marked reserved.
**Confidence:** high (low-effort, high-impact for end-to-end SO authoring)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | Cannot grow/shrink array fields on SO assets — blocks authoring of any SO with collections |
| 2 | G6 | Capability Gap | 4 | 2 | 16 | high | Cannot set initial property values during Create — forces 2-step authoring |
| 3 | R1 | Redundancy | 4 | 3 | 12 | high | SO.Inspect/Modify duplicate Object.GetData/Modify with subtle behavioural drift (Undo, bool parsing) |
| 4 | G2 | Capability Gap | 4 | 3 | 12 | high | No support for Vector4/Quaternion/Rect/Bounds/LayerMask/Vector2Int/Vector3Int/Hash128 in Modify |
| 5 | G5 | Capability Gap | 4 | 3 | 12 | high | No type-discovery tool — LLM must know exact fully-qualified SO type name a priori |
| 6 | A5 | Ambiguity | 3 | 1 | 15 | high | SO.Inspect and SO.List should set ReadOnlyHint = true (parity with Object.GetData / Asset.Find) |
| 7 | D4 | Default/Parity | 3 | 1 | 15 | high | SO.Modify lacks Undo.RecordObject — diverges from Object.Modify behaviour |
| 8 | G3 | Capability Gap | 4 | 4 | 8 | high | Cannot set AnimationCurve/Gradient/ManagedReference — common SO config patterns blocked |
| 9 | A2 | Ambiguity | 3 | 1 | 15 | high | Modify description claims 8 types, supports 9, silently rejects unsupported ones |
| 10 | A4 | Ambiguity | 3 | 1 | 15 | high | List uses short type names while Create requires fully-qualified — inconsistent semantics |
| 11 | D3 | Default Issue | 2 | 2 | 8 | high | List truncation count is misleading when type filter is applied |
| 12 | A1 | Ambiguity | 2 | 1 | 10 | high | Modify value-encoding docs omit Enum support and bool-parsing strictness |
| 13 | R2 | Redundancy | 2 | 3 | 6 | medium | SO.List partially overlaps Asset.Find (`t:WeaponConfig`); discoverability concern |
| 14 | A3 | Ambiguity | 1 | 1 | 5 | medium | Inspect skips `m_Script` silently — minor invisibility issue |
| 15 | D1 | Default Issue | 2 | 1 | 10 | medium | Create has no default folderPath |
| 16 | D2 | Default Issue | 1 | 2 | 4 | low | Create has no default assetName |
| 17 | G4 | Capability Gap | 1 | 1 | 5 | low | SO duplication discoverability (covered by Asset.Copy) |

Priority formula: `Impact × (6 - Effort)`. Top of the list is `R1` and `G2` clusters because they touch the highest-traffic flow (modify a SO field).

---

## 7. Notes

- **The cleanest single intervention** is probably G6 (add `propertiesJson` to SO.Create). It is low-effort (mirror Asset.Create's signature, plumb through the same parser Object.Modify uses), eliminates a multi-call workflow, and the JSON parser already exists in `Tool_Object.Modify.cs` (`ParseFlatJson`). A planner could lift that helper to a shared place during consolidation.
- **R1 deserves explicit strategic input from Ramon.** Three plausible directions:
  1. Keep the SO domain as a thin convenience layer, but make it call Object.GetData/Modify internally (resolve assetPath -> instanceId, then delegate). Cuts duplication, preserves discoverability.
  2. Deprecate SO.Inspect and SO.Modify entirely, lean on Object.* tools. Cuts surface area, hurts discoverability ("I have a SO; what tool do I use?").
  3. Keep both but unify the `SetPropertyValue` helper into a shared `Editor/Tools/Helpers/SerializedPropertyEncoder.cs` utility. Cuts duplication without changing public surface. Fixes the bool-parsing-divergence and Undo asymmetry by construction.
  Option 3 is the lowest-risk and probably what the consolidation-planner will recommend, but worth confirming.
- **G1 (array operations) likely wants its own design pass.** A generic "modify array" tool is harder to design well than it looks: insert-at, append, remove-at, set-element-N, clear, swap-i-j, reorder-by-permutation are all distinct ops. The `Tool_Animation.ConfigureController.cs` `action`-dispatch pattern referenced in the audit prompt would fit here — e.g. `scriptableobject-modify-array(assetPath, propertyPath, action: "append" | "insert" | "remove" | "clear")`. But this is planner territory; flagging only.
- **Cross-domain dependency to call out:** `Tool_Object` uses the deprecated `EditorUtility.InstanceIDToObject` API (suppressed via `#pragma warning disable CS0618`). Per `CLAUDE.md`, the project standard is `EntityIdToObject`. If R1 consolidates SO.* into Object.*, this deprecation should be addressed simultaneously rather than inheriting it.
- **Open question for the reviewer:** is there a reason the SO domain explicitly does not include a `Delete` tool? Asset.Delete handles it generically, which is consistent with R1-style "let the generic tools own lifecycle". If yes, the same logic argues against adding SO-specific array ops in G1 — instead, expose them on the Object domain. This is a strategic choice for Ramon.
