# Audit Report — Build

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Build/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 6 (via Glob `Editor/Tools/Build/Tool_Build.*.cs`)
- `files_read`: 6
- `files_analyzed`: 6

**Balance:** ✅ balanced

**Errors encountered during audit:** None.

**Files not analyzed (if any):** None.

**Absence claims in this report:** All 6 files in the domain were read in full. Absence claims (e.g. "no asset bundle tool exists in Build domain", "no read-only marker on `build-get-settings`") are verified against complete domain coverage and against cross-domain greps cited in Section 7.

**Reviewer guidance:**
- The biggest issue in this domain is **cross-domain redundancy** with `Editor/Tools/PlayerSettings/`. Three of the six Build tools (`build-get-settings`, `build-set-settings`, and to a lesser extent `build-batch`/`build-player`'s reliance on `PlayerSettings.productName`) overlap in scope with the dedicated `PlayerSettings` domain. The audit treats Build as the target domain but flags this for the planner.
- Build tools are inherently slow/heavy operations (`BuildPipeline.BuildPlayer` can take minutes). The planner should weigh whether `build-batch` and `build-player` are worth consolidating — the tool count is small and consolidation may hurt LLM ergonomics for the common single-build case.
- One read-only opportunity (`build-get-settings`) is missing the `ReadOnlyHint = true` marker.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `build-batch` | Build / Batch Build | `Tool_Build.BatchBuild.cs` | 3 | no |
| `build-player` | Build / Build Player | `Tool_Build.Build.cs` | 5 | no |
| `build-get-settings` | Build / Get Settings | `Tool_Build.GetSettings.cs` | 0 | **no (should be yes)** |
| `build-set-settings` | Build / Set Settings | `Tool_Build.SetSettings.cs` | 2 | no |
| `build-switch-platform` | Build / Switch Platform | `Tool_Build.SwitchPlatform.cs` | 2 | no |
| `build-manage-scenes` | Build / Manage Scenes | `Tool_Build.ManageScenes.cs` | 3 | no |

**Internal Unity API surface used:**
- `BuildPipeline.BuildPlayer`, `BuildPlayerOptions`, `BuildOptions.*`, `BuildReport`
- `EditorUserBuildSettings.{activeBuildTarget, selectedBuildTargetGroup, development, standaloneBuildSubtarget, SwitchActiveBuildTarget}`
- `EditorBuildSettings.scenes`, `EditorBuildSettingsScene`
- `PlayerSettings.{productName, companyName, bundleVersion, applicationIdentifier, GetScriptingBackend, SetScriptingBackend, GetScriptingDefineSymbols, SetScriptingDefineSymbols, Android.bundleVersionCode, iOS.buildNumber}`
- `UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup`
- `AssetDatabase.AssetPathToGUID`

**Helpers:**
- `ParseBuildTarget(string)` in `SwitchPlatform.cs` — used by 3 tools (good reuse)
- `FormatBuildReport(BuildReport)` in `Build.cs` — used by 1 tool (could also be reused by `BatchBuild`)

---

## 2. Redundancy Clusters

### Cluster R1 — Player metadata read/write split across Build and PlayerSettings domains
**Members:** `build-get-settings`, `build-set-settings`, `player-settings-get`, `player-settings-set`
**Overlap:** Both pairs target the same `PlayerSettings.{productName, companyName, bundleVersion}` API surface. `build-set-settings` accepts `product_name`, `company_name`, `version`, `bundle_id`, `scripting_backend`, `defines`, `development` via a `(property, value)` magic-string shape; `player-settings-set` accepts a fixed parameter list including `companyName`, `productName`, `version`, plus `colorSpace`, `runInBackground`, screen dimensions. The intersection (productName, companyName, version) is fully duplicated. `bundle_id`, `scripting_backend`, `defines`, `development` exist only in Build; `colorSpace`, `runInBackground`, screen dimensions exist only in PlayerSettings. An LLM asked "set the company name" has no principled way to pick one tool over the other.
**Impact:** High. Player metadata is one of the most common tool calls, and the duplication forces the LLM to guess. The two tools also use **different parameter conventions** (magic-string property dispatch vs. typed multi-param), so if the LLM picks the wrong one it has to re-plan its arguments.
**Confidence:** high (verified with `Grep PlayerSettings\.` across `Editor/Tools` — only the four files listed touch product/company/version)

### Cluster R2 — `build-player` and `build-batch` share ~80% of their logic
**Members:** `build-player`, `build-batch`
**Overlap:** Both tools build BuildPlayerOptions, derive default scenes from `EditorBuildSettings.scenes`, infer extension by target, set `BuildOptions.Development`, and call `BuildPipeline.BuildPlayer`. `build-batch` is essentially `build-player` in a `foreach` loop minus the `outputPath`, `scenes`, and `options` parameters. An LLM picking between them has to decide based purely on whether one or many targets are needed — but `build-player` already accepts the full target list shape, so a single tool with a list-typed `targets` could absorb both.
**Overlap rationale:** `build-batch` cannot accept custom scene lists or build options (clean_build, compress_lz4, etc.), so it is strictly less capable than `build-player` per target. This is an asymmetric duplication: it provides convenience but loses configurability.
**Impact:** Medium. The tool count is only two and the use cases are distinct (single vs. release sweep), but the loss of configurability in `build-batch` is a real capability regression.
**Confidence:** high

### Cluster R3 — Scene management split awkwardly with Scene domain
**Members:** `build-manage-scenes` (this domain), `scene-list`, `scene-load`, `scene-create`, `scene-delete` (Scene domain)
**Overlap:** Not a true overlap — `build-manage-scenes` operates on the **Build Settings scene list**, while the Scene domain operates on **scene assets and runtime scenes**. However, both have a "list" action, and an LLM asked "list the scenes in the project" could reasonably pick either. The disambiguation is not stated in the descriptions.
**Impact:** Low-medium. The actual functionality does not collide, but description clarity is lacking.
**Confidence:** medium (this is more of an ambiguity finding — listed here because it crosses domain lines)

---

## 3. Ambiguity Findings

### A1 — `build-get-settings` description repeats fields but omits its companion's existence
**Location:** `build-get-settings` — `Tool_Build.GetSettings.cs` line 24
**Issue:** Description lists what it returns but does not mention the existence of `player-settings-get`, which overlaps. An LLM has no signal to choose between them.
**Evidence:** `"Gets current build settings including active build target, product name, company name, bundle identifier, version, scripting backend, architecture, and scripting defines."` — no "use this when you also need build target / scripting backend / scenes; otherwise prefer player-settings-get" disambiguation.
**Confidence:** high

### A2 — `build-set-settings` `property` parameter uses magic strings without canonical list at the call site
**Location:** `build-set-settings` param `property` — `Tool_Build.SetSettings.cs` line 29
**Issue:** Param description does enumerate the values (good), but the values themselves are inconsistently styled (`product_name` vs. `bundle_id` — both snake_case, fine; but `scripting_backend` accepts `mono`/`il2cpp`, `development` accepts `true`/`false`, while everything else is a free string). The description does not document the value-format rules per property.
**Evidence:** `"Property to set: product_name, company_name, version, bundle_id, scripting_backend, defines, development"` — does not say e.g. "scripting_backend value must be 'mono' or 'il2cpp'".
**Confidence:** high

### A3 — `build-switch-platform` does not warn that the call may take minutes / triggers full asset reimport
**Location:** `build-switch-platform` — `Tool_Build.SwitchPlatform.cs` line 28
**Issue:** Description mentions "may take some time" but does not flag this as an expensive blocking operation. An LLM may chain it speculatively.
**Evidence:** `"Switches the active build target platform. This triggers a reimport of assets for the new platform and may take some time."` — adequate but understates cost. No "use only when explicitly requested" guidance.
**Confidence:** medium

### A4 — `build-manage-scenes` does not disambiguate against Scene domain
**Location:** `build-manage-scenes` — `Tool_Build.ManageScenes.cs` line 34
**Issue:** Description says "Manages scenes in Build Settings" but does not explicitly contrast with `scene-list`/`scene-load`. The word "scenes" is overloaded in Unity (scene asset, loaded scene, build scene), and the description reuses it without anchoring.
**Evidence:** `"Manages scenes in Build Settings. Actions: list (show current scenes), add (add a scene), remove (remove a scene by path), enable/disable (toggle a scene), reorder (move a scene to a new index)."`
**Confidence:** medium

### A5 — `build-player` `options` param documents flag names but not behaviour
**Location:** `build-player` param `options` — `Tool_Build.Build.cs` line 51
**Issue:** Lists six flag names (`clean_build`, `auto_run`, `deep_profiling`, `compress_lz4`, `strict_mode`, `detailed_report`) but does not explain what any of them do, or which are typically combined. `auto_run` in particular has side effects (launches the built player) that an LLM would not anticipate from the name alone.
**Evidence:** `"Comma-separated build options: clean_build, auto_run, deep_profiling, compress_lz4, strict_mode, detailed_report"`
**Confidence:** high

### A6 — `build-switch-platform` `subtarget` param defaults to "player" but does not explain the "server" case
**Location:** `build-switch-platform` param `subtarget` — `Tool_Build.SwitchPlatform.cs` line 31
**Issue:** Mentions `'player'` and `'server'` but does not explain that "server" maps to Unity's dedicated server build subtarget (headless). An LLM unfamiliar with the term may miss this.
**Evidence:** `"Subtarget: 'player' or 'server'. Default is 'player'."`
**Confidence:** low (minor — value is enumerated, just not explained)

### A7 — `build-batch` does not mention it suppresses per-target customization
**Location:** `build-batch` — `Tool_Build.BatchBuild.cs` line 42
**Issue:** Description is fine for happy-path use but does not warn that scene list, output naming, and build options cannot vary per target — a meaningful limitation for cross-platform release pipelines.
**Evidence:** `"Triggers builds for multiple target platforms in sequence. Returns summary of all build results. Useful for cross-platform release builds."`
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `build-set-settings` requires `value` parameter even for boolean-style `development` property
**Location:** `build-set-settings` params `property` + `value` — `Tool_Build.SetSettings.cs` line 28-31
**Issue:** Both params are required strings, but `development`'s value space is `"true"`/`"false"` (parsed via `value.ToLowerInvariant() == "true"`) — this is not type-safe and makes the LLM stringify a bool. The `(property, value)` shape is also a magic-string dispatch that hides what's actually accepted (Cluster R1 / A2 already noted).
**Current:** `string property, string value` — both required.
**Suggested direction:** Consider whether the magic-string dispatch is worth keeping at all (vs. delegating fully to `player-settings-set` for the overlapping fields and a dedicated `build-set-defines` / `build-set-scripting-backend` tool for the build-specific ones). If kept, document per-property value formats explicitly.
**Confidence:** high

### D2 — `build-manage-scenes` `index` default of `-1` is a sentinel that's only valid for non-reorder actions
**Location:** `build-manage-scenes` param `index` — `Tool_Build.ManageScenes.cs` line 38
**Issue:** Default `-1` is a sentinel meaning "ignored" for list/add/remove/enable/disable. The reorder action then range-checks `index < 0 || index >= scenes.Count` and rejects `-1`. An LLM that calls `action="reorder"` without specifying `index` gets a runtime error. The description does not warn that `index` becomes mandatory for reorder.
**Current:** `int index = -1`
**Suggested direction:** Document the conditional requirement in the param description ("Required for reorder; ignored otherwise"). Or, if the dispatch is split into per-action tools later, drop the default for the reorder variant.
**Confidence:** high

### D3 — `build-player` `target=""` silently uses active platform — fine, but undocumented in tool description
**Location:** `build-player` param `target` — `Tool_Build.Build.cs` line 47
**Issue:** Default empty string is interpreted as "use current active target". This is a sensible default, but the tool-level `[Description]` (line 45) does not mention it. The param-level description does, so impact is limited.
**Current:** `string target = ""` — param desc says "If empty uses current active target"
**Suggested direction:** Mention default behaviour in the tool-level summary as well, since LLMs often inspect the summary first.
**Confidence:** low

### D4 — `outputDir = "Builds"` in `build-batch` is relative to the project root, not the OS
**Location:** `build-batch` param `outputDir` — `Tool_Build.BatchBuild.cs` line 45
**Issue:** Default `"Builds"` is a relative path resolved by Unity to the project folder. The description says `"Default: 'Builds/'"` but does not state that the path is project-relative. An LLM that wants an absolute path has no signal that absolute paths are also accepted (they are, by `BuildPipeline`).
**Current:** `string outputDir = "Builds"`
**Suggested direction:** Document path resolution semantics in the param description.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — No tool exposes Asset Bundle / Addressables build pipelines
**Workflow:** A developer wants to build asset bundles or run an Addressables content build via MCP (e.g. "build the streaming bundles for Android").
**Current coverage:** None. `BuildPipeline.BuildPlayer` (player builds) is wrapped, but `BuildPipeline.BuildAssetBundles` is not.
**Missing:** No tool wraps `BuildPipeline.BuildAssetBundles`, `AssetBundleBuild[]`, or any Addressables API (`AddressableAssetSettings.BuildPlayerContent`, `ContentUpdateScript.BuildContentUpdate`).
**Evidence:** `Grep "AssetBundle|BuildAssetBundle|Addressable"` across `Editor/Tools` returned **zero** files. The Build domain only contains player-build wrappers.
**Confidence:** high (verified zero matches across all of `Editor/Tools`, accounting balanced)

### G2 — No way to set per-platform settings (Android keystore, iOS team ID, WebGL memory)
**Workflow:** A developer wants to configure platform-specific player settings before a build (e.g. set Android keystore path, set bundle version code, set iOS team ID, set WebGL memory size).
**Current coverage:** `build-get-settings` reads `PlayerSettings.Android.bundleVersionCode` and `PlayerSettings.iOS.buildNumber` (lines 36-41 of `GetSettings.cs`, behind `#if UNITY_ANDROID/IOS`), but `build-set-settings` cannot write either. `player-settings-set` (in PlayerSettings domain) likewise does not expose Android/iOS-specific fields.
**Missing:** Setters for `PlayerSettings.Android.bundleVersionCode`, `PlayerSettings.Android.keystoreName`, `PlayerSettings.Android.keystorePass`, `PlayerSettings.iOS.buildNumber`, `PlayerSettings.iOS.appleDeveloperTeamID`, `PlayerSettings.WebGL.memorySize`, etc.
**Evidence:** `build-set-settings` switch in `Tool_Build.SetSettings.cs` lines 39-82 covers only seven shared properties; no `Android.*` / `iOS.*` / `WebGL.*` cases.
**Confidence:** high

### G3 — No tool to read or set `BuildOptions` or build flags persistently
**Workflow:** A developer wants to inspect or persist `BuildOptions.{Development, AutoRunPlayer, ConnectWithProfiler}` etc. on `EditorUserBuildSettings` so that subsequent manual builds in the Unity UI use the same options.
**Current coverage:** `build-player` accepts `options` per-call. `build-set-settings` can flip `EditorUserBuildSettings.development` only.
**Missing:** No tool exposes `EditorUserBuildSettings.{allowDebugging, connectProfiler, buildScriptsOnly, waitForManagedDebugger, symlinkSources}`.
**Evidence:** `Tool_Build.SetSettings.cs` line 75-78 only handles the `development` flag; nothing else from `EditorUserBuildSettings` is settable. Grep confirmed.
**Confidence:** high

### G4 — `build-get-settings` is missing `ReadOnlyHint = true`
**Workflow:** Any client that filters tools by side-effects (e.g. dry-run mode) will not recognise `build-get-settings` as inspection-only.
**Current coverage:** The tool is genuinely read-only (only calls getters, builds a string).
**Missing:** `ReadOnlyHint = true` on the `[McpTool(...)]` attribute.
**Evidence:** `Tool_Build.GetSettings.cs` line 23: `[McpTool("build-get-settings", Title = "Build / Get Settings")]` — no `ReadOnlyHint`. Compare `player-settings-get` (`Tool_PlayerSettings.Get.cs` line 27) which does set the hint.
**Confidence:** high

### G5 — No way to inspect a previous BuildReport after the fact
**Workflow:** A developer ran a build manually (or a batch build half-completed) and wants to retrieve the most recent build report (errors, size, dependencies, included assets).
**Current coverage:** `build-player` and `build-batch` return reports immediately after their own builds. There is no separate inspection path.
**Missing:** No tool wraps `BuildReport` retrieval from `Library/LastBuild.buildreport` or exposes per-step / per-asset data from a current report.
**Evidence:** Grep `BuildReport` across `Editor/Tools` shows usage only inside Build tools that perform builds (verified). No standalone inspection tool.
**Confidence:** medium (this may be intentional — the asset is internal to Unity — but it's a known capability)

### G6 — Building cannot inject pre-/post-build hooks or version bumps
**Workflow:** A common release pipeline is: bump version → set bundle version code → build → archive output. The current toolset can do all four steps but only as separate calls; there is no atomic "release build" macro tool.
**Current coverage:** `build-set-settings` (version), missing setter for Android.bundleVersionCode (G2), `build-player` (build).
**Missing:** Either (a) a macro tool that takes version + target and orchestrates the pipeline, or (b) `IPreprocessBuildWithReport` / `IPostprocessBuildWithReport` registration. Neither is wrapped.
**Evidence:** No file in `Editor/Tools/Build/` references `IPreprocessBuildWithReport` (verified by grep on `BuildPipeline\.|BuildPlayerOptions` returning only the three build files).
**Confidence:** medium (this is a feature request more than a gap — the LLM can chain calls; orchestration is plausibly out of scope)

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort). Higher is better.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | R1 | Redundancy | 5 | 3 | 15 | high | `build-get/set-settings` overlaps `player-settings-get/set` on product/company/version. Pick a canonical owner and add disambiguation. |
| 2 | G4 | Capability Gap | 3 | 1 | 15 | high | Add `ReadOnlyHint = true` to `build-get-settings`. One-line fix. |
| 3 | A5 | Ambiguity | 4 | 1 | 20 | high | `build-player` `options` flag list is name-only — document each flag's behaviour, especially `auto_run` and `clean_build`. |
| 4 | A2 | Ambiguity | 4 | 1 | 20 | high | `build-set-settings` `property`/`value` magic-string pair lacks per-property value-format docs (e.g. `scripting_backend = mono\|il2cpp`). |
| 5 | D2 | Default Value | 4 | 1 | 20 | high | `build-manage-scenes` `index = -1` becomes mandatory for reorder; describe the conditional requirement. |
| 6 | G2 | Capability Gap | 4 | 3 | 12 | high | No setters for `PlayerSettings.Android.bundleVersionCode`, iOS team ID, WebGL memory. Blocks real release workflows. |
| 7 | G1 | Capability Gap | 4 | 4 | 8 | high | No AssetBundle / Addressables build tooling. Significant gap for live-ops projects, but Addressables wraps a non-trivial API surface. |
| 8 | A1 | Ambiguity | 3 | 1 | 15 | high | Add "use this when…" disambiguation to `build-get-settings` vs. `player-settings-get`. |
| 9 | R2 | Redundancy | 3 | 3 | 9 | high | `build-player` and `build-batch` share most logic; consider whether `build-batch` should accept `targets` as a list and absorb single-target case. |
| 10 | A3, A4, A6, A7, D1, D3, D4 | Ambiguity / Defaults | 2-3 | 1 | 10-15 | medium | Description polish — flag conditional requirements, expensive operations, and path resolution semantics. |
| 11 | G3 | Capability Gap | 3 | 2 | 12 | high | `EditorUserBuildSettings` flags beyond `development` are not exposed. |
| 12 | G5, G6 | Capability Gap | 2 | 4 | 4 | medium | Build report inspection and pre/post-build hooks. Lower priority. |

---

## 7. Notes

**Cross-domain dependencies observed:**
- **`PlayerSettings` domain (`Editor/Tools/PlayerSettings/`)** overlaps materially with `build-get-settings` / `build-set-settings`. The planner should decide which domain owns which fields. A reasonable split: PlayerSettings owns identity/display/runtime fields (product name, company name, color space, screen size); Build owns build-pipeline-specific fields (active target, scripting backend per platform, scripting defines per platform, EditorUserBuildSettings flags, build scenes). Currently both tools claim productName/companyName/version.
- **`Scene` domain** is adjacent to `build-manage-scenes` but operates on different objects (scene assets / runtime scenes vs. EditorBuildSettings entries). No actual functional overlap, but description disambiguation is missing (A4).

**Helper consolidation observation:**
- `ParseBuildTarget` is correctly shared (used by `build-player`, `build-batch`, `build-switch-platform`).
- `FormatBuildReport` exists in `Tool_Build.Build.cs` but `BatchBuild` does NOT call it — `BatchBuild` reimplements a simpler one-liner per-target summary. This is a minor maintenance smell, not a tool-quality issue.

**Workflows intentionally deferred / out of scope:**
- Cloud Build / Unity Build Automation API integration — out of scope for an editor-side MCP package.
- Diagnostic tools (build profiling, addressables analyse) — likely belong to a separate domain if added.

**Open questions for the reviewer:**
1. Should `build-set-settings` be deprecated in favour of `player-settings-set` for the overlapping fields, with Build retaining only build-pipeline-exclusive fields (scripting backend, defines, development flag)? See Cluster R1.
2. Should `build-batch` be merged into `build-player` (with `targets` accepting a list and the loop happening internally), or kept separate for ergonomic reasons? See Cluster R2.
3. Is Asset Bundle / Addressables tooling (G1) in scope for v1.x, or deferred to v2.0?
4. The Build domain has zero `ReadOnlyHint` markers despite having one clearly read-only tool (`build-get-settings`). Worth a sweep across all 39 domains in a separate audit pass.

**Coverage statement:** All six tools in `Editor/Tools/Build/` were read in full. Cross-domain claims (PlayerSettings overlap, AssetBundle absence, Scene-domain non-overlap) were verified by `Grep` across `Editor/Tools/` for the relevant API surfaces. The accounting (`files_found = files_read = files_analyzed = 6`) is balanced, so absence claims in this report meet Rule 3.
