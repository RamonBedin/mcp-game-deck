# Audit Report — Type

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Type/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 1 (via Glob over `Editor/Tools/Type/Tool_Type.*.cs`)
- `files_read`: 1
- `files_analyzed`: 1

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None. The Type domain contains exactly one tool file (`Tool_Type.GetSchema.cs`) plus its `.meta` companion. Full coverage achieved.

**Absence claims in this report:**
- Permitted (accounting balanced). Where I make absence claims that span beyond the Type domain (e.g. "no other tool produces a JSON-schema output"), I cite the cross-domain Grep that backs them and note the scope.

**Reviewer guidance:**
- Type is a **single-tool domain**. Internal redundancy is impossible by definition — the redundancy/consolidation lens has to be cross-domain.
- The single tool (`type-get-json-schema`) overlaps heavily with `reflect-get-type` and `reflect-get-member` in the Reflect domain. This is the most important finding to weigh — the question is whether Type should exist as a separate domain at all, or whether its tool should fold into Reflect.
- I cross-referenced the Reflect domain (`Tool_Reflect.GetType.cs`, `Tool_Reflect.GetMember.cs`, `Tool_Reflect.Search.cs`) and `Tool_Component.Get.cs` to assess overlap, but those domains are **not in scope for findings** — I only describe Type-domain issues. Cross-domain restructuring decisions belong to the consolidation-planner.
- The system prompt at `Server~/prompts/core-system-prompt.md` lists `type-get-json-schema` under **"Meta"** (line 82), separate from the **"Reflection"** group (line 80) that owns `reflect-get-type`. The LLM is therefore told these are different categories of tools, which masks the overlap.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `type-get-json-schema` | Type / Get JSON Schema | `Tool_Type.GetSchema.cs` | 1 (`typeName: string`, required) | ✅ yes |

**Internal Unity / .NET API surface used:**
- `System.Type.GetType(string, throwOnError, ignoreCase)` — primary type resolution
- `System.AppDomain.CurrentDomain.GetAssemblies()` — fallback enumeration across all loaded assemblies
- `System.Reflection.Assembly.GetTypes()` — with `ReflectionTypeLoadException` swallow
- `Type.GetFields(BindingFlags.Public | BindingFlags.Instance)` — instance-only, public-only
- `Type.GetProperties(BindingFlags.Public | BindingFlags.Instance)` — instance-only, public-only
- `PropertyInfo.GetIndexParameters()` — used to skip indexers
- `MemberInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: true)` — pulls human-readable descriptions

**Output format:** plaintext block resembling JSON (`"FieldName": "type"  // description`, joined with commas) but not actually valid JSON — closing brace appended with no trailing-comma awareness on the last entry, and the inline `// comments` make it non-parseable.

---

## 2. Redundancy Clusters

### Cluster R1 — Type reflection vs. Reflect domain
**Members:** `type-get-json-schema` (Type domain), `reflect-get-type` (Reflect domain), `reflect-get-member` (Reflect domain)
**Overlap:** All three accept a class name and reflect over loaded assemblies to describe a C# type. `reflect-get-type` returns a richer summary (base type, interfaces, constructors, public methods, properties, fields, enum values, with `BindingFlags.Static` included). `type-get-json-schema` returns a strictly narrower view (public instance fields and properties only, no constructors/methods/interfaces/base type), wrapped in a JSON-shaped string. The two type-resolution helpers (`Tool_Type.ResolveType` and `Tool_Reflect.FindType`) are nearly line-for-line equivalent — both try `Type.GetType` first, then iterate `AppDomain.CurrentDomain.GetAssemblies()`, both swallow `ReflectionTypeLoadException`, both do case-insensitive simple-name fallback. The only meaningful behavioural difference is the output format and the binding flags chosen.
**Impact:** When an LLM asks "what fields does `Rigidbody` have?", both `type-get-json-schema` and `reflect-get-type` are valid answers. The system prompt categorises them in two different groups ("Meta" vs "Reflection"), which would lead the model to choose based on group label rather than capability. The JSON-ish output format of `type-get-json-schema` is also not valid JSON, so its claimed differentiator (machine-parseable schema) doesn't actually deliver.
**Confidence:** high — verified by reading all three files in full and confirming the helper-function duplication.

---

## 3. Ambiguity Findings

### A1 — No disambiguation against `reflect-get-type`
**Location:** `type-get-json-schema` — `Tool_Type.GetSchema.cs` line 30
**Issue:** The method-level `[Description]` advertises "JSON-like schema of its public fields and properties, including declared types and description annotations. Searches all loaded assemblies." It does not mention when to prefer this tool over `reflect-get-type`, nor that `reflect-get-type` returns a strictly richer view. An LLM facing both tools has no signal to pick one over the other.
**Evidence:** Verbatim description on line 30: `"Resolves a C# type by name and returns a JSON-like schema of its public fields and properties, including declared types and description annotations. Searches all loaded assemblies."`
**Confidence:** high

### A2 — "JSON-like" is misleading
**Location:** `type-get-json-schema` — `Tool_Type.GetSchema.cs` lines 30, 50–101
**Issue:** The description and tool ID both promise a JSON output, but the produced text is not parseable JSON. It contains `//` line comments after each entry (lines 67, 91), brackets like `[get;set;]` in property entries (line 92–93), and emits a comma after the last field but before properties begin without recovering the trailing-comma state across the two loops. Calling code that tries `JSON.parse` on the output will fail. The description should either say "human-readable schema" / "JSON-flavored summary" or the format should be repaired to be valid JSON. Today the name oversells.
**Evidence:** Output format line 67: `sb.Append($"  \"{field.Name}\": \"{MapTypeName(field.FieldType)}\"{descStr}");` where `descStr` is `$"  // {desc}"` — `//` is illegal in JSON. Line 92: `[get;set;]` access marker is also illegal in a JSON value.
**Confidence:** high

### A3 — Behavior on unknown types depends on `ignoreCase: true` quirk
**Location:** `type-get-json-schema` — `Tool_Type.GetSchema.cs` line 118
**Issue:** `Type.GetType(typeName, throwOnError: false, ignoreCase: true)` is called with `ignoreCase: true`, which means a query for `"rigidbody"` (lowercase) will resolve to `UnityEngine.Rigidbody`. This behavior is not documented in the parameter description, which only gives the example `"UnityEngine.Rigidbody" or "Rigidbody"` (both capitalized). Callers may be surprised by silent case-folding (e.g. `monobehaviour` resolves to `MonoBehaviour`). Compare to `reflect-get-type` (`Tool_Reflect.GetType.cs` line 185) which calls `Type.GetType(className)` with default casing — semantics differ between the two near-duplicate tools.
**Evidence:** Line 118: `Type? t = Type.GetType(typeName, throwOnError: false, ignoreCase: true);`
**Confidence:** medium — the case-insensitive fallback is consistently applied, but the divergence from `reflect-get-type` (case-sensitive in the equivalent first probe) creates inconsistent behavior across the two tools.

### A4 — Excludes static members and methods/events without saying so
**Location:** `type-get-json-schema` — `Tool_Type.GetSchema.cs` lines 54, 71
**Issue:** Both `GetFields` and `GetProperties` are called with `BindingFlags.Public | BindingFlags.Instance` only — no `Static`, no methods, no events. The description does not mention this — it says "public fields and properties". A user reading the description might reasonably expect static members (e.g. `Vector3.zero`, `Time.deltaTime`) to appear; they will not. By contrast, `reflect-get-type` includes `BindingFlags.Static` (line 87, 104, 142). The mismatch is not signalled to the LLM.
**Evidence:** Lines 54 and 71 explicitly omit `BindingFlags.Static`. Description on line 30 says only "public fields and properties" with no scope qualifier.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `typeName` is required but no error-recovery guidance
**Location:** `type-get-json-schema` param `typeName`
**Issue:** Required string with no default (correct — there is no sensible default for a type name). However, the error path on type-not-found (line 46) returns `"Type '{typeName}' could not be found in any loaded assembly."` without suggesting the user run `reflect-search` to find a matching type. `reflect-get-type` (line 40) provides exactly this hint: `"Try the search action to find the correct name."` The Type domain tool gives the LLM no recovery path.
**Current:** `string typeName` (required) — no default, no fallback hint on miss.
**Suggested direction:** Add a "use `reflect-search` to look up by partial name" hint to the not-found error message, OR (preferred) bring the tools together so this redundancy disappears. No code suggested — that's the planner's job.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — No way to inspect static members through the Type domain
**Workflow:** "I'm scripting against `Time` / `Mathf` / `Vector3` and need to know which static fields/properties exist."
**Current coverage:** `type-get-json-schema` reflects only `BindingFlags.Public | BindingFlags.Instance` (lines 54, 71). It returns nothing for static-only types like `UnityEngine.Mathf` or static-heavy types like `Time`, `Application`.
**Missing:** static-member inclusion. `reflect-get-type` in the Reflect domain *does* cover this with `BindingFlags.Static` (lines 87, 104, 142) — so the capability exists in the codebase, just not in the Type domain. From the perspective of a user who has been steered to the Type domain by the system prompt's "Meta" grouping, the static-member workflow is unreachable.
**Evidence:** `Tool_Type.GetSchema.cs` line 54: `FieldInfo[] fields = resolved.GetFields(BindingFlags.Public | BindingFlags.Instance);` and line 71: `PropertyInfo[] props = resolved.GetProperties(BindingFlags.Public | BindingFlags.Instance);` — no `BindingFlags.Static`.
**Confidence:** high (single file, fully read; the absence is local to the Type domain — `reflect-get-type` covers this gap from a different domain).

### G2 — No way to inspect methods, constructors, events, or interfaces from the Type domain
**Workflow:** "Show me the public method signatures of `Physics` so I can pick the right `Raycast` overload."
**Current coverage:** `type-get-json-schema` enumerates only fields and properties. Methods, constructors, events, base type, and interfaces are not surfaced. There is no second tool in the Type domain to fill the gap.
**Missing:** method/constructor/event/interface enumeration. Again, `reflect-get-type` and `reflect-get-member` in the Reflect domain cover this — but a user routed to "Meta › type-get-json-schema" via the system prompt has no path to those methods through the Type domain.
**Evidence:** `Tool_Type.GetSchema.cs` only iterates `GetFields` and `GetProperties` (lines 54, 71). No `GetMethods`, no `GetConstructors`, no `GetEvents`, no `BaseType`, no `GetInterfaces` calls anywhere in the file.
**Confidence:** high (full-file read).

### G3 — No way to inspect a single named member (overload-aware)
**Workflow:** "Tell me the signature of `Physics.Raycast` — there are several overloads, which one do I want?"
**Current coverage:** None within the Type domain. `type-get-json-schema` returns the *whole* type's field/property table, which doesn't include methods. To answer this question via the Type domain, the LLM has no available tool. The Reflect domain has `reflect-get-member` exactly for this purpose.
**Missing:** named-member lookup with overload listing. Inside the Type domain there is no equivalent to `reflect-get-member`.
**Evidence:** Domain contains exactly one method, `GetSchema`, with one parameter `typeName` and no member-name parameter. Confirmed by the Glob in Phase 0 returning a single file.
**Confidence:** high (full-domain coverage).

### G4 — Output is not actually JSON, so it cannot feed downstream JSON-consuming tools
**Workflow:** "Get the schema of `MyConfig`, then pipe it to a JSON-driven validation step / display it in a JSON editor / cache it as a `.json` artifact."
**Current coverage:** `type-get-json-schema` returns a string that *resembles* JSON but contains `// comments` and `[get;set;]` access markers, neither of which parse as JSON. There is no mode flag (e.g. `format: "json" | "text"`) to switch to a strict JSON output.
**Missing:** strict JSON emission. Today the tool's name promises a parseable artefact and delivers a human-readable summary that happens to use braces. A caller assuming `JSON.parse(response)` will succeed will be wrong.
**Evidence:** `Tool_Type.GetSchema.cs` lines 67, 91 inject `//` comments, line 92–93 inject `[get;set;]` markers — neither is legal JSON.
**Confidence:** high (full-file inspection of the entire output-building loop).

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | R1 | Redundancy (cross-domain) | 5 | 4 | 10 | high | `type-get-json-schema` overlaps with `reflect-get-type`; ResolveType/FindType helpers are near-duplicates. Whole domain may belong inside Reflect. |
| 2 | G2 | Capability Gap | 4 | 3 | 12 | high | Type domain cannot list methods, constructors, events, base type, or interfaces — only fields and properties. |
| 3 | G1 | Capability Gap | 4 | 1 | 20 | high | Type domain ignores `BindingFlags.Static`; static-only types (Mathf, Time) return empty. One-line fix in scope, but the redundancy with Reflect may make it moot. |
| 4 | A2 | Ambiguity | 4 | 2 | 16 | high | Output is named "JSON schema" but is not parseable JSON (`//` comments, `[get;set;]` markers). Mismatch between promise and delivery. |
| 5 | A1 | Ambiguity | 4 | 1 | 20 | high | No disambiguation in description against `reflect-get-type`; LLM has no signal for picking one over the other. |
| 6 | A4 | Ambiguity | 3 | 1 | 15 | high | Description says "public fields and properties" but silently excludes statics — diverges from sibling `reflect-get-type` behaviour. |
| 7 | G4 | Capability Gap | 3 | 2 | 12 | high | No strict-JSON output mode; tool's name implies machine-readability that the implementation does not deliver. |
| 8 | G3 | Capability Gap | 3 | 3 | 9 | high | No named-member lookup inside the Type domain. Workflow exists in Reflect domain only. |
| 9 | A3 | Ambiguity | 2 | 1 | 10 | medium | `Type.GetType(..., ignoreCase: true)` silently case-folds; behaviour diverges from `reflect-get-type`'s case-sensitive first probe. |
| 10 | D1 | Default | 2 | 1 | 10 | high | Not-found error gives no recovery hint (compare `reflect-get-type` which suggests `reflect-search`). |

(Priority = Impact × (6 − Effort). Larger is better.)

**The headline finding is R1 + G1/G2/G3 considered together:** the Type domain is essentially a smaller, narrower, and partly-broken subset of the Reflect domain. Whether the right move is to merge it into Reflect, repurpose it (e.g. as a "produce strict-JSON for downstream tooling" tool with a single clear job), or leave it alone but rewrite descriptions to disambiguate, is a planner-level decision. The audit's job is to surface that this domain is the most consolidation-eligible single-tool domain I'd expect to see — almost everything it does is already done elsewhere, and where it differs (instance-only, JSON-shaped) it differs in ways that aren't documented or aren't actually true.

---

## 7. Notes

**Cross-domain dependencies noticed:**
- The system prompt at `Server~/prompts/core-system-prompt.md` line 80 advertises `reflect-get-type, -get-member, -search, -call-method, -find-method` as **"Reflection"**, while line 82 lists `type-get-json-schema` under **"Meta"** alongside `tool-list-all` and `specialist-ping`. This grouping is misleading: the tool is not meta-tooling, it is reflection. If the tool stays, it should be regrouped — but ideally it shouldn't stay separate.
- `Tool_Type.ResolveType` (lines 116-156) and `Tool_Reflect.FindType` (`Tool_Reflect.GetType.cs` lines 183-221) are functionally near-duplicates with subtle behavioural differences (case sensitivity in the first probe, exception-handling style). Any consolidation effort should pick one canonical resolver and centralise it under `Editor/Tools/Helpers/` rather than maintaining two.

**Open questions for the reviewer (Ramon):**
- **Is `type-get-json-schema` meant to produce strict JSON?** If yes, the implementation needs repair. If no, the name and description need to stop promising it.
- **Should the Type domain exist?** If the tool's purpose is "give me a quick schema-shaped summary of a data class so I can populate it," it could plausibly live as a thin wrapper over `reflect-get-type` with a `format: "schema"` flag. If its purpose is genuinely different (e.g. only public-instance, only for serializable user data classes like `[Serializable]` POCOs and ScriptableObjects), the description needs to say so explicitly.
- **Static member inclusion:** trivial to fix in isolation (one extra `BindingFlags.Static` flag), but doing so makes the tool even more redundant with `reflect-get-type`. The decision about G1 is entangled with the decision about R1.

**Workflows intentionally deferred:**
- I did not audit the Reflect domain for its own findings — only used it as cross-reference context. A separate `tool-auditor Reflect` run is warranted before any consolidation plan that merges Type into Reflect.
- I did not assess the embedding of `[DescriptionAttribute]` reading (lines 239-249) for correctness against Unity's serialisation rules — this is fine and likely useful, just out of scope.

**Limits of this audit:**
- The tool was not invoked at runtime; all findings are based on static code reading. Behavioural assertions (e.g. "static members aren't returned") are inferred from `BindingFlags` arguments and are high-confidence but not runtime-verified.
- I did not attempt to determine *why* the Type domain exists alongside Reflect (no commit-history access; per project rules, no `git log`). Ramon's intent for the split would inform whether the right move is consolidation or clearer separation.
