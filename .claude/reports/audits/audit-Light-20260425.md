# Audit Report — Light

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Light/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 3 (via Glob `Editor/Tools/Light/Tool_Light.*.cs`)
- `files_read`: 3
- `files_analyzed`: 3

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Absence claims are permitted (accounting balanced). They are still scoped: claims of the form "no tool exists for X" are limited to the Light domain and confirmed via cross-domain Grep where noted. Cross-domain absence (e.g., URP 2D Light tooling) was verified by Grep over the entire `Editor/Tools/` tree.

**Cross-domain reads performed (informational only, not part of `files_analyzed`):**
- `Editor/Tools/Transform/Tool_Transform.cs` — to confirm `FindGameObject` helper signature
- `Editor/Tools/Graphics/Tool_Graphics.Baking.cs` (first 80 lines) — to confirm baking lives in Graphics domain, not Light
- `Editor/Tools/Component/Tool_Component.Update.cs` (first 60 lines) — to confirm the generic component-update fallback applies to Light

**Reviewer guidance:**
- This is a small domain (3 tools, all clean). Findings are mostly capability-gap and minor signature concerns. Do not expect heavy redundancy here.
- The most important findings are G1 (no rotation parameter on Create — directional/spot lights are essentially unusable without it), G2 (no URP 2D Light support, which is critical given the test project Jurassic Survivors is a 2D URP game per `CLAUDE.md`), and D1 (sentinel-based "skip" defaults on Configure).

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `light-create` | Light / Create | `Tool_Light.Create.cs` | 7 | no |
| `light-configure` | Light / Configure | `Tool_Light.Configure.cs` | 7 | no |
| `light-list` | Light / List | `Tool_Light.List.cs` | 0 | yes |

**Internal Unity API surface used:**
- `UnityEngine.Light` (type, intensity, color, range, spotAngle, shadows, enabled)
- `UnityEngine.LightType` (Directional, Point, Spot, Rectangle)
- `UnityEngine.LightShadows` (None, Hard, Soft)
- `UnityEditor.Undo.RecordObject`, `Undo.RegisterCreatedObjectUndo`
- `UnityEditor.Selection.activeGameObject`
- `UnityEditor.EditorUtility.SetDirty`
- `UnityEngine.SceneManagement.SceneManager.GetActiveScene`
- `UnityEngine.ColorUtility.TryParseHtmlString`
- Helper: `Tool_Transform.FindGameObject`

**Parameter signatures (full):**

`light-create(string lightType="Point", string name="", float posX=0, float posY=0, float posZ=0, float intensity=1, string color="#FFFFFF")`

`light-configure(int instanceId=0, string objectPath="", float intensity=-1, string color="", float range=-1, float spotAngle=-1, string shadows="")`

`light-list()`

---

## 2. Redundancy Clusters

No redundancy clusters identified. The three tools have clearly distinct responsibilities:
- Create constructs a new GameObject + Light
- Configure mutates an existing Light
- List enumerates lights in the active scene

There is no overlap between Create and Configure beyond the shared `intensity`/`color` semantics, and that overlap is appropriate (Create needs initial values; Configure needs deltas). Splitting them is the right call.

One soft observation (not a finding): `light-create` always **adds a Light to a brand-new GameObject**. There is no path to add a Light to an existing GameObject through the Light domain — that requires the cross-domain `component-add` tool. This is not redundancy, but it is worth a "use this when X" disambiguation note in the description (see A2).

---

## 3. Ambiguity Findings

### A1 — `shadows` parameter doesn't enumerate values in Configure's main description block clearly
**Location:** `light-configure` — `Tool_Light.Configure.cs`
**Issue:** The method-level description does enumerate values (`'None'/'Hard'/'Soft'`), but the per-param `[Description]` for `shadows` says "Shadow mode: 'None', 'Hard', or 'Soft'. Empty to leave unchanged." which is fine. The actual ambiguity: Unity's `LightShadows` enum has only those three values, but URP/HDRP introduce additional shadow concepts (cascade count, soft shadow filter) that are NOT exposed here. An LLM reading the description has no way to know "soft shadow type" or "shadow strength" are unsupported.
**Evidence:** `Tool_Light.Configure.cs:41` — `[Description("Shadow mode: 'None', 'Hard', or 'Soft'. Empty to leave unchanged.")]`
**Confidence:** medium

### A2 — `light-create` description doesn't disambiguate vs. `component-add`
**Location:** `light-create` — `Tool_Light.Create.cs:38`
**Issue:** Description doesn't mention that this tool **always creates a new GameObject**. An LLM trying to "add a Light to my existing 'Sun' GameObject" might pick `light-create` and unintentionally make a duplicate. There is no "use this when starting fresh; use `component-add` to attach to an existing GameObject" guidance.
**Evidence:** `Tool_Light.Create.cs:38` — `[Description("Creates a new Light GameObject in the scene. Supports Directional, Point, Spot, and Area light types. Color is specified as a hex string (e.g. '#FFFFFF'). The operation is registered with Undo.")]`
**Confidence:** high

### A3 — `lightType="Area"` silently maps to `LightType.Rectangle`
**Location:** `light-create` — `Tool_Light.Create.cs:67-69`
**Issue:** The user-facing string `"Area"` is mapped to Unity's enum value `LightType.Rectangle`. Unity also has `LightType.Disc` (disc area light), which is not reachable. The description and param doc both say "Area" without clarifying it means Rectangle specifically. An LLM asking for a "disc area light" cannot get one.
**Evidence:** `Tool_Light.Create.cs:67-69`:
```csharp
case "area":
    unityLightType = LightType.Rectangle;
    break;
```
And `Tool_Light.Create.cs:40`: `[Description("Light type: 'Directional', 'Point', 'Spot', or 'Area'. Default 'Point'.")]`
**Confidence:** high

### A4 — `light-list` description doesn't mention rotation is omitted
**Location:** `light-list` — `Tool_Light.List.cs:26`
**Issue:** The List output includes position-relevant data (range, spotAngle) but not rotation. Directional and Spot lights are direction-driven; their rotation is a primary property. An LLM reading the description ("Returns name, instanceId, type, intensity, color, enabled, range, spotAngle, and shadow mode") may wrongly conclude rotation isn't needed.
**Evidence:** `Tool_Light.List.cs:26` — description; `Tool_Light.List.cs:53-69` — output builder, no rotation written.
**Confidence:** medium (this could equally be a Capability Gap — see G3)

### A5 — `range = 0` warning is logged but value is still applied
**Location:** `light-configure` — `Tool_Light.Configure.cs:94-103`
**Issue:** When `range=0` is supplied for a Point/Spot light, the tool prints `[WARNING] range = 0 is invalid for Point/Spot lights.` but **still writes 0 to `light.range`**. From the LLM/caller's perspective this is ambiguous — was the operation an error or a success? The success path also returns `ToolResponse.Text` which usually signals success.
**Evidence:** `Tool_Light.Configure.cs:94-103`:
```csharp
if (range >= 0f)
{
    if (range == 0f)
    {
        sb.AppendLine("  [WARNING] range = 0 is invalid for Point/Spot lights.");
    }
    light.range = range;
    sb.AppendLine($"  Range: {range}");
}
```
**Confidence:** high

---

## 4. Default Value Issues

### D1 — Sentinel-based "skip" defaults on `light-configure`
**Location:** `light-configure` — all numeric/string params on `Tool_Light.Configure.cs:34-42`
**Issue:** The tool uses `-1` for "skip" on numeric params and `""` for "skip" on strings. This is documented in descriptions, but it's a magic sentinel pattern that:
1. Disallows legitimate zero values for `intensity` (intensity=0 is valid — turns the light off without disabling the component). The check `intensity >= 0f` on line 77 means `intensity=0` IS applied — but the param doc says "-1 to leave unchanged" without clarifying that `0` means "set to zero", not "skip". An LLM might assume any default-ish value is skipped.
2. Disallows valid zero `range`. The code permits `range=0` but warns (see A5).
3. The pattern is fragile: a future Unity API change (e.g., negative intensity for HDR-fallback) would break it.
**Current:** `float intensity = -1f, string color = "", float range = -1f, float spotAngle = -1f, string shadows = ""`
**Suggested direction:** Document the sentinel convention more precisely in each param description (e.g., "intensity=0 sets light to zero brightness; pass any negative value to leave unchanged"), or migrate to nullable types if the framework supports them. No code change suggested — flagging the pattern for the planner.
**Confidence:** high

### D2 — `light-create` has no `rotation` parameter (default-value tied to capability gap)
**Location:** `light-create` — `Tool_Light.Create.cs:39-47`
**Issue:** All position params exist with default 0. There is no `rotX/rotY/rotZ` parameter at all. For a Directional light, the default rotation `(0,0,0)` points straight down -Z, which is **never what a user wants** (typical "sun" lights point at angle 50,-30,0 or similar). The most-common-case default would be a sensible directional light angle, but there's no slot for it.
**Current:** No rotation params; default rotation is identity.
**Suggested direction:** Add rotation parameters with sensible defaults for directional (e.g., 50,-30,0). This bleeds into G1 (capability gap).
**Confidence:** high

### D3 — `light-create` color default duplicated in two formats
**Location:** `light-create` — `Tool_Light.Create.cs:46`
**Issue:** Description says "Default '#FFFFFF'" but `TryParseHexColor` accepts both `#FFFFFF` and `FFFFFF`. The default is fine; the issue is the parameter description shows two example formats `'#FFFFFF', 'FF8800'` which mildly contradict the "Default '#FFFFFF'" line just above. Cosmetic, low impact.
**Current:** `string color = "#FFFFFF"`
**Suggested direction:** Pick one canonical example format in the parameter description.
**Confidence:** low

---

## 5. Capability Gaps

### G1 — Directional/Spot light orientation cannot be set during creation
**Workflow:** Create a directional "sun" light at a specific angle (e.g., rotation 50°, -30°, 0°), or a spot light pointing at a target.
**Current coverage:** `light-create` accepts position only (`posX, posY, posZ`). After creation, rotation must be set via the cross-domain `transform-rotate` tool (in `Editor/Tools/Transform/Tool_Transform.Rotate.cs`).
**Missing:** No rotation parameter on `light-create`. For directional and spot lights — which are the lights whose orientation matters most — this forces a 2-step LLM workflow (create then rotate) for the most common usage. A user asking "create a sun light pointing down at 45°" cannot be served in one tool call.
**Evidence:** `Tool_Light.Create.cs:39-47` — signature has `posX/posY/posZ` but no `rotX/rotY/rotZ`. Line 83: `go.transform.position = new Vector3(posX, posY, posZ);` — only position is set; rotation is left at identity.
**Confidence:** high

### G2 — No support for URP 2D Lights (`Light2D`)
**Workflow:** Add a 2D point light, freeform light, sprite light, or global light to a 2D URP scene (the test project Jurassic Survivors is exactly this case per `CLAUDE.md`).
**Current coverage:** None. `light-create` constructs `UnityEngine.Light` (3D component). `light-list` only enumerates `UnityEngine.Light` components.
**Missing:** No tool wraps `UnityEngine.Rendering.Universal.Light2D`. Required APIs include `Light2D.lightType`, `Light2D.color`, `Light2D.intensity`, `Light2D.pointLightInnerRadius`, `Light2D.pointLightOuterRadius`, `Light2D.shapePath` (freeform), and the `Light2D` component must be added through `AddComponent`. Cross-domain Grep across `Editor/Tools/` for `Light2D|UniversalRenderPipeline\.Light` returns **zero matches** — this functionality is wholly absent from the codebase.
**Evidence:** Grep `Light2D|Light 2D|UniversalRenderPipeline|Universal\.Light` over `Editor/Tools/` returned 0 matches. `Tool_Light.Create.cs` and `Tool_Light.List.cs` reference only `UnityEngine.Light`.
**Workaround possible:** `component-add` (`Editor/Tools/Component/Tool_Component.Add.cs`) plus `component-update` (`Tool_Component.Update.cs`) could be used by an LLM if `Light2D` resolves via type name lookup, but `Light2D` properties like `shapePath` (`Vector3[]`) are not part of `component-update`'s supported types (it supports float/int/bool/string only — see `Tool_Component.Update.cs:36`).
**Confidence:** high

### G3 — `light-list` omits rotation, position, and parent path
**Workflow:** Inspect lights in the scene to understand directional orientation, world position, and hierarchy parent.
**Current coverage:** `light-list` returns name, instanceId, type, intensity, color, enabled state, shadows, range (non-Directional), and spotAngle (Spot). No transform info.
**Missing:** Rotation is critical for Directional and Spot lights (they're direction-only / direction-cone). Position is needed for Point and Spot lights. Hierarchy path (parent) is needed when multiple lights share the same name. Without these, an LLM cannot tell where a light points or where it lives in the scene without a follow-up `transform-` call per light.
**Evidence:** `Tool_Light.List.cs:53-69` — output builder only writes the listed fields; no `transform.position` or `transform.eulerAngles` access.
**Confidence:** high

### G4 — No tool to delete a Light component (or its GameObject)
**Workflow:** Remove a misconfigured light from the scene.
**Current coverage:** Cross-domain only. `component-remove` (`Editor/Tools/Component/Tool_Component.Remove.cs`) can strip a Light component from a GameObject, and `gameobject-delete` (likely in GameObject domain) can destroy the GameObject.
**Missing:** No `light-delete` convenience tool. This is acceptable (deletion is generic), but it means the Light domain is a partial CRUD surface — Create, Read (List), Update (Configure), but no Delete. The disambiguation in A2 should mention this so the LLM knows to fall back to `component-remove`.
**Evidence:** No `Tool_Light.Delete.cs` or similar in the directory listing from Phase 0.
**Confidence:** medium (this is design-by-omission rather than a hard gap; flagging it for reviewer discretion)

### G5 — No access to advanced Light properties beyond the basics
**Workflow:** Configure realtime/baked mode, color temperature, light cookies, bounce intensity, indirect multiplier, culling mask, render mode, soft shadow type, shadow strength, shadow bias, shadow normal bias, shadow near plane, lightmap bake type.
**Current coverage:** `light-configure` exposes intensity, color, range, spotAngle, shadows mode (None/Hard/Soft). That's it.
**Missing:** All of the following Unity `Light` properties are unreachable through this domain: `Light.colorTemperature`, `Light.useColorTemperature`, `Light.cookie`, `Light.cookieSize`, `Light.flare`, `Light.bounceIntensity`, `Light.lightmapBakeType`, `Light.cullingMask`, `Light.renderMode`, `Light.shadowStrength`, `Light.shadowBias`, `Light.shadowNormalBias`, `Light.shadowNearPlane`, `Light.shadowResolution`, `Light.innerSpotAngle`, `Light.areaSize`. Most (not all) are float/int/bool/string and *would* be reachable via `component-update`, so this is a partial gap rather than a blocker.
**Evidence:** `Tool_Light.Configure.cs:34-42` — full param list; the API surface stops at the 5 properties named.
**Confidence:** high

### G6 — Light cannot be created as a child of an existing GameObject
**Workflow:** Create a "headlight" spot light parented to a Player GameObject.
**Current coverage:** `light-create` always creates a root-level GameObject (`new GameObject(goName)` with no parent argument). To parent it, a follow-up `transform-` or hierarchy tool call is required.
**Missing:** No `parentInstanceId` or `parentPath` parameter on `light-create`. The 2-step "create then re-parent" workflow is fragile — between steps the new light affects the scene at world origin.
**Evidence:** `Tool_Light.Create.cs:82` — `var go = new GameObject(goName);` — no parent argument; line 83 sets world position.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher = more valuable to fix first.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 2 | 20 | high | Add rotation params to `light-create`; directional/spot are unusable without orientation |
| 2 | G2 | Capability Gap | 5 | 4 | 10 | high | URP 2D `Light2D` not wrapped at all; required for the test project (2D URP) |
| 3 | G3 | Capability Gap | 4 | 2 | 16 | high | `light-list` should include rotation + position + hierarchy path |
| 4 | G6 | Capability Gap | 4 | 2 | 16 | high | `light-create` cannot parent the new light to an existing GameObject |
| 5 | A2 | Ambiguity | 3 | 1 | 15 | high | `light-create` description doesn't disambiguate vs. `component-add`; can cause duplicates |
| 6 | G5 | Capability Gap | 3 | 3 | 9 | high | Many `Light` properties (cookie, colorTemperature, bias, bounceIntensity, etc.) unreachable except via generic `component-update` |
| 7 | A5 | Ambiguity | 3 | 1 | 15 | high | `range=0` is warned but still applied; ambiguous success/error semantics |
| 8 | A3 | Ambiguity | 3 | 1 | 15 | high | `"Area"` silently means Rectangle; `LightType.Disc` unreachable |
| 9 | D1 | Default Value | 3 | 2 | 12 | high | Sentinel-based skip pattern needs clearer per-param documentation |
| 10 | A4 | Ambiguity | 2 | 1 | 10 | medium | `light-list` description doesn't admit rotation is omitted |
| 11 | A1 | Ambiguity | 2 | 1 | 10 | medium | URP/HDRP-only shadow concepts unreachable; not stated |
| 12 | G4 | Capability Gap | 2 | 1 | 10 | medium | No `light-delete`; falls back to `component-remove` (acceptable, just document it) |
| 13 | D3 | Default Value | 1 | 1 | 5 | low | Cosmetic: color example format inconsistent with stated default |

**Top 3 actionable wins for the planner:**
- **G1 + D2 (rotation on create)** — small surface area change with high impact for directional lights.
- **G3 (list richer transform info)** — single-tool change, no new APIs.
- **A2 + A3 + A5 (description disambiguation)** — pure description tweaks, lowest effort, real LLM-misuse impact.

The G2 (URP 2D Light) finding is the highest impact for *the test project specifically*, but is also the largest piece of work — needs new tools, not changes to existing ones. Plan it as a separate epic.

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `Tool_Light.Configure.cs` uses `Tool_Transform.FindGameObject` — a healthy dependency, the helper is reused across domains.
- Lightmap baking lives in the Graphics domain (`Tool_Graphics.Baking.cs`), not Light. This is the correct partitioning (baking is a scene-level operation, not per-light), but the Light domain's lack of any cross-reference to baking tools may confuse an LLM that asks "how do I bake my lights" and stays inside the Light domain.

**Workflows intentionally deferred:**
- Light Probe and Reflection Probe management appears in the Graphics domain (per Grep on `LightProbe|ReflectionProbe|RenderSettings` in Phase 0 cross-domain check). Out of scope for Light audit.
- Ambient lighting / skybox / environment lighting is a `RenderSettings` concern — also Graphics domain. Out of scope.

**Open questions for the reviewer:**
1. Does Ramon want `Light2D` (URP 2D) to live as `Tool_Light.*` (e.g., `light-create-2d`) or as a separate `Light2D` domain? This is a planner decision but worth flagging now.
2. Is it acceptable to extend `light-create` with rotation + parent params and grow it past 9 params, or should there be a `light-create-advanced` variant? (My read: extend; 9 params is fine for this domain.)
3. For G5, is "use `component-update` for advanced properties" the long-term answer, or should `light-configure` grow more first-class params? Trade-off: explicit params = better LLM ergonomics; generic update = lower maintenance surface.

**Honest limits of this audit:**
- I did not run `dotnet build`, so any compile-level issues are not caught here.
- I did not check whether `Light2D` is even available in this Unity project's package manifest. If URP 2D isn't installed, G2 may be a non-issue for v1.x and only relevant when the project takes a URP-2D dependency.
- The "common case" claim in D2 (directional default rotation should be 50,-30,0) is opinion-driven, drawn from the typical Unity Sun preset — Ramon may have a different convention in mind.
