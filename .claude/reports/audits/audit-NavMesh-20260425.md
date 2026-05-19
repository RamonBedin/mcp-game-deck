# Audit Report — NavMesh

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/NavMesh/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via `Glob Editor/Tools/NavMesh/Tool_NavMesh.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** balanced

**Errors encountered during audit:** None

**Files not analyzed (if any):** None

**Absence claims in this report:**
- Coverage is balanced, so absence claims are permitted. Where I assert "no tool exists" I have read every file in the domain and grepped the wider `Editor/Tools/` tree to verify.

**Cross-domain checks performed:**
- `Grep "NavMesh|NavMeshAgent|NavMeshSurface|NavMeshLink|NavMeshObstacle|NavMeshModifier" Editor/Tools` — only matches inside `Editor/Tools/NavMesh/` itself plus an unrelated mention in `Tool_Reflect.Search.cs`.
- `Grep "ClearAllNavMeshData|RemoveAllNavMeshData|navmesh-clear" Editor/Tools` — no matches.
- `Grep "SamplePosition|CalculatePath|NavMesh\.Raycast" Editor/Tools` — no matches.
- `Grep "SetStaticEditorFlags|StaticEditorFlags|NavigationStatic" Editor/Tools` — only matches are descriptive prose inside `Tool_NavMesh.Bake.cs` itself; no tool actually sets these flags.
- `Tool_Component.Add` exists and can attach components by name (so `NavMeshAgent`, `NavMeshObstacle`, etc. could in principle be added, although NavMeshSurface/Link/Modifier live in the optional `com.unity.ai.navigation` package and may not resolve via the default `UnityEngine` namespace search).

**Reviewer guidance:**
- This is one of the smallest domains in the package (2 files, 2 tools). The domain is essentially a thin wrapper around the legacy `UnityEditor.AI.NavMeshBuilder` API. Most of the value of the audit is in Section 5 (Capability Gaps) — there are large workflow gaps versus what a Unity dev would expect from a "NavMesh" tool surface. Sections 2 and 4 are deliberately short; do not interpret the brevity as low quality.
- The class-level summary on `Tool_NavMesh.Bake.cs` claims the domain "Covers NavMesh generation via NavMeshBuilder, state queries, **and area cost inspection**" — area cost inspection is read-only via `GetInfo`, but there is no tool to *set* area costs or define areas, which the summary implicitly hints at. Worth flagging during planning.
- The Bake tool advertises four agent settings as parameters but the trailing message admits "Custom agent settings are not applied via this API." This is a real correctness/UX issue and is the highest-priority item below.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `navmesh-bake` | NavMesh / Bake | `Tool_NavMesh.Bake.cs` | 4 (agentRadius=0.5f, agentHeight=2f, maxSlope=45f, stepHeight=0.4f) | no |
| `navmesh-get-info` | NavMesh / Get Info | `Tool_NavMesh.GetInfo.cs` | 0 | yes (`ReadOnlyHint = true`) |

**Internal Unity API surface used:**
- `UnityEditor.AI.NavMeshBuilder.BuildNavMesh()` (deprecated — wrapped in `#pragma warning disable CS0618`)
- `UnityEngine.AI.NavMesh.CalculateTriangulation()`
- `UnityEngine.AI.NavMesh.GetAreaCost(int)`

**Observations:**
- `navmesh-get-info` correctly sets `ReadOnlyHint = true`; `navmesh-bake` correctly does not.
- `Tool_NavMesh.Bake.cs` carries the `[McpToolType]` attribute and the class-level `<summary>`. `Tool_NavMesh.GetInfo.cs` correctly omits the duplicate summary, matching the partial-class convention in `CLAUDE.md`.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The domain has only 2 tools and they cover distinct actions (write vs read).

---

## 3. Ambiguity Findings

### A1 — Bake parameters silently ignored
**Location:** `navmesh-bake` — `Tool_NavMesh.Bake.cs`
**Issue:** All four parameters (`agentRadius`, `agentHeight`, `maxSlope`, `stepHeight`) are accepted, range-validated, and echoed back in the success message — but the underlying `NavMeshBuilder.BuildNavMesh()` call ignores them. The tool tells the caller this only at the very end of the response: "Custom agent settings are not applied via this API. Configure settings in the NavMeshSurface component." Both the method `[Description]` and each parameter `[Description]` claim the values are "used during NavMesh generation," which is misleading. An LLM caller reading the descriptions has no way to know the values are inert.
**Evidence:**
- Line 41: `[Description("Bakes the NavMesh for the active scene using NavMeshBuilder.BuildNavMesh(). Configure agent radius, height, max walkable slope, and step height before baking. ...")]`
- Line 43: `[Description("Agent cylinder radius used during NavMesh generation. Default 0.5.")] float agentRadius = 0.5f`
- Line 80: `sb.AppendLine("Note: Custom agent settings are not applied via this API. Configure settings in the NavMeshSurface component.");`
**Confidence:** high

### A2 — Vague top-level description on `navmesh-get-info`
**Location:** `navmesh-get-info` — `Tool_NavMesh.GetInfo.cs`
**Issue:** Description is 13 words, no example, no disambiguation about what "area info" means. A caller does not learn from the description that the response includes per-area cost values, only counts.
**Evidence:** Line 22: `[Description("Returns information about the baked NavMesh: triangle count, vertex count, and area info.")]`
**Confidence:** medium

### A3 — `maxSlope` upper bound is undocumented
**Location:** `navmesh-bake` param `maxSlope` — `Tool_NavMesh.Bake.cs`
**Issue:** The parameter description says "Maximum walkable slope angle in degrees. Default 45." but the validator rejects values > 60. The 60° cap is not in the description, so an LLM that suggests 75° (a value Unity's Navigation window actually allows up to 60° but some users expect higher) would only learn the rule by failing.
**Evidence:**
- Line 45: `[Description("Maximum walkable slope angle in degrees. Default 45.")]`
- Line 61-64: `if (maxSlope < 0f || maxSlope > 60f) { return ToolResponse.Error("maxSlope must be in the range 0–60 degrees."); }`
**Confidence:** high

### A4 — No disambiguation about "active scene" vs multi-scene setups
**Location:** `navmesh-bake` and `navmesh-get-info`
**Issue:** Neither description clarifies behaviour when multiple scenes are loaded. `NavMeshBuilder.BuildNavMesh()` bakes the active scene only; `NavMesh.CalculateTriangulation()` returns the global NavMesh state. A caller working with multi-scene editing has no way to know this from the descriptions.
**Evidence:** Both descriptions reference "the scene"/"the current scene" without qualification.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — No defaults to flag as wrong; defaults match Unity's own defaults
**Location:** `navmesh-bake`
**Issue:** None. The 4 defaults (radius 0.5, height 2.0, slope 45°, step 0.4) match Unity's stock NavMesh agent defaults. This is correct behaviour.
**Confidence:** high

(No other parameters in the domain.)

---

## 5. Capability Gaps

### G1 — Cannot clear/remove the baked NavMesh
**Workflow:** A developer iterating on level layout commonly bakes, edits geometry, and re-bakes. They also want to clear the existing NavMesh to start fresh, or to commit a clean scene to source control with no baked data.
**Current coverage:** `navmesh-bake` (build) and `navmesh-get-info` (inspect).
**Missing:** No tool wraps `UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshData()`. Verified via `Grep "ClearAllNavMeshData|RemoveAllNavMeshData|navmesh-clear" Editor/Tools` returned zero matches.
**Evidence:** Domain contains exactly 2 files (Bake, GetInfo); both inspected.
**Confidence:** high

### G2 — Bake parameters do not actually configure the bake
**Workflow:** Adjust the bake's agent radius/height/slope/step from a tool call to see how a NavMesh changes for a different agent profile (e.g. small enemy vs player vs large boss).
**Current coverage:** `navmesh-bake` accepts the four parameters and echoes them; the legacy `NavMeshBuilder.BuildNavMesh()` API uses whatever is configured in the legacy Navigation window or per-NavMeshSurface, not the values passed in.
**Missing:** A real binding between parameters and the bake. To honour the parameter contract, the tool would need to either (a) write to `NavMeshBuilder.navMeshSettingsObject` / use `SerializedObject` on the global NavMesh settings before `BuildNavMesh()`, or (b) move to the modern `com.unity.ai.navigation` package and operate on `NavMeshSurface` settings via `NavMeshBuilder.UpdateNavMeshData(...)` / `NavMeshBuildSettings`.
**Evidence:** `Tool_NavMesh.Bake.cs` line 71 calls `NavMeshBuilder.BuildNavMesh();` with no arguments and no preceding settings mutation; line 80 acknowledges the gap to the user post-hoc.
**Confidence:** high

### G3 — Cannot mark geometry as Navigation Static
**Workflow:** Programmatically prepare a scene for baking by flagging walkable geometry as Navigation Static (a prerequisite the tool's own description names).
**Current coverage:** None. `Tool_Component.Add` can add components by name, but Navigation Static is a `StaticEditorFlags` bit on the GameObject, not a component.
**Missing:** A tool that calls `GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic | ...)`. Verified via `Grep "SetStaticEditorFlags|StaticEditorFlags|NavigationStatic" Editor/Tools` — only matches are inside the `navmesh-bake` description and the class-level summary; no tool actually sets the flag. This gap forces the LLM to use the bake tool while also instructing the user to manually flag geometry, breaking the automation story the description promises.
**Evidence:** Cross-domain grep above; absence verified across all of `Editor/Tools/`.
**Confidence:** high

### G4 — No runtime/path query tools
**Workflow:** Validate that an enemy spawn point is reachable, sample the nearest NavMesh point for a placement, or compute a path between two points to debug AI.
**Current coverage:** None.
**Missing:** Wrappers around `NavMesh.SamplePosition`, `NavMesh.Raycast`, and `NavMesh.CalculatePath`. Verified via `Grep "SamplePosition|CalculatePath|NavMesh\.Raycast" Editor/Tools` — zero matches anywhere in the package.
**Evidence:** Cross-domain grep above.
**Confidence:** high (these are read-only queries; would naturally pair with `ReadOnlyHint = true`)

### G5 — No support for the modern `com.unity.ai.navigation` package
**Workflow:** Add a `NavMeshSurface` to a level root, configure agent type/area mask/voxel size, bake that surface, add a `NavMeshLink` for jumps, add a `NavMeshObstacle` for dynamic blockers, mark a region with `NavMeshModifier`.
**Current coverage:** None. `Tool_Component.Add` can in principle resolve types in any loaded assembly, but its description and namespace search are oriented at `UnityEngine` core types ("Rigidbody, BoxCollider, AudioSource, Light"). The NavMesh domain offers no dedicated support.
**Missing:** Either (a) NavMesh-specific component-add helpers that know about `Unity.AI.Navigation.NavMeshSurface` etc. and configure them, or (b) explicit documentation that `component-add "Unity.AI.Navigation.NavMeshSurface"` is the supported path. The current tool's note about "Configure settings in the NavMeshSurface component" assumes the package and component already exist with no help from the tool surface to set that up.
**Evidence:** `Grep` for `NavMeshAgent|NavMeshSurface|NavMeshObstacle|NavMeshLink|NavMeshModifier` outside the NavMesh domain — only the unrelated `Tool_Reflect.Search.cs` text match. No dedicated tooling.
**Confidence:** medium (this is partly addressed by the generic component-add tool; severity depends on whether Ramon wants NavMesh to be a turnkey domain or a thin Unity wrapper)

### G6 — Cannot set per-area costs or define areas
**Workflow:** Tune AI pathing by raising the cost of "swamp" or "hazard" areas, or by adding a custom area.
**Current coverage:** `navmesh-get-info` reports area costs via `NavMesh.GetAreaCost`.
**Missing:** A wrapper around `NavMesh.SetAreaCost(int, float)` and an inspector for the project's area definitions (the `NavMeshAreas.asset`). The domain class-level summary advertises "area cost inspection" — but only inspection, not mutation. An LLM that reads the class summary may infer mutation is possible.
**Evidence:** `Tool_NavMesh.GetInfo.cs` line 52 calls `GetAreaCost`; no `SetAreaCost` anywhere in the package.
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | A1 | Ambiguity | 5 | 1 | 25 | high | Bake params claim to be applied but are silently ignored — fix descriptions or wire them up |
| 2 | G1 | Capability Gap | 4 | 1 | 20 | high | Add `navmesh-clear` wrapping `NavMeshBuilder.ClearAllNavMeshData()` |
| 3 | G2 | Capability Gap | 5 | 4 | 10 | high | Make bake parameters actually configure the bake (settings mutation or modern API migration) |
| 4 | G4 | Capability Gap | 4 | 2 | 16 | high | Add read-only path/sample query tools (SamplePosition / CalculatePath / Raycast) |
| 5 | G3 | Capability Gap | 4 | 2 | 16 | high | Add a tool to set `StaticEditorFlags.NavigationStatic` on GameObjects |
| 6 | G6 | Capability Gap | 3 | 2 | 12 | high | Add `navmesh-set-area-cost` to mirror the existing `GetAreaCost` |
| 7 | A3 | Ambiguity | 3 | 1 | 15 | high | Document the 0–60° cap on `maxSlope` in its parameter description |
| 8 | A2 | Ambiguity | 2 | 1 | 10 | medium | Expand `navmesh-get-info` description to enumerate output fields |
| 9 | G5 | Capability Gap | 3 | 4 | 6 | medium | First-class wrappers for `com.unity.ai.navigation` components |
| 10 | A4 | Ambiguity | 2 | 1 | 10 | medium | Clarify multi-scene behaviour in both tool descriptions |

(Priority computed as `Impact × (6 − Effort)`. Some lower-effort ambiguity fixes outrank larger gaps and should be batched.)

---

## 7. Notes

- **Cross-domain dependency:** Several proposed gaps (G3, G5) are partially addressable via existing tools in the `Component`, `Transform`, and `GameObject` domains. The `consolidation-planner` should consider whether NavMesh-specific helpers add enough value over just documenting the generic path. My recommendation is yes for G3 (Navigation Static is a flag, not a component, so `component-add` cannot help) and "documentation might suffice" for G5.
- **Open question for the reviewer:** A1 is currently the highest-priority finding, but its fix is bimodal — either tighten the descriptions to admit the limitation (1 line of effort) OR actually wire the parameters to a working bake (4–5 of effort, possibly migrating off the deprecated API). The planner needs a directional answer from Ramon before drafting a concrete plan. The fact that `BuildNavMesh()` is wrapped in `#pragma warning disable CS0618` on line 70 is itself a signal that this domain is overdue for a modernization pass.
- **Testability note:** All of the proposed new tools (G1, G3, G4, G6) are pure wrappers around stable `UnityEngine.AI.NavMesh` / `UnityEditor.AI.NavMeshBuilder` static APIs and would be straightforward for `build-validator` to compile-check.
- **Workflows intentionally not audited:** I did not investigate the experimental `NavMeshBuildMarkup` / `NavMeshBuildSource` "build-from-sources" API surface; that is a much larger feature and probably out of scope for v1.x.
- **Limit of audit:** I did not run the tools live or test against a Unity project — the audit is static-analysis only against the source. The "parameters are ignored" finding (A1) is grounded in the source admitting it on line 80, not in runtime observation.
