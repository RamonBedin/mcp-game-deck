# Audit Report — AddAssetToScene

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/AddAssetToScene/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via Glob `Editor/Tools/AddAssetToScene/**/*.cs`)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** balanced

**Note on Glob:** the standard pattern `Tool_AddAssetToScene.*.cs` returned zero hits because this domain uses a single non-actioned filename (`Tool_AddAssetToScene.cs`), unlike the convention `Tool_[Domain].[Action].cs` documented in CLAUDE.md. A broader `**/*.cs` glob found the file. This is itself a minor structural anomaly worth flagging to the reviewer (see Section 7).

**Errors encountered during audit:**
- None.

**Files not analyzed:**
- None.

**Cross-domain reads (not part of accounting, used to substantiate redundancy / gap findings):**
- `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs` — direct functional overlap.
- `Editor/Tools/GameObject/Tool_GameObject.Create.cs` — adjacent creation tool.
- Glob over `Editor/Tools/Prefab/`, `Editor/Tools/GameObject/`, `Editor/Tools/Scene/` — surveyed for related instantiation tools.
- Grep over `Editor/Tools/` for `InstantiatePrefab|Instantiate(` — found 4 hits (this file, `Prefab.Instantiate`, `GameObject.Duplicate`, `ProBuilder.MeshOps`).
- Grep over `Editor/Tools/` for `PrefabAssetType|GetPrefabAssetType|isModel|.fbx` — only 2 hits (this file and `Prefab.GetInfo`).

**Reviewer guidance:**
- The domain contains a single tool (`add-asset-to-scene`) that overlaps significantly with `prefab-instantiate`. The headline question for review is **whether this domain should exist at all**, or whether its unique behaviors (model/non-prefab fallback, Y-rotation, ping) should be folded into `prefab-instantiate`.
- All absence claims in this report are backed by complete coverage of the AddAssetToScene domain (1/1 files) and targeted Greps across the whole `Editor/Tools/` tree. Confidence on absence claims is therefore high for this domain, medium for cross-domain assertions where I did not exhaustively read every file.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `add-asset-to-scene` | Scene / Add Asset to Scene | `Tool_AddAssetToScene.cs` | 7 (1 required, 6 optional) | no |

**Tool details:**
- **Method:** `Tool_AddAssetToScene.AddAsset(...)`
- **Method `[Description]`:** "Instantiates a prefab or model asset into the current scene at the specified position and rotation. Returns the created GameObject name and instance ID." (24 words)
- **Parameters:**
  - `string assetPath` (required) — `[Description("Asset path of the prefab or model (e.g. 'Assets/Prefabs/Player.prefab').")]`
  - `float posX = 0f` — `[Description("X position in world space. Default 0.")]`
  - `float posY = 0f` — `[Description("Y position in world space. Default 0.")]`
  - `float posZ = 0f` — `[Description("Z position in world space. Default 0.")]`
  - `float rotY = 0f` — `[Description("Y rotation in degrees (Euler). Default 0.")]`
  - `string name = ""` — `[Description("Optional name for the instantiated GameObject. If empty, uses the prefab name.")]`
  - `string parentName = ""` — `[Description("Optional parent GameObject name to parent the new object under.")]`
- **Internal Unity API surface:**
  - `AssetDatabase.LoadAssetAtPath<GameObject>(...)`
  - `PrefabUtility.GetPrefabAssetType(...)`
  - `PrefabUtility.InstantiatePrefab(GameObject)` (single-arg overload — no parent)
  - `Object.Instantiate(asset, position, rotation)` (fallback for non-prefab assets)
  - `GameObject.Find(parentName)` (name-based lookup, not hierarchy path)
  - `Transform.SetParent(parent, worldPositionStays: true)` (`true`, despite parameter being unnamed)
  - `Undo.RegisterCreatedObjectUndo(...)`
  - `Selection.activeGameObject = ...`
  - `EditorGUIUtility.PingObject(...)`
- **Class-level XML summary:** present (correct per partial-class rule, since this is the file containing `[McpToolType]`).

---

## 2. Redundancy Clusters

### Cluster R1 — Asset instantiation duplicated across two domains
**Members:** `add-asset-to-scene` (AddAssetToScene), `prefab-instantiate` (Prefab)
**Overlap:** Both tools take an `Assets/...prefab` path and instantiate it into the active scene with a world position, optional name override, and optional parent. Of `add-asset-to-scene`'s 7 parameters, **6 are functionally identical** to params on `prefab-instantiate` (assetPath/prefabPath, posX, posY, posZ, name, parent). That is ~86% parameter overlap. The descriptions are almost interchangeable from an LLM's perspective:
- `add-asset-to-scene`: "Instantiates a prefab or model asset into the current scene at the specified position and rotation."
- `prefab-instantiate`: "Loads a Prefab asset and instantiates it into the active scene as a linked prefab instance."

For any user prompt like "instantiate the Player prefab at (0,0,0)", the LLM has no principled way to choose between the two tools.

**Differences (the only things that distinguish them):**
1. `add-asset-to-scene` accepts a `rotY` Y-Euler rotation; `prefab-instantiate` has no rotation params at all.
2. `add-asset-to-scene` falls back to `Object.Instantiate` for non-prefab `GameObject` assets (e.g. raw FBX models in `Assets/`); `prefab-instantiate` requires a real prefab and errors otherwise — though confusingly, `prefab-instantiate` does not actually validate `PrefabAssetType` either, so its behavior on a model asset is unverified.
3. `add-asset-to-scene` uses `GameObject.Find(parentName)` (a flat name search across the scene); `prefab-instantiate` uses `GameObject.Find(parentPath)` but treats it as a hierarchy path. Same API, different documented semantics — a quiet inconsistency.
4. `add-asset-to-scene` validates only `IsNullOrWhiteSpace(assetPath)`; `prefab-instantiate` additionally enforces `StartsWith("Assets/")`.
5. `add-asset-to-scene` calls `EditorGUIUtility.PingObject(...)`; `prefab-instantiate` does not.
6. `add-asset-to-scene` uses `Transform.SetPositionAndRotation`; `prefab-instantiate` sets only `transform.position` after instantiation.
7. `add-asset-to-scene` uses the non-parented `InstantiatePrefab(GameObject)` overload then re-parents; `prefab-instantiate` uses `InstantiatePrefab(GameObject, Transform)` (the parent-aware overload). Both produce a correct linked prefab instance, but the first form is slightly less idiomatic.

**Impact:** High. Two tools with nearly identical surfaces and overlapping intent are a textbook source of LLM tool-selection ambiguity. The 39-domain inventory in CLAUDE.md lists `Prefab` as a Tier-1 domain; `AddAssetToScene` is not even mentioned, which suggests this single-tool domain is a candidate for absorption.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — Description does not disambiguate from `prefab-instantiate`
**Location:** `add-asset-to-scene` — `Tool_AddAssetToScene.cs` line 33
**Issue:** The method `[Description]` makes no attempt to explain when to pick this tool over `prefab-instantiate`. Per the audit checklist, when 2+ tools overlap in purpose, descriptions should contain a "use this when X, not Y" clause.
**Evidence:** `[Description("Instantiates a prefab or model asset into the current scene at the specified position " + "and rotation. Returns the created GameObject name and instance ID.")]` — no mention of model fallback, no contrast with `prefab-instantiate`, no guidance about which tool to prefer.
**Confidence:** high

### A2 — `parentName` semantics ambiguous: name vs. hierarchy path
**Location:** `add-asset-to-scene` param `parentName`
**Issue:** The description says "parent GameObject name", and the implementation calls `GameObject.Find(parentName)`. `GameObject.Find` accepts **either** a top-level GameObject name **or** a slash-separated hierarchy path (e.g. `"World/Enemies"`). The description neither documents this dual mode nor warns about ambiguity when multiple GameObjects share a name. The sister tool `prefab-instantiate` documents the same param as a hierarchy path. An LLM cannot infer which form is expected.
**Evidence:** Param description: `"Optional parent GameObject name to parent the new object under."` — no example, no mention of paths, no mention of duplicates.
**Confidence:** high

### A3 — `rotY`-only rotation is a hidden limitation, not a documented choice
**Location:** `add-asset-to-scene` param `rotY`
**Issue:** Only Y-axis rotation is exposed. The description says "Y rotation in degrees (Euler). Default 0." but does not explain that X- and Z-rotations are intentionally absent, nor does it tell the LLM what to do if it needs full Euler rotation. From the LLM's vantage point, this looks like a coincidental subset rather than a deliberate constraint, leading to silent inability to satisfy "rotate 90 around X" requests.
**Evidence:** Method signature has only `rotY`; description text does not call out the limitation.
**Confidence:** high

### A4 — `assetPath` description does not communicate model-asset support
**Location:** `add-asset-to-scene` param `assetPath`
**Issue:** Description gives only a `.prefab` example. The implementation explicitly handles non-prefab `GameObject` assets via `Object.Instantiate` (lines 63-71), but the description doesn't expose this capability. This is the **only** real differentiator vs. `prefab-instantiate`, and it's invisible to the LLM.
**Evidence:** `[Description("Asset path of the prefab or model (e.g. 'Assets/Prefabs/Player.prefab').")]` — mentions "model" in the noun list but uses only a prefab example. No `.fbx`, no `.obj`, no guidance.
**Confidence:** high

### A5 — Output format is a multi-line `StringBuilder`, not structured
**Location:** `add-asset-to-scene` return value (lines 92-104)
**Issue:** Not strictly an ambiguity in the input contract, but the response is a free-form text block. Downstream tooling and LLMs that need to programmatically chain on the resulting instance ID must regex-parse `"  Instance ID: {n}"`. Other tools in the codebase (e.g. `prefab-instantiate`, line 87) return a single sentence — neither approach is structured. Flagging because output shape inconsistency between two near-duplicate tools compounds R1's selection ambiguity.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `name` default of `""` is sensible but undocumented as "use prefab name"
**Location:** `add-asset-to-scene` param `name`
**Issue:** Default `""` triggers fallback to the prefab's own name. This is reasonable but is a **magic default** — empty string isn't an obviously-valid name. The description does say "If empty, uses the prefab name." which is correct documentation. Listed for completeness; no change needed beyond what's already there.
**Current:** `string name = ""`
**Suggested direction:** acceptable as-is; possibly consider `string? name = null` for stronger intent, but not necessary.
**Confidence:** low

### D2 — `parentName` default of `""` is sensible
**Location:** `add-asset-to-scene` param `parentName`
**Issue:** Default `""` means "no parent / scene root". Also a magic default, also documented. Same status as D1.
**Confidence:** low

### D3 — Position/rotation defaults of `0` are reasonable
**Location:** `posX`, `posY`, `posZ`, `rotY`
**Issue:** None — `(0,0,0)` is a normal placement convention. No finding.
**Confidence:** n/a

(No high-impact default issues identified for this domain.)

---

## 5. Capability Gaps

### G1 — Full rotation (X/Y/Z Euler or Quaternion) not supported
**Workflow:** A developer wants to drop a prefab at a specific orientation other than upright — e.g. a wall tilted 30° forward, a ramp rotated -15° on Z, or an item placed via an arbitrary Euler triple from a level-design spec. Standard Unity workflow uses `Transform.eulerAngles = new Vector3(x, y, z)` or `Quaternion.Euler(x, y, z)`.
**Current coverage:** Only `rotY` is exposed. The implementation builds rotation as `Quaternion.Euler(0, rotY, 0)` (line 59), discarding any caller-specified X or Z axis.
**Missing:** `rotX` and `rotZ` parameters (or a full `Vector3 eulerAngles` shape). Confirmed via Grep over `Tool_Prefab.Instantiate.cs` for `rotX|rotZ|eulerAngles|Quaternion.Euler` — zero matches, so `prefab-instantiate` cannot fill the gap either: **no tool in the Prefab or AddAssetToScene domains can instantiate with full rotation**.
**Evidence:** `Tool_AddAssetToScene.cs` line 59: `var rotation = Quaternion.Euler(0, rotY, 0);` — X and Z hard-coded to 0. `Tool_Prefab.Instantiate.cs` has no rotation parameter at all.
**Confidence:** high

### G2 — Local-space placement / `worldPositionStays = false` not exposed
**Workflow:** A developer wants to add a prefab as a **local child** of an existing object — e.g. attaching a "Hat" prefab to an "Avatar" so the prefab's `(0,0,0)` lands at the avatar's transform, not at world origin. This requires `SetParent(parent, worldPositionStays: false)` plus `localPosition = ...`.
**Current coverage:** The tool always uses `SetParent(parent.transform, true)` (world-position-stays = true) and only writes world-space coordinates. There is no way to tell it "I want these coordinates interpreted in the parent's local space."
**Missing:** A `useLocalSpace` (or equivalent) flag, or a separate `localX/localY/localZ` triple. This is a common workflow when laying out prefabs under a logical parent.
**Evidence:** `Tool_AddAssetToScene.cs` line 84: `instance.transform.SetParent(parent.transform, true);` — hard-coded `true`. No subsequent `localPosition` write.
**Confidence:** high

### G3 — `parentName` collision handling: no path/ID disambiguation
**Workflow:** A scene has two GameObjects named `"Enemies"` (e.g. one in Scene A, another nested under a tilemap). The user wants to parent under the **specific** one. `GameObject.Find` returns the first match in scene order with no guarantee of which.
**Current coverage:** Tool accepts only a name string and silently picks whatever `GameObject.Find` returns first. No parameter for a hierarchy path, instance ID, or scene scope.
**Missing:** Either accept a hierarchy path (matching `prefab-instantiate`'s convention), accept a parent **instance ID** (matching the rest of the GameObject domain's identity model — see `gameobject-create`'s `parentPath`), or both. The simplest cross-domain consistency improvement would be aligning on `parentPath`.
**Evidence:** `Tool_AddAssetToScene.cs` line 80: `var parent = GameObject.Find(parentName);` — name-only lookup.
**Confidence:** high

### G4 — Silent failure when `parentName` is supplied but not found
**Workflow:** User asks to parent under a typo'd or non-existent GameObject name. They expect an error so they can fix the input.
**Current coverage:** Lines 78-86 — if `parent == null`, the branch silently exits without setting a parent. The instance is created at scene root and the success message reports `Position: ...` without any warning that the requested parent was missing.
**Missing:** Equivalent error-on-not-found behavior to `prefab-instantiate` (which returns `ToolResponse.Error($"Parent GameObject not found at path '{parentPath}'.")`). The asymmetry between these two near-duplicate tools is itself a hazard: same prompt, same data, different outcome.
**Evidence:** `Tool_AddAssetToScene.cs` lines 80-86:
```
var parent = GameObject.Find(parentName);
if (parent != null)
{
    instance.transform.SetParent(parent.transform, true);
}
```
No `else` branch, no error, no warning logged.
**Confidence:** high

### G5 — No way to instantiate as a non-linked copy (break-prefab-on-instantiate)
**Workflow:** A user wants to add a prefab to the scene **without** keeping the prefab link — i.e. a one-off copy that won't propagate prefab edits. Unity supports this via `Object.Instantiate(prefabAsset)` rather than `PrefabUtility.InstantiatePrefab`, and via `PrefabUtility.UnpackPrefabInstance`.
**Current coverage:** This tool always preserves the prefab link when the asset is a prefab (line 65). There's no parameter to opt out.
**Missing:** A `preserveLink` or `unpack` boolean parameter. Likely low-priority for v1.x but worth noting since the domain's name ("Add Asset to Scene") implies generality the tool doesn't deliver.
**Evidence:** `Tool_AddAssetToScene.cs` lines 63-71 — branching is on `PrefabAssetType.NotAPrefab` only; no caller control.
**Confidence:** medium

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher is better.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|------------|---------|
| 1 | R1 | Redundancy | 5 | 2 | 20 | high | `add-asset-to-scene` and `prefab-instantiate` are near-duplicates; ~86% param overlap and indistinguishable descriptions cause guaranteed LLM tool-selection ambiguity. |
| 2 | A1 | Ambiguity | 5 | 1 | 25 | high | No "use this when X, not Y" clause to disambiguate from `prefab-instantiate` — single description edit. |
| 3 | G1 | Capability Gap | 4 | 2 | 16 | high | Only Y-axis rotation supported; X/Z hard-coded to 0. Affects any non-trivial placement. |
| 4 | G4 | Capability Gap | 4 | 1 | 20 | high | Missing parent silently ignored — diverges from `prefab-instantiate` behavior, hides user errors. |
| 5 | A2 | Ambiguity | 4 | 1 | 20 | high | `parentName` doesn't document name-vs-path semantics; conflicts with sister tool's convention. |
| 6 | G3 | Capability Gap | 3 | 2 | 12 | high | Name-based parent lookup can't disambiguate duplicate names; misaligned with rest of codebase's path/ID conventions. |
| 7 | A3 | Ambiguity | 3 | 1 | 15 | high | `rotY`-only limitation is hidden, not documented. |
| 8 | A4 | Ambiguity | 3 | 1 | 15 | high | Model-asset support (the tool's only real differentiator from `prefab-instantiate`) is invisible in description. |
| 9 | G2 | Capability Gap | 3 | 3 | 9 | high | No local-space placement / `worldPositionStays=false` option. |
| 10 | G5 | Capability Gap | 2 | 2 | 8 | medium | No way to instantiate as a non-linked / unpacked copy. |
| 11 | A5 | Ambiguity | 2 | 2 | 8 | medium | Free-form `StringBuilder` output not structured; inconsistent with sister tool's one-line response. |

---

## 7. Notes

- **Structural observation — file-naming convention.** Per CLAUDE.md ("One file per tool action: `Tool_[Domain].[Action].cs`"), this domain should arguably be named `Tool_AddAssetToScene.AddAsset.cs`. The current `Tool_AddAssetToScene.cs` (no action segment) is the reason my initial action-pattern Glob returned zero. Worth flagging to the consolidation-planner if/when this domain is touched.
- **The headline question for the reviewer.** Should `AddAssetToScene` continue to exist as a separate domain at all? Three plausible futures, in increasing scope:
  1. Keep the domain, sharpen its description to clearly own "model + non-prefab GameObject asset" cases (the only thing it does that `prefab-instantiate` doesn't); fix G1, G3, G4 in place.
  2. Fold the unique behaviors (full rotation, model fallback, parent-by-path-or-ID, ping) into `prefab-instantiate`, then delete this domain.
  3. Promote a unified `scene-add-asset` (or rename `prefab-instantiate` → `prefab-instantiate-or-import`) and absorb both. This is the cleanest answer but the most invasive.
  My audit can't decide between these — the consolidation-planner agent should.
- **Cross-domain dependencies noted.** No other tool in `Editor/Tools/` calls into this tool. No tests reference it (no test files were Glob-able under `Editor/Tools/AddAssetToScene/`).
- **Things I deliberately did not investigate.** Whether `prefab-instantiate` itself has audit-worthy issues — that's its own domain's audit. I only used it as a comparator for redundancy and gap analysis.
- **Open question for Ramon.** Was the Y-only rotation a deliberate UX choice (e.g. for top-down 2D-style placement, given Jurassic Survivors' 2D URP target), or an incidental shortcut? The answer drives whether G1 is a description fix (A3 alone) or a real capability addition.
