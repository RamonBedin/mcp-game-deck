# Audit Report — Editor

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Editor/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 7 (via Glob `Editor/Tools/Editor/Tool_Editor.*.cs`)
- `files_read`: 7
- `files_analyzed`: 7

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None

**Files not analyzed (if any):**
- None

**Absence claims in this report:**
- All absence claims are backed by complete domain coverage. Cross-domain Grep was used to confirm whether supporting tools exist outside `Editor/Tools/Editor/` (e.g. `Tool_Scene.Save`).

**Reviewer guidance:**
- This domain is intentionally a "kitchen sink" for raw Editor automation (play mode, menu, undo, prefs, transform tool, tags/layers). It overlaps thematically with `Scene`, `BatchExecute`, and `Reflect` domains, but the boundary appears intentional. The most important issues here are (a) two tools doing essentially the same job (`editor-get-state` vs `editor-info`), and (b) the absence of *list/inspect* tools for Tags, Layers, and EditorPrefs — making the existing add/remove/set tools blind.
- The blocked menu prefix list in `ExecuteMenu` is a security surface worth reviewer attention but is not a defect per se.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `editor-get-state` | Editor / Get State | `Tool_Editor.GetState.cs` | 0 | yes |
| `editor-play` | Editor / Play | `Tool_Editor.PlayMode.cs` | 0 | no |
| `editor-pause` | Editor / Pause | `Tool_Editor.PlayMode.cs` | 0 | no |
| `editor-stop` | Editor / Stop | `Tool_Editor.PlayMode.cs` | 0 | no |
| `editor-set-active-tool` | Editor / Set Active Tool | `Tool_Editor.SetActiveTool.cs` | 1 (`toolName="Move"`) | no |
| `editor-add-tag` | Editor / Add Tag | `Tool_Editor.Tags.cs` | 1 (`tagName`) | no |
| `editor-add-layer` | Editor / Add Layer | `Tool_Editor.Tags.cs` | 1 (`layerName`) | no |
| `editor-remove-tag` | Editor / Remove Tag | `Tool_Editor.Tags.cs` | 1 (`tagName`) | no |
| `editor-remove-layer` | Editor / Remove Layer | `Tool_Editor.Tags.cs` | 1 (`layerName`) | no |
| `editor-undo` | Editor / Undo | `Tool_Editor.Undo.cs` | 0 | no |
| `editor-redo` | Editor / Redo | `Tool_Editor.Undo.cs` | 0 | no |
| `editor-execute-menu` | Editor / Execute Menu | `Tool_Editor.ExecuteMenu.cs` | 1 (`menuPath`) | no |
| `editor-get-pref` | Editor / Get Preference | `Tool_Editor.Preferences.cs` | 2 (`key`, `type="string"`) | no (should be yes) |
| `editor-set-pref` | Editor / Set Preference | `Tool_Editor.Preferences.cs` | 3 (`key`, `value`, `type="string"`) | no |
| `editor-info` | Editor / Info | `Tool_Editor.Preferences.cs` | 0 | no (should be yes) |

**Total tools:** 15 across 7 files.
**Read-only marked:** 1 of 15 (`editor-get-state` only).
**Internal Unity APIs touched:** `EditorApplication` (isPlaying/isPaused/isCompiling/ExecuteMenuItem), `UnityEditor.Tools.current`, `Undo.PerformUndo/PerformRedo`, `EditorPrefs` (Get/Set String/Int/Float/Bool), `SerializedObject` over `ProjectSettings/TagManager.asset`, `EditorUserBuildSettings.activeBuildTarget`, `SceneManager.GetActiveScene`.

---

## 2. Redundancy Clusters

### Cluster R1 — Editor State / Info dual tools
**Members:** `editor-get-state`, `editor-info`
**Overlap:** Both return a multi-line text block describing Unity Editor state. `editor-get-state` reports Unity version, build target, isPlaying/isPaused/isCompiling/isUpdating, active scene name+path, scene isDirty, scene rootCount. `editor-info` reports Unity version, platform (Application.platform — slightly different from build target), data paths, isPlaying/isCompiling/isPaused, active scene name+path, sceneCount, build target. Roughly 70% of fields overlap; the only meaningful unique data in `editor-info` is the three filesystem paths (dataPath, persistentDataPath, streamingAssetsPath). For the LLM "give me Unity Editor status" intent, both tools are valid answers.
**Impact:** High. The two tools live in the same domain and have nearly identical descriptions ("current Editor state… play mode, …, current scene, platform, Unity version" vs "Unity Editor information including version, platform, data paths, play mode state, and current scene"). An LLM cannot reliably pick one over the other; descriptions do not contain a "use this when X, not Y" disambiguation clause. Additionally `editor-info` is missing `ReadOnlyHint = true` even though it is purely informational.
**Confidence:** high

### Cluster R2 — Play mode triplet (borderline)
**Members:** `editor-play`, `editor-pause`, `editor-stop`
**Overlap:** Three zero-arg tools each manipulating `EditorApplication.isPlaying` / `isPaused`. Could plausibly be unified as `editor-set-play-state(state: "play"|"pause"|"resume"|"stop"|"toggle-pause")`. They are not strictly redundant (each does a single distinct action) but they fragment a single conceptual capability ("play mode control") into three tools. Compare `Tool_Animation.ConfigureController.cs` (cited in audit prompt as good example) where related actions are unified under `action` dispatch.
**Impact:** Medium. Three tools is a small fragment; the cost is mostly inventory bloat. LLM disambiguation is easy because the verbs are distinct, but having the LLM inspect three tool descriptions to perform one workflow is wasteful.
**Confidence:** medium

### Cluster R3 — Tags / Layers add+remove parallel tools
**Members:** `editor-add-tag`, `editor-remove-tag`, `editor-add-layer`, `editor-remove-layer`
**Overlap:** Four tools all operating on `ProjectSettings/TagManager.asset` via `SerializedObject`. Same input shape (a single string), same backing helper (`LoadTagManager`). They could collapse to two tools with an `action` parameter (`editor-tag(action: "add"|"remove", name)` and `editor-layer(action: "add"|"remove", name)`), or even one (`editor-project-symbol(kind: "tag"|"layer", action: "add"|"remove", name)`).
**Impact:** Medium. Disambiguation is easy because tags vs layers are clearly named, but the count inflates the domain unnecessarily. More importantly, the *missing* counterparts (list/get) are a bigger problem than the redundancy here — see G2 below.
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `editor-get-state` and `editor-info` lack mutual disambiguation
**Location:** `editor-get-state` — `Tool_Editor.GetState.cs` line 26; `editor-info` — `Tool_Editor.Preferences.cs` line 141
**Issue:** Both descriptions describe nearly identical functionality without any "use this when X, not Y" guidance. This is the prototype Rule-3-style ambiguity: an LLM asked "is the editor playing?" will see both as valid candidates.
**Evidence:**
- `editor-get-state`: `"Returns current Editor state: play mode, pause, compiling, current scene, platform, Unity version."`
- `editor-info`: `"Gets Unity Editor information including version, platform, data paths, play mode state, and current scene."`
**Confidence:** high

### A2 — `editor-execute-menu` does not enumerate blocked prefixes
**Location:** `editor-execute-menu` — `Tool_Editor.ExecuteMenu.cs` line 44
**Issue:** The description gives valid examples but does not warn that `File/Build`, `File/Exit`, `File/New Project`, `File/Open Project`, `File/Open Recent` are blocked. An LLM will discover this only by trial and error (and will receive an error mentioning "blocked for security reasons" with no list of what *is* allowed).
**Evidence:** `Description("Executes a Unity Editor menu item by its full path (e.g. 'File/Save Project', 'Assets/Refresh'). Equivalent to clicking the menu item in the Editor.")` — no mention of blocked categories. The block list lives in `_blockedMenuPrefixes` (lines 21-28) and is not surfaced anywhere the LLM sees.
**Confidence:** high

### A3 — `editor-set-pref` description omits the `GameDeck_` prefix constraint
**Location:** `editor-set-pref` — `Tool_Editor.Preferences.cs` line 84
**Issue:** The method enforces that the key must start with `GameDeck_` (line 98-101) and returns an error otherwise, but the `[Description]` says only `"Sets an EditorPrefs value by key."` and the `key` param description says only `"EditorPrefs key to set."`. The LLM has no way to know the prefix is required until it gets an error.
**Evidence:** Description verbatim: `[Description("Sets an EditorPrefs value by key.")]`. Code constraint: `if (!key.StartsWith(PREF_WRITE_PREFIX, StringComparison.Ordinal)) { return ToolResponse.Error($"Only keys with prefix '{PREF_WRITE_PREFIX}' can be written. Got: '{key}'"); }`
**Confidence:** high

### A4 — `editor-set-pref` description is too short (under 15 words)
**Location:** `editor-set-pref` — `Tool_Editor.Preferences.cs` line 84
**Issue:** `"Sets an EditorPrefs value by key."` is 6 words and gives no example, no hint about the prefix restriction (A3), no mention of supported types. Marginal value to an LLM.
**Evidence:** See A3.
**Confidence:** high

### A5 — `editor-get-pref` does not document sensitive-key blocking
**Location:** `editor-get-pref` — `Tool_Editor.Preferences.cs` line 41
**Issue:** Reading keys whose names contain `token`, `secret`, `password`, `license`, `auth`, `credential`, `apikey`, `api_key` is silently rejected (line 54-57). The LLM cannot anticipate this from the description.
**Evidence:** Description: `"Gets an EditorPrefs value by key. Returns the stored string, int, float, or bool value."` — no mention of the sensitive-key filter.
**Confidence:** high

### A6 — `editor-add-layer` defaults the search range without telling the LLM
**Location:** `editor-add-layer` — `Tool_Editor.Tags.cs` line 70
**Issue:** Description says "first empty user layer slot (8-31)" — this is good. But fails when the slot is full and returns an error referencing `8-31` again. That is fine. However, the param description `"Name of the layer to add."` does not mention any constraint on layer name length or characters, and Unity does enforce some constraints. Minor.
**Evidence:** Param description verbatim: `[Description("Name of the layer to add.")]`.
**Confidence:** low

### A7 — `editor-set-active-tool` description has trailing redundancy
**Location:** `editor-set-active-tool` — `Tool_Editor.SetActiveTool.cs` line 26
**Issue:** Acceptable values are listed in both the method `[Description]` and the param `[Description]`. Not strictly an ambiguity issue, just stylistic. Note the description uses `"Sets the active transform tool in the Unity Editor scene view."` — fine, clear, has enumeration.
**Evidence:** Method desc: `"Sets the active transform tool in the Unity Editor scene view. Accepted values: Move, Rotate, Scale, Rect, Transform, View."`. Param desc: `"Tool to activate: Move, Rotate, Scale, Rect, Transform, View. Default 'Move'."`
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `editor-set-active-tool` has a default that is rarely the desired value
**Location:** `editor-set-active-tool` param `toolName`
**Issue:** Defaulting `toolName = "Move"` means if the LLM omits the param the tool silently sets the editor to Move. For an action tool like this, callers virtually always want to pass an explicit value. A defaulted "Move" hides bugs (LLM forgets the param, gets unexpected behavior with no error).
**Current:** `string toolName = "Move"`
**Suggested direction:** make the parameter required (no default), so omission produces a clear validation error.
**Confidence:** medium

### D2 — `editor-get-pref` and `editor-set-pref` default `type` to `"string"` silently
**Location:** `editor-get-pref` / `editor-set-pref` param `type`
**Issue:** The default `"string"` is sensible, but `editor-get-pref` swallows unknown type values into the string branch via the `_ => EditorPrefs.GetString(key)` fallback. If the LLM passes `type = "double"` or `"long"` (Unity doesn't support those in EditorPrefs), the tool silently returns the string-version value rather than reporting an error. This is more an ambiguity-from-default than a default-value bug, but worth noting.
**Current:** `string type = "string"` with a `switch` that has no `default` validation.
**Suggested direction:** when a non-recognised type is passed, return an error listing valid values, rather than falling through to string.
**Confidence:** medium

### D3 — `editor-info` and `editor-get-state` are missing `ReadOnlyHint = true`
**Location:** `editor-info` (Preferences.cs line 140), `editor-get-pref` (Preferences.cs line 40)
**Issue:** Both are pure-read operations. `editor-get-state` is correctly tagged `ReadOnlyHint = true`; `editor-info` and `editor-get-pref` are not. This is a small consistency defect.
**Current:** `[McpTool("editor-info", Title = "Editor / Info")]`, `[McpTool("editor-get-pref", Title = "Editor / Get Preference")]`
**Suggested direction:** add `ReadOnlyHint = true` to both.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Saving the project / current scene from the Editor domain
**Workflow:** A common LLM task is "save my work" — save the current scene and project assets. From the Editor domain, the natural tool would be either a dedicated `editor-save` action or going through `editor-execute-menu("File/Save Project")`.
**Current coverage:** `editor-execute-menu` can invoke `File/Save Project` (the description even uses it as an example). `Editor/Tools/Scene/Tool_Scene.Save.cs` exists for scene saving (cross-domain).
**Missing:** No first-class save action in the Editor domain. The "save project" example in `editor-execute-menu`'s description is the only documented path. This is acceptable but worth noting that menu invocation is the *only* way to trigger File/Save Project programmatically from this domain — and `File/Build`-style entries are blocked, so an LLM might not realise non-Build File/* entries are still allowed.
**Evidence:** `Tool_Editor.ExecuteMenu.cs` line 44 (description) and lines 21-28 (block list — no Save entry blocked).
**Confidence:** medium

### G2 — Cannot list existing Tags or Layers
**Workflow:** Before adding a tag or layer, an LLM should be able to verify what already exists (to avoid duplicates, to match GameObject expectations, to inspect project setup). Unity exposes this via `InternalEditorUtility.tags` and `InternalEditorUtility.layers`, or via the `SerializedObject` already used by `LoadTagManager`.
**Current coverage:** `editor-add-tag`, `editor-add-layer`, `editor-remove-tag`, `editor-remove-layer` — all four are *write* operations. There is **no read tool** for either tags or layers in this domain.
**Missing:** A `editor-list-tags` and `editor-list-layers` (or a unified `editor-list-project-symbols(kind)`) tool. Without it, the LLM has to call `editor-add-tag` and parse the "already exists" response to detect duplicates, and has no way to enumerate existing layers at all.
**Evidence:** Confirmed via cross-domain Grep for `tag-list|list-tags|GetTags|tagsProp|InternalEditorUtility\.tags` — only file matched is `Tool_Editor.Tags.cs` itself, which uses `tagsProp` only inside add/remove paths. No `SortingLayer`-related tools either.
**Confidence:** high

### G3 — Cannot delete or list EditorPrefs keys
**Workflow:** Manage GameDeck-prefixed prefs from the LLM: list current keys, inspect them, clean up stale ones.
**Current coverage:** `editor-get-pref` reads one key. `editor-set-pref` writes one key (with `GameDeck_` prefix gate).
**Missing:** No `editor-delete-pref` (Unity API: `EditorPrefs.DeleteKey(key)`). No `editor-list-prefs` (Unity does NOT expose enumeration of EditorPrefs keys directly, but a "list known GameDeck_* keys" registry stored alongside the prefix gate is feasible — or at minimum delete should exist). Without delete, the LLM can write prefs but cannot clean them up; the only escape is `EditorPrefs.DeleteAll` via menu (catastrophic, blocked or not).
**Evidence:** Grep `EditorPrefs\.DeleteKey|delete-pref` returned only the `Tool_Reflect.CallMethod.cs` reflection helper plus this file's own `HasKey` check. No delete-pref tool exists in any domain.
**Confidence:** high (every file in the Editor domain was read; cross-domain Grep covered `Editor/Tools/`)

### G4 — Undo/Redo cannot scope to a named group
**Workflow:** Group a multi-step LLM operation (e.g. "build a level") under one undo entry, then undo the whole group with one call. Unity exposes `Undo.SetCurrentGroupName(...)`, `Undo.IncrementCurrentGroup()`, `Undo.GetCurrentGroup()`, `Undo.RevertAllInCurrentGroup()`, `Undo.PerformUndo()` repeatedly to a group index.
**Current coverage:** `editor-undo` and `editor-redo` perform a single step each.
**Missing:** No way to start a named undo group, no way to undo/revert "everything since group X", no way to query the current undo group name. For long LLM-driven workflows this matters — a single failed step leaves partial state and the LLM has to call `editor-undo` an unknown number of times.
**Evidence:** Grep `GroupName|RecordGroup|IncrementCurrentGroup|GetCurrentGroupName|RevertAllInCurrentGroup` matched only `Tool_BatchExecute.cs`. The Editor domain itself does not expose any group-aware undo surface.
**Confidence:** high

### G5 — Cannot pause/step a frame in Play mode
**Workflow:** While paused in Play mode, advance one frame to inspect deterministic behaviour. Unity exposes `EditorApplication.Step()` (or menu `Edit/Step`).
**Current coverage:** `editor-pause` toggles pause. `editor-execute-menu("Edit/Step")` would technically work via menu invocation if the LLM knew it.
**Missing:** No first-class `editor-step` tool. Unlike G1, there is no example of the menu path in any description, so the LLM is unlikely to find this on its own.
**Evidence:** Grep `EditorApplication\.Step|step-frame|StepFrame|frame-step` returned zero matches across `Editor/Tools/`.
**Confidence:** high

### G6 — Cannot read or set the pivot/handle mode for the active transform tool
**Workflow:** Switch the scene-view gizmo between Pivot/Center and Local/Global handle rotation. `editor-set-active-tool` selects which tool is active (Move/Rotate/etc), but `Tools.pivotMode` and `Tools.pivotRotation` control how that tool behaves.
**Current coverage:** `editor-set-active-tool` only.
**Missing:** No exposure of `UnityEditor.Tools.pivotMode` (Pivot/Center) or `UnityEditor.Tools.pivotRotation` (Local/Global). These are toggleable in the scene view header.
**Evidence:** Grep `PivotMode|PivotRotation|Tools\.pivotMode|Tools\.pivotRotation|handleRotation` returned zero matches across `Editor/Tools/`.
**Confidence:** medium — this gap is real but may be intentional scope limitation; not every editor knob needs a tool.

### G7 — `editor-add-layer` cannot manage Unity 2D Sorting Layers
**Workflow:** Add a sorting layer for 2D rendering order (separate from the standard layers in slots 8-31).
**Current coverage:** `editor-add-layer` manages `tagManager.layers` only — the standard physics/render layers.
**Missing:** No tool for Sorting Layers (`SortingLayer` API). For a 2D project (the test project Jurassic Survivors is 2D URP per CLAUDE.md), this is a concrete gap.
**Evidence:** Grep `SortingLayer|sorting-layer` returned zero matches across `Editor/Tools/`.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher = address sooner.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | R1 | Redundancy | 5 | 1 | 25 | high | `editor-get-state` vs `editor-info` are near-duplicates with no disambiguation; merge or sharply differentiate. |
| 2 | G2 | Capability Gap | 5 | 1 | 25 | high | No way to list existing Tags or Layers; LLM must guess or trial-add. |
| 3 | A3 | Ambiguity | 5 | 1 | 25 | high | `editor-set-pref` does not mention the mandatory `GameDeck_` key prefix; LLM hits hidden constraint. |
| 4 | G3 | Capability Gap | 4 | 1 | 20 | high | No `editor-delete-pref`; prefs can be created and read but never cleaned up. |
| 5 | A2 | Ambiguity | 4 | 1 | 20 | high | `editor-execute-menu` does not list blocked prefixes; LLM trial-and-errors. |
| 6 | A5 | Ambiguity | 3 | 1 | 15 | high | `editor-get-pref` silently rejects sensitive-key patterns; not documented. |
| 7 | G4 | Capability Gap | 4 | 3 | 12 | high | No named undo groups; long workflows can't be cleanly rolled back. |
| 8 | D3 | Default | 3 | 1 | 15 | high | `editor-info` and `editor-get-pref` missing `ReadOnlyHint = true`. |
| 9 | G5 | Capability Gap | 3 | 1 | 15 | high | No `editor-step` for one-frame advancement during pause. |
| 10 | R3 | Redundancy | 3 | 2 | 12 | medium | Tag/Layer add+remove could collapse to one or two action-dispatch tools. |
| 11 | D1 | Default | 3 | 1 | 15 | medium | `editor-set-active-tool` defaults `toolName` to "Move" silently; should be required. |
| 12 | G7 | Capability Gap | 3 | 2 | 12 | high | No support for Unity 2D Sorting Layers (the test project is 2D URP). |
| 13 | R2 | Redundancy | 2 | 2 | 8 | medium | Play/Pause/Stop could unify under `editor-set-play-state`; small win. |
| 14 | A4 | Ambiguity | 2 | 1 | 10 | high | `editor-set-pref` description under-15-words and uninformative. |
| 15 | D2 | Default | 2 | 1 | 10 | medium | `editor-get-pref` falls through to string for unknown `type` values; should error. |
| 16 | G6 | Capability Gap | 2 | 1 | 10 | medium | No pivotMode / pivotRotation control; minor gap. |
| 17 | G1 | Capability Gap | 2 | 1 | 10 | medium | No first-class save-project tool; menu path works but isn't discoverable for non-Build File items. |
| 18 | A6 | Ambiguity | 1 | 1 | 5 | low | `editor-add-layer` param desc minor. |
| 19 | A7 | Ambiguity | 1 | 1 | 5 | low | `editor-set-active-tool` description duplication; cosmetic. |

**Top action set (highest combined priority):**
1. Disambiguate or merge `editor-get-state` and `editor-info` (R1).
2. Add `editor-list-tags` / `editor-list-layers` (G2).
3. Document the `GameDeck_` prefix in `editor-set-pref` description (A3).
4. Add `editor-delete-pref` (G3).
5. Document blocked menu prefixes in `editor-execute-menu` description (A2).

---

## 7. Notes

- **Cross-domain dependencies:** `editor-execute-menu` is the de facto escape hatch for any Unity menu action this domain doesn't wrap explicitly. That includes `File/Save Project`, `Edit/Step`, etc. The block list in `_blockedMenuPrefixes` is conservative (Build/Exit/New Project/Open Project/Open Recent) and seems reasonable, but the LLM has no way to introspect it. Consider exposing a `editor-list-blocked-menus` read-only helper, or surfacing the list in the description.
- **Security surface:** `editor-set-pref` enforces the `GameDeck_` prefix and `editor-get-pref` blocks sensitive keys. Both are good defensive defaults but completely undocumented in tool descriptions — see A3 and A5. Documentation alone (no code change) would fix this.
- **Inconsistent ReadOnlyHint usage:** Only 1 of 15 tools has `ReadOnlyHint = true` (`editor-get-state`). At minimum `editor-info` and `editor-get-pref` should also be marked. (D3.)
- **Naming inconsistency:** Method is named `PerformUndo` and `PerformRedo` (verbose), tool ID is `editor-undo` / `editor-redo` (terse). This is fine but worth noting that the C# method name does not match the tool ID — readers searching for `editor-undo` in code via grep won't find it without searching the McpTool attribute.
- **Open question for reviewer:** Is the Editor domain meant to be the long-term home for all "Editor automation" tools, or are some of these (notably Tags/Layers and Preferences) candidates for their own domains as the project grows? Consolidation decisions in R3 depend on this.
- **What this audit did NOT cover:** runtime correctness, threading, error-message quality, or whether `EditorApplication.isPlaying = true` is the right way to enter Play mode (it is, but the deprecation status of certain APIs was not checked). Also did not run any builds or `dotnet build`.
