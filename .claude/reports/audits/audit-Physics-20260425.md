# Audit Report — Physics

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Physics/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 16 (via Glob `Editor/Tools/Physics/Tool_Physics.*.cs`)
- `files_read`: 16
- `files_analyzed`: 16

**Balance:** ✅ balanced — all 16 files read and incorporated.

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted (accounting balanced). All "no tool exists for X" claims were verified by reading every Physics-domain file plus targeted grep over `Editor/Tools/` for adjacent surfaces (`Rigidbody2D`, `Physics2D`, `CapsuleCast`, `OverlapCapsule`, `CheckSphere`, `CheckBox`, `ComputePenetration`, `ClosestPoint`, `JointBreakEvent`).

**Cross-domain context consulted:**
- `Editor/Tools/Component/` — confirmed `component-add` exists for adding generic components, but the Physics domain has its own `physics-configure-rigidbody` and `physics-add-joint` shortcuts.
- Searched the entire `Editor/Tools/` tree for `Rigidbody2D`/`Physics2D` — **zero matches**. No 2D physics surface exists anywhere in the package.

**Reviewer guidance:**
- The Physics domain is reasonably self-consistent and has good helper centralization (`AppendHitInfo`). The biggest issues are (a) total absence of 2D physics support, (b) cast/overlap fragmentation that mirrors a well-known Unity API ergonomic problem, and (c) `physics-ping` overlapping `physics-get-settings`.
- Several tools share the same parameter shapes (origin XYZ + direction XYZ + maxDistance + layerMask) which the LLM has to fill 8+ floats for. Worth flagging for the consolidation-planner — may benefit from `Vector3` parameters as in `physics-set-settings` rather than 6 separate floats.
- One subtle correctness issue: `Tool_Physics.SetSettings.cs` does **not** set `defaultMaxAngularSpeed` even though `Tool_Physics.GetSettings.cs` reports it. This is a real symmetry bug worth noting.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `physics-apply-force` | Physics / Apply Force | `Tool_Physics.ApplyForce.cs` | 5 | no |
| `physics-get-collision-matrix` | Physics / Get Collision Matrix | `Tool_Physics.CollisionMatrix.cs` | 0 | no (should be yes) |
| `physics-set-collision-matrix` | Physics / Set Collision Matrix | `Tool_Physics.CollisionMatrix.cs` | 3 | no |
| `physics-get-settings` | Physics / Get Settings | `Tool_Physics.GetSettings.cs` | 0 | no (should be yes) |
| `physics-linecast` | Physics / Linecast | `Tool_Physics.Linecast.cs` | 7 | no (should be yes) |
| `physics-overlap-sphere` | Physics / Overlap Sphere | `Tool_Physics.Overlap.cs` | 5 | no (should be yes) |
| `physics-overlap-box` | Physics / Overlap Box | `Tool_Physics.Overlap.cs` | 7 | no (should be yes) |
| `physics-create-material` | Physics / Create Physics Material | `Tool_Physics.PhysicsMaterial.cs` | 7 | no |
| `physics-assign-material` | Physics / Assign Physics Material | `Tool_Physics.PhysicsMaterial.cs` | 2 | no |
| `physics-ping` | Physics / Ping | `Tool_Physics.Ping.cs` | 0 | **yes** |
| `physics-raycast` | Physics / Raycast | `Tool_Physics.Raycast.cs` | 8 | no (should be yes) |
| `physics-set-settings` | Physics / Set Settings | `Tool_Physics.SetSettings.cs` | 6 | no |
| `physics-shapecast` | Physics / Shape Cast | `Tool_Physics.ShapeCast.cs` | 10 | no (should be yes) |
| `physics-simulate-step` | Physics / Simulate Step | `Tool_Physics.SimulateStep.cs` | 2 | no |
| `physics-add-joint` | Physics / Add Joint | `Tool_Physics.Joint.cs` | 3 | no |
| `physics-configure-joint` | Physics / Configure Joint | `Tool_Physics.Joint.cs` | 5 | no |
| `physics-remove-joint` | Physics / Remove Joint | `Tool_Physics.Joint.cs` | 1 | no |
| `physics-raycast-all` | Physics / Raycast All | `Tool_Physics.RaycastAll.cs` | 8 | no (should be yes) |
| `physics-get-rigidbody` | Physics / Get Rigidbody | `Tool_Physics.Rigidbody.cs` | 1 | no (should be yes) |
| `physics-configure-rigidbody` | Physics / Configure Rigidbody | `Tool_Physics.Rigidbody.cs` | 6 | no |
| `physics-validate` | Physics / Validate | `Tool_Physics.Validate.cs` | 0 | no (should be yes) |

**Total:** 21 tools, 1 helper file (`Tool_Physics.Helpers.cs`). Only **1** of 21 tools (`physics-ping`) is marked `ReadOnlyHint = true` — which is a notable gap because at least 9 of these tools are pure inspection (raycast/linecast/overlap/shapecast/get-* family).

**Internal Unity APIs used:**
- `Physics.AddForce`, `Physics.Raycast`, `Physics.RaycastNonAlloc`, `Physics.Linecast`, `Physics.SphereCast`, `Physics.BoxCast`, `Physics.OverlapSphereNonAlloc`, `Physics.OverlapBoxNonAlloc`
- `Physics.IgnoreLayerCollision`, `Physics.GetIgnoreLayerCollision`
- `Physics.gravity`, `Physics.defaultSolverIterations`, `Physics.defaultSolverVelocityIterations`, `Physics.bounceThreshold`, `Physics.sleepThreshold`, `Physics.defaultContactOffset`, `Physics.defaultMaxAngularSpeed`
- `Physics.simulationMode`, `Physics.Simulate`
- `PhysicsMaterial`, `PhysicsMaterialCombine`
- `Rigidbody.AddForce`, `Rigidbody.mass`, `Rigidbody.linearDamping`, `Rigidbody.angularDamping`, `Rigidbody.useGravity`, `Rigidbody.isKinematic`, `Rigidbody.linearVelocity`, etc.
- `FixedJoint`, `HingeJoint`, `SpringJoint`, `CharacterJoint`, `ConfigurableJoint` (`Joint.breakForce`, `breakTorque`, `enableCollision`, `enablePreprocessing`, `connectedBody`)
- `AssetDatabase.CreateAsset`, `AssetDatabase.LoadAssetAtPath`, `AssetDatabase.IsValidFolder`, `AssetDatabase.CreateFolder`, `AssetDatabase.SaveAssets`
- `EditorUtility.SetDirty`
- `LayerMask.LayerToName`, `LayerMask.NameToLayer`
- `SceneManager` (for `physics-validate`)

---

## 2. Redundancy Clusters

### Cluster R1 — Settings Inspection Overlap (`ping` vs `get-settings`)
**Members:** `physics-ping`, `physics-get-settings`
**Overlap:** `physics-ping` returns: gravity, fixedDeltaTime, defaultSolverIterations, defaultSolverVelocityIterations, bounceThreshold. `physics-get-settings` returns: gravity, defaultSolverIterations, defaultSolverVelocityIterations, bounceThreshold, sleepThreshold, defaultContactOffset, defaultMaxAngularSpeed. The intersection is 4 of 5 fields — `ping` is essentially `get-settings` minus three properties plus `Time.fixedDeltaTime`. The LLM has no clear way to choose between them without already knowing both exist.
**Impact:** Medium. Domain has a `Ping` tool by convention (most domains do), but its scope here significantly overlaps with the dedicated getter. The LLM will sometimes pick `ping` when the user wanted full settings, or vice versa. `physics-ping` could be retained as a `ReadOnlyHint = true` smoke-test (it is the only such tool in the domain) but its description should make the contrast explicit.
**Confidence:** high

### Cluster R2 — Cast/Overlap Family Fragmentation
**Members:** `physics-raycast`, `physics-raycast-all`, `physics-linecast`, `physics-shapecast`, `physics-overlap-sphere`, `physics-overlap-box`
**Overlap:** Six tools share huge swaths of parameter shape. All raycast/linecast/shapecast tools take some combination of origin XYZ (3 floats) + direction XYZ (3 floats) + maxDistance + layerMask. Overlap tools take center XYZ + size + layerMask. They differ only in which Unity API is invoked under the hood (`Raycast` vs `RaycastNonAlloc` vs `Linecast` vs `SphereCast`/`BoxCast` vs `OverlapSphereNonAlloc`/`OverlapBoxNonAlloc`). `physics-shapecast` already demonstrates the consolidation pattern by accepting `shape: "sphere" | "box"` — but only for casts, not overlaps.
**Impact:** High. This is a textbook redundancy cluster the LLM has to disambiguate constantly. `physics-raycast` vs `physics-raycast-all` differ only in `firstHitOnly: bool`. `physics-linecast` differs from `physics-raycast` only in whether the second point is "endpoint" (linecast) or "origin + direction × maxDistance" (raycast) — same physical query semantically. `physics-overlap-sphere` and `physics-overlap-box` could merge with `physics-shapecast` into a single `physics-query` tool with a `kind: cast | overlap | line` dispatcher, or at minimum overlap-sphere+overlap-box should collapse the way shapecast already does.
**Confidence:** high

### Cluster R3 — Joint Lifecycle Tools
**Members:** `physics-add-joint`, `physics-configure-joint`, `physics-remove-joint`
**Overlap:** Conceptually parallel to the Rigidbody pair (`get-rigidbody` / `configure-rigidbody`), but split into 3. `add-joint` and `configure-joint` could plausibly merge: `add-joint` does not let you set `breakForce`, `breakTorque`, `enableCollision`, or `enablePreprocessing` at creation time — the LLM must call two tools in sequence to add a configured joint. There is no `physics-get-joint` reader, even though every other component family has a getter (`get-rigidbody`, `get-collision-matrix`, `get-settings`).
**Impact:** Medium. Less severe than R2 because the actions are distinct (add vs modify vs remove), but the missing getter is real and the lack of configure-at-add forces 2-call workflows.
**Confidence:** medium (this is closer to a capability gap than a strict redundancy; flagging here for cluster-level visibility).

---

## 3. Ambiguity Findings

### A1 — `physics-ping` description doesn't disambiguate from `physics-get-settings`
**Location:** `physics-ping` — `Tool_Physics.Ping.cs:21`
**Issue:** Two tools both inspect physics settings; neither description tells the LLM when to prefer one over the other. See cluster R1.
**Evidence:** Description: *"Returns a quick summary of core physics settings: gravity, fixedDeltaTime, defaultSolverIterations, defaultSolverVelocityIterations, and bounceThreshold."* Compare to `physics-get-settings`: *"Gets current physics settings including gravity, default solver iterations, bounce threshold, sleep threshold, contact offset, and max angular speed."* No "use this when X, not Y" clause in either.
**Confidence:** high

### A2 — `physics-shapecast` `size` parameter is overloaded and underspecified
**Location:** `physics-shapecast` param `size` — `Tool_Physics.ShapeCast.cs:39`
**Issue:** The `size` parameter means radius for sphere casts but **half-extent of a uniform cube** for box casts. A box cast that needs different X/Y/Z extents (which is the common case) has no way to express that — the box is forced to be a cube. Description does not warn the LLM about this asymmetry.
**Evidence:** Line 67: `var halfExtents = new Vector3(size, size, size);` — hardcoded uniform extents. Description: *"Radius for sphere cast or half-extent for box cast. Defaults to 0.5."*
**Confidence:** high

### A3 — `physics-set-collision-matrix` description doesn't mention "all-pairs" or "reset" workflows
**Location:** `physics-set-collision-matrix` — `Tool_Physics.CollisionMatrix.cs:87-88`
**Issue:** Tool only sets one pair at a time. There is no batch-set or reset-all, and the description does not warn the LLM that configuring an N×N matrix requires N(N+1)/2 separate calls.
**Evidence:** Description: *"Sets whether two physics layers collide with each other. Accepts layer names or numeric indices."* No mention of bulk-set limitations.
**Confidence:** medium

### A4 — `physics-add-joint` description omits the connected-body workflow
**Location:** `physics-add-joint` — `Tool_Physics.Joint.cs:25`
**Issue:** Description lists supported joint types but does not explain that a `connectedBody` is *optional* and that omitting it creates a joint anchored to the world. Param description for `connectedBody` says "optional" but does not explain what "no connected body" means semantically.
**Evidence:** Method description: *"Adds a joint component to a GameObject. Supports fixed, hinge, spring, character, and configurable joint types."* No mention of connected-body semantics. Param description: *"Optional name or path of the GameObject to use as connected body."*
**Confidence:** medium

### A5 — `physics-add-joint` `jointType` magic string list duplicated in two places
**Location:** `physics-add-joint` — `Tool_Physics.Joint.cs:28`
**Issue:** Valid values are listed in the description ("fixed, hinge, spring, character, configurable"), but other tools that use enum-like strings (e.g. `physics-apply-force` `forceMode`) wrap valid values in single quotes. Inconsistent formatting across the domain.
**Evidence:** `physics-apply-force` forceMode: *"'Force', 'Impulse', 'Acceleration', or 'VelocityChange'"*. `physics-add-joint` jointType: *"fixed, hinge, spring, character, configurable"* (no quotes).
**Confidence:** low (cosmetic, but matters for LLM string parsing reliability)

### A6 — `physics-configure-rigidbody` description doesn't disclose side effect of adding component
**Location:** `physics-configure-rigidbody` — `Tool_Physics.Rigidbody.cs:69`
**Issue:** The tool **adds a Rigidbody if one doesn't exist** (line 91-95), but the description only mentions this in passing. Strong side effect that could surprise the LLM/user — should be the first sentence.
**Evidence:** Description: *"Configures rigidbody properties on a GameObject. Adds a Rigidbody component if one is not already present. Only non-null parameters are applied."* Buried in the middle.
**Confidence:** medium

### A7 — Tool descriptions never mention which `layerMask` format is expected
**Location:** Every cast/overlap tool — `Linecast.cs:35`, `Raycast.cs:37`, `RaycastAll.cs:38`, `ShapeCast.cs:41`, `Overlap.cs:31`, `Overlap.cs:89`
**Issue:** All cast/overlap tools accept `int layerMask` with description "-1 means all layers." But Unity layerMasks are **bitmasks** (e.g. layer 5 = `1 << 5 = 32`). An LLM reading "layer mask to filter which layers the ray can hit" might pass `5` thinking it means layer 5, when it actually means a mask covering layer 0 + layer 2. None of the descriptions explain bitmask encoding or give an example.
**Evidence:** `Tool_Physics.Raycast.cs:37` — *"Layer mask to filter which layers the ray can hit. -1 means all layers."* No example like `1 << 5` or `LayerMask.GetMask("Default", "Water")`.
**Confidence:** high

### A8 — Read-only tools not marked `ReadOnlyHint = true`
**Location:** `physics-get-settings`, `physics-get-rigidbody`, `physics-get-collision-matrix`, `physics-raycast`, `physics-raycast-all`, `physics-linecast`, `physics-shapecast`, `physics-overlap-sphere`, `physics-overlap-box`, `physics-validate`
**Issue:** 10 of 21 tools in the domain perform pure inspection (no scene mutation), but only `physics-ping` is marked `ReadOnlyHint = true`. The hint is documented in CLAUDE.md as a meaningful flag for inspection tools.
**Evidence:** Searched the domain for `ReadOnlyHint`; only one match at `Tool_Physics.Ping.cs:20`.
**Confidence:** high (verified against complete domain coverage)

---

## 4. Default Value Issues

### D1 — `physics-create-material` `savePath` default likely wrong
**Location:** `physics-create-material` param `savePath` — `Tool_Physics.PhysicsMaterial.cs:32`
**Issue:** Default is `"Assets/"`, which dumps physics materials at the project root. Common Unity convention is `"Assets/PhysicsMaterials/"` or `"Assets/Materials/Physics/"`. Saving to `Assets/` root is rarely what anyone wants.
**Current:** `string savePath = "Assets/"`
**Suggested direction:** Default to a conventional folder like `"Assets/PhysicsMaterials/"` (the tool already auto-creates the folder if missing — see lines 76-81), or require explicit savePath without a default.
**Confidence:** medium

### D2 — `physics-shapecast` `dirX`/`dirY`/`dirZ` defaults make a degenerate scene
**Location:** `physics-shapecast` params `dirX`, `dirY`, `dirZ` — `Tool_Physics.ShapeCast.cs:36-38`
**Issue:** Defaults are `0, 0, 1` (Z-forward). That's fine, but combined with `originX/Y/Z = 0` defaults and `size = 0.5`, calling the tool with no args casts a 0.5-unit sphere from world origin in +Z. That happens to be a useful smoke test, but the description should call it out as a deliberate "default smoke probe" or the defaults should be removed to force the LLM to specify origin + direction explicitly.
**Current:** `originX = 0f, originY = 0f, originZ = 0f, dirX = 0f, dirY = 0f, dirZ = 1f, size = 0.5f, maxDistance = 100f`
**Suggested direction:** Either keep defaults and explicitly document the "default smoke cast" use case, or drop defaults on origin/direction so the LLM must supply them. Inconsistent with `physics-raycast`/`physics-linecast` which have **no** defaults on origin/direction.
**Confidence:** medium

### D3 — `physics-shapecast` `size` default of 0.5 is arbitrary
**Location:** `physics-shapecast` param `size` — `Tool_Physics.ShapeCast.cs:39`
**Issue:** A 0.5 radius sphere cast or half-extent box cast (=1m wide cube) has no semantic justification. Most projects work in meters with units = 1, so 0.5 is "half a typical primitive." Marginal usefulness as a default.
**Current:** `float size = 0.5f`
**Suggested direction:** Make `size` required, OR document why 0.5 is the chosen default (matches Unity's default Sphere primitive radius).
**Confidence:** low

### D4 — `physics-simulate-step` step count default of 1 is too small for typical workflows
**Location:** `physics-simulate-step` param `steps` — `Tool_Physics.SimulateStep.cs:25`
**Issue:** Default `steps = 1, stepSize = 0.02` simulates 0.02s = 1 frame at 50Hz. For "let physics settle" workflows the user typically wants 30-100 steps. Default is technically valid but not aligned with common usage.
**Current:** `int steps = 1, float stepSize = 0.02f`
**Suggested direction:** Keep the defaults (they are conservative and explicit) but document a "typical settle" example in the description (e.g. "for letting physics settle, try steps = 50").
**Confidence:** low

### D5 — `physics-create-material` `dynamicFriction = 0.6, staticFriction = 0.6` defaults are reasonable but undocumented
**Location:** `physics-create-material` — `Tool_Physics.PhysicsMaterial.cs:33-34`
**Issue:** Defaults `0.6, 0.6` match Unity's built-in default PhysicsMaterial. Good choice, but description doesn't tell the LLM these match Unity defaults — the LLM may not know what "reasonable" friction is.
**Current:** `float dynamicFriction = 0.6f, float staticFriction = 0.6f`
**Suggested direction:** Add to description: "Defaults match Unity's built-in physics material (0.6 friction, 0 bounce)."
**Confidence:** low

---

## 5. Capability Gaps

### G1 — No 2D Physics Surface At All
**Workflow:** Configure 2D physics for a 2D project (URP 2D, sprites, top-down or side-scroller). Examples: add `Rigidbody2D` to a player, set gravity scale, raycast in 2D, configure `BoxCollider2D`, create a `PhysicsMaterial2D` for bouncy walls.
**Current coverage:** **None.** The Physics domain wraps `Physics` (3D) exclusively. No `physics2d-*` tools, no `Rigidbody2D` handling, no `Physics2D.Raycast` wrapper.
**Missing:** Entire 2D physics namespace. All of: `Rigidbody2D`, `Collider2D` family (BoxCollider2D, CircleCollider2D, PolygonCollider2D, EdgeCollider2D, CapsuleCollider2D, CompositeCollider2D), `Physics2D.Raycast` / `LinecastAll` / `OverlapBox` / `OverlapCircle` / `OverlapPoint`, `PhysicsMaterial2D`, 2D joints (`HingeJoint2D`, `SpringJoint2D`, `DistanceJoint2D`, `WheelJoint2D`, `SliderJoint2D`, `RelativeJoint2D`, `FrictionJoint2D`, `FixedJoint2D`, `TargetJoint2D`), `Physics2D.gravity` (Vector2 — not the same as 3D `Vector3` gravity).
**Evidence:** Grep across `Editor/Tools/` for `Rigidbody2D|Physics2D|Collider2D` returns **zero** matches. The test project (per CLAUDE.md) is *Jurassic Survivors — a 2D URP roguelike* — so this gap directly blocks the package's primary use case.
**Confidence:** high (verified by tree-wide grep)

### G2 — No `OverlapCapsule` or `CapsuleCast`
**Workflow:** Detect collisions for capsule-shaped characters/agents. Capsule is the most common collision proxy for humanoid characters in 3D.
**Current coverage:** `physics-overlap-sphere`, `physics-overlap-box`, `physics-shapecast` (sphere | box). No capsule.
**Missing:** Wrappers for `Physics.OverlapCapsule`, `Physics.OverlapCapsuleNonAlloc`, `Physics.CapsuleCast`, `Physics.CapsuleCastNonAlloc`. These are first-class Unity APIs and standard for character controllers.
**Evidence:** Grep for `CapsuleCast|OverlapCapsule` in `Editor/Tools/Physics/` returns zero matches.
**Confidence:** high

### G3 — No `Physics.CheckSphere` / `CheckBox` / `CheckCapsule` (boolean-only overlap)
**Workflow:** "Is there anything at this location?" — common for AI sensors, spawn validation, "is the ground beneath me solid?" ground checks. The Check* APIs return only `bool`, far cheaper than the full overlap variants when callers don't need collider details.
**Current coverage:** `physics-overlap-sphere` and `physics-overlap-box` always allocate a 256-element buffer and return a full listing.
**Missing:** Lightweight boolean overlap test. Could be folded into the existing overlap tools via an `outputMode = "list" | "count" | "any"` parameter, or surfaced as a dedicated `physics-check` tool.
**Evidence:** Grep for `CheckSphere|CheckBox|CheckCapsule` in domain returns zero matches.
**Confidence:** high

### G4 — No way to read or list joints on a GameObject
**Workflow:** Inspect existing joint configuration before modifying. A Unity dev would reasonably ask "what joint is on this object and what are its current break thresholds?"
**Current coverage:** `physics-add-joint`, `physics-configure-joint` (modifies first joint found), `physics-remove-joint` (removes all). No reader.
**Missing:** A `physics-get-joint` (or `physics-list-joints`) tool. Every other physics-component family in the domain has a getter (`physics-get-rigidbody`, `physics-get-settings`, `physics-get-collision-matrix`).
**Evidence:** No file in `Editor/Tools/Physics/` defines a tool reading joint properties. Searched the domain explicitly; `Tool_Physics.Joint.cs` contains only `AddJoint`, `ConfigureJoint`, `RemoveJoint`.
**Confidence:** high

### G5 — `physics-configure-joint` operates on first joint only, with no selector
**Workflow:** Configure a *specific* joint when a GameObject has more than one (common for ragdolls — a hip with both `CharacterJoint` and `ConfigurableJoint` is plausible).
**Current coverage:** `Tool_Physics.Joint.cs:122` calls `go.TryGetComponent<Joint>(out var joint)` which returns the first match. No way to specify "configure the HingeJoint" vs "configure the SpringJoint" on the same object.
**Missing:** A `jointIndex` or `jointType` selector on `physics-configure-joint`. Note `physics-remove-joint` already operates on all joints (line 179: `go.GetComponents<Joint>()`), so the joint API is asymmetric — can't selectively remove either.
**Evidence:** `Tool_Physics.Joint.cs:122` — `if (!go.TryGetComponent<Joint>(out var joint))`. `Tool_Physics.Joint.cs:179` — `var joints = go.GetComponents<Joint>();` (loops to remove all).
**Confidence:** high

### G6 — `physics-add-joint` cannot configure properties at creation
**Workflow:** Add a joint with a specific break force in one call. Currently requires `physics-add-joint` followed by `physics-configure-joint` — a fragile two-step the LLM might forget to complete.
**Current coverage:** `physics-add-joint` accepts only `target`, `jointType`, `connectedBody`. To set `breakForce` etc. the LLM must call `physics-configure-joint` separately.
**Missing:** `breakForce`, `breakTorque`, `enableCollision`, `enablePreprocessing`, anchor positions, axis vectors, joint-type-specific properties on the add call.
**Evidence:** `Tool_Physics.Joint.cs:24-30` — signature shows only 3 params. Compare to `physics-configure-rigidbody` (`Tool_Physics.Rigidbody.cs:70-77`) which folds creation + configuration into one tool.
**Confidence:** high

### G7 — Type-specific joint properties unreachable
**Workflow:** Configure hinge joint angular limits, spring joint min/max distance and spring force, character joint twist/swing limits, configurable joint XYZ motion drives. These are the *primary reason* you'd choose one joint type over another, but no tool exposes them.
**Current coverage:** `physics-configure-joint` only sets common base-class `Joint` properties (breakForce, breakTorque, enableCollision, enablePreprocessing).
**Missing:** Type-specific configuration: `HingeJoint.spring`, `HingeJoint.limits`, `HingeJoint.motor`, `HingeJoint.useLimits`, `HingeJoint.useMotor`; `SpringJoint.spring`, `SpringJoint.damper`, `SpringJoint.minDistance`, `SpringJoint.maxDistance`; `CharacterJoint.lowTwistLimit`, `highTwistLimit`, `swing1Limit`, `swing2Limit`; full `ConfigurableJoint` motion/drive surface.
**Evidence:** `Tool_Physics.Joint.cs:130-152` — only 4 properties handled. `joint.GetType().Name` is logged on line 128 but never used as a dispatcher into type-specific configuration.
**Confidence:** high

### G8 — No tool for `Rigidbody.AddTorque`, `AddForceAtPosition`, `AddExplosionForce`, `MovePosition`, `MoveRotation`
**Workflow:** Apply rotation forces (spinning a wheel), point-applied forces (impact at hit location), explosions (knockback), kinematic movement (`MovePosition` for kinematic rigidbodies).
**Current coverage:** Only `physics-apply-force` (`Rigidbody.AddForce`).
**Missing:** Wrappers for `AddTorque`, `AddForceAtPosition`, `AddExplosionForce`, `MovePosition`, `MoveRotation`, `AddRelativeForce`, `AddRelativeTorque`. Could be folded into a single `physics-apply-force` with a `mode = force | torque | forceAtPosition | explosion | movePosition | moveRotation` dispatcher (similar to how `physics-shapecast` dispatches by shape).
**Evidence:** `Tool_Physics.ApplyForce.cs:62` — only `rb.AddForce(force, mode)` is called. Grep for `AddTorque|AddForceAtPosition|AddExplosionForce|MovePosition|MoveRotation` in `Editor/Tools/Physics/` returns zero matches.
**Confidence:** high

### G9 — `physics-configure-rigidbody` doesn't expose interpolation, collision detection, or constraints
**Workflow:** Tune a fast-moving Rigidbody (set `collisionDetectionMode = Continuous`), enable interpolation for smooth visual movement, lock axes (e.g. freeze Y position for a 2.5D platformer's 3D Rigidbody).
**Current coverage:** `physics-configure-rigidbody` accepts only mass, drag, angularDrag, useGravity, isKinematic. The reader `physics-get-rigidbody` *reports* interpolation, collisionDetectionMode, and constraints (lines 47-51 of `Rigidbody.cs`) but the writer cannot set them.
**Missing:** Setters for `interpolation` (string: None | Interpolate | Extrapolate), `collisionDetectionMode` (string: Discrete | Continuous | ContinuousDynamic | ContinuousSpeculative), `constraints` (RigidbodyConstraints flags — string mask like "FreezePositionY|FreezeRotation").
**Evidence:** `Tool_Physics.Rigidbody.cs:70-77` — only 5 nullable settable params. Compare to lines 47-51 which read 4 *additional* properties. The asymmetry between getter and setter is concrete.
**Confidence:** high

### G10 — No `physics-set-settings` support for `defaultMaxAngularSpeed` (correctness gap)
**Workflow:** Configure max angular speed, e.g. for high-RPM wheels.
**Current coverage:** `physics-get-settings` reads `Physics.defaultMaxAngularSpeed` (`Tool_Physics.GetSettings.cs:33`) but `physics-set-settings` has no parameter for it.
**Missing:** A `defaultMaxAngularSpeed` parameter on `physics-set-settings`. Symmetric with the existing reader.
**Evidence:** `Tool_Physics.GetSettings.cs:33` reads `Physics.defaultMaxAngularSpeed`. `Tool_Physics.SetSettings.cs:28-35` lists 6 nullable params; none is `defaultMaxAngularSpeed`. Provable asymmetry.
**Confidence:** high

### G11 — `physics-create-material` cannot edit existing materials
**Workflow:** Tweak a `PhysicsMaterial` asset's friction or bounce after creation — common iterative tuning workflow.
**Current coverage:** `physics-create-material` (creates new), `physics-assign-material` (assigns to collider). No edit/update.
**Missing:** A `physics-update-material` tool, or an `overwrite` parameter on `physics-create-material`. Currently the only way to modify an existing material is to delete it and recreate.
**Evidence:** `Tool_Physics.PhysicsMaterial.cs:84` — `AssetDatabase.CreateAsset(mat, assetPath);` will *fail* if the asset already exists at path (Unity behavior). No code path handles update/overwrite.
**Confidence:** high

### G12 — No way to reset collision matrix or query whole matrix as data
**Workflow:** Reset the collision matrix to "all collide" (factory default), or read the matrix programmatically rather than as formatted text.
**Current coverage:** `physics-get-collision-matrix` returns formatted text. `physics-set-collision-matrix` sets one pair at a time.
**Missing:** A reset action. Also no structured (pair-list) output format — the LLM can only get formatted text it must re-parse.
**Evidence:** `Tool_Physics.CollisionMatrix.cs` contains only the two tools described. No reset, no structured output.
**Confidence:** medium

### G13 — `physics-validate` is read-only but has no auto-fix mode
**Workflow:** "Find and fix all rigidbodies missing colliders" — a Unity dev would expect either a separate "fix" tool or a `--fix` flag on validate. Currently the LLM has to read the report and orchestrate fixes one by one.
**Current coverage:** `physics-validate` reports 4 categories of issues (missing collider, no rigidbody on non-static collider, mass ratio >100:1, non-uniform scale).
**Missing:** No companion fix tool, no `autoFix: bool` parameter. Each warning requires a separate LLM-orchestrated remediation call.
**Evidence:** `Tool_Physics.Validate.cs:25` — signature has zero params. Tool reports issues but does not act on them.
**Confidence:** medium (this is design choice; flagging because the gap forces 5+ tool-call workflows the LLM may abandon mid-sequence)

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher = more bang for buck.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 5 | 5 | high | No 2D physics support — blocks Jurassic Survivors test project entirely. Large effort but enormous impact. |
| 2 | R2 | Redundancy | 5 | 3 | 15 | high | Cast/overlap fragmentation — 6 tools share parameter shape; LLM has to disambiguate constantly. Consolidate via shape/kind dispatch. |
| 3 | G7 | Capability Gap | 4 | 3 | 12 | high | Type-specific joint properties (HingeJoint limits, SpringJoint spring, etc) unreachable; the *reason* you pick a joint type is unconfigurable. |
| 4 | G9 | Capability Gap | 4 | 2 | 16 | high | `configure-rigidbody` missing interpolation/collisionDetection/constraints; getter already reports them. Trivial setter additions, big payoff. |
| 5 | A8 | Ambiguity | 3 | 1 | 15 | high | 10 read-only tools missing `ReadOnlyHint = true`. One-line fix per file. |
| 6 | G6 | Capability Gap | 3 | 2 | 12 | high | `physics-add-joint` can't set properties at creation — forces 2-call workflow that's fragile. |
| 7 | G10 | Capability Gap | 3 | 1 | 15 | high | `set-settings` missing `defaultMaxAngularSpeed` — pure correctness gap, getter/setter asymmetry. |
| 8 | A7 | Ambiguity | 4 | 1 | 20 | high | `layerMask` descriptions don't explain bitmask encoding — LLM will pass `5` instead of `1<<5`. Highest-priority fix on the list. |
| 9 | A2 | Ambiguity | 3 | 2 | 12 | high | `physics-shapecast` `size` forces uniform box extents; description doesn't warn. |
| 10 | G4 | Capability Gap | 3 | 2 | 12 | high | No `physics-get-joint` reader — breaks the get/configure parity used elsewhere in the domain. |
| 11 | G2 | Capability Gap | 3 | 3 | 9 | high | No `CapsuleCast` / `OverlapCapsule` — standard for character controllers. |
| 12 | G8 | Capability Gap | 3 | 3 | 9 | high | Only `AddForce` exposed; no torque, explosion, force-at-position, kinematic move. |
| 13 | R1 | Redundancy | 2 | 1 | 10 | high | `physics-ping` overlaps `physics-get-settings`. Fix via description disambiguation. |
| 14 | G11 | Capability Gap | 2 | 2 | 8 | high | Can't update a PhysicsMaterial without delete+recreate. |
| 15 | A6 | Ambiguity | 2 | 1 | 10 | medium | `configure-rigidbody` description buries the side effect of adding component. |
| 16 | D1 | Default | 2 | 1 | 10 | medium | `create-material` `savePath` default is project root — likely wrong. |

(Lower-priority findings R3, A1, A3, A4, A5, D2, D3, D4, D5, G3, G5, G12, G13 are documented above but not in the top ranks; revisit during planning if scope allows.)

---

## 7. Notes

**Cross-domain considerations for the planner:**
- `Editor/Tools/Component/Tool_Component.Add.cs` already provides a generic component-add tool. There may be an opportunity to deprecate `physics-add-joint` and `physics-configure-rigidbody`'s "auto-add" branch in favor of routing through the Component domain, *if* the planner chooses to consolidate across domains. That said, Physics-domain shortcuts have ergonomic value (one tool for "set up a joint") and removing them would worsen LLM workflow fragmentation. Default recommendation: keep the Physics-domain shortcuts but verify they don't drift from `Tool_Component.Add.cs` semantics.
- The 2D physics gap (G1) is enormous. Whether to add a separate `Physics2D` domain or fold 2D handling into the Physics domain via a `dimension: "2d" | "3d"` parameter is a strategic call for the consolidation-planner / Ramon. Recommendation: separate `Physics2D` domain — the API surfaces are non-trivially different (`Vector2` gravity, `RigidbodyType2D`, `effectiveLayerMask`, `RaycastHit2D`).

**Workflows intentionally deferred:**
- I did not audit the proxy/server side (`Server~/`) to check whether any physics-related tools exist there. Scope was strictly `Editor/Tools/Physics/`.
- I did not check the `UnityDocs/` curated knowledge layer for physics documentation that might offset some description ambiguity. If `UnityDocs` already includes a "physics primer" tool, several A-class findings could be downgraded.

**Open questions for the reviewer (Ramon):**
1. Is 2D physics support already on the v2.0 roadmap, or is the package intentionally 3D-only? If intentional, G1 should be re-tagged as "won't fix" rather than a gap.
2. Should `physics-ping` be retained as a stylistic convention (every domain has one) or merged into `physics-get-settings`?
3. For consolidation of cast/overlap (R2), is there a preferred dispatch style — `kind: cast | overlap | line` with sub-params, or stay with separate tools and just improve descriptions?

**Audit limits:**
- This audit reads source only; it does not execute any tool against a live Unity instance. Behavioral bugs (e.g. does `physics-simulate-step` actually restore `simulationMode` correctly across an assembly reload?) are out of scope.
- I did not run `dotnet build` to verify compilation. All findings assume the source compiles as-is.
