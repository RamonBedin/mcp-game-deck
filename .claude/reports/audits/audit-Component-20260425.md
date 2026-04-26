# Audit Report — Component

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Component/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via Glob `Editor/Tools/Component/Tool_Component.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** balanced

**Errors encountered during audit:** None

**Files not analyzed (if any):** None

**Absence claims in this report:**
- All absence claims are backed by complete coverage of the 5 in-domain files plus targeted cross-domain Greps for related capabilities. Cross-domain Greps are noted per finding.

**Reviewer guidance:**
- The Component domain is small and tight (5 tools, single partial class). The biggest issues are not in the existing tools' shape but in (a) a stark capability mismatch between `component-get` (reads ~17 property types) and `component-update` (writes only 4), and (b) several missing companion verbs that block common Editor workflows (enable/disable, copy/paste between objects, multi-component-add, get-by-instance-id).
- The overall code quality is high — XML docs, Undo registration, and `MainThreadDispatcher.Execute` are consistent across all 5 files. Most findings are about *gaps and ambiguity*, not code quality.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `component-add` | Component / Add | `Tool_Component.Add.cs` | 3 (`instanceId=0`, `objectPath=""`, `componentType=""`) | no |
| `component-get` | Component / Get Properties | `Tool_Component.Get.cs` | 3 (`instanceId=0`, `objectPath=""`, `componentType=""`) | yes |
| `component-list` | Component / List | `Tool_Component.List.cs` | 2 (`instanceId=0`, `objectPath=""`) | yes |
| `component-remove` | Component / Remove | `Tool_Component.Remove.cs` | 3 (`instanceId=0`, `objectPath=""`, `componentType=""`) | no |
| `component-update` | Component / Update Properties | `Tool_Component.Update.cs` | 4 (`instanceId=0`, `objectPath=""`, `componentType=""`, `propertiesJson=""`) | no |

**Internal Unity API surface used:**
- `Undo.AddComponent`, `Undo.DestroyObjectImmediate`
- `GameObject.GetComponent(Type)`, `GameObject.GetComponents<Component>()`
- `SerializedObject` + `SerializedProperty.NextVisible`, `FindProperty`, `ApplyModifiedProperties`
- `Behaviour.enabled` (read-only via List)
- `System.AppDomain.CurrentDomain.GetAssemblies()` for type resolution

**Shared helper used (not in domain):** `Tool_Transform.FindGameObject(instanceId, objectPath)` — confirmed at `Editor/Tools/Transform/Tool_Transform.cs:24`.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The five tools form a clean CRUD-shaped surface (Add / Get / List / Update / Remove) with no semantic overlap. Each verb maps to a distinct Unity API call.

One small note: `component-list` and `component-get` could in principle be unified under a single `component-inspect` with an `includeProperties: bool` flag, but the current split is justified — the read-only "list everything cheaply" and "drill into one component's properties" intents are different enough that LLM disambiguation is unlikely to fail. **Confidence: medium** that they should remain separate.

---

## 3. Ambiguity Findings

### A1 — `component-update` description hides supported-type limitation
**Location:** `component-update` — `Tool_Component.Update.cs` line 36
**Issue:** The description advertises "JSON property map" but the supported set is narrower than `component-get` reports. The description does say "Supports float, int, bool, and string value types" — so it's not silent — but it does not call out the asymmetry with `component-get`, nor does it tell the LLM what to do when it needs to set a Vector3 or Color (very common in practice for Transform position, Light color, Camera background, etc.). An LLM reading `component-get`'s output, which prints Vector3 / Color / ObjectReference / Enum / LayerMask values, will reasonably assume those are round-trippable.
**Evidence:** `Tool_Component.Update.cs:36` description, vs. `Tool_Component.Get.cs:113-216` which handles 17+ property types including Vector2/3/4, Color, ObjectReference, Enum, LayerMask, Quaternion, Bounds, Rect, AnimationCurve, Hash128.
**Confidence:** high

### A2 — `componentType` parameter does not warn about ambiguous type names
**Location:** `component-add`, `component-get`, `component-remove`, `component-update`
**Issue:** `ResolveComponentType` (Add.cs line 95) does a case-insensitive match across all loaded assemblies and returns the **first** match. There is no description text warning the LLM about ambiguous simple names (e.g. `"Camera"` exists in `UnityEngine`, `Cinemachine`, and arguably user code). The description says "UnityEngine namespaces are searched automatically" but does not say "if multiple types match, the first-found wins — pass a fully-qualified name to disambiguate."
**Evidence:** `Tool_Component.Add.cs:97-113` — early-exits on first match, prefix-by-prefix, with `ignoreCase: true`.
**Confidence:** medium (real ambiguity exists in projects with custom Camera/Light/Renderer classes; severity depends on user codebase)

### A3 — `objectPath` description does not specify scene-vs-prefab semantics
**Location:** All 5 tools
**Issue:** Every tool says `Hierarchy path of the target GameObject (e.g. 'Parent/Child')`. It does not state which scene is searched, or whether prefab-stage objects are reachable, or what happens when the same path exists in multiple scenes. This is the same pattern as Transform tools (so it's consistent), but the ambiguity persists across the whole CRUD surface.
**Evidence:** Every `objectPath` parameter description in the domain. The actual behavior lives in `Tool_Transform.FindGameObject` at `Editor/Tools/Transform/Tool_Transform.cs:24`.
**Confidence:** medium (cross-domain pattern; flagging for awareness, not as a Component-specific fix)

### A4 — `propertiesJson` does not document the "not found" / "unsupported" partial-success behavior
**Location:** `component-update` — `Tool_Component.Update.cs` line 41
**Issue:** The tool silently downgrades unknown property names (line 98: `[not found]`) and unsupported types (line 111: `[unsupported type]`) to warnings rather than errors, and still returns success if any property succeeded. The parameter description does not tell the LLM this — so an LLM that asks "set mass=5.0 and centerOfMass=(0,1,0)" will receive a "1 property updated" success and likely not notice that `centerOfMass` was skipped because Vector3 is unsupported.
**Evidence:** `Tool_Component.Update.cs:96-112` (warnings collected separately from errors), lines 120-130 (success message printed even with warnings).
**Confidence:** high

### A5 — No mention of how to address property paths for nested fields or arrays
**Location:** `component-update` — `propertiesJson` parameter
**Issue:** `serializedObj.FindProperty(key)` accepts SerializedProperty paths like `stats.health` and `items.Array.data[0]` (this is documented in `scriptableobject-modify` at `Tool_ScriptableObject.Modify.cs:38`). The Component tool's description gives only top-level examples (`mass`, `useGravity`, `tag`) and never mentions that nested or array paths are usable. An LLM trying to set `material.color` on a Renderer will not know whether to try `materials.Array.data[0].color` or fall back to a different tool.
**Evidence:** `Tool_Component.Update.cs:36` description vs. `Tool_ScriptableObject.Modify.cs:38` which documents the path syntax.
**Confidence:** medium (Component's main use case is top-level scalars, but nested access on Renderer/Animator is common)

---

## 4. Default Value Issues

### D1 — `instanceId = 0` and `objectPath = ""` create a hidden "must specify one" contract
**Location:** All 5 tools, both selector params
**Issue:** Both selectors default to "no value." The implementation requires that at least one resolves successfully. There is no description-level statement of the contract "exactly one of instanceId or objectPath must be a valid identifier." The error path (`GameObject not found. instanceId=0, objectPath=''`) is informative once it fires, but the LLM has no a-priori signal.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** Add a "use at least one" sentence to both descriptions, OR if the project already has a convention that `instanceId=0` always means "use objectPath," document it as a contract in the description. (No code change implied.)
**Confidence:** high

### D2 — `componentType = ""` is required-but-defaulted on Add/Get/Remove/Update
**Location:** `component-add`, `component-get`, `component-remove`, `component-update` — `componentType` param
**Issue:** Each tool validates `string.IsNullOrWhiteSpace(componentType)` at runtime and errors. The parameter is required by intent but signed as optional. This pattern is a known C# constraint (you cannot have a required param after an optional one in attribute ordering), but the **descriptions** do not say "required" — so the schema surfaced to the LLM looks fully optional.
**Current:** `string componentType = ""`
**Suggested direction:** Either re-order so required params come first (breaking signature), or prefix descriptions with "(required) Simple or fully-qualified component type name…". No code change implied — direction only.
**Confidence:** high

### D3 — `propertiesJson = ""` is also required-but-defaulted
**Location:** `component-update` — `propertiesJson` param
**Issue:** Same pattern as D2. Param is required (line 51-54 errors if blank) but typed `string propertiesJson = ""`.
**Current:** `string propertiesJson = ""`
**Suggested direction:** Mark "(required)" in description text.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot enable/disable a component
**Workflow:** A developer wants to disable an `Animator`, `Light`, `Collider`, or `MonoBehaviour` at runtime via tooling — equivalent to unchecking the component header checkbox in the Inspector.
**Current coverage:** `component-list` *reads* `Behaviour.enabled` (Tool_Component.List.cs:61-63). `component-update` accepts bool values, so in theory `propertiesJson = "{\"m_Enabled\":true}"` might work via the `m_Enabled` SerializedProperty.
**Missing:** No first-class enable/disable verb. Workflow requires knowing the internal serialized name (`m_Enabled`, hidden from regular SerializedObject iteration because `m_Script` is the only m_-prefixed property explicitly skipped). This is undiscoverable from `component-get`'s output (which calls `enterChildren = false` after `m_Script` and skips that property anyway). Result: an LLM cannot reliably toggle a component without trial-and-error.
**Evidence:** `Tool_Component.Update.cs:79-92` iterates only properties whose names came from JSON keys; never enumerates `m_Enabled`. `Tool_Component.Get.cs:81-85` skips `m_Script` but does not surface `m_Enabled`. No `component-set-enabled` tool found (cross-domain Grep `McpTool\("component-` returned only the 5 known tools).
**Confidence:** high

### G2 — Cannot set Vector / Color / ObjectReference / Enum / LayerMask values
**Workflow:** Setting standard component properties: `Light.color`, `Camera.backgroundColor`, `Rigidbody.centerOfMass`, `MeshRenderer.sharedMaterial`, `Collider.sharedMaterial` (PhysicsMaterial), `GameObject.layer` (LayerMask on some components), or any enum field (e.g. `Light.type`, `Rigidbody.collisionDetectionMode`).
**Current coverage:** `component-update` supports Float / Int / Bool / String only. `component-get` *displays* all these types but `component-update` cannot round-trip them.
**Missing:** Vector2/3/4, Color, ObjectReference (asset-path lookup), Enum (by name or index), LayerMask (by mask value or by layer-name list). The sister tool `scriptableobject-modify` (`Editor/Tools/ScriptableObject/Tool_ScriptableObject.Modify.cs:35`) advertises support for Vector2/Vector3/Color/ObjectReference — proving the pattern exists in the codebase.
**Evidence:** `Tool_Component.Update.cs:318-358` `ApplyPropertyValue` switch covers exactly 4 cases (Float, Integer, Boolean, String) and falls through to `return false` for everything else. Compare `Tool_ScriptableObject.Modify.cs:35` description listing 7+ types.
**Confidence:** high

### G3 — Cannot copy a component (or its values) from one GameObject to another
**Workflow:** "Apply the same `Light` settings from object A to object B" / "Duplicate this Rigidbody configuration onto a new prefab variant." In Unity Editor this is the right-click "Copy Component" / "Paste Component Values" menu, backed by `UnityEditorInternal.ComponentUtility.CopyComponent` and `PasteComponentValues` / `PasteComponentAsNew`.
**Current coverage:** None. An LLM would have to call `component-get`, parse the human-readable output, then call `component-add` + `component-update` and re-format every value as JSON. This is fragile (parsing back from `(x, y, z)` strings), lossy (non-supported types from G2), and many calls.
**Missing:** A `component-copy` / `component-paste-values` / `component-paste-as-new` capability — or a single `component-duplicate(sourceInstanceId, targetInstanceId, componentType)` macro.
**Evidence:** Cross-domain Grep `CopyComponent|PasteComponent|ComponentUtility|MoveComponentUp|MoveComponentDown` across `Editor/Tools/` returned zero matches. No tool wraps this API anywhere in the project.
**Confidence:** high

### G4 — Cannot reorder components on a GameObject
**Workflow:** "Move this script above the Rigidbody so it executes first" — Unity Inspector's "Move Up / Move Down" context menu, backed by `UnityEditorInternal.ComponentUtility.MoveComponentUp/Down`.
**Current coverage:** None. `component-list` returns components with their index but no reorder verb exists.
**Missing:** `component-move-up` / `component-move-down`, or a `component-reorder(componentType, newIndex)`.
**Evidence:** Same Grep as G3 — zero matches. `Tool_Component.List.cs:47-69` does print indices but there is no companion write tool.
**Confidence:** high

### G5 — Cannot operate on a component by its own instanceId
**Workflow:** "I have a component instanceId from a previous Get/List call — let me update it directly without re-resolving the GameObject + component-type."
**Current coverage:** None of the 4 write/read tools accept a component instanceId. They always require GameObject identification + componentType. This is wasteful when the LLM already has the component instanceId in conversation context (because Get/List both print it).
**Missing:** Either a per-tool `componentInstanceId` parameter (third selector path), or `component-update-by-id(componentInstanceId, propertiesJson)`. The current model also has a correctness gap: if a GameObject has two `MonoBehaviour` instances of different concrete types, addressing one specifically is awkward.
**Evidence:** `Tool_Component.Update.cs:39-42` signature accepts only `instanceId` (the GameObject's), `objectPath`, `componentType`, `propertiesJson`. `Tool_Component.Get.cs:31` and `Tool_Component.List.cs:26` both *return* component instanceIds in their text output. The asymmetry "we tell you the ID but won't accept it back" is a fragmentation cost.
**Confidence:** high

### G6 — Cannot add multiple components in one call
**Workflow:** Setting up a typical 2D enemy: `Rigidbody2D` + `BoxCollider2D` + `SpriteRenderer` + custom MonoBehaviour. With current tools that's 4 sequential `component-add` calls plus 4 `component-update` calls — 8 round trips minimum to set up one entity.
**Current coverage:** `component-add` accepts a single `componentType`.
**Missing:** A batch verb such as `component-add-many(componentTypes: string[])` or — more powerfully — `component-add-with-properties(componentType, propertiesJson)` so add+configure happens atomically.
**Evidence:** `Tool_Component.Add.cs:39-43` signature accepts a single `string componentType`.
**Confidence:** medium (this is fragmentation more than missing capability — workflow is *possible*, just verbose)

### G7 — Cannot query "does this GameObject have component X?" without enumerating
**Workflow:** Conditional logic: "If this GameObject already has a `NavMeshAgent`, skip adding it; otherwise add it."
**Current coverage:** `component-list` returns the full list as text; `component-get` errors with "Component X not found" if not present (could be used as a probe but the error path is noisy and consumes context).
**Missing:** `component-has(componentType)` returning a clean bool, or a query mode on `component-get` that returns null instead of erroring.
**Evidence:** `Tool_Component.Get.cs:62-65` errors on missing component with no "soft-fail" mode.
**Confidence:** low (workaround via `component-list` exists; impact depends on how often the LLM needs this idempotency check)

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort).

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G2 | Capability Gap | 5 | 2 | 20 | high | `component-update` cannot set Vector/Color/ObjectReference/Enum/LayerMask — scriptableobject-modify already proves the pattern |
| 2 | G1 | Capability Gap | 5 | 2 | 20 | high | No way to enable/disable a component; `m_Enabled` is hidden from current iteration logic |
| 3 | A4 | Ambiguity | 4 | 1 | 20 | high | `component-update` silently skips not-found / unsupported properties and reports overall success |
| 4 | G6 | Capability Gap | 4 | 2 | 16 | medium | No `component-add` + configure macro; entity setup costs 2N round trips |
| 5 | G3 | Capability Gap | 4 | 3 | 12 | high | No copy/paste-values across GameObjects; ComponentUtility unwrapped |
| 6 | A1 | Ambiguity | 3 | 1 | 15 | high | `component-update` description does not flag the get/update type asymmetry |
| 7 | G5 | Capability Gap | 3 | 2 | 12 | high | Cannot address a component by its own instanceId; List/Get return IDs that can't be reused |
| 8 | D2/D3 | Defaults | 3 | 1 | 15 | high | Required params (`componentType`, `propertiesJson`) signed as optional; no "(required)" in description |
| 9 | G4 | Capability Gap | 2 | 2 | 8 | high | No reorder; `component-list` prints index but there's no write counterpart |
| 10 | A5 | Ambiguity | 3 | 1 | 15 | medium | Nested SerializedProperty paths (`x.y`, `Array.data[i]`) work but undocumented |
| 11 | D1 | Defaults | 3 | 1 | 15 | high | "instanceId=0 OR objectPath=''" contract not stated in descriptions |
| 12 | A2 | Ambiguity | 2 | 1 | 10 | medium | Ambiguous `componentType` resolution — first match wins, not documented |
| 13 | G7 | Capability Gap | 2 | 2 | 8 | low | `component-has` would simplify idempotent setup; workaround exists via List |

Top three are tied at priority 20. **G2 (write-type parity) and A4 (silent partial-success) compound each other** — fixing G2 without fixing A4 means the LLM still won't realize when a Vector3 it tried to set was silently dropped on a deployment that hasn't picked up the new types yet.

---

## 7. Notes

**Cross-domain dependencies observed:**
- All 5 tools call `Tool_Transform.FindGameObject` (Transform domain). Any change to that helper's semantics — e.g. supporting prefab-stage paths — propagates to the entire Component domain transparently. This is a healthy abstraction.
- `Tool_ScriptableObject.Modify.cs` is a near-sibling and supports a richer type set than `Tool_Component.Update.cs`. Consolidation could share the SerializedProperty value-parser. Not the auditor's call to design — flagging for the consolidation-planner.

**Workflows intentionally not flagged:**
- "Find all GameObjects with component X" — this belongs to the GameObject/Find domain, not Component. Confirmed `gameobject-find` exists at `Editor/Tools/GameObject/Tool_GameObject.Find.cs`. Out of scope.
- "List all components of a given type in the scene" — same, GameObject/Selection domain concern.

**Open questions for the reviewer:**
- For G5 (component-instance-id addressing): is this a real LLM workflow, or do we always identify by GameObject + type? If the latter, downgrade G5.
- For G6 (batch add): is the preferred shape (a) a true batch verb or (b) extending `component-add` with a `propertiesJson` parameter? The latter halves round trips for the most common case (add + immediately configure) without changing the cardinality model.
- For A4 (silent partial-success): should the default behavior be "strict — error on any not-found / unsupported," "lenient — current," or a new `strict: bool = false` parameter? The fix is description-only if behavior stays as-is.

**Honest limits of this audit:**
- I did not exercise the JSON parser at `Tool_Component.Update.cs:160-306` against malformed input. The custom hand-rolled parser looks reasonable on inspection but its failure modes (e.g. unescaped quotes inside strings, scientific notation) were not stress-tested. If reviewers want correctness assurances on that, that's a build-validator or test-author task, not an audit task.
- I did not check whether `ResolveComponentType` correctly handles generic component types or interface-typed lookups. The current implementation does `IsAssignableFrom(typeof(Component))` which excludes interfaces, but that's likely intentional. Flagging as an open question only.
