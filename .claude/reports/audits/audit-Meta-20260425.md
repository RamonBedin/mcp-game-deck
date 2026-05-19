# Audit Report — Meta

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Meta/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via `Glob Editor/Tools/Meta/Tool_Meta.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** ✅ balanced

**Errors encountered during audit:** None.

**Files not analyzed (if any):** None.

**Absence claims in this report:** Permitted — accounting is balanced and the entire domain (2 files, 2 tools) was inspected. A confirming `Glob **/Tool_Meta*.cs` returned exactly the same two paths.

**Reviewer guidance:**
- This is a tiny domain (2 tools), so the report is short. Most findings concentrate on G1, which is a behavioural correctness issue (the only mutating tool in the domain has no observable effect on the live tool registry).
- Several findings reference `Editor/MCP/Discovery/ToolDiscovery.cs` and `Editor/MCP/Server/McpServer.cs`. Those files are out-of-scope for the audit but were read briefly to verify whether claims about "the registry" (made by `tool-set-enabled`'s description) are accurate. The audit does not propose changes to those files.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `tool-list-all` | Meta / List All Tools | `Tool_Meta.ListAll.cs` | 0 | yes |
| `tool-set-enabled` | Meta / Set Tool Enabled | `Tool_Meta.SetEnabled.cs` | 2 (`toolId: string`, `enabled: bool = true`) | no |

**Internal Unity / .NET API surface used:**
- Both tools: `AppDomain.CurrentDomain.GetAssemblies()`, `Assembly.GetTypes()` (with `ReflectionTypeLoadException` fallback to `ex.Types`), `Type.GetCustomAttributes(typeof(McpToolTypeAttribute), …)`, `Type.GetMethods(BindingFlags.Public | Instance | Static)`, `MethodInfo.GetCustomAttributes(typeof(McpToolAttribute), …)`.
- `tool-set-enabled` additionally reflects on `typeof(McpToolAttribute).GetProperty("Enabled", …)` and `PropertyInfo.SetValue(attr, enabled)`.
- Neither tool talks to `ToolRegistry`, `ResourceRegistry`, `PromptRegistry`, or `McpServer` — they walk reflection metadata directly. This is the root cause of G1 below.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The two tools have orthogonal purposes (read-only listing vs mutating enable/disable) and share no parameters.

---

## 3. Ambiguity Findings

### A1 — `tool-list-all` description undersells output and lacks filter guidance
**Location:** `tool-list-all` — `Tool_Meta.ListAll.cs` line 27
**Issue:** The description says it returns "tool IDs and display names" but does not mention that (a) the list is alphabetically sorted by ID, (b) the list is unfiltered (no domain or substring filter), (c) the AI cannot pass any argument to narrow it. With 268 tools in the registry the output is a very long blob; the AI calling this tool has no way to know in advance whether it's wasting tokens or whether a future filtered alternative exists.
**Evidence:** Verbatim description: *"Scans all assemblies for MCP tools and returns a full list of tool IDs and display names. Useful for discovering available tools without prior knowledge of the registry."*
**Confidence:** medium — this is partly an ambiguity issue and partly a capability gap (see G2). Even without a filter param, naming the sort order would help.

### A2 — `tool-set-enabled` description omits its effective scope
**Location:** `tool-set-enabled` — `Tool_Meta.SetEnabled.cs` line 30
**Issue:** The description says "persists in memory only and is not written to disk", which is true at the attribute level but **misleading at the behavioural level**. The tool registry (`ToolRegistry` populated from `ToolDiscovery.DiscoverTools()` in `McpServer.DiscoverAndRegister`) is built once per AppDomain and never re-reads `attr.Enabled` afterward — see ToolDiscovery.cs lines 63-66 (the only consumer of `Enabled`). After this tool runs, `tool-list-all` will still show the tool, the registry will still expose it, and the AI client will still be allowed to call it. The XML doc on the method (lines 19-21) hints at the mechanism ("persists for the lifetime of the AppDomain session") but the AI-facing `[Description]` glosses over the consequence: from the AI's perspective the call is essentially a no-op.
**Evidence:** Verbatim description: *"Enables or disables a registered MCP tool by its ID for the current session. The change persists in memory only and is not written to disk. Use tool-list-all to discover tool IDs."* Cross-reference: `Editor/MCP/Discovery/ToolDiscovery.cs:63` reads `Enabled` only inside `ScanTypeForTools`, which `McpServer.cs:285` calls exactly once via the `_discovered` guard.
**Confidence:** high — confirmed by reading ToolDiscovery.cs and McpServer.cs end-to-end for the Enabled keyword.

### A3 — Dead-code branch returns a misleading "not supported" message
**Location:** `tool-set-enabled` — `Tool_Meta.SetEnabled.cs` lines 91-96
**Issue:** Code reflects `typeof(McpToolAttribute).GetProperty("Enabled")` and checks `enabledProp.CanWrite`. But `McpToolAttribute.Enabled` is declared `public bool Enabled { get; set; } = true;` (see `Editor/MCP/Attributes/McpToolAttribute.cs:105`), so this branch is unreachable. If anyone ever does flip it to `{ get; }`, the AI would see *"Tool enable/disable not supported at runtime. The McpToolAttribute.Enabled property is read-only at this scope."* — which sounds like a transient runtime restriction rather than a code change. The defensive code is fine, the message is not.
**Evidence:** Verbatim:
```
return ToolResponse.Text("Tool enable/disable not supported at runtime. " +
    "The McpToolAttribute.Enabled property is read-only at this scope.");
```
**Confidence:** high.

### A4 — `enabled` param description repeats the type instead of explaining intent
**Location:** `tool-set-enabled` param `enabled` — `Tool_Meta.SetEnabled.cs` line 33
**Issue:** *"True to enable the tool, false to disable it. Default true."* — 12 words, no example of when an LLM would pick `false` vs `true`. Combined with A2 the AI has no signal that disabling will (in current code) not actually prevent invocations.
**Evidence:** Verbatim quoted above.
**Confidence:** medium.

---

## 4. Default Value Issues

### D1 — `tool-set-enabled.enabled` default of `true` masks the common intent
**Location:** `tool-set-enabled` param `enabled`
**Issue:** The most common reason an LLM (or human) would call this tool is to *disable* a noisy or dangerous tool for the rest of the session. Defaulting `enabled = true` means a call like `tool-set-enabled(toolId: "asset-delete")` silently re-enables an already-enabled tool — confusing telemetry and providing no friction against accidental re-enabling. A required parameter (no default) would force the caller to be explicit and matches the "destructive change requires intent" pattern that the rest of the codebase generally follows.
**Current:** `bool enabled = true`
**Suggested direction:** make `enabled` required (no default) so the caller must state the intent. Alternative: keep the default but flip it to `false`, since "set enabled" reads more naturally as "I'm changing the state, here's the new state" and disabling is the high-value case.
**Confidence:** medium — this is style/UX rather than a hard bug.

---

## 5. Capability Gaps

### G1 — Disabling a tool has no observable effect on the live MCP registry
**Workflow:** "AI assistant accidentally keeps invoking a destructive or noisy tool. The user (or a guard agent) wants to disable that tool ID for the rest of the session so further invocations are rejected." The tool description for `tool-set-enabled` advertises exactly this workflow.
**Current coverage:** `tool-set-enabled` mutates `McpToolAttribute.Enabled` on the reflected attribute instance.
**Missing:** A re-discovery / unregister step. `ToolRegistry` is populated exactly once per AppDomain via `McpServer.DiscoverAndRegister` (gated by `_discovered`), and `ToolDiscovery.ScanTypeForTools` only reads `Enabled` at that initial scan (`ToolDiscovery.cs:63`). After the registry is built, no code path in the server (`Editor/MCP/Server/`) or the registry (`Editor/MCP/Registry/ToolRegistry.cs` — `Grep` for `Enabled` in that file: zero matches) consults `attr.Enabled` again. The mutation therefore changes a piece of metadata that is already cached and unused. The AI client's `tools/list` response, the dispatcher's invocation path, and `tool-list-all` itself (which re-walks reflection but only filters by `attr.Enabled` at line 76 — note: `tool-list-all` *does* respect the flag, so the listing UI lies in the opposite direction from the actual registry) all continue to behave as if nothing happened.
**Evidence:**
- `Tool_Meta.SetEnabled.cs:99` mutates the attribute: `enabledProp.SetValue(attr, enabled);`
- `ToolDiscovery.cs:63` is the sole consumer: `if (!toolAttr.Enabled) { continue; }`
- `McpServer.cs:285-301` runs discovery once under `if (_discovered) return;`
- `Editor/MCP/Registry/ToolRegistry.cs` contains zero references to `Enabled` (verified by `Grep "Enabled" path=…/Registry/ToolRegistry.cs` — no matches).
- Asymmetric side effect: `Tool_Meta.ListAll.cs:76` *does* skip disabled tools when listing, so after `tool-set-enabled(x, false)` the listing hides `x` while the registry still happily executes it. This is worse than a no-op — it actively misleads.
**Confidence:** high — accounting is balanced, both Meta tools were read in full, and the cross-reference to ToolDiscovery / ToolRegistry / McpServer was done by Grep (not inference).

### G2 — No way to fetch a single tool's detail (description, parameter schema, hints)
**Workflow:** "Before calling an unfamiliar tool, the AI wants to read its full description and parameter schema without dumping all 268 tool entries." Today this is only possible by either (a) calling `tool-list-all` and parsing 268 lines (which still doesn't include parameter info — only ID + title), or (b) the AI relying on the schema baked into the MCP `tools/list` payload at session start.
**Current coverage:** `tool-list-all` returns `[id] title` only — no description, no parameters, no `ReadOnlyHint`/`IdempotentHint` flags.
**Missing:** A `tool-describe` (or `tool-get`) tool that takes `toolId` and returns the full `McpToolInfo` payload (description, parameters with their types/defaults/descriptions, hints). The data is already assembled in `McpToolInfo` records by `ToolDiscovery.DiscoverTools()`; exposing one element of that list is a small wrapper. Without this, an AI that has lost or never received the upstream tool list has no introspection path.
**Evidence:** `Tool_Meta.ListAll.cs:82` only adds `KeyValuePair<string, string>(attr.Id, title)` — description and parameter info are deliberately discarded. No other meta tool exists (`Glob **/Tool_Meta*.cs` returns exactly two files).
**Confidence:** high — coverage is complete.

### G3 — No filter / search on the listing
**Workflow:** "Show me every tool in the `animation-` domain" or "find the tool that creates a prefab variant". With 268 tools, dumping the full sorted list every time is wasteful in tokens and forces the caller to do client-side filtering.
**Current coverage:** None — `tool-list-all` accepts zero parameters.
**Missing:** Optional `prefix` / `contains` / `domain` parameter on `tool-list-all` (or a sibling `tool-search`). Implementation would be a one-line `entries.Where(...)` after the existing scan.
**Evidence:** `Tool_Meta.ListAll.cs:28` — method signature is `public ToolResponse ListAll()` with no parameters; the entire method body builds a fully unfiltered list.
**Confidence:** high.

### G4 — No way to query the *current* enable-state of a tool
**Workflow:** "Is `asset-delete` currently enabled?" — useful for guard agents that want to verify state before taking action.
**Current coverage:** Indirect only. `tool-list-all` filters out `Enabled = false` entries (line 76), so absence-from-list signals disabled; but this conflates "doesn't exist" with "exists but disabled" and gives no way to introspect `ReadOnlyHint`, `IdempotentHint`, or other attribute fields.
**Missing:** Either fold this into G2's `tool-describe`, or add a dedicated `tool-is-enabled(toolId)`. Note that even if added, it is subject to the same caveat as G1 — the queried value is the attribute, not what the live registry actually exposes.
**Confidence:** medium — partly a duplicate of G2, partly its own thing.

### G5 — No bulk operations
**Workflow:** "Disable every tool in the `prefab-` domain for this session" or "list all read-only tools".
**Current coverage:** None.
**Missing:** Either bulk variants of `tool-set-enabled` (accepting a prefix or a list of IDs) or filter support that, combined with a hypothetical batch-set, covers the workflow. Lower priority than G1/G2; mentioned for completeness.
**Confidence:** low — it is plausible Ramon does not want bulk meta operations exposed to the AI at all (blast radius). Flagging for awareness, not as a recommendation.

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap (correctness) | 5 | 3 | 15 | high | `tool-set-enabled` mutates a cached attribute; live registry never re-reads it, so disabling does nothing. |
| 2 | A2 | Ambiguity | 4 | 1 | 20 | high | Description for `tool-set-enabled` claims session-scoped persistence but doesn't reveal that the registry ignores the change. Either fix G1 or fix the description; either way the doc must change. |
| 3 | G2 | Capability Gap | 4 | 2 | 16 | high | No `tool-describe`/`tool-get` for fetching a single tool's full schema. Forces AI to dump all 268 entries via `tool-list-all`. |
| 4 | G3 | Capability Gap | 3 | 1 | 15 | high | `tool-list-all` has no filter parameter; with 268 tools this is wasteful per call. |
| 5 | A1 | Ambiguity | 2 | 1 | 10 | medium | `tool-list-all` description doesn't mention sort order or that there is no filter option. |
| 6 | A3 | Ambiguity | 2 | 1 | 10 | high | Dead-code branch in `tool-set-enabled` returns a confusing "not supported at runtime" message that misleads the caller. |
| 7 | D1 | Default Value | 2 | 1 | 10 | medium | `enabled = true` default in `tool-set-enabled` masks the common (disable) intent. |
| 8 | A4 | Ambiguity | 1 | 1 | 5 | medium | `enabled` param description is just "true to enable, false to disable" — adds no value. |
| 9 | G4 | Capability Gap | 2 | 2 | 8 | medium | No way to query a tool's current attribute state (subset of G2). |
| 10 | G5 | Capability Gap | 2 | 3 | 6 | low | No bulk enable/disable. Plausibly intentional. |

Priority formula: Impact × (6 - Effort).

---

## 7. Notes

**The big-picture call for the reviewer:** finding G1 is the only one that's a behavioural defect rather than a polish issue. The two reasonable resolutions are (a) make `tool-set-enabled` actually unregister/re-register the tool from `ToolRegistry` (touching `Editor/MCP/Server/McpServer.cs` and probably exposing a `ToolRegistry.Unregister(string id)` method — which crosses the Meta domain boundary), or (b) demote the tool to honesty: rename it to `tool-mark-disabled-in-listing`, narrow the description to "hides this tool from `tool-list-all` output, but does not block invocation", and decide whether that limited capability is worth keeping. Either way, finding A2 must be addressed.

**Cross-domain dependencies noticed:**
- The Meta domain reaches across the entire codebase via reflection. Unlike most domains it has no Unity API surface — the "platform" it manipulates is the MCP framework itself (`Editor/MCP/Attributes/`, `Editor/MCP/Discovery/`, `Editor/MCP/Registry/`, `Editor/MCP/Server/`). Refactors in this domain will likely require coordinated changes outside `Editor/Tools/Meta/`.
- A consolidated `tool-describe` (G2) would naturally surface fields like `IdempotentHint` and parameter schemas. The MCP framework already computes these via `DiscoveryHelper.BuildParameterList` (`ToolDiscovery.cs:76`); reusing that path would keep Meta in sync with what the protocol actually advertises.

**Workflows intentionally deferred:**
- `tool-call`/dispatch from Meta (i.e. invoking another tool by ID through Meta) is out of scope and probably should stay out — that is the McpServer's job.
- Persisting enable-state to disk across sessions is a v2.0 concern (see `docs/internal/`) and not in scope for this audit.

**Open questions for Ramon:**
1. Do you actually use `tool-set-enabled` from your AI workflow today? If not, the cheapest fix to G1 is to delete the tool entirely and revisit when there's a real use case.
2. If you want a `tool-describe` (G2), should it return JSON-formatted output (parser-friendly) or human-readable text like `tool-list-all` does? Other domains seem to favour text — confirm before planning.
3. Is the omission of `IdempotentHint` from `tool-list-all` deliberate? It's also dropped from the listing today, on top of `ReadOnlyHint` being absent.
