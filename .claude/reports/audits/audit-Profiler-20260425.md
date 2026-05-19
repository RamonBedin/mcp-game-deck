# Audit Report — Profiler

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Profiler/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via Glob `Editor/Tools/Profiler/Tool_Profiler.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None

**Files not analyzed (if any):**
- None

**Absence claims in this report:**
- Absence claims (e.g. "no tool exposes the Memory Profiler package API") are permitted because file accounting is balanced. Each absence claim cites the exact file/method evidence that demonstrates the gap.

**Reviewer guidance:**
- The Profiler domain has 13 distinct tool methods across 5 files. Two of them (`profiler-status`/`profiler-ping`, and `profiler-start`+`profiler-stop` vs `profiler-toggle`) are clear redundancy clusters.
- The domain's biggest functional gap is `profiler-memory-snapshot` — it is named like a snapshot tool but does NOT actually create a `.snap` file usable by Unity's Memory Profiler package; it just dumps current memory totals. The accompanying `profiler-memory-list-snapshots` and `profiler-memory-compare` tools assume real snapshots exist on disk, but the only writer in the domain doesn't produce them. This is a coherence problem, not just a missing feature.
- Frame Debugger reflection-based tools (`profiler-frame-debugger-*`) duplicate code and are fragile. They are not "broken" — I did not run them — but their structure invites a consolidation finding.
- Cross-domain note: `Tool_Graphics.Stats.cs` already wraps the same `ProfilerRecorder` Render counters (`Batches Count`, `SetPass Calls Count`, `Triangles Count`, `Vertices Count`) that `profiler-get-counters` returns for the Render category. This is cross-domain redundancy worth flagging to the consolidation-planner.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `profiler-start` | Profiler / Start | `Tool_Profiler.Extended.cs` | 2 | no |
| `profiler-stop` | Profiler / Stop | `Tool_Profiler.Extended.cs` | 0 | no |
| `profiler-get-counters` | Profiler / Get Counters | `Tool_Profiler.Extended.cs` | 1 | yes |
| `profiler-memory-snapshot` | Profiler / Memory Snapshot | `Tool_Profiler.Extended.cs` | 1 | no |
| `profiler-ping` | Profiler / Ping | `Tool_Profiler.Extended.cs` | 0 | yes |
| `profiler-set-areas` | Profiler / Set Areas | `Tool_Profiler.Extended.cs` | 1 | no |
| `profiler-memory-list-snapshots` | Profiler / List Snapshots | `Tool_Profiler.Extended.cs` | 1 | yes |
| `profiler-memory-compare` | Profiler / Compare Snapshots | `Tool_Profiler.Extended.cs` | 2 | yes |
| `profiler-frame-debugger-enable` | Profiler / Frame Debugger Enable | `Tool_Profiler.Extended.cs` | 0 | no |
| `profiler-frame-debugger-disable` | Profiler / Frame Debugger Disable | `Tool_Profiler.Extended.cs` | 0 | no |
| `profiler-frame-debugger-events` | Profiler / Frame Debugger Events | `Tool_Profiler.Extended.cs` | 2 | yes |
| `profiler-frame-timing` | Profiler / Frame Timing | `Tool_Profiler.FrameTiming.cs` | 1 | no |
| `profiler-get-object-memory` | Profiler / Get Object Memory | `Tool_Profiler.GetMemory.cs` | 1 | no |
| `profiler-status` | Profiler / Status | `Tool_Profiler.Status.cs` | 0 | no |
| `profiler-toggle` | Profiler / Toggle | `Tool_Profiler.Toggle.cs` | 2 | no |

**Internal Unity API surface used:**
- `UnityEngine.Profiling.Profiler` (enabled, supported, logFile, enableBinaryLog, GetTotalAllocatedMemoryLong, GetTotalReservedMemoryLong, GetTotalUnusedReservedMemoryLong, GetMonoHeapSizeLong, GetMonoUsedSizeLong, GetTempAllocatorSize, GetRuntimeMemorySizeLong, SetAreaEnabled)
- `UnityEditorInternal.ProfilerDriver.deepProfiling`
- `UnityEditorInternal.ProfilerArea` (enum)
- `Unity.Profiling.ProfilerRecorder`, `Unity.Profiling.ProfilerCategory`
- `UnityEngine.FrameTimingManager` (CaptureFrameTimings, GetLatestTimings, struct `FrameTiming`)
- `UnityEditorInternal.FrameDebuggerUtility` via reflection (`SetEnabled`, `count`)
- `UnityEngine.GameObject.Find`, `Component.GetComponents`, `MeshFilter.sharedMesh`, `Renderer.sharedMaterial`
- `System.IO.Directory.GetFiles`, `System.IO.FileInfo`

**Inventory observations (carried into later sections):**
- 3 tools are read-only as marked. Two more (`profiler-status`, `profiler-ping`, `profiler-frame-timing`, `profiler-get-object-memory`) read state without mutating but lack `ReadOnlyHint = true`.
- Both the `profiler-toggle` file and the `profiler-start`/`profiler-stop` pair manipulate `Profiler.enabled`/`Profiler.logFile`.

---

## 2. Redundancy Clusters

### Cluster R1 — Profiler enable/disable
**Members:** `profiler-start`, `profiler-stop`, `profiler-toggle`
**Overlap:** `profiler-toggle(enable, logFile)` covers exactly the same state machine as `profiler-start(logFile, deep)` plus `profiler-stop()`. The only behavior in `start` not in `toggle` is `ProfilerDriver.deepProfiling = deep`. `toggle` adds `Profiler.enableBinaryLog` handling, which `start` omits. So neither is a strict superset, but the LLM has three nearly-identical entry points to choose from for "turn the profiler on".
**Impact:** High — a developer asking "start the profiler with deep profiling and a log file" must pick `start`; a developer asking "enable profiling and record a .raw" must pick `toggle`. Discovery requires reading both descriptions.
**Confidence:** high

### Cluster R2 — Profiler status reporting
**Members:** `profiler-status`, `profiler-ping`
**Overlap:** Both report `Profiler.enabled` and `Profiler.supported`. `profiler-status` adds memory totals (already available via `profiler-memory-snapshot`); `profiler-ping` adds `ProfilerDriver.deepProfiling` and `Profiler.logFile`. The union of fields is small; one consolidated tool would expose all of them with no parameter explosion.
**Impact:** Medium — the LLM has two near-synonyms ("status" vs "ping") for the same intent.
**Confidence:** high

### Cluster R3 — Memory totals reporting
**Members:** `profiler-status`, `profiler-memory-snapshot`
**Overlap:** Both print Total Allocated, Total Reserved, Mono Heap, Mono Used, Temp Allocator. `profiler-memory-snapshot` adds Total Unused (also in `profiler-status`) and a self-acknowledged dead-code note about `snapshotPath` being "reserved for future snapshot-to-file support". The two methods produce ~95% identical output. See also G1 for the deeper coherence problem with `profiler-memory-snapshot`.
**Impact:** High — `profiler-memory-snapshot` over-promises (its name implies a Memory Profiler `.snap`) and the LLM may select it expecting persisted snapshots, then call `profiler-memory-list-snapshots` and find nothing.
**Confidence:** high

### Cluster R4 — Frame Debugger enable/disable
**Members:** `profiler-frame-debugger-enable`, `profiler-frame-debugger-disable`
**Overlap:** Both reflect into `UnityEditorInternal.FrameDebuggerUtility.SetEnabled` with literally the same code path; only the boolean argument differs. These are textbook candidates for one tool with an `enable: bool` parameter (mirroring `profiler-toggle`'s shape).
**Impact:** Medium — duplicates ~30 lines of reflection plumbing across two methods.
**Confidence:** high

### Cluster R5 — Cross-domain Render counters
**Members:** `profiler-get-counters` (category=Render), `graphics-stats` (in `Tool_Graphics.Stats.cs`)
**Overlap:** Both use `ProfilerRecorder.StartNew(ProfilerCategory.Render, ...)` for the same four counters: `Batches Count`, `SetPass Calls Count`, `Triangles Count`, `Vertices Count`. Confirmed by `Tool_Graphics.Stats.cs:30-33`.
**Impact:** Low-medium — cross-domain duplication; the LLM might pick either tool when asked "how many draw calls is the scene making". Not a within-Profiler problem but worth flagging to the consolidation planner.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — `profiler-memory-snapshot` description misrepresents behavior
**Location:** `profiler-memory-snapshot` — `Tool_Profiler.Extended.cs` line 119–144
**Issue:** The `[Description]` says "Takes a memory snapshot for analysis." This implies a persistent `.snap` file (Unity Memory Profiler convention). In reality the tool prints current memory totals to text and the `snapshotPath` parameter is dead — see line 132: "Note: snapshotPath is reserved for future snapshot-to-file support." A user/LLM reading the description would reasonably expect `profiler-memory-list-snapshots` to find the file afterward.
**Evidence:** `[Description("Takes a memory snapshot for analysis.")]` at line 120 is contradicted by lines 131–133.
**Confidence:** high

### A2 — `profiler-get-counters` does not enumerate valid categories
**Location:** `profiler-get-counters` — `Tool_Profiler.Extended.cs` line 67–111
**Issue:** Param description says "Category name (e.g. 'Render', 'Scripts', 'Memory', 'Physics')." but the implementation also accepts `Audio` and `Ai` (lines 91–92). Conversely, the per-category counter list (lines 78–84) only handles `Render`, `Memory`, `Physics`, with all others (including `Scripts`, `Audio`, `Ai`) silently falling into a default `Main Thread, Render Thread` array — meaning `category="Scripts"` returns CPU-thread counters, not script counters. The mapping between "valid value" and "what you get back" is opaque.
**Evidence:** Lines 78–84 vs 86–94 use two different switches over `category`. The user-visible enumeration omits `Audio` and `Ai`.
**Confidence:** high

### A3 — `profiler-set-areas` uses substring matching with no documentation
**Location:** `profiler-set-areas` — `Tool_Profiler.Extended.cs` line 170–204
**Issue:** Param says "Comma-separated areas to enable: 'CPU,Memory,Rendering'." The actual matcher (line 191) does case-insensitive `IndexOf` substring matching against `ProfilerArea.ToString()`. So passing `"CPU"` will match `CPU`, but passing `"Render"` matches `Rendering`, and passing `"r"` enables every area containing the letter r. The full enum value list is not documented and the substring behavior is not mentioned.
**Evidence:** Line 191: `area.ToString().IndexOf(areas[j].Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0`.
**Confidence:** high

### A4 — `profiler-frame-debugger-events` description omits Play Mode requirement
**Location:** `profiler-frame-debugger-events` — `Tool_Profiler.Extended.cs` line 363–400
**Issue:** Description says "Lists draw call events from the Frame Debugger (must be enabled first)." The Frame Debugger only captures events when the application is running (Play Mode or paused on a frame), and the underlying `count` reflection returns 0 outside of an active capture. The description tells the LLM to enable first, but doesn't tell it to enter Play Mode and pause, which is the actual prerequisite.
**Evidence:** Description at line 364; the tool's empty-result branch at line 388–391 says "Enable Frame Debugger first" which mirrors the description's incomplete advice.
**Confidence:** medium (Frame Debugger Play Mode requirement is well-known Unity behavior; absence from description is the issue, not the tool's logic)

### A5 — `profiler-memory-compare` description hedges but tool returns size diff anyway
**Location:** `profiler-memory-compare` — `Tool_Profiler.Extended.cs` line 252–281
**Issue:** Description says "Compares two memory snapshots. Requires com.unity.memoryprofiler package." The implementation does NOT depend on the Memory Profiler package — it reads `FileInfo.Length` and prints byte differences (line 279). The "requires package" line will mislead callers into expecting structural diff (object counts, type breakdown). The tool is essentially `ls -l` on two files.
**Evidence:** Line 253 description vs lines 276–279 implementation.
**Confidence:** high

### A6 — Vague top-level descriptions on Start/Stop
**Location:** `profiler-start`, `profiler-stop` — `Tool_Profiler.Extended.cs` lines 27–28, 50–51
**Issue:** Both descriptions are under 12 words and contain no disambiguation against `profiler-toggle`. With three near-equivalents (R1), each description should say "use this when X, prefer profiler-toggle when Y". Neither does.
**Evidence:** `[Description("Starts the Unity Profiler, optionally logging to a file.")]` and `[Description("Stops the Unity Profiler.")]`.
**Confidence:** high

### A7 — Missing ReadOnlyHint on inspection-only tools
**Location:** `profiler-status`, `profiler-frame-timing`, `profiler-get-object-memory`
**Issue:** All three only read state (Profiler totals, FrameTimingManager output, object memory size); none mutate. They should carry `ReadOnlyHint = true` to align with `profiler-ping`, `profiler-get-counters`, `profiler-memory-list-snapshots`, `profiler-memory-compare`, and `profiler-frame-debugger-events` which already do. This affects host-side safety policies that gate write tools.
**Evidence:** `Tool_Profiler.Status.cs:22` declares `[McpTool("profiler-status", Title = "Profiler / Status")]` with no `ReadOnlyHint`; same pattern in `FrameTiming.cs:25` and `GetMemory.cs:25`.
**Confidence:** high

### A8 — `profiler-get-object-memory` doc-string says "managed memory" but tool only reports native
**Location:** `profiler-get-object-memory` — `Tool_Profiler.GetMemory.cs` line 16–96
**Issue:** XML summary line 19: "Reports native and managed memory usage." The implementation only calls `Profiler.GetRuntimeMemorySizeLong`, which returns native memory, and the output total is labeled "Total Native Memory" (line 92). No managed/Mono heap walk is performed.
**Evidence:** Line 19 summary vs lines 49–92 implementation.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `profiler-memory-snapshot` default `snapshotPath` is misleading
**Location:** `profiler-memory-snapshot` param `snapshotPath`
**Issue:** Default is `"Assets/MemorySnapshot"` but the parameter is dead-coded (the tool admits this in its own output: line 132). A user supplying a path will silently get no file written. Either the default should be removed and the parameter dropped, or the parameter should actually do something. See also G1.
**Current:** `string snapshotPath = "Assets/MemorySnapshot"`
**Suggested direction:** Remove the parameter (since it has no effect), or wire it up to write the formatted memory text via `File.WriteAllText`, or require the Memory Profiler package and produce a real `.snap`. Pick one.
**Confidence:** high

### D2 — `profiler-set-areas` default does not enable GPU
**Location:** `profiler-set-areas` param `enabledAreas`
**Issue:** Default is `"CPU,Memory"`. For most performance investigations the GPU area is critical. The default also doesn't include Rendering. This default reflects an arbitrary choice and is not documented.
**Current:** `string enabledAreas = "CPU,Memory"`
**Suggested direction:** Either default to a broader sensible set ("CPU,GPU,Memory,Rendering") or remove the default and require explicit selection. The current default forces callers who want GPU/Rendering to know they must override it.
**Confidence:** medium

### D3 — `profiler-get-counters` default `category="Render"` is OK but undocumented relationship
**Location:** `profiler-get-counters` param `category`
**Issue:** Default value is fine in isolation. The issue is that the default category produces a different counter set than the param description suggests is configurable (see A2). Not strictly a default-value bug, but flagged because the default masks the bug — most callers will never discover the silent-fall-through behavior because they don't pass anything.
**Current:** `string category = "Render"`
**Suggested direction:** Keep `Render` as default but fix A2 (enumerate valid values, branch handle each).
**Confidence:** medium

### D4 — `profiler-frame-timing` default `frameCount=1` reasonable but fights its averaging code
**Location:** `profiler-frame-timing` param `frameCount`
**Issue:** Default is 1, but the averaging block (lines 66–80 of FrameTiming.cs) only fires when `captured > 1`. So the default produces output without averages, which is the less interesting case for profiling. Most callers want a small window (5–10 frames). Not wrong, but suboptimal.
**Current:** `int frameCount = 1`
**Suggested direction:** Default to something like 5 to surface averages. Low priority.
**Confidence:** low

### D5 — `profiler-memory-compare` requires both paths but offers `""` defaults
**Location:** `profiler-memory-compare` params `snapshotA`, `snapshotB`
**Issue:** Both defaulted to empty string, then the tool errors with "Both snapshotA and snapshotB paths are required." This is a fake-optional pattern. Default should be removed and the params declared without default values so the schema marks them required.
**Current:** `string snapshotA = "", string snapshotB = ""`
**Suggested direction:** Drop the defaults so the MCP schema reports them as required parameters; the code's null-check becomes redundant.
**Confidence:** high

### D6 — `profiler-get-object-memory` `objectName` is required but signature has no default — flagged for completeness
**Location:** `profiler-get-object-memory` param `objectName`
**Issue:** Already required (no default). Mentioned only to confirm correct shape; this is the right pattern that D5 should follow.
**Current:** `string objectName` (no default)
**Suggested direction:** No change — used as positive reference for D5.
**Confidence:** high

### D7 — `profiler-frame-debugger-events` `pageSize=50` and `cursor=0` documented as pagination but tool ignores them
**Location:** `profiler-frame-debugger-events` params `pageSize`, `cursor`
**Issue:** Both parameters are accepted but only echoed in the header line `showing {cursor}-{cursor + pageSize}` (line 394). The tool returns the same single number (`total`) regardless of pagination input. Defaults aren't wrong but the parameters don't do anything.
**Current:** `int pageSize = 50, int cursor = 0`
**Suggested direction:** Either implement actual event-by-event reflection (hard — `FrameDebuggerUtility` private API), or remove the parameters and replace with a clear "use Frame Debugger window" message. As written, the params are misleading.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Real Memory Profiler snapshot capture
**Workflow:** A developer asks the assistant to "take a memory snapshot and compare against last week's". This is the canonical Unity Memory Profiler workflow: capture a `.snap` via `MemoryProfiler.TakeSnapshot`, save to disk, then later diff against another `.snap` using the package's API.
**Current coverage:** `profiler-memory-snapshot` (despite the name), `profiler-memory-list-snapshots`, `profiler-memory-compare`.
**Missing:** No tool actually captures a `.snap`. `profiler-memory-snapshot` only formats current `Profiler.GetTotal*` numbers as text and admits in its own output "snapshotPath is reserved for future snapshot-to-file support" (Extended.cs line 132). `profiler-memory-list-snapshots` searches for `*.snap` files (line 226) that no tool in this domain produces. `profiler-memory-compare` reads file sizes only, not snapshot contents (line 276–279). So the chain `snapshot → list → compare` is broken at step 1.
- The required Unity API is `Unity.MemoryProfiler.Editor.MemoryProfiler.TakeSnapshot(path, callback, captureFlags)` from the `com.unity.memoryprofiler` package, which is referenced in `profiler-memory-compare`'s description but not depended on anywhere.
**Evidence:** `Tool_Profiler.Extended.cs:120` `[Description("Takes a memory snapshot for analysis.")]` vs lines 125–144 (formats Profiler totals only). Line 226: `Directory.GetFiles(searchPath, "*.snap", ...)`. Line 253 description claims dependency on `com.unity.memoryprofiler` but no `using` for that namespace appears in any file (verified via Grep — zero `MemoryProfiler.Editor` matches in domain).
**Confidence:** high

### G2 — Profiler data export / programmatic readout from a session
**Workflow:** Run a Play Mode session with the profiler recording, then later read the per-frame samples (CPU markers, GPU events, memory deltas) from the resulting `.raw` log without opening the Profiler window.
**Current coverage:** `profiler-toggle` and `profiler-start` can write a `.raw` log via `Profiler.logFile` / `enableBinaryLog`. No tool reads it back.
**Missing:** No `profiler-load-log` / `profiler-read-frame` / `profiler-export-csv` tool. Unity exposes `ProfilerDriver.LoadProfile(path, false)` and `ProfilerDriver.GetFormattedStatisticsValue` plus `ProfilerDriver.GetFrameDataView` for programmatic access. None of these are wrapped.
**Evidence:** Grep across all 5 domain files for `LoadProfile`, `GetFormattedStatisticsValue`, `GetFrameDataView`, `ProfilerDriver.GetFrame` returned zero matches (Grep verified across the 5-file domain). The only `ProfilerDriver` usage is `deepProfiling` (Extended.cs lines 42, 160).
**Confidence:** high

### G3 — Programmatic ProfilerRecorder for arbitrary counter / custom marker
**Workflow:** "Tell me how many `MyGame.Combat.ResolveHit` calls happened in the last second" — i.e. attach a `ProfilerRecorder` to a custom `ProfilerMarker` and read its `LastValueAsDouble`/`Count`/`ElapsedNanoseconds`.
**Current coverage:** `profiler-get-counters` reads built-in counters from a fixed list per category (Extended.cs lines 78–84).
**Missing:** No tool accepts an arbitrary counter name + category pair. The hard-coded counter list excludes everything outside the four chosen names per category. Custom user-defined `ProfilerMarker` instances cannot be queried at all.
**Evidence:** `Tool_Profiler.Extended.cs:78-84` shows the only counter selection is from a hard-coded array per category; no user-supplied counter name parameter exists.
**Confidence:** high

### G4 — Per-frame marker / callstack inspection
**Workflow:** "What were the top 10 expensive functions on the main thread in frame N of the last capture?" — needed for actual profiling triage.
**Current coverage:** `profiler-frame-timing` returns aggregate CPU/GPU times per frame; `profiler-get-counters` returns last value of a few counters.
**Missing:** No tool walks `HierarchyFrameDataView` / `RawFrameDataView` to enumerate samples by name + time. Unity 6000.0+ exposes `ProfilerDriver.GetHierarchyFrameDataView` / `GetRawFrameDataView` for exactly this purpose. Nothing in the domain calls them.
**Evidence:** Grep across domain for `HierarchyFrameDataView`, `RawFrameDataView`, `FrameDataView` returned zero matches.
**Confidence:** high

### G5 — Frame Debugger event detail beyond total count
**Workflow:** "List the draw calls in the captured frame, sorted by cost, and show shader/keywords/render target for each."
**Current coverage:** `profiler-frame-debugger-enable`/`disable` and `profiler-frame-debugger-events` (returns total count only).
**Missing:** Per-event inspection. The reflection layer in the existing tools only reads `count`, not `GetFrameEventInfo` / `eventInfo` arrays. The tool itself acknowledges this on line 395: "(Detailed event inspection requires the Frame Debugger window.)" That admission marks this as an explicit gap.
**Evidence:** Extended.cs line 395.
**Confidence:** high

### G6 — GC allocation tracking per call site
**Workflow:** "Find the source of the GC pressure during combat — show me allocations per managed call site."
**Current coverage:** Only aggregate Mono heap totals via `profiler-status` / `profiler-memory-snapshot`.
**Missing:** No wrapper for `Profiler.BeginSample` / GC allocation markers, no tool to read `GC.Alloc` count from a captured frame, no integration with the GC Allocations sample in HierarchyFrameDataView (also blocked by G4).
**Evidence:** Grep for `GC.Alloc`, `BeginSample`, `EndSample` across domain returned zero matches.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort). Higher is more attractive to fix.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | A1 + G1 | Ambiguity + Capability Gap | 5 | 3 | 15 | high | `profiler-memory-snapshot` lies about what it does AND the `.snap`-based workflow is broken end-to-end. Highest leverage fix. |
| 2 | R1 | Redundancy | 4 | 2 | 16 | high | Three overlapping enable/disable tools (`start`, `stop`, `toggle`) — collapse into one with `enable/deep/logFile` params. |
| 3 | R2 | Redundancy | 3 | 1 | 15 | high | Merge `profiler-status` + `profiler-ping` into one read-only status tool. Trivial. |
| 4 | A5 | Ambiguity | 4 | 1 | 20 | high | `profiler-memory-compare` description claims package dependency it does not have. One-line description fix. |
| 5 | A2 | Ambiguity | 4 | 2 | 16 | high | `profiler-get-counters` accepts categories that silently fall through; doc says one set, code accepts another. Fix description + branch. |
| 6 | A7 | Ambiguity / Hint | 3 | 1 | 15 | high | Add `ReadOnlyHint = true` to `profiler-status`, `profiler-frame-timing`, `profiler-get-object-memory`. |
| 7 | D7 | Default | 3 | 1 | 15 | high | Remove ineffective `pageSize`/`cursor` params from `profiler-frame-debugger-events`, or implement them. |
| 8 | A3 | Ambiguity | 3 | 2 | 12 | high | `profiler-set-areas` substring-matching is undocumented and surprising. Document + use exact-match. |
| 9 | A8 | Ambiguity | 3 | 1 | 15 | high | `profiler-get-object-memory` summary claims managed memory, code only reports native. One-line doc fix or implement managed walk. |
| 10 | R4 | Redundancy | 2 | 1 | 10 | high | Collapse `profiler-frame-debugger-enable` + `disable` into one `profiler-frame-debugger-toggle(enable: bool)`. |
| 11 | G2/G3/G4 | Capability Gap | 5 | 5 | 5 | high | Real profiling — load `.raw`, custom counters, frame-data-view triage. Big effort but big payoff for serious users. |
| 12 | R5 | Cross-domain Redundancy | 2 | 3 | 6 | medium | `profiler-get-counters` (Render) overlaps `graphics-stats`. Defer to consolidation-planner. |

---

## 7. Notes

**Cross-domain dependency:**
- `Tool_Graphics.Stats.cs:30-33` already exposes the same Render-category counters as `profiler-get-counters`. The consolidation-planner should decide which domain owns this surface; my recommendation is Profiler (since Graphics is broader).

**Workflows intentionally deferred:**
- I did not exhaustively check every other domain for cross-cutting profiling helpers. R5 was found incidentally by a `profiler` grep; there may be more overlap (e.g. memory utilities under `Asset` or `Texture`). Worth a short sweep before consolidation.

**Open questions for the reviewer:**
1. Should `profiler-memory-snapshot` be repaired to actually integrate `com.unity.memoryprofiler` (G1), or should it be renamed to `profiler-memory-totals` and the snapshot-pretender behavior dropped? The first is a real feature; the second is honest naming. The current state is the worst of both.
2. Is the Frame Debugger reflection-based approach acceptable long-term, or should the domain depend on a public API that requires Unity 6000.x updates? If reflection is fine, R4 + D7 are the right cleanups; if not, the whole `frame-debugger-*` cluster needs a different approach.
3. `profiler-toggle` enables `enableBinaryLog`, but `profiler-start` does not. Was this deliberate (different intended use cases) or a bug? Resolving this informs how R1 collapses.
4. Should `profiler-set-areas` migrate to a list-of-enums parameter once that style is supported by the MCP schema layer? Today it forces a substring-match string parser that is hard to use correctly.

**Limits of this audit:**
- I did not run any tool against a live Unity Editor. All findings are static analysis.
- I did not verify reflection paths (`FrameDebuggerUtility`, `count`, `SetEnabled`) still resolve in Unity 6000.3 — assumed valid because they compile and are wrapped in defensive `null` checks.
- Effort estimates assume the existing `Tool_Profiler` partial class structure is preserved and that consolidation happens via the standard pipeline (`consolidation-planner` → `tool-consolidator` → `build-validator`).
