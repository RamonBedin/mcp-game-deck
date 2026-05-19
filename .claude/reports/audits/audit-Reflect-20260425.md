# Audit Report — Reflect

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Reflect/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via Glob `Editor/Tools/Reflect/Tool_Reflect.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- All 5 files were read end-to-end before any "no X" claim was made. Cross-domain checks (Component / Script) were performed via Grep to confirm there is no duplicate reflection surface elsewhere in `Editor/Tools/`.

**Reviewer guidance:**
- The Reflect domain is small (5 tools) but acts as a meta-API surface: it exposes `System.Reflection` to the LLM. Findings here have unusually high *upstream* impact — every other domain benefits when an LLM can correctly introspect a Unity type before scripting against it. So even a 1-point description tweak can be high-priority.
- Most findings concern ambiguity / disambiguation across the 4 inspection tools (`reflect-search`, `reflect-get-type`, `reflect-find-method`, `reflect-get-member`), which form a partial overlap chain. Ramon should pay particular attention to G1 (read/write of property and field values), which is the most consequential capability gap.
- One file (`GetMember.cs`) holds the canonical `[McpToolType]` summary for the partial class. The rest correctly omit it. No XML doc duplication found.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `reflect-search` | Reflect / Search Types | `Tool_Reflect.Search.cs` | 3 (`query`, `scope="unity"`, `maxResults=30`) | no (effectively read-only, hint missing) |
| `reflect-get-type` | Reflect / Get Type | `Tool_Reflect.GetType.cs` | 1 (`className`) | no (effectively read-only, hint missing) |
| `reflect-find-method` | Reflect / Find Method | `Tool_Reflect.FindMethod.cs` | 3 (`className`, `methodName=""`, `scope="all"`) | yes |
| `reflect-get-member` | Reflect / Get Member | `Tool_Reflect.GetMember.cs` | 2 (`className`, `memberName`) | no (effectively read-only, hint missing) |
| `reflect-call-method` | Reflect / Call Method | `Tool_Reflect.CallMethod.cs` | 4 (`typeName`, `methodName`, `instanceId=0`, `argsJson=""`) | no (correctly: this one mutates) |

Internal Unity API surface used:
- `AppDomain.CurrentDomain.GetAssemblies()` and `Assembly.GetTypes()` (Search, GetType — via `FindType`)
- `Type.GetMethods` / `Type.GetMember` / `Type.GetProperties` / `Type.GetFields` / `Type.GetConstructors` / `Type.GetInterfaces`
- `EditorUtility.EntityIdToObject(int)` (CallMethod) — correct API per project conventions
- `MethodInfo.Invoke` (CallMethod)
- `MainThreadDispatcher.Execute` wrapper used uniformly

Static block lists (CallMethod): `_blockedTypes` (4 entries) and `_blockedMethods` (10 entries) — see lines 26-47.

---

## 2. Redundancy Clusters

### Cluster R1 — Type-discovery / type-inspection trio
**Members:** `reflect-search`, `reflect-get-type`, `reflect-get-member`, `reflect-find-method`
**Overlap:** All four are read-only inspection of C# types, but they fan out by granularity:
- `reflect-search` → "find a type by partial name" (returns a list)
- `reflect-get-type` → "show everything about this type" (returns full summary)
- `reflect-find-method` → "show methods on this type matching a name filter"
- `reflect-get-member` → "show one specific member in detail (with overloads, defaults, generics)"

The seams are real and defensible — each output format genuinely differs — but the *invocation intent* overlaps heavily. An LLM asked "what's the signature of `Physics.Raycast`?" could plausibly call `reflect-get-type` (to see Raycast in the Methods list), `reflect-find-method` (filtered by `Raycast`), or `reflect-get-member`. Only `reflect-get-member` returns the per-overload parameter detail, but nothing in the tool descriptions tells the LLM that.

Note: `reflect-get-type` lists methods *but only with simple type names* (line 136: `parmSb.Append($"{methodParams[pi].ParameterType.Name} {methodParams[pi].Name}")`), which is identical formatting to `reflect-find-method`. So `reflect-find-method` is essentially `reflect-get-type` filtered + scope-able. This is the strongest overlap.

**Impact:** Medium. The LLM will frequently pick the wrong tool first, then retry. Wastes calls but isn't fatal.
**Confidence:** high

### Cluster R2 — Search vs. Get Type for "does this exist?"
**Members:** `reflect-search`, `reflect-get-type`
**Overlap:** Both tools answer "does X exist as a Unity type?" but in different shapes. `reflect-search` does a *substring* match across many assemblies; `reflect-get-type` does an *exact-or-case-insensitive-simple-name* match. Either can confirm existence, and the LLM is likely to flip a coin. The descriptions don't establish "use search when you don't know the exact name; use get-type when you do."

**Impact:** Medium-low. Both work; difference is efficiency only.
**Confidence:** high

---

## 3. Ambiguity Findings

### A1 — `reflect-find-method` description doesn't differentiate from `reflect-get-member`
**Location:** `reflect-find-method` — `Tool_Reflect.FindMethod.cs` line 31
**Issue:** Description says "Searches a C# type for methods matching an optional name filter. Returns full signatures: 'returnType MethodName(paramType paramName, ...)'." But `reflect-get-member` *also* returns method signatures (with even more detail including defaults and generic args). No "use this when X, not Y" disambiguation between the two.
**Evidence:** `[Description("Searches a C# type for methods matching an optional name filter. Returns full signatures: 'returnType MethodName(paramType paramName, ...)'. scope: 'all' (default), 'instance', or 'static'.")]`
**Confidence:** high

### A2 — `reflect-get-member` description doesn't say "for one specific named member, including all overloads"
**Location:** `reflect-get-member` — `Tool_Reflect.GetMember.cs` line 29
**Issue:** Description correctly says it returns parameter types, return type, attributes, overloads — but does not name the killer feature that distinguishes it from `reflect-find-method`: it lists *every overload* with full per-parameter type, default values, generic parameters, and `ref`/`out` modifiers. Without this hint, the LLM will keep reaching for `reflect-find-method`.
**Evidence:** `[Description("Gets detailed signature information for a specific member (method, property, field, or event) of a C# type. Returns full parameter types, return type, attributes, and overloads.")]`
**Confidence:** high

### A3 — `reflect-get-type` doesn't mention truncation behavior or ordering
**Location:** `reflect-get-type` — `Tool_Reflect.GetType.cs` line 24
**Issue:** This tool dumps every constructor, property, method, and field of a type. For a type like `UnityEngine.Object` or `UnityEditor.EditorGUI`, the output can be very large. Description doesn't warn the LLM, and the implementation has no `maxItems` cap. The LLM may dump 5KB of methods just to find one it cared about.
**Evidence:** Implementation has no truncation: `for (int i = 0; i < methods.Count; i++)` (line 119) — the loop is unbounded. Description: `"Gets a summary of a C# type from loaded assemblies via reflection. Returns base type, interfaces, public methods, properties, fields, and events. Use this to verify a class/struct exists before writing code."` — silent on size.
**Confidence:** high

### A4 — `reflect-call-method` block list is invisible from the description
**Location:** `reflect-call-method` — `Tool_Reflect.CallMethod.cs` line 75
**Issue:** Description mentions "Safety: only types from UnityEngine/UnityEditor assemblies are allowed" but does not enumerate or even hint at the more specific block lists at lines 26-47 (`_blockedTypes` and `_blockedMethods`). An LLM trying to call `EditorApplication.ExecuteMenuItem` or `AssetDatabase.DeleteAsset` will get a runtime error with no upfront warning. There is no read-only "describe what's blocked" tool, and no documentation surface for the policy.
**Evidence:** `_blockedMethods` (lines 34-47) blocks 10 specific methods including `EditorApplication.ExecuteMenuItem`, `AssetDatabase.DeleteAsset`, `EditorPrefs.SetString`, etc. None are mentioned in the `[Description]`.
**Confidence:** high

### A5 — `reflect-call-method` `argsJson` parser is hand-rolled and silently lossy
**Location:** `reflect-call-method` — `Tool_Reflect.CallMethod.cs` lines 253-330
**Issue:** Description says `"JSON array of arguments (e.g. '[42, true, \"hello\"]'). Leave empty or pass '[]' for parameterless methods."` — implying full JSON support. The actual parser (`ParseArgsJson` at line 253) is a custom tokenizer that splits on commas/whitespace. It does NOT support: nested arrays, nested objects, unicode escapes (the `\uXXXX` form is lost — line 296-300 only honors single-char escapes), `null` as a JSON literal (it's only recognized as the bare token `null`, line 353), or numbers in scientific notation. An LLM trusting the description and passing `[{"x":1,"y":2}]` will get a confusing argument-coercion failure rather than a parse error pointing at the unsupported feature.
**Evidence:** Method body at line 253 onward is a custom stream parser, not a JSON library. It has no schema-aware error reporting and the `\\` escape on line 296 only accepts the next single character literally (so `\"` works but `\u0041` does not).
**Confidence:** high

### A6 — `reflect-call-method` `instanceId=0` semantic isn't enforced for static-only callers
**Location:** `reflect-call-method` — line 79 param `instanceId`
**Issue:** Description says "Use 0 for static methods." Implementation correctly errors when an instance method gets `instanceId=0`. But the converse (passing `instanceId=42` for a static method) silently ignores the instance ID — the method runs as static, ignoring the supplied object entirely. The LLM may believe its target was respected when it wasn't.
**Evidence:** Lines 134-169 only check `if (!method.IsStatic)`. The static branch never validates that `instanceId` was 0.
**Confidence:** medium

### A7 — `reflect-search` `scope` accepts undocumented fallback for unknown values
**Location:** `reflect-search` — line 29 param `scope`
**Issue:** Description enumerates `'unity'`, `'packages'`, `'project'`, `'all'` and says "Default 'unity'". But `MatchesScope` (line 131) silently maps any unrecognized value back to `unity` behavior (line 141 `_ => ...`). If the LLM passes `'editor'` or `'core'` (typos / synonyms), it gets unity-only results without an error. Description doesn't document this. (Same pattern in `reflect-find-method` `scope`, line 59.)
**Evidence:** `_ => name.StartsWith("UnityEngine"...) || name.StartsWith("UnityEditor"...)` at line 141 of Search.cs.
**Confidence:** medium

### A8 — `reflect-find-method` returns `DeclaredOnly` methods but description doesn't say so
**Location:** `reflect-find-method` — line 52
**Issue:** Implementation uses `BindingFlags.Public | BindingFlags.DeclaredOnly` (line 52). This excludes inherited methods. So `reflect-find-method("Camera", "GetComponent")` would return zero results because `GetComponent` is on `Component`, not `Camera`. Description does not mention this.
**Evidence:** Line 52: `BindingFlags flags = BindingFlags.Public | BindingFlags.DeclaredOnly;` — no description annotation.
**Confidence:** high

### A9 — `reflect-search` only returns public types, undocumented
**Location:** `reflect-search` — line 90
**Issue:** Implementation skips `!type.IsPublic` (line 90). Description says "Searches for C# types across loaded assemblies by name pattern" with no public-only qualifier. Internal Unity types (e.g. `UnityEditor.EditorAssembliesPostProcessor`) won't be findable but the LLM has no way to know why.
**Evidence:** Line 90: `if (!type.IsPublic) { continue; }` with no description annotation.
**Confidence:** high

### A10 — Inconsistent param naming across the family (`className` vs `typeName`)
**Location:** All 5 tools
**Issue:** `reflect-call-method` uses `typeName`; `reflect-get-type`, `reflect-get-member`, `reflect-find-method` all use `className`; `reflect-search` uses `query`. The first two refer to the same concept but with different names. An LLM that learned the parameter shape on one tool will guess wrong on the other.
**Evidence:** Compare CallMethod.cs line 77 (`string typeName`) vs GetType.cs line 26, GetMember.cs line 31, FindMethod.cs line 33 (all `string className`).
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `reflect-call-method.argsJson` default is `""` not `"[]"`
**Location:** `reflect-call-method` param `argsJson`
**Issue:** Default is empty string `""` (line 80). The parser does treat `""` as "no args" (line 255), so functionally fine, but the description says "Leave empty or pass '[]'" — making `""` the implicit no-args sentinel. A canonical JSON empty array would be a more honest default and would match the documented type ("JSON array of arguments").
**Current:** `string argsJson = ""`
**Suggested direction:** `string argsJson = "[]"` — keeps semantics, matches the documented schema.
**Confidence:** medium

### D2 — `reflect-find-method.scope` default `"all"` may not match real usage
**Location:** `reflect-find-method` param `scope`
**Issue:** Default is `"all"` (instance + static), which is fine. But for the most common LLM workflow — "I have an instance, what method should I call?" — the default returns inherited-irrelevant statics mixed in. Combined with finding A8 (`DeclaredOnly`), this default produces weird output: inherited methods are hidden, but static class-level methods are mixed in.
**Current:** `string scope = "all"`
**Suggested direction:** Worth Ramon's review — does the LLM use this tool more for "method discovery on an instance" or "any method on a type"? If the former, `"instance"` may be a better default. Lower priority than fixing A8.
**Confidence:** low

### D3 — `reflect-search.maxResults=30` is a magic value, not documented as truncation behavior
**Location:** `reflect-search` param `maxResults`
**Issue:** When the search returns >30 hits, the tool silently truncates with no "..." or "more results available" indicator (search loop breaks at line 51-53 / 85-87, and the final summary line 111 says "X found" using the truncated count). The LLM cannot tell whether "20 found" means "exactly 20 exist" or "20 of many, capped".
**Current:** `int maxResults = 30`
**Suggested direction:** Default value is fine. The fix is at the implementation/description level: surface a "truncated" flag in the response when the cap was hit. (Not a code-suggestion — just a direction.)
**Confidence:** high

### D4 — `reflect-get-type` has no `maxItems` cap
**Location:** `reflect-get-type` (no param to control output volume)
**Issue:** No truncation control exists at all. See A3 for the symptom. Adding an optional `maxMethods` / `maxFields` would let the LLM bound the output for large types.
**Current:** Single param `className` only.
**Suggested direction:** Consider an optional `maxItemsPerSection = 50` or similar. Not blocking.
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Read or write a property/field VALUE on an existing instance
**Workflow:** Given an instance (e.g. a Rigidbody on a GameObject), a Unity dev expects to ask "what's its current `velocity`?" and "set its `mass` to 5.0" using reflection — without writing a custom script. This is the bread-and-butter introspection workflow.
**Current coverage:**
- `reflect-get-member` describes *that* a property/field exists and its type/access.
- `reflect-call-method` only invokes methods. It does NOT call property getters/setters; line 122 explicitly skips `IsSpecialName` (which excludes `get_X`/`set_X` accessor methods). It also cannot read fields.
- Nothing else in the domain reads or writes values.
- Component-domain tools (`Tool_Component.Get.cs`, `Tool_Component.List.cs`) exist but are scoped to Unity component property paths, not arbitrary reflection.
**Missing:** A `reflect-get-value` and/or `reflect-set-value` (or a unified `reflect-property` action) that wraps `PropertyInfo.GetValue` / `SetValue` and `FieldInfo.GetValue` / `SetValue`. An optional `instanceId=0` (matching CallMethod conventions) would cover both static and instance reads.
**Evidence:** `Tool_Reflect.CallMethod.cs` line 122: `if (candidates[i].Name == methodName && !candidates[i].IsSpecialName)` — the `!IsSpecialName` filter eliminates property accessors. Grep across the entire Reflect domain for `GetValue` / `SetValue`: 0 matches (only `PropertyInfo` and `FieldInfo` *types* appear, in GetMember, for description purposes).
**Confidence:** high (all 5 files read; cross-domain Grep for `GetValue`/`SetValue` confirmed zero hits within Reflect)

### G2 — Inspect inherited members of a type
**Workflow:** "What public methods does `Camera` actually expose?" — most of them come from `Behaviour`, `Component`, `Object`. A Unity dev expects the answer to include those.
**Current coverage:**
- `reflect-get-type` uses `BindingFlags.DeclaredOnly` (line 87 for properties, line 104 for methods, line 142 for fields). So it returns only Camera's *own* declarations.
- `reflect-find-method` uses `BindingFlags.DeclaredOnly` (line 52). Same problem.
- `reflect-get-member` does NOT use `DeclaredOnly` (line 54) — so it works correctly. But the LLM has to know the member name first.
**Missing:** No tool can list inherited members. The LLM would have to walk the type chain manually via repeated `reflect-get-type` calls climbing the `BaseType`. There is no flag to opt into inherited members.
**Evidence:** `Tool_Reflect.GetType.cs` lines 87, 104, 142 — all use `BindingFlags.DeclaredOnly`. `Tool_Reflect.FindMethod.cs` line 52 — uses `BindingFlags.DeclaredOnly`. `Tool_Reflect.GetMember.cs` line 54 — does NOT (uses `Public | NonPublic | Instance | Static`).
**Confidence:** high

### G3 — Discover/inspect attributes on types and members beyond methods
**Workflow:** Unity dev wants to find every type marked `[CustomEditor(typeof(Camera))]`, or check whether a property has `[SerializeField]` / `[HideInInspector]`. Attribute inspection is critical for editor scripting.
**Current coverage:**
- `reflect-get-member` lists method-level custom attributes (lines 98-111). It does NOT list attributes on properties, fields, or events (no equivalent `GetCustomAttributes` block in those switch arms — see lines 115-149).
- `reflect-search` cannot filter by attribute.
- `reflect-get-type` does not list type-level attributes at all.
**Missing:** (a) Type-level attribute listing in `reflect-get-type`. (b) Attribute listing for property/field/event branches in `reflect-get-member`. (c) An attribute-search capability (e.g. "list all types with attribute `CustomEditor`").
**Evidence:** `Tool_Reflect.GetMember.cs` — lines 98-111 list attributes only inside `case MethodInfo method:`. No corresponding block for `case PropertyInfo prop:` (line 115), `case FieldInfo field:` (line 133), or `case EventInfo evt:` (line 145).
**Confidence:** high

### G4 — Inspect generic-type construction (closed generics)
**Workflow:** "I have `List<int>` — what's its element type?" or "show me the methods of `Dictionary<string,GameObject>`". An LLM wiring a Unity API call against a generic collection would expect this.
**Current coverage:**
- `FindType` (line 183-221) calls `Type.GetType` and `assembly.GetType` directly. Neither resolves the typical user input `"List<int>"` — they require backtick form (`"System.Collections.Generic.List`1[System.Int32]"`). No tool translates the friendly form.
- `reflect-get-type` displays generic argument names *if* a type happens to be generic (the `FormatTypeName` helper in GetMember.cs handles display, but GetType.cs uses raw `.Name` at line 98 — so `Properties` show e.g. `IList\`1`).
**Missing:** A way to resolve and inspect a closed generic type by friendly name. Currently the LLM cannot discover `List<int>.Count` via this tool family.
**Evidence:** `Tool_Reflect.GetType.cs` line 98: `sb.AppendLine($"  {isStatic}{p.PropertyType.Name} {p.Name} {{ {access} }}");` — uses raw `.Name`, not `FormatTypeName`. `FindType` at line 183 has no string-rewriting logic for `T<U>` → backtick form.
**Confidence:** medium (the workflow is real, but lower priority than G1-G3; the user can usually navigate via the open type and infer)

### G5 — Discover constructors via search/find
**Workflow:** "How do I construct a `MaterialPropertyBlock` / `Mesh` / `BoneWeight`?" — the LLM wants to see the constructor list for a value type before scripting against it.
**Current coverage:**
- `reflect-get-type` does include constructors (lines 73-85). Good.
- `reflect-find-method` does NOT consider constructors (it uses `Type.GetMethods`, not `GetConstructors`).
- `reflect-get-member` can be queried with `memberName=".ctor"` and `Type.GetMember` will return them — but the description doesn't mention this and an LLM is unlikely to discover the trick.
**Missing:** A way to ask "show me constructor X" specifically, or to filter constructors. Low priority because `reflect-get-type` covers the basic need.
**Evidence:** `Tool_Reflect.FindMethod.cs` line 62: `MethodInfo[] methods = type.GetMethods(flags);` — `GetMethods` does not include constructors.
**Confidence:** medium

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | No tool reads or writes property/field VALUES on an instance — only signatures. |
| 2 | G2 | Capability Gap | 4 | 1 | 20 | high | `reflect-get-type` and `reflect-find-method` use `DeclaredOnly`, so inherited methods are invisible; flip the flag or expose a param. |
| 3 | A4 | Ambiguity | 4 | 1 | 20 | high | `reflect-call-method` description never enumerates the 14 blocked types/methods; LLM hits runtime errors blindly. |
| 4 | A8 | Ambiguity | 4 | 1 | 20 | high | `reflect-find-method` silently uses `DeclaredOnly`; an LLM searching `Camera.GetComponent` gets nothing. |
| 5 | A1 + A2 + R1 | Ambiguity / Redundancy | 4 | 2 | 16 | high | The 4 inspection tools overlap; descriptions don't distinguish "use `find-method` for filtered list, `get-member` for one specific overload set." |
| 6 | A5 | Ambiguity | 3 | 2 | 12 | high | `argsJson` parser is hand-rolled and lossy; description claims general JSON support that doesn't exist. |
| 7 | A3 + D4 | Ambiguity / Default | 3 | 2 | 12 | high | `reflect-get-type` has no truncation; large types dump huge output. |
| 8 | G3 | Capability Gap | 4 | 3 | 12 | high | Attribute inspection is method-only; no attribute on properties/fields/types and no attribute search. |
| 9 | A10 | Ambiguity | 3 | 1 | 15 | high | Inconsistent param naming (`className` vs `typeName`) within the same domain. |
| 10 | D3 | Default | 3 | 2 | 12 | high | `reflect-search` truncates at `maxResults` silently; no "...more available" signal. |
| 11 | A9 | Ambiguity | 2 | 1 | 10 | high | `reflect-search` quietly excludes non-public types. |
| 12 | A7 | Ambiguity | 2 | 1 | 10 | medium | Unknown `scope` values silently fall back to `unity` instead of erroring. |
| 13 | A6 | Ambiguity | 2 | 1 | 10 | medium | `reflect-call-method` ignores `instanceId` for static methods without warning. |
| 14 | G4 | Capability Gap | 3 | 4 | 6 | medium | Cannot inspect closed generic types (`List<int>`) via friendly name. |
| 15 | G5 | Capability Gap | 2 | 3 | 6 | medium | Constructors aren't discoverable via `find-method`. |
| 16 | D1 | Default | 1 | 1 | 5 | medium | `argsJson` default is `""`, should be `"[]"` to match documented schema. |

Priority formula: Impact × (6 − Effort).

---

## 7. Notes

**Cross-domain observations**
- The Reflect family is the *only* reflection surface in `Editor/Tools/`. There is no overlap with the Component or Script domains. So redundancy is purely intra-domain.
- The Component domain (`Tool_Component.Get.cs`, `Tool_Component.List.cs`) handles Unity component property paths but does not generalise to arbitrary `Type` inspection. They are complementary, not competing.

**Security model is undocumented but reasonable**
- The block lists in `Tool_Reflect.CallMethod.cs` (lines 26-47) are a sensible defense-in-depth layer over the assembly-prefix gate (lines 19-24). The structure is good — but neither the lists nor their rationale is exposed to the LLM. Consider either (a) a read-only tool that returns the policy, or (b) including a one-line summary in the `[Description]` ("Some destructive APIs like `AssetDatabase.DeleteAsset` and `EditorPrefs.Set*` are blocked.").

**Code quality is high overall**
- All 5 files conform to project conventions: `MainThreadDispatcher.Execute`, brace-everywhere style, no empty catches, `EntityIdToObject` (not deprecated `InstanceIDToObject`), nullable annotations enabled, `CultureInfo.InvariantCulture` on numeric parses (CallMethod.cs lines 370-382). The `[McpToolType]` summary lives only on `GetMember.cs` — no XML doc duplication. No language-feature violations spotted.
- The hand-rolled JSON parser in CallMethod.cs is the only smell — consider replacing with a small dependency or `System.Text.Json`. Treat as an incremental improvement, not a blocker.

**Open questions for the reviewer**
- Should value read/write (G1) be a NEW tool (`reflect-get-value`, `reflect-set-value`), or fold into `reflect-call-method` via an `action` param (`action: "call" | "get-property" | "set-property" | "get-field" | "set-field"`)? The latter matches the consolidation pattern in `Tool_Animation.ConfigureController.cs`, but `reflect-call-method` is the only mutating tool in the domain — adding read actions to it muddies the read/write split.
- Is "list inherited members" a flag on existing tools (e.g. `includeInherited=false`) or its own tool? A flag is simpler and is what most reflection libraries do.
- For G3 (attribute inspection), is there appetite for a `reflect-find-by-attribute` tool? It opens powerful editor-scripting workflows ("show me every `[CustomEditor]`") but adds another tool to a family that already has 5.

**Limits of this audit**
- All findings derive from static reading of the 5 source files. Runtime behavior (e.g. exact output size on a large type, JSON parser corner cases) was inferred from the code, not measured. A follow-up validation run that calls `reflect-get-type("UnityEditor.EditorGUI")` and `reflect-call-method` with a tricky `argsJson` payload would confirm the truncation and parser findings A3/A5 empirically.
