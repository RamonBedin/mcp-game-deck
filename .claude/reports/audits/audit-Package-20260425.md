# Audit Report — Package

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Package/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via `Glob Editor/Tools/Package/Tool_Package.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None. Cross-check with `Glob Editor/Tools/Package/*.cs` returned the same 5 files (no helpers, no .asmdef present in this audit's listing).

**Absence claims in this report:**
- All absence claims are backed by the balanced 5/5 coverage and additional cross-domain greps for `PackageManager`, `manifest.json`, and `Client.{Add|Remove|List|Search|Embed|Resolve|Pack|AddAndRemove|GetRegistries}` across `Editor/Tools/`. Package Manager surface lives entirely in this domain — no other domain wraps `UnityEditor.PackageManager.Client`.

**Reviewer guidance:**
- This is a small, mostly stable domain (10 tools across 5 files). The biggest risks are not redundancy but (a) `RemoveRegistry` lying to the LLM (it claims action but only returns instructions), (b) `AddRegistry`'s string-splice JSON manipulation that can corrupt `manifest.json` in known edge cases, and (c) a few capability gaps that block end-to-end registry / dependency workflows.
- Several tools have meaningful overlap (`package-list` vs `package-status` vs `package-ping`) — not catastrophic, but the LLM has three near-identical inspection tools to choose from.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `package-add` | Package / Add | `Tool_Package.Add.cs` | `packageId: string`, `version: string = ""` | no |
| `package-remove` | Package / Remove | `Tool_Package.Remove.cs` | `packageId: string` | no |
| `package-list` | Package / List | `Tool_Package.List.cs` | (none) | yes |
| `package-search` | Package / Search | `Tool_Package.Search.cs` | `query: string` | yes |
| `package-get-info` | Package / Get Info | `Tool_Package.Extended.cs` | `packageId: string` | yes |
| `package-embed` | Package / Embed | `Tool_Package.Extended.cs` | `packageId: string` | no |
| `package-resolve` | Package / Resolve | `Tool_Package.Extended.cs` | (none) | no |
| `package-ping` | Package / Ping | `Tool_Package.Extended.cs` | (none) | yes |
| `package-list-registries` | Package / List Registries | `Tool_Package.Extended.cs` | (none) | yes |
| `package-add-registry` | Package / Add Registry | `Tool_Package.Extended.cs` | `name: string`, `url: string`, `scopes: string = ""` | no |
| `package-remove-registry` | Package / Remove Registry | `Tool_Package.Extended.cs` | `nameOrUrl: string` | no |
| `package-status` | Package / Status | `Tool_Package.Extended.cs` | (none) | yes |

**Total tools:** 12
**Read-only tools:** 6 (`package-list`, `package-search`, `package-get-info`, `package-ping`, `package-list-registries`, `package-status`)

**Internal Unity API surface:**
- `UnityEditor.PackageManager.Client.Add`, `.Remove`, `.List`, `.SearchAll`, `.Embed`, `.Resolve`
- Direct `System.IO.File` reads/writes on `Packages/manifest.json`
- Manual JSON string manipulation (no `JsonUtility`, no `JObject`)

---

## 2. Redundancy Clusters

### Cluster R1 — Three near-identical "is Package Manager working?" inspection tools
**Members:** `package-list`, `package-status`, `package-ping`
**Overlap:** All three call `Client.List(...)`, drain the request synchronously, and emit a status message. `package-ping` returns just `"OK. N packages installed."`. `package-status` returns `"Status: OK\nInstalled Packages: N"`. `package-list` returns the full enumerated list. From the LLM's viewpoint these are concentric circles: `ping ⊂ status ⊂ list`. There is no scenario where `ping` and `status` produce information `list` does not.
**Impact:** Medium. The LLM has to choose among three when asking "what's installed?" or "is PM healthy?". Description text overlaps verbatim ("Checks Package Manager status…", "Returns current Package Manager status…", "Lists all packages…"). Likely cause of shrugging tool-pick errors.
**Confidence:** high

### Cluster R2 — `package-list` vs `package-get-info` overlap
**Members:** `package-list`, `package-get-info`
**Overlap:** `package-get-info` re-iterates the entire `Client.List(true)` result and returns details for one matched package. It cannot be used without first knowing the package name (which usually comes from `package-list`). A `package-list` that accepted an optional `packageId` filter, or a `verbose` flag, would cover both. They share parameter shape if `packageId` becomes optional.
**Impact:** Low–medium. Functional but creates a two-call sequence for the common "tell me about TextMeshPro" intent.
**Confidence:** medium

### Cluster R3 — Registry tools split awkwardly
**Members:** `package-list-registries`, `package-add-registry`, `package-remove-registry`
**Overlap:** Not redundant per se, but `package-remove-registry` does not actually remove anything (see Finding A1) — it only checks whether the name appears anywhere in the manifest text and prints instructions. Functionally, the "remove registry" capability is provided by zero tools, while two tools imply it is provided by one. This is a redundancy of *naming* against *capability*.
**Impact:** High when the user genuinely wants to remove a registry — the LLM will call `package-remove-registry`, get a "success" Text response, and report success despite no change.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — `package-remove-registry` description does not warn that it does NOT remove the registry
**Location:** `package-remove-registry` — `Tool_Package.Extended.cs` (lines 285–313)
**Issue:** Tool description says "Removes a scoped registry from Packages/manifest.json by name or URL." The implementation never modifies the file. It returns `ToolResponse.Text(...)` (success status) telling the user to manually edit. The LLM and user will both believe the registry was removed.
**Evidence:** Method body (lines 311–312):
> `return ToolResponse.Text($"To remove registry '{nameOrUrl}', manually edit Packages/manifest.json and remove the entry from scopedRegistries array, then resolve packages.");`
Plus the description verbatim: `"Removes a scoped registry from Packages/manifest.json by name or URL."`
**Confidence:** high

### A2 — `package-add` description does not enumerate identifier formats
**Location:** `package-add` — `Tool_Package.Add.cs`
**Issue:** Description mentions "registry packages, Git URLs, and local paths" but the `packageId` param description gives only a single registry example. The branching logic (line 45 — skip version concat when `://` or `file:` is present) is invisible to the caller. The LLM has to infer that `version` is silently ignored for Git URLs.
**Evidence:** Param description: `"Package identifier (e.g. 'com.unity.textmeshpro'), Git URL, or local path."` — no Git URL example, no file path example, no statement that `version` is ignored for URL/file forms.
**Confidence:** high

### A3 — `package-embed` description doesn't say what "embed" means or its prerequisites
**Location:** `package-embed` — `Tool_Package.Extended.cs` (lines 82–104)
**Issue:** "Embeds (copies) a registry package into the Packages folder for local editing." A user unfamiliar with UPM internals won't know that this only works for **already installed** registry packages, that it physically copies sources into `Packages/<id>/`, and that it makes Unity treat it as an embedded package (not registry-managed). Also no disambiguation against `package-add` with a `file:` path.
**Evidence:** Description verbatim above. No mention of "must be installed first" or "use this when you want to fork/edit a Unity package locally".
**Confidence:** medium

### A4 — `package-status` and `package-ping` descriptions don't disambiguate
**Location:** `package-status`, `package-ping` — `Tool_Package.Extended.cs`
**Issue:** Per Cluster R1, descriptions are nearly identical and contain no "use this when X, not Y" clause. `package-status` even says "any pending operations" but the implementation does not surface pending operations — only installed count.
**Evidence:**
> `package-ping`: `"Checks Package Manager status and installed package count."`
> `package-status`: `"Returns current Package Manager status and any pending operations."` (claim of "pending operations" is unsupported by implementation — only `installed` count is returned, lines 339–347)
**Confidence:** high

### A5 — `package-add-registry` `scopes` parameter accepts comma-separated string with no validation guidance
**Location:** `package-add-registry` — param `scopes`
**Issue:** Description says `"Scopes as comma-separated (e.g. 'com.company,com.other')."` but does not mention: (a) what happens if empty (line 243 produces a `[""]` array, embedding an empty-string scope into the JSON, which is invalid for UPM), (b) whether whitespace-tolerance applies (it does — `.Trim()`), (c) that scopes must be reverse-domain.
**Evidence:** Lines 243–252:
> `string[] scopeArr = string.IsNullOrWhiteSpace(scopes) ? new[] { "" } : scopes.Split(',');`
This silently produces `"scopes":[""]` when the caller omits scopes — written to `manifest.json` and surviving Unity load is dubious; not flagged to the LLM.
**Confidence:** high

### A6 — `package-search` doesn't say it searches the **installed** registry view (offlineMode=false) or what "registry" means
**Location:** `package-search` — `Tool_Package.Search.cs`
**Issue:** Description says "Unity Package Manager registry" without clarifying it's the configured registries (default + scoped). It uses `Client.SearchAll(offlineMode: false)`, which performs a network call and can be slow/fail offline. No timeout / offline fallback mentioned.
**Evidence:** Description verbatim. Line 34: `Client.SearchAll(offlineMode: false)` — caller cannot opt into offline.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `package-add-registry.scopes` defaults to empty, producing invalid manifest entry
**Location:** `package-add-registry` param `scopes`
**Issue:** Default `""` flows through `string.IsNullOrWhiteSpace(scopes)` → `new[] { "" }` and writes `"scopes":[""]` into `manifest.json`. Empty scope string is not a valid scope — UPM will likely reject the registry or apply nothing. The default is a silent error trap.
**Current:** `string scopes = ""`
**Suggested direction:** Either make `scopes` required (no default), or short-circuit the path with an explicit error when empty/whitespace, or default to producing `"scopes":[]` (still useless but at least syntactically valid). The reviewer should pick the shape; the audit only flags that the current default is a foot-gun.
**Confidence:** high

### D2 — `package-add.version` default `""` is fine; intent is clear, but no doc note about Git/file behaviour
**Location:** `package-add` param `version`
**Issue:** Default of empty is correct for "use latest from registry", but for Git URLs the standard way to pin a revision is `<git-url>#<sha-or-tag>` (appended directly to the URL), not the `@version` syntax. The current implementation skips version-concat for URLs (good) but offers no alternate path for pinning. The LLM will not know to embed `#tag` in `packageId` itself.
**Current:** `string version = ""`
**Suggested direction:** Document in description that for Git URLs, append `#<ref>` to `packageId` directly (this matches Unity's documented Git dependency syntax). Or accept `gitRef` as a separate optional param.
**Confidence:** medium

### D3 — `package-list` cannot opt out of network calls
**Location:** `package-list`
**Issue:** Hard-coded `offlineMode: false, includeIndirectDependencies: false` (line 26). No way for caller to request a fast offline list or pull indirect deps. For a routine "what's installed?" question, network round-trip is unnecessary.
**Current:** No params at all.
**Suggested direction:** Optional `offline: bool = true` (faster, accurate enough for installed listing) and `includeDependencies: bool = false` would cover the common spectrum. Audit only flags the rigidity; doesn't prescribe shape.
**Confidence:** medium

### D4 — `package-search` has no result limit / no pagination
**Location:** `package-search` param set
**Issue:** Search dumps every match with no cap. For a query like `"com"` or `"unity"` this can produce hundreds of entries spanning many KB of tokens. No `limit` parameter, no default truncation.
**Current:** Single `query` param, no limit.
**Suggested direction:** Optional `limit: int = 25` or similar. Audit flags the missing default; planner can decide value.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot actually remove a scoped registry programmatically
**Workflow:** Remove a scoped registry (e.g. OpenUPM, custom company registry) from `manifest.json`.
**Current coverage:** `package-remove-registry` exists in name only — see Finding A1. It detects whether the name/url string appears in the file and returns instructions; it never edits `manifest.json`.
**Missing:** A real removal implementation. Would need to parse `scopedRegistries` array, locate the entry by `name` or `url`, splice it out, and write back. Could reuse the depth-tracking parser already in `ListRegistries` (lines 186–205).
**Evidence:** `Tool_Package.Extended.cs` lines 285–313. Body has zero write side effects:
> No `File.WriteAllText`, no `Client.Resolve()`, no array manipulation. Only `content.IndexOf(nameOrUrl)` and a `ToolResponse.Text(...)` with manual instructions.
**Confidence:** high

### G2 — No tool reads or modifies the `dependencies` block of `manifest.json` directly
**Workflow:** Pin or unpin a package version, switch a registry-installed package to a Git URL or local path **without** going through `Client.Add`/`Client.Remove`. Or: read the *raw* manifest dependency entry to detect lockfile drift.
**Current coverage:** `package-add` / `package-remove` operate via `Client`, which writes to `manifest.json` indirectly. There is no read-side tool for raw manifest content.
**Missing:** Read-only `package-get-manifest` (or equivalent) that exposes raw `dependencies` and `scopedRegistries` JSON. Useful for diff-driven workflows where an LLM needs to know whether a package is registry-pinned vs Git-pinned vs file-referenced before deciding next action. Currently the LLM must use the (presumably-existing) `file-read` tool against `Packages/manifest.json`.
**Evidence:** Greps for `manifest.json` across `Editor/Tools/` return only the two registry tools. No `package-get-manifest`, `package-get-dependencies`, or similar is present in any of the 5 files.
**Confidence:** high

### G3 — No tool reads `packages-lock.json`
**Workflow:** Inspect resolved package versions (with hashes) for reproducibility — e.g. an LLM debugging "why is my collaborator getting a different version?".
**Current coverage:** None. `package-list` shows resolved version per package but not lockfile metadata (hash, depth, source).
**Missing:** A read-only tool exposing `Packages/packages-lock.json` content. Unity offers no `Client` API for this; would be a direct file read.
**Evidence:** Grep for `packages-lock` and `Lock` across `Editor/Tools/Package/` returned no matches.
**Confidence:** high

### G4 — No tool wraps `Client.AddAndRemove` for atomic batch operations
**Workflow:** Switch a project's package set in one operation (e.g. "install URP, remove HDRP, add Cinemachine"). Doing this with separate `package-add` / `package-remove` calls forces 3 sequential UPM resolutions, each blocking the editor.
**Current coverage:** Only single-package `Add` / `Remove`. No batch surface.
**Missing:** Wrapper for `UnityEditor.PackageManager.Client.AddAndRemove(string[] packagesToAdd, string[] packagesToRemove)`. This is a documented Unity API since 2021.2.
**Evidence:** Grep for `Client.AddAndRemove` across `Editor/Tools/` returns zero matches. All current `Client.*` calls are individual `Add`, `Remove`, `List`, `Embed`, `Resolve`, `SearchAll`.
**Confidence:** high

### G5 — Cannot create a tarball / package from a local source folder
**Workflow:** Distribute a local Unity package as a `.tgz` for a coworker or CI system.
**Current coverage:** None.
**Missing:** Wrapper for `UnityEditor.PackageManager.Client.Pack(string packageFolder, string targetFolder)`. Non-trivial but documented.
**Evidence:** Grep for `Client.Pack` returned zero matches. No tool in domain has a "tarball" / "pack" verb.
**Confidence:** medium (this is a less-common workflow — flagged for completeness, not as a top priority)

### G6 — `package-add-registry` uses naive string splicing, can corrupt manifest
**Workflow:** (Anti-)gap: the existing tool covers the workflow but in a fragile way that future additions break.
**Current coverage:** `package-add-registry` writes JSON via `string.Insert` (lines 265, 270).
**Missing:** Robust JSON read-modify-write. The current code:
- Inserts at `depIdx` with trailing comma+space — fine if `dependencies` is the first key but inserts before the opening `{` of the dependencies object if structure changes
- When `scopedRegistries` already exists, inserts the new entry at `arrStart + 1` (right after `[`) followed by a comma — produces a leading comma `[entry,...]` that is **valid** but produces malformed JSON if the array was empty (`[]`) → `[entry,]` which is invalid JSON
**Evidence:** Lines 265 and 270:
> `content = content.Insert(depIdx, $"\"scopedRegistries\":[{entry}],\n  ");`
> `content = content.Insert(arrStart + 1, entry + ",");`
The empty-array case `[]` produces `[entry,]` — invalid JSON, will fail Unity's manifest parser silently.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort).

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | A1 / G1 | Ambiguity + Capability Gap | 5 | 2 | 20 | high | `package-remove-registry` lies about success — no actual removal. Both fix description and implement real removal. |
| 2 | G6 | Capability Gap (correctness) | 5 | 3 | 15 | high | `package-add-registry` JSON splicing can corrupt `manifest.json` on empty `scopedRegistries: []`. |
| 3 | R1 | Redundancy | 4 | 2 | 16 | high | Three overlapping inspection tools (`list`, `status`, `ping`); collapse to one with optional verbosity. |
| 4 | A4 | Ambiguity | 4 | 1 | 20 | high | `package-status` description claims "pending operations" output that the implementation never produces. Fix the description or add the data. |
| 5 | D1 | Default | 4 | 1 | 20 | high | `scopes=""` writes `[""]` (invalid scope) into manifest silently. |
| 6 | A2 | Ambiguity | 3 | 1 | 15 | high | `package-add` description lacks Git URL / local path examples and doesn't say `version` is ignored for those. |
| 7 | D4 | Default | 4 | 2 | 16 | high | `package-search` has no result cap; broad queries flood context. |
| 8 | G4 | Capability Gap | 3 | 2 | 12 | high | No `Client.AddAndRemove` wrapper for batch package operations. |
| 9 | G2 | Capability Gap | 3 | 3 | 9 | high | No raw-manifest read tool; LLM cannot inspect dependency source/format without external file tool. |
| 10 | R2 | Redundancy | 2 | 2 | 8 | medium | `package-get-info` could fold into `package-list` with optional filter. |
| 11 | G3 | Capability Gap | 2 | 2 | 8 | high | No `packages-lock.json` reader for reproducibility checks. |
| 12 | A5 | Ambiguity | 3 | 1 | 15 | high | `scopes` param doesn't document empty/whitespace handling or reverse-domain requirement. |
| 13 | A3 | Ambiguity | 2 | 1 | 10 | medium | `package-embed` doesn't document "must be installed first" prerequisite. |
| 14 | D2 | Default | 2 | 2 | 8 | medium | `package-add.version` doesn't document `#ref` syntax for Git URLs. |
| 15 | D3 | Default | 2 | 1 | 10 | medium | `package-list` cannot opt into offline mode — slower than necessary for routine inspection. |
| 16 | G5 | Capability Gap | 1 | 4 | 2 | medium | No `Client.Pack` wrapper. Less-common workflow. |
| 17 | A6 | Ambiguity | 2 | 1 | 10 | medium | `package-search` doesn't document offline behaviour or that it hits the network. |

**Top three Ramon should look at first:**
1. **Finding A1 / G1** — the lying `package-remove-registry`. The LLM will report fake successes today. Highest combined risk × low effort.
2. **Finding G6** — the manifest-corruption edge case in `package-add-registry`. Latent but catastrophic when triggered.
3. **Finding A4 + Cluster R1** — the redundant inspection tools and the misleading "pending operations" claim. Cheap to fix; clarifies the domain shape immediately.

---

## 7. Notes

- **Cross-domain dependencies:** Greps for `PackageManager`, `manifest.json`, and `Client.{Add|Remove|...}` across `Editor/Tools/` confirm no other domain owns Package Manager surface. The audit's claims of "no tool does X" are scoped to the entire codebase, not just `Editor/Tools/Package/`.
- **`package-list-registries` parser quality:** The depth-tracking bracket parser in `ListRegistries` (lines 186–205) is solid and could be reused for a real `package-remove-registry` (Finding G1). Worth keeping as a helper if the consolidator refactors registry tools.
- **Synchronous polling pattern:** Every tool here uses `while (!request.IsCompleted) Thread.Sleep(100);` on the main thread. This is consistent with other domains' approach to UPM, but worth noting that long operations (e.g. first-time `Client.Add` of a remote Git repo) will block the editor thread until completion. Not flagged as a finding because consistency matters more than per-tool fixes here, but the consolidation-planner may want to consider a shared async helper.
- **`#nullable enable` is consistent across all 5 files.** Good baseline — no fixes needed for null safety pragma.
- **No tool in this domain logs via `McpLogger`.** All errors flow back through `ToolResponse.Error`. This is consistent with the response pattern but means failures don't appear in the Unity console for human debugging. Not flagged as a finding since it's a domain-wide convention question.
- **Open question for reviewer:** Should `package-resolve` exist as its own tool, or should `Client.Resolve()` always be implicit at the end of mutating operations? Currently `Resolve` is only called inside `AddRegistry`. `Add`/`Remove`/`Embed` rely on UPM's auto-resolve. The audit doesn't flag this as a finding, but the planner may want to standardize behaviour.
