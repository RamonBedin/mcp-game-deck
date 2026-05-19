# Audit Report — UnityDocs

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/UnityDocs/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 3 (via Glob `Editor/Tools/UnityDocs/Tool_UnityDocs.*.cs`)
- `files_read`: 3
- `files_analyzed`: 3

**Balance:** ✅ balanced

**Errors encountered during audit:** None

**Files not analyzed:** None

**Absence claims in this report:**
Permitted — accounting is balanced. Absence claims are scoped to the UnityDocs domain only; cross-domain checks were performed via Grep across `Editor/Tools/` and are noted where used.

**Reviewer guidance:**
- Domain is small (3 tools) and tightly cohesive: all wrap `https://docs.unity3d.com` HTTP fetches plus one `Help.BrowseURL` opener.
- Two tools (`unity-docs-get`, `unity-docs-manual`) are pure read-only HTTP fetches but neither sets `ReadOnlyHint = true`. This is a low-effort, repeatable issue.
- The HTML parsing in `GetDoc.cs` and `GetManual.cs` is regex-based against Unity's docs HTML structure, which Unity may change between releases. This is a fragility worth flagging but is out of scope for "tool consolidation"; treat it as a longer-term maintenance concern.
- The domain duplicates URL-construction logic across all 3 tools — a candidate for a private helper, not a tool consolidation.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `unity-docs-get` | Unity Docs / Get | `Tool_UnityDocs.GetDoc.cs` | 3 (`className`, `memberName=""`, `version=""`) | no |
| `unity-docs-manual` | Unity Docs / Get Manual Page | `Tool_UnityDocs.GetManual.cs` | 2 (`slug`, `version=""`) | no |
| `unity-docs-open` | Unity Docs / Open | `Tool_UnityDocs.OpenDoc.cs` | 2 (`className`, `memberName=""`) | no |

**Internal Unity APIs used:**
- `Help.BrowseURL(string)` — `OpenDoc.cs` line 36
- `MainThreadDispatcher.Execute(...)` — `OpenDoc.cs` line 27
- `System.Net.Http.HttpClient` — shared static field in `GetDoc.cs` (10s timeout); reused in `GetManual.cs`

**Notable shared state:**
- Static `_httpClient` declared in `GetDoc.cs` lines 23–26, accessed by `GetManual.cs` line 40. Couples partial files via implicit ordering.

**Notable internal helpers (not tools):**
- `FetchDocHtml(...)` — `GetDoc.cs` line 130 (handles dot-style → dash-style URL fallback)
- `ParseScriptReferenceHtml(...)` — `GetDoc.cs` line 159
- `StripHtmlTags(...)` — `GetDoc.cs` line 215 (used by both GetDoc and GetManual)
- `DocParseResult` private struct — `GetDoc.cs` line 223

---

## 2. Redundancy Clusters

### Cluster R1 — Overlap between `unity-docs-get` and `unity-docs-open`
**Members:** `unity-docs-get`, `unity-docs-open`
**Overlap:** Both accept the same `(className, memberName)` shape and produce a Unity ScriptReference URL for the same target. `unity-docs-get` returns the parsed page text; `unity-docs-open` opens the URL in a browser. ~67% parameter overlap (2/3 params match), and they would compete for the same user intent ("look at Physics.Raycast docs"). The LLM must guess whether the user wants browser-open or in-chat parsed content.
**Impact:** Medium. The disambiguation is real (browser vs. parsed text), but neither description explicitly says "use this when X, not Y." An LLM working with a user prompt like "show me Physics.Raycast docs" has no clear cue which to choose. `unity-docs-get` is almost always the right answer for headless/agent contexts; `unity-docs-open` is only useful when there is an attended Unity Editor.
**Confidence:** medium (overlap is concrete; whether to consolidate or just disambiguate descriptions is a planner question)

### Cluster R2 — `unity-docs-get` vs `unity-docs-manual` URL-base divergence
**Members:** `unity-docs-get`, `unity-docs-manual`
**Overlap:** Both fetch from `docs.unity3d.com` and share the same `version` parameter semantics. They differ only in URL prefix (`/ScriptReference/` vs `/Manual/`) and identifier shape (className.memberName vs slug). The LLM must categorize whether a topic is "scripting reference" or "manual" — for some topics (e.g. `UIE-USS-Properties-Reference`) this is unambiguous, but for borderline topics (e.g. "physics") the LLM may pick the wrong tool.
**Impact:** Low-medium. Could plausibly be unified behind a single `unity-docs-fetch(target, kind?)` tool, but the current two-tool split is defensible because the URL shape and parsing logic genuinely differ. Flag for planner consideration; not an obvious consolidation win.
**Confidence:** low

---

## 3. Ambiguity Findings

### A1 — `unity-docs-open` missing disambiguation vs `unity-docs-get`
**Location:** `unity-docs-open` — `Tool_UnityDocs.OpenDoc.cs` line 21
**Issue:** Description does not say when to prefer this tool over `unity-docs-get`. In headless/agent contexts opening a browser is rarely useful; the LLM needs a "use this when the user wants to read in their own browser; otherwise prefer unity-docs-get" cue.
**Evidence:** `[Description("Opens the Unity documentation page for a given class or member in the default browser. Uses Unity's built-in Help.BrowseURL with the ScriptReference URL.")]`
**Confidence:** high

### A2 — `unity-docs-get` does not mention browser-open alternative
**Location:** `unity-docs-get` — `Tool_UnityDocs.GetDoc.cs` line 41
**Issue:** Mirror of A1. Description does not mention that `unity-docs-open` is the alternative when the user wants the page itself rather than parsed text. Without this cue, an LLM may use `unity-docs-get` for "open the docs for me" requests.
**Evidence:** `[Description("Fetches Unity ScriptReference documentation for a class or member and returns a parsed summary with description, signatures, and parameters. Works offline-friendly by extracting key information from the HTML page.")]`
**Confidence:** medium ("offline-friendly" phrasing is misleading: the tool requires HTTP, see A4)

### A3 — `unity-docs-manual` `slug` parameter — no concrete URL-mapping example
**Location:** `unity-docs-manual` param `slug` — `Tool_UnityDocs.GetManual.cs` line 26
**Issue:** Description gives example values ('execution-order', 'physics-overview', 'UIE-USS-Properties-Reference') but never explicitly states "this is the path segment between `/Manual/` and `.html`". Users who don't already know Unity's URL structure may pass the page title or a concept name instead.
**Evidence:** `[Description("Manual page slug (e.g. 'execution-order', 'physics-overview', 'UIE-USS-Properties-Reference'). This is the last part of the URL path.")]`
The "last part of the URL path" wording is present but easy to miss; an explicit "Look at the URL `https://docs.unity3d.com/Manual/<slug>.html` and copy the `<slug>` portion" example would be clearer.
**Confidence:** low-medium

### A4 — `unity-docs-get` claims "offline-friendly" but is online-only
**Location:** `unity-docs-get` — `Tool_UnityDocs.GetDoc.cs` line 41
**Issue:** Description says "Works offline-friendly by extracting key information from the HTML page." This is misleading — the tool calls `_httpClient.GetAsync(url)` against `docs.unity3d.com`. There is no offline cache or fallback. "Offline-friendly" appears to mean "extracts compact text instead of opening a browser" but is easy to misread as "works without network."
**Evidence:** `Tool_UnityDocs.GetDoc.cs` line 41 description text. Counter-evidence: line 132 `using var response = await _httpClient.GetAsync(dotUrl);` — pure HTTP fetch with 10s timeout, no fallback path on network failure.
**Confidence:** high

### A5 — `version` parameter format not enumerated
**Location:** `unity-docs-get` param `version`, `unity-docs-manual` param `version`
**Issue:** Description says `"Unity version for versioned docs (e.g. '6000.0'). Empty for latest."` — this is an enum-like string accepting a specific format, but the description does not say which formats Unity's docs server actually accepts (`6000.0`, `2023.3`, `2022.3LTS`?). An LLM passing `"6000.0.3f1"` (the full version string from `Application.unityVersion`) would silently get a 404.
**Evidence:** Same description string in `GetDoc.cs` line 45 and `GetManual.cs` line 27.
**Confidence:** medium

### A6 — `unity-docs-open` does not accept `version` parameter
**Location:** `unity-docs-open` — `Tool_UnityDocs.OpenDoc.cs` line 22–25
**Issue:** Inconsistent with `unity-docs-get`/`unity-docs-manual`, which both expose `version`. Opening always points to `https://docs.unity3d.com/ScriptReference/` (latest), with no way to open the version-specific URL. Not an "ambiguity" per se but an asymmetry the description does not flag — users may expect parity.
**Evidence:** `Tool_UnityDocs.OpenDoc.cs` line 35: `string url = $"https://docs.unity3d.com/ScriptReference/{page}";` — hard-coded to latest.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `version=""` semantics rely on caller knowing "" means latest
**Location:** `unity-docs-get` param `version`, `unity-docs-manual` param `version`
**Issue:** Empty string is used as a sentinel for "latest". This is documented in the param description ("Empty for latest"), so it's not strictly hidden, but it's a magic default. A nullable string with default `null` and explicit "omit for latest" wording would be more idiomatic.
**Current:** `string version = ""`
**Suggested direction:** Consider nullable (`string? version = null`) for clearer intent, or document that `""` is the documented sentinel. Low priority — current behavior is functional.
**Confidence:** low

### D2 — `memberName=""` does double duty as "class-only" sentinel
**Location:** `unity-docs-get` param `memberName`, `unity-docs-open` param `memberName`
**Issue:** Empty `memberName` triggers the class-only URL path (`{className}.html`). The default and behavior are documented, so this is acceptable, but flagging for completeness alongside D1 — same nullable-vs-empty-string consideration applies.
**Current:** `string memberName = ""`
**Suggested direction:** Same as D1.
**Confidence:** low

### D3 — No defaults missing; no required-but-commonly-same params
None observed. `className` and `slug` are genuinely required and have no obvious "common case" default.

---

## 5. Capability Gaps

### G1 — Read-only intent not surfaced (`ReadOnlyHint`)
**Workflow:** Agent host (Claude/orchestrator) wants to determine which tools are safe to call without user confirmation in restricted modes. `ReadOnlyHint = true` is the project's documented mechanism for this (per `CLAUDE.md` "Marking Read-Only Tools").
**Current coverage:** `unity-docs-get` and `unity-docs-manual` are pure HTTP GETs with zero side effects in Unity. `unity-docs-open` causes a browser side effect (`Help.BrowseURL`) so should NOT be marked read-only.
**Missing:** Neither `GetDoc.cs` nor `GetManual.cs` declares `ReadOnlyHint = true`.
**Evidence:** Grep for `ReadOnlyHint` across `Editor/Tools/UnityDocs/` returned no matches. `Tool_UnityDocs.GetDoc.cs` line 40: `[McpTool("unity-docs-get", Title = "Unity Docs / Get")]` — no `ReadOnlyHint`. `Tool_UnityDocs.GetManual.cs` line 23: same.
**Confidence:** high (full domain coverage; absence verified)

### G2 — No "search" capability against Unity docs
**Workflow:** Developer asks "where in the Unity docs is X documented?" without knowing the exact class/member or slug. Today the LLM must guess the className and call `unity-docs-get`, hoping for a hit; on miss it gets back "Documentation not found for 'X'. Check the class/member name spelling."
**Current coverage:** None. `unity-docs-get` requires exact `className`; `unity-docs-manual` requires exact `slug`.
**Missing:** No tool wraps Unity's docs search endpoint (`https://docs.unity3d.com/ScriptReference/30_search.html?q=...`) or provides a fuzzy/listing search.
**Evidence:** Grep across `Editor/Tools/` for `unity-docs|docs.unity3d.com|ScriptReference` returned only the 3 UnityDocs files; no search tool exists in any domain.
**Confidence:** high (cross-domain search performed; absence verified)

### G3 — No way to fetch a specific section of a long manual page
**Workflow:** Developer wants only the "Component lifecycle" section of `execution-order.html` — the full page parses into many sections and is verbose for the LLM context window.
**Current coverage:** `unity-docs-manual` returns ALL `<h2>`/`<h3>` sections concatenated. There is no `section` filter.
**Missing:** No parameter on `unity-docs-manual` to filter by heading, no auxiliary "list-sections" tool.
**Evidence:** `Tool_UnityDocs.GetManual.cs` lines 57–81: section regex iterates all `<h2|h3>` blocks unconditionally and appends every paragraph found.
**Confidence:** medium (workflow is plausible; whether it's high-impact depends on observed LLM context overflows)

### G4 — `unity-docs-open` cannot open versioned or Manual pages
**Workflow:** "Open the Unity 6000.0 docs for `Physics.Raycast`" or "Open the Manual page for `execution-order` in my browser."
**Current coverage:** `unity-docs-open` opens latest ScriptReference only.
**Missing:** No `version` parameter and no Manual-vs-ScriptReference toggle.
**Evidence:** `Tool_UnityDocs.OpenDoc.cs` line 35: `string url = $"https://docs.unity3d.com/ScriptReference/{page}";` — hard-coded. The `version` parameter present on the other two tools is absent here.
**Confidence:** high

### G5 — Docs HTML parsing is brittle to Unity layout changes
**Workflow:** Maintain reliable parsing across Unity doc-site refreshes.
**Current coverage:** Hand-rolled regex against specific CSS class names (`subsection`, `signature-CS`, `codeExampleCS`).
**Missing:** No HTML parser library, no graceful degradation if regex misses (returns empty section silently). If Unity renames a class, the tool returns blank fields and the agent doesn't know parsing failed vs. content genuinely absent.
**Evidence:** `Tool_UnityDocs.GetDoc.cs` lines 162, 169, 176, 192, 199 — regex literal class-name matches. `ParseScriptReferenceHtml` returns `DocParseResult` with empty fields on no-match (lines 161–206); no warning is surfaced.
**Confidence:** medium (this is a maintenance concern, not a strict capability gap; flagging for awareness)

### G6 — No package-docs support
**Workflow:** Fetch docs for a Unity package (e.g. URP, Input System) — these live at `https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.0/manual/index.html`, NOT at the main `/Manual/` or `/ScriptReference/` paths.
**Current coverage:** None. `unity-docs-get` only knows the ScriptReference base URL; `unity-docs-manual` only knows the Manual base URL.
**Missing:** No tool accepts a package identifier or arbitrary docs URL.
**Evidence:** `Tool_UnityDocs.GetDoc.cs` line 54 hardcodes `https://docs.unity3d.com/ScriptReference` (with `version` interpolation). `Tool_UnityDocs.GetManual.cs` line 35 hardcodes `https://docs.unity3d.com/Manual`. Neither can hit `/Packages/...`.
**Confidence:** high (full domain coverage; absence verified)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 4 | 1 | 20 | high | Add `ReadOnlyHint = true` to `unity-docs-get` and `unity-docs-manual` |
| 2 | A4 | Ambiguity | 4 | 1 | 20 | high | Remove "offline-friendly" from `unity-docs-get` description (misleading) |
| 3 | A1 | Ambiguity | 3 | 1 | 15 | high | Add "use this when user wants browser-open; otherwise prefer unity-docs-get" to `unity-docs-open` |
| 4 | G4 | Capability Gap | 3 | 2 | 12 | high | `unity-docs-open` should accept `version` and a Manual/ScriptReference selector |
| 5 | A6 | Ambiguity | 3 | 2 | 12 | high | Document or remove the `version`-parameter asymmetry on `unity-docs-open` |
| 6 | G2 | Capability Gap | 4 | 4 | 8 | high | No search capability over Unity docs; agents have to guess names |
| 7 | A5 | Ambiguity | 3 | 1 | 15 | medium | Enumerate accepted `version` formats (e.g. '6000.0', not '6000.0.3f1') |
| 8 | R1 | Redundancy | 3 | 2 | 12 | medium | `unity-docs-get` vs `unity-docs-open` lack mutual disambiguation |
| 9 | G6 | Capability Gap | 3 | 4 | 6 | high | No way to fetch package docs (`/Packages/com.unity.*` URLs) |
| 10 | G3 | Capability Gap | 2 | 3 | 6 | medium | `unity-docs-manual` returns full page; no section filter |
| 11 | G5 | Capability Gap | 3 | 5 | 3 | medium | Docs HTML regex parsing is fragile; silent failure on layout change |
| 12 | A3 | Ambiguity | 2 | 1 | 10 | low-medium | `slug` description could be clearer about URL-path mapping |
| 13 | D1/D2 | Default | 1 | 1 | 5 | low | Empty-string sentinels for `version` and `memberName` |
| 14 | R2 | Redundancy | 2 | 4 | 4 | low | Possible (but probably-not-worth-it) `unity-docs-fetch(kind, target)` unification |

Priority formula: Impact × (6 − Effort). Higher = better ROI.

**Top recommendations for the planner:**
1. G1 + A4 + A1 + A6 are description/attribute-only fixes that together close ~half the LLM-confusion surface in this domain. They're cheap and high-value.
2. G4 (extend `unity-docs-open` with `version` and Manual support) is a small expansion that brings parity across the three tools.
3. G2 (search) is the single biggest capability gain — but it's also a real new feature, not a refactor; flag for v1.2 backlog rather than the current consolidation pass.

---

## 7. Notes

**Cross-domain observations:**
- The UnityDocs domain is independent — no other domain references its tools or shares state with it. Cross-domain Grep for `unity-docs` returned only the 3 files in this domain.
- `OpenDoc.cs` is the only tool here that wraps a Unity Editor API (`Help.BrowseURL`) and correctly uses `MainThreadDispatcher.Execute(...)`. The two HTTP-fetching tools do not need main-thread dispatch.

**Coupling concern (not a tool finding, but worth flagging to the planner):**
- The static `_httpClient` field is declared in `Tool_UnityDocs.GetDoc.cs` but used by `Tool_UnityDocs.GetManual.cs`. This works because they're partial classes, but a reader looking only at `GetManual.cs` cannot tell where `_httpClient` is configured. If the planner consolidates or relocates files, the static field's home should be made obvious (e.g. moved to a dedicated `Tool_UnityDocs.cs` with the `[McpToolType]` attribute, or extracted to a small private helper class).

**Out of scope for this audit:**
- Whether to maintain a local cache of fetched docs (offline mode for real). This is a feature decision, not a tool-shape decision.
- Whether to migrate from regex parsing to an HTML parser dependency. Same.

**Open questions for the reviewer:**
- Should `unity-docs-open` be deprecated in favor of returning the URL as text from `unity-docs-get` and letting the user click? In agent contexts the browser-open is rarely useful. (R1 / A1 territory.)
- Is "search" (G2) in scope for the current consolidation cycle, or deferred to v1.2 feature work?
- Is package-docs support (G6) something Ramon wants — given the test project is 2D URP, URP package docs are very plausibly something a user would ask for.
