# Audit Report — Shader

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Shader/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/Shader/Tool_Shader.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Permitted because accounting is balanced. Cross-domain absence claims (e.g. "no tool exposes ShaderGraph creation") were verified with Grep across `Editor/Tools/` for `ShaderGraph`, `ShaderGraphImporter`, `.shadergraph`, `ShaderUtil`, `ShaderVariant`, and `shader_feature` — all returned zero matches. The Shader-related code paths in other domains (`Material/Tool_Material.Create.cs`, `Asset/Tool_Asset.Create.cs`, `Graphics/Tool_Graphics.Stats.cs`) only consume shaders via `Shader.Find(...)`; none authors or modifies shader assets.

**Reviewer guidance:**
- The Shader domain is one of the smallest in the codebase — only 2 tools, both read-only inspection. The high-value findings here are about **capability gaps**, not redundancy or ambiguity. Consider whether write-side shader workflows (keyword toggling, variant collection, ShaderGraph authoring) are in scope for v1.x or deferred.
- Note: `Tool_Shader.Inspect.cs` does NOT set `ReadOnlyHint = true` despite being a pure inspection tool. `Tool_Shader.List.cs` also does not. This is a small but consistent miss.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `shader-inspect` | Shader / Inspect | `Tool_Shader.Inspect.cs` | 1 (`shaderName`) | no (missing hint) |
| `shader-list` | Shader / List | `Tool_Shader.List.cs` | 3 (`nameFilter`, `includeBuiltin`, `maxResults`) | no (missing hint) |

**Internal Unity API surface used:**
- `Shader.Find(name)`
- `AssetDatabase.LoadAssetAtPath<Shader>(path)`
- `AssetDatabase.FindAssets("t:Shader")`
- `AssetDatabase.GUIDToAssetPath(guid)`
- `Shader.GetPropertyCount/GetPropertyName/GetPropertyType/GetPropertyDescription`
- `Shader.passCount`, `Shader.FindPassTagValue`, `ShaderTagId`
- `Shader.keywordSpace.keywords`
- `Shader.renderQueue`, `Shader.isSupported`

**Notable:** Both tools are read-only by intent and behavior. Neither writes to disk or modifies project state.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The two tools serve clearly distinct purposes:
- `shader-list` discovers shaders (project-wide scan).
- `shader-inspect` reports details about a single shader.

There is **mild overlap risk** in that `shader-list` could be extended with a `verbose` flag that emits per-shader inspect details, collapsing both tools — but at current granularity, the split is reasonable. Confidence: high.

---

## 3. Ambiguity Findings

### A1 — `shader-inspect` lacks disambiguation vs. `material-get-info`
**Location:** `shader-inspect` — `Tool_Shader.Inspect.cs` line 29
**Issue:** Both `shader-inspect` and `material-get-info` (Material domain, `Tool_Material.GetInfo.cs` lines 27-100) iterate shader properties with `GetPropertyCount/GetPropertyName/GetPropertyType`. An LLM asked "what properties does this shader expose?" cannot tell from descriptions alone whether to call `shader-inspect` (against the shader asset) or `material-get-info` (against a material instance). Neither description warns about the other.
**Evidence:** `[Description("Inspects a shader and returns its properties, passes, keywords, render queue, and supported features.")]` — no "use this when you have a shader asset; use material-get-info when you have a material" clause.
**Confidence:** medium (cross-domain ambiguity, not Shader-internal).

### A2 — `shader-list` `nameFilter` semantics under-specified
**Location:** `shader-list` param `nameFilter` — `Tool_Shader.List.cs` line 27
**Issue:** Description says "case-insensitive partial match" but does not clarify whether the match is against the shader's display name (e.g. `Universal Render Pipeline/Lit`) or asset path. The implementation (line 59) matches against `shader.name` (display name), but the result line (line 64) shows both. A user passing `"Assets/Shaders/Custom"` (a path fragment) would get zero results despite the path being shown.
**Evidence:** `[Description("Filter by shader name pattern (case-insensitive partial match). Empty for all.")]`
**Confidence:** high.

### A3 — `shader-inspect` ambiguous on lookup precedence
**Location:** `shader-inspect` param `shaderName` — `Tool_Shader.Inspect.cs` line 31
**Issue:** Param accepts either a shader name OR an asset path, with `Shader.Find` tried first then `AssetDatabase.LoadAssetAtPath` as fallback. Description says "Shader name (e.g. 'Universal Render Pipeline/Lit') or asset path" but does not explain the precedence. If a project has a shader file at path `"Standard"` (unusual but possible), the user cannot disambiguate which lookup wins.
**Evidence:** Lines 41-46. Param description does not mention lookup order or what happens on collision.
**Confidence:** low (edge case, but easy to fix with one sentence).

### A4 — `includeBuiltin` description does not match implementation semantics
**Location:** `shader-list` param `includeBuiltin` — `Tool_Shader.List.cs` line 28
**Issue:** Description claims "If true, include built-in/package shaders." But the actual filter (line 47) is `path.StartsWith("Assets/")` — meaning any path NOT starting with `Assets/` is filtered out when `includeBuiltin` is false. This includes:
- Built-in Unity shaders (correct — desired behavior)
- Package shaders under `Packages/com.unity.render-pipelines.universal/...` (probably desired)
- **Local embedded packages, custom packages, and `Library/`-resolved shaders** (likely unintentional — these are project-authored shaders the user may want)
The description conflates "built-in" with "anything outside Assets/". A user with custom shaders in an embedded package will not find them with default settings.
**Evidence:** Lines 47-49 vs. description on line 28.
**Confidence:** high.

---

## 4. Default Value Issues

### D1 — `maxResults = 50` is silently truncating
**Location:** `shader-list` param `maxResults`
**Issue:** Default of 50 is reasonable, but when truncation occurs (line 40-43, `break`), the response (line 68) reports `Shaders ({count})` without indicating that more shaders exist beyond the limit. A caller seeing "Shaders (50)" cannot distinguish between "exactly 50 shaders match" and "at least 50 shaders match — call again with higher limit". URP projects routinely have 100+ shaders.
**Current:** `int maxResults = 50`
**Suggested direction:** Either (a) include a `truncated: true` indicator in output when limit was hit, or (b) raise default to a higher number more in line with typical project shader counts. The default itself is fine; the silent truncation is the real problem.
**Confidence:** high.

### D2 — `includeBuiltin = false` excludes too aggressively (default mismatch)
**Location:** `shader-list` param `includeBuiltin`
**Issue:** See A4 — the default of `false` excludes package and embedded-package shaders, not just built-in ones. For a URP project, the user typically wants URP/Lit, URP/Unlit, URP/SimpleLit etc. visible by default. With `includeBuiltin=false` (default), they are hidden. Most callers will need to flip the default to find any shader of interest.
**Current:** `bool includeBuiltin = false`
**Suggested direction:** Either rename the param to reflect what it actually does (e.g. `includeNonProjectShaders`), or change the filter to specifically exclude only built-in resources (`Resources/unity_builtin_extra` etc.) while keeping package shaders included by default.
**Confidence:** high.

### D3 — `shader-inspect` `shaderName` has no default and no fallback to selection
**Location:** `shader-inspect` param `shaderName`
**Issue:** Required param, no default. If the user has a Material currently selected in the Project view whose shader they want to inspect, they must first call another tool to discover the shader name, then pass it here. Many other tools in this codebase accept "leave empty to use Selection" — `shader-inspect` does not.
**Current:** `string shaderName` (required)
**Suggested direction:** Allow empty to fall back to `Selection.activeObject` if it's a Shader or a Material (extracting `material.shader.name`).
**Confidence:** medium (depends on Ramon's stance on Selection-fallback patterns).

---

## 5. Capability Gaps

### G1 — Cannot create or modify shader assets
**Workflow:** A common Unity workflow is to scaffold a new ShaderGraph, surface shader, or shader variant directly from a recipe (e.g. "create a URP unlit ShaderGraph at `Assets/Shaders/Toon.shadergraph`"). LLM agents authoring 2D/3D content often need to author shaders, not just consume them.
**Current coverage:** `shader-list` and `shader-inspect` are read-only. `Tool_Asset.Create.cs` (Asset domain) creates Materials by calling `Shader.Find("Standard")` but does not create shader assets. `Tool_Material.Create.cs` only creates Materials, not shaders.
**Missing:** No tool wraps `ShaderGraphImporter`, no tool writes `.shader` files, no tool clones an existing shader as a template. Grep across `Editor/Tools/` for `ShaderGraph`, `ShaderGraphImporter`, `.shadergraph` returned zero matches.
**Evidence:** `Tool_Shader.Inspect.cs` and `Tool_Shader.List.cs` are the only files in the domain; both perform read-only `Shader.Find` / `AssetDatabase.FindAssets` calls. Project-wide grep confirms no shader authoring API is wrapped anywhere.
**Confidence:** high (domain fully analyzed; cross-domain grep confirms absence).

### G2 — Cannot toggle global shader keywords
**Workflow:** Toggling global shader keywords (e.g. `_SHADOWS_SOFT`, custom feature flags) is standard for runtime/editor visual debugging or for forcing variant compilation. Unity exposes `Shader.EnableKeyword(string)`, `Shader.DisableKeyword(string)`, `Shader.IsKeywordEnabled(string)` and the modern `GlobalKeyword` API.
**Current coverage:** `shader-inspect` reports the keyword space (lines 80-91) but no tool sets/clears keywords.
**Missing:** No `shader-set-keyword` / `shader-toggle-keyword` tool. A user cannot script "enable _DEBUG_OVERLAY globally and screenshot the scene" via MCP.
**Evidence:** Grep across `Editor/Tools/` for `EnableKeyword|DisableKeyword|GlobalKeyword|globalShaderKeyword` returned only the read-side reference in `Tool_Shader.Inspect.cs` line 80 (`shader.keywordSpace.keywords`); zero call sites for the write-side APIs.
**Confidence:** high.

### G3 — Cannot inspect or warm shader variants / variant collections
**Workflow:** Build optimization workflows require inspecting `ShaderVariantCollection` assets, listing currently warmed variants, or pre-warming a collection from script.
**Current coverage:** None.
**Missing:** No tool wraps `ShaderVariantCollection`, `ShaderUtil.GetCurrentShaderVariantCollectionVariantCount`, or `ShaderUtil.SaveCurrentShaderVariantCollection`. No tool to list/clear variants.
**Evidence:** Grep for `ShaderVariant|ShaderUtil` returned zero matches across `Editor/Tools/`.
**Confidence:** high.

### G4 — Cannot reimport / recompile a single shader
**Workflow:** When a shader compiler error appears, the standard fix loop is "edit shader → reimport → check console". An LLM agent reading errors from `console-get-logs` cannot trigger a targeted reimport.
**Current coverage:** `Tool_RecompileScripts` exists for C# scripts but does not handle shaders. `AssetDatabase.ImportAsset(path)` would suffice but is not wrapped specifically for shaders.
**Missing:** No `shader-reimport` tool. The user must call a generic asset-reimport tool (if it exists) or `recompile-scripts` (which doesn't trigger shader recompile).
**Evidence:** No `ImportAsset` call referencing a shader path in the Shader domain. Two files in domain confirmed read-only.
**Confidence:** medium (a generic asset-reimport tool may exist in the Asset domain — not verified — but a domain-specific shader reimport is more discoverable).

### G5 — Cannot get the shader source / property defaults
**Workflow:** When debugging "why does my Material look wrong?", inspecting the shader's *default* property values (the values a freshly-instantiated material would have) is useful. `shader-inspect` lists property names and types but not defaults.
**Current coverage:** `shader-inspect` reports name, type, description per property (lines 65-69) — but skips `GetPropertyDefaultFloatValue`, `GetPropertyDefaultVectorValue`, `GetPropertyTextureDefaultName`, and the property flags / range limits.
**Missing:** Default values, range min/max for `Range` properties, texture default names, and `ShaderPropertyFlags` (HideInInspector, MainTexture, MainColor, NoScaleOffset, etc.) — all exposed by Unity's `Shader` API but not surfaced.
**Evidence:** `Tool_Shader.Inspect.cs` lines 63-69 show only `GetPropertyName/Type/Description`. `Tool_Material.GetInfo.cs` line 100 *does* read `GetPropertyFlags`, so the API is known to be in use elsewhere — just not in the Shader domain.
**Confidence:** high.

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | D2 | Default Value | 4 | 1 | 20 | high | `includeBuiltin=false` hides URP/HDRP package shaders by default — most callers will get no useful results on first try |
| 2 | A4 | Ambiguity | 4 | 1 | 20 | high | `includeBuiltin` description misrepresents what is actually filtered (anything outside `Assets/`) |
| 3 | G2 | Capability Gap | 4 | 2 | 16 | high | Cannot toggle global shader keywords — blocks visual debug & variant workflows |
| 4 | D1 | Default Value | 3 | 1 | 15 | high | Silent truncation at 50 results; output gives no signal that more exist |
| 5 | G5 | Capability Gap | 3 | 2 | 12 | high | `shader-inspect` omits default values, range limits, and property flags |
| 6 | G1 | Capability Gap | 4 | 4 | 8 | high | No shader authoring/creation tools (ShaderGraph, surface shaders, templates) |
| 7 | A2 | Ambiguity | 2 | 1 | 10 | high | `nameFilter` matches display name only, but result shows path — inconsistent mental model |
| 8 | A1 | Ambiguity | 2 | 1 | 10 | medium | `shader-inspect` vs `material-get-info` — no disambiguation in either description |
| 9 | G4 | Capability Gap | 3 | 2 | 12 | medium | No targeted shader reimport; requires generic asset reimport (if it even exists) |
| 10 | D3 | Default Value | 2 | 2 | 8 | medium | `shader-inspect` won't accept a Selection fallback — extra hop required |
| 11 | G3 | Capability Gap | 2 | 3 | 6 | high | No variant collection inspection / warming — niche but a real gap for build pipelines |
| 12 | A3 | Ambiguity | 1 | 1 | 5 | low | Lookup precedence (Shader.Find vs LoadAssetAtPath) not documented |

---

## 7. Notes

- **Domain size:** Shader is one of the smallest domains (2 tools). The findings here are dominated by capability gaps, not redundancy or ambiguity. If the consolidation pipeline targets reduction of tool count, this domain has nothing to consolidate; if it targets *capability completeness*, this domain has clear missing surface area.

- **ReadOnlyHint missing on both tools:** Neither `shader-inspect` nor `shader-list` declares `ReadOnlyHint = true` despite being pure inspection. This is a one-line fix per file but matters for clients that filter tools by safety class. Treated as a small consistency issue rather than a separate finding.

- **Cross-domain note:** The Material domain (`Tool_Material.GetInfo.cs`) replicates ~30 lines of property iteration logic that `shader-inspect` also implements (with slightly different surface). If the codebase has shared helpers for property iteration, both tools should converge on it. If not, the duplication is small enough to leave alone for now.

- **`Tool_Asset.Create.cs` line 66** hardcodes `Shader.Find("Standard")` without any URP/HDRP detection — Material domain's `Tool_Material.Create.cs` lines 56-78 has proper RP detection logic. Worth surfacing to the Asset domain audit, but out of scope here.

- **Open question for the reviewer:** Should v1.x scope include shader *write* tools (G1, G2, G4) or are those v2.0 territory? Current Shader domain feels like a "minimum viable inspection layer" — that's a defensible position. The audit flags the gaps so the planner can decide.
