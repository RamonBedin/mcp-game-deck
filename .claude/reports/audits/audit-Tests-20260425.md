# Audit Report — Tests

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Tests/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/Tests/Tool_Tests.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Coverage is complete (2/2 files), so absence claims are admissible. Cross-domain searches for `TestRunnerApi`, `TestMode.EditMode|PlayMode`, and the `tests-` tool-id prefix returned only the two Tests-domain files — no other domain wraps Test Runner.

**Reviewer guidance:**
- This is a very small domain (2 tools, both wrapping `UnityEditor.TestTools.TestRunner.Api`). The findings are correspondingly narrow but include one severe correctness issue (G1: the run loop blocks the main thread it depends on for completion) and one severe naming/contract mismatch (R1/A1: `tests-get-results` does not return results).
- The `Filter.testNames` semantics (A2) are subtle and worth Ramon's attention before any plan goes ahead — the current docs say "substring match" but the Test Runner API treats `testNames` as an exact-match list.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `tests-run` | Tests / Run | `Tool_Tests.Run.cs` | 2 | no |
| `tests-get-results` | Tests / Get Test List | `Tool_Tests.GetResults.cs` | 1 | yes |

**Tool details:**

### `tests-run` (Tool_Tests.Run.cs)
- Method-level `[Description]`: *"Runs Unity tests (EditMode, PlayMode, or All) and returns pass/fail results."* (12 words)
- Params:
  - `string testMode = "All"` — `[Description("Test mode: 'EditMode', 'PlayMode', or 'All'. Default 'All'.")]`
  - `string filter = ""` — `[Description("Optional filter to match test names (substring match).")]`
- Internal Unity API: `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.RegisterCallbacks/UnregisterCallbacks/Execute`, `Filter`, `ExecutionSettings`, `TestMode`, `ICallbacks`, `ITestAdaptor`, `ITestResultAdaptor`.
- Notable mechanics: spins on `Thread.Sleep(100)` for up to 30 s while waiting for `IsComplete`; runs inside `MainThreadDispatcher.Execute(...)`.
- ReadOnlyHint: not set.

### `tests-get-results` (Tool_Tests.GetResults.cs)
- Method-level `[Description]`: *"Lists all available tests in the Test Runner for the specified mode."* (13 words)
- Params:
  - `string testMode = "All"` — `[Description("Test mode: 'EditMode', 'PlayMode', or 'All'. Default 'All'.")]`
- Internal Unity API: `TestRunnerApi.RetrieveTestList`, `TestMode`, `ITestAdaptor`.
- Notable mechanics: `RetrieveTestList` is async by callback; the code calls `Object.DestroyImmediate(api)` immediately after invoking it and before the callback fires, then checks `result == null`. See A4/G3 below.
- ReadOnlyHint: ✅ `ReadOnlyHint = true`.

---

## 2. Redundancy Clusters

### Cluster R1 — Name/contract collision between `tests-run` and `tests-get-results`
**Members:** `tests-run`, `tests-get-results`
**Overlap:** The tool ID `tests-get-results` strongly implies "fetch the results of a test run" — it is a natural counterpart to `tests-run`. In reality, `tests-get-results` does not return results at all; it returns the **test tree** (the catalog of available tests). The Title (`Tests / Get Test List`) is correct, but the tool ID and Description (`"Lists all available tests in the Test Runner for the specified mode."`) disagree about the contract.

This is not classic redundancy (they don't do the same thing), but it is the same failure mode: an LLM picking between two tools will be misled. After running `tests-run`, an LLM that wants more detail than the summary in the run output will reasonably reach for `tests-get-results` and get an unrelated catalog. There is also no actual "get last results" tool, so the misnamed one occupies that namespace.

**Impact:** High. Almost any chain-of-tools prompt that involves "run tests, then inspect failures" will misroute. This is partly a redundancy concern (naming overlap with an implied missing tool) and partly an ambiguity concern; it is also covered as A1 below for the description text, and as G2 below for the missing capability.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — `tests-get-results` ID and Description do not match its behavior
**Location:** `tests-get-results` — `Tool_Tests.GetResults.cs`
**Issue:** The tool ID is `tests-get-results` but the tool returns the **test tree**, not results from a previous run. The method-level description is *"Lists all available tests in the Test Runner for the specified mode."* which matches the Title (`Tests / Get Test List`) and the implementation, but contradicts the ID. An LLM that selects tools by ID first (which is the common case) will pick this expecting result data.
**Evidence:**
- `[McpTool("tests-get-results", Title = "Tests / Get Test List", ReadOnlyHint = true)]` (line 30)
- `[Description("Lists all available tests in the Test Runner for the specified mode.")]` (line 31)
- Body uses `api.RetrieveTestList(...)` (line 56), never reads any prior result store.
**Confidence:** high

### A2 — `tests-run` `filter` parameter description misstates the semantics
**Location:** `tests-run` param `filter` — `Tool_Tests.Run.cs`
**Issue:** The parameter description says *"Optional filter to match test names (substring match)."* The implementation assigns `executionFilter.testNames = new[] { filter };` (line 58). Per the Unity Test Runner `Filter.testNames` API, `testNames` is an exact-match list of fully-qualified test names, not a substring filter. (For substring/wildcard matching, the API exposes `Filter.groupNames` with regex patterns or `Filter.assemblyNames` / `Filter.categoryNames`.) An LLM passing `"PlayerHealth"` expecting to match `MyGame.Tests.PlayerHealthTests.Damage_ReducesHP` will get zero tests run and no diagnostic.
**Evidence:** `Tool_Tests.Run.cs` line 58: `executionFilter.testNames = new[] { filter };` — combined with line 31 description claiming substring match.
**Confidence:** high

### A3 — `tests-run` description does not mention the 30 s timeout
**Location:** `tests-run` — `Tool_Tests.Run.cs`
**Issue:** The method-level description is *"Runs Unity tests (EditMode, PlayMode, or All) and returns pass/fail results."* The implementation hard-codes a 30 000 ms wait cap (line 64) and returns an error if the run is incomplete by then. PlayMode test suites of any size will routinely exceed this. An LLM using this tool has no way to know the cap exists or to extend it. The trailing note in the success message (*"Test execution monitoring is limited. Check the Test Runner window for detailed results."*) reinforces the impression that the tool is reliable when in fact it can drop genuinely-running suites as failed/timeout.
**Evidence:** `Tool_Tests.Run.cs` line 28 (description) vs lines 62-74 (timeout loop and error path).
**Confidence:** high

### A4 — `tests-get-results` description does not warn about callback timing
**Location:** `tests-get-results` — `Tool_Tests.GetResults.cs`
**Issue:** The implementation calls `api.RetrieveTestList(mode, OnTestListReceived)` (line 56), then immediately `Object.DestroyImmediate(api)` (line 58), then synchronously checks `if (result == null)` (line 60). The Test Runner API documents `RetrieveTestList` as asynchronous — the callback is not guaranteed to have fired by line 60. In some Unity versions / cold-cache states this returns `"No tests found."` even when tests exist. The description gives no hint of this fragility. (See also G3.)
**Evidence:** lines 54-62 of `Tool_Tests.GetResults.cs`.
**Confidence:** medium — Unity's behavior here has changed across 2022/2023/6000 versions; in some versions the callback is invoked synchronously when the test tree is already cached. The fragility is real but its frequency is version-dependent.

### A5 — Magic strings on `testMode` not enumerated tightly
**Location:** both tools, param `testMode`
**Issue:** Both tools accept `"EditMode"`, `"PlayMode"`, `"All"` (case-insensitive). Any other string silently falls into the `All` branch (`Tool_Tests.Run.cs` lines 51-54; `Tool_Tests.GetResults.cs` lines 49-52). The descriptions enumerate the valid values, which is good — but the silent fallback means a typo (`"editmode "` with trailing space is fine, but `"edit"` becomes `"All"`) produces wrong results without warning. An explicit "unknown value → error" branch would surface mistakes.
**Evidence:** Both files, the `if/else if/else` mode dispatch.
**Confidence:** medium

### A6 — `tests-run` is not marked `ReadOnlyHint`, but EditMode tests are typically read-only-ish
**Location:** `tests-run`
**Issue:** `tests-run` has no `ReadOnlyHint` flag. This is arguably correct (PlayMode tests can mutate scene state, and even EditMode tests can call any editor API), so omitting the hint is defensible. Flagging only as a documentation gap: a description sentence like *"May enter Play mode and modify project state"* would help the LLM reason about when it's safe to call this without confirmation.
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `tests-run` `filter = ""` default conflicts with broken filter semantics
**Location:** `tests-run` param `filter`
**Issue:** The default `""` is fine in isolation (empty → run all). But because the `testNames` semantics are exact-match (see A2), any non-empty user value other than a fully-qualified test name silently runs zero tests. The default being safe masks how brittle non-default values are.
**Current:** `string filter = ""`
**Suggested direction:** Keep the empty default; the bigger fix is correcting the semantics (A2) so non-default values do something useful (substring/regex match against `testNames`, or routing to `Filter.groupNames`).
**Confidence:** high

### D2 — `testMode = "All"` default is fine but expensive
**Location:** both tools, param `testMode`
**Issue:** The default `"All"` triggers `TestMode.EditMode | TestMode.PlayMode`, which for `tests-run` means the call may enter Play mode every invocation. An LLM iterating quickly during development will hit Play-mode startup costs (~5-30 s per run) on every default-arg call. EditMode-only is the more common iteration target.
**Current:** `string testMode = "All"`
**Suggested direction:** Consider `EditMode` as the default for `tests-run` (faster, safer iteration) and keep `All` for `tests-get-results` (catalog, cheap).
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Synchronous wait blocks the main thread it depends on
**Workflow:** Run a Unity test suite via MCP and receive results.
**Current coverage:** `tests-run` wraps `TestRunnerApi.Execute` and the `ICallbacks` interface.
**Missing:** A non-blocking execution path. The current implementation calls `MainThreadDispatcher.Execute(...)` (line 34) and **inside that main-thread continuation** spins `Thread.Sleep(100)` for up to 30 s waiting for `_isComplete` (lines 64-68). EditMode tests are dispatched and run on the editor main thread, so blocking the main thread inside the dispatcher prevents the editor from pumping the messages that would let tests progress. In practice this either deadlocks (tests never run, then time out at 30 s and report "timed out") or only succeeds when Unity processes the callbacks off the dispatcher tick — which is undocumented behavior.

What is needed instead: an async start/poll pattern. Either (a) split into `tests-start` (kicks off run, returns a run-id) + `tests-poll` (returns current status and results when complete), or (b) integrate with `MainThreadDispatcher` so the dispatch returns immediately and a separate completion callback posts results back to the MCP client. Both shapes exist for similar long-running editor operations elsewhere in Unity tooling (e.g. `AssetDatabase.RefreshSynchronousImport` vs the async refresh paths).
**Evidence:** `Tool_Tests.Run.cs` lines 34-100. The lambda passed to `MainThreadDispatcher.Execute` performs the entire wait loop on the main thread.
**Confidence:** high — the structure is plain to read and matches a known anti-pattern. Whether it deadlocks vs. merely degrades depends on the dispatcher implementation, but it is at minimum incorrect by design.

### G2 — No way to retrieve detailed failure data (messages, stack traces, per-test logs)
**Workflow:** Run tests, then inspect why a specific test failed (assertion message, stack trace, captured log lines).
**Current coverage:** `tests-run` returns counts plus a list of `result.Test.FullName` for failed tests (`Tool_Tests.Run.cs` lines 165-187).
**Missing:** Failure detail. `ITestResultAdaptor` exposes `Message`, `StackTrace`, `Output`, `Duration`, `ResultState`, and child results. None of these are captured. The user-facing message says *"Check the Test Runner window for detailed results."* which is exactly the workflow the MCP layer should automate. There is also no "get-results-of-last-run" tool — the misnamed `tests-get-results` returns the catalog, not results (see A1/R1).

A useful addition would be either:
- a `tests-get-failure-details(testName)` read-only tool, or
- expanding `tests-run`'s output to include per-failure `Message` + first N lines of `StackTrace`,
- or both, with the granular tool gated by `ReadOnlyHint = true`.
**Evidence:** `Tool_Tests.Run.cs` line 181: `_failedTests.Add(result.Test.FullName);` — only the name is captured. Lines 155-187 never touch `result.Message`, `result.StackTrace`, or `result.Output`.
**Confidence:** high (the file was read in full; no other field is captured).

### G3 — No way to wait for the test catalog to materialize
**Workflow:** Ask "what tests exist in this project?" and get the answer reliably even on cold project load.
**Current coverage:** `tests-get-results` calls `RetrieveTestList` and reads the result synchronously.
**Missing:** A wait/retry path for the async callback. As noted in A4, on cold cache the callback may not have fired by the time the synchronous null check runs, producing a spurious *"No tests found."* The minimal fix is the same kind of bounded wait used in `tests-run` (with a smaller timeout, ~5 s); the better fix is the async pattern in G1.
**Evidence:** `Tool_Tests.GetResults.cs` lines 54-62. Note also `Object.DestroyImmediate(api)` on line 58 happens before the callback could fire — depending on Unity's internal handling this can also cause the callback never to be invoked.
**Confidence:** medium — see A4.

### G4 — No category / assembly / namespace filtering
**Workflow:** Run only the tests in `MyGame.Tests.Combat.*`, or only tests tagged `[Category("Slow")]`.
**Current coverage:** `tests-run` exposes only `testNames` via the `filter` param.
**Missing:** `Filter.categoryNames`, `Filter.assemblyNames`, `Filter.groupNames` (regex on full name) are unwrapped. The Unity Test Runner GUI exposes all of these; an MCP user iterating on a single feature area will have to either run all tests (slow) or know the exact fully-qualified test name (and even then, see A2 — the substring claim is wrong).
**Evidence:** `Tool_Tests.Run.cs` line 41 creates an empty `Filter`; only `testMode` and `testNames` are populated.
**Confidence:** high

### G5 — No way to cancel a running test execution
**Workflow:** Start a long PlayMode suite, realize it was the wrong one, cancel it.
**Current coverage:** None. `tests-run` is fire-and-wait-30 s.
**Missing:** A `tests-cancel` tool. `TestRunnerApi` does not directly expose Cancel, but `EditorApplication.ExitPlaymode()` plus `UnregisterCallbacks` is the practical equivalent. This is a smaller gap than G1/G2 but worth flagging once the async pattern from G1 is in place.
**Confidence:** medium — depends on async refactor being done first, otherwise there's nothing to cancel.

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort).

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 4 | 10 | high | `tests-run` blocks the main thread it relies on; rework into async start/poll. |
| 2 | A1 / R1 | Ambiguity / Redundancy | 5 | 1 | 25 | high | `tests-get-results` ID lies about its behavior — rename to `tests-list` or similar; reserve `tests-get-results` for a real results-retrieval tool. |
| 3 | G2 | Capability Gap | 5 | 2 | 20 | high | Capture `Message` / `StackTrace` / `Output` from `ITestResultAdaptor` so failures are diagnosable without opening the Test Runner window. |
| 4 | A2 | Ambiguity | 4 | 1 | 20 | high | `filter` param claims substring match but uses `Filter.testNames` (exact match). Fix description and/or implementation. |
| 5 | A3 | Ambiguity | 3 | 1 | 15 | high | Document the 30 s timeout in the `tests-run` description (or remove the hard cap as part of G1). |
| 6 | G4 | Capability Gap | 3 | 2 | 12 | high | Expose category / assembly / regex filtering via additional params or a structured filter object. |
| 7 | D2 | Default Value | 3 | 1 | 15 | medium | Default `testMode` to `EditMode` for `tests-run` to avoid surprise PlayMode entry. |
| 8 | G3 / A4 | Capability Gap / Ambiguity | 3 | 2 | 12 | medium | `RetrieveTestList` is async — add a bounded wait so cold-cache calls don't return "No tests found." spuriously. |
| 9 | A5 | Ambiguity | 2 | 1 | 10 | medium | Reject unknown `testMode` values instead of silently defaulting to `All`. |
| 10 | G5 | Capability Gap | 2 | 3 | 6 | medium | Add `tests-cancel` once the async pattern lands. |
| 11 | A6 | Ambiguity | 1 | 1 | 5 | low | Note PlayMode side-effects in `tests-run` description. |

---

## 7. Notes

- The Tests domain is one of the smallest in the project (2 tools) but is also one of the most leveraged: any agent doing TDD-style iteration will lean on `tests-run` heavily. The G1 / G2 / A2 cluster makes this leverage unreliable — high-priority for the consolidation pipeline.
- No cross-domain dependencies were observed. Searches for `TestRunnerApi`, `TestMode.EditMode|PlayMode`, and the `tests-` tool-id prefix returned only the two files in `Editor/Tools/Tests/`. A consolidation plan can treat this domain as standalone.
- The `Tool_Tests.Run.cs` file mixes the public tool surface and the private `TestResultCollector` ICallbacks implementation in one partial. That's fine, but if the async refactor in G1 is taken, the collector likely needs to outlive a single tool invocation (run-id keyed) and may want to move to a separate file.
- Open question for the reviewer: should the async refactor (G1) be scoped into the Tests audit's plan, or treated as a separate spec? It's likely a 4-effort change touching `MainThreadDispatcher` semantics — not a 1-line fix.
- Open question for the reviewer: which tool ID convention should the rename in #2 use? Candidates: `tests-list`, `tests-list-tests`, `tests-list-catalog`. The ID `tests-get-results` should be freed for an actual results-retrieval tool.
