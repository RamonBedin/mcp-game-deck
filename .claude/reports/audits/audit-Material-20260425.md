# Audit Report — Material

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Material/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 4 (via `Glob` on `Editor/Tools/Material/Tool_Material.*.cs`)
- `files_read`: 4
- `files_analyzed`: 4

**Balance:** ✅ balanced

**Errors encountered during audit:** None.

**Files not analyzed (if any):** None.

**Absence claims in this report:**
- Permitted because accounting is balanced. Cross-domain searches were performed via `Grep` over `Editor/Tools/` for `SetTexture|SetVector|SetFloat|SetInt|SetColor`, `EnableKeyword|DisableKeyword|shaderKeywords`, `SkinnedMeshRenderer|SpriteRenderer|LineRenderer|TrailRenderer`, and `renderQueue|enableInstancing|globalIlluminationFlags|doubleSidedGI`. Results cited inline in Section 5 findings.

**Reviewer guidance:**
- The Material domain is small (4 tools). The most consequential issues are capability gaps (texture & keyword support), not redundancy. Treat Section 5 as the priority section.
- `Tool_Material.Update.cs` has a parser-style approach that, while clever, is fragile and silently lossy for texture/vector properties — flagged in both Section 3 and Section 5.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `material-create` | Material / Create | `Tool_Material.Create.cs` | 3 (`name`, `shaderName=""`, `savePath="Assets"`) | no |
| `material-assign` | Material / Assign | `Tool_Material.Assign.cs` | 4 (`instanceId=0`, `objectPath=""`, `materialPath=""`, `materialIndex=0`) | no |
| `material-update` | Material / Update | `Tool_Material.Update.cs` | 3 (`assetPath=""`, `materialName=""`, `propertiesJson=""`) | no |
| `material-get-info` | Material / Get Info | `Tool_Material.GetInfo.cs` | 2 (`assetPath=""`, `materialName=""`) | yes |

**Unity APIs touched:**
- `material-create`: `Shader.Find`, `GraphicsSettings.currentRenderPipeline`, `AssetDatabase.CreateAsset`, `AssetDatabase.GenerateUniqueAssetPath`, `AssetDatabase.SaveAssets`, `System.IO.Directory.CreateDirectory`.
- `material-assign`: `Tool_Transform.FindGameObject`, `AssetDatabase.LoadAssetAtPath<Material>`, `Renderer.sharedMaterials`, `Undo.RecordObject`.
- `material-update`: `AssetDatabase.LoadAssetAtPath<Material>`, `AssetDatabase.FindAssets("t:Material …")`, `Material.SetColor/SetFloat/SetInt`, `Undo.RecordObject`, `EditorUtility.SetDirty`, `AssetDatabase.SaveAssets`.
- `material-get-info`: `AssetDatabase.LoadAssetAtPath<Material>`, `Shader.GetPropertyCount/Name/Type/Description/Flags`, `Material.GetFloat/GetColor/GetVector/GetTexture/GetInt`, `Material.renderQueue/enableInstancing/doubleSidedGI`.

---

## 2. Redundancy Clusters

### Cluster R1 — `material-update` vs `material-get-info` lookup logic
**Members:** `material-update`, `material-get-info`
**Overlap:** Both tools accept a `(assetPath, materialName)` pair and re-implement the same disambiguation logic (lines 37–76 of `GetInfo.cs` and lines 43–84 of `Update.cs`). The two blocks are near line-for-line duplicates, including identical error strings and the `t:Material {materialName}` query. This is internal duplication, not LLM-facing ambiguity, but it doubles the surface for drift.
**Impact:** Low for the LLM; medium for maintainers — any change to lookup semantics (e.g. supporting GUID, supporting subassets, fuzzy match) needs to be made twice.
**Confidence:** high

> No LLM-facing redundancy clusters were identified — the four tools have distinct verbs (create / assign / update / get-info) and the LLM is unlikely to confuse them.

---

## 3. Ambiguity Findings

### A1 — `material-update` does not enumerate supported property types
**Location:** `material-update` — `Tool_Material.Update.cs` lines 28–33
**Issue:** Description says "Supports float, int, and Color (#RRGGBB) properties" — but the LLM has no way to know that **Texture, Vector, and Range** are silently unsupported. A request like `_BaseMap=Assets/Textures/wood.png` will be parsed, fail `float.TryParse`, and produce a `[SKIP]` line. The user sees "updated" but the texture is missing.
**Evidence:** Method `[Description]`: *"Updates shader property values on an existing Material asset. Supports float, int, and Color (#RRGGBB) properties via semicolon-separated key=value pairs."* — does not state what's NOT supported, nor that unsupported entries become silent skips.
**Confidence:** high

### A2 — `material-update` integer detection is by string punctuation, not by shader property type
**Location:** `material-update` — `Tool_Material.Update.cs` lines 150–162
**Issue:** The tool decides `SetFloat` vs `SetInt` based on whether the **input string contains a `.`**, not on the shader's actual property type. A user passing `_Metallic=1` (a float property in URP/Lit) will end up calling `SetInt("_Metallic", 1)`, which Unity may treat differently from `SetFloat("_Metallic", 1f)`. The description does not warn about this, so the LLM cannot know to add `.0` to numeric values.
**Evidence:** `if (propValue.Contains(".")) { material.SetFloat(...) } else { material.SetInt(...) }` — no consultation of `shader.GetPropertyType(...)`.
**Confidence:** high

### A3 — `material-create` shader auto-detect is undocumented from the LLM's perspective
**Location:** `material-create` — `Tool_Material.Create.cs` lines 32–33
**Issue:** Param description says "Leave empty for auto-detect" but doesn't list the candidate set (URP/Lit → HDRP/Lit → Standard). An LLM asked "create a 2D unlit material" will leave `shaderName` empty and silently get URP/Lit, which is not unlit. The behavior is correct as a fallback chain but opaque to the caller.
**Evidence:** `[Description("Shader name (e.g. 'Standard', 'Universal Render Pipeline/Lit'). Leave empty for auto-detect.")]` — no mention of the fallback ladder.
**Confidence:** medium

### A4 — `material-update` parser format is not actually JSON
**Location:** `material-update` — param `propertiesJson`
**Issue:** The parameter is named `propertiesJson` but the format is a semicolon-separated `key=value` string, not JSON. An LLM trained on standard schemas may emit `{"_Metallic": 0.5}` and have it silently rejected (no `=` separator → `[SKIP]`).
**Evidence:** Param name `propertiesJson` contradicts param description: *"Semicolon-separated property overrides. Format: 'PropName=Value'."* The XML doc on line 24 also calls it "JSON-like" — it is not.
**Confidence:** high

### A5 — `material-assign` does not state what error occurs for `MeshRenderer` vs `SkinnedMeshRenderer` vs `SpriteRenderer`
**Location:** `material-assign` — description
**Issue:** The tool calls `TryGetComponent<Renderer>` (the abstract base), so it works for any concrete renderer. The description says "Renderer component" without clarifying that this includes `SpriteRenderer`, `SkinnedMeshRenderer`, `LineRenderer`, etc. — useful info for the LLM when reasoning about a 2D vs 3D scene. Not a critical issue, but borderline jargon-without-example.
**Evidence:** `[Description("Assigns a Material asset to a GameObject's Renderer at the specified material index.")]`
**Confidence:** low

### A6 — `material-get-info` description begins with shader name
**Location:** `material-get-info` — description
**Issue:** Minor — the description does a good job of listing returned property types `(Float, Color, Texture, Vector, Int)`, but does NOT mention that for each property the **current value** is also returned (the body of `GetInfo` reads and prints values for Float/Range/Color/Vector/Texture/Int). The LLM may invoke `material-get-info` followed by extra calls thinking it needs to fetch values separately.
**Evidence:** `[Description("Returns the shader name, render queue, and full property list of a Material asset. Properties include their shader name and type (Float, Color, Texture, Vector, Int).")]` — no mention of values.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `material-assign` declares `materialPath` with default `""` but it is required
**Location:** `material-assign` param `materialPath`
**Issue:** Signature `string materialPath = ""` makes it look optional, but the body returns `Error("materialPath is required.")` if empty. The default exists only to keep all params optional positionally. Either make it required (`string materialPath`) or remove the validation; the current state misleads tooling that introspects the signature.
**Current:** `[Description("Asset path of the material (e.g. 'Assets/Materials/Red.mat').")] string materialPath = ""`
**Suggested direction:** Drop the default; it has no useful semantics.
**Confidence:** high

### D2 — `material-update` `propertiesJson` has the same issue
**Location:** `material-update` param `propertiesJson`
**Issue:** Same pattern: declared `string propertiesJson = ""` but body errors out when empty. The XOR dance with `assetPath` / `materialName` already requires at least one to be set; `propertiesJson` is unconditionally required.
**Current:** `string propertiesJson = ""`
**Suggested direction:** Make this a required parameter (no default).
**Confidence:** high

### D3 — `material-create` defaults `savePath` to `"Assets"` (project root)
**Location:** `material-create` param `savePath`
**Issue:** Saving materials directly under `Assets/` litters the project root and is rarely the correct default for production. Unity convention is `Assets/Materials/` or a per-asset folder. Not a bug — but for an LLM it's a sub-optimal default that will produce messy projects unless the LLM knows to override.
**Current:** `[Description("Folder path to save the material (e.g. 'Assets/Materials'). Default 'Assets'.")] string savePath = "Assets"`
**Suggested direction:** Either change the default to `"Assets/Materials"` (matching the example in the description) or document that the caller should specify a folder for any non-trivial project. The author's intent is unclear from the file.
**Confidence:** medium

### D4 — `material-assign` `instanceId=0` + `objectPath=""` allows both-empty calls
**Location:** `material-assign` params `instanceId`, `objectPath`
**Issue:** With both at default values, `Tool_Transform.FindGameObject(0, "")` will return null and the tool reports "GameObject not found." rather than "you must provide one of instanceId or objectPath." Same issue exists in other domains using this helper, so not unique to Material — but the user-facing error is misleading here.
**Current:** Both default to empty/zero; helper handles missing inputs by returning null.
**Suggested direction:** Validate explicitly and return a more actionable error when both are empty. (No code prescribed — that's planner territory.)
**Confidence:** medium

---

## 5. Capability Gaps

### G1 — Cannot set texture properties on materials
**Workflow:** Apply a texture (albedo / normal / mask map / sprite) to a material. Standard Unity workflow: load Texture asset, call `material.SetTexture("_BaseMap", tex)`, save.
**Current coverage:** `material-create` creates a material with default-textured shader. `material-update` exists and looks like the right tool.
**Missing:** `material-update` cannot accept a texture asset path. Its parser handles `#RRGGBB` (color), numeric (float/int), and rejects everything else as `[SKIP] '… is not a valid number or #RRGGBB color'`. There is no syntax for `_BaseMap=Assets/Textures/wood.png`. `Material.SetTexture(string, Texture)` is never invoked anywhere in the domain.
**Evidence:** `Tool_Material.Update.cs` lines 119–168: parser branches are `#` → color, `TryParse` → float/int, else → skip. Cross-domain `Grep` for `SetTexture` (excluding `SetTextureOffset`) within `Editor/Tools/Material/` returns zero matches; only `Tool_Material.GetInfo.cs` line 134 reads textures via `GetTexture`.
**Confidence:** high

### G2 — Cannot set Vector4 properties (`_Tiling`, custom shader vectors, sprite-uv vectors)
**Workflow:** Adjust a Vector4 shader property, e.g. tiling/offset combined into `_BaseMap_ST`, or a custom mask vector on a stylized shader.
**Current coverage:** `material-update` is the natural tool.
**Missing:** No parser branch for vectors. A request like `_BaseMap_ST=(1,1,0,0)` or `_Tint=1,0.5,0.5,1` will hit the `[SKIP]` path. `Material.SetVector` is not called anywhere in the domain (`material-get-info` reads vectors but writes are absent).
**Evidence:** `Tool_Material.Update.cs` lines 150–167 — only `SetFloat`/`SetInt` after the color branch; no vector parsing. `Grep` for `SetVector` in `Editor/Tools/Material/` returns zero matches.
**Confidence:** high

### G3 — No way to toggle shader keywords / variant features
**Workflow:** Enable URP/Lit's `_NORMALMAP`, `_METALLICSPECGLOSSMAP`, `_EMISSION`, or `_ALPHATEST_ON` keyword to activate a variant. This is required when applying a normal map (the texture set is a no-op until the keyword is enabled).
**Current coverage:** None.
**Missing:** No `material-set-keyword` / `material-enable-keyword` tool, and `material-update` has no syntax for keywords. `Material.EnableKeyword`, `Material.DisableKeyword`, and `material.shaderKeywords` are not referenced anywhere in `Editor/Tools/` (Grep across the entire tools directory returned zero files).
**Evidence:** Grep `EnableKeyword|DisableKeyword|shaderKeywords` over `Editor/Tools` → "No files found".
**Confidence:** high

### G4 — Cannot change non-property material settings (renderQueue, enableInstancing, doubleSidedGI, globalIlluminationFlags)
**Workflow:** Override a material's render queue (e.g. force `Transparent+1`), turn on GPU instancing, or enable double-sided GI on a foliage material.
**Current coverage:** `material-get-info` *reads* `renderQueue`, `enableInstancing`, `doubleSidedGI` (lines 83–85). `material-update` does NOT write any of them; its parser only writes shader properties.
**Missing:** Asymmetric coverage: visible in get-info, not writable. The asymmetry is itself a capability gap because the LLM will see the values in get-info output and reasonably assume update can set them.
**Evidence:** Grep `renderQueue|enableInstancing|globalIlluminationFlags|doubleSidedGI` in `Editor/Tools/` returned only `Tool_Material.GetInfo.cs` (read) and `Tool_Shader.Inspect.cs` (read); no write usages anywhere.
**Confidence:** high

### G5 — No tool to change a material's shader after creation
**Workflow:** Convert an existing material from `Standard` to `Universal Render Pipeline/Lit` (common when migrating a project to URP), or swap to a custom shader.
**Current coverage:** `material-create` lets you choose the shader at creation time only.
**Missing:** No `material.shader = newShader` operation. Once created, a material's shader is locked from this tool set's perspective. The user would have to delete and recreate, losing all property overrides.
**Evidence:** Grep `material.shader\s*=` in `Editor/Tools/Material/` returns zero matches; no setter on `material.shader` anywhere in the domain.
**Confidence:** high

### G6 — No way to copy / duplicate a material
**Workflow:** Use a base material as a template, duplicate it, then tweak properties — common when authoring color variants of one base.
**Current coverage:** `material-create` always starts from a fresh shader-default material.
**Missing:** No `material-duplicate` (would map to `Object.Instantiate(material)` + `AssetDatabase.CreateAsset`). The LLM would have to call get-info → create → update repeatedly for each property, which is fragile because update can't even handle textures or keywords.
**Evidence:** No matching tool in inventory; no `Object.Instantiate.*Material` calls in the domain.
**Confidence:** high

### G7 — No reverse lookup / bulk operations on which GameObjects use a material
**Workflow:** "Find every renderer in the scene using `Materials/OldRed.mat` and replace with `Materials/NewRed.mat`."
**Current coverage:** `material-assign` works for one renderer at a time, given a known GameObject.
**Missing:** No "list all references", no batch replace. The LLM has no way to enumerate users of a material short of walking the scene via Hierarchy tools and checking each renderer manually. Note: this is borderline cross-domain; partial coverage may exist via Asset domain. Did NOT exhaustively check Asset tools — flagging as a workflow gap rather than a definite missing API.
**Evidence:** No tool inventory entry mentions reverse lookup; `material-assign` requires `instanceId` or `objectPath`.
**Confidence:** medium (partial cross-domain coverage check)

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 2 | 20 | high | Add texture support to `material-update` (or new tool); silent skip today |
| 2 | G3 | Capability Gap | 5 | 2 | 20 | high | Shader keyword enable/disable absent — blocks normal maps & variants |
| 3 | A2 | Ambiguity | 4 | 1 | 20 | high | float/int dispatch by string punctuation, not by shader property type |
| 4 | G2 | Capability Gap | 4 | 2 | 16 | high | Cannot write Vector4 properties (`_BaseMap_ST`, custom vectors) |
| 5 | A1 | Ambiguity | 4 | 1 | 20 | high | `material-update` description doesn't disclose unsupported types → silent SKIP |
| 6 | A4 | Ambiguity | 3 | 1 | 15 | high | Param `propertiesJson` is misnamed — format is not JSON |
| 7 | G4 | Capability Gap | 3 | 2 | 12 | high | renderQueue / instancing / GI flags readable but not writable (asymmetry) |
| 8 | G5 | Capability Gap | 3 | 2 | 12 | high | Cannot swap a material's shader after creation |
| 9 | G6 | Capability Gap | 3 | 2 | 12 | high | No material duplicate / template-based copy |
| 10 | D1+D2 | Defaults | 2 | 1 | 10 | high | Required params declared with `""` default — misleads schema introspection |
| 11 | A3 | Ambiguity | 2 | 1 | 10 | medium | Shader auto-detect fallback ladder undocumented |
| 12 | R1 | Redundancy | 2 | 2 | 8 | high | Lookup logic duplicated between `material-update` and `material-get-info` |
| 13 | D3 | Defaults | 2 | 1 | 10 | medium | `savePath` default `Assets` litters project root |
| 14 | A6 | Ambiguity | 2 | 1 | 10 | medium | `get-info` description omits that values are returned |
| 15 | G7 | Capability Gap | 2 | 3 | 6 | medium | No reverse lookup / batch replace of material references |
| 16 | A5 | Ambiguity | 1 | 1 | 5 | low | `assign` description doesn't enumerate Renderer subclasses |
| 17 | D4 | Defaults | 2 | 1 | 10 | medium | both-empty `instanceId`+`objectPath` produces misleading error |

(Priority = Impact × (6 − Effort).)

---

## 7. Notes

- **The Material domain is small but capability-thin.** The four tools cover a happy-path "create + assign + tweak floats/colors + inspect" flow, but anything involving textures, keywords, vectors, or shader swaps falls off a cliff. For a Unity project that uses URP (the assumed Tier-1 target), texture and keyword support are table stakes.
- **`material-update`'s parser-string approach is the structural root cause of G1, G2, A1, A2, A4.** Any consolidation plan that addresses these gaps should consider whether this tool needs a richer input shape (e.g. typed property arrays) rather than further patching the string parser. This is planner territory — flagging it as the through-line that ties multiple findings together.
- **Cross-domain dependency:** `material-assign` calls `Tool_Transform.FindGameObject(instanceId, objectPath)`. If the GameObject domain ever changes that helper's contract, this tool breaks silently. Worth a regression note when refactoring GameObject/Transform.
- **Adjacent domain (Shader):** The `Shader` domain has `shader-list` and `shader-inspect` (read-only). It complements `material-create`'s shader argument — the LLM can discover shader names there. No overlap, no missing handoff.
- **Open question for the reviewer:**
  1. Should `material-update` evolve into typed property batches (cleaner, more verbose) or grow more parser cases (faster patch, technical debt)? — answer drives the consolidation plan shape.
  2. Is `material-duplicate` a separate tool or an option on `material-create` (e.g. `templatePath` param)?
  3. Should `material-update` switch from "string-input → property-type-guess" to "consult `shader.GetPropertyType(name)` then parse accordingly"? This would fix A2 and unlock cleaner texture/vector support in one move.
- **Workflows intentionally not deeply explored:** material variants, ShaderGraph property metadata, keyword-driven feature flags. Flagged as medium-confidence gaps (G3, G7) where appropriate; full coverage would need a follow-up pass.
