# Audit Report — Graphics

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Graphics/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via Glob `Editor/Tools/Graphics/Tool_Graphics.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** balanced — every file in the domain was read in full and incorporated into findings.

**Errors encountered during audit:** None.

**Files not analyzed:** None.

**Absence claims in this report:** Permitted (accounting balanced). Absence claims are scoped to the Graphics domain unless explicitly noted otherwise (cross-domain absence claims are spot-checked via Grep across `Editor/Tools/`).

**Reviewer guidance:**
- The Graphics domain mixes five fairly distinct sub-areas: lightmap baking, light/reflection probes, render-pipeline/quality settings, render statistics + Scene View debug, and SRP Volumes (post-processing). The redundancy clusters call out where these sub-areas could be consolidated; the capability gaps are mostly inside the Volume sub-area, which is the most user-visible piece (post-FX authoring).
- The Volume tools rely on reflection rather than referencing URP/HDRP types directly. This was a deliberate decision (graceful degrade when SRP is missing) — the auditor flags missing capabilities relative to the URP/HDRP API but does NOT recommend removing the reflection layer; consolidation-planner should treat that as a constraint.
- Several "no-op" sentinel encodings (`-1=unchanged`, `-999=unchanged`, `1/0/-1` for tri-state booleans) appear repeatedly. Unifying these is a low-effort, high-value cleanup.

---

## 1. Inventory

| # | Tool ID | Title | File | Params | ReadOnly |
|---|---------|-------|------|--------|----------|
| 1 | `graphics-bake-start` | Graphics / Bake Start | `Tool_Graphics.Baking.cs` | 1 | no |
| 2 | `graphics-bake-cancel` | Graphics / Bake Cancel | `Tool_Graphics.Baking.cs` | 0 | no |
| 3 | `graphics-bake-status` | Graphics / Bake Status | `Tool_Graphics.Baking.cs` | 0 | yes |
| 4 | `graphics-bake-clear` | Graphics / Bake Clear | `Tool_Graphics.Baking.cs` | 0 | no |
| 5 | `graphics-bake-reflection-probe` | Graphics / Bake Reflection Probe | `Tool_Graphics.Baking.cs` | 2 | no |
| 6 | `graphics-bake-get-settings` | Graphics / Bake Get Settings | `Tool_Graphics.Baking.cs` | 0 | yes |
| 7 | `graphics-bake-create-reflection-probe` | Graphics / Create Reflection Probe | `Tool_Graphics.Baking.cs` | 9 | no |
| 8 | `graphics-bake-create-light-probes` | Graphics / Create Light Probes | `Tool_Graphics.Baking.cs` | 8 | no |
| 9 | `graphics-bake-set-settings` | Graphics / Bake Set Settings | `Tool_Graphics.Baking.cs` | 3 | no |
| 10 | `graphics-bake-set-probe-positions` | Graphics / Set Probe Positions | `Tool_Graphics.Baking.cs` | 3 | no |
| 11 | `graphics-get-settings` | Graphics / Get Settings | `Tool_Graphics.GetSettings.cs` | 0 | no (should be yes) |
| 12 | `graphics-set-quality` | Graphics / Set Quality Level | `Tool_Graphics.SetQuality.cs` | 1 | no |
| 13 | `graphics-stats-get` | Graphics / Stats Get | `Tool_Graphics.Stats.cs` | 0 | yes |
| 14 | `graphics-stats-get-memory` | Graphics / Stats Memory | `Tool_Graphics.Stats.cs` | 0 | yes |
| 15 | `graphics-pipeline-get-info` | Graphics / Pipeline Info | `Tool_Graphics.Stats.cs` | 0 | yes |
| 16 | `graphics-stats-list-counters` | Graphics / List Counters | `Tool_Graphics.Stats.cs` | 0 | yes |
| 17 | `graphics-stats-set-debug` | Graphics / Set Debug Mode | `Tool_Graphics.Stats.cs` | 1 | no |
| 18 | `graphics-volume-create` | Graphics / Volume Create | `Tool_Graphics.Volume.cs` | 4 | no |
| 19 | `graphics-volume-get-info` | Graphics / Volume Get Info | `Tool_Graphics.Volume.cs` | 2 | yes |
| 20 | `graphics-volume-add-effect` | Graphics / Volume Add Effect | `Tool_Graphics.Volume.cs` | 3 | no |
| 21 | `graphics-volume-remove-effect` | Graphics / Volume Remove Effect | `Tool_Graphics.Volume.cs` | 3 | no |
| 22 | `graphics-volume-list-effects` | Graphics / Volume List Effects | `Tool_Graphics.Volume.cs` | 0 | yes |
| 23 | `graphics-volume-create-profile` | Graphics / Volume Create Profile | `Tool_Graphics.Volume.cs` | 1 | no |
| 24 | `graphics-volume-set-properties` | Graphics / Volume Set Properties | `Tool_Graphics.Volume.cs` | 5 | no |
| 25 | `graphics-volume-set-effect` | Graphics / Volume Set Effect | `Tool_Graphics.Volume.cs` | 5 | no |

**Total:** 25 tools across 5 files. 9 marked `ReadOnlyHint = true` (good coverage of inspection tools).

**Unity API surface used:**
- `UnityEditor.Lightmapping` (`Bake`, `BakeAsync`, `Cancel`, `Clear`, `BakeReflectionProbe`, `lightingSettings`, `isRunning`)
- `QualitySettings.*` (names, GetQualityLevel, SetQualityLevel, antiAliasing, shadows, shadowResolution, shadowDistance, lodBias, etc.)
- `UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline`
- `UnityEngine.SystemInfo` (graphicsDeviceName, graphicsMemorySize, etc.)
- `Unity.Profiling.ProfilerRecorder` (Render category counters)
- `UnityEditor.SceneView.GetBuiltinCameraMode` + `DrawCameraMode`
- `ReflectionProbe`, `LightProbeGroup` (direct types) for probe creation/baking
- `UnityEngine.Rendering.Volume`, `VolumeProfile`, `VolumeComponent` — accessed via runtime reflection (`Type.GetType` + `MethodInfo.Invoke`) so the assembly compiles without URP/HDRP

---

## 2. Redundancy Clusters

### Cluster R1 — Bake lifecycle as four discrete tools
**Members:** `graphics-bake-start`, `graphics-bake-cancel`, `graphics-bake-status`, `graphics-bake-clear`
**Overlap:** All four wrap a single `UnityEditor.Lightmapping.<verb>()` call with no parameters (or one boolean for start). They form a tight state machine the LLM has to dispatch across 4 tool IDs. A single `graphics-bake(action: 'start'|'cancel'|'status'|'clear', async: bool)` would cover the same ground with one entry point — see `Tool_Animation.ConfigureController.cs` for the in-codebase reference pattern.
**Impact:** Medium. Four near-identical tool surfaces inflate the tool catalogue and force the LLM to remember which verb is its own tool vs. a parameter.
**Confidence:** high

### Cluster R2 — Probe creation split by probe type
**Members:** `graphics-bake-create-reflection-probe`, `graphics-bake-create-light-probes`
**Overlap:** Both tools create a fresh GameObject, place it at a position, attach a single component, and `Undo.RegisterCreatedObjectUndo`. Param overlap: `name`, `posX`, `posY`, `posZ` (4 params shared). Action diverges only in component-specific knobs (resolution/hdr vs grid/spacing). A unified `graphics-create-probe(kind: 'reflection'|'light-group', ...)` would mirror how `Tool_Light.Create` handles spot vs directional vs point in one entry point.
**Impact:** Medium. The two are conceptually "create a probe in the scene" but require the LLM to pick the right tool by name; bundling under one tool with a `kind` param makes the workflow one-shot.
**Confidence:** high

### Cluster R3 — Volume property setters split into "container" vs "effect" vs "params"
**Members:** `graphics-volume-set-properties`, `graphics-volume-set-effect`, `graphics-volume-add-effect`, `graphics-volume-remove-effect`
**Overlap:** All four target the same Volume GameObject and operate on either the Volume component directly or on its profile's components list. Their parameter shapes share `instanceId`, `objectPath`, and most also share `effectType`. The boundary between "set properties on the Volume" and "set parameters on an effect inside the Volume's profile" is reasonable, but `add-effect` and `remove-effect` are obvious candidates for one `graphics-volume-modify-effect(action: add|remove|set, ...)`.
**Impact:** Low–Medium. The split is at least semantically defensible. Worth considering, not urgent.
**Confidence:** medium

### Cluster R4 — Pipeline/quality info scattered across three read-only tools
**Members:** `graphics-get-settings`, `graphics-pipeline-get-info`, `graphics-stats-list-counters` (partial overlap), `graphics-bake-get-settings`
**Overlap:** `graphics-get-settings` and `graphics-pipeline-get-info` BOTH report: render-pipeline name/type, color space, quality level, anti-aliasing, shadow distance, shadow resolution, available quality levels. `graphics-get-settings` is the strict superset — it adds VSync, anisotropic filtering, texture quality, LOD bias, max LOD level, pixel light count. The two tools answer the question "what are my graphics settings?" with substantial overlap.
**Impact:** Medium. When the LLM is asked "what's the render pipeline?" it has two valid choices that return slightly different (overlapping) reports. This is exactly the kind of LLM-facing ambiguity tool consolidation aims to remove.
**Confidence:** high

### Cluster R5 — Memory/stats split between `graphics-stats-*` and `profiler-*` (cross-domain — informational)
**Members (Graphics):** `graphics-stats-get`, `graphics-stats-get-memory`
**Cross-domain neighbours:** `profiler-get-counters`, `profiler-get-object-memory`, `profiler-frame-timing`
**Overlap:** Graphics' two stats tools use `ProfilerRecorder` from the Render category; Profiler domain uses `ProfilerRecorder` more generally. The user query "show me current rendering stats" is answerable from either domain. Not strictly redundant (Graphics is render-only), but the LLM has to know which domain to ask.
**Impact:** Low. Cross-domain consolidation is out of scope for this audit; flagging only.
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `graphics-bake-set-probe-positions` description does not mention LightProbeGroup
**Location:** `graphics-bake-set-probe-positions` — `Tool_Graphics.Baking.cs:282`
**Issue:** The method-level description ("Sets custom positions for a LightProbeGroup from a JSON array of [x,y,z] arrays.") is fine, BUT the file ID prefix (`graphics-bake-set-probe-positions`) is ambiguous between reflection probes and light probes. A user/LLM reading only the tool ID could reasonably think this controls reflection probe positions. The `Title` ("Graphics / Set Probe Positions") inherits the same ambiguity.
**Evidence:** Tool ID literal: `"graphics-bake-set-probe-positions"`. Title literal: `"Graphics / Set Probe Positions"`.
**Confidence:** medium

### A2 — `graphics-bake-create-reflection-probe` parameter descriptions are one-word stubs
**Location:** `graphics-bake-create-reflection-probe` — `Tool_Graphics.Baking.cs:152-162`
**Issue:** Every parameter description is a single noun: `"Name."`, `"X position."`, `"Y position."`, `"Z position."`, `"Box size X."`, `"Resolution."`, `"HDR."`. None explain units, sensible ranges, or what HDR controls (HDR cubemap output? hint to `Camera.allowHDR`?). For an LLM that has never authored a probe before, the `resolution` default of 128 is opaque (powers-of-2 expected? max value? cubemap face size or total texel count?).
**Evidence:** Lines 153–161 contain parameter descriptions of length 1–3 words each.
**Confidence:** high

### A3 — `graphics-bake-create-light-probes` `gridX/Y/Z` semantics undocumented
**Location:** `graphics-bake-create-light-probes` — `Tool_Graphics.Baking.cs:191-200`
**Issue:** Description says "Creates a Light Probe Group with a grid layout." The grid is laid out at `(x*spacing, y*spacing, z*spacing)` — i.e. positions are in LOCAL space relative to the GameObject's transform, anchored at (0,0,0) with no centering. This is non-obvious. Default `posY=0`, `gridY=2`, `spacing=2` produces a group whose Y range is [0, 2] — biased upward, not centred. Users will likely expect a centred grid.
**Evidence:** Lines 217 — `positions[idx++] = new Vector3(x * spacing, y * spacing, z * spacing);` — no centring offset.
**Confidence:** high

### A4 — `graphics-bake-set-settings` "leave unchanged" sentinel hidden in param description
**Location:** `graphics-bake-set-settings` — `Tool_Graphics.Baking.cs:240-242`
**Issue:** Parameters use `-1` as the "leave unchanged" sentinel. The convention is documented in each `[Description("...-1=unchanged.")]` but there's no discoverable reason for the LLM to expect this rather than, say, omitting the param. (The signature has no defaults — `int maxBounces = -1` IS the default, so the LLM can omit, but the description suggests it MUST pass `-1` explicitly.)
**Evidence:** Line 240: `[Description("Max bounces. -1=unchanged.")] int maxBounces = -1`.
**Confidence:** medium

### A5 — `graphics-volume-set-properties` tri-state `isGlobal` parameter is obscure
**Location:** `graphics-volume-set-properties` — `Tool_Graphics.Volume.cs:364`
**Issue:** `isGlobal` is encoded as `int` with `-1=unchanged, 0=false, 1=true`. The LLM is more likely to guess a bool than this convention. Worse, this is the ONLY parameter in the domain that uses tri-state int — every other "leave unchanged" sentinel is `-1` for numerics, no convention for bools.
**Evidence:** Line 364: `[Description("IsGlobal (-1=unchanged, 0=false, 1=true).")] int isGlobal = -1`.
**Confidence:** high

### A6 — `graphics-volume-set-effect` `parameterValue` accepts only float/int/bool but does not say so up front
**Location:** `graphics-volume-set-effect` — `Tool_Graphics.Volume.cs:436`
**Issue:** Description: `"Parameter value."` (two words). The implementation supports float, int, bool ONLY (lines 527–553). Color, Vector2/3/4, Texture references, enum-typed VolumeParameters, and curve parameters are silently rejected (the `parsed != null` check on line 554 means unsupported types are no-ops with NO error returned to the caller). The user gets a success message even when nothing was set.
**Evidence:** Line 554–557: `if (parsed != null) { valueProp.SetValue(paramObj, parsed); }`. Line 569: returns success regardless. Description on line 436: `[Description("Parameter value.")]`.
**Confidence:** high — this is both an ambiguity (description) and a correctness bug (silent success). See G1 below for the capability gap angle.

### A7 — `graphics-volume-add-effect` does not list the available effect names
**Location:** `graphics-volume-add-effect` — `Tool_Graphics.Volume.cs:139`
**Issue:** `effectType` accepts a string like `"Bloom"`. Valid values depend on the loaded SRP (URP vs HDRP). The description says only `"Effect type (e.g. 'Bloom').";` — no hint that `graphics-volume-list-effects` exists to enumerate them. Cross-tool discoverability is broken.
**Evidence:** Line 139.
**Confidence:** medium

### A8 — `graphics-stats-set-debug` only supports 3 of ~20 DrawCameraMode values
**Location:** `graphics-stats-set-debug` — `Tool_Graphics.Stats.cs:155-157`
**Issue:** Description says `"Sets the Scene View debug visualization mode (e.g. wireframe, overdraw)."`. The example mentions `overdraw`, but the implementation switch (lines 168–173) supports ONLY `Textured`, `Wireframe`, `TexturedWire`. Overdraw, ShadowCascades, Mipmaps, Lightmap, etc. are not wired up. The description thus misadvertises capability.
**Evidence:** Line 154 says "e.g. wireframe, overdraw"; lines 168–173 reject anything not matching the 3 cases (falls through to `_ => Textured`).
**Confidence:** high

### A9 — `graphics-get-settings` is read-only but not marked `ReadOnlyHint`
**Location:** `graphics-get-settings` — `Tool_Graphics.GetSettings.cs:21`
**Issue:** This tool only queries `GraphicsSettings` and `QualitySettings` and writes nothing. Other inspection tools in the domain are correctly marked `ReadOnlyHint = true` (e.g. `graphics-bake-get-settings`, `graphics-pipeline-get-info`). This one is the odd one out and should match.
**Evidence:** Line 21: `[McpTool("graphics-get-settings", Title = "Graphics / Get Settings")]` — no `ReadOnlyHint`.
**Confidence:** high

### A10 — Disambiguation missing between `graphics-get-settings` and `graphics-pipeline-get-info`
**Location:** Both tools.
**Issue:** Per Cluster R4, these tools heavily overlap. Neither description contains a "use this when X, not Y" clause. An LLM picking between them has to read both responses to decide.
**Evidence:** Compare line 22 vs `Tool_Graphics.Stats.cs:80`. Both describe a render-pipeline + quality summary.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `graphics-bake-set-probe-positions` has no default, but `instanceId=0` AND `objectPath=""` is the implicit default
**Location:** `graphics-bake-set-probe-positions` params `instanceId=0`, `objectPath=""`
**Issue:** Both targeting params default to a "missing" value. With both at default, `Tool_Transform.FindGameObject(0, "")` returns null and the tool errors out. This isn't broken per se (it's how every tool in the domain handles the dual-locator pattern), but the defaults make the signature look optional when in fact at least one of the two is required.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** Either make targeting truly required (no defaults) or add a description note: "exactly one of instanceId or objectPath is required". The same comment applies to all 9 tools in the domain that use this pattern (R5, A4, A5, etc).
**Confidence:** medium

### D2 — `graphics-bake-create-light-probes` defaults produce a 18-probe rectangular lump biased upward
**Location:** `graphics-bake-create-light-probes`, params `gridX=3, gridY=2, gridZ=3, spacing=2, posY=0`
**Issue:** Defaults give a 3×2×3 grid (18 probes) anchored at the GO origin, extending [0..4] on X, [0..2] on Y, [0..4] on Z. For most use cases the dev expects a centred volume around `pos`. See A3.
**Current:** `gridX=3, gridY=2, gridZ=3, spacing=2f`
**Suggested direction:** Either centre the grid (offset by `-(grid-1)*spacing*0.5`) or document explicitly that positions are anchored at the lower corner. The numeric defaults themselves are reasonable; the layout origin is the issue.
**Confidence:** high

### D3 — `graphics-volume-create-profile` default path collides on rerun
**Location:** `graphics-volume-create-profile` param `path = "Assets/VolumeProfile.asset"`
**Issue:** Default save path is generic. The tool itself uses `AssetDatabase.GenerateUniqueAssetPath` so it won't overwrite (it'll create `VolumeProfile 1.asset`, etc), but a default of `Assets/VolumeProfiles/VolumeProfile.asset` would be more consistent with typical Unity project layouts and the profile would land in a subfolder rather than the asset root.
**Current:** `string path = "Assets/VolumeProfile.asset"`
**Suggested direction:** Default to a subfolder like `Assets/Settings/VolumeProfiles/VolumeProfile.asset` (matching URP project template) OR make the path required so the LLM must specify it.
**Confidence:** low (this is a taste call, not a bug)

### D4 — `graphics-volume-set-properties` mixes `-1` and `-999` sentinels
**Location:** `graphics-volume-set-properties` params `weight=-1f, priority=-999, isGlobal=-1`
**Issue:** The "unchanged" sentinel is `-1f` for `weight`, `-999` for `priority`, `-1` for `isGlobal`. Reason: priority is a signed int and `-1` is a valid priority. But the inconsistency is itself a footgun. The LLM has to memorise which sentinel applies to which param.
**Current:** `float weight = -1f, int priority = -999, int isGlobal = -1`
**Suggested direction:** Use nullable types (`float? weight = null`, `int? priority = null`, `bool? isGlobal = null`) — null means "unchanged" universally and avoids the sentinel-clash problem entirely. Alternatively, an `update` action with separate `set-priority` / `set-weight` etc methods, but consolidation prefers fewer tools.
**Confidence:** high

### D5 — `graphics-bake-start` `asyncBake = true` default is reasonable but potentially surprising
**Location:** `graphics-bake-start` param `asyncBake = true`
**Issue:** Async is the right default for interactive use. BUT when an LLM-driven workflow asks "bake the lights and report the result", async returns immediately ("Async bake started.") and the tool gives no follow-up handle to poll. The LLM has no idea the bake is still running unless it hits `graphics-bake-status` itself. This is a workflow trap, not strictly a default issue — see G2.
**Current:** `bool asyncBake = true`
**Suggested direction:** Default is fine; description should mention "use `graphics-bake-status` to poll for completion" so the LLM knows the next step.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Volume effect parameters of color / vector / texture / enum types cannot be set
**Workflow:** Configure a Bloom effect on a Volume profile, including its colour tint and threshold curve. Or configure a ColorAdjustments effect with a tinted `colorFilter`. Standard URP/HDRP post-processing authoring.
**Current coverage:** `graphics-volume-set-effect` writes the value of `VolumeParameter.value` for `float`, `int`, `bool` only.
**Missing:** Every other VolumeParameter subtype: `ColorParameter` (Color), `Vector2Parameter`, `Vector3Parameter`, `Vector4Parameter`, `TextureParameter` (texture asset reference), enum-typed `VolumeParameter<T>`. There is also no support for `min`/`max` paired parameters or animation curves. The implementation silently no-ops for these types AND returns success — see A6.
**Evidence:** `Tool_Graphics.Volume.cs:527-553` — only `if (targetType == typeof(float) | int | bool)` branches exist. Line 554: `if (parsed != null) { valueProp.SetValue(paramObj, parsed); }` — no `else` branch and no error returned.
**Confidence:** high — confirmed via Grep for `ColorParameter`, `VolumeParameter`, `SetObjectReferenceCurve` across `Editor/Tools/Graphics/`: zero matches.

### G2 — No way to wait for / await an async lightmap bake from a single workflow
**Workflow:** "Bake the scene's lighting and tell me the result." A scripted workflow expects: kick off bake, block (or poll) until done, return final settings or error.
**Current coverage:** `graphics-bake-start` (kicks it off), `graphics-bake-status` (single-shot status query), `graphics-bake-cancel`.
**Missing:** No tool that polls/waits for completion. The LLM has to drive a polling loop manually, which is fragile (rate-limit, unclear exit condition). No tool surfaces `Lightmapping.bakeProgress` or `Lightmapping.bakeCompleted` event-derived state.
**Evidence:** `Tool_Graphics.Baking.cs:67` — `bool baking = Lightmapping.isRunning;` returns only a boolean snapshot, no progress percentage. No other status method in the file. `Lightmapping.BakeAsync()` on line 35 returns immediately with no handle.
**Confidence:** high

### G3 — No tool to switch the active Render Pipeline asset
**Workflow:** Switch the active SRP (e.g. swap from one URP asset to a higher-quality URP asset, or swap URP→HDRP, or set Built-in for a comparison).
**Current coverage:** `graphics-pipeline-get-info` and `graphics-get-settings` REPORT the current pipeline but cannot change it.
**Missing:** A tool that calls `GraphicsSettings.defaultRenderPipeline = X` and/or sets `QualitySettings.renderPipeline` per quality level. Both are settable via the Unity API.
**Evidence:** Grep for `defaultRenderPipeline` and `renderPipeline =` across `Editor/Tools/`: no matches in any domain. `Tool_Graphics.Stats.cs:85` and `Tool_Graphics.GetSettings.cs:30` only READ `GraphicsSettings.currentRenderPipeline`.
**Confidence:** high

### G4 — No tool to set individual quality settings (only pick a quality level by name)
**Workflow:** "Lower the shadow distance to 30 and disable VSync, but keep the rest of the Ultra preset."
**Current coverage:** `graphics-set-quality` switches the entire active level. `graphics-get-settings` reads individual fields.
**Missing:** A `graphics-set-quality-property(name, value)` (or per-property setter) that writes to `QualitySettings.shadowDistance`, `vSyncCount`, `antiAliasing`, `globalTextureMipmapLimit`, `lodBias`, `pixelLightCount`, etc. These are all mutable from C# but not exposed.
**Evidence:** `Tool_Graphics.SetQuality.cs:36` — `QualitySettings.SetQualityLevel(idx);` is the only mutation. No setters elsewhere in the domain.
**Confidence:** high

### G5 — Lightmap bake settings tool covers only 3 of ~20 settable fields
**Workflow:** Configure a bake before kicking it off — choose lightmapper backend (Progressive CPU vs GPU vs Bakery), bake direction mode, indirect resolution, sample count, denoiser, etc.
**Current coverage:** `graphics-bake-set-settings` accepts `maxBounces`, `lightmapResolution`, `lightmapPadding` only.
**Missing:** The `LightingSettings` asset has many more fields: `lightmapper` (enum), `directionalityMode`, `mixedBakeMode`, `directSampleCount`, `indirectSampleCount`, `environmentSampleCount`, `filterTypeDirect`, `filterTypeIndirect`, `denoiserType*`, `albedoBoost`, `indirectScale`, `compressLightmaps`, etc. None are exposed.
**Evidence:** `Tool_Graphics.Baking.cs:239-272` — only three fields written.
**Confidence:** high

### G6 — No tool to assign or swap a VolumeProfile asset on an existing Volume
**Workflow:** Create a VolumeProfile asset (`graphics-volume-create-profile`) and assign it to an existing Volume in the scene, replacing any inline profile.
**Current coverage:** `graphics-volume-create` creates a new Volume AND inline-creates an embedded profile. `graphics-volume-create-profile` creates a standalone profile asset. There is no "wire profile X to volume Y" step.
**Missing:** A tool that sets `volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path)`. This is the standard URP/HDRP authoring path (asset-based profiles are the recommended workflow).
**Evidence:** `Tool_Graphics.Volume.cs:51-56` — `volume.profile = ScriptableObject.CreateInstance(profileType);` is the only profile assignment. No `sharedProfile` setter anywhere; no LoadAssetAtPath usage in the file. Confirmed via Grep across the Volume file.
**Confidence:** high

### G7 — `graphics-bake-create-reflection-probe` does not let the caller choose probe TYPE (baked vs realtime vs custom)
**Workflow:** Place a realtime reflection probe for a moving water surface, or a custom (cubemap-asset-driven) probe for a baked-environment fallback.
**Current coverage:** `graphics-bake-create-reflection-probe` creates the GO and sets size/resolution/HDR but leaves `mode` at default (Baked).
**Missing:** The `ReflectionProbe.mode` property is settable (`ReflectionProbeMode.Baked | Realtime | Custom`) and affects whether the probe is included in lightmap bake at all. Not exposed.
**Evidence:** `Tool_Graphics.Baking.cs:152-177` — no `probe.mode` assignment. Default mode stays Baked.
**Confidence:** high

### G8 — No tool exposes Scene View overdraw / mipmap / shadow-cascade visualisation
**Workflow:** Diagnose overdraw or shadow-cascade overlap in the Scene View — a standard rendering-perf workflow.
**Current coverage:** `graphics-stats-set-debug` supports only `Textured | Wireframe | TexturedWire` — see A8.
**Missing:** The `DrawCameraMode` enum has many other values (`Overdraw`, `Mipmaps`, `RealtimeCharting`, `BakedCharting`, `BakedAlbedo`, `BakedEmissive`, `BakedLightmap`, `ShadowCascades`, `RenderPaths`, `LightOverlap`, etc.). None are reachable through the current tool.
**Evidence:** `Tool_Graphics.Stats.cs:168-173` — switch with 3 cases + default.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher is more urgent.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | `graphics-volume-set-effect` silently no-ops on Color/Vector/Texture params AND returns success — caller is misled |
| 2 | G6 | Capability Gap | 5 | 2 | 20 | high | No way to assign a VolumeProfile asset to an existing Volume — breaks the asset-based authoring workflow |
| 3 | G3 | Capability Gap | 4 | 2 | 16 | high | No tool to set the active Render Pipeline asset (read-only today) |
| 4 | A6 | Ambiguity / Correctness | 5 | 1 | 25 | high | `parameterValue` description is two words; silent-success on unsupported types — quick win to error explicitly |
| 5 | R1 | Redundancy | 3 | 2 | 12 | high | Bake lifecycle (`start/cancel/status/clear`) → consolidate to `graphics-bake(action: ...)` |
| 6 | A8 | Ambiguity / Correctness | 4 | 1 | 20 | high | `graphics-stats-set-debug` advertises "overdraw" but only supports 3 modes |
| 7 | G4 | Capability Gap | 3 | 2 | 12 | high | No per-property setter for QualitySettings (shadow distance, vsync, etc.) |
| 8 | A5 | Ambiguity | 3 | 1 | 15 | high | `isGlobal: int -1/0/1` should be a nullable bool |
| 9 | D4 | Defaults | 3 | 1 | 15 | high | Mixed `-1` / `-999` sentinels in `volume-set-properties` — replace with nullables |
| 10 | A9 | Ambiguity | 2 | 1 | 10 | high | `graphics-get-settings` missing `ReadOnlyHint = true` |
| 11 | G5 | Capability Gap | 3 | 3 | 9 | high | Bake settings exposes 3 of ~20 LightingSettings fields |
| 12 | G2 | Capability Gap | 3 | 2 | 12 | high | No bake-progress polling helper; LLM must orchestrate |
| 13 | R4 | Redundancy | 3 | 2 | 12 | high | `graphics-get-settings` and `graphics-pipeline-get-info` overlap heavily |
| 14 | A3 + D2 | Ambiguity + Defaults | 3 | 1 | 15 | high | Light probe grid is anchored at lower corner, not centred |
| 15 | R2 | Redundancy | 2 | 2 | 8 | high | Two probe-create tools could merge into `graphics-create-probe(kind: ...)` |
| 16 | G7 | Capability Gap | 2 | 1 | 10 | high | `graphics-bake-create-reflection-probe` does not let caller set `mode` |
| 17 | G8 | Capability Gap | 2 | 2 | 8 | high | Scene View debug-mode tool exposes 3 of ~20 modes |
| 18 | A2 | Ambiguity | 2 | 1 | 10 | high | Probe-creation parameter descriptions are one-word stubs |
| 19 | A1 | Ambiguity | 2 | 1 | 10 | medium | "Probe positions" tool ID ambiguous between reflection vs light probes |
| 20 | A10 | Ambiguity | 2 | 1 | 10 | high | `get-settings` vs `pipeline-get-info` missing "use this when X" disambiguation |

Top 5 (best ROI):
1. **A6 (Volume set-effect description + silent success bug)** — priority 25, trivial doc + error-return fix
2. **G6 (assign profile to volume)** — priority 20, single-method addition
3. **A8 (debug-mode misadvertised)** — priority 20, either widen the switch or fix the description
4. **G3 (set render pipeline)** — priority 16, single-method addition with high workflow value
5. **G1 (Volume Color/Vector/Texture support)** — priority 15, larger but unblocks real post-FX authoring

---

## 7. Notes

**Cross-domain dependencies noticed:**
- Every targeted Volume / probe tool calls `Tool_Transform.FindGameObject(instanceId, objectPath)` (helper in the Transform domain). This is consistent with the rest of the codebase but means consolidation must preserve the dual-locator convention.
- The probe sub-area (`bake-create-reflection-probe`, `bake-create-light-probes`) is a thin wrapper around `new GameObject + AddComponent`. It overlaps conceptually with `Tool_Light.Create` (which spawns Light GOs) and the broader `gameobject-*` / `component-add` tools. The audit does NOT recommend collapsing into the GameObject domain because the probe-specific knobs (resolution, HDR, grid) deserve a focused tool — but the boundary is worth Ramon's review.
- `graphics-stats-get-memory` and `profiler-get-object-memory` both report memory but at different scopes (system info vs per-object). Not redundant; just neighbours.

**Workflows intentionally deferred:**
- HDRP-specific workflows (HDRP Volume effects, HDRP-specific shadow / lighting toggles) are not separately enumerated — the Volume tooling handles them generically through reflection. Worth a separate audit pass if HDRP becomes a target.
- Rendering features tied to specific URP renderer features (e.g. SSAO, Screen Space Shadows) are surfaced via the Volume profile system, so they fall under G1 once Color/Texture param support lands.

**Open questions for Ramon:**
1. Is the reflection-based access to Volume / VolumeProfile / VolumeComponent intentional (graceful degrade when SRP is missing) or a historical artifact? If the package now declares URP/HDRP as a soft dependency, switching to direct typed access would shorten code substantially. The audit assumes reflection is intentional.
2. Should bake-progress polling become a generic pattern for other long-running operations (NavMesh, AI generation) rather than a one-off `graphics-bake-status-progress` tool?
3. The "audit absent of cross-domain ambiguity" caveat (R5): would a unified `stats` namespace make sense in v2.0, or is the current Graphics/Profiler split worth preserving?

**Limits of this audit:**
- The audit is static-only; no tool was actually executed to verify behaviour. Findings A6 (silent success) and G1 are inferred from the code path, not from runtime observation.
- "Unity API X is mutable" claims (e.g. `GraphicsSettings.defaultRenderPipeline` is settable) are based on documented Unity APIs. If a particular setter is editor-only or play-mode-only, that constraint should surface during planning, not now.
