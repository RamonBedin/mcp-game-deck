# Audit Report — GameObject

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/GameObject/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 10 (via Glob `Editor/Tools/GameObject/Tool_GameObject.*.cs`)
- `files_read`: 10
- `files_analyzed`: 10

**Balance:** balanced (10 / 10 / 10)

**Errors encountered during audit:**
- None.

**Files not analyzed:**
- None.

**Absence claims in this report:**
- Permitted — coverage is complete. Cross-domain absence claims (e.g. "no `SetSiblingIndex` anywhere in `Editor/Tools/`") are backed by explicit Grep over the entire `Editor/Tools/` tree.

**Reviewer guidance:**
- Several findings hinge on overlap between this domain and `Tool_Transform` / `Tool_Selection`. The boundary between `gameobject-*` and `transform-*` is currently fuzzy; consolidation decisions in this domain affect those domains.
- The shared helper `Tool_Transform.FindGameObject(int, string)` is used by 9 of 10 GameObject tools; it lives in another domain. That cross-domain coupling is also called out in this audit.
- One small but real bug: `Tool_Transform.FindGameObject` uses the deprecated `EditorUtility.InstanceIDToObject` (with a `#pragma warning disable`). CLAUDE.md explicitly mandates `EntityIdToObject`. Selection domain already migrated. This is a coding-standard violation that propagates into every GameObject tool that does lookup. Worth flagging to the human reviewer even though the helper itself isn't owned by this domain.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `gameobject-create` | GameObject / Create | `Tool_GameObject.Create.cs` | 6 | no |
| `gameobject-delete` | GameObject / Delete | `Tool_GameObject.Delete.cs` | 2 | no |
| `gameobject-duplicate` | GameObject / Duplicate | `Tool_GameObject.Duplicate.cs` | 6 | no |
| `gameobject-find` | GameObject / Find | `Tool_GameObject.Find.cs` | 4 | yes |
| `gameobject-get` | GameObject / Get Info | `Tool_GameObject.Get.cs` | 4 | yes |
| `gameobject-look-at` | GameObject / Look At | `Tool_GameObject.LookAt.cs` | 6 | no |
| `gameobject-move-relative` | GameObject / Move Relative | `Tool_GameObject.MoveRelative.cs` | 6 | no |
| `gameobject-select` | GameObject / Select | `Tool_GameObject.Select.cs` | 4 | no |
| `gameobject-set-parent` | GameObject / Set Parent | `Tool_GameObject.SetParent.cs` | 5 | no |
| `gameobject-update` | GameObject / Update | `Tool_GameObject.Update.cs` | 7 | no |

**Internal Unity API surface used (summary):**
- `GameObject.CreatePrimitive(PrimitiveType.*)`, `new GameObject(name)`, `GameObject.Find`, `GameObject.FindGameObjectsWithTag`
- `Object.Instantiate`, `Undo.RegisterCreatedObjectUndo`, `Undo.DestroyObjectImmediate`, `Undo.RecordObject`, `Undo.SetTransformParent`
- `Selection.activeGameObject`, `EditorGUIUtility.PingObject`, `EditorUtility.SetDirty`
- `Transform.LookAt`, `Transform.SetParent`, `Transform.position`, raw transform `forward`/`right`/`up`
- `LayerMask.NameToLayer`, `LayerMask.LayerToName`
- `SceneManager.GetActiveScene().GetRootGameObjects()`
- `EditorUtility.InstanceIDToObject` (deprecated — via `Tool_Transform.FindGameObject`)

**Cross-domain helpers:**
- `Tool_Transform.FindGameObject(int instanceId, string objectPath)` — used by 9 of 10 tools (every tool except `gameobject-create`).
- `gameobject-create` uses raw `GameObject.Find(parentPath)` instead of the shared helper, an unexplained inconsistency.

---

## 2. Redundancy Clusters

### Cluster R1 — Move/Translate redundancy across domains
**Members:** `gameobject-move-relative`, `transform-move`
**Overlap:** `transform-move` already supports relative movement in world OR local space (the `space` + `relative` parameters). `gameobject-move-relative` re-implements relative motion using named directions (`forward`/`back`/`up`/...) and a `referenceObject` pivot. The intersection set is large: a translation along world `+X` by 5 units can be expressed as either `transform-move(x=5, relative=true)` or `gameobject-move-relative(direction='right', distance=5, worldSpace=true)`. The unique value of `gameobject-move-relative` is the `referenceObject` axis frame and named-direction ergonomics — but those could be folded into `transform-move` as an optional axis-resolution parameter.
**Impact:** An LLM asked to "move the player 2 meters forward in world space" has two plausible tools and no disambiguation in either description. Both tools register Undo, both accept the same `instanceId`/`objectPath` lookup pattern. High likelihood of inconsistent picks across runs.
**Confidence:** high

### Cluster R2 — Selection set across domains
**Members:** `gameobject-select`, `selection-set` (in Selection domain)
**Overlap:** `gameobject-select` selects exactly one GameObject (with optional ping). `selection-set` sets the active selection from comma-separated lists of instanceIds and/or paths. The single-object case is fully covered by `selection-set` with one entry. The unique value of `gameobject-select` is (a) the `objectName` last-resort `GameObject.Find` lookup and (b) the `ping` flag.
**Impact:** Medium. The two tools live in different domains, which weakens disambiguation cues in the LLM's tool list. Both `gameobject-create` and `gameobject-duplicate` already silently set `Selection.activeGameObject` as a side effect, so callers who just want to keep the freshly-created object selected don't need either tool — that further muddies the picture.
**Confidence:** high

### Cluster R3 — LookAt vs Rotate (cross-domain)
**Members:** `gameobject-look-at`, `transform-rotate`
**Overlap:** Lower than R1/R2. `transform-rotate` sets Euler angles directly; `gameobject-look-at` computes a quaternion that points the forward axis at a target. Distinct intent, but neither description tells the LLM "use look-at when you have a target position; use rotate when you have explicit angles." The LLM has to infer that.
**Impact:** Medium-low. Two tools, distinct enough mechanically that the LLM probably picks correctly when the prompt mentions "look at" verbatim, but ambiguous prompts ("face the camera") could go either way.
**Confidence:** medium

### Cluster R4 — Create-vs-Update activation/static state
**Members:** `gameobject-create`, `gameobject-update`
**Overlap:** Minor but worth noting: `gameobject-create` does not let the caller set `tag`, `layer`, `isActive`, or `isStatic` at creation time. To create a tagged enemy on the Enemy layer, the LLM must call `gameobject-create` then `gameobject-update`. This isn't redundancy in the strict sense (no tool does both), but it's a coverage seam that creates two-step workflows where one would do.
**Impact:** Low (it works) but adds one tool call per typical create. Mostly relevant in Capability Gaps below.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — Update parameter `name` shadows `Update.name` and lacks "to keep current" note
**Location:** `gameobject-update` param `name` — `Tool_GameObject.Update.cs`
**Issue:** Description: `"New name for the GameObject. Empty string to leave unchanged."` This is fine, but the tool's pattern of using sentinel defaults (empty string for strings, `-1` for `layer`/`isActive`/`isStatic`) is non-uniform. `name`/`tag` use `""`, `layer` uses `-1`, `isActive` and `isStatic` use `int` tri-state. An LLM has to learn three sentinel conventions for one tool. Booleans-as-int (`1/0/-1`) is the most jarring — Unity has `bool?` semantics naturally and the rest of the codebase uses booleans plainly.
**Evidence:** `[Description("Active state: 1 = active, 0 = inactive, -1 = unchanged.")] int isActive = -1`
**Confidence:** high

### A2 — `gameobject-create` does not list valid `primitiveType` values in the param description with the same set as the method description
**Location:** `gameobject-create` param `primitiveType` — `Tool_GameObject.Create.cs`
**Issue:** Method-level description and the `<param>` doc both list values, but the `[Description]` on the parameter itself omits the case-insensitivity note that's in the XML doc. Minor, but inconsistencies between layers of doc are exactly the surfaces an LLM may consult.
**Evidence:** `[Description("Type of object to create: Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty"` — case sensitivity unclear at the param level. (Implementation does `ToLowerInvariant()`, so it IS case-insensitive — but the LLM may not know.)
**Confidence:** medium

### A3 — `gameobject-find` `by_layer` semantics are split across description and runtime
**Location:** `gameobject-find` param `searchTerm` — `Tool_GameObject.Find.cs`
**Issue:** The param description correctly says `by_layer = layer name or index`, but does not mention what happens when the layer name has a space (it works because `LayerMask.NameToLayer` accepts spaces, but most LLMs don't know this). Also, the failure mode for a typo'd layer is "not found" which is fine, but the description doesn't preview the error format.
**Evidence:** `"by_layer = layer name or index"` is the only hint.
**Confidence:** low

### A4 — `gameobject-set-parent`: "leave empty to unparent" is mentioned but not emphasized
**Location:** `gameobject-set-parent` — `Tool_GameObject.SetParent.cs`
**Issue:** The "unparent to scene root" behavior requires BOTH `parentInstanceId == 0` AND `parentPath == ""`. The method description says "Leave parentInstanceId=0 and parentPath empty to unparent". That's clear. But the param descriptions individually say "Pass 0 to use parentPath or to unparent" and "Leave empty to move to scene root" — each implies the other param is irrelevant, when in reality both must be empty simultaneously. An LLM reading just the param description for `parentInstanceId` could think "ok, 0 = unparent" and pass a non-empty `parentPath` accidentally.
**Evidence:** Param `parentInstanceId`: `"Pass 0 to use parentPath or to unparent."` — ambiguous between "use parentPath" and "unparent".
**Confidence:** medium

### A5 — `gameobject-look-at` does not specify what happens when `targetName` matches multiple objects
**Location:** `gameobject-look-at` param `targetName` — `Tool_GameObject.LookAt.cs`
**Issue:** Description says "Name or hierarchy path of a target GameObject." Internally it calls `Tool_Transform.FindGameObject(0, targetName)`, which delegates to `GameObject.Find(targetName)`. Unity's `GameObject.Find` returns the first match it encounters in the hierarchy — undefined order. The param description gives no warning that ambiguous names produce undefined targeting.
**Evidence:** `[Description("Name or hierarchy path of a target GameObject. When non-empty, overrides targetX/Y/Z.")] string targetName = ""`
**Confidence:** medium

### A6 — `gameobject-select` `objectName` vs `objectPath` distinction is subtle
**Location:** `gameobject-select` — `Tool_GameObject.Select.cs`
**Issue:** Both `objectPath` and `objectName` ultimately reach `GameObject.Find` (since `Tool_Transform.FindGameObject` uses `GameObject.Find` for paths anyway). The "last resort" framing in the description is misleading — the actual difference is that `objectName` is queried with `GameObject.Find` directly, which Unity treats the same as a path search. Practically the two params are interchangeable, which is confusing.
**Evidence:** Code path: when `instanceId == 0` and `objectPath != ""`, `FindGameObject` calls `GameObject.Find(objectPath)`. When both empty, the tool falls back to `GameObject.Find(objectName)`. Same Unity API, same semantics — yet two parameters and a "resolution priority" sentence implying they're different.
**Confidence:** high

### A7 — `gameobject-move-relative` direction priority sentence is in XML but not in `[Description]`
**Location:** `gameobject-move-relative` — `Tool_GameObject.MoveRelative.cs`
**Issue:** The XML `<summary>` clearly explains the 3-tier priority (referenceObject > worldSpace > self). The `[Description]` attribute the LLM actually sees says only `"The orientation frame is taken from a reference object, world space, or the object's own transform."` — the priority order is implicit. An LLM reading just the description has to guess what happens when both `referenceObject` and `worldSpace=true` are passed.
**Evidence:** `[Description(... "The orientation frame is taken from a reference object, world space, or the object's own transform. ...")]`
**Confidence:** high

### A8 — `gameobject-find` `by_component` doesn't say where the search runs (active scene)
**Location:** `gameobject-find` — `Tool_GameObject.Find.cs`
**Issue:** Both XML summary and `[Description]` say "Searches all GameObjects in the active scene". That's accurate. But the implementation only walks the active scene's root objects — an LLM working in a multi-scene setup will silently miss other loaded scenes. The constraint "active scene only" is documented; the implication "won't find in additively-loaded scenes" is not.
**Evidence:** `var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();`
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `gameobject-update` int tri-state for booleans
**Location:** `gameobject-update` params `isActive`, `isStatic`
**Issue:** Tri-state via `int` (`-1`/`0`/`1`) is the workaround for "no nullable bool in MCP transport." But the rest of the codebase uses sentinel strings (`""`) for unchanged-string semantics. For booleans, this protocol could equally well use a string `"true"|"false"|""` or split into two boolean params (`setActive`, `activeValue`). Whatever the project's convention is, it should be uniform — and the current choice forces the LLM to remember "for booleans, magic is -1; for strings, magic is empty; for layer, magic is -1."
**Current:** `int isActive = -1`, `int isStatic = -1`
**Suggested direction:** Pick a consolidation-time convention for "leave unchanged" sentinels and apply it consistently. The Update tool is the most extreme case in this domain. Don't fix it inline — flag for planner.
**Confidence:** medium

### D2 — `gameobject-create` lacks `tag`, `layer`, `isActive`, `isStatic` defaults
**Location:** `gameobject-create`
**Issue:** Common workflow: "Create an Enemy on the Enemy layer with tag Enemy, marked static." Today this requires `gameobject-create` + `gameobject-update`. Adding optional `tag = ""`, `layer = -1`, etc. defaults to `gameobject-create` would let the LLM do it in one call.
**Current:** Only `name`, `primitiveType`, `posX/Y/Z`, `parentPath`.
**Suggested direction:** Allow optional initial tag/layer/active/static at creation time (planner decides format). Also see G3 below.
**Confidence:** high

### D3 — `gameobject-create` has `parentPath` only — no `parentInstanceId`
**Location:** `gameobject-create` param `parentPath`
**Issue:** Every other tool in this domain accepts `instanceId`-style targeting alongside path. `gameobject-create` accepts only `parentPath` (string), forcing the caller to know the path even when an instanceId is on hand from a previous tool call. Inconsistent with `gameobject-set-parent`, `gameobject-duplicate`, `gameobject-update`, etc.
**Current:** `string parentPath = ""`
**Suggested direction:** Add a `parentInstanceId` companion parameter, mirroring `gameobject-set-parent`. Defaults: both empty / 0 → scene root. Planner to confirm the default order.
**Confidence:** high

### D4 — `gameobject-create` always uses `worldPositionStays: true` when parenting
**Location:** `gameobject-create` parenting block
**Issue:** Hard-coded `worldPositionStays: true` matches what most callers want for "place at world position X under parent Y" — but `gameobject-set-parent` exposes this as a configurable param. Inconsistent. For a primitive being created visually under a parent, the caller might want `worldPositionStays: false` so the supplied `posX/Y/Z` are interpreted as local. The signature claims "World-space X position" so the current behavior is technically correct, but the asymmetry between the two tools is worth noting.
**Current:** Hard-coded `worldPositionStays: true`. Position params are documented as world-space.
**Suggested direction:** Either expose `worldPositionStays` (then position param semantics depend on it) or leave it world-only and document why explicitly. Planner decides.
**Confidence:** medium

### D5 — `gameobject-find` `maxResults = 50` but no parameter for "all"
**Location:** `gameobject-find` param `maxResults`
**Issue:** Default 50 is sensible. Hard cap at 500 (silently lowered in code) is also sensible. But there's no escape hatch for "I really want all matches" — passing `int.MaxValue` is silently capped to 500 with no warning in the response. Most callers won't know.
**Current:** `int maxResults = 50`, capped at 500 silently.
**Suggested direction:** Either document the 500 cap in the param description, or surface a warning in the response when the cap clips. Don't change behavior without planner approval.
**Confidence:** medium

### D6 — `gameobject-look-at` defaults all target coords to 0
**Location:** `gameobject-look-at` params `targetX`, `targetY`, `targetZ`
**Issue:** With all defaults at 0, calling `gameobject-look-at` with only `instanceId` makes the source object look at world origin — that may be intentional but it's a surprise default. There's no way for the tool to detect "user forgot to pass target" because `(0,0,0)` is a valid world position. A small Boolean `useTarget = false` flag, or making `targetName` the only target-specifying mechanism, would be cleaner. Flag for planner.
**Current:** All `targetX/Y/Z = 0f`, `targetName = ""`, no required-target signal.
**Suggested direction:** Planner decides: either require an explicit "target mode" flag, or document that the default is "look at world origin."
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Cannot create Sprite/2D GameObjects
**Workflow:** "Spawn a 2D Sprite GameObject in the scene with a given sprite asset." Standard for any 2D game (the test project Jurassic Survivors is 2D URP).
**Current coverage:** `gameobject-create` supports only 3D primitives (`Cube`, `Sphere`, `Capsule`, `Cylinder`, `Plane`, `Quad`) and `Empty`. After creation, the LLM would need `component-add` (Component domain) to add a `SpriteRenderer`, then assign a Sprite asset to its `sprite` property — `component-update` may or may not support `Object` reference assignment (out of scope for this audit).
**Missing:** No tool wraps Unity's `GameObject Create > 2D Object > Sprite` menu. The 3D primitive enum in `gameobject-create` line 77-115 has no `Sprite` case. Searched the entire `Editor/Tools/` tree for `SpriteRenderer` references — zero matches:
```
Grep "SpriteRenderer|2d-sprite|sprite-create" path=Editor/Tools → No files found
```
A 2D-native creation path is absent. Workaround requires `gameobject-create` (Empty) → `component-add` (SpriteRenderer) → asset-binding for the sprite — at least 3 tool calls and the asset-binding step depends on Component domain capability that is not validated here.
**Evidence:** `Tool_GameObject.Create.cs` lines 77-116 enumerate exactly: empty, cube, sphere, capsule, cylinder, plane, quad. Default is "Empty". No 2D path.
**Confidence:** high (full domain coverage confirmed; cross-domain `Editor/Tools/` Grep for SpriteRenderer/Sprite returned zero matches)

### G2 — Cannot reorder siblings in the hierarchy
**Workflow:** "Make this UI element render last (top of canvas)" or "move this child to the top of its parent's child list." Standard editor operation: drag-reorder in the Hierarchy panel, or `Transform.SetSiblingIndex` in code.
**Current coverage:** None. `gameobject-set-parent` reparents but doesn't control sibling order within a parent. `gameobject-update` doesn't touch hierarchy.
**Missing:** No wrapper for `Transform.SetSiblingIndex(int)`, `Transform.SetAsFirstSibling()`, or `Transform.SetAsLastSibling()`. Confirmed by Grep across entire `Editor/Tools/`:
```
Grep "SetSiblingIndex" path=Editor/Tools → No matches found
```
This is a real gap for UI work and for any hierarchy where order matters (UGUI z-order, deterministic Awake/Start order via hierarchy).
**Evidence:** Zero hits for `SetSiblingIndex` across the entire tools tree. `gameobject-set-parent` line 68: `Undo.SetTransformParent(child.transform, newParent, worldPositionStays, ...)` — sets parent but appends as last sibling (Unity default), no index control.
**Confidence:** high (full tree Grep confirms absence)

### G3 — Atomic create-with-properties workflow requires multi-call orchestration
**Workflow:** "Create an enemy GameObject named Goblin, on layer Enemies, tagged 'Enemy', static, parented under World/Enemies." A single high-level intent.
**Current coverage:** `gameobject-create` (sets name, primitive, position, parent) → `gameobject-update` (sets tag, layer, isStatic). Two calls minimum.
**Missing:** No single-call create-with-full-properties tool. The current shape is workable but every "create populated GameObject" intent costs at least 2 tool calls and the LLM has to remember to chain them.
**Evidence:** `Tool_GameObject.Create.cs` parameter list (lines 44-51) is `name, primitiveType, posX/Y/Z, parentPath` — no tag/layer/active/static. `Tool_GameObject.Update.cs` parameter list (lines 38-44) is name/tag/layer/isActive/isStatic — disjoint set. No tool merges them.
**Confidence:** high

### G4 — No tool exposes recursive `GetComponentInChildren` lookup for GameObjects
**Workflow:** "Find the Animator component anywhere under this prefab subtree." A frequent inspection pattern.
**Current coverage:** `gameobject-find` `by_component` walks the active scene from roots downward — there's no way to anchor the search at a specific GameObject. `gameobject-get` lists components on the GameObject itself plus optional direct children — does not recurse.
**Missing:** No "find component within subtree of GameObject X" tool. Workaround is `gameobject-get(includeChildren=true)` per node, which is N tool calls for an N-deep tree. Cross-domain reference: `Tool_Component.Get.cs` and `Tool_Component.List.cs` exist (not audited here), but their scope re. recursion is unverified.
**Evidence:** `gameobject-find` always starts from `SceneManager.GetActiveScene().GetRootGameObjects()` (line 72, 99, 120, 132). `gameobject-get` `includeChildren` walks only direct children (`for (int i = 0; i < t.childCount; i++)` line 93, no recursion). No anchored-subtree search anywhere in the domain.
**Confidence:** medium (full domain confirmed; Component domain not deeply audited so cross-domain coverage is partial — there may be a Component-domain tool that fills this gap)

### G5 — Active-scene-only restriction on Find
**Workflow:** "Find all GameObjects named 'SpawnPoint' in any loaded scene." Common in projects with additive scene loading or multi-scene editing.
**Current coverage:** `gameobject-find` searches only `SceneManager.GetActiveScene()`.
**Missing:** No tool covers `SceneManager.sceneCount`/`GetSceneAt(i)` traversal. An LLM working with additive scenes will get false-negative "not found" responses.
**Evidence:** All four search branches use `SceneManager.GetActiveScene().GetRootGameObjects()` (lines 72, 99, 120, 132 of Find.cs). No iteration over `SceneManager.GetSceneAt`.
**Confidence:** high (full domain coverage)

### G6 — No way to query world transform without a full info dump
**Workflow:** "Where is the Player right now?" (just position, no components, no children).
**Current coverage:** `gameobject-get` returns name, instanceId, tag, layer, active flags, scene, full transform, components (default true), and optionally children. Even with `includeComponents=false, includeChildren=false`, the response is ~10 lines for what could be 3.
**Missing:** No lightweight `gameobject-get-transform` or `transform-get` tool. Note that `Tool_Transform.cs` has only Move/Rotate/Scale operations, no read-only `Get`. So the only way to read a transform is through `gameobject-get`, which is heavier than necessary and not labeled `ReadOnlyHint = true` on the transform-only path (it IS labeled ReadOnly overall, fine).
**Evidence:** `Tool_Transform/` Glob shows only Move, Rotate, Scale — no Get. `gameobject-get` always emits full block (lines 53-65 always run regardless of include* flags).
**Confidence:** high (Transform domain Glob confirms three files, none read-only)

---

## 6. Priority Ranking

Priority = Impact x (6 - Effort). Sorted descending.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | No 2D Sprite GameObject creation path; blocks 2D workflows entirely. |
| 2 | R1 | Redundancy | 5 | 3 | 15 | high | `gameobject-move-relative` overlaps with `transform-move`; LLM disambiguation cost. |
| 3 | G3 | Capability Gap | 4 | 2 | 16 | high | Create-with-properties needs 2 calls; merge into `gameobject-create`. |
| 4 | D3 | Default Issue | 4 | 1 | 20 | high | `gameobject-create` lacks `parentInstanceId`; trivial to add for consistency. |
| 5 | D2 | Default Issue | 4 | 2 | 16 | high | `gameobject-create` missing tag/layer/active/static initial values (companion to G3). |
| 6 | G2 | Capability Gap | 4 | 2 | 16 | high | No `SetSiblingIndex` wrapper anywhere; UI/hierarchy ordering blocked. |
| 7 | R2 | Redundancy | 3 | 3 | 9 | high | `gameobject-select` vs `selection-set`; cross-domain duplication. |
| 8 | A7 | Ambiguity | 3 | 1 | 15 | high | `move-relative` direction-frame priority not in `[Description]`, only XML. |
| 9 | A6 | Ambiguity | 3 | 1 | 15 | high | `gameobject-select` `objectName` vs `objectPath` are functionally identical. |
| 10 | D1 | Default Issue | 3 | 2 | 12 | medium | `gameobject-update` triple-sentinel convention (`""`, `-1`, int tri-state) is non-uniform. |
| 11 | G6 | Capability Gap | 3 | 2 | 12 | high | No lightweight transform-get tool; heavy `gameobject-get` is the only path. |
| 12 | A4 | Ambiguity | 2 | 1 | 10 | medium | `set-parent` "unparent" requires both params empty; param-level descriptions don't say so jointly. |
| 13 | G5 | Capability Gap | 2 | 2 | 8 | high | Find restricted to active scene; no additive-scene traversal. |
| 14 | G4 | Capability Gap | 2 | 3 | 6 | medium | No anchored-subtree component search. |

Top three by raw priority:
1. **D3** (priority 20) — add `parentInstanceId` to `gameobject-create`. One-line fix, eliminates a real LLM failure mode (path-only when instanceId is on hand).
2. **G3 / D2** (priority 16) — fold tag/layer/active/static into `gameobject-create`. Also resolves R4 indirectly.
3. **G1 / R1 / G2** (priority 15) — three structurally larger items (2D create path, transform-move overlap, sibling order). All worth addressing in a consolidation pass.

---

## 7. Notes

**Cross-domain coupling (heads up to planner):**
- `Tool_Transform.FindGameObject` is the de-facto shared lookup helper for this domain. 9 of 10 GameObject tools call it. Any change to its signature (e.g. switching off the deprecated `InstanceIDToObject`) ripples across both domains.
- That helper currently uses `EditorUtility.InstanceIDToObject` with `#pragma warning disable CS0618`. CLAUDE.md mandates `EntityIdToObject` (Unity 6000.3). `Selection/Tool_Selection.Set.cs` line 67 already uses `EntityIdToObject`. Migration is overdue but is a Transform-domain concern, not a GameObject-domain concern. Worth flagging when planner runs the Transform audit.

**Inconsistencies observed but not findings on their own:**
- `gameobject-create` uses raw `GameObject.Find(parentPath)` (line 64) instead of the shared `Tool_Transform.FindGameObject` helper. Every other tool in the domain uses the helper. Easy alignment.
- `gameobject-create` is the only tool that doesn't accept an `instanceId` for its target ref (D3) AND the only one that bypasses the shared helper. These are likely linked — fixing one likely fixes the other.

**Workflows intentionally deferred:**
- Component attachment / removal workflows (Component domain). Mentioned in G4 but not deeply traced.
- Prefab instantiation workflows (Prefab domain). The cluster `gameobject-create` / `prefab-instantiate` likely overlaps for "create from existing prefab" intent, but Prefab domain is out of scope.

**Open questions for the reviewer:**
- Should `transform-move` and `gameobject-move-relative` be merged into one tool, or should one explicitly cite the other in its `[Description]` ("for named-direction moves with reference frames, use `gameobject-move-relative`; for explicit XYZ moves, use `transform-move`")? The latter is much cheaper and probably enough.
- Should `gameobject-select` be deprecated in favor of `selection-set` plus the auto-select side effects in `create`/`duplicate`? Or should `selection-set` itself absorb the `ping` flag?
- Is the int-tri-state convention in `gameobject-update` deliberate (transport-layer constraint) or organic? If organic, the planner can normalize it.

**Limits of this audit:**
- I did not run `dotnet build`, did not validate that descriptions match generated tool schemas, and did not test any tool live against a Unity project.
- Cross-domain absence claims (G1, G2) were validated by Grep over `Editor/Tools/`. Cross-domain presence claims (e.g. "Component-domain tools may handle G4") were NOT deeply validated — those depend on Component-domain audit output.
- I did not score the quality of the search algorithms in `gameobject-find` (recursion shape, allocation, edge cases) — that's a code-review concern, not an MCP-tool-quality concern.
