# Audit Report — Terrain

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Terrain/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/Terrain/Tool_Terrain.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** ✅ balanced

**Errors encountered during audit:** None.

**Files not analyzed (if any):** None.

**Absence claims in this report:**
Absence claims are permitted because accounting is balanced. Cross-domain absence claims (e.g. "no other tool wraps `TerrainData`") are backed by an explicit `Grep` over `Editor/Tools/` for `TerrainData|TerrainLayer|treePrototype|detailPrototype|SetHeights|GetHeights|alphamap|detailmap`, which returned matches only in the two Terrain files.

**Reviewer guidance:**
- The Terrain domain is one of the smallest in the codebase (only 2 tools). The most important section here is **Section 5 (Capability Gaps)** — virtually every meaningful Unity terrain workflow is unsupported.
- The two existing tools are well-structured (good XML docs, `ReadOnlyHint` correctly used on `GetInfo`, `Undo` registration on Create, parameters validated). Findings under Sections 2-4 are minor; the domain's pain point is breadth, not quality.
- This domain depends on `Tool_Transform.FindGameObject` (cross-domain helper). If that helper's signature changes, `terrain-get-info` is affected.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `terrain-create` | Terrain / Create | `Tool_Terrain.Create.cs` | 6 | no |
| `terrain-get-info` | Terrain / Get Info | `Tool_Terrain.GetInfo.cs` | 2 | yes |

**Per-tool parameter detail:**

### `terrain-create`
- `name` (string, default `"Terrain"`) — "Name for the terrain. Default 'Terrain'."
- `width` (float, default `500f`) — "Terrain width in world units. Default 500."
- `height` (float, default `200f`) — "Terrain height in world units. Default 200."
- `length` (float, default `500f`) — "Terrain length (depth) in world units. Default 500."
- `heightmapResolution` (int, default `513`) — "Heightmap resolution (power of 2 + 1). Default 513."
- `savePath` (string, default `"Assets"`) — "Save path for terrain data. Default 'Assets'."

**Method [Description]:** "Creates a new Terrain object with the specified dimensions and resolution."

**Unity API surface used:** `TerrainData` (constructor + `heightmapResolution`/`size` props), `AssetDatabase.GenerateUniqueAssetPath`, `AssetDatabase.CreateAsset`, `Terrain.CreateTerrainGameObject`, `Undo.RegisterCreatedObjectUndo`.

### `terrain-get-info`
- `instanceId` (int, default `0`) — "Instance ID of the Terrain GameObject. 0 to use objectPath."
- `objectPath` (string, default `""`) — "Name or path of the Terrain GameObject. Empty uses the first Terrain found."

**Method [Description]:** "Returns information about the active Terrain or a Terrain found by name."

**Unity API surface used:** `Tool_Transform.FindGameObject` (cross-domain helper), `GameObject.GetComponent<Terrain>`, `Terrain.activeTerrain`, `Terrain.terrainData` (size, heightmapResolution, detailResolution, alphamapResolution, terrainLayers, treeInstanceCount).

---

## 2. Redundancy Clusters

No redundancy clusters identified. The two tools have orthogonal purposes (create vs. inspect) and non-overlapping parameter sets. The domain is too sparse to be redundant — the opposite problem (gaps) dominates.

---

## 3. Ambiguity Findings

### A1 — Vague method-level description on `terrain-create`
**Location:** `terrain-create` — `Tool_Terrain.Create.cs` line 35
**Issue:** The `[Description]` is 10 words and contains no example or disambiguation. While only one create-style tool exists in the domain today, the description gives the LLM no hint about (a) where the asset is saved, (b) that an associated `TerrainData` asset is generated automatically, or (c) what reasonable values for `heightmapResolution` look like beyond the default. No "use this when..." clause distinguishes it from a hypothetical `asset-create` (which exists in the Asset domain but does NOT support `TerrainData` — see G3).
**Evidence:** `[Description("Creates a new Terrain object with the specified dimensions and resolution.")]`
**Confidence:** medium

### A2 — `heightmapResolution` parameter description omits the validation rule example
**Location:** `terrain-create` param `heightmapResolution`
**Issue:** Parameter description says "(power of 2 + 1)" but does not enumerate concrete legal values. Unity restricts `TerrainData.heightmapResolution` to a finite set (33, 65, 129, 257, 513, 1025, 2049, 4097); arbitrary "power of 2 + 1" values like 9 or 17 are technically legal but produce tiny terrains. An LLM passing `heightmapResolution = 1000` will silently get a clamped/snapped value with no warning. The XML doc parent comment says "(e.g. 513)" but the `[Description]` attribute (which is what the LLM sees per CLAUDE.md note) does not include any example beyond the default.
**Evidence:** `[Description("Heightmap resolution (power of 2 + 1). Default 513.")]` (line 41)
**Confidence:** medium

### A3 — `savePath` parameter description does not mention the prefix rule
**Location:** `terrain-create` param `savePath`
**Issue:** The validation logic at line 47 enforces `savePath.StartsWith("Assets/") || savePath == "Assets"`, but the parameter `[Description]` does not warn about this. The LLM will only discover the rule by reading the error message returned at runtime.
**Evidence:** `[Description("Save path for terrain data. Default 'Assets'.")]` (line 42) — vs. validation: `if (!savePath.StartsWith("Assets/") && savePath != "Assets")` (line 47)
**Confidence:** high

### A4 — `terrain-get-info` description does not disambiguate "active" vs. "by name" precedence
**Location:** `terrain-get-info` — `Tool_Terrain.GetInfo.cs` line 26
**Issue:** Description says "active Terrain or a Terrain found by name". The actual logic (lines 36-49) is: if either `instanceId != 0` OR `objectPath` is set, it tries that lookup first; if it fails to find a Terrain component (e.g. wrong GameObject), it silently falls back to `Terrain.activeTerrain`. This silent fallback is non-obvious — if an LLM passes a wrong path, it gets info about a *different* terrain with no warning.
**Evidence:** Lines 36-49 of `Tool_Terrain.GetInfo.cs`. The fallback at line 48 (`terrain = Terrain.activeTerrain;`) is reached even when `objectPath` was provided but produced no Terrain component.
**Confidence:** high

### A5 — `instanceId = 0` sentinel is a magic value
**Location:** `terrain-get-info` param `instanceId`
**Issue:** `0` is used as "not provided" sentinel. This is consistent with other tools using `Tool_Transform.FindGameObject` so it is not a domain-specific issue, but the description does not explain *why* 0 means "skip" (instance IDs in Unity are non-zero, but an LLM may not know that).
**Evidence:** `[Description("Instance ID of the Terrain GameObject. 0 to use objectPath.")]` — terse but acceptable. Lower priority because pattern is repeated across the codebase.
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `savePath` default `"Assets"` writes to project root
**Location:** `terrain-create` param `savePath`
**Issue:** Default `"Assets"` causes the generated `*_Data.asset` file to land at the top of the project. Most projects organize terrain data under `Assets/Terrains/` or similar. This is a magic default that works but is rarely what the user wants.
**Current:** `string savePath = "Assets"`
**Suggested direction:** Consider a more conventional default folder (e.g. `Assets/Terrains`) and/or auto-create the folder if missing (the `Tool_Asset.Create.cs` pattern at lines 52-58 already demonstrates this idiom). Or document that the default is project root and that callers should override.
**Confidence:** medium

### D2 — `width`/`height`/`length` defaults are reasonable but undocumented as a coupled set
**Location:** `terrain-create` params `width`, `height`, `length`
**Issue:** Defaults `500/200/500` produce a 500x500 m terrain with 200 m vertical range. These are sensible Unity defaults but the LLM has no signal that `width` and `length` should typically match (square terrains are conventional) or that `height` is in the same world units. Not a bug — minor documentation concern.
**Current:** `float width = 500f, float height = 200f, float length = 500f`
**Suggested direction:** No default change needed. Consider a one-line note in the method `[Description]` like "Terrains are commonly square (width = length)."
**Confidence:** low

### D3 — `terrain-get-info` has no required parameters; behavior depends entirely on scene state
**Location:** `terrain-get-info` (whole tool)
**Issue:** Both parameters are optional. With no args, the tool reads `Terrain.activeTerrain`, which is implementation-defined (Unity picks one if multiple are loaded). Calling with no args in a multi-terrain scene gives non-deterministic results. This is a default-related concern: the "default behavior" silently picks one of N terrains.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** No signature change required, but the description should warn that with no arguments the result is whichever Terrain Unity considers active, which may not be unique. (Overlaps with A4.)
**Confidence:** medium

---

## 5. Capability Gaps

This is the dominant section for this domain. The two existing tools cover only **terrain creation** and **terrain inspection**. Virtually every other terrain workflow a Unity developer would expect to automate is unsupported.

### G1 — Heightmap sculpting / SetHeights
**Workflow:** Author terrain shape programmatically — flatten, raise, lower, or apply a procedural heightmap (e.g. import a noise pattern, load a heightmap PNG, level a region for a building site).
**Current coverage:** None. `terrain-create` produces a flat terrain. `terrain-get-info` reports resolution but cannot read or write heights.
**Missing:** No tool wraps `TerrainData.SetHeights(int xBase, int yBase, float[,] heights)` or `TerrainData.GetHeights(...)`. The fundamental "modify the terrain shape" Unity API is entirely absent.
**Evidence:** `Grep` for `SetHeights|GetHeights` across `Editor/Tools/` matched only the Terrain domain files, and neither file references those methods. `Tool_Terrain.GetInfo.cs` line 56 reads `terrain.terrainData` but only inspects metadata (`size`, `heightmapResolution`, etc.) — it does not touch heightmap arrays.
**Confidence:** high

### G2 — Terrain layer (texture / splatmap) management
**Workflow:** Apply textures to a terrain — assign grass, rock, sand `TerrainLayer` assets and paint them via splatmaps so the terrain looks like more than a flat-shaded mesh.
**Current coverage:** `terrain-get-info` reports `data.terrainLayers.Length` (a count). Nothing else.
**Missing:** No tool wraps `TerrainData.terrainLayers` setter, `TerrainLayer` asset creation, or `TerrainData.SetAlphamaps(...)` for painting splatmap weights. A terrain created today via `terrain-create` is permanently visually flat unless the user opens the Editor and paints manually.
**Evidence:** `Grep` for `TerrainLayer|alphamap` across `Editor/Tools/` matched only the Terrain files, and neither sets layers or alphamaps. `Tool_Terrain.GetInfo.cs` line 64 only reads `data.terrainLayers.Length`.
**Confidence:** high

### G3 — TerrainData asset creation outside `terrain-create`
**Workflow:** Create a standalone `TerrainData` asset for reuse (e.g. share one heightmap across multiple Terrain GameObjects, or pre-generate terrain data assets for streaming).
**Current coverage:** `terrain-create` creates a `TerrainData` and a `Terrain` GameObject together (coupled). The generic `asset-create` tool in the Asset domain does NOT support `TerrainData`.
**Missing:** No way to create a `TerrainData` asset without spawning a Terrain GameObject. No way to assign an existing `TerrainData` to a different Terrain GameObject.
**Evidence:** `Tool_Asset.Create.cs` line 33 enumerates supported asset types: `'Material', 'RenderTexture', 'PhysicMaterial', 'AnimatorController'`. The switch at lines 62-110 has no `TerrainData` case; the default branch (line 108) returns "Unsupported assetType". `Tool_Terrain.Create.cs` lines 52-67 always creates GameObject + asset together.
**Confidence:** high

### G4 — Tree placement and tree prototype management
**Workflow:** Add tree prefabs to a terrain — register a tree prototype and place tree instances at given positions.
**Current coverage:** `terrain-get-info` reports `data.treeInstanceCount` (read-only count). No write path.
**Missing:** No tool wraps `TerrainData.treePrototypes` (setter) or `TerrainData.SetTreeInstances(...)`. The Unity tree painting workflow is fully unsupported.
**Evidence:** `Grep` for `treePrototype` across `Editor/Tools/` matched only the Terrain files, and neither file writes tree prototypes. `Tool_Terrain.GetInfo.cs` line 65 reads only the count.
**Confidence:** high

### G5 — Detail / grass placement
**Workflow:** Add ground details — grass billboards, small rocks, foliage — via `TerrainData.detailPrototypes` and `SetDetailLayer`.
**Current coverage:** `terrain-get-info` reports `data.detailResolution`. Nothing else.
**Missing:** No tool wraps `TerrainData.detailPrototypes` or `TerrainData.SetDetailLayer(...)`. The "paint grass" workflow is unsupported end-to-end.
**Evidence:** `Grep` for `detailPrototype|detailmap` across `Editor/Tools/` matched only the Terrain files, and neither file references these APIs.
**Confidence:** high

### G6 — Terrain-from-heightmap-image import
**Workflow:** Import a heightmap PNG/RAW into a terrain — a very common workflow (e.g. real-world DEM data, externally generated noise).
**Current coverage:** None.
**Missing:** No tool reads a Texture2D / PNG and applies its luminance to terrain heights. This would compose `Texture2D.GetPixels()` with `TerrainData.SetHeights()`. Both halves are missing (G1 covers the SetHeights gap; this is the higher-level macro).
**Evidence:** No `SetHeights` reference anywhere in the codebase; `Tool_Terrain.Create.cs` only creates a flat terrain.
**Confidence:** high

### G7 — Terrain neighbor stitching for multi-tile terrains
**Workflow:** Create adjacent terrain tiles and stitch their edges so heights match (Unity's `Terrain.SetNeighbors`).
**Current coverage:** `terrain-create` can create terrains, but each is independent.
**Missing:** No tool calls `Terrain.SetNeighbors(...)` to wire up tile adjacency. Multi-tile terrains will show seams.
**Evidence:** `Grep` for `SetNeighbors` across `Editor/Tools/` returned no files (verified by absence in the broader terrain-related grep).
**Confidence:** medium (workflow is less common than G1/G2 but important when it matters)

---

## 6. Priority Ranking

Priority formula: Impact × (6 − Effort). Higher = better ROI for the human reviewer.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | No tool sets terrain heights — terrains are stuck flat. Foundational. |
| 2 | G2 | Capability Gap | 5 | 4 | 10 | high | No tool assigns terrain layers / paints splatmaps — terrains are unpainted. |
| 3 | G6 | Capability Gap | 4 | 3 | 12 | high | No heightmap-from-image import. Macro that depends on G1. |
| 4 | G4 | Capability Gap | 4 | 3 | 12 | high | No tree prototype / tree instance placement. |
| 5 | G5 | Capability Gap | 3 | 3 | 9 | high | No grass / detail placement. |
| 6 | A4 | Ambiguity | 4 | 1 | 20 | high | `terrain-get-info` silently falls back to active terrain when lookup fails — quick description fix. |
| 7 | A3 | Ambiguity | 3 | 1 | 15 | high | `savePath` "Assets/" prefix rule undocumented at param level — quick description fix. |
| 8 | G3 | Capability Gap | 3 | 2 | 12 | high | `TerrainData` cannot be created standalone (not in `asset-create`, not in `terrain-create`). |
| 9 | G7 | Capability Gap | 3 | 3 | 9 | medium | No `Terrain.SetNeighbors` — multi-tile terrains seam. |
| 10 | A2 | Ambiguity | 3 | 1 | 15 | medium | `heightmapResolution` legal-values list missing from param description. |
| 11 | D1 | Default | 2 | 1 | 10 | medium | `savePath` default `"Assets"` dumps assets in project root. |
| 12 | A1 | Ambiguity | 2 | 1 | 10 | medium | `terrain-create` method description vague (10 words, no example). |
| 13 | D3 | Default | 2 | 1 | 10 | medium | `terrain-get-info` no-arg call is non-deterministic in multi-terrain scenes (overlaps A4). |

**Recommended order of attack (for the planner):**
1. Cheap wins first: A4, A3, A2, A1, D1, D3 — all are description / default tweaks (Effort = 1) and bring the existing tools to a higher quality bar before expanding the surface.
2. Then G1 + G6 as a coherent feature: "set terrain heights" + "import heightmap from image". G6 should wrap G1 internally.
3. Then G2 (terrain layers / splatmaps) — the next-biggest visual unlock.
4. Then G3, G4, G5, G7 — additional capability per project need.

---

## 7. Notes

**Cross-domain dependencies observed:**
- `Tool_Terrain.GetInfo.cs` line 38 calls `Tool_Transform.FindGameObject(instanceId, objectPath)`. This is the standard cross-domain helper used by Move/Rotate/Scale and is stable.
- `Tool_Terrain.Create.cs` interacts with `AssetDatabase` directly. This is consistent with `Tool_Asset.Create.cs` which uses the same idiom.

**Workflows intentionally deferred / out of scope:**
- Terrain collision tuning (e.g. `Terrain.collectDetailPatches`, `Terrain.heightmapPixelError`) — useful but secondary.
- Wind zones for terrain trees — orthogonal subsystem.
- Terrain materials / shader graph integration — overlaps with the (existing) Material tooling under `asset-create`.

**Open questions for the reviewer:**
1. Is the Terrain domain a Tier 1 priority? It is not listed in the CLAUDE.md Tier 1 set (GameObject, Component, Prefab, Scene, Asset, Script, Editor, Selection). If terrain workflows are out of scope for v1.x, the gaps in Section 5 may be deliberate non-goals — please confirm before the planner proposes expansion.
2. Should new height/layer/tree tools live in this domain (`Tool_Terrain.*`) or be split (e.g. `Tool_TerrainHeight.*`, `Tool_TerrainPaint.*`)? The domain is small enough today to absorb growth, but capability adds could push it to 8-10 tools.
3. The `asset-create` tool currently does not handle `TerrainData`. Should G3 be solved by extending `asset-create` (a tiny edit to its switch statement) or by adding a sibling `terrain-create-data` tool? This is a planner question, but flagged here because the answer affects scope estimation.

**Limits of this audit:**
- I did not test runtime behavior of the existing tools — findings are static-analysis only.
- The Unity API surface for terrains is large and I have enumerated the most common gaps; less common APIs (e.g. holes via `TerrainData.SetHoles`, base map generation) may also be missing but were not exhaustively cross-referenced.
- Cross-domain grep was scoped to terrain-specific Unity API names. If a generic helper (e.g. a hypothetical `unity-call-method` tool) existed elsewhere, it could partially cover some gaps; no such tool was observed.
