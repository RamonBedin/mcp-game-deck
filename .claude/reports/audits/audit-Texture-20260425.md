# Audit Report — Texture

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Texture/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 5 (via Glob `Editor/Tools/Texture/Tool_Texture.*.cs`)
- `files_read`: 5
- `files_analyzed`: 5

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None. All 5 files in domain were read and analyzed.

**Cross-domain reads (for context only, not audited):**
- `Editor/Tools/Asset/Tool_Asset.ImportSettings.cs` — checked for overlap with `texture-configure`.
- `Editor/Tools/Asset/Tool_Asset.GetInfo.cs` — checked for overlap with `texture-inspect`.
- `Editor/Tools/Sprite/` — confirmed via Glob that NO `Sprite` domain exists (zero files). Sprite-related responsibilities therefore land on the Texture domain.

**Absence claims in this report:**
- All five files were successfully analyzed; absence claims are permitted under Rule 3.
- A `Grep` for `spriteImportMode|spritesheet|SpriteMetaData|spritePixelsPerUnit` over `Editor/Tools` returned only `Tool_Texture.Inspect.cs` (read-only display) — no tool *writes* these. This supports gap G2.
- A `Grep` for `filterMode|wrapMode|anisoLevel` in `Editor/Tools/Texture` shows these are read in Inspect.cs (lines 53–55) but never written by any Texture tool. This supports gap G3.

**Reviewer guidance:**
- The domain is small (5 tools) and self-consistent in style. Most findings are surface-level (descriptions, defaults, missing `ReadOnlyHint`). The substantial findings are capability gaps in sprite metadata and runtime sampler settings (filter/wrap/aniso).
- `texture-configure` deliberately overlaps with the generic `asset-set-import-settings` — that is a design choice (texture-typed convenience wrapper). The redundancy entry below is informational, not a recommendation to delete.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `texture-apply-gradient` | Texture / Apply Gradient | `Tool_Texture.ApplyGradient.cs` | 5 | no |
| `texture-apply-pattern` | Texture / Apply Pattern | `Tool_Texture.ApplyPattern.cs` | 5 | no |
| `texture-configure` | Texture / Configure Import Settings | `Tool_Texture.Configure.cs` | 7 | no |
| `texture-create` | Texture / Create | `Tool_Texture.Create.cs` | 7 | no |
| `texture-inspect` | Texture / Inspect | `Tool_Texture.Inspect.cs` | 1 | **no (should be yes)** |

**Unity API surface used:**
- `Texture2D` (`SetPixels32`, `Apply`, `EncodeToPNG`)
- `AssetDatabase.LoadAssetAtPath`, `AssetDatabase.ImportAsset`, `AssetDatabase.IsValidFolder`, `AssetDatabase.Refresh`
- `AssetImporter.GetAtPath` cast to `TextureImporter` (`maxTextureSize`, `textureCompression`, `textureType`, `sRGBTexture`, `mipmapEnabled`, `isReadable`, `alphaSource`, `alphaIsTransparency`, `spriteImportMode`, `spritePixelsPerUnit`, `SaveAndReimport`)
- `ColorUtility.TryParseHtmlString`
- `Profiling.Profiler.GetRuntimeMemorySizeLong`
- `File.WriteAllBytes`, `Directory.CreateDirectory`, `Path.GetDirectoryName`

---

## 2. Redundancy Clusters

### Cluster R1 — Solid-fill / pattern / gradient as three separate "paint a texture" tools
**Members:** `texture-create`, `texture-apply-pattern`, `texture-apply-gradient`
**Overlap:** All three perform the same conceptual action — "fill a Texture2D PNG asset with pixel data" — and share the same downstream pipeline (`Texture2D` → `SetPixels32` → `Apply` → `EncodeToPNG` → `File.WriteAllBytes` → `AssetDatabase.ImportAsset`). They differ only in how the pixel array is generated:
- `texture-create`: uniform fill (`Array.Fill`)
- `texture-apply-pattern`: checkerboard / horizontal / vertical stripes
- `texture-apply-gradient`: linear / radial two-colour interpolation
A single tool dispatching on a `mode` (or `fillType`) parameter — `solid | pattern-checker | pattern-stripes-h | pattern-stripes-v | gradient-linear | gradient-radial` — would cover all three with shared validation. The `[Description]` clauses already overlap heavily ("Texture2D PNG asset", "Colors are hex strings") in `apply-pattern` and `apply-gradient` while `texture-create` accepts RGBA floats — itself an inconsistency (see A4).
**Impact:** Medium. The LLM choosing between "create" vs "apply-X" is ambiguous because the `apply-*` tools also create the file when absent (`apply-gradient` line 70–76). A user asking "make a 256x256 red-to-blue gradient texture" could legitimately call any of them.
**Confidence:** high

### Cluster R2 — `texture-configure` overlaps with `asset-set-import-settings`
**Members:** `texture-configure`, `asset-set-import-settings` (cross-domain)
**Overlap:** `texture-configure` exposes a typed convenience subset of what `asset-set-import-settings` already does generically via `SerializedObject` + property paths. Six of the seven configurable fields (`maxSize`, `compression`, `textureType`, `sRGB`, `mipmaps`, `readable`) are simply hard-coded property paths on `TextureImporter`.
**Impact:** Low — this is intentional design (typed convenience). Worth flagging only because the LLM may pick the generic tool when the typed one would be safer, or vice versa. A cross-link in the description ("prefer `texture-configure` for textures, fall back to `asset-set-import-settings` for advanced fields") would help.
**Confidence:** medium (design intent is plausible, not confirmed)

---

## 3. Ambiguity Findings

### A1 — `texture-inspect` description does not state read-only nature, and `ReadOnlyHint` is missing
**Location:** `texture-inspect` — `Tool_Texture.Inspect.cs:22`
**Issue:** The `[McpTool]` attribute does not set `ReadOnlyHint = true` despite the tool performing only `LoadAssetAtPath` + `StringBuilder` formatting. Compare the equivalent `asset-get-info` and `asset-get-import-settings`, which both set `ReadOnlyHint = true`. This is a missed safety hint, not an ambiguity per se — but inconsistent metadata across the codebase confuses the LLM about which tools are safe to call exploratorily.
**Evidence:** `Tool_Texture.Inspect.cs:22`: `[McpTool("texture-inspect", Title = "Texture / Inspect")]` — no `ReadOnlyHint`. Confirmed by `Grep` for `ReadOnlyHint` in `Editor/Tools/Texture` returning zero matches.
**Confidence:** high

### A2 — `texture-configure` boolean params use string `"true"`/`"false"` with empty-string sentinel
**Location:** `texture-configure` — `Tool_Texture.Configure.cs:35–37`
**Issue:** `srgb`, `mipmaps`, `readable` are typed as `string` with empty-string default = "skip". This is a workaround for "optional bool", but it forces the LLM to know the convention and is fragile (e.g. `"True"`, `"yes"`, `"1"` are silently rejected — only literal `"true"` lower-cased succeeds because `srgb.ToLowerInvariant() == "true"` treats anything else as `false`, line 104). The description says `'true'/'false'` but does not warn that any other string evaluates to `false`.
**Evidence:** `Tool_Texture.Configure.cs:104`: `importer.sRGBTexture = srgb.ToLowerInvariant() == "true";` — silent coercion of `"yes"`, `"1"`, etc. to `false`.
**Confidence:** high

### A3 — `texture-configure` `maxSize` lacks validation against valid size set
**Location:** `texture-configure` param `maxSize` — `Tool_Texture.Configure.cs:32`
**Issue:** Description enumerates "32, 64, 128, 256, 512, 1024, 2048, 4096, 8192" but the code accepts any `int > 0` (line 62). Passing `maxSize=999` will be accepted and silently rounded by Unity. The LLM has no way to know whether its value was honoured exactly without reinspecting.
**Evidence:** `Tool_Texture.Configure.cs:62–66`: `if (maxSize > 0) { importer.maxTextureSize = maxSize; sb.AppendLine($"  Max Size: {maxSize}"); }` — reports the *requested* value, not the actual `importer.maxTextureSize` after assignment.
**Confidence:** high

### A4 — `texture-create` uses RGBA floats while `apply-*` tools use hex strings
**Location:** `texture-create` params `fillR`/`fillG`/`fillB`/`fillA` vs `texture-apply-gradient`/`texture-apply-pattern` `color1Hex`/`color2Hex`
**Issue:** Three tools in the same domain take colour input in two incompatible representations. An LLM building a workflow ("create a red texture, then apply a darker red gradient") has to translate between `1.0/0.0/0.0/1.0` and `#FF0000`. There is no reason for the inconsistency.
**Evidence:** `Tool_Texture.Create.cs:37–40` (RGBA floats) vs `Tool_Texture.ApplyGradient.cs:42–43` and `Tool_Texture.ApplyPattern.cs:35–36` (hex strings).
**Confidence:** high

### A5 — `texture-apply-gradient` silently inherits dimensions from existing file
**Location:** `texture-apply-gradient` — `Tool_Texture.ApplyGradient.cs:68–76`
**Issue:** When the target file exists, the tool reuses its `width`/`height`. When it does not exist, it hard-codes 256×256. Neither behaviour is documented in the `[Description]` or `<param>` docs. There is also no `width`/`height` parameter to override either. The LLM cannot create a 512×512 gradient from scratch via this tool.
**Evidence:** `Tool_Texture.ApplyGradient.cs:68–76`:
```csharp
int w = 256;
int h = 256;
var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
if (existing != null) { w = existing.width; h = existing.height; }
```
The method-level `[Description]` says only "Creates the texture if it does not exist" with no mention of size behaviour.
**Confidence:** high

### A6 — `gradientType` and `pattern` params accept magic strings without enumerated alternatives in description
**Location:** `texture-apply-gradient` `gradientType`, `texture-apply-pattern` `pattern`
**Issue:** Param `[Description]` does enumerate values for both — that part is OK. However, unrecognised values silently fall through to a default branch (gradient → linear, pattern → checkerboard) instead of erroring. The user gets a success message claiming pattern `"foo"` was applied while in fact a checkerboard was painted.
**Evidence:** `Tool_Texture.ApplyPattern.cs:98–103`:
```csharp
var useFirst = normPattern switch {
    "stripes-horizontal" => (y / patternSize) % 2 == 0,
    "stripes-vertical"   => (x / patternSize) % 2 == 0,
    _ => ((x / patternSize) + (y / patternSize)) % 2 == 0,  // silent default
};
```
And `Tool_Texture.ApplyPattern.cs:131`: `return ToolResponse.Text($"Pattern '{pattern}' applied to '{path}'…");` — echoes the bogus value back as if accepted.
**Confidence:** high

### A7 — `Tool_Texture.ApplyPattern.cs` region typo
**Location:** `Tool_Texture.ApplyPattern.cs:14`
**Issue:** `#region TOOOL METHODS` (three Os). Cosmetic, but worth flagging since the convention across the rest of the domain is `#region TOOL METHODS`.
**Evidence:** Line 14 verbatim: `#region TOOOL METHODS`.
**Confidence:** high

---

## 4. Default Value Issues

### D1 — `texture-create` default size 64×64 is unusually small
**Location:** `texture-create` params `width`, `height`
**Issue:** A default of 64×64 is small enough that nearly every real-world caller will pass an explicit size, defeating the purpose of the default. 256×256 (which is what `texture-apply-gradient` quietly assumes when creating from scratch — see A5) is closer to a typical use case for a placeholder solid-fill texture used for material previews / UI backgrounds.
**Current:** `int width = 64, int height = 64`
**Suggested direction:** Either raise the default to a more representative size (e.g. 256), or remove the default entirely and require the caller to specify. Pick one and align with `apply-gradient`'s implicit 256.
**Confidence:** medium

### D2 — `texture-apply-gradient` lacks `width`/`height` parameters entirely
**Location:** `texture-apply-gradient` (no such params exist)
**Issue:** This is partly a capability gap (G4 below), but listing here as a default issue: the only path to a 512×512 gradient is to create the texture first via `texture-create` then apply the gradient. The implicit 256-default is documented nowhere.
**Current:** params: `path, gradientType, angle, color1Hex, color2Hex`
**Suggested direction:** Add optional `width = 256`, `height = 256` and document that existing-file dimensions take precedence (or override them with explicit params).
**Confidence:** high

### D3 — `texture-configure` uses `-1` and `""` as skip sentinels
**Location:** `texture-configure` all params
**Issue:** Two different sentinel idioms in the same tool: `int = -1` and `string = ""`. The LLM has to remember which one means "skip" for which param. A C# `Nullable<int>`/`int?` plus optional strings would be more uniform; alternatively all skip-sentinels could be `""` (with `int.TryParse` for `maxSize`).
**Current:** `int maxSize = -1`, others `string ... = ""`
**Suggested direction:** Adopt one sentinel convention across the tool. `""` for everything is the lower-friction option.
**Confidence:** medium

### D4 — `texture-apply-pattern` silently clamps `patternSize < 1` to 1
**Location:** `texture-apply-pattern` param `patternSize`
**Issue:** Line 51–54 mutates the input value rather than returning an error. Caller passing `patternSize=0` or `-5` gets a success message with an unannounced correction.
**Current:** `int patternSize = 8`, with silent clamp to ≥ 1
**Suggested direction:** Return `ToolResponse.Error` for `patternSize < 1` instead of silently clamping.
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot draw arbitrary pixel data / text / shapes onto a texture
**Workflow:** A common LLM use case is "stamp a 16×16 icon onto a texture", "draw a circle at (cx,cy) with radius r", or "blit one PNG onto another at a given position". None of these are possible.
**Current coverage:** `texture-apply-pattern` covers procedural fills only; `texture-apply-gradient` covers gradients only; `texture-create` covers uniform fills only.
**Missing:** No `texture-blit` (rect-copy from texture A to texture B), no `texture-set-pixels` (write an array of `(x,y,colour)` triples), no `texture-draw-shape` (circle / rect / line). The underlying API (`Texture2D.SetPixels32`) is already used internally — exposing it via a region rectangle would be straightforward.
**Evidence:** Domain has 3 tools that mutate pixel data (`Create`, `ApplyPattern`, `ApplyGradient`), and Grep over the domain shows no `SetPixel` / `blit` / `CopyTexture` API call exposed externally. None of the existing tools accept positional input.
**Confidence:** high

### G2 — Cannot configure sprite slicing / pivot / pixels-per-unit / spritesheet
**Workflow:** Convert an imported PNG into a multi-sprite spritesheet: set `textureType=Sprite`, `spriteImportMode=Multiple`, define `spritesheet` rectangles, set `spritePixelsPerUnit`, set `pivot`. This is one of the most common 2D Unity workflows.
**Current coverage:** `texture-configure` can set `textureType=sprite` only. `texture-inspect` can *read* `spriteImportMode` and `spritePixelsPerUnit` (lines 81–83) but no tool can *write* them.
**Missing:** No way to set `TextureImporter.spriteImportMode`, `spritePixelsPerUnit`, `spritePivot`, or `spritesheet` (the `SpriteMetaData[]` array). The fallback is the generic `asset-set-import-settings` with raw property paths — this is brittle for the LLM, especially for the array-of-rects `spritesheet` field which is not a flat key.
**Evidence:** `Tool_Texture.Configure.cs:30–38` shows the seven configurable fields; none of them are sprite-mode metadata. `Tool_Texture.Inspect.cs:80–84` reads but does not write these fields. A `Grep` over `Editor/Tools` for `spriteImportMode|spritesheet|SpriteMetaData|spritePixelsPerUnit` returned only `Tool_Texture.Inspect.cs` — confirming no writer exists. There is also no `Editor/Tools/Sprite/` domain (Glob returned zero files).
**Confidence:** high

### G3 — Cannot configure runtime sampler settings (filterMode, wrapMode, anisoLevel)
**Workflow:** Make a pixel-art texture point-sampled (`filterMode=Point`), make a tiling texture wrap (`wrapMode=Repeat`), or boost anisotropic filtering on a ground texture.
**Current coverage:** `texture-inspect` reads all three (lines 53–55) but `texture-configure` exposes none of them.
**Missing:** `TextureImporter.filterMode`, `TextureImporter.wrapMode`, `TextureImporter.anisoLevel` are not configurable through the typed `texture-configure` tool.
**Evidence:** `Tool_Texture.Configure.cs:30–38` parameter list has 7 fields, none related to sampler. Grep for `filterMode|wrapMode|anisoLevel` in `Editor/Tools/Texture` matches only `Inspect.cs:53–55`.
**Confidence:** high

### G4 — Cannot create a gradient texture from scratch with chosen dimensions
**Workflow:** "Create a 512×128 horizontal gradient PNG at Assets/UI/Bar.png from #000 to #FFF".
**Current coverage:** `texture-apply-gradient` creates the file if absent, but uses hard-coded 256×256.
**Missing:** Optional `width`/`height` parameters on `texture-apply-gradient`. As noted in A5/D2, this currently requires a two-step workaround (`texture-create` then `texture-apply-gradient`).
**Evidence:** `Tool_Texture.ApplyGradient.cs:68–69`: `int w = 256; int h = 256;` — hard-coded fallback.
**Confidence:** high

### G5 — Cannot read/write pixels of an existing texture programmatically
**Workflow:** "Recolour every black pixel in this texture to red", "extract the colour at (10,10)", "make all opaque pixels semi-transparent".
**Current coverage:** None.
**Missing:** No `texture-get-pixel(x,y)`, no `texture-recolour(fromHex, toHex, tolerance)`, no `texture-modify-channel(channel, multiplier)`. The `apply-pattern` flow already toggles `isReadable` to read pixels (`Tool_Texture.ApplyPattern.cs:67–78`), so the infrastructure is half there.
**Evidence:** Domain has no tool with name containing `pixel`, `recolour`/`recolor`, `channel`, or `modify`. Inventory in §1 covers all 5 tools.
**Confidence:** high

### G6 — Cannot resize / crop / rotate / flip a texture
**Workflow:** "Downscale this 4096 texture to 1024 to reduce build size", "flip a sprite horizontally", "rotate this PNG 90°".
**Current coverage:** `texture-configure` exposes `maxSize` (an importer hint) but not actual texture resampling. There is no crop / rotate / flip.
**Missing:** No `texture-resize`, `texture-crop`, `texture-flip(horizontal|vertical)`, `texture-rotate(degrees)`. These are routine asset-prep operations.
**Evidence:** Inventory in §1 shows the 5 existing tools; none accept transform arguments. Grep over `Editor/Tools/Texture` for `resize|crop|flip|rotate` returned no matches in tool method bodies relevant to actual texture transformation.
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | G2 | Capability Gap | 5 | 4 | 10 | high | Cannot write sprite metadata (slicing, PPU, pivot) — critical for 2D workflows |
| 2 | G3 | Capability Gap | 4 | 1 | 20 | high | filterMode/wrapMode/anisoLevel not in texture-configure (cheap to add) |
| 3 | A4 | Ambiguity | 4 | 2 | 16 | high | Inconsistent colour input (RGBA floats vs hex) across same domain |
| 4 | G4 | Capability Gap | 4 | 1 | 20 | high | apply-gradient missing width/height params; hard-coded 256×256 |
| 5 | A1 | Ambiguity | 3 | 1 | 15 | high | texture-inspect missing ReadOnlyHint — one-line fix |
| 6 | A6 | Ambiguity | 4 | 1 | 20 | high | gradientType / pattern silently fall through to defaults on bad input |
| 7 | A2 | Ambiguity | 3 | 2 | 12 | high | Configure bool fields use string sentinel; non-`"true"` silently coerces to `false` |
| 8 | R1 | Redundancy | 3 | 4 | 6 | high | create / apply-pattern / apply-gradient share a pixel-write pipeline |
| 9 | G1 | Capability Gap | 4 | 4 | 8 | high | No pixel-level draw / blit / set-pixels primitive |
| 10 | G6 | Capability Gap | 3 | 3 | 9 | high | No resize / crop / flip / rotate |
| 11 | A3 | Ambiguity | 2 | 1 | 10 | high | maxSize accepts any int; response echoes requested not actual |
| 12 | A5 | Ambiguity | 3 | 1 | 15 | high | apply-gradient size behaviour undocumented |
| 13 | D1 | Default | 2 | 1 | 10 | medium | texture-create default 64×64 likely too small |
| 14 | D4 | Default | 2 | 1 | 10 | high | apply-pattern silently clamps patternSize < 1 |
| 15 | A7 | Ambiguity | 1 | 1 | 5 | high | Region typo `TOOOL METHODS` in ApplyPattern.cs |

(Priority computed as Impact × (6 − Effort). Higher = better ROI.)

**Suggested triage clusters for the planner agent:**
- **Quick wins (Effort 1, finish in one PR):** A1, A6, A7, D4, G3, G4 — all small description / signature tweaks with high clarity payoff.
- **Mid-effort consistency:** A2, A3, A4, A5, D2 — colour input unification + sentinel cleanup + documenting size behaviour.
- **Strategic capability work:** G1, G2, G5, G6 — these are new tools, not refactors. G2 (sprite slicing) is the most user-visible gap; G6 (resize/crop) is the most asset-pipeline-relevant.
- **Informational only:** R2 (cross-domain overlap with `asset-set-import-settings`) — mention in description, no removal.

---

## 7. Notes

- **Cross-domain dependency observed:** `texture-configure` is a typed convenience over `asset-set-import-settings`. If sprite metadata writers (G2) are added as a new texture-domain tool, the same convenience-vs-generic question will recur. The planner should decide whether new sprite tools live in Texture or warrant a new `Sprite/` domain. Given there is currently NO `Editor/Tools/Sprite/` directory (confirmed by Glob), starting one would be a meaningful architectural decision.
- **Style consistency caveat:** Tool method `CreateTexture` (in `Tool_Texture.Create.cs:33`) is the only method whose C# name diverges from its tool ID stem (`CreateTexture` vs `texture-create`). All other methods match (`ApplyGradient`/`apply-gradient`, `ApplyPattern`/`apply-pattern`, `Configure`/`configure`, `Inspect`/`inspect`). Minor, but worth aligning to `Create` for symmetry.
- **Workflows intentionally not deep-dived:** I did not investigate compute-shader or GPU-based texture generation paths. The domain is firmly CPU-side (`SetPixels32` + `EncodeToPNG`). If GPU paths become a goal, that is out of scope for this audit.
- **Open question for reviewer:** Is `texture-configure` intended to remain the typed wrapper indefinitely, or is the long-term plan to fold all importer config into `asset-set-import-settings`? The answer affects whether G2/G3 should extend `texture-configure` or live as new tools.
