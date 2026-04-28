# Audit Report — Screenshot

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Screenshot/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 3 (via Glob `Editor/Tools/Screenshot/Tool_Screenshot.*.cs`)
- `files_read`: 3
- `files_analyzed`: 3

**Balance:** balanced

**Errors encountered during audit:**
- None

**Files not analyzed (if any):**
- None

**Absence claims in this report:**
- All absence claims are made over the full domain (3/3 files). Cross-domain absence claims (e.g. "no return-as-base64 option in `screenshot-game-view`") were verified by reading every Screenshot tool file. One adjacent tool was also read for context: `Editor/Tools/Camera/Tool_Camera.ScreenshotMultiview.cs`.

**Reviewer guidance:**
- The Screenshot domain is small (3 tools, ~340 LOC total) and behaviorally close to a single conceptual action ("render something to PNG"). Inter-tool overlap is the dominant theme.
- A near-twin tool (`camera-screenshot-multiview`) lives in the **Camera** domain. It is genuinely different (multi-angle contact sheet, auto-radius from bounds), but it overlaps in user intent ("take a picture of this object"). Worth flagging for the consolidation-planner even though it is outside this domain's directory.
- The domain has zero `ReadOnlyHint` tools, but that's correct — every tool here writes a PNG to disk by default. Not a finding.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `screenshot-camera` | Screenshot / Camera | `Tool_Screenshot.Camera.cs` | 6 | no |
| `screenshot-game-view` | Screenshot / Game View | `Tool_Screenshot.GameView.cs` | 3 | no |
| `screenshot-scene-view` | Screenshot / Scene View | `Tool_Screenshot.SceneView.cs` | 1 | no |

**Parameter detail:**

- `screenshot-camera(cameraName="", instanceId=0, width=1920, height=1080, savePath="Assets/Screenshots/Camera.png", returnBase64=false)`
- `screenshot-game-view(savePath="Assets/Screenshots/GameView.png", width=1920, height=1080)`
- `screenshot-scene-view(savePath="Assets/Screenshots/SceneView.png")`

**Internal Unity API surface used:**
- `Camera.allCameras`, `Camera.main`, `Camera.targetTexture`, `Camera.Render()`
- `RenderTexture`, `Texture2D.ReadPixels`, `Texture2D.EncodeToPNG`
- `SceneView.lastActiveSceneView`, `SceneView.camera`, `SceneView.position`
- `EditorUtility.EntityIdToObject`, `AssetDatabase.ImportAsset`
- `File.WriteAllBytes`, `Directory.CreateDirectory`, `Path.GetDirectoryName`

**Common helper APIs intentionally not used here but used in adjacent domain:**
- `Tool_Transform.FindGameObject(instanceId, objectPath)` — used by `camera-screenshot-multiview` in the Camera domain to resolve a target by `(instanceId | objectPath)`. The Screenshot domain's `screenshot-camera` resolves by `(cameraName | instanceId)` instead, which is inconsistent with the project's prevailing identifier pattern (`instanceId` + `objectPath`).

---

## 2. Redundancy Clusters

### Cluster R1 — All three tools are conceptually one "capture-to-PNG" action
**Members:** `screenshot-camera`, `screenshot-game-view`, `screenshot-scene-view`
**Overlap:** All three tools execute the same core sequence: (1) pick a `Camera`, (2) allocate a `RenderTexture` at `(width, height)`, (3) `cam.Render()` + `ReadPixels` into a `Texture2D`, (4) `EncodeToPNG`, (5) `File.WriteAllBytes` + `AssetDatabase.ImportAsset`. The only varying input is **how the camera is selected**:
- `screenshot-camera` → by name / instanceId / fallback to `Camera.main`
- `screenshot-game-view` → always `Camera.main`
- `screenshot-scene-view` → `SceneView.lastActiveSceneView.camera`

Notably, `screenshot-game-view` is a strict subset of `screenshot-camera` — passing no `cameraName` and no `instanceId` to `screenshot-camera` already falls back to `Camera.main` (line 102 of `Tool_Screenshot.Camera.cs`). The two tools produce equivalent output for the default case but differ in pixel format (`RGBA32` vs `RGB24`) and AA setup.
**Impact:** High. An LLM asked "take a screenshot of the game" has three candidates, and the cheapest mental model ("just use the main camera") is split between two tools without any disambiguating phrase. `screenshot-game-view` adds no capability over `screenshot-camera` with default params.
**Confidence:** high

### Cluster R2 — `screenshot-camera` overlaps in intent with `camera-screenshot-multiview` (cross-domain)
**Members:** `screenshot-camera` (this domain), `camera-screenshot-multiview` (Camera domain)
**Overlap:** Both render a Unity scene to PNG and save under `Assets/Screenshots/`. They differ functionally (single-shot vs 3x2 contact sheet of orthographic angles), but for an LLM dispatching a user request like "show me what this enemy looks like", both are plausible. `camera-screenshot-multiview` uses `Tool_Transform.FindGameObject(instanceId, objectPath)`; `screenshot-camera` uses its own `cameraName`/`instanceId` resolution. The two parameter shapes for "find the thing to look at" are inconsistent.
**Impact:** Medium. Different enough not to be a true duplicate, but the LLM-side disambiguation is weak — neither tool's description references the other. Out of scope for direct merging, but the Screenshot domain's descriptions should at least mention "for multi-angle previews, use `camera-screenshot-multiview`."
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `screenshot-game-view` description is vague and under-distinguishes itself from `screenshot-camera`
**Location:** `screenshot-game-view` — `Tool_Screenshot.GameView.cs` line 26
**Issue:** The description "Captures a screenshot from the main camera and saves it as PNG." is 11 words, has no example, and contains no "use this when X, not Y" disambiguator versus `screenshot-camera`. An LLM reading both descriptions has no signal for which to pick when the user says "screenshot the game".
**Evidence:**
```csharp
[Description("Captures a screenshot from the main camera and saves it as PNG.")]
```
**Confidence:** high

### A2 — `screenshot-scene-view` description does not explain "Scene View" vs "Game View" tradeoff
**Location:** `screenshot-scene-view` — `Tool_Screenshot.SceneView.cs` line 25
**Issue:** Description is "Captures a screenshot from the active Scene View and saves it as PNG." (13 words). It doesn't tell the LLM that this captures the **editor camera** (with gizmos/grid as currently configured), not the player-facing view. An LLM asked to "screenshot the game" could pick this and produce a result with editor gizmos baked in.
**Evidence:**
```csharp
[Description("Captures a screenshot from the active Scene View and saves it as PNG.")]
```
**Confidence:** high

### A3 — `screenshot-camera` cameraName/instanceId precedence is not documented at description level
**Location:** `screenshot-camera` — `Tool_Screenshot.Camera.cs` line 36
**Issue:** The method-level description does not mention that `cameraName` takes precedence over `instanceId`, and that *both* being empty falls back to `Camera.main`. The XML doc on line 25 states this, but the `[Description]` attribute (which is what reaches the LLM per CLAUDE.md) does not. The per-param descriptions hint at it ("Used only when cameraName is empty") but never say what happens when both are empty.
**Evidence:** Method `[Description]` at line 36:
```csharp
"Renders a Unity camera to a PNG. Find by cameraName or instanceId. " +
"Returns the image as base64 (returnBase64=true) or saves it to savePath."
```
The fallback `Camera.main` behavior at line 102 is hidden from the LLM.
**Confidence:** high

### A4 — Inconsistent identifier vocabulary across the domain
**Location:** `screenshot-camera` parameter set
**Issue:** The rest of the project uses `(instanceId, objectPath)` to identify GameObjects (see `Tool_Transform.FindGameObject` and `camera-screenshot-multiview`). `screenshot-camera` instead uses `(cameraName, instanceId)` — string is the **name**, not a hierarchy path. This means `cameraName="MainCamera"` works but `cameraName="Player/Cameras/MainCamera"` does not. The description does not warn about this.
**Evidence:** `Tool_Screenshot.Camera.cs` lines 57-66 — only matches `gameObject.name`, no path resolution.
**Confidence:** high

### A5 — `returnBase64` semantics and `savePath` interaction not fully spelled out
**Location:** `screenshot-camera` param `savePath`
**Issue:** Param description says "Ignored when returnBase64 is true." But there's no statement of the *reverse* — that when `returnBase64=false`, `savePath` is required-with-default. There's also no warning that the LLM is about to receive a potentially large base64 string (1920x1080 PNGs are easily several hundred KB; the response includes `b64Length` but that is computed *after* sending). For an LLM with a context budget this matters.
**Evidence:** `Tool_Screenshot.Camera.cs` line 42 + line 141.
**Confidence:** medium

### A6 — `screenshot-game-view` and `screenshot-scene-view` lack a base64 / inline option
**Location:** `screenshot-game-view`, `screenshot-scene-view`
**Issue:** Only `screenshot-camera` exposes `returnBase64`. The other two always write to disk and return only a path. An LLM that wants to *see* the screenshot it just took must either follow up with another tool to load the file or fall back to `screenshot-camera`. This is more of a capability gap (see G2) but it also makes the tool descriptions misleading by omission — they don't disclose the disk-only constraint.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — Three different default save paths, all under the same folder, with predictable filename collisions
**Location:** `screenshot-camera.savePath`, `screenshot-game-view.savePath`, `screenshot-scene-view.savePath`
**Issue:** Defaults are `Assets/Screenshots/Camera.png`, `Assets/Screenshots/GameView.png`, `Assets/Screenshots/SceneView.png`. Calling any tool twice silently overwrites the previous capture. There is no timestamp suffix, no `_001`/`_002` rotation, no warning. For an LLM doing iterative work ("compare before and after"), this destroys the previous shot without telling the model.
**Current:**
```csharp
string savePath = "Assets/Screenshots/Camera.png"
string savePath = "Assets/Screenshots/GameView.png"
string savePath = "Assets/Screenshots/SceneView.png"
```
**Suggested direction:** Either include a timestamp or sequence in the default path, OR change the response message to call out "(overwrote existing file)" when applicable so the LLM knows. The audit doesn't prescribe one over the other; that's the planner's call.
**Confidence:** high

### D2 — `width`/`height` defaults of 1920x1080 are reasonable for game capture but heavy for LLM-visible base64
**Location:** `screenshot-camera.width=1920, height=1080`
**Issue:** When `returnBase64=true` is used, a 1920x1080 PNG can easily exceed 500 KB → ~700 KB base64. That's a large slice of LLM context. There's no smaller default specifically for the base64 path. The LLM has no signal that it should drop to e.g. 512x512 when asking for a base64 preview.
**Suggested direction:** Either document in the description that `returnBase64=true` callers should reduce dimensions, or consider a different default when `returnBase64=true`. Planner decides.
**Confidence:** medium

### D3 — `screenshot-camera` allows `cameraName=""` AND `instanceId=0` simultaneously; falls back to `Camera.main` silently
**Location:** `screenshot-camera` (params `cameraName`, `instanceId`)
**Issue:** Both defaults represent "not provided." The fallback to `Camera.main` is convenient, but it's a third behavior not declared as a default. From the LLM's perspective, the tool has an implicit default target ("main camera") that is not stated in any description. This is the same root cause as A3, but framed as a default-value issue: there is effectively a *hidden default target*.
**Suggested direction:** Surface this in the method `[Description]` so the LLM can predict behavior without reading source.
**Confidence:** high

### D4 — `screenshot-game-view` lacks `returnBase64` default at all
**Location:** `screenshot-game-view` parameter set
**Issue:** Not a wrong default per se — the param doesn't exist. Listed here because adding a default-`false` `returnBase64` parameter would harmonize the three tools without breaking callers. See also A6 / G2.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Cannot capture a SceneView at a specified resolution
**Workflow:** "Render the current Scene View to a 1920x1080 PNG so I can review composition at presentation size."
**Current coverage:** `screenshot-scene-view` exists but always uses `(int)sv.position.width × (int)sv.position.height` — i.e., the editor window's pixel size at capture time.
**Missing:** `width` / `height` parameters on `screenshot-scene-view`. Both `screenshot-game-view` and `screenshot-camera` accept them; `screenshot-scene-view` does not, even though the underlying capture path is structurally identical.
**Evidence:** `Tool_Screenshot.SceneView.cs` lines 26-28 — only one parameter (`savePath`); lines 58-59 hardwire dimensions to the SceneView window's size.
**Confidence:** high

### G2 — Cannot return GameView or SceneView screenshot as base64 / inline image
**Workflow:** "Show me what the scene looks like right now" — LLM needs the image content directly to reason about it (composition, color, layout), not just a file path it cannot read back.
**Current coverage:** `screenshot-camera` exposes `returnBase64=true` and uses `ToolResponse.Image(...)` to inline the bytes.
**Missing:** `screenshot-game-view` and `screenshot-scene-view` always write to disk and return `ToolResponse.Text(path)`. The MCP host has no built-in way to read that file back into the model's context. The base64 escape hatch only exists for `screenshot-camera`.
**Evidence:** `Tool_Screenshot.GameView.cs` line 78 returns only path text; `Tool_Screenshot.SceneView.cs` line 85 same. `ToolResponse.Image(...)` is defined at `Editor/MCP/Models/ToolResponse.cs:139` and is only invoked from `screenshot-camera`.
**Confidence:** high

### G3 — Cannot find a camera by hierarchy path
**Workflow:** "Capture from the camera at `Player/Rig/AimCamera`" — ambiguous when multiple GameObjects share a name (e.g. nested rigs).
**Current coverage:** `screenshot-camera.cameraName` matches `gameObject.name` only (no path), and `instanceId` requires the LLM to already know the integer ID.
**Missing:** A path-based selector (the project's standard `objectPath` parameter, used by `Tool_Transform.FindGameObject` and by `camera-screenshot-multiview`).
**Evidence:** `Tool_Screenshot.Camera.cs` lines 57-66 — uses `Camera.allCameras` then matches `gameObject.name == cameraName`, returning the first match. No path traversal.
**Confidence:** high

### G4 — Cannot capture transparent backgrounds (alpha channel) from GameView / SceneView
**Workflow:** "Capture the title screen UI with transparent background to use in marketing material."
**Current coverage:** `screenshot-camera` uses `RenderTextureFormat.ARGB32` + `TextureFormat.RGBA32` — alpha-aware. `screenshot-game-view` uses default RT format and `TextureFormat.RGB24` — alpha is dropped. `screenshot-scene-view` same: `TextureFormat.RGB24`.
**Missing:** No `transparentBackground` / `format` flag on GameView or SceneView captures. An LLM asked for transparent capture must use `screenshot-camera` directly, but only after first knowing which camera to pick.
**Evidence:** `Tool_Screenshot.GameView.cs` line 64 — `new Texture2D(width, height, TextureFormat.RGB24, false)`; `Tool_Screenshot.SceneView.cs` line 71 same.
**Confidence:** high

### G5 — Cannot configure render quality / anti-aliasing
**Workflow:** "Take a high-quality 4K screenshot with 8x MSAA for documentation."
**Current coverage:** `screenshot-camera` hardcodes `antiAliasing = 1`. `screenshot-game-view` and `screenshot-scene-view` do not set AA at all (uses RT default).
**Missing:** No `antiAliasing` / `msaa` parameter on any tool. No `quality` knob. No control over `RenderTextureFormat`. For a creative-tooling MCP package, this is a notable gap — high-quality stills are a primary use case.
**Evidence:** `Tool_Screenshot.Camera.cs` line 112 — `antiAliasing = 1` hardcoded; `Tool_Screenshot.GameView.cs` line 59 — `new RenderTexture(width, height, 24)` no AA spec.
**Confidence:** high

### G6 — No way to capture multiple cameras in one call (batch)
**Workflow:** "Capture all 4 player cameras side-by-side for a split-screen mockup."
**Current coverage:** `screenshot-camera` captures one camera at a time. `camera-screenshot-multiview` (Camera domain) captures one *focus object* from 6 fixed angles using a temp orthographic camera — different use case.
**Missing:** A batch / list-of-cameras mode. Or a contact-sheet variant that accepts an array of camera names. Not necessarily critical, but worth noting since the multiview tool already proves this composition pattern is wanted.
**Evidence:** `Tool_Screenshot.Camera.cs` only iterates a single `target` camera.
**Confidence:** medium

### G7 — `savePath` is restricted to `Assets/` but Unity supports captures outside the project
**Workflow:** "Save the screenshot to my Desktop / external review folder." (Common when sharing with non-Unity teammates.)
**Current coverage:** All three tools enforce `savePath.StartsWith("Assets/")` and return an error otherwise.
**Missing:** No way to write outside the project. This may be intentional (keeps it under `AssetDatabase`), but the descriptions don't tell the LLM the constraint exists, so it will burn a turn discovering the rule.
**Evidence:** `Tool_Screenshot.Camera.cs` line 149, `Tool_Screenshot.GameView.cs` line 35, `Tool_Screenshot.SceneView.cs` line 32.
**Confidence:** medium (low impact — likely an intentional safety rail; flagging mainly so the planner can decide whether to lift the constraint or simply document it)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | R1 | Redundancy | 5 | 2 | 20 | high | Three tools collapse into one capture-to-PNG action with a `source` selector. `game-view` is a strict subset of `camera`. |
| 2 | G2 | Capability Gap | 4 | 1 | 20 | high | Add `returnBase64` to GameView + SceneView so LLM can actually see what it captured. |
| 3 | G1 | Capability Gap | 4 | 1 | 20 | high | Add `width`/`height` to `screenshot-scene-view` — already trivial in the other two tools. |
| 4 | A1+A2 | Ambiguity | 4 | 1 | 20 | high | GameView and SceneView descriptions need a "use this not that" clause referring to each other and to `screenshot-camera`. |
| 5 | D1 | Default | 3 | 2 | 12 | high | Default save paths collide silently on repeat calls; either timestamp or surface "overwrote existing" in response. |
| 6 | G3 | Capability Gap | 3 | 2 | 12 | high | Allow finding camera by `objectPath` to align with the rest of the project's identifier convention. |
| 7 | A3+D3 | Ambiguity / Default | 3 | 1 | 15 | high | Document the implicit `Camera.main` fallback in the method `[Description]` of `screenshot-camera`. |
| 8 | G4 | Capability Gap | 3 | 2 | 12 | high | Add transparent-background / RGBA option for GameView + SceneView captures. |
| 9 | A4 | Ambiguity | 3 | 1 | 15 | high | `cameraName` is a name match, not a path — disclose this or rename to `cameraObjectPath` with proper resolution. |
| 10 | G5 | Capability Gap | 2 | 3 | 6 | high | Expose `antiAliasing` / quality knobs for high-fidelity stills. |
| 11 | G6 | Capability Gap | 2 | 3 | 6 | medium | Batch capture across multiple cameras. |
| 12 | A5 | Ambiguity | 2 | 1 | 10 | medium | Warn callers about base64 size when using `returnBase64=true` with default 1920x1080. |
| 13 | D2 | Default | 2 | 2 | 8 | medium | Consider smaller default dims for base64 path. |
| 14 | A6 | Ambiguity | 3 | 1 | 15 | high | (Subsumed by G2) GameView/SceneView descriptions don't disclose disk-only constraint. |
| 15 | G7 | Capability Gap | 1 | 2 | 4 | medium | `Assets/`-only restriction is undocumented; either lift or document. |
| 16 | R2 | Redundancy (cross-domain) | 2 | 4 | 4 | medium | `camera-screenshot-multiview` overlaps in user intent — at minimum cross-link it from Screenshot descriptions. |

**Top 3 actionable bundles for the planner:**

1. **Consolidate the three capture tools.** R1 + G1 + G2 + A1 + A2 + A3 + D3 all collapse into a single action: one `screenshot` tool with a `source` parameter (`"camera" | "game-view" | "scene-view"`) plus a `target` (path/name/instanceId) and a unified set of options (`width`, `height`, `returnBase64`, `transparentBackground`). This is the highest-leverage move.
2. **Identifier vocabulary cleanup.** G3 + A4 — use `objectPath` and `Tool_Transform.FindGameObject` like the rest of the project.
3. **Description hygiene.** D1 (collision messaging), A5 (base64 size warning), G7 (Assets/ rule disclosure) are all single-line fixes.

---

## 7. Notes

**Cross-domain dependency to flag for the planner:**
- `camera-screenshot-multiview` lives in `Editor/Tools/Camera/`, not `Editor/Tools/Screenshot/`. If the consolidation-planner produces a unified `screenshot` macro tool, the planner should at minimum decide whether multiview belongs in the Screenshot domain too, or whether the two domains should cross-reference each other in their descriptions. I am not making a recommendation either way — flagging only.

**Code-quality observations (not findings, just FYI for whoever edits these files):**
- `Tool_Screenshot.GameView.cs` and `Tool_Screenshot.SceneView.cs` mutate `cam.targetTexture` without saving and restoring its previous value. `Tool_Screenshot.Camera.cs` does save/restore (lines 115-127). If the GameView's `Camera.main` happened to already have a `targetTexture` assigned (e.g. by another tool or a render pipeline asset), these tools would clear it. Low-probability but not zero.
- All three tools use `Object.DestroyImmediate` on textures and RTs — correct in editor context. Just noting it for any reviewer who flinches at `DestroyImmediate`.
- Per CLAUDE.md, XML doc summaries should appear on exactly one partial file (the one with `[McpToolType]`). `Tool_Screenshot.GameView.cs` line 15-17 and `Tool_Screenshot.SceneView.cs` line 16-18 both have method-level `<summary>` blocks — those are method summaries, not class summaries, so this is fine. The class-level summary correctly only lives in `Tool_Screenshot.Camera.cs` lines 12-16.

**Open question for the reviewer:**
- Is the `Assets/`-only restriction (G7) intentional safety, or accidental? The auto-import via `AssetDatabase.ImportAsset` only makes sense for in-project paths, but the LLM is never told.

**Limits of this audit:**
- I did not run the tools or test their output. All findings are static-read based.
- I read one out-of-domain file (`Tool_Camera.ScreenshotMultiview.cs`) for context. I did not exhaustively audit the Camera domain — R2 is a cross-domain *flag*, not a verdict.
- No timing / performance analysis. 1920x1080 RT allocations on every call may be heavier than needed for repeated previews; would require profiling to confirm.
