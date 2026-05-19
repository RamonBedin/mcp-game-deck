# Audit Report — FindInFile

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/FindInFile/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via Glob over `Editor/Tools/FindInFile/**/*.cs`)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** balanced

**Note on naming pattern:** The standard convention `Tool_[Domain].[Action].cs` produced zero hits because this domain uses a single non-suffixed file `Tool_FindInFile.cs` with one action only. This is unusual relative to other domains (see Section 7) but is a structural observation, not an error.

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Cross-domain files consulted (read-only, for context):**
- `Editor/Tools/Asset/Tool_Asset.Find.cs` — to assess potential overlap with `asset-find`.
- `Editor/Tools/Script/Tool_Script.Read.cs` — to understand the typical follow-up workflow after a content match.

**Absence claims in this report:**
- Coverage is complete (1/1 files), so absence claims about the FindInFile domain are made with high confidence. Cross-domain absence claims are scoped via `Grep` and cite the search pattern used.

**Reviewer guidance:**
- This is a single-tool, single-file domain. Findings are necessarily fewer in number than a typical 5+ tool domain — quality over quantity.
- The tool itself is implemented cleanly and follows the project's C# standards (braces, partial class, XML docs, `MainThreadDispatcher`). Findings here are about API design, defaults, and capability surface — not code quality.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `find-in-files` | Editor / Find in Files | `Tool_FindInFile.cs` | 7 | **no** |

**Method:** `FindInFiles(string pattern, string extension = ".cs", string folder = "Assets", bool regex = false, bool caseSensitive = false, int maxResults = 30, int contextLines = 1)`

**Method-level Description:** "Searches for a text pattern or regex in project files. Returns matching file paths and line numbers with context. Useful for finding usages of a class, method, or string."

**Per-parameter descriptions:**
- `pattern` — "Search pattern (text or regex)."
- `extension` — "File extension filter (e.g. '.cs', '.shader', '.uxml'). Empty for all text files."
- `folder` — "Search folder relative to project root (e.g. 'Assets/Scripts'). Default 'Assets'."
- `regex` — "Use regex pattern matching. Default false (plain text search)."
- `caseSensitive` — "Case sensitive search. Default false."
- `maxResults` — "Maximum number of results. Default 30."
- `contextLines` — "Number of context lines to show around each match. Default 1."

**Internal API surface used:**
- `System.IO.Directory.GetFiles(... SearchOption.AllDirectories)`
- `System.IO.File.ReadAllLines`
- `System.Text.RegularExpressions.Regex` (with `IgnoreCase` option)
- `string.Contains(string, StringComparison)` for plain text path
- `UnityEngine.Application.dataPath` for project root resolution
- Path containment check via `Path.GetFullPath(...).StartsWith(...)` (escape guard)

---

## 2. Redundancy Clusters

No redundancy clusters identified. The domain has exactly one tool.

**Cross-domain note (not a cluster, but worth recording):**
- `asset-find` (Asset domain) searches by Unity metadata (type/label/name) using `AssetDatabase.FindAssets`. `find-in-files` searches by file content. They are semantically distinct and complementary, not redundant. However, an LLM disambiguation hint in `find-in-files`' description ("Use this for content/source-text search; for metadata such as `t:Prefab` or labels, use `asset-find`") would prevent the LLM from picking the wrong tool when a user says "find the Player prefab".

---

## 3. Ambiguity Findings

### A1 — Missing read-only hint
**Location:** `find-in-files` — `Tool_FindInFile.cs:35`
**Issue:** This tool performs only file reads (no writes, no asset modifications), but is missing `ReadOnlyHint = true` on its `[McpTool(...)]` attribute. Sibling read-only tools (`asset-find`, `gameobject-find`, `reflect-find-method`, `package-search`) all set this hint, which influences how the MCP host treats permission gates and confirmation prompts.
**Evidence:** Line 35: `[McpTool("find-in-files", Title = "Editor / Find in Files")]` — no `ReadOnlyHint`. Compare line 22 of `Tool_Asset.Find.cs`: `[McpTool("asset-find", Title = "Asset / Find", ReadOnlyHint = true)]`.
**Confidence:** high

### A2 — No disambiguation against `asset-find` and `script-read`
**Location:** `find-in-files` — method-level Description, line 36
**Issue:** Description says "Useful for finding usages of a class, method, or string" but does not steer the LLM away from sibling search tools. Without a "use this when … not …" clause, an LLM facing a prompt like "find all references to PlayerHealth" might reasonably pick `asset-find`, `reflect-search`, or `find-in-files`.
**Evidence:** Line 36: `"Searches for a text pattern or regex in project files. Returns matching file paths and line numbers with context. Useful for finding usages of a class, method, or string."` — adequate as a standalone description, but says nothing distinguishing it from metadata or reflection-based search.
**Confidence:** high

### A3 — `extension` parameter accepts ambiguous formats
**Location:** `find-in-files` param `extension`
**Issue:** Description shows examples with leading dot (`'.cs'`, `'.shader'`). The implementation builds `$"*{extension}"` (line 67), so passing `"cs"` (no dot) silently produces a wrong glob `"*cs"` that matches any filename ending in "cs" (e.g. `"docs"`, `"specs"`). The description does not warn against this, and there's no input normalization.
**Evidence:** Line 67: `var searchPattern = string.IsNullOrWhiteSpace(extension) ? "*.*" : $"*{extension}";`
**Confidence:** high

### A4 — `regex` flag interaction with `caseSensitive` is undocumented
**Location:** `find-in-files` params `regex` + `caseSensitive`
**Issue:** When `regex=true`, `caseSensitive` controls a `RegexOptions.IgnoreCase` flag (line 75). When `regex=false`, it controls `StringComparison.Ordinal` vs `OrdinalIgnoreCase` (line 84). This is correct behavior, but neither parameter description mentions that `caseSensitive` applies to both modes. If the LLM assumes case sensitivity is regex-only (a common assumption since regex has its own `(?i)` syntax), it may pass redundant or contradictory inputs.
**Evidence:** Param descriptions for `regex` (line 41) and `caseSensitive` (line 42) are independent and don't reference each other.
**Confidence:** medium

### A5 — No mention of binary/encoding behavior
**Location:** `find-in-files` method-level Description
**Issue:** When `extension` is empty, the tool reads `*.*` which can include binary files (`.png`, `.dll`, `.fbx`). `File.ReadAllLines` on binary content can produce nonsense matches or throw (silently swallowed by the `catch { continue; }` at line 100-103). The description does not warn against this nor does it describe any binary detection.
**Evidence:** Line 67-68 (search pattern), line 99-103 (silent exception swallow). The `catch` at line 100 is also empty — see C2 below.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `extension` default of `.cs` may surprise non-script searches
**Location:** `find-in-files` param `extension`
**Issue:** Defaulting to `.cs` is a sensible bias for a Unity project, but this is also the most common cause of "the LLM searched and got zero results" when the user wanted to find content in `.uxml`, `.shader`, `.json`, or `.txt`. The description does mention the default, but the LLM has to remember to override it.
**Current:** `string extension = ".cs"`
**Suggested direction:** Either (a) keep the default but expand the description to list common alternatives the LLM should consider, or (b) consider whether an `"all"` mode (e.g. defaulting to a curated list of text extensions: `.cs .uxml .uss .shader .json .txt .md .asmdef`) would better match LLM expectations. No code change recommendation here — flagging for the planner.
**Confidence:** medium

### D2 — `maxResults = 30` is low for codebase-wide searches
**Location:** `find-in-files` param `maxResults`
**Issue:** With 39 tool domains and 268 tools in this repo alone, a search for a common token (e.g. `"ToolResponse"`, `"MainThreadDispatcher"`, `"McpTool"`) will silently truncate at line 30, hiding the true scope from the LLM. There is no signal in the response that truncation occurred — line 142 reports the count of returned matches, not the total found before the cap.
**Current:** `int maxResults = 30`
**Suggested direction:** Either raise the default to ~100, or change the response to indicate truncation explicitly ("showing 30 of N+ matches; raise maxResults to see more"). The implementation breaks the loop at the cap (line 90-93, 109-112) without counting beyond it, so a true total would require continuing iteration past the cap or a separate count pass. Flagging for the planner.
**Confidence:** high

### D3 — `contextLines = 1` is low for code search
**Location:** `find-in-files` param `contextLines`
**Issue:** A single line of context above and below a match is often insufficient to understand the surrounding code (method signature, class name, etc.). Most CLI grep tools default to 0; IDE searches default to 2-3. The default of 1 is unusual and probably under-tuned for LLM consumption (the LLM benefits from more context to interpret the match).
**Current:** `int contextLines = 1`
**Suggested direction:** Consider 2 or 3 as a default. Low priority.
**Confidence:** low

### D4 — `folder = "Assets"` excludes Packages by default
**Location:** `find-in-files` param `folder`
**Issue:** Default `"Assets"` is reasonable, but a Unity project's source code commonly lives under `Packages/` too (local packages, embedded packages). The description doesn't hint that `Packages/` is searchable, and the LLM may not think to broaden the search when `Assets` returns no results.
**Current:** `string folder = "Assets"`
**Suggested direction:** Update description to mention `Packages/` is also valid, or add a hint that broadening to `""` (project root) is supported if path validation allows it. Note: line 57 enforces project-root containment but accepts any subfolder.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — No "exclude paths" or ignore-folder support
**Workflow:** Developer wants to find usages of a token in their own code, excluding generated/third-party noise (e.g. `Library/`, `Temp/`, `obj/`, `node_modules/`, `Server~/dist/`). With the current tool, searches under `""` or even broad paths under `Assets/` can hit thousands of irrelevant matches.
**Current coverage:** Tool accepts a single `folder` to descend into recursively (`SearchOption.AllDirectories`, line 68). No exclude list.
**Missing:** No way to pass a list of folders or globs to skip. This is a common feature in `ripgrep`, `grep -r --exclude`, and IDE search.
**Evidence:** `Tool_FindInFile.cs:68` — `Directory.GetFiles(searchPath, searchPattern, SearchOption.AllDirectories);` with no filter applied to the returned paths beyond the read step.
**Confidence:** high

### G2 — No multi-extension filter
**Workflow:** Developer wants to search across `.cs`, `.uxml`, and `.uss` simultaneously (e.g. finding all references to a UI element name, which can appear in code, UXML, and USS).
**Current coverage:** `extension` accepts a single string used as `$"*{extension}"` (line 67).
**Missing:** No support for comma-separated extensions, glob alternation (e.g. `*.{cs,uxml,uss}`), or an array parameter. To cover three extensions, the LLM must call the tool three separate times.
**Evidence:** `Tool_FindInFile.cs:39` parameter signature; line 67 single-glob construction.
**Confidence:** high

### G3 — No "files-only" / "matches-per-file count" mode
**Workflow:** Developer wants to know which files contain a token (without reading every match). This is the equivalent of `grep -l` or `rg -l` and is the standard first step for narrowing down a refactor.
**Current coverage:** Tool always returns matched lines with context.
**Missing:** No mode flag for "list files only" or "count matches per file". For broad searches, the result is a wall of context that consumes LLM tokens.
**Evidence:** Method body lines 88-135 always emit `relativePath:line` header followed by context lines for every match. No alternative output mode exists.
**Confidence:** high

### G4 — No "whole word" matching for plain-text mode
**Workflow:** Developer wants to find usages of a class `Player` without matching `PlayerHealth`, `PlayerController`, etc. With regex, the user can write `\bPlayer\b`, but in plain-text mode the only option is substring match.
**Current coverage:** `regex=true` lets the user build word-boundary patterns manually.
**Missing:** No `wholeWord` boolean. This forces the LLM either to enable regex (which then requires it to escape the pattern correctly — easy to get wrong with names containing `.` or `(`) or to accept noisy substring hits.
**Evidence:** Line 114 in plain-text mode uses `lines[i].Contains(pattern, comparison)` with no boundary check.
**Confidence:** medium

### G5 — Silent exception swallow on unreadable files
**Workflow:** Developer searches a folder that contains a locked file, a binary file, or a file with non-UTF8 encoding.
**Current coverage:** Lines 99-103 wrap `File.ReadAllLines` in `try { ... } catch { continue; }` with an empty `catch` block.
**Missing:** This is technically a code-quality gap (project standard forbids empty catches, see CLAUDE.md "Error Handling"). The user has no way to learn that some files were skipped, nor why. There is also no `McpLogger.Error` or `Debug.LogWarning` call on the failure path.
**Evidence:** `Tool_FindInFile.cs:96-103`:
```csharp
try
{
    lines = File.ReadAllLines(file);
}
catch
{
    continue;
}
```
This violates the project's stated rule: *"Empty `catch` blocks are forbidden — must log the error"* (CLAUDE.md → C# Coding Standards → Error Handling).
**Confidence:** high

### G6 — No replace / preview-replace capability
**Workflow:** Developer wants to find-and-replace across files (a common refactor: rename a constant, update a string literal across UXML, fix a typo). With the current tooling, the LLM must read each match via `script-read`, compute the new content, and call `script-apply-edits` per file.
**Current coverage:** Search only.
**Missing:** No `find-and-replace` companion tool. This is intentionally out of scope for a "find" tool, but worth flagging because it's a high-frequency LLM workflow that the FindInFile domain is the natural home for. Could be a separate sibling tool (`find-replace-in-files`) rather than a flag on this one.
**Evidence:** Domain has only `find-in-files`. Cross-domain `script-apply-edits` exists in the Script domain (confirmed via Grep at the top of the audit) but is path-specific, not bulk.
**Confidence:** medium

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort).

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | A1 | Ambiguity / Hint | 4 | 1 | 20 | high | Add `ReadOnlyHint = true` — single-line attribute fix. |
| 2 | G5 | Code quality / Capability | 4 | 1 | 20 | high | Empty `catch` block violates project standard; add `McpLogger.Error` and continue. |
| 3 | D2 | Default | 4 | 2 | 16 | high | `maxResults = 30` truncates silently; either raise default or surface "N+ more" in the response. |
| 4 | G3 | Capability Gap | 4 | 2 | 16 | high | Add a "files-only" or "count" output mode for token-efficient narrowing. |
| 5 | G2 | Capability Gap | 4 | 2 | 16 | high | Support multi-extension filter (array or comma-separated) to cut multi-call overhead. |
| 6 | A2 | Ambiguity | 3 | 1 | 15 | high | Description should disambiguate against `asset-find` and `reflect-search`. |
| 7 | A3 | Ambiguity | 3 | 1 | 15 | high | Normalize/validate `extension` (auto-prepend `.` or reject without dot) and document it. |
| 8 | G1 | Capability Gap | 4 | 3 | 12 | high | Add an `excludeFolders` or `ignoreGlobs` parameter. |
| 9 | A5 | Ambiguity | 3 | 2 | 12 | high | Document binary-file behavior; consider skipping known-binary extensions when `extension` is empty. |
| 10 | G4 | Capability Gap | 3 | 2 | 12 | medium | Add a `wholeWord` flag for plain-text mode. |

Lower-priority findings (D1, D3, D4, A4, G6) are listed in their respective sections but did not crack the top 10.

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `find-in-files` is the natural complement to `script-read` (Script domain) and `asset-find` (Asset domain). Disambiguation guidance (A2) and a possible future `find-replace-in-files` companion (G6) should be considered together with whoever owns the Script and Asset domain refactors.
- The Reflect domain (`reflect-search`, `reflect-find-method`) covers compiled-type discovery, which is a third orthogonal axis (assemblies vs. source vs. assets). Worth surfacing in description guidance so the LLM picks the right axis.

**Structural observation (not a finding, but worth flagging for the reviewer):**
- The FindInFile domain consists of a single tool in a single non-suffixed file (`Tool_FindInFile.cs`), unlike most other domains which use the `Tool_[Domain].[Action].cs` partial pattern even when there is only one action. The class is already declared `partial`, so the file could be renamed to `Tool_FindInFile.FindInFiles.cs` for consistency without code changes. This is purely a naming convention question; the reviewer (Ramon) should decide whether to enforce uniformity. Flagging here, not in findings.

**Workflows intentionally deferred:**
- Find-and-replace (G6) is flagged but not deeply explored — it's a separate-tool conversation, not a `find-in-files` tweak.
- I did not check whether the proxy or chat UI exposes `find-in-files` differently than the raw MCP surface — out of scope for a tool audit.

**Open questions for the reviewer:**
1. Should `find-in-files` gain `ReadOnlyHint = true` and become consistent with the four other read-only "find/search" tools? (My read: yes, this is a clear bug.)
2. Is the empty `catch` block (G5) a blocker that should be fixed before any other work in this domain?
3. Is bulk find-and-replace (G6) in scope for this domain, or does it belong with `script-apply-edits`?
4. Should the file be renamed `Tool_FindInFile.FindInFiles.cs` for convention consistency? (Cosmetic only.)
