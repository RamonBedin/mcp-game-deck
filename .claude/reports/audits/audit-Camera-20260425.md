# Audit Report — Camera

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Camera/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 19 (via Glob `Editor/Tools/Camera/Tool_Camera.*.cs`)
- `files_read`: 19
- `files_analyzed`: 19

**Balance:** balanced

**Errors encountered during audit:** None.

**Files not analyzed:** None.

**Absence claims in this report:**
Allowed (accounting balanced). Cross-domain absence claims (e.g. "no other domain creates Cinemachine cameras") are backed by `Grep` over `Editor/Tools` for `CinemachineCamera|CinemachineVirtualCamera`, returning matches only inside `Editor/Tools/Camera` and `Editor/Tools/Helpers/CinemachineHelper.cs`.

**Reviewer guidance:**
- Several tools rely on heavy reflection because the same code must support Cinemachine v2 and v3. That's structurally fine; the audit does not flag the reflection itself, only when reflection masks logic bugs (see G2, G4, B1).
- I flag **two real behavioural bugs** (B1 GetBrainStatus output truncation; B2 SetAim/SetBody dead `aimType`/`bodyType` parameters; B3 SetTarget clears Follow when caller only supplies LookAt). These are not just description issues — they affect runtime output. Treat as P0.
- Some "issues" are actually **dead parameters** that the description promises but the implementation never uses. I have flagged them as both ambiguity AND capability-gap findings because the right fix could go either way (delete the param or implement it).

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---|---|---|---|---|
| `camera-add-extension` | Camera / Add Extension | `Tool_Camera.AddExtension.cs` | 3 | no |
| `camera-align-to-view` | Camera / Align to Scene View | `Tool_Camera.AlignToView.cs` | 1 | no |
| `camera-configure` | Camera / Configure | `Tool_Camera.Configure.cs` | 9 | no |
| `camera-create` | Camera / Create | `Tool_Camera.Create.cs` | 12 | no |
| `camera-ensure-brain` | Camera / Ensure Brain | `Tool_Camera.EnsureBrain.cs` | 3 | no |
| `camera-force-camera` | Camera / Force Camera | `Tool_Camera.ForceCamera.cs` | 1 | no |
| `camera-get-brain-status` | Camera / Get Brain Status | `Tool_Camera.GetBrainStatus.cs` | 0 | **yes** |
| `camera-list` | Camera / List | `Tool_Camera.List.cs` | 0 | no (should be yes) |
| `camera-ping` | Camera / Ping | `Tool_Camera.Ping.cs` | 0 | **yes** |
| `camera-release-override` | Camera / Release Override | `Tool_Camera.ReleaseOverride.cs` | 0 | no |
| `camera-remove-extension` | Camera / Remove Extension | `Tool_Camera.RemoveExtension.cs` | 3 | no |
| `camera-screenshot-multiview` | Camera / Screenshot Multiview | `Tool_Camera.ScreenshotMultiview.cs` | 4 | no |
| `camera-set-aim` | Camera / Set Aim | `Tool_Camera.SetAim.cs` | 3 | no |
| `camera-set-blend` | Camera / Set Blend | `Tool_Camera.SetBlend.cs` | 2 | no |
| `camera-set-body` | Camera / Set Body | `Tool_Camera.SetBody.cs` | 3 | no |
| `camera-set-lens` | Camera / Set Lens | `Tool_Camera.SetLens.cs` | 5 | no |
| `camera-set-noise` | Camera / Set Noise | `Tool_Camera.SetNoise.cs` | 3 | no |
| `camera-set-priority` | Camera / Set Priority | `Tool_Camera.SetPriority.cs` | 2 | no |
| `camera-set-target` | Camera / Set Target | `Tool_Camera.SetTarget.cs` | 3 | no |

**Internal Unity API surface used:** `Camera`, `CameraClearFlags`, `Texture2D`, `RenderTexture`, `Undo.RecordObject`, `Undo.RegisterCreatedObjectUndo`, `Undo.AddComponent`, `Undo.DestroyObjectImmediate`, `EditorUtility.SetDirty`, `EditorUtility.EntityIdToObject`, `SceneView.lastActiveSceneView`, `AssetDatabase.ImportAsset`, `Object.FindObjectsByType`, `GameObject.Find`. Cinemachine APIs are accessed exclusively via reflection (`Type.GetType`, `GetProperty`/`GetField`, `Activator.CreateInstance`, `Enum.Parse`).

---

## 2. Redundancy Clusters

### Cluster R1 — Camera property-mutation tools fragmented across many files
**Members:** `camera-configure`, `camera-set-lens`
**Overlap:** `camera-set-lens` is a strict subset of `camera-configure` for Cinemachine + regular cameras combined. `camera-configure` already accepts `fieldOfView`, `nearClip`, `farClip`, `orthoSize`. The only thing `camera-set-lens` adds is reflection-based forwarding to a Cinemachine `Lens` struct. An LLM asked "set fov to 90" has two equally-plausible tools.
**Impact:** Medium-high — both tools are present, both partially overlap, neither description disambiguates. The LLM will guess.
**Confidence:** high

### Cluster R2 — Brain blend-style configuration duplicated
**Members:** `camera-ensure-brain`, `camera-set-blend`
**Overlap:** Both call `ApplyBlendDefinition(brain, "DefaultBlend"|"m_DefaultBlend", style, duration, sb)`. `camera-ensure-brain` is a superset (it also adds the Brain when missing). `camera-set-blend` is a strict subset that errors when the Brain doesn't exist. They are interchangeable in 95% of calls.
**Impact:** Medium — neither description tells the LLM "use ensure-brain when uncertain whether the Brain exists; use set-blend when you only want to update an existing one." Tool will sometimes call set-blend and fail; sometimes call ensure-brain when only blend update was needed (which mutates the wrong thing if the user doesn't want default style overwritten).
**Confidence:** high

### Cluster R3 — Force / Release override is a Priority shortcut
**Members:** `camera-force-camera`, `camera-release-override`, `camera-set-priority`
**Overlap:** `camera-force-camera` sets priority to 9999. `camera-release-override` resets all to 10. `camera-set-priority` sets a specific value. Force + Release is a *convention layer* on top of Priority. Not a hard redundancy but the boundary between them ("call set-priority(9999) vs force-camera") is undocumented.
**Impact:** Low-medium — descriptions for force/release do explain the relationship between the three. Mild ambiguity.
**Confidence:** medium

### Cluster R4 — Aim/Body/Noise share a single shape
**Members:** `camera-set-aim`, `camera-set-body`, `camera-set-noise`
**Overlap:** All three target a Cinemachine sub-component on one virtual camera and apply named property overrides. SetAim and SetBody share the EXACT same code shape (`GetCinemachineSubComponent` + `ApplyKeyValueProperties`). SetNoise is similar but with two pre-named float fields. A single `camera-configure-pipeline(stage: Aim|Body|Noise, properties: ...)` could replace all three. (See `Tool_Animation.ConfigureController.cs` line 47 for reference of an `action`-dispatched approach.)
**Impact:** Medium — the LLM's mental model "to configure a Cinemachine pipeline I have N tools" becomes "I have 1 tool with a stage param." But splitting also has some benefit (per-tool descriptions can be richer for each stage).
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `camera-list` not marked ReadOnlyHint
**Location:** `camera-list` — `Tool_Camera.List.cs` line 19
**Issue:** Tool is purely read (`return ToolResponse.Text(...)` listing properties; no Undo, no SetDirty) but `[McpTool]` lacks `ReadOnlyHint = true`. Compare with `camera-get-brain-status` (line 22) and `camera-ping` (line 21) which correctly mark themselves read-only.
**Evidence:** `[McpTool("camera-list", Title = "Camera / List")]` — no ReadOnlyHint. Body (lines 23-58) only reads `Camera.allCameras` and formats output.
**Confidence:** high

### A2 — `camera-set-aim` & `camera-set-body` use deceptive jargon `propertiesJson` for non-JSON input
**Location:** `camera-set-aim`, `camera-set-body` — params `propertiesJson`
**Issue:** Parameter is named `propertiesJson` but accepts a semicolon-separated `key=value;key=value` string, NOT JSON. The description does say "Semicolon-separated key=value pairs" but the LLM will see the param name and likely send actual JSON, which silently fails the split.
**Evidence:** `Tool_Camera.SetAim.cs` line 37: `[Description("Semicolon-separated key=value property overrides for the aim component. Example: 'SoftZoneWidth=0.8;SoftZoneHeight=0.8'. Empty to skip.")] string propertiesJson = ""`. Same pattern in SetBody line 39.
**Confidence:** high

### A3 — `camera-set-aim` example mentions properties that may not exist on v3 Composer
**Location:** `camera-set-aim` description and parameter
**Issue:** Example properties `SoftZoneWidth=0.8;SoftZoneHeight=0.8` are Cinemachine v2 property names. In v3 (`Unity.Cinemachine` namespace), the equivalent is on `CinemachineRotationComposer.Composition.ScreenPosition.x/y` or `DeadZone.Size`. The example will silently fail on v3 (per `ApplyKeyValueProperties` line 191-194: `"property/field not found"`).
**Evidence:** `Tool_Camera.SetAim.cs` line 36: aim type list `Composer, HardLookAt, POV, SameAsFollowTarget, GroupComposer` — these are also v2 names; v3 uses `CinemachineRotationComposer`, `CinemachineHardLookAt`, `CinemachinePanTilt`, `CinemachineSameAsFollowTarget`, `CinemachineGroupFraming`. No "use this for v2 / use that for v3" disambiguation.
**Confidence:** high

### A4 — `camera-set-body` example uses dotted path syntax that ApplyKeyValueProperties does not parse
**Location:** `camera-set-body` param `propertiesJson`
**Issue:** Example: `'FollowOffset.y=2;XDamping=0.5'`. `ApplyKeyValueProperties` (SetBody lines 135-196) splits on `=` and treats the left side as a single property/field name via `target.GetType().GetProperty(key, ...)`. There is NO support for dotted nested paths like `FollowOffset.y`. A user copying the example verbatim will hit `"property/field not found"`.
**Evidence:** SetBody.cs line 158: `var prop = target.GetType().GetProperty(key, ...)` — `key` here is `"FollowOffset.y"` literally. No split on `.`. No alternative resolution path.
**Confidence:** high

### A5 — `camera-configure` `orthographic` parameter typed as string instead of bool
**Location:** `camera-configure` param `orthographic`
**Issue:** Declared `string orthographic = ""`. Description: "Set to true for orthographic, false for perspective. Empty string to skip." The MCP framework supports nullable bool and tri-state ints elsewhere — using `string` is unusual and forces the LLM to pass `"true"` or `"false"` as text. Easy to get wrong (e.g. passing `True` would still work due to ToLowerInvariant, but `"1"`, `"yes"`, `"on"` would silently set to perspective).
**Evidence:** `Tool_Camera.Configure.cs` line 36; switch logic line 71: `resolvedCam.orthographic = orthographic.ToLowerInvariant() == "true";` — only literal `"true"` works to enable ortho.
**Confidence:** high

### A6 — `camera-configure` magic sentinels (-1, -9999) are scattered and inconsistent
**Location:** `camera-configure` params `fieldOfView`, `orthoSize`, `nearClip`, `farClip`, `depth`
**Issue:** Most params use `-1` to mean "skip", but `depth` uses `-9999`. Params test `> 0` (so depth = 0 cannot be applied even though that's a perfectly valid camera depth, and FOV/clip = 0 is the only way it would skip — but `-1 > 0` is also false, so all sentinels work; nevertheless 0 is unreachable). Description does mention the sentinel for each, but the inconsistency (-1 vs -9999) is a footgun.
**Evidence:** `Tool_Camera.Configure.cs` lines 35-43. Compare line 63 `if (fieldOfView > 0)` (so FOV = 0 is silently dropped) vs line 92 `if (depth > -9998f)` (different sentinel logic).
**Confidence:** high

### A7 — `camera-screenshot-multiview` description silent on prerequisites
**Location:** `camera-screenshot-multiview` — file/method description
**Issue:** Tool creates a temporary camera and writes a PNG to disk. Description says "saves them as a 3x2 PNG contact sheet" but does not mention: (a) that the output folder is created if missing, (b) that the file is written even if the focus object has no renderers (uses world origin + radius=5 fallback), (c) that AssetDatabase imports the file. None of these are dealbreakers but a planning LLM benefits from knowing.
**Evidence:** `Tool_Camera.ScreenshotMultiview.cs` line 31: `[Description("Renders 6 orthographic views of a target object (front/back/left/right/top/bird_eye) and saves them as a 3x2 PNG contact sheet. resolution controls individual tile size.")]`. Folder creation at line 81-84; renderer fallback at line 56-77.
**Confidence:** medium

### A8 — `camera-add-extension` / `camera-remove-extension` give no list of valid extensions
**Location:** `camera-add-extension` and `camera-remove-extension` — param `extensionType`
**Issue:** Description gives 2 examples (`CinemachineConfiner2D`, `CinemachineCollider`) but does not enumerate the valid set, and there are ~10 extension types per Cinemachine version. No hint that v3 may use different names. The LLM will guess type names that may not exist.
**Evidence:** `Tool_Camera.AddExtension.cs` line 33; `Tool_Camera.RemoveExtension.cs` line 33.
**Confidence:** medium

### A9 — `camera-set-aim` description claims `aimType` is a type identifier; code never uses it
**Location:** `camera-set-aim` — param `aimType`
**Issue:** Description: "Pass aimType to identify the algorithm". XML doc: "Empty to skip type change." Implementation (lines 50-66) never reads `aimType`. The parameter is dead. This is **both** an ambiguity (description lies) and a capability gap (the operation it promises doesn't exist) — see G2.
**Evidence:** `Tool_Camera.SetAim.cs` lines 36, 50-66. The variable `aimType` is parameter-only; grep shows zero references in the method body.
**Confidence:** high

### A10 — `camera-set-body` description claims `bodyType` switches algorithm; code never uses it
**Location:** `camera-set-body` — param `bodyType`
**Issue:** Same pattern as A9. Description: "Pass bodyType to switch algorithm." Implementation (lines 41-71) never reads `bodyType`. Parameter is dead.
**Evidence:** `Tool_Camera.SetBody.cs` lines 38, 41-71. Variable `bodyType` is parameter-only; no references in method body.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `camera-set-target` default behaviour clears Follow target when only LookAt is supplied
**Location:** `camera-set-target` params `followTarget`, `lookAtTarget`
**Issue:** Both default to `""`. The clearing logic (`else if (followTarget == "")`) fires whenever the param is the default empty string, which means calling `set-target(cameraName: X, lookAtTarget: Y)` will silently clear the camera's Follow target — likely not what the user wanted.
**Current:** `string followTarget = "", string lookAtTarget = ""`
**Suggested direction:** Use a distinct sentinel for "leave unchanged" vs "clear" (e.g. `"<unchanged>"` default, `""` to clear). Or, more cleanly, `null` default with explicit `"clear"` keyword. Either way, the default must distinguish "user did not pass" from "user passed empty to clear".
**Confidence:** high

### D2 — `camera-create` default position is `(0, 1, -10)` — fine for 3D, terrible for 2D
**Location:** `camera-create` params `posX`, `posY`, `posZ`
**Issue:** Defaults match the standard Unity 3D Main Camera. For 2D projects (Jurassic Survivors is 2D URP — see `CLAUDE.md`), the typical 2D camera spawns at `(0, 0, -10)` with `orthographic=true`. The defaults bias toward 3D. For 2D usage the LLM would need to remember to override `posY` AND `orthographic`.
**Current:** `posY = 1f, posZ = -10f, orthographic = false, orthoSize = 5f`
**Suggested direction:** Either keep current 3D defaults but add a `template = "3d" | "2d"` param that adjusts a coherent set of values, or document in the method description that defaults are 3D-tuned.
**Confidence:** medium

### D3 — `camera-configure` `clearFlags` empty-string default is reasonable but undocumented
**Location:** `camera-configure` param `clearFlags`
**Issue:** Default is `""` (skip). Valid values per code: `skybox`, `solid_color`/`solidcolor`, `depth_only`/`depthonly`/`depth`, `nothing`/`none`. Description enumerates only one variant (`skybox, solid_color, depth_only, nothing`) — the alternate spellings work but aren't documented, leading to inconsistent LLM output across runs.
**Current:** `string clearFlags = ""`
**Suggested direction:** Either drop the multi-spelling acceptance (canonical only) or document all accepted spellings.
**Confidence:** medium

### D4 — `camera-set-priority` default `priority = 10` is the system default but unhelpful
**Location:** `camera-set-priority` param `priority`
**Issue:** Default 10 is the Cinemachine default priority, so calling the tool with no value is a no-op (sets priority to its existing default). Not a bug, but the param is effectively required in practice. Caller almost always passes a non-default value.
**Current:** `int priority = 10`
**Suggested direction:** Make `priority` required (no default), or default to a value that signals intent (e.g. -1 + reject).
**Confidence:** low

### D5 — `camera-screenshot-multiview` `savePath` magic default may collide silently
**Location:** `camera-screenshot-multiview` param `savePath`
**Issue:** Default `"Assets/Screenshots/Multiview.png"` overwrites without warning on repeated calls. Description does not warn. Folder is auto-created (line 81-84) but the file is unconditionally overwritten (line 175 `File.WriteAllBytes`). Not strictly broken — but a planning LLM may call it twice and lose the first sheet.
**Current:** `string savePath = "Assets/Screenshots/Multiview.png"`
**Suggested direction:** Either document the overwrite behaviour, or default to a timestamped filename, or reject when the file already exists.
**Confidence:** low

---

## 5. Capability Gaps

### G1 — Cannot create a Cinemachine virtual camera
**Workflow:** A Unity dev sets up Cinemachine in a new scene: install the package (already there), create the Brain (`camera-ensure-brain` ✓), then create a virtual camera that follows the player.
**Current coverage:** `camera-ensure-brain` adds the Brain. `camera-set-target`, `camera-set-priority`, `camera-set-body`, `camera-set-aim`, `camera-set-noise`, `camera-add-extension`, `camera-set-lens` all assume a Cinemachine camera **already exists**. There is no `camera-create-cinemachine`.
**Missing:** A tool that creates a GameObject and attaches `CinemachineCamera` (v3) or `CinemachineVirtualCamera` (v2) via reflection, parallel to `Tool_Camera.AddExtension.cs`'s `ResolveCinemachineType` + `Undo.AddComponent` pattern. Optionally place at scene-view position (mirroring `camera-align-to-view`).
**Evidence:** Grep `Editor/Tools` for `CinemachineCamera|CinemachineVirtualCamera` returns matches only inside `Editor/Tools/Camera/*` and `Helpers/CinemachineHelper.cs`; none of those create one. Cross-check: `Editor/Tools/Component/Tool_Component.Add.cs` could *technically* attach one, but it would still leave the LLM to construct the GameObject + Undo + selection scaffolding manually. No first-class tool.
**Confidence:** high

### G2 — Cannot switch the Aim algorithm of a Cinemachine camera
**Workflow:** A dev wants to convert a Composer-aimed CM camera to a HardLookAt camera, or vice versa. This is a routine retargeting operation in Unity.
**Current coverage:** `camera-set-aim` accepts an `aimType` parameter that is documented as "Pass aimType to identify the algorithm" with valid values `Composer, HardLookAt, POV, SameAsFollowTarget, GroupComposer`.
**Missing:** The implementation **never reads `aimType`**. There is no code path that destroys the current Aim sub-component and adds a different one. The promised capability does not exist. To actually switch aim, the user would need to use `Component / Add` and `Component / Remove` against the Cinemachine pipeline GameObject child — which is a v2-specific concept; v3 uses a different pipeline architecture.
**Evidence:** `Tool_Camera.SetAim.cs` line 36 (parameter declaration), lines 39-69 (method body — `aimType` not referenced).
**Confidence:** high

### G3 — Cannot switch the Body algorithm of a Cinemachine camera
**Workflow:** Same as G2 but for the Body stage (Transposer ↔ FramingTransposer ↔ TrackedDolly etc.).
**Current coverage:** `camera-set-body` accepts a `bodyType` parameter described as "Pass bodyType to switch algorithm".
**Missing:** Same as G2 — `bodyType` is never read. Pipeline-stage type switching is not implemented. v3 architecture (Body components are siblings on the same GameObject, not children) makes this even more intricate; the existing `GetCinemachineSubComponent` only handles lookups, not destructive type swaps.
**Evidence:** `Tool_Camera.SetBody.cs` line 38 (parameter declaration), lines 41-71 (method body — `bodyType` not referenced).
**Confidence:** high

### G4 — `camera-get-brain-status` enumerates virtual cameras but never appends them to output
**Workflow:** Inspect the Cinemachine state of the scene, including a list of every virtual camera and its priority.
**Current coverage:** `camera-get-brain-status` claims to return "all virtual cameras in the scene".
**Missing:** The loop at `Tool_Camera.GetBrainStatus.cs` lines 91-106 iterates `allCams[]`, computes `pri` and `isEnabled`, and **discards both**. Nothing is appended to `sb`. The returned text says `"\nVirtual Cameras in scene (N):"` and then... nothing. This is a silent output bug, not a description bug.
**Evidence:** Lines 89-106:
```
sb.AppendLine($"\nVirtual Cameras in scene ({allCams.Length}):");
for (int i = 0; i < allCams.Length; i++)
{
    UnityEngine.Component? vc = allCams[i] as UnityEngine.Component;
    if (vc == null) { continue; }
    object? pri = GetProperty(vc, "Priority");
    pri ??= GetField(vc, "Priority");
    pri ??= GetField(vc, "m_Priority");
    var vcBehaviour = vc as Behaviour;
    bool isEnabled = vcBehaviour != null && vcBehaviour.enabled;
}
```
No `sb.AppendLine(...)` for `vc.gameObject.name`, `pri`, or `isEnabled` inside the loop.
**Confidence:** high

### G5 — No tool for setting the Cinemachine noise *profile asset*
**Workflow:** Add camera shake to a virtual camera. Standard Unity workflow: add `CinemachineBasicMultiChannelPerlin`, assign a `NoiseSettings` asset to the `m_NoiseProfile` field, then tune amplitude/frequency.
**Current coverage:** `camera-set-noise` adjusts AmplitudeGain and FrequencyGain. `camera-add-extension` can add the noise component (extensions, not pipeline components — but for v3 they may co-exist).
**Missing:** No tool assigns the actual `NoiseSettings` profile asset (e.g. `6D Wobble`, `Handheld Tele Mild` from the Cinemachine package samples). Without a profile assigned, AmplitudeGain/FrequencyGain do nothing. The error in SetNoise (`"Add a CinemachineBasicMultiChannelPerlin component and assign a noise profile first"`) acknowledges this prerequisite but provides no tool to satisfy it.
**Evidence:** `Tool_Camera.SetNoise.cs` line 45-46 (the error explicitly tells the user to assign a profile manually). Grep `NoiseSettings|m_NoiseProfile` over `Editor/Tools` returns no matches in the Camera domain (verified via reading all 19 files — no such reference). Could be done via `Component / Update` from another domain, but is not first-class.
**Confidence:** high

### G6 — `camera-add-extension` has no inverse for "list extensions on this camera"
**Workflow:** Inspect what extensions are attached to a virtual camera before adding more. Useful before calling `add-extension` (the tool already early-returns if the same ext is present, but doesn't tell you what IS present).
**Current coverage:** Extensions can be added (`camera-add-extension`) and removed (`camera-remove-extension`). `Component / List` from another domain can list components on a GameObject.
**Missing:** No `camera-list-extensions(cameraName)`. Forces the LLM to call `Component / List` and filter for type names starting with `Cinemachine`. Minor, but if other parts of the domain provide read-only inspectors (`get-brain-status`), the asymmetry is notable.
**Confidence:** medium (this is a soft gap — workaround exists in another domain)

---

## 6. Behavioural Bugs (separate category — these are correctness issues, not description issues)

### B1 — `camera-get-brain-status` truncates virtual-camera list (see G4)
Already documented as G4. Listed here separately to flag that it is a runtime correctness bug (output truncation), not just a documentation issue.

### B2 — `camera-set-aim` and `camera-set-body` have dead `aimType` / `bodyType` params (see G2, G3, A9, A10)
Same root cause as G2/G3. Listed here to emphasize that the surface-level fix (improve description) is a lie — the actual fix requires either implementing the capability or removing the parameter.

### B3 — `camera-set-target` default-empty clears Follow when only LookAt is provided (see D1)
Same as D1. Documented in section 4.

---

## 7. Priority Ranking

| # | ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|---|---|---|---|---|---|---|
| 1 | G4 / B1 | Behavioural Bug | 5 | 1 | 25 | high | `get-brain-status` loop computes per-camera info but never writes it to output |
| 2 | D1 / B3 | Default Value | 5 | 2 | 20 | high | `set-target` default `""` clears Follow on lookAt-only calls |
| 3 | G2 / A9 | Capability Gap | 4 | 4 | 8 | high | `set-aim` `aimType` param is dead; cannot switch Aim algorithm |
| 4 | G3 / A10 | Capability Gap | 4 | 4 | 8 | high | `set-body` `bodyType` param is dead; cannot switch Body algorithm |
| 5 | G1 | Capability Gap | 5 | 3 | 15 | high | No tool creates a Cinemachine virtual camera |
| 6 | A4 | Ambiguity | 4 | 1 | 20 | high | `set-body` example uses dotted path syntax that the parser does not handle |
| 7 | R1 | Redundancy | 3 | 2 | 12 | high | `camera-configure` and `camera-set-lens` overlap heavily on FOV/clip/orthoSize |
| 8 | A2 | Ambiguity | 4 | 1 | 20 | high | `propertiesJson` param name is misleading (semicolon kv, not JSON) |
| 9 | A1 | Ambiguity | 2 | 1 | 10 | high | `camera-list` not marked `ReadOnlyHint = true` |
| 10 | A3 | Ambiguity | 3 | 2 | 12 | high | Aim/Body type lists use Cinemachine v2 names with no v3 disambiguation |
| 11 | A6 | Ambiguity | 3 | 2 | 12 | high | `camera-configure` mixes -1 and -9999 sentinels inconsistently |
| 12 | G5 | Capability Gap | 3 | 3 | 9 | high | No tool assigns a NoiseSettings profile asset |
| 13 | A5 | Ambiguity | 2 | 2 | 8 | high | `camera-configure orthographic` typed as string instead of bool |
| 14 | R2 | Redundancy | 3 | 2 | 12 | high | `ensure-brain` and `set-blend` overlap; no disambiguation |
| 15 | A8 | Ambiguity | 3 | 1 | 15 | medium | `add-extension` / `remove-extension` enumerate only 2 examples of ~10 |
| 16 | R4 | Redundancy | 3 | 4 | 6 | medium | `set-aim`/`set-body`/`set-noise` could collapse to one stage-dispatched tool |
| 17 | D2 | Default | 2 | 3 | 6 | medium | `camera-create` defaults bias toward 3D; 2D users override 2 params |
| 18 | A7 | Ambiguity | 2 | 1 | 10 | medium | `screenshot-multiview` description silent on overwrite/folder-creation |
| 19 | G6 | Capability Gap | 2 | 2 | 8 | medium | No `camera-list-extensions`; must use cross-domain `Component/List` |
| 20 | R3 | Redundancy | 2 | 1 | 10 | medium | `force-camera`/`release-override` are priority-shortcut conventions |
| 21 | D3 | Default | 2 | 1 | 10 | medium | `clearFlags` accepts undocumented spelling variants |
| 22 | D5 | Default | 2 | 1 | 10 | low | `screenshot-multiview` overwrites silently on repeat calls |
| 23 | D4 | Default | 1 | 1 | 5 | low | `set-priority` default 10 is a no-op |

(Priority = Impact × (6 − Effort).)

**Top 5 to act on first:**
1. **G4/B1** — One-line fix in `GetBrainStatus.cs` to actually emit the per-camera info inside the loop.
2. **D1/B3** — Fix `set-target` so default `""` does not clear; introduce explicit "leave unchanged" sentinel.
3. **A4** — Either implement dotted-path resolution in `ApplyKeyValueProperties` or remove the misleading `FollowOffset.y=2` example.
4. **A2** — Rename `propertiesJson` to `propertiesKv` (or similar) across SetAim and SetBody to stop suggesting JSON.
5. **G1** — Add `camera-create-cinemachine` so the basic CM workflow doesn't require dropping into `Component/Add` reflection.

---

## 8. Notes

- **Cinemachine v2 vs v3 is a recurring theme.** Several tools' parameter examples and accepted enum values are v2-flavoured (Composer, HardLookAt, FramingTransposer, m_Lens, m_Priority). The reflection paths in Ping/EnsureBrain/SetBlend correctly try v3 first, but the user-facing descriptions don't communicate which version a given example targets. A consolidation effort should consider whether the Camera domain commits to v3-only (current Unity 6000.x default) and treats v2 as legacy fallback, or stays bilingual.
- **`Tool_Camera.SetAim.cs` and `SetBody.cs` both look like a half-finished refactor.** The XML docs and `[Description]` attributes describe a richer tool than the implementation provides (type switching). Either the implementation was reverted and the docs not updated, or the docs were aspirational. Either way, this is the kind of mismatch that wastes LLM effort.
- **Reflection helpers are duplicated and live in `Tool_Camera.Ping.cs`.** `GetProperty`, `SetProperty`, `GetField`, `SetField` are defined in `Ping.cs` but used across 7+ files. They are appropriately `private static` so they can be partial-class shared, but the choice to host them in `Ping.cs` (not `Tool_Camera.Reflection.cs` or similar) is surprising. Not a finding — just a structural observation for the planner.
- **`camera-screenshot-multiview` depends on `Tool_Transform.FindGameObject`.** That's a cross-domain coupling. It's already used in 27 files (per Grep) so it's a stable pattern, but new auditors should know.
- **Dead-code observation in `GetBrainStatus.cs` line 100-105:** `pri` and `isEnabled` are computed but unused. The compiler does not warn because there are no `unused variable` warnings when the value is read into `var`/`bool`. Static analyzer would catch this.
- **No tests visible in `Editor/Tools/Camera/`.** Build-validator step will rely on `dotnet build` only. If the team wants regression coverage for B1 in particular, manual testing is needed.
