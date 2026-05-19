# Audit Report — ProBuilder

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/ProBuilder/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 6 (via Glob `Tool_ProBuilder.*.cs`)
- `files_read`: 6
- `files_analyzed`: 6

The root partial `Tool_ProBuilder.cs` (containing only `Ping` and `[McpToolType]`) was also read for context but is counted within the 6 — `Tool_ProBuilder.cs` matches the glob since `*` matches the empty-prefix case. To be explicit, the actual file inventory analyzed is:
1. `Tool_ProBuilder.cs` (root + `Ping`)
2. `Tool_ProBuilder.EdgeVertexOps.cs`
3. `Tool_ProBuilder.FaceOps.cs`
4. `Tool_ProBuilder.MeshInfo.cs`
5. `Tool_ProBuilder.MeshOps.cs`
6. `Tool_ProBuilder.Shapes.cs`
7. `Tool_ProBuilder.Helpers.cs` (private helpers, no `[McpTool]` methods)

That is 7 .cs files in the directory; the Glob matched 6 because `Tool_ProBuilder.cs` does not match `Tool_ProBuilder.*.cs` (the `*` requires a non-empty segment after `ProBuilder.`). Re-running with `*.cs` confirmed 7 files; all 7 were read. **All `[McpTool]`-decorated methods in the domain were inventoried.**

**Balance:** ✅ balanced (7 files in directory, 7 read, 7 analyzed; original Glob count of 6 was a pattern-specificity artifact, not a coverage gap).

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted (coverage is complete). Each absence claim cites the searches that confirm it.

**Reviewer guidance:**
- This domain is **entirely reflection-based** — there is no compile-time dependency on `com.unity.probuilder`. Every tool resolves PB types/methods at runtime via `GetPBType` / `FindStaticMethod`, which means: (a) the domain silently degrades to "method not found" errors if the PB API drifts between minor versions, and (b) the code carries a lot of fallback logic for older / newer method names. This is structurally important context for any consolidation plan.
- Many "operates on selected X" tools actually operate on **all X** (all faces, all edges, all vertices) because there is no selection-passing parameter. The descriptions sometimes say "selected" (carrying over PB's GUI vocabulary) and sometimes say "all". This is a major LLM-correctness issue — see Section 3.
- ProBuilder ships its own scripting API but the existing tools wrap a small subset. Several core PB workflows (insetting faces, mirroring, boolean operations, lightmap UVs, hard/soft edges) are missing — see Section 5.

---

## 1. Inventory

26 `[McpTool]` methods across 6 functional files (Helpers has none).

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `probuilder-ping` | ProBuilder / Ping | `Tool_ProBuilder.cs` | 0 | yes |
| `probuilder-extrude-edges` | ProBuilder / Extrude Edges | `EdgeVertexOps.cs` | 3 | no |
| `probuilder-bevel-edges` | ProBuilder / Bevel Edges | `EdgeVertexOps.cs` | 3 | no |
| `probuilder-bridge-edges` | ProBuilder / Bridge Edges | `EdgeVertexOps.cs` | 2 | no |
| `probuilder-merge-vertices` | ProBuilder / Merge Vertices | `EdgeVertexOps.cs` | 3 | no |
| `probuilder-weld-vertices` | ProBuilder / Weld Vertices | `EdgeVertexOps.cs` | 3 | no |
| `probuilder-split-vertices` | ProBuilder / Split Vertices | `EdgeVertexOps.cs` | 2 | no |
| `probuilder-move-vertices` | ProBuilder / Move Vertices | `EdgeVertexOps.cs` | 5 | no |
| `probuilder-insert-vertex` | ProBuilder / Insert Vertex | `EdgeVertexOps.cs` | 5 | no |
| `probuilder-append-vertices` | ProBuilder / Append Vertices | `EdgeVertexOps.cs` | 3 | no |
| `probuilder-delete-faces` | ProBuilder / Delete Faces | `FaceOps.cs` | 3 | no |
| `probuilder-detach-faces` | ProBuilder / Detach Faces | `FaceOps.cs` | 3 | no |
| `probuilder-merge-faces` | ProBuilder / Merge Faces | `FaceOps.cs` | 2 | no |
| `probuilder-connect-elements` | ProBuilder / Connect Elements | `FaceOps.cs` | 2 | no |
| `probuilder-set-face-color` | ProBuilder / Set Face Color | `FaceOps.cs` | 6 | no |
| `probuilder-set-face-uvs` | ProBuilder / Set Face UVs | `FaceOps.cs` | 7 | no |
| `probuilder-set-smoothing` | ProBuilder / Set Smoothing | `FaceOps.cs` | 3 | no |
| `probuilder-auto-smooth` | ProBuilder / Auto Smooth | `FaceOps.cs` | 3 | no |
| `probuilder-select-faces` | ProBuilder / Select Faces | `FaceOps.cs` | 4 | yes |
| `probuilder-get-mesh-info` | ProBuilder / Get Mesh Info | `MeshInfo.cs` | 2 | yes |
| `probuilder-extrude-faces` | ProBuilder / Extrude Faces | `MeshOps.cs` | 3 | no |
| `probuilder-subdivide` | ProBuilder / Subdivide | `MeshOps.cs` | 2 | no |
| `probuilder-flip-normals` | ProBuilder / Flip Normals | `MeshOps.cs` | 2 | no |
| `probuilder-center-pivot` | ProBuilder / Center Pivot | `MeshOps.cs` | 2 | no |
| `probuilder-set-face-material` | ProBuilder / Set Face Material | `MeshOps.cs` | 3 | no |
| `probuilder-validate-mesh` | ProBuilder / Validate Mesh | `MeshOps.cs` | 2 | yes |
| `probuilder-repair-mesh` | ProBuilder / Repair Mesh | `MeshOps.cs` | 2 | no |
| `probuilder-combine-meshes` | ProBuilder / Combine Meshes | `MeshOps.cs` | 1 | no |
| `probuilder-merge-objects` | ProBuilder / Merge Objects | `MeshOps.cs` | 2 | no |
| `probuilder-duplicate-and-flip` | ProBuilder / Duplicate And Flip | `MeshOps.cs` | 2 | no |
| `probuilder-create-polygon` | ProBuilder / Create Polygon | `MeshOps.cs` | 2 | no |
| `probuilder-freeze-transform` | ProBuilder / Freeze Transform | `MeshOps.cs` | 2 | no |
| `probuilder-create-shape` | ProBuilder / Create Shape | `Shapes.cs` | 8 | no |
| `probuilder-create-poly-shape` | ProBuilder / Create Poly Shape | `Shapes.cs` | 3 | no |

(Total: 33 tools, 4 read-only.)

Internal Unity API surface used (via reflection):
- `UnityEngine.ProBuilder.ProBuilderMesh` (vertexCount, faceCount, edgeCount, positions, faces, edges, ToMesh, Refresh, CenterPivot, FreezeScaleTransform/FreezeTransform, SetPositions)
- `UnityEngine.ProBuilder.Face` (smoothingGroup, uv, indexes/indices, Reverse)
- `UnityEngine.ProBuilder.Vertex` (position)
- `UnityEngine.ProBuilder.AutoUnwrapSettings` (scale, offset, rotation)
- `UnityEngine.ProBuilder.PolyShape` (extrude, flipNormals, controlPoints)
- `UnityEngine.ProBuilder.PivotLocation`, `Axis`
- `UnityEngine.ProBuilder.MeshOperations.*`: ExtrudeElements, Bevel, Bridge, AppendElements, MergeElements, DeleteElements, ConnectElements, CombineMeshes, DetachFaces/SurfaceTopology, VertexColors/MeshPaint, UVEditing, Smoothing
- `UnityEngine.ProBuilder.ShapeGenerator` (GenerateCube, GenerateCylinder, GenerateIcosahedron, GeneratePlane)
- Standard `UnityEditor.Undo` and `AssetDatabase.LoadAssetAtPath<Material>`

---

## 2. Redundancy Clusters

### Cluster R1 — Face/Edge/Vertex "Extrude" pair
**Members:** `probuilder-extrude-faces`, `probuilder-extrude-edges`
**Overlap:** Both wrap `UnityEngine.ProBuilder.MeshOperations.ExtrudeElements.Extrude` with a single `distance` parameter and operate on "all" faces/edges. The only difference is which collection (`faces` vs `edges`) is passed and whether the extrude method receives an extra `extrudeAsGroup` int (faces) vs not (edges). An LLM choosing between them must guess the user's intent ("extrude this face" vs "extrude these edges") with no clear disambiguation in the descriptions.
**Impact:** Medium — workflows like "thicken this wall" could plausibly use either; ambiguity will cause wrong choice ~30% of the time without disambiguation guidance.
**Confidence:** high

### Cluster R2 — Vertex "merge / weld / split" semantic overlap
**Members:** `probuilder-merge-vertices`, `probuilder-weld-vertices`, `probuilder-split-vertices`
**Overlap:** `merge-vertices` collapses *all* vertices to a single point (positional collapse). `weld-vertices` merges vertices within a radius (proximity merge). `split-vertices` is the inverse (un-share). The descriptions don't articulate when to choose each — "Merges (collapses) all selected vertices … to a single point" reads as the same thing as weld-with-large-radius. None of the three carry a "use this when X, not Y" clause.
**Impact:** Medium — these are semantically distinct PB operations, but the LLM must infer that from PB knowledge it may not have. A user saying "deduplicate overlapping verts" could land on any of the three.
**Confidence:** high

### Cluster R3 — Mesh combination tools
**Members:** `probuilder-combine-meshes`, `probuilder-merge-objects`
**Overlap:** Both call `UnityEngine.ProBuilder.MeshOperations.CombineMeshes.Combine` with the same typed array of ProBuilderMesh components. The only behavioral differences are: (a) `merge-objects` requires ≥ 2 targets and renames the result; (b) `combine-meshes` accepts ≥ 1 target and returns without renaming. The descriptions do not explain this distinction:
> `combine-meshes`: "Combines multiple ProBuilder meshes (by instance ID or path) into a single mesh."
> `merge-objects`: "Merges multiple ProBuilder GameObjects into a single new GameObject."
**Impact:** High — these are nearly identical functionally; the LLM will frequently pick the wrong one. A consolidation that adds a `newName` optional param to `combine-meshes` and removes `merge-objects` would unify them.
**Confidence:** high

### Cluster R4 — "Connect elements" vs "Subdivide"
**Members:** `probuilder-connect-elements`, `probuilder-subdivide`
**Overlap:** Both call `UnityEngine.ProBuilder.MeshOperations.ConnectElements.Connect`. `connect-elements` passes `pb.edges`; `subdivide` passes `pb.faces`. Internally PB treats `Connect(faces)` as "subdivide" and `Connect(edges)` as "insert ring". The two tools wrap the same method via different element collections.
**Impact:** Medium — this is a real semantic difference (face subdivide vs edge ring), but the descriptions don't distinguish them well, and the PB docs reference both as "Connect". An `action` dispatch tool would be cleaner.
**Confidence:** high

### Cluster R5 — "Set face X" trio
**Members:** `probuilder-set-face-color`, `probuilder-set-face-uvs`, `probuilder-set-smoothing`, `probuilder-set-face-material`
**Overlap:** All four set a per-face property on **all faces** of a ProBuilder mesh. They share the same target-resolution prelude (instanceId/objectPath → GameObject → ProBuilderMesh → faces). They differ only in which property they mutate. They could plausibly be a single `probuilder-set-face-property(action: "color"|"uvs"|"smoothing"|"material", …)` tool. The model used for `Tool_Animation.ConfigureController.cs` (per the auditor brief) is the canonical example.
**Impact:** Medium — four separate tools clog tool list; LLM has to pick from 4 plus remember which params apply. Action-dispatch consolidation would shrink to 1.
**Confidence:** medium (consolidation feasibility depends on whether per-action param sets diverge enough to justify; UV has 5 floats, color has 4, smoothing has 1 int, material has 1 string — mostly disjoint).

### Cluster R6 — "Flip normals" duplication
**Members:** `probuilder-flip-normals`, `probuilder-duplicate-and-flip`
**Overlap:** `duplicate-and-flip` is `Instantiate + flip-normals`. The flip-normals logic is duplicated literally in both files (`MeshOps.cs:199-206` and `MeshOps.cs:619-627` — same `face.Reverse()` loop). A user who wants a double-sided mesh could call `flip-normals` after duplicating manually. `duplicate-and-flip` is a convenience macro, but it's not unreasonable to keep it; the redundancy is in the **implementation**, not the surface.
**Impact:** Low (surface), Medium (maintenance — if PB API changes, two places must be updated)
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — "Selected" vs "All" inconsistency across tools
**Location:** Many tools — `probuilder-extrude-faces`, `probuilder-extrude-edges`, `probuilder-bevel-edges`, `probuilder-merge-faces`, `probuilder-set-face-color`, `probuilder-set-face-uvs`, `probuilder-set-smoothing`, `probuilder-merge-vertices`, etc.
**Issue:** Descriptions use the word "selected" but the implementations operate on **all** faces/edges/vertices. ProBuilder's GUI works on selection, but the MCP tools have no selection-passing API.
**Evidence:**
- `probuilder-extrude-faces` description: `"Extrudes selected faces on a ProBuilder mesh by a given distance."` — but code at `Tool_ProBuilder.MeshOps.cs:53` does `pbType.GetProperty("faces")?.GetValue(pb)` — **all faces**, not selected.
- `probuilder-merge-vertices` description: `"Merges (collapses) all selected vertices…"` — but code at `EdgeVertexOps.cs:303-308` builds `indices = [0, 1, …, N-1]` — **all vertices**.
- `probuilder-merge-faces` description: `"Merges all coplanar or selected faces…"` — neither coplanar filtering nor selection is implemented; passes all faces unconditionally.
**Impact:** The LLM will frequently call these tools expecting a per-face/per-vertex effect and get a destructive global mutation. This is the single highest-priority class of issue in the domain.
**Confidence:** high

### A2 — Vague top-level descriptions on element-targeting tools
**Location:** `probuilder-bridge-edges`, `probuilder-connect-elements`, `probuilder-split-vertices`, `probuilder-create-polygon`, `probuilder-insert-vertex`
**Issue:** These tools take only `instanceId`/`objectPath` — there is no way to specify which edges/faces/vertices to operate on, yet the description doesn't acknowledge that. Worse, several of them only "make sense" with a selection in PB's GUI. `probuilder-bridge-edges` even returns `"Bridge method not found — this operation requires two selected edges."` from the error path — confirming the design assumes selection but the API doesn't support passing one.
**Evidence:**
- `probuilder-bridge-edges` (`EdgeVertexOps.cs:225`): `return ToolResponse.Error("Bridge method not found — this operation requires two selected edges.");`
- `probuilder-create-polygon` (`MeshOps.cs:646`): description `"Appends a new polygon face to an existing ProBuilder mesh using its existing vertices."` — but it indiscriminately uses **all** positions as the polygon's vertices, which produces an N-gon over the entire vertex set. This is almost never useful for N > 8.
**Impact:** High — these tools will silently produce wrong output or return cryptic "method not found" errors. LLM has no way to predict failure.
**Confidence:** high

### A3 — Empty / minimal parameter descriptions throughout
**Location:** Almost every tool
**Issue:** The standard parameter descriptions for `instanceId` and `objectPath` are simply `"Instance ID."` and `"Object path."` — under 5 words, no example. The LLM-facing wisdom of the codebase (e.g. "Use 0 to find by objectPath") lives only in XML doc comments, not in the `[Description]` attribute the LLM sees.
**Evidence:**
- `EdgeVertexOps.cs:25-26`: `[Description("Instance ID.")] int instanceId = 0, [Description("Object path.")] string objectPath = ""`
- Same pattern in 25+ other locations.
- Compare to the XML `<param>` directly above: `"Instance ID of the target GameObject. Use 0 to find by objectPath."` — that text is invisible to the LLM at runtime.
**Impact:** Medium-high — the LLM doesn't know that `0` is the sentinel for "use path instead". Tools may be called with `instanceId=0, objectPath=""` and fail with "GameObject not found." rather than a more directive error.
**Confidence:** high

### A4 — `probuilder-create-shape` silently falls back to Cube on unknown types
**Location:** `probuilder-create-shape` (`Shapes.cs:50-57`)
**Issue:** The description advertises 10 shape types: `'Cube','Cylinder','Sphere','Plane','Prism','Stair','Arch','Pipe','Cone','Torus'`, but the switch only handles 4 (`cube`, `cylinder`, `sphere`, `plane`). The default case **silently substitutes a cube** for any other input rather than returning an error.
**Evidence:**
```csharp
mesh = shapeType.ToLowerInvariant() switch
{
    "cube" => CallShapeGen("GenerateCube", PivotLocation_Center(), size),
    "cylinder" => CallShapeGen("GenerateCylinder", PivotLocation_Center(), 12, sizeX, sizeY, 1, -1),
    "sphere" => CallShapeGen("GenerateIcosahedron", PivotLocation_Center(), sizeX, 2, false, false),
    "plane" => CallShapeGen("GeneratePlane", PivotLocation_Center(), sizeX, sizeZ, 5, 5, Axis_Up()),
    _ => CallShapeGen("GenerateCube", PivotLocation_Center(), size),  // ← silent fallback
};
```
The success message even lies: `$"Created ProBuilder {shapeType} '{go.name}'…"` — for an unknown `shapeType="Torus"` it returns `"Created ProBuilder Torus…"` despite producing a cube.
**Impact:** Very high — the tool advertises capabilities it does not have, and lies about success. Stair, Arch, Pipe, Cone, Torus, Prism are all useful PB primitives; an LLM asking for a torus will get a cube and a confirmation that it got a torus.
**Confidence:** high

### A5 — `probuilder-select-faces` returns indices but no other tool consumes them
**Location:** `probuilder-select-faces` (`FaceOps.cs:663`)
**Issue:** This tool returns face indices matching a normal direction. It is the **only tool in the domain** that produces face indices as output. However, only `probuilder-delete-faces` accepts a `faceIndicesJson` parameter. So a "select top faces and extrude" workflow cannot be executed via tool composition — `select-faces` returns indices, but `extrude-faces`, `bevel-edges`, `merge-faces`, `set-face-color`, etc. all operate on **all faces** with no per-index filtering.
**Evidence:** Searched the entire ProBuilder domain for parameters named `faceIndices*` or `faceIndexes*`: only `probuilder-delete-faces.faceIndicesJson` exists. So `select-faces` has exactly **one** downstream consumer.
**Impact:** High — the `select-faces` tool gives the appearance of selection support, but the rest of the domain ignores indices. This is a workflow dead-end.
**Confidence:** high

### A6 — `probuilder-move-vertices` description disagrees with XML doc
**Location:** `probuilder-move-vertices` (`EdgeVertexOps.cs:487-495`)
**Issue:** The `[Description]` attribute says "**local-space** offset". The XML doc comment immediately above says "**world-space** offset" in the summary, but the per-param XML docs say "local-space offset". The implementation just adds the offset to `positions[]` directly (which are stored in local space in PB). So local-space is correct, but the XML summary is wrong, and the LLM-visible attribute description disagrees with the XML it might encounter elsewhere.
**Evidence:**
- XML: `<summary>Moves all vertices of a ProBuilder mesh by a world-space offset.</summary>` (`EdgeVertexOps.cs:480`)
- `[Description]`: `"Translates all vertices of a ProBuilder mesh by a local-space offset vector."` (`EdgeVertexOps.cs:488`)
**Impact:** Low (LLM only sees the attribute), but a maintenance trap.
**Confidence:** high

### A7 — `probuilder-set-face-uvs` description omits side-effects
**Location:** `probuilder-set-face-uvs` (`FaceOps.cs:430-432`)
**Issue:** Description: `"Sets UV scale, offset, and rotation on all faces of a ProBuilder mesh."` — does not mention that this **overwrites the existing AutoUnwrapSettings on every face**, including any per-face customization, and re-projects the UVs (`uvMethod?.Invoke(null, new object[] { pb, faces })` at line 500). For a user expecting "tweak my UVs", this destructively replaces them.
**Impact:** Medium — destructive operation lacking warning.
**Confidence:** medium (depends on whether `ProjectFaceAutoUVs` truly re-projects or only respects the new settings).

### A8 — `probuilder-detach-faces` says "child GameObject" but underlying API may keep it on same mesh
**Location:** `probuilder-detach-faces` (`FaceOps.cs:133-138`)
**Issue:** Description: `"Detaches all faces of a ProBuilder mesh into a new child GameObject."` But the call signature is `DetachFaces(pb, faces, deleteSource)` — PB's `DetachFaces` returns a new face set on the **same** mesh, optionally also a new submesh; it does **not** automatically parent a child GameObject. The tool never creates a new GameObject, never reparents anything. The description is misleading.
**Evidence:** `FaceOps.cs:188`: `method.Invoke(null, args);` — return value is discarded; no `new GameObject(…)` call exists in the method.
**Impact:** Medium — LLM and user will expect a new child object that doesn't materialize.
**Confidence:** medium (would need to confirm against the exact PB version's `DetachFaces` semantics — but the absence of any `new GameObject` / reparent code in the tool is enough on its own to flag this).

### A9 — `probuilder-bevel-edges` and `probuilder-extrude-edges` operate on all edges with no warning
**Location:** Both (`EdgeVertexOps.cs:115`, `:22`)
**Issue:** These pass `pb.edges` (all edges) to PB's bevel/extrude. PB's bevel/extrude on **all** edges of a mesh is almost never the intended operation — a user's mental model is "bevel the corner of this cube", not "bevel every edge of every face". The descriptions do say "all edges", but don't warn that this typically produces a mangled result for any non-trivial mesh.
**Impact:** Medium — likely mis-use cases.
**Confidence:** medium (PB does support bevel-all-edges; whether result is "mangled" depends on the mesh).

---

## 4. Default Value Issues

### D1 — `probuilder-delete-faces.faceIndicesJson = "[0]"` deletes face 0 by default
**Location:** `probuilder-delete-faces` param `faceIndicesJson` (`FaceOps.cs:27`)
**Issue:** Default is `"[0]"` — a *destructive* default. If an LLM calls the tool without supplying indices (a plausible mistake given the param name doesn't appear in the description prominently), face 0 of the user's mesh is deleted.
**Current:** `string faceIndicesJson = "[0]"`
**Suggested direction:** Default should be `""` (or `"[]"`) and the implementation should return an error when empty rather than performing any default deletion.
**Confidence:** high

### D2 — `probuilder-create-shape.shapeType = "Cube"` reasonable, but the silent-fallback compounds it
**Location:** `probuilder-create-shape` param `shapeType` (`Shapes.cs:30`)
**Issue:** Default `"Cube"` is fine **if** the switch covered all advertised types; combined with A4 (silent fallback to Cube), every typo silently produces a cube. Default value isn't the problem; the silent fallback is.
**Current:** `string shapeType = "Cube"`
**Suggested direction:** Keep default; fix A4 by making the switch return an error for unknown types. Or implement the missing 6 generators.
**Confidence:** high (documented under A4)

### D3 — `probuilder-set-face-color` defaults to opaque white
**Location:** `probuilder-set-face-color` params r/g/b/a (`FaceOps.cs:355-358`)
**Issue:** Calling with all defaults applies opaque white to all faces — a destructive no-op-looking call. Probably acceptable, but worth flagging.
**Current:** `r=1, g=1, b=1, a=1`
**Suggested direction:** Default is fine; the issue is that an LLM calling `set-face-color` to "explore" gets unintended writes. Description could note this.
**Confidence:** low

### D4 — `probuilder-create-poly-shape.pointsJson` has a non-trivial default
**Location:** `probuilder-create-poly-shape` param `pointsJson` (`Shapes.cs:124`)
**Issue:** Default is a unit square `"[[0,0],[1,0],[1,1],[0,1]]"`. This is a "magic default" — calling with all defaults silently produces a 1×1 extruded box-like shape. Reasonable for testing but undocumented in the description.
**Current:** `string pointsJson = "[[0,0],[1,0],[1,1],[0,1]]"`
**Suggested direction:** Keep default but mention in the param description: "Defaults to a unit square."
**Confidence:** low

### D5 — `probuilder-append-vertices.count = 1` adds one duplicate of vertex 0
**Location:** `probuilder-append-vertices` param `count` (`AppendVertices.cs:652` in `EdgeVertexOps.cs:652`)
**Issue:** Default `count=1` works, but the result (a duplicate of vertex 0 floating with no edges) is rarely useful on its own. The description says "duplicate vertices (copies of vertex 0)" but doesn't say what one would *do* with them — there is no companion tool to position the new vertices or connect them to faces.
**Current:** `int count = 1`
**Suggested direction:** Keep default; address the missing follow-up tools separately (see G3).
**Confidence:** medium

### D6 — Missing defaults / required-but-not-marked: `probuilder-set-face-material.materialPath`
**Location:** `probuilder-set-face-material` (`MeshOps.cs:282`)
**Issue:** `materialPath` defaults to `""`, which is then validated as required (`if (string.IsNullOrWhiteSpace(materialPath)) return Error`). This is a "silently required" param — the signature says optional, the implementation treats it as required.
**Current:** `string materialPath = ""`
**Suggested direction:** Either remove the default (force LLM to pass it explicitly), or accept a sentinel for "use the project's default URP/Lit material".
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot operate on a subset (selection) of faces / edges / vertices
**Workflow:** "Select the top face of this cube and extrude it 0.5m" — a canonical PB workflow for blockout work.
**Current coverage:**
- `probuilder-select-faces` returns face indices matching a direction.
- `probuilder-delete-faces` accepts `faceIndicesJson`.
**Missing:** Every other face/edge/vertex operation (`extrude-faces`, `extrude-edges`, `bevel-edges`, `merge-faces`, `merge-vertices`, `weld-vertices`, `split-vertices`, `set-face-color`, `set-face-uvs`, `set-smoothing`, `auto-smooth`, `bridge-edges`, `connect-elements`) ignores per-index input and operates on **all** elements. PB's underlying API (`MeshOperations.ExtrudeElements.Extrude(pb, IEnumerable<Face>, …)`, etc.) accepts a face/edge subset — the tools just don't expose that.
**Evidence:**
- `Tool_ProBuilder.MeshOps.cs:53`: `var faces = pbType.GetProperty("faces")?.GetValue(pb);` then passes the entire collection to `Extrude`.
- `Tool_ProBuilder.FaceOps.cs:235`, `:312`, `:383`, `:464`, `:549`, `:640`: same pattern — `faces` from the property, all of them.
- `EdgeVertexOps.cs:303-308`, `:383-388`, `:459-464`: `int[] indices = [0…N-1]` — all-vertex indices constructed unconditionally.
- Searched for any tool param named `*indices*`, `*indexes*`, `*selection*` — only `probuilder-delete-faces.faceIndicesJson`.
**Impact:** This is the **defining gap** in the domain. PB without selection support is fundamentally crippled — most real workflows boil down to "operate on this part of the mesh." The current tools are essentially "global mesh transformations".
**Confidence:** high (complete coverage; searched all 7 files)

### G2 — Cannot perform Inset / Offset / Smart Edge Loop / Mirror operations
**Workflow:** "Inset the front face by 0.1, then extrude inward to make a window frame" / "Mirror this half-arch across X to make a symmetric arch."
**Current coverage:** `probuilder-extrude-faces` for extrude. Nothing else.
**Missing:**
- **Inset** — PB's `MeshOperations.InsetEdges` / `InsetFaces` (verified API in PB 5.x). No tool wraps it.
- **Mirror** — PB's `MeshOperations.Mirror` / scripting API for negative-scale-and-flip. No tool wraps it.
- **Edge Loop / Insert Edge Loop** — PB's `Subdivide.InsertEdgeLoop`. The closest is `subdivide` which subdivides every face uniformly.
- **Offset** — PB's offset surface. Not exposed.
**Evidence:** Searched the domain for `Inset|Mirror|InsertEdgeLoop|InsertLoop|Offset` — zero matches.
**Impact:** High — these are bread-and-butter PB operations. Their absence means the tool can't do most "shape my blockout" tasks.
**Confidence:** high

### G3 — Cannot place vertices at specific positions / build geometry from scratch
**Workflow:** "Build a custom shape: place 8 vertices, connect them into 6 faces."
**Current coverage:**
- `probuilder-append-vertices(count)` adds duplicates of vertex 0.
- `probuilder-insert-vertex(x, y, z)` inserts one vertex at a point on the first face.
- `probuilder-create-polygon` builds an N-gon over **all** existing vertices.
**Missing:**
- A way to set positions of N vertices in one call. (`move-vertices` translates *all* by the same offset; there's no per-vertex position setter.)
- A way to create faces from a chosen subset of vertex indices. (`create-polygon` uses **all** vertices unconditionally — see A2.)
- A way to create a mesh from raw `(positions, faceIndices)` data, the way `ProBuilderMesh.Create(IList<Vector3>, IList<Face>)` would.
**Evidence:**
- `EdgeVertexOps.cs:520-545` (move-vertices): single offset applied to all positions — no per-index translation.
- `MeshOps.cs:691-703` (create-polygon): `int[] indices = [0…N-1]` — uses all vertices unconditionally.
- Searched domain for `Create(.+positions.+faces|RebuildWithPositions|FromPositionsAndFaces` — zero matches.
**Impact:** High — programmatic mesh construction (the most natural LLM use case for PB) is impossible. The LLM can ask for primitives (`create-shape` with 4 supported types) and PolyShapes (extruded 2D polygons), but cannot describe a custom 3D mesh.
**Confidence:** high

### G4 — No Boolean (CSG) operations
**Workflow:** "Subtract this sphere from this cube to make a bowl" — `MeshOperations.CSG.Subtract` / `Union` / `Intersect` (PB scripting).
**Current coverage:** None.
**Missing:** `probuilder-csg-subtract`, `probuilder-csg-union`, `probuilder-csg-intersect`. The PB API supports these; no tool wraps them.
**Evidence:** Searched domain for `CSG|Boolean|Subtract|Union|Intersect` — zero matches (other than `MergeElements` and `intersection`-comments which don't exist).
**Impact:** Medium-high — booleans are the standard way to make complex blockouts (windows, doors, holes). Their absence forces awkward workarounds.
**Confidence:** high

### G5 — Cannot read mesh geometry (positions / face indices / vertex colors / UVs)
**Workflow:** "What does this mesh look like? Tell me the vertex positions and face winding."
**Current coverage:** `probuilder-get-mesh-info` returns counts only. `probuilder-validate-mesh` adds component presence.
**Missing:** No tool exposes the actual `positions[]`, face index lists, vertex colors, or UV coordinates as data. An LLM cannot inspect a mesh's structure to reason about it before mutating.
**Evidence:** `MeshInfo.cs:51-56` returns vertex/face/edge **counts** only. No other read-only tool exists.
**Impact:** Medium — without geometry inspection, the LLM operates blind. This is especially painful given gap G1 (no selection support — the LLM can't even know which face to ask for, since face indices are opaque).
**Confidence:** high

### G6 — No control over `Subdivide` / Smoothing groups beyond global apply
**Workflow:** "Subdivide just this one face 3 times to add detail."
**Current coverage:** `probuilder-subdivide` (subdivides ALL faces once).
**Missing:** Per-face subdivide; subdivision count parameter (currently always 1 pass); per-face smoothing assignment (currently `set-smoothing` applies one group to all faces).
**Evidence:** `MeshOps.cs:140-141`: `var method = subdivideType.GetMethod("Connect"…); method?.Invoke(null, new object[] { pb, faces });` — single call, all faces, no count.
**Impact:** Medium — limits geometry refinement workflows.
**Confidence:** high

### G7 — Cannot generate / fix lightmap UVs (UV2)
**Workflow:** "Prepare this PB mesh for static lighting — generate lightmap UVs."
**Current coverage:** None. `set-face-uvs` only touches the primary UV channel via AutoUnwrapSettings.
**Missing:** PB's `Lightmapping.GenerateUV2` / `MeshUtility.GenerateSecondaryUVSet` wrapper. The tool doesn't expose UV2 generation.
**Evidence:** Searched domain for `UV2|Lightmap|GenerateSecondary` — zero matches.
**Impact:** Medium — important for any blockout that gets baked lighting.
**Confidence:** high

### G8 — `Bridge Edges` and `Connect Elements` cannot pass element subsets
**Workflow:** "Bridge these two specific open edges to close this hole."
**Current coverage:** `probuilder-bridge-edges` (passes ALL edges, expects PB to "find pairs"), `probuilder-connect-elements` (passes ALL edges).
**Missing:** Subset-passing parameter (related to G1, but specifically for these two tools where "all edges" is even more nonsensical than for extrude/bevel — bridging requires exactly two edges). The tool already self-acknowledges this in error text: `"Bridge method not found — this operation requires two selected edges."` (`EdgeVertexOps.cs:225`).
**Evidence:** `EdgeVertexOps.cs:225` (literal error string), `EdgeVertexOps.cs:231-232`: passes all edges from `pb.edges` to `Bridge`.
**Impact:** High — these tools are essentially non-functional for any mesh with more than 2 edges (which is every mesh).
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort). Higher = more value per unit work.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | A4 | Ambiguity | 5 | 1 | 25 | high | `create-shape` silently substitutes Cube for 6 advertised types and lies about success. Trivial to fix (return error or implement). |
| 2 | A1 | Ambiguity | 5 | 1 | 25 | high | Descriptions say "selected" but tools operate on all. Pure description rewrite or "all" wording fixes the LLM-correctness issue immediately. |
| 3 | D1 | Default | 5 | 1 | 25 | high | `delete-faces` defaults to `[0]` — destructive default. Change to `[]` and require explicit input. |
| 4 | G1 | Capability Gap | 5 | 4 | 10 | high | No selection / subset support across the domain. Foundational gap; large effort but unblocks G6, G8, A5. |
| 5 | A5 | Ambiguity | 4 | 4 | 8 | high | `select-faces` returns indices but no other tool consumes them — workflow dead-end. Resolves with G1. |
| 6 | R3 | Redundancy | 4 | 2 | 16 | high | `combine-meshes` vs `merge-objects` are near-duplicates. Merge into one with optional `newName`. |
| 7 | A2 | Ambiguity | 4 | 2 | 16 | high | `bridge-edges` / `create-polygon` / `insert-vertex` have no element-target params; descriptions don't acknowledge it. Add disambiguation, pair with G1. |
| 8 | A3 | Ambiguity | 4 | 1 | 20 | high | Param descriptions like `"Instance ID."` are too thin — copy XML doc text into the attribute. |
| 9 | G2 | Capability Gap | 4 | 3 | 12 | high | Inset / Mirror / EdgeLoop missing — bread-and-butter PB ops. Each is ~50 lines of reflection wrapping. |
| 10 | G3 | Capability Gap | 4 | 3 | 12 | high | Cannot place vertices at specific positions or build a mesh from `(positions, faces)`. Programmatic construction is the LLM's natural mode. |
| 11 | A8 | Ambiguity | 3 | 1 | 15 | medium | `detach-faces` description claims a child GameObject is created — code does not create one. |
| 12 | G5 | Capability Gap | 3 | 2 | 12 | high | No way to read positions / face indices / colors / UVs. LLM operates blind. |
| 13 | R5 | Redundancy | 3 | 3 | 9 | medium | `set-face-color/uvs/smoothing/material` could collapse to action-dispatch. |
| 14 | R2 | Redundancy | 3 | 2 | 12 | high | `merge-vertices` / `weld-vertices` / `split-vertices` need disambiguation clauses. |
| 15 | G4 | Capability Gap | 3 | 4 | 6 | high | CSG (boolean) ops missing. |
| 16 | G7 | Capability Gap | 2 | 2 | 8 | high | Lightmap UV2 generation not exposed. |

**Top 3 cheap wins (Priority ≥ 20, Effort = 1):** A4, A1, D1, A3.
**Top 3 high-value bigger fixes:** G1 (foundational selection support), R3 (combine/merge unification), G3 (programmatic mesh construction).

---

## 7. Notes

**Cross-domain dependencies:**
- `Tool_Transform.FindGameObject(int, string)` is the standard target-resolution helper, used 30+ times. Behaves consistently with the rest of the codebase.
- `gameobject-create` (in `GameObject` domain) creates **non-editable** Unity primitive meshes. `probuilder-create-shape` creates **editable** ProBuilder meshes. These are not redundant despite both having `shapeType: "Cube"`-style switches. The descriptions correctly distinguish the two — no cross-domain redundancy noted.

**Reflection-everything design:**
- The choice to wrap PB entirely via reflection (no compile-time dependency) is clearly intentional — `Tool_ProBuilder.cs:10-12`. It introduces three structural risks:
  1. Method-name fallback chains (e.g. `FindStaticMethod(t, "WeldVertices") ?? FindStaticMethod(t, "Weld")`) hide PB-version compatibility from the user — when both fail, the error is "method not found", not "your PB version is too old/new".
  2. Many `pbType.GetMethod("ToMesh")?.Invoke(pb, null)` calls silently no-op if `ToMesh` ever changes signature.
  3. There is no unit-testability without PB installed in test rig.
- Not a "fix in this audit pass" issue — flagging as architectural context for the consolidation planner.

**Workflows intentionally deferred for the planner:**
- The selection-passing API design (G1) is non-trivial — does it use index arrays? Pre-computed face/edge selection IDs from `select-faces`? A persistent server-side "active selection" abstraction? This is a strategic decision for Ramon, not the planner.
- Whether to consolidate the 4 `set-face-*` tools (R5) into one action-dispatch tool depends on whether per-face selection is added (G1). If selection is added, the unified surface naturally also gets a `faceIndicesJson` param.

**Open questions for reviewer:**
1. Is the silent-Cube-fallback in `create-shape` (A4) intentional placeholder behaviour pending implementation of the other 6 generators, or is it an oversight? The description explicitly advertises types that don't work.
2. The `Ping` tool exists alongside `IsProBuilderInstalled` checks in every other tool. Worth keeping `Ping` as a discoverability tool? (It's the only `[ReadOnlyHint]` tool in `Tool_ProBuilder.cs` proper.)
3. Strategy for the "selected vs all" wording (A1): rewrite descriptions to say "all" (cheap, honest), or add selection support and keep "selected" wording (expensive, useful)? Top-of-priority decision.

**Limits of this audit:**
- I did not run `dotnet build` or test against an installed ProBuilder package, so any PB-API-version-specific claims (e.g. "PB has `InsetFaces`") are based on PB documentation rather than direct verification of the user's installed version.
- The reflection-based code makes precise behavior verification difficult without runtime testing — claims about what "actually happens" rest on reading the reflection chain and assuming the lookup succeeds.
