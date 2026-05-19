# Audit Report — Script

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Script/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 6 (via Glob `Editor/Tools/Script/Tool_Script.*.cs`)
- `files_read`: 6
- `files_analyzed`: 6

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted. All six tool files were read end-to-end. Cross-domain checks were also performed (`Console`, `RecompileScripts`, `Asset`) to substantiate gap claims.

**Reviewer guidance:**
- This domain is small (6 files, 6 tools) and its API surface is internally consistent: every write tool routes through `ValidateScriptPath` and `ValidateFileSize`, which is good. The most interesting findings are around (a) overlap between `Update` and `ApplyEdits`, (b) the hand-written JSON parser for edits, and (c) capability gaps that force the LLM to bounce out to other domains (`Console`, `RecompileScripts`) for normal compile-error workflows.
- One semantic risk: this `Tool_Script` partial class declares a method called `Read` (line 24 of `Tool_Script.Read.cs`). The auditor's tool of the same name is unrelated; just flagging because LLMs may confuse them in plans.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `script-create` | Script / Create | `Tool_Script.Create.cs` | 4 (`path`, `template="MonoBehaviour"`, `namespaceName=""`, `className=""`) | no |
| `script-delete` | Script / Delete | `Tool_Script.Delete.cs` | 1 (`path`) | no |
| `script-validate` | Script / Validate | `Tool_Script.Validate.cs` | 1 (`path`) | yes |
| `script-read` | Script / Read | `Tool_Script.Read.cs` | 1 (`path`) | yes |
| `script-update` | Script / Update | `Tool_Script.Update.cs` | 2 (`path`, `content`) | no |
| `script-apply-edits` | Script / Apply Edits | `Tool_Script.ApplyEdits.cs` | 2 (`path`, `editsJson`) | no |

**Internal Unity APIs used:**
- `AssetDatabase.MoveAssetToTrash` (Delete)
- `AssetDatabase.Refresh` (Create)
- `AssetDatabase.ImportAsset` (Update, ApplyEdits)
- `EditorApplication.isCompiling` (Validate)
- `CompilationPipeline.GetAssemblies` (Validate)
- `File.ReadAllText` / `File.WriteAllText` / `Directory.CreateDirectory` (all writers)

**Shared private helpers** (defined in `ApplyEdits.cs` alongside the partial-class XML summary):
`ValidateScriptPath`, `ValidateFileSize`, `ParseEditOperations`, `ExtractJsonObjects`, `ExtractJsonStringField`, `SplitIntoLines`, `JoinLines`, `TruncateForLog`.

---

## 2. Redundancy Clusters

### Cluster R1 — Full-overwrite vs. patch-edit on the same file
**Members:** `script-update`, `script-apply-edits`
**Overlap:** Both tools write to the same script file and call `AssetDatabase.ImportAsset(path)` afterwards. They differ only in *how* they specify the new content: `Update` takes a full-content string, `ApplyEdits` takes a JSON array of `replace` / `insert_after` / `delete_line` ops. For very small changes, an LLM has to choose between (a) reading + composing a full file and calling `script-update`, or (b) emitting an edits array and calling `script-apply-edits`. There is no per-tool guidance telling it which to prefer.
**Impact:** Medium. The tools are not strict duplicates (small targeted edits vs. wholesale rewrites are genuinely different), but neither description disambiguates them. An LLM optimising for token usage will sometimes pick `script-update` for a 5-character change and re-emit an entire 800-line file, or pick `script-apply-edits` for a wholesale rewrite and emit a verbose JSON.
**Confidence:** medium — overlap is real and observable from the descriptions, but consolidation may be undesirable; this cluster is more of a "needs disambiguation" finding than a "merge these" finding.

### Cluster R2 — Compilation-status reporting split across domains
**Members:** `script-validate`, `recompile-status` (in `Editor/Tools/RecompileScripts/`), `recompile-scripts`
**Overlap:** `script-validate` reports `EditorApplication.isCompiling` and lists which assembly contains a given script. `recompile-status` reports `EditorApplication.isCompiling`, `isPlaying`, `isPaused`, and lists *all* assemblies. `recompile-scripts` triggers compilation and also reports `isCompiling`. Three tools, three subtly different views of "is the project compiling?" — the LLM has to pick.
**Impact:** Medium. This is cross-domain so out of strict scope, but worth flagging because `script-validate`'s usefulness is partially shadowed by `recompile-status` (and vice versa).
**Confidence:** medium — cross-domain claim; flagged for the planner rather than acted on here.

---

## 3. Ambiguity Findings

### A1 — `script-create` does not say what happens when the file already exists
**Location:** `script-create` — `Tool_Script.Create.cs`
**Issue:** The implementation calls `File.WriteAllText(path, ...)` unconditionally, which silently overwrites an existing script. The method-level description ("Creates a new C# script from a template …") implies a fresh creation, and there is no parameter like `overwrite` or `failIfExists`. An LLM will assume "create" is non-destructive, but it is.
**Evidence:** `Tool_Script.Create.cs` line 121: `File.WriteAllText(path, sb.ToString());` — no existence check before the write.
**Confidence:** high

### A2 — `template` parameter values are case-sensitive but not documented as such
**Location:** `script-create` param `template`
**Issue:** The switch on line 76 uses `case "ScriptableObject":`, `case "EditorWindow":`, `case "Empty":`, with the default branch falling through to a `MonoBehaviour` template. So `"scriptableobject"`, `"editor-window"`, or `"monobehaviour"` will all silently produce a MonoBehaviour, with no error and a misleading success message ("template: scriptableobject"). The `[Description]` enumerates valid values but does not mention case sensitivity.
**Evidence:** `Tool_Script.Create.cs` line 31: `[Description("Template: 'MonoBehaviour', 'ScriptableObject', 'EditorWindow', 'Empty'. Default 'MonoBehaviour'.")]` — values look like an enum but the matcher is exact-case.
**Confidence:** high

### A3 — `script-apply-edits` description is dense but omits behavioural details
**Location:** `script-apply-edits` — `Tool_Script.ApplyEdits.cs`
**Issue:** The description packs all three op shapes into a single line, which is good, but does not state crucial behaviour: (a) `replace` only replaces the **first** occurrence, (b) `insert_after` matches the first line **containing** the anchor (substring match, not full-line match), (c) operations apply sequentially and earlier ops change line numbers seen by later ops, and (d) failed ops are SKIPPED, not aborted — the call returns success even if every op was skipped.
**Evidence:**
- `Tool_Script.ApplyEdits.cs` line 106: `int idx = current.IndexOf(op.Old, …);` (first occurrence).
- Line 128: `if (lines[li].Contains(op.Anchor))` (substring, not equality).
- Lines 102–164: every failure path appends `[SKIP …]` and `break`s; the file is still written and the response is `Text(...)` (not `Error(...)`).
**Confidence:** high

### A4 — `delete_line` semantics for sequential ops are unspecified
**Location:** `script-apply-edits` op `delete_line`
**Issue:** Line numbers are 1-based and computed against the *current* state of the line list at the time the op executes. So `[{"op":"delete_line","line":5},{"op":"delete_line","line":6}]` deletes lines 5 and 7 of the original file (because line 6 of the original becomes line 5 after the first delete). The description does not warn about this — an LLM batching multiple deletes will routinely get this wrong.
**Evidence:** `Tool_Script.ApplyEdits.cs` lines 151–158: `int zeroIdx = op.Line - 1; … lines.RemoveAt(zeroIdx);` operating on the mutated `lines` list.
**Confidence:** high

### A5 — `script-validate` description undersells what it returns
**Location:** `script-validate` — `Tool_Script.Validate.cs`
**Issue:** The description ("Checks if a script file exists and whether the project is compiling without errors.") is misleading on the second clause: the tool only checks `EditorApplication.isCompiling`, which means "compilation in progress right now". When `isCompiling` is `false` the tool says "project compiled OK" — but it does NOT inspect the console for compile errors. So a project with red errors that isn't actively recompiling will be reported as "compiled OK".
**Evidence:** `Tool_Script.Validate.cs` line 84: `sb.AppendLine(isCompiling ? "  Status: Compilation in progress — check console for errors." : "  Status: No compilation running — project compiled OK.");` — no error-list check.
**Confidence:** high

### A6 — `path` parameter rules are not documented
**Location:** all six tools, param `path`
**Issue:** `ValidateScriptPath` enforces that `path` must start with `Assets/` or `Packages/` and rejects path traversal. None of the parameter `[Description]` strings mention this constraint — they just say "File path (e.g. 'Assets/Scripts/Player.cs')." An LLM that tries `./Assets/...`, `\Assets\...`, or an absolute path gets a generic error.
**Evidence:** `Tool_Script.ApplyEdits.cs` lines 516–536 (`ValidateScriptPath`) reject anything not starting with `Assets/` or `Packages/`. Compare to e.g. `Tool_Script.Create.cs` line 30 which only says `[Description("File path (e.g. 'Assets/Scripts/Player.cs').")]`.
**Confidence:** high

### A7 — `script-apply-edits` XML doc and method docstring describe `path` differently than the validator enforces
**Location:** `script-apply-edits` param `path`
**Issue:** The XML doc on line 27 says "Project-relative or absolute path to the script file", but the validator on line 523 *rejects* anything not starting with `Assets/` or `Packages/`. The XML doc is inaccurate.
**Evidence:** `Tool_Script.ApplyEdits.cs` line 27 vs. line 523.
**Confidence:** high

### A8 — `editsJson` schema is documented in C# XML but not in the parameter description
**Location:** `script-apply-edits` param `editsJson`
**Issue:** The parameter `[Description]` is "JSON array of edit operation objects (replace, insert_after, delete_line)." The detailed schema (with field names and shapes) lives in the method-level `[Description]` and the XML `<param>` block. An LLM reading the parameter description in isolation does not see the field names (`old`, `new`, `anchor`, `text`, `line`).
**Evidence:** `Tool_Script.ApplyEdits.cs` line 45: parameter description omits field names.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `script-create.template` default is reasonable but the fallback for unknown values is a silent footgun
**Location:** `script-create` param `template`
**Issue:** Default is `"MonoBehaviour"`. That is a sensible default. The problem is the *fallback*: any unrecognised value (typo, wrong casing) silently produces a MonoBehaviour, so the LLM never learns it sent a bad value. This is a default-related defect even though the default itself is fine — the parameter behaves as if it has *two* defaults: the explicit one (`"MonoBehaviour"`) and an implicit one (any unknown value).
**Current:** `string template = "MonoBehaviour"` with `default:` branch on line 102 producing MonoBehaviour.
**Suggested direction:** Either reject unknown templates with an error listing valid values, or document the silent-fallback behaviour explicitly. Preferred: reject — it surfaces typos to the LLM.
**Confidence:** high

### D2 — `script-create.namespaceName` and `className` defaults documented inconsistently
**Location:** `script-create` params `namespaceName=""`, `className=""`
**Issue:** Empty-string defaults mean "no namespace" and "derive from filename" respectively. These are reasonable but rely on the LLM reading the description carefully — empty-string-as-sentinel is a pattern that often gets passed through as a literal empty class name by less-careful callers. Worth documenting that the empty-string sentinel is intentional.
**Current:** `string namespaceName = ""`, `string className = ""`.
**Suggested direction:** No code change urgent. Tighten descriptions to say "Pass empty string to skip / auto-derive."
**Confidence:** low

### D3 — `script-validate` is missing an option to include compile errors from the console
**Location:** `script-validate` param list (none beyond `path`)
**Issue:** Related to A5. The tool's "validation" stops at `isCompiling`. An optional `includeErrors` flag (defaulting to `true`) that pulls from the same internal `LogEntries` API as `console-get-logs` would close the gap. Today the LLM has to call `script-validate` AND `console-get-logs` to actually check whether a script compiled.
**Current:** `Validate(string path)` — no flag.
**Suggested direction:** Add an opt-in flag (or fold the behaviour into the default and report errors filtered to the script's path).
**Confidence:** medium — overlaps with capability gap G2 below.

---

## 5. Capability Gaps

### G1 — Rename a script (rename file + class name in one step)
**Workflow:** A Unity dev wants to rename `Assets/Scripts/Player.cs` (containing `class Player`) to `PlayerController.cs` (containing `class PlayerController`). This is one of the most common script refactor operations.
**Current coverage:** `Asset / Rename` (`Tool_Asset.Rename.cs`) renames the file. `script-apply-edits` could rewrite the class declaration. There is no single tool that does both atomically, and the class-rename path requires the LLM to know the exact text of `class Player` (including base-class clause) so it can craft a `replace` op.
**Missing:** No `script-rename` that combines file rename + class-name rewrite + (optionally) updating references elsewhere. There is also no AST-aware "rename symbol" tool — Unity itself doesn't provide one out of the box, but a partial solution using `MonoScript` and reflection is feasible.
**Evidence:** `Tool_Script.*` files contain no rename method. `Tool_Asset.Rename.cs` exists but operates on the `.cs` file as an opaque asset and does not touch its contents.
**Confidence:** high

### G2 — Read compile errors for a specific script
**Workflow:** After `script-update` or `script-apply-edits`, the LLM wants to confirm "did this script compile cleanly?" The natural answer is "show me errors associated with this file path."
**Current coverage:** `script-validate` reports `EditorApplication.isCompiling` but not the actual error list. `console-get-logs` (different domain) returns the console buffer but is not filtered to a particular script. `recompile-scripts` triggers a build but doesn't return errors. So the LLM must (a) call `recompile-scripts`, (b) wait, (c) call `console-get-logs`, (d) filter results client-side.
**Missing:** A tool like `script-get-errors(path)` that returns compile errors whose source path matches `path`. The data is available — Unity's internal `LogEntries` API exposes file/line metadata, as already used by `console-get-logs` (`Editor/Tools/Console/Tool_Console.GetLogs.cs`).
**Evidence:** `Tool_Script.Validate.cs` lines 30–87 — no error enumeration. `Tool_Console.GetLogs.cs` lines 22–23 reference the internal `LogEntries` type, which exposes per-entry file/line.
**Confidence:** high

### G3 — Find references to a class / method
**Workflow:** "Where is `class PlayerController` used in the project?" — needed before deletion or rename.
**Current coverage:** None in the Script domain. There is no Unity API for AST-level reference search, but a textual `grep`-style tool over `Assets/**/*.cs` would close the basic case.
**Missing:** No `script-find-references` or `script-grep` tool. The closest workaround is `Tool_Asset.Find` (asset-database query) which doesn't search file contents.
**Evidence:** Glob over `Editor/Tools/Script/` returns only the 6 files inventoried; no search/grep tool present. No matches for `script-find` or `script-grep` anywhere under `Editor/Tools/`.
**Confidence:** high

### G4 — Read part of a script (range/region)
**Workflow:** For a 2000-line file, the LLM wants to see only lines 50–120 around an error, not the whole file (which may exceed the model's effective working window or simply waste tokens).
**Current coverage:** `script-read` returns the full file. `ValidateFileSize` caps at 10 MB but does not page.
**Missing:** No `startLine`/`endLine` or `region`/`pattern` params on `script-read`. For large generated scripts (e.g. shader stub files, long ScriptableObject definitions) this forces full reads.
**Evidence:** `Tool_Script.Read.cs` line 24: signature is `Read(string path)` — single param, no slicing.
**Confidence:** high

### G5 — Manage Assembly Definition (`.asmdef`) files
**Workflow:** Create or edit `.asmdef` files to organise scripts into separate assemblies — a routine task for any non-trivial Unity project.
**Current coverage:** `script-create` only handles `.cs` templates. `script-update` can write any text content, including JSON, but the LLM has to know the asmdef schema by heart and pass it via `content`.
**Missing:** No `script-create-asmdef` or `assembly-create` tool, and no template option in `script-create`. Grep for `asmdef`, `assembly-definition`, `AssemblyDefinition` across `Editor/Tools/` returns zero matches.
**Evidence:** Grep `asmdef|assembly-definition|AssemblyDefinition` over `Editor/Tools/` → "No files found".
**Confidence:** high

### G6 — Validate edits before applying
**Workflow:** The LLM wants a dry-run: "if I apply these edits, will any of them be skipped?" — particularly useful when an `apply-edits` call has 10+ ops and one missed anchor silently leaves the file in a half-edited state.
**Current coverage:** None. `script-apply-edits` writes the result regardless of whether every op succeeded; partial application is the default.
**Missing:** A `dryRun` flag on `script-apply-edits`, or a separate `script-preview-edits` that runs the same parser and reports per-op success/failure without writing.
**Evidence:** `Tool_Script.ApplyEdits.cs` lines 92–169: every op variant has `[SKIP …]` paths but execution continues; `File.WriteAllText` and `AssetDatabase.ImportAsset` are unconditional after the loop.
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G2 | Capability Gap | 5 | 2 | 20 | high | Add per-script error retrieval; today the LLM cannot tell whether its own edits compiled. |
| 2 | A5 | Ambiguity | 5 | 1 | 25 | high | `script-validate` claims "compiled OK" without checking errors — actively misleading. Tighten description AND/OR fix behaviour. |
| 3 | A3 | Ambiguity | 4 | 1 | 20 | high | `script-apply-edits` description omits "first occurrence", "substring anchor", "skip-on-fail" — root cause of subtle wrong-edit bugs. |
| 4 | A4 | Ambiguity | 4 | 1 | 20 | high | `delete_line` numbering shifts between ops; not documented; common cause of wrong deletions. |
| 5 | A1 | Ambiguity | 4 | 1 | 20 | high | `script-create` silently overwrites — surprising for an LLM, and risky for users who run plans repeatedly. |
| 6 | D1 | Defaults | 4 | 2 | 16 | high | `script-create.template` silently falls back to MonoBehaviour for any unknown value; reject instead. |
| 7 | G6 | Capability Gap | 4 | 2 | 16 | high | Add dry-run / preview to `script-apply-edits` so partial-application failures surface before the file is mutated. |
| 8 | G3 | Capability Gap | 4 | 3 | 12 | high | No way to find references to a class/method; required for safe rename or delete. |
| 9 | G1 | Capability Gap | 3 | 3 | 9 | high | No combined script + class rename; LLM has to orchestrate file + content edit and may miss base-class declarations. |
| 10 | A6 | Ambiguity | 3 | 1 | 15 | high | `path` constraints (Assets/Packages prefix) not documented in any `[Description]`. |
| 11 | G4 | Capability Gap | 3 | 2 | 12 | high | No range read on `script-read`; large files force full-file reads. |
| 12 | R1 | Redundancy | 3 | 1 | 15 | medium | Add "use this when…" disambiguation between `script-update` and `script-apply-edits`. |

---

## 7. Notes

- **Cross-domain dependencies noticed:**
  - `script-validate` and `recompile-status`/`recompile-scripts` (in `Editor/Tools/RecompileScripts/`) all touch `EditorApplication.isCompiling`. Worth a follow-up multi-domain audit to decide whether `script-validate` should delegate to `recompile-status` or be merged.
  - `console-get-logs` (in `Editor/Tools/Console/`) already wraps the internal `LogEntries` API and could be the implementation target for the proposed `script-get-errors` (G2).
  - `Tool_Asset.Rename`/`Move`/`Delete` are the natural collaborators for any future `script-rename` (G1).
- **Hand-written JSON parser:** `ApplyEdits.cs` ships its own JSON parser (`ExtractJsonObjects`, `ExtractJsonStringField`). This is fragile for nested objects, escaped quotes inside strings, and multi-line `text` fields that contain `{` or `}` characters. Not flagged as a finding because the audit scope is API/UX, not implementation, but the planner should be aware that any change to the edit ops schema (e.g. to support arrays inside ops) will need a real JSON dependency.
- **Domain-level XML doc summary** lives on `Tool_Script.ApplyEdits.cs` (line 13–17). That file also hosts the shared private helpers — a slightly unusual layout (most domains put helpers in a `*.Helpers.cs` partial). Not a defect, but a planner doing rename or split work should know.
- **What I did not audit:**
  - Behavioural correctness of the JSON parser under edge cases (escaped quotes, unicode escapes, nested arrays).
  - Whether `AssetDatabase.ImportAsset` vs. `AssetDatabase.Refresh` is the right post-write call for each tool — Update/ApplyEdits use the former, Create uses the latter; a minor inconsistency that may or may not matter for `.cs` files.
  - Permissions / sandboxing: `ValidateScriptPath` rejects path traversal but I did not exhaustively test edge cases (e.g. symlinks in Packages folders).
- **Open question for the reviewer:** for G2 (per-script errors), should the implementation live in the Script domain (`script-get-errors`) or in the Console domain (`console-get-logs` with a `pathFilter` param)? Both are defensible; the consolidation-planner should pick.
