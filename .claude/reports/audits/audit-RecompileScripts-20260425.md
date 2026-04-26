# Audit Report — RecompileScripts

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/RecompileScripts/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via `Glob Editor/Tools/RecompileScripts/Tool_RecompileScripts.*.cs` — note the directory uses a single non-partial-suffixed file name; broader glob `Editor/Tools/RecompileScripts/**/*.cs` returns the same 1 file)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** ✅ balanced (1 / 1 / 1)

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted — accounting is balanced. Cross-domain absence claims (e.g. "no other tool requests script recompilation") are backed by `Grep` over the entire `Editor/Tools/` tree for `RequestScriptCompilation` / `CompilationPipeline` / `isCompiling` (results captured below).

**Reviewer guidance:**
- This domain is unusually small: 1 file, 2 tools. Findings are concentrated on overlap with other domains (`asset-refresh`, `editor-get-state`, `script-validate`) rather than intra-domain redundancy.
- The naming "RecompileScripts" describes the *action*, not a Unity subsystem. The two tools live under the `Editor / …` title prefix, suggesting they conceptually belong to the Editor domain. This is the single most important question for the reviewer to weigh before consolidation.
- The file does not follow the `Tool_[Domain].[Action].cs` partial-class convention used elsewhere in the codebase — both tools are in one monolithic file. This is a minor structural inconsistency, not a functional defect.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `recompile-scripts` | Editor / Recompile Scripts | `Tool_RecompileScripts.cs` | 1 (`forceReimport: bool = false`) | no |
| `recompile-status` | Editor / Compilation Status | `Tool_RecompileScripts.cs` | 0 | no (should be `true`) |

**Unity API surface used:**
- `EditorApplication.isCompiling`, `isPlaying`, `isPaused`
- `AssetDatabase.Refresh()` / `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`
- `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()`
- `UnityEditor.Compilation.CompilationPipeline.GetAssemblies(AssembliesType.Player | AssembliesType.Editor)`

---

## 2. Redundancy Clusters

### Cluster R1 — AssetDatabase refresh duplicated across domains
**Members:** `recompile-scripts` (RecompileScripts), `asset-refresh` (Asset)
**Overlap:** Both tools wrap `AssetDatabase.Refresh()` with an identical `forceUpdate`/`forceReimport` boolean parameter. `recompile-scripts` performs the refresh as a *side effect* before calling `CompilationPipeline.RequestScriptCompilation()`. The user-facing capability "refresh assets" is therefore reachable through two entry points with different parameter names (`forceReimport` vs `forceUpdate`) and slightly different output strings.
**Impact:** When the LLM wants to refresh the database it has to choose between two semantically-overlapping tools. If it picks `recompile-scripts` to "just refresh", it incurs an unwanted recompile request.
**Confidence:** high

### Cluster R2 — Compilation status reported by three tools
**Members:** `recompile-status` (RecompileScripts), `editor-get-state` (Editor), `script-validate` (Script)
**Overlap:** All three surface `EditorApplication.isCompiling`. `editor-get-state` already reports `isPlaying`, `isPaused`, `isCompiling`, `isUpdating`, plus Unity version, platform, and active scene. `recompile-status` reports a strict subset (`isCompiling`, `isPlaying`, `isPaused`) plus the assembly listing. `script-validate` adds per-script assembly lookup. The "list assemblies" capability is unique to `recompile-status`, but the "report compilation flags" capability is duplicated.
**Impact:** Medium-to-high LLM ambiguity — when a user asks "is Unity compiling?" the LLM must pick among three tools with overlapping outputs. The non-readonly marking on `recompile-status` makes it look more invasive than it is, which may push the LLM toward `editor-get-state` and miss the assembly listing entirely.
**Confidence:** high

### Cluster R3 — Domain naming itself
**Members:** `recompile-scripts`, `recompile-status` (own domain), all `editor-*` tools
**Overlap:** Both tool *titles* use the `Editor / …` prefix (`Editor / Recompile Scripts`, `Editor / Compilation Status`), matching the Editor domain's title convention. The MCP tool IDs (`recompile-scripts`, `recompile-status`) are the only ones in the codebase that don't follow the `[domain]-[action]` pattern derived from their containing folder. This suggests RecompileScripts is conceptually a sub-area of Editor, not a peer domain.
**Impact:** Discoverability — users and LLMs grouping tools by domain prefix will not find these alongside `editor-*` tools.
**Confidence:** medium (this is a structural observation; Ramon may have intentionally split it out)

---

## 3. Ambiguity Findings

### A1 — `recompile-scripts` description doesn't disambiguate from `asset-refresh`
**Location:** `recompile-scripts` — `Tool_RecompileScripts.cs:26`
**Issue:** The description states "Triggers a script recompilation in Unity Editor. Also refreshes the AssetDatabase." It does not tell the LLM "use this when you want to recompile, not when you only want to refresh assets — use `asset-refresh` for that". Given Cluster R1, this is a missing disambiguation clause.
**Evidence:** `[Description("Triggers a script recompilation in Unity Editor. Also refreshes the AssetDatabase. Returns current compilation status and any pending assembly info.")]`
**Confidence:** high

### A2 — `recompile-status` description doesn't disambiguate from `editor-get-state`
**Location:** `recompile-status` — `Tool_RecompileScripts.cs:64`
**Issue:** Description doesn't mention that `editor-get-state` already returns `isCompiling`. A reader would not know which to pick. Should call out the unique capability ("lists player and editor assemblies — for compile flags only, prefer `editor-get-state`").
**Evidence:** `[Description("Checks current script compilation status — whether compilation is in progress and lists player/editor assemblies.")]`
**Confidence:** high

### A3 — `forceReimport` parameter description is shallow
**Location:** `recompile-scripts` param `forceReimport` — `Tool_RecompileScripts.cs:28`
**Issue:** Says "If true, also forces a full AssetDatabase reimport. Default false." Doesn't explain the cost (full project reimport can take minutes), or when this is appropriate (e.g. after manually editing a `.meta` file, or after Git operations). Without this guidance, the LLM may flip it on speculatively.
**Evidence:** `[Description("If true, also forces a full AssetDatabase reimport. Default false.")]`
**Confidence:** medium

### A4 — `recompile-scripts` "already compiling" path silently no-ops the refresh
**Location:** `recompile-scripts` — `Tool_RecompileScripts.cs:35-39`
**Issue:** When `EditorApplication.isCompiling` is `true`, the tool returns early without honoring `forceReimport`. The behavior is reasonable but undocumented — neither the method description nor the parameter description warn that the request may be silently ignored if a compile is already in flight.
**Evidence:**
```
if (EditorApplication.isCompiling)
{
    sb.AppendLine("Scripts are already compiling. Waiting for current compilation to finish.");
    return ToolResponse.Text(sb.ToString());
}
```
The message says "Waiting for…" but the tool does not actually wait — it returns immediately. The wording will mislead the LLM into thinking the operation was queued.
**Confidence:** high

### A5 — Title naming asymmetry
**Location:** Both tools — `Tool_RecompileScripts.cs:25, 63`
**Issue:** The `recompile-status` tool's title is `Editor / Compilation Status` (not `Editor / Recompile Status`), while its tool ID uses `recompile-status`. The mismatch between ID stem (`recompile-`) and title stem (`Compilation`) makes search/discovery slightly harder.
**Evidence:** `[McpTool("recompile-status", Title = "Editor / Compilation Status")]`
**Confidence:** low

### A6 — Description string concatenation (cosmetic)
**Location:** Both tools' `[Description(...)]` attributes
**Issue:** Both descriptions are written as `"… first part. " + "second part."` — needless string concatenation. Compiles fine, but the only reason to use `+` here would be line-wrapping in source. It's a minor style nit and consistent across both tools.
**Evidence:** `Tool_RecompileScripts.cs:26, 64`
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `forceReimport` default may not match common intent
**Location:** `recompile-scripts` param `forceReimport`
**Issue:** Default `false` is correct for most cases (cheap), but the surrounding "Recompile Scripts" framing suggests the user already accepts a non-trivial wait. In practice an LLM invoking this tool typically wants to settle the project state after edits, where a plain `AssetDatabase.Refresh()` is usually enough. The default is fine; documentation around when to flip it is what's missing (see A3).
**Current:** `bool forceReimport = false`
**Suggested direction:** Keep default `false`. Strengthen description so the LLM understands when (rarely) to set it.
**Confidence:** medium

### D2 — `recompile-status` has no parameters but could be filtered
**Location:** `recompile-status`
**Issue:** Tool always returns full assembly listings. For projects with hundreds of assemblies (common with packages), output bloats the LLM context. A filter parameter (e.g. `includeAssemblies: bool = true`) would let callers ask "is it compiling?" without paying for the full list.
**Current:** `public ToolResponse CompilationStatus()`
**Suggested direction:** Add an optional filter so callers who only want flags can opt out of the assembly dump.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — No way to wait for compilation to finish
**Workflow:** LLM edits a script, requests a recompile, then needs to verify the project compiles before proceeding to next step (e.g. running a test, attaching the new component to a prefab).
**Current coverage:** `recompile-scripts` triggers compilation and returns immediately. `recompile-status` and `editor-get-state` return a snapshot of `isCompiling`. There is no blocking / polling-with-timeout / "wait until idle" tool.
**Missing:** A `recompile-and-wait` (or `editor-wait-for-compile`) tool that polls `EditorApplication.isCompiling` until false (or timeout), and ideally inspects `CompilationPipeline.assemblyCompilationFinished` events to surface compile errors. Without this, the LLM has to guess polling intervals or fire-and-forget, leading to brittle multi-step workflows after script edits.
**Evidence:** `Tool_RecompileScripts.cs:31-57` returns synchronously after `RequestScriptCompilation()` — there is no awaiting mechanism. No other domain wraps a polling loop on `isCompiling` (verified via `Grep` for `RequestScriptCompilation` and `isCompiling` across `Editor/Tools/`).
**Confidence:** high

### G2 — No surfacing of compile errors / warnings
**Workflow:** After a recompile, the LLM needs to know "did the build succeed? what failed?" so it can self-correct.
**Current coverage:** None. `recompile-scripts` reports only that compilation was *requested*. `script-validate` reports "Status: No compilation running — project compiled OK" based on `!isCompiling`, which is an incorrect inference (project may have failed to compile and stopped, leaving `isCompiling == false`). `recompile-status` lists assemblies but no errors.
**Missing:** A tool that subscribes to `CompilationPipeline.assemblyCompilationFinished` (or reads recent `LogEntry` console errors) and returns the list of compile errors with file/line. The most common follow-up to "I edited a script" is "did it compile?" and the current toolset answers only "is the editor still busy?".
**Evidence:** `Tool_RecompileScripts.cs:51-55` calls `RequestScriptCompilation()` and reports state without subscribing to any compilation completion event. No `CompilationPipeline.assemblyCompilationFinished` or `compilationFinished` reference exists anywhere in `Editor/Tools/` (verified via `Grep` for `CompilationPipeline`).
**Confidence:** high

### G3 — No targeted single-assembly recompile
**Workflow:** LLM edits one script in `Assembly-CSharp-Editor` and wants to recompile only that assembly to save time.
**Current coverage:** `recompile-scripts` triggers a project-wide compilation via `RequestScriptCompilation()` (no overload taking an assembly name).
**Missing:** A targeted recompile, possibly via `CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions)` (Unity 2021.2+) which accepts a `requestScriptCompilationOptions` flag, or via assembly-definition-targeted tooling. This is a low-priority gap because Unity itself does incremental compilation under the hood, but the public Unity API surface for it does exist and isn't wrapped.
**Evidence:** `Tool_RecompileScripts.cs:51` — `CompilationPipeline.RequestScriptCompilation();` (no parameter).
**Confidence:** low (Unity's incremental compiler likely covers most use cases; the gap is the lack of a "force this one assembly" knob)

### G4 — `ReadOnlyHint = true` missing on `recompile-status`
**Workflow:** Tools that mutate state should be marked separately from inspection tools so `auto` permission modes can permit them differently.
**Current coverage:** Other readonly inspection tools in adjacent domains (`asset-get-info`, `asset-find`, `asset-get-import-settings`, `editor-get-state`, `script-read`, `script-validate`) all use `ReadOnlyHint = true`.
**Missing:** `recompile-status` performs no mutations — it only reads `EditorApplication` flags and calls `CompilationPipeline.GetAssemblies(...)`. It should be marked `ReadOnlyHint = true` for consistency.
**Evidence:** `Tool_RecompileScripts.cs:63` — `[McpTool("recompile-status", Title = "Editor / Compilation Status")]` — no `ReadOnlyHint`.
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G2 | Capability Gap | 5 | 3 | 15 | high | No way to retrieve compile errors after a recompile — biggest blocker for autonomous code-edit loops |
| 2 | G1 | Capability Gap | 5 | 3 | 15 | high | No "wait for compile to finish" tool — multi-step workflows after script edits are brittle |
| 3 | A4 | Ambiguity | 4 | 1 | 20 | high | "Already compiling" path returns "Waiting for…" but does not actually wait — misleading message |
| 4 | R2 | Redundancy | 4 | 2 | 16 | high | Compilation status reported by 3 different tools — LLM ambiguity for "is Unity compiling?" |
| 5 | G4 | Capability Gap | 3 | 1 | 15 | high | `recompile-status` missing `ReadOnlyHint = true` — inconsistent with sibling inspection tools |
| 6 | R1 | Redundancy | 3 | 2 | 12 | high | `forceReimport` on `recompile-scripts` overlaps with `asset-refresh` `forceUpdate` |
| 7 | A1 | Ambiguity | 3 | 1 | 15 | high | `recompile-scripts` description lacks "use this when, not that" clause vs `asset-refresh` |
| 8 | A2 | Ambiguity | 3 | 1 | 15 | high | `recompile-status` description doesn't disambiguate from `editor-get-state` |
| 9 | R3 | Redundancy/Structure | 2 | 4 | 6 | medium | RecompileScripts may belong inside the Editor domain (titles already use `Editor / …` prefix) |
| 10 | A3 | Ambiguity | 2 | 1 | 10 | medium | `forceReimport` description doesn't explain cost or when to use it |
| 11 | D2 | Default | 2 | 2 | 8 | medium | `recompile-status` always emits full assembly list — no filter for "just give me the flags" |

Priority formula: **Impact × (6 - Effort)**.

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `Tool_Asset.Refresh.cs` is the canonical wrapper for `AssetDatabase.Refresh()`. `recompile-scripts` quietly duplicates this call inline. If consolidation moves forward, decide whether `recompile-scripts` should *call* `asset-refresh`'s code path or whether the AssetDatabase refresh should be removed from `recompile-scripts` entirely (and the LLM expected to compose the two tools).
- `Tool_Editor.GetState.cs` already reports `isCompiling` plus other flags. If `recompile-status` is consolidated, its assembly-listing capability would naturally migrate either there or into a new `editor-list-assemblies` (read-only) tool.
- `Tool_Script.Validate.cs:84` makes an incorrect inference: `isCompiling == false` ⇒ "project compiled OK". That is a finding for the Script domain audit, not this one — flagging it here only because it's directly adjacent.

**Workflows intentionally deferred:**
- Async/event-driven compilation status (Unity's `CompilationPipeline.compilationStarted` / `compilationFinished` / `assemblyCompilationFinished` events). The capability gap is real (G2), but the actual implementation surface (subscribing in `[InitializeOnLoad]` and caching the most recent error list) is a design decision for the consolidation-planner, not the auditor.

**Open questions for the reviewer:**
1. Is the RecompileScripts directory intentionally a separate domain, or should it merge into Editor (R3)?
2. Should `recompile-scripts` perform `AssetDatabase.Refresh()` as a side effect, or should the LLM be expected to call `asset-refresh` separately (R1)?
3. How important is "give me the compile errors" (G2) for v1.2 vs deferring to v2.0? It would unlock substantially better autonomous edit loops but requires non-trivial event-subscription scaffolding.

**Limits of this audit:**
- I did not run the code or measure Unity API timing (e.g. how long `AssetDatabase.Refresh(ForceUpdate)` takes on a real project). All "cost" claims in findings are based on Unity documentation knowledge, not measurements.
- I did not check git history for whether RecompileScripts was once part of a larger Editor domain and split out, which would inform R3.
