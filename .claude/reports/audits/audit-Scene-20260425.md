# Audit Report — Scene

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Scene/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 9 (via Glob `Editor/Tools/Scene/Tool_Scene.*.cs`)
- `files_read`: 9
- `files_analyzed`: 9

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None

**Files not analyzed (if any):**
- None

**Absence claims in this report:**
- All absence claims (e.g. "no tool to set the active scene", "no tool to merge scenes") are backed by complete domain coverage (9/9 files read) plus targeted Grep checks across `Editor/Tools/` for Unity APIs that would implement the missing capability (`SetActiveScene`, `MergeScenes`, `MoveGameObjectToScene`, `SceneTemplate`, `SceneVisibilityManager`).

**Reviewer guidance:**
- The Scene domain is small (9 tools) and largely well-formed. Most findings are about (a) overlap with the `Build` domain and (b) missing multi-scene workflow primitives (active-scene management, cross-scene GameObject movement).
- The `build-manage-scenes` tool in `Editor/Tools/Build/Tool_Build.ManageScenes.cs` is reachable from this audit's scope because both `scene-create` and `scene-delete` write directly to `EditorBuildSettings.scenes`. Cross-domain coupling is significant — flagged below.
- `Tool_Scene.ViewFrame` arguably belongs in a `SceneView` domain (the Camera and Screenshot domains also touch `SceneView.lastActiveSceneView`). Not blocking, but worth noting.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `scene-create` | Scene / Create | `Tool_Scene.Create.cs` | 3 | no |
| `scene-delete` | Scene / Delete | `Tool_Scene.Delete.cs` | 2 | no |
| `scene-get-hierarchy` | Scene / Get Hierarchy | `Tool_Scene.GetHierarchy.cs` | 4 | yes |
| `scene-get-info` | Scene / Get Info | `Tool_Scene.GetInfo.cs` | 1 | yes |
| `scene-list` | Scene / List | `Tool_Scene.List.cs` | 0 | yes |
| `scene-load` | Scene / Load | `Tool_Scene.Load.cs` | 2 | no |
| `scene-save` | Scene / Save | `Tool_Scene.Save.cs` | 1 | no |
| `scene-unload` | Scene / Unload | `Tool_Scene.Unload.cs` | 1 | no |
| `scene-view-frame` | Scene / View Frame | `Tool_Scene.ViewFrame.cs` | 2 | no |

**Read-only coverage:** 3/9 tools (`get-hierarchy`, `get-info`, `list`). Healthy for an inspection-heavy domain.

**Unity API surface used:**
- `EditorSceneManager.NewScene` / `OpenScene` / `SaveScene` / `CloseScene` / `SaveCurrentModifiedScenesIfUserWantsTo`
- `SceneManager.GetActiveScene` / `GetSceneAt` / `GetSceneByPath` / `sceneCount`
- `EditorBuildSettings.scenes` (in Create + Delete — overlap with `Tool_Build.ManageScenes`)
- `AssetDatabase.GenerateUniqueAssetPath` / `DeleteAsset` / `GetMainAssetTypeAtPath` / `IsValidFolder` / `Refresh`
- `SceneView.lastActiveSceneView`, `SceneView.Frame`
- `Tool_Transform.FindGameObject` (helper, used by `view-frame` and `get-hierarchy`)

---

## 2. Redundancy Clusters

### Cluster R1 — Build Settings scene management split across two domains
**Members:** `scene-create` (param `addToBuildSettings`), `scene-delete` (param `removeFromBuildSettings`), `build-manage-scenes` (action `add` / `remove` / `enable` / `disable` / `reorder` / `list`)
**Overlap:** `scene-create` and `scene-delete` both directly mutate `EditorBuildSettings.scenes` via inline code, duplicating logic that already lives in `Tool_Build.ManageScenes.cs` (the `add` and `remove` cases). An LLM asked to "create a scene and add it to Build Settings" can pick `scene-create(addToBuildSettings=true)` OR `scene-create(addToBuildSettings=false)` followed by `build-manage-scenes(action="add")`. The result is the same; the choice is arbitrary, which is the textbook redundancy signal.
**Impact:** Low-frequency but high-confusion. Adds maintenance risk: a bug fix in `Tool_Build.ManageScenes` (e.g. duplicate-detection on add) would not propagate to `Tool_Scene.Create`. Currently `scene-create` does NOT check for duplicates before appending — `build-manage-scenes` does. Inconsistency already present.
**Confidence:** high

### Cluster R2 — `scene-get-info` vs `scene-list` overlap on single-scene case
**Members:** `scene-get-info`, `scene-list`
**Overlap:** When a single scene is open, `scene-list` returns the same fields (name, path, isLoaded, isDirty, buildIndex) that `scene-get-info` returns for the active scene. The two tools differ only in whether they report 1 or N scenes and in their formatting. The disambiguation hinge ("are you asking about THE scene or ALL scenes?") is real but subtle.
**Impact:** Medium — the LLM has to pick correctly when the user says "what scene is open?" (ambiguous between one and many). Fixable with a one-line "use this when X, not Y" clause in each description.
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `scene-load` mode parameter doesn't enumerate values in description
**Location:** `scene-load` — `Tool_Scene.Load.cs`
**Issue:** The `mode` parameter accepts the strings `"Single"` or `"Additive"` (case-insensitive). The parameter description says `"Load mode: 'Single' replaces current scene, 'Additive' adds to current scenes. Default 'Single'."` — this is actually OK. However, the **method-level** `[Description]` (`"Opens a scene in the Editor. Prompts to save the current scene if it has unsaved changes."`) does not mention that an additive mode exists at all, so an LLM scanning method summaries (without inspecting params) may not know this tool can do multi-scene loading.
**Evidence:** `Tool_Scene.Load.cs` line 29: `[Description("Opens a scene in the Editor. Prompts to save the current scene if it has unsaved changes.")]`
**Confidence:** medium

### A2 — `scene-save` description hides the path-dispatch behavior
**Location:** `scene-save` — `Tool_Scene.Save.cs`
**Issue:** Method description is 11 words: `"Saves the current active scene or a specific scene by path."` It does not state that:
- The scene must already be open in the editor (passing a path of an unloaded scene returns "Scene not found").
- The empty default for `scenePath` means "active scene" (only documented in the param, not at the method level).
- It does NOT save dirty-flag-only scenes via `SaveCurrentModifiedScenesIfUserWantsTo` — it always calls `SaveScene` directly even on clean scenes.
**Evidence:** `Tool_Scene.Save.cs` lines 27, 42–47.
**Confidence:** high

### A3 — `scene-view-frame` lives under `Scene` but operates on `SceneView`, not `Scene`
**Location:** `scene-view-frame` — `Tool_Scene.ViewFrame.cs`
**Issue:** Tool ID and naming suggest operation on a Scene asset, but the tool actually frames a GameObject in the editor's `SceneView` window. An LLM asked "frame the boss in the scene view" might find this; an LLM asked "what does scene-view-frame do?" may misread it as scene-list-related. The Title `"Scene / View Frame"` parses awkwardly. A more accurate name would put it in a `scene-view-*` family (currently empty) or under a separate `view` / `editor-view` domain.
**Evidence:** `Tool_Scene.ViewFrame.cs` line 25 + body uses `SceneView.lastActiveSceneView`. Compare with `screenshot-scene-view` in the Screenshot domain which uses the same API and got a different naming convention.
**Confidence:** medium

### A4 — `scene-get-hierarchy` parentPath: empty-string semantics not in method description
**Location:** `scene-get-hierarchy` — `Tool_Scene.GetHierarchy.cs`
**Issue:** Method description does say "Leave parentPath empty to list root objects" — that's actually well-handled. No issue.
**Confidence:** n/a — withdrawn during review

### A5 — `scene-create` does not document that scene names get auto-uniquified
**Location:** `scene-create` — `Tool_Scene.Create.cs`
**Issue:** Line 66 calls `AssetDatabase.GenerateUniqueAssetPath(scenePath)` which silently appends ` 1`, ` 2`, etc. if the name collides. The description and param doc do not mention this — an LLM that creates "Main.unity" twice will report success both times but the second one is actually "Main 1.unity". This is a soft data-loss class of issue (the LLM thinks it loaded `Main.unity` next, but operates on a different file).
**Evidence:** `Tool_Scene.Create.cs` line 66.
**Confidence:** high

### A6 — `scene-load` does not surface the user-cancelled-save case clearly
**Location:** `scene-load` — `Tool_Scene.Load.cs`
**Issue:** When `SaveCurrentModifiedScenesIfUserWantsTo()` returns false, the tool returns `ToolResponse.Text("Scene load cancelled by the user.")` — a successful Text response, not an Error. The XML doc-comment notes this; the `[Description]` does not. An LLM checking only `success` may treat the cancellation as success and continue with a workflow on the unchanged scene.
**Evidence:** `Tool_Scene.Load.cs` lines 59–64. `ToolResponse.Text(...)` indicates success but the load did not happen.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `scene-create.folderPath` default may not exist in the project
**Location:** `scene-create` param `folderPath`
**Issue:** Default is `"Assets/Scenes"`. The tool auto-creates this directory if missing (lines 51–63), so this is safe. However: in projects that organize scenes elsewhere (e.g. `Assets/_Project/Scenes`), the default silently creates a stray empty `Assets/Scenes` folder that the user did not intend. Not critical — flag for awareness.
**Current:** `string folderPath = "Assets/Scenes"`
**Suggested direction:** Keep the default but call out the auto-create behavior in the `[Description]` so the LLM understands the side-effect.
**Confidence:** medium

### D2 — `scene-create.addToBuildSettings = true` default is opinionated
**Location:** `scene-create` param `addToBuildSettings`
**Issue:** Default is `true`. For scratch / experiment scenes (a common LLM use case), being added to Build Settings is usually unwanted (it bloats the build). For "real" scenes, it is wanted. There is no objectively correct default; either choice surprises some callers.
**Current:** `bool addToBuildSettings = true`
**Suggested direction:** Either flip to `false` (safer side-effect) or document the rationale explicitly. This is a strategic decision — escalate to reviewer.
**Confidence:** medium

### D3 — `scene-load.mode = "Single"` default can clobber unsaved work silently
**Location:** `scene-load` param `mode`
**Issue:** Default `"Single"` means "replace current scene". Combined with A6 (cancellation reported as success), an LLM could call `scene-load` on the wrong scene path, get a "load cancelled" Text response, think it succeeded, and never realize the user blocked the change. Default itself is conventional Unity behavior, so probably OK — but the interaction with A6 amplifies the impact.
**Current:** `string mode = "Single"`
**Suggested direction:** Default is fine; fix is in A6 (clearer return signaling).
**Confidence:** low

### D4 — `scene-get-hierarchy.pageSize = 50` is reasonable but undocumented as cap
**Location:** `scene-get-hierarchy` param `pageSize`
**Issue:** Default 50, hard cap 500 (line 45–48). The cap is silent — passing `pageSize=1000` does not error, it silently clamps to 500. An LLM may not realize a "page" is partial.
**Current:** `int pageSize = 50`
**Suggested direction:** Either document the 500 cap in the param description, or return a soft warning when clamped.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Setting the active scene in a multi-scene editor session
**Workflow:** A developer with multiple additively-loaded scenes wants to designate which one receives newly-instantiated GameObjects (the "active" scene). This is standard multi-scene Unity workflow.
**Current coverage:** `scene-list` reports which scene is loaded but does not flag which is active. `scene-get-info` reports info about "the active scene" but does not let you change it.
**Missing:** No tool wraps `SceneManager.SetActiveScene(Scene)`. Grep confirms zero matches for `SetActiveScene` across `Editor/Tools/`.
**Evidence:** `Grep` for `SetActiveScene` in `Editor/Tools/` returned no files. The 9 Scene-domain files contain no call to it.
**Confidence:** high

### G2 — Moving a GameObject between loaded scenes
**Workflow:** With two scenes loaded additively (e.g. Persistent + Level1), the user wants to move a GameObject from Level1 to Persistent before unloading Level1.
**Current coverage:** None in the Scene domain. The GameObject domain (not in scope) handles parenting within a scene but cross-scene moves use a distinct API.
**Missing:** No tool wraps `SceneManager.MoveGameObjectToScene(GameObject, Scene)`. Grep confirms zero matches for `MoveGameObjectToScene` across `Editor/Tools/`.
**Evidence:** `Grep` for `MoveGameObjectToScene` returned no files.
**Confidence:** high

### G3 — Merging scenes
**Workflow:** Combine two scenes into one (e.g. merge a sub-level into a master scene before final build).
**Current coverage:** None.
**Missing:** No tool wraps `SceneManager.MergeScenes(Scene, Scene)`. Grep confirms zero matches across `Editor/Tools/`.
**Evidence:** `Grep` for `MergeScenes` returned no files.
**Confidence:** high

### G4 — Creating from a Scene Template (Unity 2020.1+)
**Workflow:** A developer with a configured `SceneTemplate` asset (e.g. "URP Sample Scene", or a project-specific template with default lighting + camera + post-processing volumes) wants to instantiate it. `scene-create` only creates `NewSceneSetup.DefaultGameObjects` — basic camera + light.
**Current coverage:** `scene-create` uses `EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive)` only.
**Missing:** No tool wraps `SceneTemplateService.Instantiate(SceneTemplateAsset, ...)` or accepts a template path. Grep confirms zero matches for `SceneTemplate` across `Editor/Tools/`.
**Evidence:** `Tool_Scene.Create.cs` line 68; `Grep` for `SceneTemplate` returned no files.
**Confidence:** high

### G5 — Scene rename / move / duplicate
**Workflow:** Standard asset operations applied to scene files: rename `Old.unity` → `New.unity`, move from `Assets/Scenes` to `Assets/_Project/Scenes`, or duplicate `Level1` to seed `Level2`.
**Current coverage:** None scene-specific. The general Asset domain (not in scope) presumably has rename/move, but those operate on raw asset paths and would NOT update `EditorBuildSettings.scenes` when a scene is moved — leaving Build Settings dangling.
**Missing:** No tool combines an asset-level rename/move with Build Settings synchronization. Without this, renaming a scene that's in Build Settings leaves an invalid entry pointing at a missing path.
**Evidence:** Inspected the 9 Scene-domain files; only `Create` and `Delete` touch `EditorBuildSettings.scenes`. No rename/move/duplicate in Scene domain. Grep `scene-rename|scene-duplicate|scene-move` across `Editor/Tools/` returned no files.
**Confidence:** high

### G6 — Scene visibility / isolation in editor
**Workflow:** Toggle scene visibility (the eye icon in the Hierarchy) or isolate a scene to focus work on it. This is the `SceneVisibilityManager` API and is part of standard Unity Editor workflow.
**Current coverage:** None.
**Missing:** No tool wraps `SceneVisibilityManager.instance.Show/Hide/Isolate(...)`. Grep confirms zero matches across `Editor/Tools/`.
**Evidence:** `Grep` for `SceneVisibilityManager` returned no files.
**Confidence:** high (but lower priority — this is editor UX, not a build-breaker)

### G7 — Scene View camera control beyond Frame
**Workflow:** Position/rotate the Scene View camera programmatically (e.g. "look at the boss spawn from above"), set 2D mode, set ortho/perspective, set view direction.
**Current coverage:** `scene-view-frame` handles framing one bounded GameObject. `Tool_Camera.AlignToView` (in Camera domain) aligns a Camera component to the current SceneView.
**Missing:** No tool sets `SceneView.pivot`, `SceneView.rotation`, `SceneView.size`, `SceneView.in2DMode`, or `SceneView.orthographic`. Grep across `Editor/Tools/` shows these properties are not used outside `Tool_Camera.AlignToView` (which reads, not writes them generically).
**Evidence:** `Grep` for `SceneView\.pivot|SceneView\.rotation|SceneView\.size` across `Editor/Tools/` returned only `Tool_Scene.ViewFrame.cs` and `Tool_Screenshot.SceneView.cs`, neither of which writes these.
**Confidence:** medium (the gap exists, but this is a "nice to have" for AI-driven editor demos, not core scene management)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 1 | 25 | high | Add `scene-set-active` (one-liner wrapping `SceneManager.SetActiveScene`). Common multi-scene workflow blocker. |
| 2 | G2 | Capability Gap | 4 | 1 | 20 | high | Add cross-scene GameObject move (`SceneManager.MoveGameObjectToScene`). |
| 3 | R1 | Redundancy | 3 | 2 | 12 | high | Decide: keep `addToBuildSettings`/`removeFromBuildSettings` shortcuts in `scene-create`/`scene-delete`, OR delegate fully to `build-manage-scenes`. Currently inconsistent (no dup-check in Create). |
| 4 | A6 | Ambiguity | 4 | 1 | 20 | high | `scene-load` returns `Text` (success-shaped) when user cancels save prompt. Should be Error or distinct status. |
| 5 | A5 | Ambiguity | 4 | 1 | 20 | high | `scene-create` silently uniquifies names via `GenerateUniqueAssetPath`; not documented. Risk of LLM acting on wrong file. |
| 6 | G5 | Capability Gap | 3 | 3 | 9 | high | Scene rename/move/duplicate that keeps Build Settings in sync. Bigger surface than G1/G2. |
| 7 | A2 | Ambiguity | 3 | 1 | 15 | high | `scene-save` description doesn't say scene must be loaded; ambiguous failure mode. |
| 8 | A3 | Ambiguity | 2 | 2 | 8 | medium | `scene-view-frame` mis-categorized; rename or move to `view`/`scene-view` family. |
| 9 | G4 | Capability Gap | 2 | 2 | 8 | high | Scene Template support (`SceneTemplateService`). Useful for projects with template assets. |
| 10 | D2 | Default | 2 | 1 | 10 | medium | Reconsider `scene-create.addToBuildSettings = true` default. Strategic call for reviewer. |
| 11 | R2 | Redundancy | 2 | 1 | 10 | medium | Add disambiguation clause between `scene-list` and `scene-get-info`. |
| 12 | G3 | Capability Gap | 2 | 2 | 8 | high | Scene merge — niche but cheap to wrap. |
| 13 | D4 | Default | 2 | 1 | 10 | medium | Document `pageSize` cap of 500 in `scene-get-hierarchy`. |
| 14 | A1 | Ambiguity | 2 | 1 | 10 | medium | Mention additive mode in `scene-load` method description. |
| 15 | G6 | Capability Gap | 1 | 2 | 4 | high | `SceneVisibilityManager` — low priority. |
| 16 | G7 | Capability Gap | 1 | 3 | 3 | medium | `SceneView` camera control — low priority, could live in dedicated `view` domain. |
| 17 | D1 | Default | 1 | 1 | 5 | medium | Document `folderPath` auto-create side effect in `scene-create`. |
| 18 | D3 | Default | 1 | 1 | 5 | low | Subsumed by A6. |

**Top-impact actionable bundle:** G1 + G2 + A5 + A6 + R1. All are low-effort and address real workflow / safety issues.

---

## 7. Notes

**Cross-domain coupling — Build Settings:**
The `scene-create` and `scene-delete` tools mutate `EditorBuildSettings.scenes` directly. This logic is duplicated (with a notable inconsistency: `Create` does not check for duplicates, `build-manage-scenes` does). The consolidation-planner should decide whether to:
- (a) collapse Build Settings handling into `build-manage-scenes` and remove the bool flags from `scene-create`/`scene-delete`, OR
- (b) keep the convenience flags but factor the underlying mutation into a shared helper used by both domains.

Either is defensible. (a) is cleaner; (b) preserves the "do it in one call" UX.

**Cross-domain coupling — Scene View:**
`Tool_Scene.ViewFrame.cs`, `Tool_Screenshot.SceneView.cs`, `Tool_Camera.AlignToView.cs`, and `Tool_Graphics.Stats.cs` all touch `SceneView.lastActiveSceneView`. There is no `SceneView` / `View` domain. If G7 (Scene View camera control) is ever pursued, consider creating one and migrating these tools — but that's a v2 question, not part of this audit's mandate.

**Naming consistency:**
The `Tool_Scene` partial class summary on `Tool_Scene.Create.cs` is the canonical XML doc per project conventions — verified correct, no duplication on the other 8 partial files. The other 8 files correctly omit class-level summaries.

**Workflows intentionally not analyzed:**
- Scene asset import settings (`SceneImporter`) — typically not a manual workflow.
- Lighting bake per-scene — already handled by `Graphics/Tool_Graphics.Baking.cs`, out of scope.

**Open questions for the reviewer:**
- Is `scene-view-frame` worth renaming to `view-frame-object` and moving to a new `View` domain, or is the current placement acceptable?
- For G5 (rename/move/duplicate), should the consolidation-planner add scene-specific tools, or extend the Asset domain with scene-aware sync of Build Settings?
- D2: should `scene-create.addToBuildSettings` default to `false` to match the principle of least surprise / least side effect?
