# Audit Report — Ping

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Ping/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via `Glob` on `Editor/Tools/Ping/*.cs`)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Coverage is balanced (1/1), so absence claims are made with full domain coverage. They remain narrowly scoped (e.g. "the only tool in this domain does not include X") rather than project-wide.

**Initial Glob anomaly (resolved):**
- The conventional pattern `Tool_Ping.*.cs` returned zero matches because the file is named `Tool_Ping.cs` (no per-action suffix). This is a deviation from the documented `Tool_[Domain].[Action].cs` convention noted in `CLAUDE.md`. A second Glob (`Editor/Tools/Ping/*.cs`) confirmed exactly one source file. No file was missed.

**Reviewer guidance:**
- The Ping domain is intentionally minimal (one tool). It also overlaps in name and concept with per-domain "ping" tools that already exist in other domains (e.g. `camera-ping`, `physics-ping`). Treat this audit primarily as a check on description quality and meta-level naming/tier hygiene, not as a refactor target with a lot of surface area.
- The hard-coded version string in the response (`v1.0.0`) is stale — `package.json` is on `1.1.0` as of this audit.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `specialist-ping` | Specialist / Ping | `Tool_Ping.cs` | 1 (`message: string? = null`) | no |

**Method-level description (verbatim):**
> "Health check tool for the MCP Game Deck extension package. Returns a pong response confirming the package is loaded and tools are registered. Use this to verify the extension is working correctly."

**Parameter description (verbatim):**
- `message`: "Optional message to echo back in the response."

**Internal Unity API surface used:**
- `Application.unityVersion`
- `Application.platform`
- `MainThreadDispatcher.Execute(...)`
- `ToolResponse.Text(...)`

No editor state is read or written beyond returning version/platform constants. The tool is effectively a pure read.

---

## 2. Redundancy Clusters

### Cluster R1 — "Ping" naming used inconsistently across domains
**Members:** `specialist-ping` (Ping domain), `camera-ping` (Camera domain), `physics-ping` (Physics domain)
**Overlap:** All three carry the verb "ping" but mean different things:
- `specialist-ping` = MCP package liveness / version echo (no Unity inspection).
- `camera-ping` = scene camera count + Cinemachine availability summary.
- `physics-ping` = global physics settings summary.

The two domain-scoped pings are actually domain "status" / "summary" tools that happen to be branded as "ping". The Ping domain tool is the only one that fits the conventional liveness-probe meaning of "ping". This is a discoverability hazard for the LLM: when a user says "ping the project" or "ping camera", which one wins? When the LLM wants a liveness check, will it pick the Camera one because the description ends with "Safe to call at any time"?
**Impact:** Low-to-medium. There is a real risk of the LLM choosing `camera-ping`/`physics-ping` for liveness checks (because they also "ping" and are read-only), or choosing `specialist-ping` when expecting domain status. Disambiguation in descriptions would solve this without code changes.
**Confidence:** medium — based on description text and naming alone; no live LLM trace data.

**Note:** This cluster is cross-domain. The fix lives partly outside the Ping domain (renaming/clarifying `*-ping` tools elsewhere or tightening this one's description). The auditor flags it but defers the cross-domain action to consolidation-planner / Ramon.

---

## 3. Ambiguity Findings

### A1 — "Specialist / Ping" title is opaque
**Location:** `specialist-ping` — `Tool_Ping.cs` line 25
**Issue:** The Title `Specialist / Ping` and ID prefix `specialist-` do not map to any documented "Specialist" domain in `CLAUDE.md` (which lists 39 domains; "Specialist" is not one of them). The directory is `Editor/Tools/Ping/`, the namespace bucket is implicitly "Ping", but the Title declares it as "Specialist". This will confuse any agent UI or tool browser that groups by Title prefix.
**Evidence:**
```csharp
[McpTool("specialist-ping", Title = "Specialist / Ping")]
```
**Confidence:** high — the mismatch is verifiable from `CLAUDE.md` directory listing and the file path.

### A2 — Description does not disambiguate from `camera-ping` / `physics-ping`
**Location:** `specialist-ping` — `Tool_Ping.cs` line 26
**Issue:** Per-domain `*-ping` tools exist (`camera-ping`, `physics-ping`) that are also read-only "summary" tools. This tool's description says "Use this to verify the extension is working correctly" but never says "use this rather than `camera-ping` / `physics-ping` when you only need to confirm the package is loaded; those return domain-specific state, not package liveness."
**Evidence:** The description never references the existence of similarly named tools, even though three exist project-wide.
**Confidence:** medium — disambiguation guidance is best-practice but not strictly required when names diverge.

### A3 — `message` parameter description is minimal
**Location:** `specialist-ping` param `message` — `Tool_Ping.cs` line 28
**Issue:** Description ("Optional message to echo back in the response.") is clear but offers no example value and no statement of what happens when omitted (the tool returns the base pong line without an `Echo: ...` suffix). It is borderline; under the 5-word floor it would fail, but at 8 words it passes.
**Evidence:**
```csharp
[Description("Optional message to echo back in the response.")] string? message = null
```
**Confidence:** low — the parameter is genuinely trivial; any "example" would feel forced. Including this for completeness only.

---

## 4. Default Value Issues

### D1 — Hard-coded version string drifts from `package.json`
**Location:** `specialist-ping` — `Tool_Ping.cs` line 33
**Issue:** Not strictly a "default value" but the closest category — the response embeds `MCP Game Deck v1.0.0` as a literal string while `package.json` declares version `1.1.0`. A liveness/version probe that lies about the version defeats its own purpose. The version should be sourced at runtime (e.g. via `PackageInfo.FindForAssembly(...)` or a generated constant) rather than literal.
**Current:**
```csharp
var response = $"pong — MCP Game Deck v1.0.0 loaded. " + ...
```
**Suggested direction:** Read the version dynamically (no specific implementation prescribed by the auditor) so it cannot drift. Failing that, a build-time constant referencing `package.json` is acceptable.
**Confidence:** high — the drift is a verbatim string vs. `package.json` mismatch.

### D2 — `message` default is fine
**Location:** `specialist-ping` param `message`
**Issue:** None. `null` default is correct for an optional echo string and is documented in the description.
**Confidence:** high.

---

## 5. Capability Gaps

### G1 — Liveness probe surfaces no useful diagnostics beyond "loaded"
**Workflow:** A developer (or the orchestrator agent) suspects the MCP server is unhealthy and runs the liveness probe to triage. They want to learn: package version, transport state (TCP listener bound? port?), tool registry size (e.g. "268 tools registered"), assembly-reload state (locked / unlocked), and recent error count.
**Current coverage:** `specialist-ping` returns version (stale, see D1), Unity version, platform, and an optional echo. None of the actionable diagnostic state above is reported.
**Missing:** Any of (a) tool registry count, (b) TCP listener bound-port confirmation, (c) `EditorApplication.isCompiling` / assembly-reload flag, (d) current MCP server uptime or last-reload timestamp. These are cheap to read and would make this tool the obvious first call for any debugging session.
**Evidence:** `Tool_Ping.cs` lines 31-41 — the entire body is a string concatenation of `Application.unityVersion` + `Application.platform`. No registry, no transport, no editor-state inspection.
**Confidence:** high — full file coverage; the absent state is verifiable by inspection.

### G2 — Tool is not marked `ReadOnlyHint = true` despite being a pure read
**Workflow:** Hosts and the agent UI use `ReadOnlyHint` to allow ping-style probes during locked / read-only sessions and to skip permission prompts. A developer in a paranoid permission profile expects "the ping tool" to always be safe.
**Current coverage:** `specialist-ping` performs no Unity write of any kind. It only formats `Application.unityVersion` / `Application.platform` into a string. The peer tools `camera-ping` (line 21) and `physics-ping` (line 20) are both declared `ReadOnlyHint = true`. This tool is not.
**Missing:** `ReadOnlyHint = true` on the `[McpTool(...)]` attribute.
**Evidence:**
```csharp
// Tool_Ping.cs:25
[McpTool("specialist-ping", Title = "Specialist / Ping")]
// vs. Tool_Camera.Ping.cs:21
[McpTool("camera-ping", Title = "Camera / Ping", ReadOnlyHint = true)]
```
**Confidence:** high — the ReadOnly status of the sibling pings and the empty side-effect surface of this method are both directly verifiable.

### G3 — No structured (machine-readable) response shape
**Workflow:** External tooling (the TS proxy, an orchestrator agent, a CI check) parses the ping response to assert version. A free-form string (`"pong — MCP Game Deck v1.0.0 loaded. Unity 6000.0..., Platform: ..."`) requires brittle regex; a JSON payload or labeled key/value lines would not.
**Current coverage:** Single string returned via `ToolResponse.Text(...)`. No structured fields.
**Missing:** Either a structured response type (if `ToolResponse` supports JSON payloads — auditor did not verify this surface area inside scope) or a stable, line-oriented `key: value` format.
**Evidence:** `Tool_Ping.cs` lines 33-40 — the response is concatenated free-form text.
**Confidence:** low — this gap is real only if external callers actually try to parse the response. The Server~ proxy may treat it as opaque text. Flagged for the reviewer to confirm scope.

---

## 6. Priority Ranking

Priority = Impact × (6 - Effort). Higher is more urgent.

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | D1 | Default / Stale Constant | 4 | 1 | 20 | high | Hard-coded `v1.0.0` in pong string vs. `package.json` `1.1.0` — version probe lies. |
| 2 | G2 | Capability Gap | 4 | 1 | 20 | high | Missing `ReadOnlyHint = true`; sibling `*-ping` tools all set it. |
| 3 | A1 | Ambiguity / Naming | 4 | 1 | 20 | high | Title `Specialist / Ping` doesn't match the `Ping` directory or any documented domain. |
| 4 | G1 | Capability Gap | 4 | 3 | 12 | high | Liveness probe returns only version+platform; tool count, transport state, reload state are missing. |
| 5 | R1 | Redundancy / Naming | 3 | 2 | 12 | medium | Three different `*-ping` tools mean three different things; LLM may pick wrong one. |
| 6 | A2 | Ambiguity | 3 | 1 | 15 | medium | Description does not disambiguate from `camera-ping` / `physics-ping`. |
| 7 | G3 | Capability Gap | 2 | 3 | 6 | low | No structured response shape; free-form text only. |
| 8 | A3 | Ambiguity | 1 | 1 | 5 | low | `message` param has no example; trivial. |

(The top three are all 1-effort, so they are effectively a single small patch despite three findings.)

---

## 7. Notes

**Domain shape:**
The Ping domain has a single tool. That is fine; not every domain needs to grow. The audit's center of gravity is therefore description quality, naming hygiene, and the small set of capability-extension opportunities listed in Section 5 — not consolidation.

**Cross-domain dependencies noticed:**
- `Server~/prompts/core-system-prompt.md` line 82 lists `specialist-ping` under the **Meta** group, not under a "Specialist" or "Ping" group. So the system prompt and the Title (`Specialist / Ping`) and the directory (`Ping`) all disagree on where this tool belongs. The reviewer may want consolidation-planner to look at this holistically: either rename the domain to `Specialist`, rename the Title/ID to `meta-ping`, or move the tool under `Editor/Tools/Meta/` and retire the `Ping` directory entirely. The auditor names this only as an open question — choosing among them is out of scope.
- The `camera-ping` and `physics-ping` tools (in their respective domains) are not part of this audit's scope but are referenced as comparators for ReadOnlyHint and naming. If the project standardises "ping = liveness only", those two should be renamed (e.g. `camera-status`, `physics-status`); if "ping = quick read-only summary", then `specialist-ping`'s description should match that meaning.

**Workflows intentionally deferred:**
- A `mcp-server-status` extended tool (transport, registry, reload state) is touched on in G1 but is not specified here — that's planner territory.
- Whether to introduce a structured response type (G3) depends on how the TS proxy and Agent SDK consume responses today; the auditor did not read those code paths.

**Open questions for the reviewer:**
1. Is "Specialist" a planned domain rename, or is `Title = "Specialist / Ping"` a leftover from an earlier iteration?
2. Should the version string be sourced at runtime, or is the package version intentionally pinned for some build reason?
3. Is the Ping domain expected to grow (more meta/diagnostic tools), or should it be folded into Meta?
