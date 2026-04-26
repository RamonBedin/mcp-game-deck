# Audit Report — Asset

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Asset/`
**Status:** COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 10 (via Glob `Editor/Tools/Asset/Tool_Asset.*.cs`)
- `files_read`: 10
- `files_analyzed`: 10

**Balance:** balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- This report does make absence claims (e.g. "no asset-set-labels tool exists in the domain", "no AssetBundle assignment tool"). All such claims were verified against the complete domain (10/10 files read) and corroborated by Grep across `Editor/Tools/**/*.cs` for the relevant Unity APIs.

**Cross-domain references checked:**
- `Editor/Tools/Material/Tool_Material.Create.cs` — overlaps with `asset-create` Material branch
- `Editor/Tools/Physics/Tool_Physics.PhysicsMaterial.cs` — overlaps with `asset-create` PhysicMaterial branch
- `Editor/Tools/ScriptableObject/Tool_ScriptableObject.Create.cs` — covers a creation gap in `asset-create`
- `Editor/Tools/Animation/Tool_Animation.CreateClip.cs` and `ConfigureController.cs` — overlap with `asset-create` AnimatorController branch
- `Editor/Tools/Object/Tool_Object.GetData.cs` and `Modify.cs` — partial overlap with import-settings pair
- `Editor/Tools/Texture/Tool_Texture.Configure.cs` and `Tool_Texture.Inspect.cs` — overlaps with import-settings pair for textures

**Reviewer guidance:**
- The Create tool stands out as the most problematic in this domain: limited type matrix, dead `using` directives, `propertiesJson` parameter that is documented as "reserved for future use" yet is wired through to `ApplyPropertiesFromJson`, and large overlap with three other domain creators.
- The `path` discipline ("auto-prepend Assets/") is consistent across 9 of 10 tools — that's a good baseline, document it once.
- Two tools (`asset-find`, `asset-get-info`, `asset-get-import-settings`) correctly use `ReadOnlyHint = true`. Coverage of read-only marking is good.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---|---|---|---|---|
| `asset-copy` | Asset / Copy | `Tool_Asset.Copy.cs` | 2 | no |
| `asset-create` | Asset / Create | `Tool_Asset.Create.cs` | 3 | no |
| `asset-create-folder` | Asset / Create Folder | `Tool_Asset.CreateFolder.cs` | 1 | no |
| `asset-delete` | Asset / Delete | `Tool_Asset.Delete.cs` | 2 | no |
| `asset-find` | Asset / Find | `Tool_Asset.Find.cs` | 3 | yes |
| `asset-get-info` | Asset / Get Info | `Tool_Asset.GetInfo.cs` | 1 | yes |
| `asset-get-import-settings` | Asset / Get Import Settings | `Tool_Asset.ImportSettings.cs` | 1 | yes |
| `asset-set-import-settings` | Asset / Set Import Settings | `Tool_Asset.ImportSettings.cs` | 2 | no |
| `asset-move` | Asset / Move | `Tool_Asset.Move.cs` | 2 | no |
| `asset-refresh` | Asset / Refresh | `Tool_Asset.Refresh.cs` | 1 | no |
| `asset-rename` | Asset / Rename | `Tool_Asset.Rename.cs` | 2 | no |

**Internal Unity APIs used:**
- `AssetDatabase.CopyAsset`, `AssetDatabase.MoveAsset`, `AssetDatabase.DeleteAsset`, `AssetDatabase.MoveAssetToTrash`
- `AssetDatabase.RenameAsset`, `AssetDatabase.GenerateUniqueAssetPath`, `AssetDatabase.IsValidFolder`, `AssetDatabase.CreateFolder`
- `AssetDatabase.CreateAsset`, `AssetDatabase.SaveAssets`, `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`
- `AssetDatabase.FindAssets`, `AssetDatabase.GUIDToAssetPath`, `AssetDatabase.AssetPathToGUID`, `AssetDatabase.LoadMainAssetAtPath`
- `AssetDatabase.GetLabels`, `AssetDatabase.GetDependencies`
- `AssetImporter.GetAtPath`, `importer.SaveAndReimport`
- `SerializedObject.GetIterator`/`FindProperty`/`ApplyModifiedPropertiesWithoutUndo`
- `new Material(Shader.Find("Standard"))`, `new RenderTexture(...)`, `new PhysicsMaterial()`, `AnimatorController.CreateAnimatorControllerAtPath`

---

## 2. Redundancy Clusters

### Cluster R1 — Asset creation surface fragmented across four domains
**Members:** `asset-create` (this domain), `material-create` (Material domain), `physics-create-material` (Physics domain), `scriptableobject-create` (ScriptableObject domain). Plus indirect overlap with `animation-create-clip` and `animation-configure-controller`.
**Overlap:**
- `asset-create assetType="Material"` creates a Material with the hard-coded `Standard` shader; `material-create` does the same job with explicit shader name and render-pipeline auto-detect. The dedicated tool is strictly better — `asset-create`'s Material branch is essentially a degraded duplicate.
- `asset-create assetType="PhysicMaterial"` produces an empty `PhysicsMaterial` asset; `physics-create-material` produces the same asset with friction/bounciness/combine modes configured. Same redundancy pattern.
- `asset-create assetType="AnimatorController"` produces an empty controller; the Animation domain has `animation-configure-controller` which goes much further. Once the controller is created here it must be reopened by the dedicated tool to be useful.
- `asset-create` does NOT support ScriptableObject creation, but `scriptableobject-create` exists. So the Create tool advertises a "general asset creator" identity that it doesn't deliver on.
**Impact:** High — when an LLM is asked "create a material with the URP Lit shader", the model has to choose between `asset-create` and `material-create`. The descriptions don't disambiguate, so it's a coin flip. If the model picks `asset-create`, it gets the wrong shader and no way to override it within that tool.
**Confidence:** high (cross-checked all four files; `Tool_Asset.Create.cs` line 66 hard-codes `Shader.Find("Standard")`).

### Cluster R2 — Generic property-write surface duplicated
**Members:** `asset-set-import-settings`, `object-modify`, `asset-create` (`propertiesJson` param)
**Overlap:** All three accept a JSON object mapping property paths to string values, then apply via `SerializedObject.FindProperty`. The implementations even share the same value-coercion logic (Integer/Float/Boolean/String/Enum/Color), but each domain has its own copy of the parser and `ApplyValue`/`ApplyStringValueToProperty` helper. Specifically:
- `Tool_Asset.Create.cs` lines 126-279 (`ApplyPropertiesFromJson` + `ApplyValue`)
- `Tool_Asset.ImportSettings.cs` lines 232-290 (`ApplyStringValueToProperty`)
- `Tool_Object.Modify.cs` (separate copy of the same logic per its file)

`asset-set-import-settings` writes through an `AssetImporter`; `object-modify` writes through any `UnityEngine.Object` resolved from instanceId. So they ARE distinct in *what* they target, but the parsing/coercion duplication is real.
**Impact:** Medium — runtime ambiguity is moderate (the targets differ enough that the LLM picks the right tool most of the time), but it's high maintenance debt and means bugs in one parser silently exist in others. The `asset-create` `propertiesJson` parameter is specifically harmful because the method-level XML doc says "reserved for future use" while the code actually wires it through — descriptions and behaviour disagree.
**Confidence:** high (read all three files).

---

## 3. Ambiguity Findings

### A1 — `asset-create` advertises a misleading scope
**Location:** `asset-create` — `Tool_Asset.Create.cs` lines 32-33
**Issue:** Description claims "Creates a new Unity asset at the given path" and lists 4 supported types, but most assets a user would want to create live in dedicated tools: ScriptableObject, AnimationClip, Prefab, Material (with shader choice), PhysicsMaterial (with parameters). The tool's name implies general capability while the implementation is a tiny subset.
**Evidence:** Method-level Description: `"Creates a new Unity asset at the given path. assetType values: 'Material', 'RenderTexture', 'PhysicMaterial', 'AnimatorController'. path must include the file name and correct extension."`
**Confidence:** high

### A2 — `asset-create` `propertiesJson` description contradicts implementation
**Location:** `asset-create` param `propertiesJson` — `Tool_Asset.Create.cs` line 37
**Issue:** Param description: `"Optional JSON object with additional initial properties. Currently reserved for future use."` But the code path actually invokes `ApplyPropertiesFromJson` for Material and PhysicMaterial branches and reports applied properties in the response. The "reserved" claim will cause LLMs to skip the parameter even when it works.
**Evidence:** Param description vs `Tool_Asset.Create.cs` lines 74, 91 which call `ApplyPropertiesFromJson(mat, propertiesJson)` and concatenate results into the response.
**Confidence:** high

### A3 — `asset-find` does not list common search-filter prefixes
**Location:** `asset-find` param `searchFilter` — `Tool_Asset.Find.cs` line 25
**Issue:** Description shows three examples (`t:Prefab`, `t:Texture2D sky`, `l:MyLabel`) but doesn't enumerate what the prefixes mean (`t:` = type, `l:` = label, `b:` = bundle, `ref:` = references). LLMs who haven't seen Unity's filter docs will copy the examples verbatim and get stuck when those don't fit the situation.
**Evidence:** `[Description("Search filter (e.g. 't:Prefab', 't:Texture2D sky', 'l:MyLabel').")]`
**Confidence:** medium (LLMs may already know the syntax from training data; flag as worth tightening but not critical).

### A4 — `asset-set-import-settings` lacks "use this when X, not Y" disambiguation versus `object-modify`
**Location:** `asset-set-import-settings` — `Tool_Asset.ImportSettings.cs` line 92
**Issue:** Both tools write properties via SerializedObject + JSON. The Asset variant targets the *importer* (and triggers `SaveAndReimport`); the Object variant targets *any* UnityEngine.Object resolved from instanceId. Neither description tells the LLM when to choose which. A user asking "set the texture's mipmap to false" could go either way (texture asset has both an importer with `mipmapEnabled` and an asset with `mipMapBias`).
**Evidence:** Description: `"Applies property overrides to an asset's importer via SerializedObject and triggers SaveAndReimport. settingsJson must be a JSON object mapping property paths to string values..."` — no mention of the alternative path or the SaveAndReimport side-effect's importance.
**Confidence:** medium

### A5 — `asset-refresh` `forceUpdate` description doesn't explain consequences
**Location:** `asset-refresh` param `forceUpdate` — `Tool_Asset.Refresh.cs` line 22
**Issue:** Description: `"Force reimport all assets. Default false."` — does not warn that ForceUpdate reimports the entire project (potentially minutes for medium projects). LLMs may pass `true` casually because it sounds like "make sure refresh actually worked".
**Evidence:** Param description above.
**Confidence:** medium

### A6 — `asset-create-folder` description omits idempotency
**Location:** `asset-create-folder` — `Tool_Asset.CreateFolder.cs` line 20
**Issue:** Tool is idempotent (returns success when folder already exists, see line 38-40), but description doesn't say so. LLMs may spend tool calls running `asset-find` first to check whether the folder exists.
**Evidence:** Description: `"Creates a folder in the project, including any missing intermediate folders."` — silent on the existing-folder case.
**Confidence:** low (mostly cosmetic — calling the tool when the folder exists is harmless).

### A7 — `asset-rename` doesn't warn about the unique-name requirement
**Location:** `asset-rename` param `newName` — `Tool_Asset.Rename.cs` line 28
**Issue:** Doesn't mention that `newName` must be unique within the same folder, or that the tool will fail if a sibling with the same name exists. The underlying `AssetDatabase.RenameAsset` returns an error string in that case which is surfaced — but the param description should warn upfront.
**Evidence:** `[Description("New file name without extension (e.g. 'NewName').")]`
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `asset-create` `assetType` default of "Material" silently runs Material branch
**Location:** `asset-create` param `assetType`, default `"Material"`
**Issue:** When LLM omits `assetType` (which it might if the prompt says only "create an asset"), the tool silently creates a Material with the hard-coded Standard shader. There's no error path for "you forgot to specify what kind". This compounds A1 (misleading scope) — the silent fallback is the worst default among the four supported types, since URP/HDRP projects don't even have the Standard shader.
**Current:** `string assetType = "Material"`
**Suggested direction:** Make `assetType` required (no default), OR keep the default but also detect render pipeline mismatch and error out before creating an unusable asset. (No code suggested — just signalling intent.)
**Confidence:** high

### D2 — `asset-find` default folder of "Assets" is correct but undocumented as project-wide
**Location:** `asset-find` param `folder`, default `"Assets"`
**Issue:** Sensible default, but the description says "Folder to search in (e.g. 'Assets/Prefabs'). Default 'Assets'." — doesn't make clear that the default *is* a project-wide search. Minor.
**Current:** `string folder = "Assets"`
**Suggested direction:** Description tweak: clarify "Default 'Assets' (entire project)."
**Confidence:** low

### D3 — `asset-find` default `maxResults = 25` may truncate silently
**Location:** `asset-find` param `maxResults`, default `25`
**Issue:** Default cap of 25 is reasonable, but the response only says "Found N assets (showing M)" once, which is fine. No issue with the value itself; flagging because a reviewer may want to consider whether 25 is the right ceiling for an LLM context window or whether it should be higher / paged.
**Current:** `int maxResults = 25`
**Suggested direction:** No action required unless evidence shows the LLM is re-issuing requests because of truncation.
**Confidence:** low

### D4 — `asset-delete` default `moveToTrash = true` is good but irreversible-mode is unprotected
**Location:** `asset-delete` param `moveToTrash`, default `true`
**Issue:** Default is safe (trash, recoverable). However when LLM passes `false`, there's no confirmation/dry-run path — the asset is gone permanently. This is by design, but the param description could surface the warning more prominently (currently: `"Move to OS trash instead of permanent delete. Default true."`).
**Current:** `bool moveToTrash = true`
**Suggested direction:** Param description: explicitly warn that `false` is unrecoverable. No code change.
**Confidence:** low

### D5 — `asset-create` `propertiesJson` default of empty string is fine but the param itself is misdocumented
**Location:** `asset-create` param `propertiesJson`, default `""`
**Issue:** Default empty is correct (skip applying properties). The problem is upstream — the description claims "reserved for future use" while the parameter is functional. See A2.
**Current:** `string propertiesJson = ""`
**Suggested direction:** Fix the description (A2), default value itself is fine.
**Confidence:** high (this is really a description bug; included here because reviewer may want to handle it as part of default-value cleanup).

---

## 5. Capability Gaps

### G1 — Asset labels are read-only in this domain
**Workflow:** Tag a freshly-imported pack of textures with a label "Boss" so the rest of the team can `asset-find l:Boss` later.
**Current coverage:** `asset-get-info` lists labels (line 59-63). `asset-find` accepts `l:Label` filter syntax.
**Missing:** No tool calls `AssetDatabase.SetLabels` or `AssetDatabase.ClearLabels`. The whole label workflow is read-only.
**Evidence:** Grep across `Editor/Tools/**/*.cs` for `SetLabels|ClearLabels|GetLabels` returns only `Tool_Asset.GetInfo.cs` (see Caveats). The setter API is not wrapped anywhere.
**Confidence:** high (10/10 Asset files analyzed; cross-domain Grep confirmed no other domain wraps it either).

### G2 — ScriptableObject creation absent from the asset-create catalog despite being a top-3 asset type
**Workflow:** "Create a new WeaponConfig asset under Assets/Data/Weapons named Sword."
**Current coverage:** A separate domain `scriptableobject-create` handles this.
**Missing:** Within the Asset domain, `asset-create` advertises itself as a general asset creator but excludes the most common scripted-data asset type. This is more of a catalog-coherence gap than a true capability gap (the capability exists), but it forces the LLM to know that "ScriptableObject" lives in a different domain even though "Material" lives in `asset-create`.
**Evidence:** `asset-create` switch statement lines 62-110 — only 4 cases, no ScriptableObject branch.
**Confidence:** high

### G3 — No tool to assign or query AssetBundle name on an asset
**Workflow:** Mark a set of prefabs as belonging to AssetBundle `world1` so they ship in a separate streaming chunk.
**Current coverage:** None.
**Missing:** `AssetImporter.assetBundleName` and `assetBundleVariant` are not wrapped. `asset-set-import-settings` *could* accept these via the SerializedObject path, but the relevant property names aren't standard SerializedObject keys; they're properties on the importer, not serialized fields. So the existing tool won't work.
**Evidence:** Grep for `AssetBundle` across `Editor/Tools/**/*.cs` returns zero matches in source files (only `.meta` folder metadata files, which are unrelated to the search).
**Confidence:** medium — high confidence the API isn't wrapped, medium confidence on whether AssetBundles are still a priority workflow in 2026 (Addressables is the modern replacement). Reviewer may decide to deprioritize this.

### G4 — No tool to write asset dependencies / detect references
**Workflow:** "Find every asset that references `Assets/Materials/Player.mat` so I can audit before deleting."
**Current coverage:** `asset-get-info` returns *outgoing* dependencies (`AssetDatabase.GetDependencies`, line 74).
**Missing:** No tool returns *incoming* references. `AssetDatabase.GetDependencies` only goes one way; the inverse query needs `AssetDatabase.FindAssets` followed by per-asset dependency scan, which the LLM cannot orchestrate efficiently. Also no tool wraps `EditorUtility.CollectDependencies` or `AssetDatabase.GetAssetDependencyHash`.
**Evidence:** `Tool_Asset.GetInfo.cs` line 74: `string[] deps = AssetDatabase.GetDependencies(assetPath, false);` — outgoing only.
**Confidence:** high

### G5 — No bulk / batch operations for any of the CRUD verbs
**Workflow:** "Move all .png files under `Assets/RawArt` into `Assets/Art/Sprites`." or "Delete every .anim file with prefix `Test_`."
**Current coverage:** Single-asset versions of move, delete, copy, rename. The LLM has to call them in a loop.
**Missing:** No batch variant. Each call costs a round-trip and a MainThreadDispatcher hop; for 50 files that's 50 calls. There's also `BatchExecute` infrastructure in the repo (saw `Editor/Tools/BatchExecute.meta`) — flag whether asset-domain bulk ops would naturally compose with that or warrant their own tool.
**Evidence:** All single-target signatures: `Move(sourcePath, destinationPath)`, `Delete(assetPath, moveToTrash)`, `Copy(sourcePath, destinationPath)`, `Rename(assetPath, newName)`.
**Confidence:** high

### G6 — No `asset-exists` / cheap existence check tool
**Workflow:** "Before I create `Assets/Prefabs/Player.prefab`, check if it exists so I can pick a new name." Or: "Is this path an asset, a folder, or nothing?"
**Current coverage:** `asset-get-info` works for existing assets but errors when the path is missing — usable but expensive (loads the asset and gathers labels + deps + size).
**Missing:** A lightweight predicate that returns `exists: bool, kind: "asset"|"folder"|"none"`. `AssetDatabase.AssetPathToGUID` returns empty string for missing assets — wrapping this would be a 5-line tool.
**Evidence:** Only existence-related entry points in the domain are `asset-get-info` (heavy) and `asset-create-folder` which checks `IsValidFolder` internally but doesn't expose the result as a query.
**Confidence:** medium (could argue this is a nice-to-have rather than a real gap — LLMs can use `asset-find` to check existence too).

### G7 — Sprite slicing / multi-sprite import workflow not addressable via import-settings
**Workflow:** "Import this sprite-sheet PNG, slice it into a 4x4 grid, generate names Idle_00..Idle_15."
**Current coverage:** `asset-set-import-settings` can flip `textureType` to Sprite (`8`) and `spriteImportMode` to Multiple (`2`), but cannot populate the sprite rect array — that requires writing a `SpriteMetaData[]` to the `TextureImporter.spritesheet` property, which is NOT a SerializedObject field exposed by name.
**Current coverage (cross-domain):** Looking at `Editor/Tools/Texture/Tool_Texture.Configure.cs` and `Tool_Texture.ApplyPattern.cs` may already address this — I noted these exist but did not deep-read them. The Asset domain itself does not.
**Missing in Asset domain:** Any path to the sprite-rect array via the generic SerializedObject mechanism. The string-only value coercion in `ApplyStringValueToProperty` (lines 232-290) cannot construct a `SpriteMetaData[]`.
**Evidence:** `Tool_Asset.ImportSettings.cs` `ApplyStringValueToProperty` switch supports Integer/Float/Boolean/String/Enum only — no array, no struct, no object reference. Anything beyond scalar and enum is unreachable.
**Confidence:** medium (the gap is real for the Asset domain; cross-domain coverage in `Tool_Texture.*` may close it but I have not read those files in full).

### G8 — `asset-set-import-settings` cannot set ObjectReference properties
**Workflow:** "Set the `secondaryTextures` of this importer", "Set the `material` reference on this importer".
**Current coverage:** `asset-set-import-settings` parses string values and dispatches via `ApplyStringValueToProperty`.
**Missing:** The switch (lines 232-290) returns `false` for every property type other than Integer/Float/Boolean/String/Enum. ObjectReference is not handled — yet `GetImporterPropertyValueString` (line 219) prints ObjectReference values, so the LLM can read them but cannot write them. Asymmetric capability.
**Evidence:** `Tool_Asset.ImportSettings.cs` lines 206-223 (read includes ObjectReference) vs lines 232-289 (write does not). Note: `Tool_Object.Modify.cs` reportedly handles object references "by asset path" per its description, so the *capability* exists in another domain — this is an asymmetry within the import-settings tool itself.
**Confidence:** high

### G9 — Dead `using` directives and unused `SimpleJSON` import suggest the file was patched without cleanup
**Workflow:** N/A — code hygiene, not capability.
**Current coverage:** N/A.
**Missing:** N/A.
**Evidence:** `Tool_Asset.Create.cs` lines 5, 8, 12, 13:
```
using SimpleJSON;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;
```
The hand-rolled JSON parsing in `ApplyPropertiesFromJson` (lines 126-213) does not invoke `SimpleJSON`. The other three are pure IDE auto-imports left behind. Compiles fine but signals the file should be cleaned.
**Confidence:** high — verified zero references to `JSON.Parse`, `SimpleJSON`, `EventTrigger`, `YamlDotNet`, `GraphicsBuffer` in the file.

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|---|---|---|---|---|---|---|
| 1 | R1 / A1 / D1 | Redundancy + Ambiguity + Default | 5 | 3 | 15 | high | `asset-create` overlaps with three dedicated creators, has misleading scope and a silent Material default. Either narrow it, deprecate it, or make it the single dispatcher and remove the others — current state forces the LLM to guess. |
| 2 | A2 / D5 | Ambiguity (description vs behaviour) | 4 | 1 | 20 | high | `asset-create` `propertiesJson` says "reserved for future use" but is wired through. One-word doc fix. |
| 3 | G1 | Capability Gap (label write) | 4 | 2 | 16 | high | No tool calls `AssetDatabase.SetLabels` / `ClearLabels`. Labels are advertised in `get-info` and `find` but cannot be written. |
| 4 | G8 | Capability Gap (asymmetry) | 4 | 2 | 16 | high | `asset-set-import-settings` reads ObjectReference but cannot write it. Asymmetric. |
| 5 | G4 | Capability Gap (incoming refs) | 4 | 3 | 12 | high | No reverse-dependency lookup. Common workflow ("what references X?") cannot be answered in one call. |
| 6 | R2 | Redundancy (parser duplication) | 3 | 4 | 6 | high | Three independent copies of the JSON-property-write helper. Mostly maintenance debt; modest LLM-ambiguity impact. |
| 7 | G5 | Capability Gap (batch ops) | 3 | 3 | 9 | high | No bulk move/delete/rename. Common bulk workflows require many round-trips. |
| 8 | A4 | Ambiguity (disambiguation) | 3 | 1 | 15 | medium | `asset-set-import-settings` vs `object-modify` lacks "use this when X, not Y" guidance. Trivial doc patch. |
| 9 | G9 | Code hygiene | 2 | 1 | 10 | high | Dead `using` directives in `Tool_Asset.Create.cs`. Trivial cleanup. |
| 10 | A3 | Ambiguity | 2 | 1 | 10 | medium | `asset-find` filter description doesn't enumerate prefix meanings. Quick description expand. |
| 11 | A5 / D4 | Ambiguity (consequences) | 2 | 1 | 10 | medium | `asset-refresh forceUpdate=true` and `asset-delete moveToTrash=false` consequences underdocumented. |
| 12 | G2 | Capability Gap (catalog coherence) | 2 | 2 | 8 | high | `asset-create` excludes ScriptableObject. Either add the case or sharpen the description to point users at `scriptableobject-create`. |
| 13 | G6 | Capability Gap | 2 | 1 | 10 | medium | No cheap `asset-exists` predicate. Nice-to-have. |
| 14 | G7 | Capability Gap (sprite slicing) | 2 | 4 | 4 | medium | Sprite rect array unreachable via current import-settings parser. May already be covered by Texture domain — confirm before scoping. |
| 15 | G3 | Capability Gap (AssetBundles) | 1 | 3 | 3 | medium | No AssetBundle name assignment. Likely deprioritized given Addressables. |
| 16 | A6 / A7 | Ambiguity (minor) | 1 | 1 | 5 | low | `asset-create-folder` idempotency + `asset-rename` uniqueness — minor description tweaks. |
| 17 | D2 / D3 | Defaults (cosmetic) | 1 | 1 | 5 | low | `asset-find` default folder/maxResults clarifications. |

Priority formula: `Impact × (6 - Effort)`. Top of the list combines high-impact tools with low-cost fixes. Items 1-5 are the highest leverage.

---

## 7. Notes

**Pipeline alignment:**
- The Asset domain is in **Tier 1** per `CLAUDE.md`, so this audit feeds directly into the consolidation pipeline.
- Several findings (R1, R2, G1, G8) overlap with cross-domain decisions: the consolidation-planner will need to weigh "fold `asset-create` into per-type creators" vs "make `asset-create` the dispatcher and deprecate per-type creators". I recommend deferring that strategic call to the planner — this audit just flags the redundancy and lets the human pick the direction.

**Things I deliberately did NOT do:**
- I did not fully read `Editor/Tools/Texture/Tool_Texture.Configure.cs`, `Tool_Texture.Inspect.cs`, `Tool_Texture.ApplyPattern.cs`, or `Editor/Tools/Object/Tool_Object.Modify.cs` end-to-end. I only opened the first 40 lines of two of these to confirm overlap pattern. Full coverage of cross-domain duplication is out of scope for an Asset-domain audit; flag G7 and R2 specifically for the planner to verify against those files.
- I did not attempt to trace whether `Editor/Tools/Meta/Tool_Meta.*.cs` covers any of the .meta-file workflows that overlap with import settings — Grep showed Meta tools exist but their relevance to the Asset domain wasn't load-bearing for this audit.

**Open questions for Ramon:**
1. Is `asset-create` intended to remain a general dispatcher or should it be replaced entirely by per-type creators? This drives whether R1 is "consolidate by absorbing" or "consolidate by deletion".
2. Is AssetBundle support (G3) still in scope for v1.x, or has it been deprecated in favour of Addressables? If the latter, demote G3 to "won't fix".
3. Should batch operations (G5) be domain-local or routed through `BatchExecute`? Decision affects the planner's signature design.
4. The `propertiesJson` parameter on `asset-create` (A2 / D5) — was this intentionally wired through but still being designed, or is the description outdated? If the former, document the intended scope; if the latter, fix the description.
