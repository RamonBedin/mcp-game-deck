# Audit Report — Transform

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Transform/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 4 (via Glob `Editor/Tools/Transform/**/*.cs`)
- `files_read`: 4
- `files_analyzed`: 4

**Balance:** ✅ balanced

**Files in scope:**
- `Editor/Tools/Transform/Tool_Transform.cs` (root partial, `[McpToolType]`, contains `FindGameObject` helper — no tool methods)
- `Editor/Tools/Transform/Tool_Transform.Move.cs` (1 tool: `transform-move`)
- `Editor/Tools/Transform/Tool_Transform.Rotate.cs` (1 tool: `transform-rotate`)
- `Editor/Tools/Transform/Tool_Transform.Scale.cs` (1 tool: `transform-scale`)

**Errors encountered during audit:** None.

**Files not analyzed:** None.

**Absence claims in this report:** Permitted — accounting is balanced. All absence claims (e.g. "no read-only `transform-get` tool exists in this domain") are made over the full domain.

**Cross-domain reads (for context only, not part of the audit scope):**
- `Editor/Tools/GameObject/Tool_GameObject.MoveRelative.cs` — directional move (forward/back/left/right/up/down) with reference object
- `Editor/Tools/GameObject/Tool_GameObject.LookAt.cs` — rotates GO toward a target point or another GO
- `Editor/Tools/GameObject/Tool_GameObject.Get.cs` — read-only; reports transform position, rotation (euler), local scale
- `Editor/Tools/GameObject/Tool_GameObject.Update.cs` — does NOT touch transform (metadata only: name/tag/layer/active/static)
- `Editor/Tools/GameObject/Tool_GameObject.SetParent.cs` — parenting lives in GameObject domain
- `Editor/Tools/Component/Tool_Component.Update.cs` — JSON property map; supports only float/int/bool/string (does NOT cover Vector3/Quaternion writes, so no cross-domain redundancy with Transform writers)

**Reviewer guidance:**
- The Transform domain is small (3 tools, all action-style, all symmetric in shape). The most interesting findings are at the BOUNDARY with the GameObject domain (where `gameobject-move-relative` and `gameobject-look-at` arguably belong here) and in the OMISSIONS (no read-only get-transform, no reset, no Quaternion path, no parent-aware coordinate handling).
- All three tools follow the same parameter pattern (`instanceId`/`objectPath` + `x/y/z` + `space` + `relative`). This is a clear consolidation candidate, but please weigh it against the LLM-ergonomics cost of a polymorphic `transform` tool with an `op` discriminator.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `transform-move` | Transform / Move | `Tool_Transform.Move.cs` | 7 (`instanceId`, `objectPath`, `x`, `y`, `z`, `space`, `relative`) | no |
| `transform-rotate` | Transform / Rotate | `Tool_Transform.Rotate.cs` | 7 (`instanceId`, `objectPath`, `x`, `y`, `z`, `space`, `relative`) | no |
| `transform-scale` | Transform / Scale | `Tool_Transform.Scale.cs` | 6 (`instanceId`, `objectPath`, `x`, `y`, `z`, `relative`) | no |

**Internal Unity API surface used:**
- `Undo.RecordObject` (all three)
- `Transform.position` / `Transform.localPosition` (Move)
- `Transform.rotation` / `Transform.localRotation`, `Quaternion.Euler` (Rotate)
- `Transform.localScale`, `Transform.lossyScale` (Scale; Scale only writes localScale — there is no world-scale write path because Unity has none directly)
- `EditorUtility.InstanceIDToObject` (deprecated, suppressed with `#pragma warning disable CS0618`) and `GameObject.Find` (in `FindGameObject` helper)

**Notes:**
- All three tools register Undo and execute on the main thread via `MainThreadDispatcher.Execute`.
- No tool in the domain is `ReadOnlyHint = true`. The closest read-only inspection of transform values lives outside this domain in `gameobject-get` (which reports position, rotation eulers, and localScale).
- `Tool_Transform.cs` houses the shared `FindGameObject(int, string)` helper used by ~30 tools across the codebase (per Grep). It is a de-facto cross-domain helper despite being declared in the Transform partial class.

---

## 2. Redundancy Clusters

### Cluster R1 — Symmetric Move/Rotate/Scale shape
**Members:** `transform-move`, `transform-rotate`, `transform-scale`
**Overlap:** All three tools share an almost identical parameter schema: GameObject locator (`instanceId`/`objectPath`) + three floats (`x`/`y`/`z`) + a `relative` boolean. Move and Rotate also share `space ("world"|"local")`. The semantics differ (translation vs Euler rotation vs componentwise scale multiply), but the LLM-facing shape is essentially `(target, vector, [space], relative)`. This is the textbook case for an `action`-dispatched tool (compare `Tool_Animation.ConfigureController.cs`, which uses an `action` discriminator over related Animator-Controller operations).
**Impact:** Medium. With only 3 tools the LLM is unlikely to mis-pick *between* them (the verbs are distinct), but the duplication multiplies the surface area for description drift, default drift, and future-feature drift (e.g. someone might add `pivot` to Rotate but forget Scale). It also forces the LLM to learn three nearly-identical schemas instead of one. Consolidation would also be a natural place to add Quaternion support, pivot/center-of-rotation, and uniform-scale shorthand (see G2/G4).
**Confidence:** high (full coverage; signatures inspected directly).

### Cluster R2 — Cross-domain transform writers (informational, not a Transform-only finding)
**Members:** `transform-move` (Transform), `gameobject-move-relative` (GameObject), `transform-rotate` (Transform), `gameobject-look-at` (GameObject)
**Overlap:** `gameobject-move-relative` writes `transform.position` (translation along a named axis). `gameobject-look-at` writes `transform.rotation` (via `Transform.LookAt`). Both are conceptually transform writes that ended up in the GameObject domain. From an LLM perspective, "move this GO 3 units forward" could plausibly route to either `transform-move` (with a hand-computed delta) or `gameobject-move-relative`; "rotate this GO to face the player" could route to either `transform-rotate` (with a hand-computed Euler) or `gameobject-look-at`.
**Impact:** Medium. This is a domain-boundary issue — the Transform domain is *missing* the directional/look-at convenience tools, and the GameObject domain is hosting work that is conceptually transform manipulation. Either move them in, or document the split clearly in descriptions so the LLM picks deterministically. This is flagged for awareness; the fix is out of scope for a Transform-only refactor.
**Confidence:** medium (the redundancy is conceptual, not literal — different APIs under the hood; flagging for the planner to consider during cross-domain consolidation).

---

## 3. Ambiguity Findings

### A1 — `space` parameter does not enumerate valid values in the Description
**Location:** `transform-move` and `transform-rotate` — `Tool_Transform.Move.cs:35`, `Tool_Transform.Rotate.cs:35`
**Issue:** The parameter `Description` mentions `"world"` and `"local"` but the runtime parser does only `space == "local"`. Any other value silently routes to the world-space branch — there is no validation, no error on a typo like `"World"`, `"Local"`, `"global"`, or `"self"`. The description does not warn about case sensitivity nor about the silent fallback.
**Evidence:** `Tool_Transform.Move.cs:51` — `bool useLocal = space == "local";`. Same pattern at `Tool_Transform.Rotate.cs:51`. Description verbatim: `"Coordinate space: 'world' or 'local'. Default is 'world'."`
**Confidence:** high

### A2 — `relative` semantics for rotation is mathematically nontrivial but undocumented
**Location:** `transform-rotate` — `Tool_Transform.Rotate.cs:36`
**Issue:** The Description says `"If true, adds the given angles to the current rotation."` That is misleading: the implementation does `go.transform.rotation *= Quaternion.Euler(euler)` (line 61) or the local equivalent. Quaternion multiplication is **composition**, not "addition of Euler angles" — order matters and the resulting Euler readout is generally not the sum of the prior Euler and the supplied one (especially around gimbal-lock zones). An LLM following the description literally will produce subtly wrong rotations on non-trivial bases.
**Evidence:** Description vs. line 57/61: `go.transform.localRotation *= Quaternion.Euler(euler);` / `go.transform.rotation *= Quaternion.Euler(euler);`
**Confidence:** high

### A3 — `transform-scale` `relative` semantics is "componentwise multiply", described as "multiplies"
**Location:** `transform-scale` — `Tool_Transform.Scale.cs:34`
**Issue:** The Description says `"multiplies each axis of the current local scale by the given values"`. This is correct for the math but the LLM may not infer that passing `relative=true, x=1, y=1, z=1` is a no-op (commonly an LLM might think "1 means 100% increase" → 2× scale). A concrete example would help: e.g. `"To double size: relative=true, x=2, y=2, z=2. To halve: x=0.5..."`.
**Evidence:** `Tool_Transform.Scale.cs:34` description; `Tool_Transform.Scale.cs:53` implementation: `new Vector3(current.x * scale.x, current.y * scale.y, current.z * scale.z)`.
**Confidence:** medium

### A4 — Tool descriptions do not disambiguate from cross-domain writers
**Location:** All three Transform tools.
**Issue:** Descriptions do not contain a "use this when X, not Y" clause distinguishing them from `gameobject-move-relative` (named-direction move) and `gameobject-look-at` (target-aware rotation). An LLM seeing the prompt "move the player 3 units forward in local space" has at least two plausible candidates (`transform-move` with a hand-computed forward vector OR `gameobject-move-relative` with `direction="forward"`). Same problem for "rotate to face X". See R2.
**Evidence:** `transform-move` description verbatim: `"Moves a GameObject to an absolute or relative position in world or local space. Returns the resulting world position after the operation."` — no mention of when NOT to use it.
**Confidence:** medium

### A5 — No mention that scale's `space` axis is ALWAYS local
**Location:** `transform-scale` — `Tool_Transform.Scale.cs:26-34`
**Issue:** Move and Rotate accept a `space` parameter; Scale does not (correctly, since Unity has no world-scale setter). But the Description ("Scales a GameObject to an absolute local scale or multiplies its current scale by the given values") could leave an LLM wondering why `space` is missing here. A short note like "Unity exposes only `localScale`; world-space lossy scale is read-only" would prevent confused follow-up calls.
**Evidence:** `Tool_Transform.Scale.cs:27` — Description omits any rationale for why this tool lacks `space`.
**Confidence:** low

### A6 — `lossyScale` is reported but its meaning is undocumented in the response
**Location:** `transform-scale` — `Tool_Transform.Scale.cs:63`
**Issue:** The response prints both `Local Scale` and `Lossy Scale`. The terms are Unity-jargon; an LLM relaying this back to a non-Unity-expert user has no description of the difference. Not strictly an ambiguity in the *tool*, but in the *output contract*.
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `transform-move` has nonsensical "all-zero" default
**Location:** `transform-move` — `Tool_Transform.Move.cs:32-34`
**Issue:** With defaults, calling `transform-move` with only an `instanceId` (no x/y/z, default `space="world"`, default `relative=false`) **teleports the object to world origin**. This is almost never the intent. If anything, the no-arg case for an LLM means "I don't know, give me a default" — and silently moving to (0,0,0) is destructive. A safer pattern: leave `x/y/z` as `float.NaN` sentinels and require at least one to be provided, OR default `relative` to `true` (then 0,0,0 is a no-op).
**Current:** `float x = 0f, float y = 0f, float z = 0f, string space = "world", bool relative = false`
**Suggested direction:** Either (a) require at least one axis to be specified and error otherwise, or (b) treat `relative=false` + all-zero as an explicit no-op with a warning, or (c) flip the default to `relative=true` so an under-specified call is a harmless null-op rather than a teleport. (No specific code suggestion — that's the planner's call.)
**Confidence:** high

### D2 — `transform-rotate` has the same all-zero teleport-to-identity problem
**Location:** `transform-rotate` — `Tool_Transform.Rotate.cs:32-36`
**Issue:** Same as D1. Default-arg call with only an `instanceId` resets the object's rotation to identity (`Quaternion.Euler(0,0,0)`). Less destructive than position-zero (rotation-zero is sometimes a deliberate "reset to upright"), but still a footgun.
**Current:** `float x = 0f, float y = 0f, float z = 0f, string space = "world", bool relative = false`
**Suggested direction:** Same options as D1. Note that "reset rotation" is itself a useful capability that currently has no dedicated tool (see G3) — the absence of `transform-reset` is part of why this default is so easy to land on accidentally.
**Confidence:** high

### D3 — `transform-scale` defaults to `(1, 1, 1)` — coherent for absolute mode, footgun in relative mode
**Location:** `transform-scale` — `Tool_Transform.Scale.cs:31-33`
**Issue:** Defaults `x=1, y=1, z=1` are semantically correct for *absolute* mode (sets identity scale) but are silent no-ops in *relative* mode. Better than D1/D2 — a default no-op is harmless — but still inconsistent with Move and Rotate where defaults are dangerous. Inconsistency across the cluster is itself a smell.
**Current:** `float x = 1f, float y = 1f, float z = 1f, bool relative = false`
**Suggested direction:** Aligning the three tools' default-value philosophy is a good consolidation goal (see R1).
**Confidence:** medium

### D4 — `space` accepts a string with hidden case sensitivity and no validation
**Location:** `transform-move` and `transform-rotate` — both at param `space`
**Issue:** `space = "world"` default with `space == "local"` check (case-sensitive substring match). Passing `"World"` (capital W) or `"local "` (trailing space) silently selects world-space. No `Trim().ToLowerInvariant()`, no error on unrecognized values. Compare `gameobject-move-relative` which does `direction.Trim().ToLowerInvariant()` and errors on unknowns (`Tool_GameObject.MoveRelative.cs:77, 105`).
**Current:** `string space = "world"`
**Suggested direction:** Normalize input and reject unknown values (or accept enum-style `0`/`1` ints). Not strictly a default-value issue but tightly related.
**Confidence:** high

### D5 — `instanceId = 0` / `objectPath = ""` requires both-defaults-means-no-target
**Location:** All three tools.
**Issue:** Default is `instanceId = 0, objectPath = ""`. If both are passed unset, `FindGameObject` returns null and the tool errors with `"GameObject not found. instanceId=0, objectPath=''."`. This is fine, but the description does not state that at least one MUST be provided. An LLM probing the tool with no args gets a runtime error instead of a schema-time hint.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** Either mark both as nullable/required-via-precondition or document the invariant explicitly in the method-level Description ("Provide either instanceId or objectPath; one is required.")
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — No read-only `transform-get` (read transform values without mutating)
**Workflow:** "What is the current world position / local rotation Euler / lossy scale of object X?" — a foundational read that the LLM needs before nearly every meaningful transform write (especially in `relative=true` cases).
**Current coverage:** `gameobject-get` (in GameObject domain, `ReadOnlyHint = true`) returns transform info as part of a larger payload (`Tool_GameObject.Get.cs:62-65`: position, rotation eulers, localScale). It does NOT report `localPosition`, `localRotation`, `localEulerAngles`, `lossyScale`, `forward/right/up` axes, or world-space rotation as a Quaternion.
**Missing:** A focused, transform-only read tool. Use cases: (a) plan an additive rotation knowing the *current* Euler (and avoid the gimbal-lock surprise of A2), (b) compute a delta to reach a target, (c) inspect lossy scale (which is read-only in Unity and not exposed elsewhere), (d) get axis vectors (`transform.forward` etc.) for client-side math.
**Evidence:** No `[McpTool("transform-get", ...)]` exists in the domain — confirmed by Grep `McpTool\("transform-` returning only `transform-move`, `transform-rotate`, `transform-scale`. No `ReadOnlyHint = true` in `Editor/Tools/Transform/` — confirmed by Grep on the directory.
**Confidence:** high (full domain coverage, accounting balanced).

### G2 — No Quaternion rotation path; only Euler
**Workflow:** Apply a precise rotation supplied by another tool/computation (e.g. an animation system, a Camera target, a serialized snapshot) without round-tripping through Euler angles.
**Current coverage:** `transform-rotate` accepts `(x, y, z)` floats and constructs `Quaternion.Euler(euler)` (`Tool_Transform.Rotate.cs:50, 57, 61, 68, 72`).
**Missing:** A path that accepts a Quaternion `(x, y, z, w)` directly. Euler→Quaternion is lossy near singularities; an LLM that just read out a Quaternion from another tool has no way to write it back without introducing drift. Unity's `Transform.rotation` setter takes a Quaternion natively — wrapping it is trivial.
**Evidence:** `Tool_Transform.Rotate.cs:50` constructs `var euler = new Vector3(x, y, z)` and only ever feeds `Quaternion.Euler(euler)` into the rotation setter. No 4-component overload anywhere in the domain.
**Confidence:** high

### G3 — No `transform-reset` (reset position / rotation / scale to identity)
**Workflow:** "Reset this object's transform" — a one-click Inspector affordance in Unity that's commonly requested in tutorials and authoring sessions.
**Current coverage:** Achievable today only by three separate calls: `transform-move x=0 y=0 z=0 relative=false`, `transform-rotate x=0 y=0 z=0 relative=false`, `transform-scale x=1 y=1 z=1 relative=false`. Each is itself a footgun (D1/D2) because of the all-zero default.
**Missing:** A single tool with optional flags `resetPosition`, `resetRotation`, `resetScale` (defaults all true) that does `localPosition = Vector3.zero; localRotation = Quaternion.identity; localScale = Vector3.one` under one Undo entry. Unity's built-in "Reset" context menu does this with `Transform.SetLocalPositionAndRotation` + `localScale = Vector3.one`.
**Evidence:** No `transform-reset` in domain (Grep `McpTool\("transform-`). No equivalent macro in any other domain (cross-checked Grep across `Editor/Tools/`).
**Confidence:** high

### G4 — No pivot-aware rotation or scale-around-point
**Workflow:** "Rotate this object 30° around the world Y axis pivoted at point P" or "scale this object 2× from the bottom-center". This is one of the most common 3D-authoring operations after move/rotate/scale, and Unity handles it via `Transform.RotateAround(point, axis, degrees)`.
**Current coverage:** `transform-rotate` always rotates in place around the object's pivot; `transform-scale` always scales in place. No `pivot` parameter on either.
**Missing:** A pivot/anchor parameter (or a separate `transform-rotate-around` tool that wraps `Transform.RotateAround`). This is especially relevant for level authoring and prefab assembly.
**Evidence:** `Tool_Transform.Rotate.cs:55-73` writes `localRotation` / `rotation` directly with no pivot logic; no calls to `Transform.RotateAround` anywhere in the Transform domain (Grep on the directory).
**Confidence:** high

### G5 — No uniform-scale convenience or 2D-axis convenience
**Workflow:** "Make this 2× larger" (uniform); "set width to 5 keeping height" (axis-locked); "scale only X". 2D workflows in particular almost never want non-uniform Z scaling.
**Current coverage:** `transform-scale` requires all three components. There is no shortcut for `uniform = 2.0` or for "leave this axis unchanged".
**Missing:** Either (a) a `uniform` float param that, when non-zero, overrides x/y/z, or (b) a sentinel value (e.g. `float.NaN` or a special negative) meaning "leave unchanged on this axis". Today the LLM has to read the current scale (via `gameobject-get`, which doesn't even include local axes separately) just to preserve one axis.
**Evidence:** `Tool_Transform.Scale.cs:48-58` — three independent floats with no aliasing.
**Confidence:** medium

### G6 — `relative` rotation cannot specify rotation order or local-axis convention
**Workflow:** "Rotate 30° around the *world* Y axis while keeping local X/Z untouched." The current `relative=true, space="world"` path does `transform.rotation *= Quaternion.Euler(0, 30, 0)`, which is a *local-frame* multiply on the right — NOT a world-space pre-multiply. Result: the object rotates 30° around its *own* Y, not the world Y.
**Current coverage:** None. The `space` parameter selects which property is read/written (`rotation` vs `localRotation`) but the Quaternion composition is always right-multiply.
**Missing:** Either (a) a clear spec correction in the Description (this is partly an A2 issue), or (b) an explicit pre/post-multiply selector, or (c) wrapping `Transform.Rotate(eulerAngles, Space.World)` which Unity provides specifically to disambiguate this.
**Evidence:** `Tool_Transform.Rotate.cs:57, 61` — both `space` branches use `*=` (right-multiply). `Transform.Rotate(...)` with explicit `Space` enum is not used anywhere in the domain.
**Confidence:** high

### G7 — No batch transform (apply same op to multiple objects)
**Workflow:** "Move all selected enemies up by 1 unit" or "scale every child of this parent by 2". Frequent in editor-driven workflows.
**Current coverage:** None of the three Transform tools accepts an array of targets; each is single-target. The Selection domain has selection-aware reads but cross-checking shows no batch transform writer.
**Missing:** Either an `instanceIds` (array) variant, or a sibling `transform-batch` macro that takes a list of targets + an op spec. Today the LLM must loop tool calls, which is fragmented and cannot share a single Undo group.
**Evidence:** All three tools accept `int instanceId` (singular) and `string objectPath` (singular). Grep across `Editor/Tools/` for `int[]` or `instanceIds` near transform-related code finds nothing in the Transform domain.
**Confidence:** medium (depends on whether batch operations are an explicit non-goal for the package; flagging for the planner).

### G8 — Deprecated `EditorUtility.InstanceIDToObject` used in shared helper
**Workflow:** Per CLAUDE.md, the project standard is `EntityIdToObject(...)` — the Unity 6000.3 successor. The shared `FindGameObject` helper in `Tool_Transform.cs:29` still uses the deprecated API with `#pragma warning disable CS0618`.
**Current coverage:** Works today, suppressed warning.
**Missing:** Migration to `EntityIdToObject`. This is technically a code-quality finding rather than a capability gap, but because the helper is shared by ~30 tools across the codebase (per Grep), it shows up as a domain-shaped concern in the Transform audit. Out of strict Transform-tool scope, but worth flagging.
**Evidence:** `Tool_Transform.cs:28-30`:
```
#pragma warning disable CS0618
                var obj = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618
```
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 1 | 25 | high | Add `transform-get` (read-only); needed before almost any informed write |
| 2 | D1 | Default Value | 5 | 1 | 25 | high | All-zero default of `transform-move` silently teleports objects to origin |
| 3 | A2 | Ambiguity | 4 | 1 | 20 | high | Rotate's "adds the angles" wording is wrong — it's quaternion right-multiply |
| 4 | G3 | Capability Gap | 4 | 1 | 20 | high | No `transform-reset` macro; current 3-call workaround is itself dangerous |
| 5 | D2 | Default Value | 4 | 1 | 20 | high | All-zero default of `transform-rotate` silently snaps to identity rotation |
| 6 | G6 | Capability Gap | 4 | 2 | 16 | high | "World rotation" with `relative=true` actually rotates around local axes — semantic bug |
| 7 | A1 / D4 | Ambiguity / Default | 3 | 1 | 15 | high | `space` param: case-sensitive, no validation, silent fallback to world |
| 8 | G2 | Capability Gap | 3 | 1 | 15 | high | No Quaternion rotation path; Euler-only causes drift on round-trips |
| 9 | G4 | Capability Gap | 4 | 3 | 12 | high | No pivot/RotateAround support — common 3D authoring op |
| 10 | R1 | Redundancy | 3 | 4 | 6 | high | 3 near-identical schemas; consolidation viable but cost-of-LLM-ergonomics is real |
| 11 | G5 | Capability Gap | 2 | 2 | 8 | medium | No uniform-scale shortcut, no per-axis "leave unchanged" sentinel |
| 12 | A3 | Ambiguity | 2 | 1 | 10 | medium | Scale `relative` could use a worked example (1× = no-op, 2× = double) |
| 13 | D5 | Default Value | 2 | 1 | 10 | medium | "instanceId XOR objectPath" invariant is undocumented in description |
| 14 | A4 | Ambiguity | 3 | 2 | 12 | medium | No "use this when X, not Y" disambiguation vs. `gameobject-move-relative` / `gameobject-look-at` |
| 15 | G7 | Capability Gap | 3 | 4 | 6 | medium | No batch transform; each call is single-target with separate Undo entries |
| 16 | A5 | Ambiguity | 1 | 1 | 5 | low | Scale's lack of `space` is unexplained (correct, but unexplained) |
| 17 | A6 | Ambiguity | 1 | 1 | 5 | low | `Lossy Scale` printed in response without description of its meaning |
| 18 | G8 | Capability Gap (cross-domain) | 2 | 2 | 8 | high | Shared helper still uses deprecated `EditorUtility.InstanceIDToObject` |
| 19 | R2 | Redundancy (cross-domain) | 2 | 4 | 4 | medium | `gameobject-move-relative` / `gameobject-look-at` are conceptually transform writes |

Priority formula: `Impact × (6 − Effort)`. Top 3 findings are all very-low-effort, very-high-impact and would catch the bulk of LLM-failure cases. The expensive findings (R1, G4, G7) are deliberately ranked lower — they're worth doing but the planner should weigh cost.

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `FindGameObject` lives in `Tool_Transform.cs` but is consumed by ~30 tools across the codebase (Grep result). Treating this as the "Transform" domain's concern is misleading; it's effectively a shared helper. If the planner consolidates Transform, decide whether to (a) leave the helper here as is, (b) move it to `Editor/Tools/Helpers/` (per CLAUDE.md directory structure), or (c) introduce a `Tool_Common` partial. Out of scope for a Transform-only refactor but worth raising with Ramon.
- `gameobject-move-relative` and `gameobject-look-at` arguably belong in the Transform domain (they write `transform.position`/`transform.rotation`). Conversely, `gameobject-set-parent` is correctly in GameObject because it changes hierarchy, not transform values per se. A future cross-domain pass could rationalize this.

**Workflows intentionally deferred:**
- 2D-specific workflows (e.g. `transform-move-2d` that ignores Z) — would need product input; flagged via G5.
- Animation-style transform tweens / coroutined moves — outside the editor-tool scope (these are runtime concerns).

**Open questions for the reviewer:**
1. Is the `relative=true` rotation behavior in `transform-rotate` intentional (right-multiply, local-frame composition regardless of `space`), or a bug? Compare A2 and G6 — if it's intentional, the description must change; if it's a bug, the implementation must change.
2. Is consolidating Move/Rotate/Scale into one `transform` tool with an `op` discriminator desirable, or does Ramon prefer the explicit verb-per-tool model? The Animation domain's `ConfigureController` is cited in agent guidance as a *reference quality* example of dispatch — but Move/Rotate/Scale are arguably distinct enough that splitting is fine. Planner's call.
3. Is a dedicated `transform-get` (read-only) acceptable given `gameobject-get` already returns transform info? Argument for: focused output, includes lossy scale and local axes which `gameobject-get` omits. Argument against: surface bloat.
4. Should the deprecated-API warning suppression (G8) be addressed in this domain's refactor, or kept as a separate cross-cutting concern?

**Limits of this audit:**
- The audit did not run `dotnet build` to confirm there are no compiler warnings beyond `CS0618`. That belongs to the build-validator step.
- Behavioral correctness claims in A2 and G6 are based on reading the implementation, not on dynamic verification. The rotation composition behavior should be sanity-checked against a runtime test before being treated as a confirmed bug. Confidence is high that the described semantics are real, but this is a static analysis.
- Cross-domain redundancy claims (R2) are deliberately scoped as "informational" because a Transform-only audit cannot speak authoritatively to the GameObject domain's design intent.
