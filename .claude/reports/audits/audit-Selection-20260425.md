# Audit Report — Selection

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Selection/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via `Glob Editor/Tools/Selection/Tool_Selection.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** balanced (2 == 2 == 2)

**Errors encountered during audit:**
- None.

**Files not analyzed:**
- None.

**Absence claims in this report:**
- Permitted under Rule 3 (full coverage of the Selection domain). Cross-domain absence claims (e.g. "no asset-selection tool exists in Asset domain") were verified by `Grep` over `Editor/Tools/` for `Selection.` API calls and tool IDs containing "select"; the only matches that *set* selection are listed in section 7.

**Reviewer guidance:**
- Selection is a small domain (2 tools), but it has very high LLM-traffic potential because almost every workflow ends with "select the thing I just made." A non-trivial finding here is that `gameobject-select` (in the GameObject domain) overlaps heavily with `selection-set`, splitting selection logic across two domains. That cross-domain redundancy is the most important takeaway and is captured in cluster R1.
- The Selection domain has no `Find`/`Filter` tools, no asset-path-based selection, and no way to clear the selection. These gaps are documented in section 5.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `selection-get` | Selection / Get | `Tool_Selection.Get.cs` | 0 | yes |
| `selection-set` | Selection / Set | `Tool_Selection.Set.cs` | 2 (`instanceIds`, `objectPaths`) | no |

**Unity APIs used:**
- `Selection.activeGameObject` (read)
- `Selection.gameObjects` (read)
- `Selection.assetGUIDs` (read)
- `Selection.objects` (write)
- `AssetDatabase.GUIDToAssetPath`
- `EditorUtility.EntityIdToObject`
- `GameObject.Find`

**Cross-domain note:** `Tool_GameObject.Select.cs` defines `gameobject-select` which also writes `Selection.activeGameObject`. It is not part of the Selection domain on disk, but is functionally a third Selection tool. It is treated below as a redundancy partner, not as a Selection-domain inventory entry.

---

## 2. Redundancy Clusters

### Cluster R1 — Two ways to set selection (cross-domain)
**Members:** `selection-set` (Selection domain), `gameobject-select` (GameObject domain)
**Overlap:** Both write `Selection.activeGameObject` / `Selection.objects`. Resolution paths overlap heavily:
- `selection-set` accepts `instanceIds` (CSV ints) and `objectPaths` (CSV hierarchy paths via `GameObject.Find`).
- `gameobject-select` accepts `instanceId` (single int), `objectPath` (single hierarchy path), and `objectName` (single `GameObject.Find` name).

The single-target case (which is the dominant case in practice — e.g. "select the cube I just made") can be served by either tool. An LLM choosing between them must guess: `selection-set` covers multi-selection but no `ping`; `gameobject-select` covers single-selection plus a `ping` flag and a fallback `objectName` field. Neither description tells the LLM which to prefer.

**Impact:** High. Selection is invoked at the end of most workflows. The split forces every prompt that does "create + select" to risk picking the wrong tool. Worse, the only way to set selection AND ping is `gameobject-select`, but that tool can only select ONE object — there is no multi-select-with-ping path at all.

**Confidence:** high (both files read; `Grep` over `Editor/Tools/` confirms these are the only two tools that write to `Selection.*` other than internal "auto-select after create" calls in Audio/Camera/Light/etc.).

---

## 3. Ambiguity Findings

### A1 — `selection-set` does not document deduplication / merge semantics
**Location:** `selection-set` — `Tool_Selection.Set.cs` line 36
**Issue:** The method-level `[Description]` says "Both lists are combined" but does not state that duplicates are silently deduplicated by instance ID, nor that warnings for unresolved entries are appended to a successful response (line 140-144). XML doc says "merged and deduplicated" but XML docs are NOT what the LLM sees — only the `[Description]` string is.
**Evidence:** `[Description("Sets the Editor selection to specific GameObjects. Provide instance IDs (comma-separated integers) and/or hierarchy paths (comma-separated strings). Both lists are combined. At least one valid GameObject must be resolved.")]`
**Confidence:** high

### A2 — `selection-set` `objectPaths` description does not clarify `GameObject.Find` behavior
**Location:** `selection-set` param `objectPaths` — line 39
**Issue:** Description says "comma-separated hierarchy paths" with example `'Player,World/Terrain'` but does not state that `GameObject.Find` (a) only finds active GameObjects, (b) returns the first match if the path is ambiguous, and (c) does not search across DontDestroyOnLoad scenes. An LLM trying to select an inactive GameObject (very common after disabling) will silently fail with "GameObject 'X' not found" and have no way to know why.
**Evidence:** XML doc on line 28-29 mentions `GameObject.Find` but the LLM-facing `[Description]` does not.
**Confidence:** high

### A3 — `selection-set` instance-ID-or-path semantics not disambiguated vs. `gameobject-select`
**Location:** `selection-set` — `Tool_Selection.Set.cs` line 36
**Issue:** No "use this when X, not Y" clause directing the LLM toward `selection-set` for multi-target and `gameobject-select` for single-target-with-ping. Per Phase 3 protocol, when 2+ tools overlap in purpose, descriptions must contain such a clause. Neither tool has one.
**Evidence:** Both descriptions describe what they do in isolation; neither references the other.
**Confidence:** high

### A4 — `selection-get` output format is undocumented
**Location:** `selection-get` — `Tool_Selection.Get.cs` line 31
**Issue:** Description states the tool returns three sections but does not show the exact format (e.g. `"Active GameObject: <name> (instanceId: <id>)"`). An LLM that wants to parse the output to chain into another tool has to guess the format, or call the tool once just to see the shape. This is a small but real inefficiency for "get selected, then act on it" flows.
**Evidence:** Description does not include a sample line like `"Active GameObject: Player (instanceId: 12345)"`.
**Confidence:** medium (this is borderline — output format documentation is rarely included in tool descriptions across the codebase, so this may be a project-wide convention rather than a Selection-specific issue).

---

## 4. Default Value Issues

### D1 — `selection-set` makes both inputs optional with empty defaults but requires at least one
**Location:** `selection-set` params `instanceIds = ""` and `objectPaths = ""`
**Issue:** Both parameters default to `""`. If the LLM calls `selection-set()` with no arguments (a very plausible misuse — "clear the selection"), the tool errors out at line 127 with `"No valid GameObjects could be resolved..."`. There is no documented way to clear the selection (set `Selection.objects = new Object[0]`). Either:
- Make a no-arg call clear the selection (most ergonomic), OR
- Add an explicit `clear: bool` parameter, OR
- Document loudly that no-arg is invalid AND add a separate `selection-clear` tool.

**Current:** `SetSelection(string instanceIds = "", string objectPaths = "")`
**Suggested direction:** Decide intent. If "clear" is a desired capability (it should be — common Unity Editor workflow), it needs a tool. Right now no-arg silently fails with an error response.
**Confidence:** high

### D2 — CSV-encoded list parameters are awkward for callers
**Location:** `selection-set` both params
**Issue:** Not strictly a *default* issue, but adjacent. Both list parameters are encoded as comma-separated strings rather than `int[]` / `string[]`. This is a project-wide pattern (also seen in `gameobject-find` etc.), but it interacts badly with CSV inputs that contain commas in path names (e.g. a GameObject literally named `"foo,bar"`). No escape/quoting is documented.
**Current:** `string instanceIds = ""`, `string objectPaths = ""`
**Suggested direction:** If MCP supports array params (it does — see `gameobject-find`'s scalar approach is one of several patterns in the codebase), consider migrating to `int[] instanceIds = null, string[] objectPaths = null`. Out of scope for description-level fixes; flagged for the planner.
**Confidence:** medium (project-wide convention may justify keeping CSV; raise with reviewer)

---

## 5. Capability Gaps

### G1 — Cannot clear the selection
**Workflow:** Unity dev wants to deselect everything (e.g. before taking a screenshot of the scene view, or before driving the Inspector to a known state). `Selection.activeObject = null` and `Selection.objects = new Object[0]`.
**Current coverage:** None. `selection-set` requires at least one resolved GameObject (line 125-128). `selection-get` is read-only. `gameobject-select` requires a target.
**Missing:** A way to set selection to empty. Either a `selection-clear` tool, a `clear: bool` parameter on `selection-set`, or treating no-arg `selection-set` as clear.
**Evidence:** `Tool_Selection.Set.cs` lines 125-128 explicitly returns an error when `resolved.Count == 0`.
**Confidence:** high

### G2 — Cannot select assets (only GameObjects)
**Workflow:** "Select the Player.prefab in the Project window so I can inspect its import settings." `Selection.objects` accepts any `Object`, not just GameObjects, and `selection-get` even *reads* selected assets (via `Selection.assetGUIDs`) — but `selection-set` cannot *write* asset selections.
**Current coverage:** `selection-get` reads selected asset paths. `selection-set` only resolves via `EditorUtility.EntityIdToObject` (works for assets in theory) and `GameObject.Find` (GameObjects only). The instance-ID path *might* work for assets if the LLM happens to know the asset's instance ID, but there is no path-based way (e.g. `Assets/Prefabs/Player.prefab`) and the docstring/XML strongly imply GameObjects only.
**Missing:** Asset selection by asset path (e.g. `AssetDatabase.LoadMainAssetAtPath`) or by GUID.
**Evidence:** `Tool_Selection.Set.cs` resolution paths: line 67 (`EntityIdToObject` — could work for assets but undocumented) and line 102 (`GameObject.Find` — GameObjects only). Method-level XML doc line 17 says "the specified GameObjects". Description line 36 says "specific GameObjects". No tool in the Selection or Asset domain accepts an asset path and selects it.
**Confidence:** high (verified by `Grep Selection\.objects|Selection\.activeObject` over `Editor/Tools/Asset` returning no matches, and full read of Selection domain.)

### G3 — Cannot select inactive GameObjects by path
**Workflow:** "Select the disabled UI panel I just toggled off so I can re-enable it." `GameObject.Find` (used at line 102) does NOT find inactive GameObjects.
**Current coverage:** `selection-set` via `objectPaths` uses `GameObject.Find` only. `selection-set` via `instanceIds` works if the LLM remembers the ID. `gameobject-select` has the same `GameObject.Find` limitation as a fallback.
**Missing:** A path-based resolver that searches inactive objects too. `gameobject-find` (in GameObject domain) DOES support `includeInactive`, but its outputs are name+instanceId pairs that the LLM must then thread into `selection-set` — adding an extra tool call to a workflow that should be one step.
**Evidence:** `Tool_Selection.Set.cs` line 102: `var go = GameObject.Find(trimmed);` — `GameObject.Find` skips inactive objects per Unity docs. No `includeInactive` parameter exists on `selection-set`.
**Confidence:** high

### G4 — No filter / type-based selection
**Workflow:** "Select all cameras in the scene." or "Select all selected child GameObjects with a Rigidbody." `Selection.GetFiltered<T>(SelectionMode)` is the standard Unity API.
**Current coverage:** None. The LLM must `gameobject-find by_component Camera`, then `selection-set` with the returned IDs. Two tool calls plus IDs to thread through.
**Missing:** `Selection.GetFiltered` or a thin wrapper that selects by component type or selection mode. Lower-impact than G1/G2 but mentioned for completeness.
**Evidence:** `Grep Selection\.GetFiltered` over `Editor/Tools/` returns no matches.
**Confidence:** high

### G5 — No "ping current selection" or "frame selection" capability
**Workflow:** After selecting something programmatically, the user often wants the editor to scroll the Hierarchy / Project window to show it (`EditorGUIUtility.PingObject`) or frame it in the Scene view (`SceneView.FrameLastActiveSceneView`).
**Current coverage:** `gameobject-select` supports `ping` for a single target. `selection-set` does not.
**Missing:** Multi-target ping, scene-view-frame, or a unified "reveal selection in editor" capability after a multi-select.
**Evidence:** `Tool_Selection.Set.cs` writes `Selection.objects` (line 130) but never calls `EditorGUIUtility.PingObject` or `SceneView.FrameLastActiveSceneView`.
**Confidence:** medium (this is convenience, not a hard capability gap — the user can still see the selection in the Inspector without ping; raise with reviewer to weigh.)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | R1 | Redundancy | 5 | 2 | 20 | high | `selection-set` and `gameobject-select` overlap heavily; LLM has to guess which to use. |
| 2 | G1 | Capability Gap | 4 | 1 | 20 | high | No way to clear the selection — required for screenshot/clean-state workflows. |
| 3 | G2 | Capability Gap | 5 | 2 | 20 | high | Cannot select assets by path; `selection-get` reads them but `selection-set` cannot write them. |
| 4 | G3 | Capability Gap | 4 | 2 | 16 | high | Cannot select inactive GameObjects by path; forces a fallback round-trip via `gameobject-find`. |
| 5 | A3 | Ambiguity | 4 | 1 | 20 | high | Neither selection tool tells the LLM when to prefer the other. |
| 6 | A1 | Ambiguity | 3 | 1 | 15 | high | `selection-set` description omits dedup / partial-warning behavior. |
| 7 | A2 | Ambiguity | 3 | 1 | 15 | high | `selection-set` does not disclose `GameObject.Find` cannot find inactive objects. |
| 8 | D1 | Defaults | 4 | 2 | 16 | high | No-arg `selection-set` errors instead of clearing — couples to G1. |
| 9 | G4 | Capability Gap | 2 | 3 | 6 | high | No `Selection.GetFiltered` wrapper for type-based selection. |
| 10 | A4 | Ambiguity | 2 | 1 | 10 | medium | `selection-get` output format not documented in description. |
| 11 | G5 | Capability Gap | 2 | 2 | 8 | medium | No multi-target ping / scene-view-frame after `selection-set`. |
| 12 | D2 | Defaults | 2 | 4 | 4 | medium | CSV list params are project-wide convention but awkward; defer to planner. |

Top three (R1, G1, G2) all share priority 20 and should be addressed together by the planner — they are interlocking. Resolving R1 by consolidating into a single Selection tool naturally creates the surface for G1 (clear) and G2 (asset selection).

---

## 7. Notes

**Cross-domain dependency — important for the planner:**
`Tool_GameObject.Select.cs` (file at `Editor/Tools/GameObject/Tool_GameObject.Select.cs`) is functionally a third Selection tool. Any consolidation plan must decide whether to:
- (a) move `gameobject-select` into the Selection domain (rename to `selection-set` and absorb `ping` + `objectName` fallback), OR
- (b) keep `gameobject-select` as a single-object convenience and have it explicitly delegate to `selection-set`, OR
- (c) deprecate one in favor of the other.

I have NOT picked a direction — that's the consolidation-planner's job. But the planner needs to know this file exists and is part of the same conceptual surface.

**Cross-domain "auto-select after create" pattern:**
The following tools write `Selection.activeGameObject` directly after creating something:
- `Tool_GameObject.Create.cs` line 126
- `Tool_GameObject.Duplicate.cs` line 57
- `Tool_Prefab.Instantiate.cs` line 85
- `Tool_Camera.Create.cs` line 68
- `Tool_Light.Create.cs` line 91
- `Tool_Audio.Create.cs` line 84
- `Tool_AddAssetToScene.cs` line 89
- `Tool_BatchExecute.cs` line 238

This is a healthy pattern (auto-select on create is good UX) and is NOT a redundancy with `selection-set` — it's an embedded side effect, not a user-facing tool. Flagged here for awareness only.

**Open questions for the reviewer:**
- Is "clear selection" a workflow Ramon wants supported? (Affects priority of G1.)
- Is the CSV-list param convention something to challenge here, or accept domain-wide? (Affects D2.)
- Should `gameobject-select` be folded into the Selection domain entirely? (Affects R1's resolution shape.)

**Limits of this audit:**
- I did not verify what other tool-auditor reports have said about `gameobject-select` (no prior audits exist for the GameObject domain at `.claude/reports/audits/` per the directory layout). The planner should cross-reference if/when the GameObject audit runs.
- I did not exercise the tools at runtime — all findings are static.
