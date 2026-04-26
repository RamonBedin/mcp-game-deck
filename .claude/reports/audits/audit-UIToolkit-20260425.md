# Audit Report — UIToolkit

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/UIToolkit/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 9 (via Glob `Editor/Tools/UIToolkit/Tool_UIToolkit.*.cs`)
- `files_read`: 9
- `files_analyzed`: 9

**Balance:** ✅ balanced

**Errors encountered during audit:**
- One Grep call to `Editor/Tools/Component/Tool_Component.Add.cs` returned `EUNKNOWN: unknown error, uv_spawn`. Recovered by reading the file directly via Read; no impact on findings.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- All 9 domain files analyzed in full. Absence claims (e.g. "no Inspect USS tool", "no element-mutation tool") are made with full coverage of the UIToolkit domain. Cross-domain absence claims (e.g. "no generic file-read tool elsewhere") were verified via project-wide Grep.

**Reviewer guidance:**
- The domain has two clear seams of redundancy: (1) UXML inspection vs. generic file read, and (2) attaching a UIDocument vs. the generic `component-add`. These should be the highest-priority items to discuss.
- Several capability gaps exist between "create a UXML file" and "manipulate a UXML tree" — the LLM today has no structured mutation surface, only blind text overwrite via `update-file`. This is the most consequential workflow problem in the domain.
- A helper `UIToolkitHelper.TryResolveGameObject` exists in `Editor/Tools/Helpers/`; it is referenced by `AttachDocument` and `GetVisualTree` and was read for context but is not part of the tool surface.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `uitoolkit-attach-document` | UI Toolkit / Attach Document | `Tool_UIToolkit.AttachDocument.cs` | 4 | no |
| `uitoolkit-create-panel-settings` | UI Toolkit / Create Panel Settings | `Tool_UIToolkit.CreatePanelSettings.cs` | 4 | no |
| `uitoolkit-create-uss` | UI Toolkit / Create USS | `Tool_UIToolkit.CreateUSS.cs` | 2 | no |
| `uitoolkit-create-uxml` | UI Toolkit / Create UXML | `Tool_UIToolkit.CreateUXML.cs` | 2 | no |
| `uitoolkit-get-visual-tree` | UI Toolkit / Get Visual Tree | `Tool_UIToolkit.GetVisualTree.cs` | 3 | yes |
| `uitoolkit-inspect-uxml` | UI Toolkit / Inspect UXML | `Tool_UIToolkit.InspectUXML.cs` | 1 | no (should be yes) |
| `uitoolkit-list` | UI Toolkit / List Assets | `Tool_UIToolkit.ListUI.cs` | 2 | no (should be yes) |
| `uitoolkit-read-file` | UI Toolkit / Read File | `Tool_UIToolkit.ReadFile.cs` | 1 | yes |
| `uitoolkit-update-file` | UI Toolkit / Update File | `Tool_UIToolkit.UpdateFile.cs` | 2 | no |

**Internal Unity APIs used:**
- `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>`, `AssetDatabase.LoadAssetAtPath<PanelSettings>`, `AssetDatabase.CreateAsset`, `AssetDatabase.SaveAssets`, `AssetDatabase.ImportAsset`, `AssetDatabase.FindAssets("t:VisualTreeAsset" / "t:StyleSheet")`, `AssetDatabase.GUIDToAssetPath`
- `Undo.RecordObject`, `Undo.AddComponent<UIDocument>`, `EditorUtility.SetDirty`
- `UIDocument.visualTreeAsset`, `UIDocument.panelSettings`, `UIDocument.rootVisualElement`
- `PanelSettings.scaleMode`, `PanelSettings.referenceResolution`
- `VisualElement.GetClasses`, `VisualElement.childCount`, `TextElement.text`
- Raw `System.IO.File.ReadAllText` / `File.WriteAllText` / `Directory.CreateDirectory`

---

## 2. Redundancy Clusters

### Cluster R1 — File read/write surface duplicated three times across the codebase
**Members:** `uitoolkit-read-file`, `uitoolkit-update-file`, `uitoolkit-inspect-uxml` (vs. `script-read` / `script-update` / `script-apply-edits` from Script domain)
**Overlap:** `uitoolkit-read-file` is described as reading "UXML, USS, or any text file" — it has no UXML-specific behavior; it is a generic `File.ReadAllText`. `uitoolkit-update-file` is described as overwriting "UXML, USS, or any text file" — also generic. `uitoolkit-inspect-uxml` performs `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>` and then `File.ReadAllText` — i.e. it validates the asset is a VisualTreeAsset and then returns the same text `read-file` would have returned. Three tools converge on "read text content of an asset path." The Script domain already has analogous tools for `.cs` files, so the project-wide pattern is "every file type gets its own read/write tool", which scales poorly.
**Impact:** When the LLM is asked "show me the content of `Assets/UI/HUD.uxml`", it has at least three plausible tools to choose from. Worse, `inspect-uxml` adds a meaningful guard (rejects the call if the asset isn't a `VisualTreeAsset`) but `read-file` does not, so the choice has correctness implications the LLM can't see from descriptions alone.
**Confidence:** high

### Cluster R2 — `uitoolkit-attach-document` vs. generic `component-add`
**Members:** `uitoolkit-attach-document`, `component-add` (Component domain)
**Overlap:** `Component.Add` can attach any Component including `UIDocument`. `AttachDocument` does the same plus assigns `visualTreeAsset` and optionally `panelSettings`. The two halves of `AttachDocument` (add component / set serialized fields) are the same operations covered by `component-add` + a hypothetical `component-update` (which already exists per the Component domain inventory). The tool exists as a convenience macro, which is defensible — but the description does not signal this, so the LLM may pick `component-add` for "add a UIDocument" and then have no idea whether to use `component-update` or `attach-document` for the field assignment.
**Impact:** Disambiguation friction. Less severe than R1 because the macro behavior of `attach-document` is genuinely useful, but the descriptions need to make the "use this when assigning UXML, otherwise use component-add" rule explicit.
**Confidence:** medium

### Cluster R3 — `uitoolkit-create-uxml` / `uitoolkit-create-uss` / `uitoolkit-update-file` (creation vs. write-anywhere)
**Members:** `uitoolkit-create-uxml`, `uitoolkit-create-uss`, `uitoolkit-update-file`
**Overlap:** `update-file` will happily write to a non-existent path (it creates the directory and the file). `create-uxml` and `create-uss` extension-validate (`.uxml` / `.uss`) and provide a default template when `content` is empty. So `update-file` is a strict superset in capability except for (a) extension validation and (b) the default-template fallback. The LLM has to pick: `create-uxml` for the first write of a UXML, then `update-file` for subsequent writes? Or always `update-file` once the file exists? Not signaled by the descriptions.
**Impact:** Medium. The existing structure works but the boundary between "create" and "update" is unclear. A consolidation could fold these into one tool with `if path doesn't exist and content is empty → use template`.
**Confidence:** medium

---

## 3. Ambiguity Findings

### A1 — `uitoolkit-list` lacks ReadOnlyHint
**Location:** `uitoolkit-list` — `Tool_UIToolkit.ListUI.cs` line 21
**Issue:** The tool only reads from AssetDatabase (`FindAssets`, `GUIDToAssetPath`) and never mutates state, but `ReadOnlyHint = true` is missing. By contrast `uitoolkit-get-visual-tree` (line 25) and `uitoolkit-read-file` (line 20) correctly mark themselves read-only.
**Evidence:** `[McpTool("uitoolkit-list", Title = "UI Toolkit / List Assets")]` — no `ReadOnlyHint`.
**Confidence:** high

### A2 — `uitoolkit-inspect-uxml` lacks ReadOnlyHint
**Location:** `uitoolkit-inspect-uxml` — `Tool_UIToolkit.InspectUXML.cs` line 21
**Issue:** Pure read operation (`AssetDatabase.LoadAssetAtPath` + `File.ReadAllText`). Missing `ReadOnlyHint = true`.
**Evidence:** `[McpTool("uitoolkit-inspect-uxml", Title = "UI Toolkit / Inspect UXML")]` — no `ReadOnlyHint`.
**Confidence:** high

### A3 — `uitoolkit-read-file` description does not disambiguate from `uitoolkit-inspect-uxml`
**Location:** `uitoolkit-read-file` — `Tool_UIToolkit.ReadFile.cs` line 21
**Issue:** Both tools read text content, but their descriptions overlap heavily. `read-file` says "Reads the raw text content of a UXML, USS, or any text file"; `inspect-uxml` says "Reads and returns the content of a UXML file. Useful for inspecting existing UI layouts before modifying them." Neither tells the LLM when to prefer one over the other.
**Evidence:** Quoted descriptions above. Compare to the redundancy analysis in R1.
**Confidence:** high

### A4 — `uitoolkit-update-file` description doesn't disambiguate from `uitoolkit-create-uxml` / `uitoolkit-create-uss`
**Location:** `uitoolkit-update-file` — `Tool_UIToolkit.UpdateFile.cs` line 24
**Issue:** "Overwrites the contents of a UXML, USS, or any text file on disk and reimports it" — does not mention when to use this vs. the create-* tools, even though the create-* tools also write files (and the file doesn't have to pre-exist for update-file to succeed, since it calls `Directory.CreateDirectory` first).
**Evidence:** See Cluster R3 above.
**Confidence:** high

### A5 — `uitoolkit-create-panel-settings` `scaleMode` accepts magic strings without enumerating them in method-level description
**Location:** `uitoolkit-create-panel-settings` param `scaleMode` — `Tool_UIToolkit.CreatePanelSettings.cs` line 26 (method `[Description]`)
**Issue:** The method-level `[Description]` says only "Creates a PanelSettings asset at the specified path and configures scale mode and reference resolution." It does not enumerate the valid `scaleMode` values. The per-parameter description does enumerate them, which mitigates this somewhat, but the convention elsewhere (e.g. `asset-create`'s `[Description]` enumerates `'Material', 'RenderTexture', 'PhysicMaterial', 'AnimatorController'`) is to put the enumeration in the method description so the LLM sees it during tool selection.
**Evidence:** Method `[Description]` line 26 vs. param `[Description]` line 29.
**Confidence:** medium

### A6 — `uitoolkit-create-panel-settings` silently coerces unknown `scaleMode` values
**Location:** `uitoolkit-create-panel-settings` — `Tool_UIToolkit.CreatePanelSettings.cs` lines 60-74
**Issue:** The `switch` on `scaleMode` falls through to `ConstantPixelSize` for any value other than the three known strings (lines 71-73: `default: settings.scaleMode = PanelScaleMode.ConstantPixelSize; break;`). If the LLM types a typo like `"ScaleWithScreen"` or `"Constant_Pixel_Size"`, the tool silently accepts it and produces a different result than requested. It also doesn't validate the input string at all — no explicit error returned for unknown values.
**Evidence:** `default:` branch line 71-73; no `ToolResponse.Error` for unknown `scaleMode`.
**Confidence:** high

### A7 — `uitoolkit-list` `type` filter not enumerated in method description
**Location:** `uitoolkit-list` — `Tool_UIToolkit.ListUI.cs` line 22
**Issue:** Method-level `[Description]` says "Lists all UI Toolkit assets (UXML and USS files) in the project, optionally filtered by folder path." Does not mention the `type` parameter at all, even though it's a magic-string filter (`'uxml' | 'uss' | 'all'`).
**Evidence:** Quoted description.
**Confidence:** medium

### A8 — `uitoolkit-attach-document` description glosses over the "update" path
**Location:** `uitoolkit-attach-document` — `Tool_UIToolkit.AttachDocument.cs` line 32
**Issue:** Description says "Adds a UIDocument component to a GameObject and assigns a UXML source asset." The implementation (lines 69-81) detects an existing UIDocument and updates the existing component instead of adding a new one. The LLM cannot infer this: it might call `component-remove` first to "make sure there isn't one", which would needlessly destroy a configured UIDocument (panelSettings, sortingOrder, etc.) before re-adding.
**Evidence:** Lines 69-81 in `AttachDocument.cs` show the branch: `if (doc == null) { ... Undo.AddComponent... } else { Undo.RecordObject... }`.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `uitoolkit-attach-document.uxmlPath` is effectively required but declared with default `""`
**Location:** `uitoolkit-attach-document` param `uxmlPath` — `Tool_UIToolkit.AttachDocument.cs` line 36
**Issue:** Declared as `string uxmlPath = ""` but the tool returns `ToolResponse.Error("uxmlPath is required.")` (line 44) if empty. This is a false-optional: the parameter looks optional in the schema but is never actually optional. Same pattern in other tools (e.g. `path` on `create-panel-settings` is correctly required) — the inconsistency confuses the LLM.
**Current:** `string uxmlPath = ""`
**Suggested direction:** Make `uxmlPath` required (no default), matching how `path` on `create-panel-settings` and `assetPath` on `create-uxml`/`create-uss` are declared.
**Confidence:** high

### D2 — `uitoolkit-attach-document.instanceId` default of `0` doubles as a sentinel
**Location:** `uitoolkit-attach-document` param `instanceId` — `Tool_UIToolkit.AttachDocument.cs` line 34 (and same pattern in `get-visual-tree`)
**Issue:** `instanceId = 0` means "use objectPath instead". This is a magic default — `0` is a real value in some Unity APIs (rare for instance IDs, but the LLM doesn't know that). The dual-input pattern (`instanceId` OR `objectPath`) is also fragile: if both are non-empty, `instanceId` wins, but the LLM may not realize that.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** Either accept a single `target` string parameter (which can be `"id:12345"` or `"path:Canvas/HUD"`), or require exactly one to be set and error on both/neither. Document the precedence in the method-level description if keeping the current shape.
**Confidence:** medium

### D3 — `uitoolkit-create-panel-settings.referenceWidth/Height` defaults are correct for the wrong scale mode
**Location:** `uitoolkit-create-panel-settings` params `referenceWidth`, `referenceHeight` — `Tool_UIToolkit.CreatePanelSettings.cs` lines 30-31
**Issue:** Defaults are `1920` x `1080`, used only when `scaleMode` is `ScaleWithScreenSize`. But the default `scaleMode` is `ConstantPixelSize`, which does not use `referenceResolution`. So in the default-everything call, the `referenceWidth`/`referenceHeight` values are silently ignored. Not strictly wrong, but the LLM may believe it's setting them and be confused later.
**Current:** `string scaleMode = "ConstantPixelSize", int referenceWidth = 1920, int referenceHeight = 1080`
**Suggested direction:** Either (a) flip the default `scaleMode` to `ScaleWithScreenSize` (more common for game UIs) so the reference defaults actually apply, or (b) document in the method `[Description]` that the reference values only matter for `ScaleWithScreenSize`.
**Confidence:** medium

### D4 — `uitoolkit-list.type = "all"` is sensible, but the default for `folderPath = ""` makes a project-wide scan the default behavior
**Location:** `uitoolkit-list` params — `Tool_UIToolkit.ListUI.cs` lines 24-25
**Issue:** Project-wide default scan can return very large lists in real projects. Most Unity projects keep UI under `Assets/UI` or similar. Not a bug, but a default-value choice worth flagging.
**Current:** `string folderPath = "", string type = "all"`
**Suggested direction:** Acceptable to leave, but the description could nudge the LLM ("Provide `folderPath` to scope the search and avoid full-project scans in large projects.").
**Confidence:** low

### D5 — `uitoolkit-update-file.contents` typed as nullable `string?` while `path` is required
**Location:** `uitoolkit-update-file` param `contents` — `Tool_UIToolkit.UpdateFile.cs` line 27
**Issue:** `contents` is declared `string? contents` (nullable, no default), and the tool errors with "contents must not be null" (line 44). This is the only nullable-typed parameter in the domain and stands out as inconsistent. Either it should be required-non-null or have a default empty string.
**Current:** `string? contents`
**Suggested direction:** Drop the `?` and make it required; or add `string contents = ""` and let the empty case be valid (it's a weird-but-legal "truncate file" operation).
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Cannot mutate UXML structurally; only blind text overwrite
**Workflow:** Modify a single element inside an existing UXML — e.g. "change the text of `#HealthLabel` to `Lives: 3`" or "add a new `Button` named `QuitButton` as a child of `#MainMenu`".
**Current coverage:** `uitoolkit-inspect-uxml` returns the raw XML. `uitoolkit-update-file` writes a new full file. There is no element-level mutation.
**Missing:** The LLM must (a) read the entire UXML, (b) re-emit a modified version preserving formatting/whitespace, and (c) write it back. This is fragile — schema mistakes (wrong namespace prefix, missing schema attributes) silently break the asset. No tool wraps `UQueryBuilder`, no tool exposes XDocument-style edits, no tool validates the resulting UXML before writing.
**Evidence:** `Tool_UIToolkit.UpdateFile.cs` line 54 (`File.WriteAllText(path, contents)` — no validation, no parse). `Tool_UIToolkit.InspectUXML.cs` line 46 returns raw text only. Project-wide Grep for `UQuery|UxmlTraits|UxmlFactory|UQueryBuilder` returned zero matches in `Editor/Tools`.
**Confidence:** high

### G2 — No structured USS editing
**Workflow:** Add or modify a single USS rule — e.g. "add a `:hover` style to `.primary-button`" or "change the `background-color` of `.root-container`".
**Current coverage:** `uitoolkit-create-uss` (full file) and `uitoolkit-update-file` (full file overwrite). Same pattern as G1.
**Missing:** No rule-level append/modify. The LLM has to read the whole file and rewrite it, with the same fragility as G1.
**Evidence:** `Tool_UIToolkit.CreateUSS.cs` only writes whole-file content. `Tool_UIToolkit.UpdateFile.cs` is whole-file. Compare to Script domain's `script-apply-edits`, which provides a structured-edit affordance for `.cs` files; no equivalent exists for USS.
**Confidence:** high

### G3 — Cannot inspect a USS file (parallel to InspectUXML missing for USS)
**Workflow:** "Show me the styles in `Assets/UI/MainMenu.uss`."
**Current coverage:** `uitoolkit-read-file` will return the raw text. `uitoolkit-inspect-uxml` exists but only handles UXML.
**Missing:** No `uitoolkit-inspect-uss`. The LLM has to fall back to `read-file`, which works but doesn't validate the asset is actually a `StyleSheet` (the LLM could be reading a stray `.uss.meta` or a wrongly-named file). Symmetry with `inspect-uxml` would help LLM tool selection.
**Evidence:** Glob over `Editor/Tools/UIToolkit/` shows no `Tool_UIToolkit.InspectUSS.cs`. Files: AttachDocument, CreatePanelSettings, CreateUSS, CreateUXML, GetVisualTree, InspectUXML, ListUI, ReadFile, UpdateFile.
**Confidence:** high

### G4 — Cannot link a USS stylesheet to a UXML asset programmatically
**Workflow:** Create `MainMenu.uxml`, create `MainMenu.uss`, then link them so the UXML loads the stylesheet. Standard Unity workflow uses `<Style src="..." />` inside the UXML.
**Current coverage:** `create-uxml` accepts arbitrary content (so the LLM can hand-write the `<Style>` tag). `create-uss` creates the stylesheet. No tool wires them together.
**Missing:** A "link-stylesheet" affordance — given a UXML path and a USS path, inject (or replace) the `<Style src="..." />` element. The LLM today must read the UXML, splice text, and write it back, falling into G1's failure mode.
**Evidence:** No `link`/`add-style` method anywhere in the 9 files.
**Confidence:** high

### G5 — Cannot query elements by name/class without dumping the full tree
**Workflow:** "Find the `Label` named `#Score` in the live UI and tell me its current text."
**Current coverage:** `uitoolkit-get-visual-tree` dumps the entire tree (with text content) up to `maxDepth`.
**Missing:** A targeted query — `uitoolkit-query-element(name | class | type)` returning a single element's properties. For deep trees the dump can be very large and the LLM must parse it; a `Q<Label>("Score")` style API would be far more efficient. Unity exposes `VisualElement.Q<T>(name, className)` for exactly this. No tool wraps it.
**Evidence:** `Tool_UIToolkit.GetVisualTree.cs` is the only runtime-tree tool; it always dumps from root. No `Q`/`Query`/`Find` tool present.
**Confidence:** high

### G6 — Cannot mutate live VisualElement state at runtime (set text, toggle class, hide/show)
**Workflow:** "While the game is running, set the text of `#GameOverLabel` to 'You win'" — common for debugging and AI-driven UI testing.
**Current coverage:** `uitoolkit-get-visual-tree` reads. No write tool exists.
**Missing:** Runtime element mutation (`label.text = ...`, `element.AddToClassList(...)`, `element.style.display = ...`). The LLM cannot drive the UI for test scenarios; it can only read snapshots and edit source files (which require a domain reload). Note: there are legitimate reasons to omit this (changes don't persist), but the absence is worth flagging.
**Evidence:** Search of all 9 files for write operations on `VisualElement` properties: zero matches.
**Confidence:** medium (the gap is real; whether to fill it depends on product intent)

### G7 — Cannot validate UXML/USS syntax before writing
**Workflow:** "Update HUD.uxml; if my edits are malformed, tell me before they break the asset."
**Current coverage:** None. `update-file` writes raw text. `AssetDatabase.ImportAsset` is called but its result is not surfaced.
**Missing:** A pre-write validation step (parse XML, check schema, return parse errors). Compare to Script domain's `script-validate` which provides exactly this for `.cs`. No equivalent for UXML/USS.
**Evidence:** `Tool_UIToolkit.UpdateFile.cs` line 55 — `AssetDatabase.ImportAsset(path);` — return value ignored, no validation. Script domain has `script-validate` but UIToolkit has no analog.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort).

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 4 | 10 | high | No structured UXML mutation; LLM must rewrite whole file every time. |
| 2 | R1 | Redundancy | 4 | 2 | 16 | high | `read-file` / `inspect-uxml` overlap with each other (and with `script-read`); collapse to one with type validation as an option. |
| 3 | A6 | Ambiguity | 4 | 1 | 20 | high | `create-panel-settings` silently coerces unknown `scaleMode` values to `ConstantPixelSize` — return an error instead. |
| 4 | A8 | Ambiguity | 4 | 1 | 20 | high | `attach-document` description hides that it updates an existing UIDocument — LLMs may remove-then-add, destroying state. |
| 5 | G7 | Capability Gap | 4 | 2 | 16 | high | No UXML/USS validate tool (Script domain has `script-validate`; pattern is missing here). |
| 6 | A1+A2 | Ambiguity | 3 | 1 | 15 | high | `uitoolkit-list` and `uitoolkit-inspect-uxml` are read-only but lack `ReadOnlyHint = true`. |
| 7 | G3 | Capability Gap | 3 | 2 | 12 | high | No `inspect-uss` symmetric to `inspect-uxml`. |
| 8 | D1 | Default Issue | 3 | 1 | 15 | high | `attach-document.uxmlPath` is false-optional (default `""` but errors on empty). |
| 9 | R3 | Redundancy | 3 | 3 | 9 | medium | `create-uxml` / `create-uss` / `update-file` boundary unclear; consolidate or document. |
| 10 | G4 | Capability Gap | 3 | 2 | 12 | high | No tool to link USS to UXML — LLM must hand-edit the `<Style>` tag. |
| 11 | G5 | Capability Gap | 3 | 3 | 9 | high | `get-visual-tree` always dumps full tree; no targeted query. |
| 12 | R2 | Redundancy | 2 | 2 | 8 | medium | `attach-document` overlaps `component-add` + `component-update` macro; needs disambiguation in description. |

---

## 7. Notes

- **Cross-domain dependency observed:** `uitoolkit-attach-document` shares territory with `component-add` (Component domain). Any consolidation that removes or merges `attach-document` should be coordinated with the Component domain audit.
- **Cross-domain pattern observed:** Script and UIToolkit both have read/write/create tools for their respective file types. The project as a whole would benefit from a unified `file-read` / `file-write` / `file-validate` surface with a `kind` parameter, but that is out of scope for a single-domain audit. Flagging for the consolidation-planner.
- **`UIToolkitHelper.TryResolveGameObject`** (in `Editor/Tools/Helpers/`) is a healthy shared helper — recommend keeping it as-is.
- **Open question for the reviewer:** Is runtime VisualElement mutation (G6) intentionally out of scope? If yes, document that decision in the review so future audits don't re-flag it. If no, this is a valuable workflow that should be planned.
- **Open question for the reviewer:** Should the file read/write surface (R1) be unified at the project level with the Script domain rather than just simplified within UIToolkit? The cross-domain version is higher-value but more invasive.
- **Limits of this audit:** I did not run UXML/USS examples through the actual Unity importer to verify which malformed inputs would be caught; G7's claim that no validation happens is based on reading the source, not on observed runtime behavior.
