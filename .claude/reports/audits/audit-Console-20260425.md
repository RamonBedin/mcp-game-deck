# Audit Report — Console

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Console/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 3 (via Glob `Editor/Tools/Console/Tool_Console.*.cs`)
- `files_read`: 3
- `files_analyzed`: 3

**Balance:** ✅ balanced — all three tool files were read and incorporated.

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted, since `files_found == files_analyzed == 3`. Absence claims are scoped to the `Editor/Tools/Console/` directory only; cross-domain absence (e.g. "no other domain wraps `LogEntries`") was confirmed via `Grep` over `Editor/Tools/`.

**Reviewer guidance:**
- The Console domain is small (3 tools, all single-method) and internally consistent — no redundancy clusters were found.
- The biggest opportunities are **capability gaps** around log-entry richness (no stack traces, no entry-context object, no clipboard/copy of selected entry, no log-callback subscription) and around **disambiguation between user-message `console-log` and `script-log` patterns** if any exist.
- The `console-log` tool prepends `"[Game Deck] "` to every message — that is intentional per the source, but the user-facing description does not mention it. Worth a one-line addition.
- Reflection-heavy `console-get-logs` is fragile across Unity versions (Unity 6000.x mode-bitmask values not guaranteed stable). The `CategoriseByMode` masks are documented as "observed across Unity 2021–6", which is a real maintenance risk but **out of scope** for this audit (it's an implementation note, not a tool-surface defect).

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `console-log` | Console / Log | `Tool_Console.Log.cs` | 2 | no |
| `console-clear` | Console / Clear | `Tool_Console.Clear.cs` | 0 | no |
| `console-get-logs` | Console / Get Logs | `Tool_Console.GetLogs.cs` | 3 | **yes** |

**Detailed signatures:**

- **`console-log`** — `Log(string message, string type = "info")`
  - Method `[Description]`: "Sends a message to the Unity Console (Info, Warning, or Error)."
  - `message`: "The message to log."
  - `type`: "Log type: 'info', 'warning', or 'error'. Default 'info'."
  - Internal API: `Debug.Log` / `Debug.LogWarning` / `Debug.LogError`. Prepends `[Game Deck]` prefix.

- **`console-clear`** — `Clear()`
  - Method `[Description]`: "Clears all entries from the Unity Console. Equivalent to clicking the Clear button in the Console window."
  - Internal API: `UnityEditor.LogEntries.Clear()` via reflection.

- **`console-get-logs`** — `GetLogs(string type = "all", int count = 20, string filterText = "")` — `ReadOnlyHint = true`
  - Method `[Description]`: "Retrieves entries from the Unity Console. Supports filtering by type (all/log/warning/error) and an optional text search substring. Returns up to 'count' entries."
  - `type`: "Log type filter: 'all', 'log', 'warning', or 'error'. Default is 'all'."
  - `count`: "Maximum number of log entries to return. Default is 20."
  - `filterText`: "Optional case-insensitive substring to filter log messages. Leave empty to return all."
  - Internal API: `UnityEditor.LogEntries.{StartGettingEntries, EndGettingEntries, GetCount, GetEntryInternal}` and `UnityEditor.LogEntry.{message, mode, file, line}` via reflection.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The three tools are semantically distinct (write / clear / read) and there is no overlap in parameter shape or intent.

---

## 3. Ambiguity Findings

### A1 — `console-log` type enum mismatch between description and behavior
**Location:** `console-log` — `Tool_Console.Log.cs`
**Issue:** The method `[Description]` says "Info, Warning, or Error" (capitalized) while the parameter description and internal switch use lowercase `'info' | 'warning' | 'error'`. An LLM may pass `"Info"` and the tool will silently fall through to `Debug.Log` (the `else` branch), because only `"warning"` and `"error"` are matched (case-insensitively). Passing `"verbose"` or `"debug"` likewise silently logs as info.
**Evidence:** `Tool_Console.Log.cs` lines 22, 25, 37, 41, 47 — final `else` swallows any unrecognized value.
**Confidence:** high

### A2 — `console-log` does not document the `[Game Deck]` prefix
**Location:** `console-log` — `Tool_Console.Log.cs` line 35
**Issue:** Every message is silently prepended with `"[Game Deck] "`. Neither the method description nor the `message` parameter description mentions this. An LLM trying to log an exact-formatted string (e.g. for grep-matching from another tool) will be surprised.
**Evidence:** `string prefixed = $"[Game Deck] {message}";` — undocumented.
**Confidence:** high

### A3 — `console-get-logs` `type` enum vs `console-log` `type` enum drift
**Location:** `console-get-logs` `type` parameter
**Issue:** `console-get-logs` accepts `"all" | "log" | "warning" | "error"`, while `console-log` writes with `"info" | "warning" | "error"`. The category produced by `console-log` is `Debug.Log` → mapped by `CategoriseByMode` to `"log"`. Filtering for `type="info"` in `console-get-logs` will silently return zero results (because the filter compares to `"log"`, not `"info"`). The mismatch isn't documented in either tool.
**Evidence:** `Tool_Console.Log.cs` line 25 (`'info'`) vs `Tool_Console.GetLogs.cs` line 47 (`'log'`) and `CategoriseByMode` line 216 (returns `"log"`).
**Confidence:** high

### A4 — `console-clear` lacks disambiguation against an "after-test" use case
**Location:** `console-clear` — `Tool_Console.Clear.cs`
**Issue:** Description is short and accurate but provides no usage guidance. An LLM operating in an autonomous loop may call this destructively (wiping legitimate compile errors). A "use this when X, not Y" clause would help (e.g. "Use after the user confirms they have read or saved the existing logs"). Mild — present mostly because there is no read-only counterpart that lets an LLM **snapshot before clear**.
**Confidence:** medium

### A5 — `console-get-logs` does not enumerate which fields are returned
**Location:** `console-get-logs` — method `[Description]`
**Issue:** The description says "Retrieves entries" but never tells the LLM what shape they come back in. Looking at the implementation, each entry returns `[CATEGORY] message` followed by `at file:line` if a file is present. An LLM has no way to know it can rely on file/line in the output (or that it shouldn't expect stack traces, exception type, timestamps, instance-id of the offending Object, etc.).
**Evidence:** `Tool_Console.GetLogs.cs` lines 45 and 150–155.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `console-log` `type` default of `"info"` is misaligned with `console-get-logs`
**Location:** `console-log` param `type`
**Issue:** Default `"info"` is intuitive for callers but is the same string that `console-get-logs` does NOT recognize (see A3). Either default should change to `"log"` or `console-get-logs` should accept `"info"` as a synonym for `"log"`. Either way, the inconsistency is a defect.
**Current:** `string type = "info"`
**Suggested direction:** Reconcile the two enums — either accept both `"info"` and `"log"` everywhere, or pick one canonical name and document it on both tools.
**Confidence:** high

### D2 — `console-get-logs` `count = 20` may truncate compile-error workflows
**Location:** `console-get-logs` param `count`
**Issue:** A fresh compile error storm in Unity often produces 30–80 entries (one per affected file plus cascading errors). Default `20` will silently truncate, and the tool's response does not warn the LLM that more entries existed (`totalCount` is read on line 99 but never reported back). An LLM filtering for `type="error"` after a Script edit may "see" 20 errors and conclude the rest are fine.
**Current:** `int count = 20`
**Suggested direction:** Either raise default to e.g. 100 for the `error`-filtered case, or always include "X of Y total entries shown" in the header so the LLM knows when it's been truncated. Note: the existing header line 183 says `"Console Logs (X entries…)"` but X is the **collected** count, not the total.
**Confidence:** high

### D3 — `console-get-logs` rejects `count <= 0` rather than treating 0 as "no limit"
**Location:** `console-get-logs` param `count`
**Issue:** `count = 0` is rejected with an error (line 56). A common LLM pattern is "give me everything" → many tools accept `0` or `-1` as "unlimited". Currently the only way to get all logs is to pass a large arbitrary number like `int.MaxValue`, which is awkward.
**Current:** `if (count <= 0) return Error(...)`.
**Suggested direction:** Either accept `count = 0` as "no limit" (with documentation), or accept a separate `unlimited: bool = false` param. Document the choice.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Cannot retrieve stack traces for logged exceptions / errors
**Workflow:** After a runtime exception is thrown during Play mode, the LLM wants to read the **stack trace** of the topmost error to identify which script/line failed. This is the canonical debugging loop.
**Current coverage:** `console-get-logs` returns `[CATEGORY] message` and an `at file:line` if `file` is non-empty. No stack trace.
**Missing:** The internal `UnityEditor.LogEntry` exposes a `condition` field (full text) and Unity's own Console window renders a multi-line stack from it. The current tool only reads `message`, not the full condition/stack. There is also no exposure of `instanceID` (the Object reference attached to the log), so the LLM cannot follow `[Game Object] error → which GameObject?`.
**Evidence:** `Tool_Console.GetLogs.cs` lines 83–86 reads only `{message, mode, file, line}`. Verified via `Grep` for `stack|StackTrace|callstack|condition` over `Editor/Tools/Console/` — zero matches.
**Confidence:** high

### G2 — No tool to read a single specific log entry by index
**Workflow:** "Show me the full details of the 3rd error from the top" — common when an LLM has scanned `console-get-logs` output and wants to drill into one entry without re-fetching the whole window.
**Current coverage:** None. `console-get-logs` always paginates from newest to oldest.
**Missing:** A `console-get-log-entry(index: int)` or equivalent that returns full message + stack + instanceID + file/line for one entry.
**Evidence:** Only one read tool (`console-get-logs`) exists; it has no `index`/`entry-id` parameter (`Tool_Console.GetLogs.cs` line 46).
**Confidence:** high

### G3 — No way to count logs without retrieving them
**Workflow:** Polling pattern: "Has a new error appeared since I last looked?" An efficient LLM loop wants a cheap `console-count(type='error')` before deciding whether to fetch full entries.
**Current coverage:** None — `console-get-logs` always fetches and formats entries.
**Missing:** A read-only `console-count(type)` tool. The internal `LogEntries.GetCount()` is already wrapped by the existing implementation (line 75) — it is mechanically trivial to expose, and the underlying `LogEntries.GetCountsByType` API returns per-type counts in a single call.
**Evidence:** `Tool_Console.GetLogs.cs` line 99: `totalCount = (int)getCountMethod.Invoke(...)` — already invoked but only used internally.
**Confidence:** high

### G4 — No tool to clear entries selectively (only nuke-everything available)
**Workflow:** "Clear only existing warnings, keep the errors so I can finish triaging them." Currently `console-clear` is all-or-nothing.
**Current coverage:** `console-clear` calls `LogEntries.Clear()` which clears every entry.
**Missing:** A type-filtered clear, OR a "clear entries matching filterText" variant. Unity's own UI doesn't expose this either, so this is a **lower-priority** capability gap — but for autonomous LLM debugging loops, the absence forces "screenshot then nuke" rather than incremental triage.
**Evidence:** `Tool_Console.Clear.cs` — only one method, no parameters.
**Confidence:** medium

### G5 — No subscription / "tail" mechanism for new logs
**Workflow:** "Run my play-mode test, then tell me every log that appeared during that run." Currently the LLM can only `console-clear` → run → `console-get-logs`, which is racy and destructive.
**Current coverage:** Clear-then-get pattern is the only workflow.
**Missing:** Either (a) a `console-get-logs-since(timestamp)` filter, or (b) a snapshot pattern: `console-snapshot()` returns an opaque cursor, then `console-get-logs(since: cursor)` returns only newer entries. Unity's `LogEntries` API does not directly support cursors, but a wall-clock-time field on each entry would suffice — and the entry's mode-bitmask + monotonic index already provides what's needed.
**Evidence:** No `since`, `after`, or `cursor` parameter exists anywhere in `Tool_Console.GetLogs.cs`.
**Confidence:** medium

### G6 — `console-log` cannot attach a context Object (for click-to-navigate)
**Workflow:** "Log a warning about this GameObject, and let the user click the entry to ping it in the Hierarchy." This is the standard `Debug.LogWarning(message, context)` overload — extremely useful for AI-authored diagnostic logs.
**Current coverage:** `console-log(message, type)` — no context entity ID.
**Missing:** An optional `entityId` / `objectPath` parameter that resolves to a `UnityEngine.Object` and passes it as the second argument to `Debug.LogXxx(message, context)`.
**Evidence:** `Tool_Console.Log.cs` lines 39–47 — all `Debug.LogXxx` calls pass only the prefixed string, never a context.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort). Higher = more attractive (high impact + low effort).

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | A3 + D1 | Ambiguity / Default | 5 | 1 | 25 | high | Reconcile `info`/`log` enum mismatch between `console-log` and `console-get-logs` — silent zero-result bug. |
| 2 | G1 | Capability Gap | 5 | 2 | 20 | high | Expose `condition`/stack-trace and `instanceID` from `LogEntry` — debugging is the #1 console workflow. |
| 3 | G3 | Capability Gap | 4 | 1 | 20 | high | Add a `console-count(type)` read-only tool — `GetCount` already invoked internally; trivial extraction. |
| 4 | D2 | Default | 4 | 1 | 20 | high | `console-get-logs` should report `X of Y total` so truncation is never silent. |
| 5 | A2 | Ambiguity | 3 | 1 | 15 | high | Document the `[Game Deck]` prefix on `console-log` (or remove it, but document either way). |
| 6 | A1 | Ambiguity | 3 | 1 | 15 | high | `console-log` silently swallows unknown `type` values — either error or document the fallthrough. |
| 7 | G6 | Capability Gap | 4 | 2 | 16 | high | Allow `console-log` to take an optional context entity for click-to-navigate. |
| 8 | A5 | Ambiguity | 3 | 1 | 15 | medium | Document the output shape of `console-get-logs` (categorical line + optional `at file:line`). |
| 9 | D3 | Default | 2 | 1 | 10 | medium | Accept `count=0` (or a `unlimited` flag) for "give me everything". |
| 10 | G2 | Capability Gap | 3 | 3 | 9 | high | Add `console-get-log-entry(index)` for drill-down — useful but workaround exists (call `console-get-logs` with narrow filter). |
| 11 | G5 | Capability Gap | 3 | 4 | 6 | medium | Cursor / "since" pattern for tailing logs — high value but requires designing a snapshot model. |
| 12 | G4 | Capability Gap | 2 | 3 | 6 | medium | Selective clear by type/filter — nice-to-have, not blocking. |
| 13 | A4 | Ambiguity | 1 | 1 | 5 | medium | `console-clear` could note that `console-get-logs` should be called first. |

**Top 3 actionable items if Ramon wants the cheapest wins first:**
1. Fix the `info`/`log` enum drift (A3 + D1) — ~10 minutes, prevents a whole class of silent bugs.
2. Expose `condition` (stack trace) and `instanceID` from `LogEntry` (G1) — the existing reflection scaffolding already reads other fields; adding two more is small.
3. Add `console-count` (G3) — one new file, ~30 lines, leverages existing constants.

---

## 7. Notes

- **Cross-domain dependency:** `Tool_Script.Validate.cs` line 84 explicitly tells callers "check console for errors" after a compilation, which makes `console-get-logs` the canonical compile-error inspection tool. G1 (no stack traces) and D2 (default count = 20) hit this workflow directly.
- **Cross-domain check:** I `Grep`'d `console|LogEntries|Debug.Log` across `Editor/Tools/` and confirmed no other domain wraps the LogEntries reflection API. The only collisions are Camera and Reflect tools that mention `Debug.Log` in their own diagnostic emission paths — unrelated.
- **`CategoriseByMode` mask values** are noted in the source as "observed across Unity 2021–6". They are not officially documented by Unity. This is an implementation risk, not a tool-surface defect, but worth flagging for a future hardening pass — if Unity changes a bitmask in 6000.4+, both filtering and counting will silently miscategorise entries.
- **Reflection-based design** is unavoidable here (no public API for reading the console). The current implementation is reasonable; the gaps in this audit are about **what to expose**, not **how it's read**.
- **`ReadOnlyHint`** is correctly set on `console-get-logs` and correctly absent from the other two. No issues.
- **Open question for reviewer:** is the `[Game Deck]` prefix on `console-log` intentional branding, or a debugging artifact? If branding, it should be documented; if artifact, it should probably be removed (or made opt-in via a `prefix` param).
- This audit is small by design — three tools, all simple. The priority list focuses on **enum reconciliation** and **richer log-entry data** because those are where AI-driven debugging loops most often fail.
