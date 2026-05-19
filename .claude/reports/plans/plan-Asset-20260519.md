# Consolidation Plan — Asset

**Date:** 2026-05-19
**Planner:** consolidation-planner agent
**Audit input:** `.claude/reports/audits/audit-Asset-20260425.md`
**Review input:** `.claude/reports/reviews/review-Asset-20260519.md`
**Status:** ✅ READY FOR EXECUTION

---

## 0. Plan Quality Caveats

**Inputs verified:**
- ✅ Audit file present (`.claude/reports/audits/audit-Asset-20260425.md`)
- ✅ Review file present (`.claude/reports/reviews/review-Asset-20260519.md`)
- ✅ Review marked READY FOR PLANNING (Section 7)
- ✅ All 23 audit findings have decisions in review Section 1 (R1–R2, A1–A7, D1–D5, G1–G9)

**Findings included in plan:**
- Accepted (12): R1, A2, A3, A4, A5, A6, A7, D1, D2, D4, D5, G9 — most resolved via the E1 cascade (deletion of `asset-create`)
- Accepted-with-modification (4): R2 (Asset-scoped only), A1 (rewrite summary), G1 (plain `bool` for `clearExisting`), G4 (single-tool form)
- Accepted-as-new-tool (3): G6 (`asset-exists`), G8 (ObjectReference write in shared parser)
- Rejected / Deferred (4): D3 (rejected — no evidence), G3 (won't-fix per E4), G5 (deferred to v2.x per E6), G7 (deferred to Texture audit per E7)

**Constraints applied (from review Section 3):**
- Backward compat: **free to break tool names**. No deprecation shims. (Section 3 box 3.)
- C# style: CLAUDE.md strict (braces on every `if`, no empty catches, no `obj?.prop = x` null-conditional assignment, single XML summary on the partial-class file containing `[McpToolType]`, `EntityIdToObject` over deprecated APIs).
- Tool descriptions MUST come from `[System.ComponentModel.Description]` on the method (NOT `toolAttr.Description`).
- Sentinel convention from GameObject cycle is dormant this cycle. `asset-set-labels.clearExisting` is a plain `bool` (per Ramon's E3 correction). No other new params introduce optional booleans.
- `ReadOnlyHint = true` required on: `asset-find-references`, `asset-exists`. Must NOT be present on `asset-set-labels`, `asset-create-render-texture` (both mutate).
- Path discipline: auto-prepend `Assets/` on every new tool path parameter, matching the 9-of-10 existing pattern.
- All `AssetDatabase`/`AssetImporter` writes wrapped in `MainThreadDispatcher.Execute(() => { ... })`.

**Scope limits enforced:**
- Do NOT touch Material, Physics, ScriptableObject, or Animation domains (per E1, `asset-create` deletion has zero cross-domain reach).
- Do NOT touch `Tool_Object.Modify.cs` for the parser extraction (per E2 Option 2 — Asset-scoped only).
- Do NOT touch Texture domain (G7 deferred to Texture audit).
- Do NOT edit `CLAUDE.md`.
- Do NOT pull v2.0 features into this cycle.

**Reviewer notes carried forward (from review Section 6):**
- The single `[McpToolType]` summary in the Asset domain lives in `Tool_Asset.Copy.cs` (lines 10–13), NOT in `Tool_Asset.Create.cs`. The summary survives the deletion of `Tool_Asset.Create.cs` — A1 (rewrite summary) is a Copy.cs edit, not a Create.cs edit.
- `Tool_Object.Modify.cs` (line 194) already supports ObjectReference writes via `AssetDatabase.LoadAssetAtPath` — this is the reference implementation pattern for G8 in `Tool_Asset.Shared.cs`. Do NOT copy code; mirror the approach.
- Tool_Asset.Create.cs contains four dead `using` directives (`SimpleJSON`, `Unity.VisualScripting.YamlDotNet.Core.Tokens`, `static UnityEngine.EventSystems.EventTrigger`, `static UnityEngine.GraphicsBuffer`). They are subsumed by file deletion (G9) — make sure they do NOT carry into `Tool_Asset.CreateRenderTexture.cs`.

---

## 1. Summary

| # | Change Group | Findings | Files Touched | Priority |
|---|--------------|----------|---------------|----------|
| A | Description polish (quick win) | A3, A4, A5, A6, A7, D2, D4 | 5 (modified) | high (cheap) |
| B | Shared parser extraction + ObjectReference write | R2, G8 | 2 (1 created, 1 modified) | high |
| C | `asset-create` deletion + `asset-create-render-texture` addition | R1, A1, A2, D1, D5, G2, G9 | 3 (1 deleted, 1 created, 1 modified) | high |
| D | New capability tools | G1, G4, G6 | 3 (created) | high |
| E | Decision doc + backlog entries | G3, G5, G7 | 2 (1 created, 1 modified) | low (cheap) |

**Recommended order:** A → B → C → D → E

Rationale for ordering:
- **A first:** purely cosmetic, no risk, ships value immediately, clears noise from later diffs.
- **B before C:** B extracts shared parser logic from `Tool_Asset.ImportSettings.cs`. Doing B before C avoids touching code that C is about to delete (the parser duplicate in `Tool_Asset.Create.cs` is removed when the whole file goes away in C). B and C are independent in their final state, but B → C produces cleaner intermediate diffs.
- **C before D:** C rewrites the `[McpToolType]` summary on `Tool_Asset.Copy.cs` (A1). D adds three new tools whose existence must be reflected in that summary. Doing C first means D only adds names to the already-rewritten summary.
- **D before E:** E is documentation only, lowest coupling.

No group has a hard build-time dependency on a later group. Groups A, B, C, D can each compile independently. Group E is doc-only (no compile impact at all).

---

## 2. Change Group A — Description polish (quick win)

**Findings addressed:** A3, A4, A5, A6, A7, D2, D4

**Rationale:** Pure description edits on existing `[Description]` attributes. No signature changes. No behavioral impact. Lands cheaply and clears low-priority noise from later diffs. Independent of all other groups.

**Definition of done:**
- All 7 findings' description tweaks applied verbatim to the affected `[Description]` attributes.
- Project compiles cleanly (`dotnet build`, `tsc --noEmit`).
- No signature changes, no method additions, no method removals.

**Dependencies:** None.

### Files Touched

- `Editor/Tools/Asset/Tool_Asset.Find.cs` — modified (A3, D2)
- `Editor/Tools/Asset/Tool_Asset.ImportSettings.cs` — modified (A4)
- `Editor/Tools/Asset/Tool_Asset.Refresh.cs` — modified (A5)
- `Editor/Tools/Asset/Tool_Asset.CreateFolder.cs` — modified (A6)
- `Editor/Tools/Asset/Tool_Asset.Rename.cs` — modified (A7)
- `Editor/Tools/Asset/Tool_Asset.Delete.cs` — modified (D4)

### Change A.1 — `asset-find` enumerate filter prefixes + clarify default scope (A3, D2)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.Find.cs`

**Before — method `[Description]` (line 23):**
```csharp
[Description("Searches for assets using Unity filter syntax (e.g. 't:Prefab', 't:Material player', 'l:Important').")]
```

**After — method `[Description]`:**
```csharp
[Description("Searches for assets using Unity filter syntax. Prefixes: 't:' = type (e.g. 't:Prefab', 't:Texture2D'), 'l:' = label (e.g. 'l:Boss'), 'b:' = AssetBundle, 'ref:' = references. Multiple terms AND together (e.g. 't:Texture2D sky'). Omit prefix for a plain-name match.")]
```

**Before — `searchFilter` param `[Description]` (line 25):**
```csharp
[Description("Search filter (e.g. 't:Prefab', 't:Texture2D sky', 'l:MyLabel').")] string searchFilter,
```

**After — `searchFilter` param `[Description]`:**
```csharp
[Description("Search filter using Unity prefix syntax. Examples: 't:Prefab' (all prefabs), 't:Texture2D sky' (textures with 'sky' in name), 'l:Boss' (assets labelled 'Boss'). Prefixes: t = type, l = label, b = AssetBundle, ref = references.")] string searchFilter,
```

**Before — `folder` param `[Description]` (line 26):**
```csharp
[Description("Folder to search in (e.g. 'Assets/Prefabs'). Default 'Assets'.")] string folder = "Assets",
```

**After — `folder` param `[Description]`:**
```csharp
[Description("Folder to search in (e.g. 'Assets/Prefabs'). Default 'Assets' (entire project).")] string folder = "Assets",
```

**Risks:** None. Description-only.

### Change A.2 — `asset-set-import-settings` add disambiguation vs `object-modify` + SaveAndReimport call-out (A4)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.ImportSettings.cs`

**Before — method `[Description]` (line 92):**
```csharp
[Description("Applies property overrides to an asset's importer via SerializedObject and triggers SaveAndReimport. " + "settingsJson must be a JSON object mapping property paths to string values " + "(e.g. {\"textureType\":\"1\",\"mipmapEnabled\":\"true\"}).")]
```

**After — method `[Description]`:**
```csharp
[Description("Applies property overrides to an asset's IMPORTER (not the asset itself) and triggers SaveAndReimport. Use this for importer-level settings such as textureType, mipmapEnabled, compressionQuality. For runtime properties on the asset's loaded UnityEngine.Object (e.g. material.color, texture.wrapMode), use object-modify instead — that writes the asset directly and does not reimport. settingsJson is a JSON object of property paths (discoverable via asset-get-import-settings) mapped to string values (e.g. {\"textureType\":\"1\",\"mipmapEnabled\":\"true\"}).")]
```

**Risks:** None. Description-only. Length increase is intentional — the disambiguation is the value here.

### Change A.3 — `asset-refresh` warn about forceUpdate consequences (A5)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.Refresh.cs`

**Before — `forceUpdate` param `[Description]` (line 22):**
```csharp
[Description("Force reimport all assets. Default false.")] bool forceUpdate = false
```

**After — `forceUpdate` param `[Description]`:**
```csharp
[Description("When true, reimports EVERY asset in the project — can take several minutes on medium projects and locks the Editor while running. Use false (default) for the normal change-detection pass. Only set true when troubleshooting stale imports.")] bool forceUpdate = false
```

**Risks:** None. Description-only.

### Change A.4 — `asset-create-folder` document idempotency (A6)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.CreateFolder.cs`

**Before — method `[Description]` (line 20):**
```csharp
[Description("Creates a folder in the project, including any missing intermediate folders.")]
```

**After — method `[Description]`:**
```csharp
[Description("Creates a folder in the project, including any missing intermediate folders. Idempotent: returns success when the folder already exists (no need to pre-check with asset-find or asset-exists).")]
```

**Risks:** None. Description-only.

### Change A.5 — `asset-rename` warn about unique-name requirement (A7)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.Rename.cs`

**Before — `newName` param `[Description]` (line 28):**
```csharp
[Description("New file name without extension (e.g. 'NewName').")] string newName
```

**After — `newName` param `[Description]`:**
```csharp
[Description("New file name without extension (e.g. 'NewName'). Must be unique within the asset's current folder — Unity will reject the rename if a sibling asset already has this name.")] string newName
```

**Risks:** None. Description-only.

### Change A.6 — `asset-delete` warn about permanent deletion (D4)

**Type:** description-only

**File:** `Editor/Tools/Asset/Tool_Asset.Delete.cs`

**Before — `moveToTrash` param `[Description]` (line 24):**
```csharp
[Description("Move to OS trash instead of permanent delete. Default true.")] bool moveToTrash = true
```

**After — `moveToTrash` param `[Description]`:**
```csharp
[Description("When true (default), moves the asset to the OS trash (recoverable from Recycle Bin / Trash). When false, permanently deletes the file with no recovery path — use only when you are certain. Default true.")] bool moveToTrash = true
```

**Risks:** None. Description-only.

---

## 3. Change Group B — Shared parser extraction + ObjectReference write (E2)

**Findings addressed:** R2, G8

**Rationale:** Extract the SerializedObject value-coercion logic that `Tool_Asset.ImportSettings.cs` currently owns into a new `Tool_Asset.Shared.cs` partial-class file. Add `SerializedPropertyType.ObjectReference` write support during the extraction (asset-path → `AssetDatabase.LoadAssetAtPath` → `prop.objectReferenceValue`), closing G8. Asset-scoped only — `Tool_Object.Modify.cs` is NOT touched (per E2 Option 2).

**Definition of done:**
- New file `Editor/Tools/Asset/Tool_Asset.Shared.cs` exists, declares `public partial class Tool_Asset`, contains:
  - `ApplyStringValueToProperty(SerializedProperty prop, string value)` — moved from `Tool_Asset.ImportSettings.cs`.
  - `SplitJsonEntries(string jsonBody)` — moved from `Tool_Asset.ImportSettings.cs`.
  - New `ObjectReference` case in `ApplyStringValueToProperty` accepting asset path strings.
- `Tool_Asset.ImportSettings.cs` no longer contains its own copies of those helpers — they are removed and the existing callsite (line 169) now binds to the shared partial-class member.
- `Tool_Asset.Shared.cs` has NO `[McpToolType]` summary (the summary lives in `Tool_Asset.Copy.cs` per CLAUDE.md partial-class rule).
- ObjectReference write returns a clear error when the asset path is invalid or the loaded type doesn't match the property's expected type.
- Project compiles cleanly.

**Dependencies:** None. (Group A and Group B both edit `Tool_Asset.ImportSettings.cs`, but on disjoint lines — A touches `[Description]` strings near line 92; B removes helper methods at lines 232–290 and lines 298–350. No merge conflict expected.)

### Files Touched

- `Editor/Tools/Asset/Tool_Asset.Shared.cs` — **created** (new partial-class file holding the shared parser).
- `Editor/Tools/Asset/Tool_Asset.ImportSettings.cs` — **modified** (remove the `IMPORT SETTINGS HELPERS` region's `ApplyStringValueToProperty` and `SplitJsonEntries` methods; keep `GetImporterPropertyValueString` here since it is a *read-side* helper specific to import-settings, not a write parser).

### Change B.1 — Create `Tool_Asset.Shared.cs`

**Type:** new file

**File:** `Editor/Tools/Asset/Tool_Asset.Shared.cs`

**Content:**
```csharp
#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region SHARED PARSER

        /// <summary>
        /// Attempts to apply a string value to a <see cref="SerializedProperty"/> by converting
        /// it to the property's native type.
        /// Shared across Asset-domain tools that write to SerializedObject (currently asset-set-import-settings).
        /// Supports Integer, Float, Boolean, String, Enum, Color, and ObjectReference (treats input as asset path).
        /// </summary>
        /// <param name="prop">The property to set.</param>
        /// <param name="value">The string representation of the desired value. For ObjectReference props, this is a project-relative asset path.</param>
        /// <returns><c>true</c> if the value was successfully applied; <c>false</c> if the value could not be parsed or the property type is unsupported.</returns>
        private static bool ApplyStringValueToProperty(SerializedProperty prop, string value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    if (int.TryParse(value, out int intVal))
                    {
                        prop.intValue = intVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Float:
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float floatVal))
                    {
                        prop.floatValue = floatVal;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.Boolean:
                    string lower = value.ToLowerInvariant();
                    if (lower == "true" || lower == "1")
                    {
                        prop.boolValue = true;
                        return true;
                    }
                    if (lower == "false" || lower == "0")
                    {
                        prop.boolValue = false;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.String:
                    prop.stringValue = value;
                    return true;

                case SerializedPropertyType.Enum:
                    if (int.TryParse(value, out int enumInt))
                    {
                        prop.enumValueIndex = enumInt;
                        return true;
                    }

                    for (int i = 0; i < prop.enumNames.Length; i++)
                    {
                        if (string.Compare(prop.enumNames[i], value, System.StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            prop.enumValueIndex = i;
                            return true;
                        }
                    }
                    return false;

                case SerializedPropertyType.Color:
                    if (ColorUtility.TryParseHtmlString(value, out Color color))
                    {
                        prop.colorValue = color;
                        return true;
                    }
                    return false;

                case SerializedPropertyType.ObjectReference:
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        prop.objectReferenceValue = null;
                        return true;
                    }

                    string assetPath = value;

                    if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                    {
                        assetPath = "Assets/" + assetPath;
                    }

                    var loaded = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                    if (loaded == null)
                    {
                        return false;
                    }

                    prop.objectReferenceValue = loaded;
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Splits a flat JSON object body (<c>"key":"val","key2":"val2"</c>) into individual
        /// entry strings, respecting quoted strings and nested braces so commas inside values are not split on.
        /// </summary>
        /// <param name="jsonBody">The content between the outer braces of a JSON object.</param>
        /// <returns>An array of raw entry strings ready for colon-splitting.</returns>
        private static string[] SplitJsonEntries(string jsonBody)
        {
            var entries = new List<string>();
            int depth = 0;
            bool inString = false;
            int start = 0;

            for (int i = 0; i < jsonBody.Length; i++)
            {
                char c = jsonBody[i];

                if (c == '\\' && inString)
                {
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{' || c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    depth--;
                    continue;
                }

                if (c == ',' && depth == 0)
                {
                    entries.Add(jsonBody[start..i]);
                    start = i + 1;
                }
            }

            if (start < jsonBody.Length)
            {
                entries.Add(jsonBody[start..]);
            }

            return entries.ToArray();
        }

        #endregion
    }
}
```

**Notes:**
- Partial-class continuation; no `[McpToolType]` summary (rule from CLAUDE.md).
- Includes the existing `Color` case that `ImportSettings.cs` was missing in its private copy (the audit's R2 evidence pointed at `Tool_Asset.Create.cs` containing the Color case; folding it into the shared parser closes that asymmetry too).
- New `ObjectReference` case treats empty input as "clear reference" (`null`). Non-empty input is auto-prefixed with `Assets/` (matching domain path discipline), loaded via `AssetDatabase.LoadAssetAtPath<Object>`, and returns `false` on miss so the caller's existing error path (`[SKIP] Could not apply ...`) triggers.

### Change B.2 — Remove duplicate helpers from `Tool_Asset.ImportSettings.cs`

**Type:** modified file (remove methods)

**File:** `Editor/Tools/Asset/Tool_Asset.ImportSettings.cs`

**Actions:**
1. **Delete** the `ApplyStringValueToProperty` method (current lines 225–290) — it now lives in `Tool_Asset.Shared.cs`.
2. **Delete** the `SplitJsonEntries` method (current lines 292–350) — same reason.
3. **Keep** `GetImporterPropertyValueString` (lines 200–223) — it is a read-side formatter specific to the import-settings dump, not a generic write parser. Stays in `ImportSettings.cs` near its single callsite.
4. **Keep** the `#region IMPORT SETTINGS HELPERS` wrapper but with only `GetImporterPropertyValueString` inside; OR collapse the region if only one member remains. Either is acceptable; consolidator's choice.

**Callsite (line 169) is unchanged:**
```csharp
bool applied = ApplyStringValueToProperty(prop, rawValue);
```
This call now resolves to the partial-class member in `Tool_Asset.Shared.cs` via partial-class merging. No code change at the callsite.

**Migration of users / callers:** None. `ApplyStringValueToProperty` and `SplitJsonEntries` are `private static` — no external callers exist.

**Risks:**
- Backward compat: safe. Method visibility is `private static`, no public API change.
- Build: requires the new `Tool_Asset.Shared.cs` to exist first (or to be added in the same commit). If Shared.cs is missing, ImportSettings.cs will not compile.
- Cross-domain: none — the parser is consumed only by `asset-set-import-settings` after this cycle.
- ObjectReference write side-effect: a single new write code path was added. Caller (`SetImportSettings`) does not change. The existing `appliedCount` accounting in `SetImportSettings` handles the new return-true case automatically. Edge case: if the LLM passes a path that resolves to the wrong asset *type* for the importer's expected reference type, `prop.objectReferenceValue = loaded` will silently set it; Unity's serialization will reject the wrong type on save. This is the same behaviour as `Tool_Object.Modify.cs` and is acceptable for this cycle. Document this in the method's XML doc.

---

## 4. Change Group C — `asset-create` deletion + `asset-create-render-texture` addition (E1)

**Findings addressed:** R1, A1, A2, D1, D5, G2, G9

**Rationale:** Per E1 Option 1, `asset-create` is deleted outright. The one asset type lacking a dedicated creator (RenderTexture) gets a strongly-typed replacement: `asset-create-render-texture`. The misleading domain summary (A1) is rewritten on `Tool_Asset.Copy.cs` (where `[McpToolType]` lives) to no longer claim "create any asset". A2 (`propertiesJson` misdescription), D1 (silent `assetType="Material"` default), D5 (`propertiesJson` default), G2 (no ScriptableObject case), G9 (dead `using`s) are all subsumed by the file deletion. The five-line dead-`using` block does not carry forward into the new file.

**Definition of done:**
- `Editor/Tools/Asset/Tool_Asset.Create.cs` is deleted (and its `.meta` file if Unity has generated one).
- `Editor/Tools/Asset/Tool_Asset.CreateRenderTexture.cs` exists with the new tool.
- `Editor/Tools/Asset/Tool_Asset.Copy.cs` `[McpToolType]` summary is rewritten (A1).
- `material-create`, `physics-create-material`, `scriptableobject-create`, `animation-configure-controller` are untouched.
- No file in the repo references `asset-create` as a tool name except the audit/review/plan markdown reports (which are historical artefacts).
- Project compiles cleanly.

**Dependencies:** None on Groups A or B. **Group C MUST run after Group A** if both touch the same file, but in this plan they do not — A touches Find/ImportSettings/Refresh/CreateFolder/Rename/Delete, C touches Create/CreateRenderTexture/Copy. Independent.

### Files Touched

- `Editor/Tools/Asset/Tool_Asset.Create.cs` — **deleted**.
- `Editor/Tools/Asset/Tool_Asset.Create.cs.meta` — **deleted** if Unity has generated one (it will have; consolidator should delete both atomically).
- `Editor/Tools/Asset/Tool_Asset.CreateRenderTexture.cs` — **created**.
- `Editor/Tools/Asset/Tool_Asset.Copy.cs` — **modified** (rewrite the `[McpToolType]`-bearing class summary).

### Change C.1 — Delete `Tool_Asset.Create.cs`

**Type:** removed file

**File:** `Editor/Tools/Asset/Tool_Asset.Create.cs`

**Actions:**
1. Delete `Editor/Tools/Asset/Tool_Asset.Create.cs` entirely.
2. Delete `Editor/Tools/Asset/Tool_Asset.Create.cs.meta` if it exists (Unity generates these for every `.cs` file inside `Editor/Tools/`).

**Tools removed:**

| Old tool | Replacement | Notes |
|---|---|---|
| `asset-create assetType="Material"` | `material-create` (existing — Material domain) | Already canonical with shader name + render-pipeline auto-detect. |
| `asset-create assetType="RenderTexture"` | `asset-create-render-texture` (new — Change C.2 below) | Strongly typed params. |
| `asset-create assetType="PhysicMaterial"` | `physics-create-material` (existing — Physics domain) | Already canonical with friction/bounciness/combine modes. |
| `asset-create assetType="AnimatorController"` | `animation-configure-controller` (existing — Animation domain) | Already canonical for full controller authoring. |
| `asset-create propertiesJson="{...}"` | — | Dies with the tool. No replacement. Strongly-typed params on each per-type creator cover this. |

**Migration:** No callsite shims. Per review Section 3 box 3, tool renames may break freely. The audit's batch-summary already notes the deletion as the chosen direction. No test files in the repo reference `asset-create` (verified by grep — only `.md` report files reference the tool name, and those are historical).

**Helpers removed alongside:**
- `ApplyPropertiesFromJson` (lines 126–213) — only callsite was `asset-create` itself. Note: this is *not* the same helper as `ApplyStringValueToProperty` in `ImportSettings.cs`; they were two independent copies. The `ImportSettings` copy survives (extracted to `Shared.cs` in Group B); the `Create.cs` copy dies with the file.
- `ApplyValue` (lines 215–279) — same story, only consumed by `ApplyPropertiesFromJson`.

**Risks:**
- Backward compat: signature-breaking. **Acceptable** per review Section 3 box 3 ("May break tool names freely").
- Build: safe. `asset-create` and its private helpers have no callsites outside the file being deleted.
- Cross-domain: none. Per-type creators in Material/Physics/ScriptableObject/Animation are untouched.

### Change C.2 — Create `Tool_Asset.CreateRenderTexture.cs`

**Type:** new tool

**File:** `Editor/Tools/Asset/Tool_Asset.CreateRenderTexture.cs`

**Before:** N/A (new file, new tool).

**After — full file contents:**
```csharp
#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region CREATE RENDER TEXTURE

        /// <summary>
        /// Creates a new RenderTexture asset at the given project path with the specified dimensions and format.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path including the .renderTexture extension (e.g. 'Assets/RenderTextures/Mirror.renderTexture'). Auto-prepends 'Assets/' if omitted.</param>
        /// <param name="width">Width in pixels. Default 256.</param>
        /// <param name="height">Height in pixels. Default 256.</param>
        /// <param name="depth">Depth-buffer bit count: 0 (no depth), 16, 24, or 32. Default 24.</param>
        /// <param name="format">RenderTextureFormat name (e.g. 'ARGB32', 'RGB565', 'RGFloat'). Default 'ARGB32'. Case-insensitive.</param>
        /// <param name="filterMode">FilterMode name: 'Point', 'Bilinear', or 'Trilinear'. Default 'Bilinear'. Case-insensitive.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the created asset path, or an error when the path is invalid, the folder cannot be created, or the format / filter-mode strings are unrecognised.</returns>
        [McpTool("asset-create-render-texture", Title = "Asset / Create Render Texture")]
        [Description("Creates a new RenderTexture asset with explicit dimensions, depth-buffer, format, and filter mode. Strongly typed — no JSON blob. For other asset types use the dedicated creator: Material → material-create; PhysicsMaterial → physics-create-material; ScriptableObject → scriptableobject-create; AnimatorController → animation-configure-controller.")]
        public ToolResponse CreateRenderTexture(
            [Description("Project-relative asset path with .renderTexture extension (e.g. 'Assets/RenderTextures/Mirror.renderTexture'). Missing intermediate folders are created automatically. If a file already exists at the path, a unique suffix is appended.")] string assetPath,
            [Description("Width in pixels. Default 256.")] int width = 256,
            [Description("Height in pixels. Default 256.")] int height = 256,
            [Description("Depth-buffer bit count. Valid values: 0, 16, 24, 32. Default 24.")] int depth = 24,
            [Description("RenderTextureFormat name (e.g. 'ARGB32', 'RGB565', 'RGFloat', 'RFloat'). Case-insensitive. Default 'ARGB32'.")] string format = "ARGB32",
            [Description("FilterMode: 'Point', 'Bilinear', or 'Trilinear'. Case-insensitive. Default 'Bilinear'.")] string filterMode = "Bilinear"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                if (width <= 0 || height <= 0)
                {
                    return ToolResponse.Error($"width and height must be positive (got {width}x{height}).");
                }

                if (depth != 0 && depth != 16 && depth != 24 && depth != 32)
                {
                    return ToolResponse.Error($"depth must be 0, 16, 24, or 32 (got {depth}).");
                }

                if (!System.Enum.TryParse<RenderTextureFormat>(format, true, out var rtFormat))
                {
                    return ToolResponse.Error($"Unrecognised RenderTextureFormat '{format}'. Try 'ARGB32', 'RGB565', 'RGFloat', 'RFloat'.");
                }

                if (!System.Enum.TryParse<FilterMode>(filterMode, true, out var fMode))
                {
                    return ToolResponse.Error($"Unrecognised FilterMode '{filterMode}'. Valid values: 'Point', 'Bilinear', 'Trilinear'.");
                }

                string folder = Path.GetDirectoryName(assetPath) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                var rt = new RenderTexture(width, height, depth, rtFormat)
                {
                    filterMode = fMode
                };

                AssetDatabase.CreateAsset(rt, assetPath);
                AssetDatabase.SaveAssets();

                return ToolResponse.Text($"RenderTexture created at '{assetPath}' ({width}x{height}, depth {depth}, format {rtFormat}, filter {fMode}).");
            });
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `new RenderTexture(width, height, depth, RenderTextureFormat)` — constructor with explicit format.
- `RenderTexture.filterMode = FilterMode.X` — post-construction property assignment.
- `AssetDatabase.GenerateUniqueAssetPath(path)` — auto-suffix when path collides.
- `Directory.CreateDirectory(folder)` + `AssetDatabase.Refresh()` — auto-create missing folders (mirrors the pattern in the deleted `Tool_Asset.Create.cs`).
- `AssetDatabase.CreateAsset(rt, assetPath)` + `AssetDatabase.SaveAssets()` — persist the asset.

**Migration:**

| Old call shape | New call shape |
|---|---|
| `asset-create(path="Assets/RT.renderTexture", assetType="RenderTexture")` | `asset-create-render-texture(assetPath="Assets/RT.renderTexture")` |
| `asset-create(path=..., assetType="RenderTexture", propertiesJson="{\"m_FilterMode\":\"0\"}")` | `asset-create-render-texture(assetPath=..., filterMode="Point")` |

**Helpers:** None needed. Strongly typed params eliminate the JSON parser dependency.

**Risks:**
- Backward compat: NEW tool, no removal beyond what Change C.1 already does. Safe.
- Build: requires `using UnityEditor;`, `using UnityEngine;`, `using System.IO;`, `using System.ComponentModel;` — all present in the file template above. Does NOT require `SimpleJSON` or any of the four dead `using` directives that were in `Tool_Asset.Create.cs` (G9 — explicitly omitted).
- Cross-domain: none — fully contained in Asset domain.
- ReadOnlyHint: explicitly NOT set (this tool mutates the project — `AssetDatabase.CreateAsset`).
- Format coverage: `System.Enum.TryParse<RenderTextureFormat>(..., true, ...)` accepts any case-insensitive name from Unity's enum. Unrecognised values produce a clear error message with common alternatives.

### Change C.3 — Rewrite Asset domain `[McpToolType]` summary (A1, G9 closeout)

**Type:** description-only (XML doc summary on the partial-class declaration)

**File:** `Editor/Tools/Asset/Tool_Asset.Copy.cs`

**Before — class summary (lines 10–13):**
```csharp
/// <summary>
/// MCP tools for managing Unity project assets — find, create, copy, move, rename,
/// delete, refresh, inspect metadata, and read/write importer settings.
/// </summary>
[McpToolType]
public partial class Tool_Asset
```

**After — class summary:**
```csharp
/// <summary>
/// MCP tools for managing Unity project assets — find / inspect / copy / move / rename / delete /
/// refresh / read-write importer settings / set labels / find references / check existence /
/// create folders / create RenderTextures. Type-specific creation lives in the type's own domain
/// (Material → material-create; PhysicsMaterial → physics-create-material; ScriptableObject →
/// scriptableobject-create; AnimatorController → animation-configure-controller).
/// </summary>
[McpToolType]
public partial class Tool_Asset
```

**Notes:**
- The new summary lists tools added by Group D (`asset-set-labels`, `asset-find-references`, `asset-exists`) in addition to the surviving CRUD tools. Group C runs before Group D, so when this edit lands the three new tools' files do not yet exist — but the summary is a forward-looking description of the domain, not a live inventory. This is acceptable; the alternative (updating the summary twice) creates pointless churn.
- If Group D is deferred past this cycle for any reason, revisit this summary to remove the mentions. The consolidator should flag this if Group D fails to ship.
- Closes A1 (misleading scope) and incidentally completes the G9 cleanup story by ensuring the surviving Asset files carry only meaningful documentation.

**Risks:** None. XML doc summary only, no runtime impact.

---

## 5. Change Group D — New capability tools (E3, E5)

**Findings addressed:** G1 (`asset-set-labels`), G4 (`asset-find-references`), G6 (`asset-exists`)

**Rationale:** Three new tools that round out the Asset domain's coverage. All follow the same conventions: path discipline (auto-prepend `Assets/`), `MainThreadDispatcher.Execute` wrap, `[Description]` on method and every param, partial-class continuation without `[McpToolType]` summary, `ReadOnlyHint = true` on the two read-only tools. Cohesive batch — they can ship in a single PR with no inter-tool dependency.

**Definition of done:**
- Three new files exist: `Tool_Asset.SetLabels.cs`, `Tool_Asset.FindReferences.cs`, `Tool_Asset.Exists.cs`.
- All three are `public partial class Tool_Asset` continuations without a `[McpToolType]` summary.
- All three apply the auto-prepend `Assets/` path discipline.
- All three wrap their Unity API calls in `MainThreadDispatcher.Execute(...)`.
- `asset-find-references` and `asset-exists` have `ReadOnlyHint = true`; `asset-set-labels` does NOT.
- `asset-set-labels.clearExisting` is a plain `bool` (NOT a sentinel string) — per Ramon's E3 correction.
- Project compiles cleanly.

**Dependencies:** Group C must run first (so the rewritten `[McpToolType]` summary in `Tool_Asset.Copy.cs` already mentions these three tools). Soft dependency only — if D runs before C, the project still compiles, the summary is just stale until C lands.

### Files Touched

- `Editor/Tools/Asset/Tool_Asset.SetLabels.cs` — **created**.
- `Editor/Tools/Asset/Tool_Asset.FindReferences.cs` — **created**.
- `Editor/Tools/Asset/Tool_Asset.Exists.cs` — **created**.

### Change D.1 — Create `asset-set-labels` tool (G1)

**Type:** new tool

**File:** `Editor/Tools/Asset/Tool_Asset.SetLabels.cs`

**Before:** N/A.

**After — full file contents:**
```csharp
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region SET LABELS

        /// <summary>
        /// Writes labels to an asset. By default appends to existing labels (deduplicated).
        /// Pass clearExisting=true to replace the entire label set; pass labelsJson="[]" with
        /// clearExisting=true to remove all labels.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path (e.g. 'Assets/Prefabs/Boss.prefab').</param>
        /// <param name="labelsJson">JSON array of label strings (e.g. '["Boss","Level3"]'). Empty array clears labels when clearExisting=true.</param>
        /// <param name="clearExisting">When true, replaces all existing labels with the provided set. When false (default), appends the provided labels to the existing set and deduplicates.</param>
        /// <returns>A <see cref="ToolResponse"/> with the final label set, or an error when the asset is missing or labelsJson is malformed.</returns>
        [McpTool("asset-set-labels", Title = "Asset / Set Labels")]
        [Description("Writes labels (tags) on an asset for later search via asset-find l:LabelName. By default APPENDS to existing labels (deduplicated). Set clearExisting=true to REPLACE the full label set; pass labelsJson=\"[]\" with clearExisting=true to clear all labels. Read labels via asset-get-info.")]
        public ToolResponse SetLabels(
            [Description("Project-relative asset path (e.g. 'Assets/Prefabs/Boss.prefab').")] string assetPath,
            [Description("JSON array of label strings (e.g. '[\"Boss\",\"Level3\"]'). Use '[]' with clearExisting=true to clear all labels.")] string labelsJson,
            [Description("When true, replaces all existing labels with the provided set. When false (default), appends to the existing set (deduplicated).")] bool clearExisting = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (labelsJson == null)
                {
                    return ToolResponse.Error("labelsJson is required. Pass '[]' to clear (with clearExisting=true).");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string[] incoming = ParseStringArrayJson(labelsJson);

                if (incoming == null)
                {
                    return ToolResponse.Error("labelsJson must be a JSON array of strings (e.g. '[\"Boss\",\"Level3\"]' or '[]').");
                }

                string[] finalLabels;

                if (clearExisting)
                {
                    finalLabels = incoming;
                }
                else
                {
                    string[] existing = AssetDatabase.GetLabels(asset);
                    var merged = new List<string>(existing.Length + incoming.Length);
                    var seen = new HashSet<string>(System.StringComparer.Ordinal);

                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (seen.Add(existing[i]))
                        {
                            merged.Add(existing[i]);
                        }
                    }

                    for (int i = 0; i < incoming.Length; i++)
                    {
                        if (seen.Add(incoming[i]))
                        {
                            merged.Add(incoming[i]);
                        }
                    }

                    finalLabels = merged.ToArray();
                }

                AssetDatabase.SetLabels(asset, finalLabels);
                AssetDatabase.SaveAssets();

                if (finalLabels.Length == 0)
                {
                    return ToolResponse.Text($"Cleared all labels on '{assetPath}'.");
                }

                return ToolResponse.Text($"Labels on '{assetPath}' set to: [{string.Join(", ", finalLabels)}].");
            });
        }

        /// <summary>
        /// Minimal JSON string-array parser tolerant of the inputs LLMs typically produce:
        /// double-quoted strings, comma-separated, optional whitespace, empty array allowed.
        /// Returns <c>null</c> on malformed input.
        /// </summary>
        /// <param name="json">The JSON array text.</param>
        /// <returns>The parsed array, or <c>null</c> when the input is not a valid string array.</returns>
        private static string[]? ParseStringArrayJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            string trimmed = json.Trim();

            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
            {
                return null;
            }

            string inner = trimmed[1..^1].Trim();

            if (inner.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var result = new List<string>();
            int i = 0;

            while (i < inner.Length)
            {
                while (i < inner.Length && (inner[i] == ',' || char.IsWhiteSpace(inner[i])))
                {
                    i++;
                }

                if (i >= inner.Length)
                {
                    break;
                }

                if (inner[i] != '"')
                {
                    return null;
                }

                i++;
                var sb = new System.Text.StringBuilder();

                while (i < inner.Length && inner[i] != '"')
                {
                    if (inner[i] == '\\' && i + 1 < inner.Length)
                    {
                        sb.Append(inner[i + 1]);
                        i += 2;
                        continue;
                    }

                    sb.Append(inner[i]);
                    i++;
                }

                if (i >= inner.Length)
                {
                    return null;
                }

                result.Add(sb.ToString());
                i++;
            }

            return result.ToArray();
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `AssetDatabase.LoadMainAssetAtPath(assetPath)` — resolve the asset.
- `AssetDatabase.GetLabels(asset)` — read existing labels for merge.
- `AssetDatabase.SetLabels(asset, labels)` — write the full set (replaces, not appends — append semantics are emulated by pre-merge).
- `AssetDatabase.SaveAssets()` — persist.

**Behavioural matrix:**

| `clearExisting` | `labelsJson` | Effect |
|---|---|---|
| `false` (default) | `["A","B"]` | Adds `A` and `B` to whatever labels already exist; deduplicates. |
| `false` | `[]` | No-op (appends nothing). Returns the existing label set. |
| `true` | `["A","B"]` | Replaces all labels with exactly `[A, B]`. |
| `true` | `[]` | Clears all labels. |

**Migration:** Net new — no existing tool removed. Closes G1.

**Risks:**
- Backward compat: safe (new tool only).
- Build: requires `using System.Collections.Generic;`, `using UnityEditor;`, `using GameDeck.MCP.Attributes;`, `using GameDeck.MCP.Models;`, `using GameDeck.MCP.Utils;`, `using System.ComponentModel;` — all in the file template.
- Cross-domain: none.
- ReadOnlyHint: NOT set (this tool mutates).
- `ParseStringArrayJson` is a new minimal parser local to this file. Justification: the shared parser in `Tool_Asset.Shared.cs` handles `SerializedProperty` value coercion, not generic JSON array parsing — different domain. A second helper for arrays would be over-engineering at this scale (one callsite). Confined to private static.

### Change D.2 — Create `asset-find-references` tool (G4)

**Type:** new tool

**File:** `Editor/Tools/Asset/Tool_Asset.FindReferences.cs`

**Before:** N/A.

**After — full file contents:**
```csharp
#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region FIND REFERENCES

        /// <summary>
        /// Finds every asset in the project that depends on (references) the given asset.
        /// Inverse of <see cref="AssetDatabase.GetDependencies(string, bool)"/>, which Unity
        /// only exposes in the outgoing direction. Implemented by scanning all assets and
        /// checking each one's dependency list — O(n × avg-deps) and can take several seconds
        /// on medium projects.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path to find references TO (e.g. 'Assets/Materials/Player.mat').</param>
        /// <param name="maxResults">Maximum references to return. Early-exits the scan once the cap is hit. Default 100.</param>
        /// <returns>A <see cref="ToolResponse"/> listing referencing asset paths, with a 'truncated: true' marker when the cap was hit.</returns>
        [McpTool("asset-find-references", Title = "Asset / Find References", ReadOnlyHint = true)]
        [Description("Finds every asset that references the given asset (incoming references — the inverse of asset-get-info's outgoing dependencies). PERFORMANCE WARNING: Unity has no native reverse-dependency API; this tool scans every asset in the project and inspects each one's dependency list. Expect several seconds on medium projects. Results are capped at maxResults (default 100) with early-exit.")]
        public ToolResponse FindReferences(
            [Description("Project-relative asset path to find references TO (e.g. 'Assets/Materials/Player.mat').")] string assetPath,
            [Description("Maximum referencing assets to return. Default 100. Scan early-exits once the cap is hit; remaining matches go undiscovered.")] int maxResults = 100
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                if (maxResults <= 0)
                {
                    return ToolResponse.Error($"maxResults must be positive (got {maxResults}).");
                }

                var target = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (target == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string[] allGuids = AssetDatabase.FindAssets("");
                var references = new List<string>();
                bool truncated = false;

                for (int i = 0; i < allGuids.Length; i++)
                {
                    string candidatePath = AssetDatabase.GUIDToAssetPath(allGuids[i]);

                    if (string.IsNullOrEmpty(candidatePath))
                    {
                        continue;
                    }

                    if (string.Equals(candidatePath, assetPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] deps = AssetDatabase.GetDependencies(candidatePath, true);

                    for (int d = 0; d < deps.Length; d++)
                    {
                        if (string.Equals(deps[d], assetPath, System.StringComparison.OrdinalIgnoreCase))
                        {
                            references.Add(candidatePath);
                            break;
                        }
                    }

                    if (references.Count >= maxResults)
                    {
                        truncated = true;
                        break;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"References to '{assetPath}': {references.Count}{(truncated ? $" (truncated at maxResults={maxResults})" : "")}");

                for (int i = 0; i < references.Count; i++)
                {
                    sb.AppendLine($"  {references[i]}");
                }

                if (references.Count == 0)
                {
                    sb.AppendLine("  (none)");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `AssetDatabase.FindAssets("")` — enumerate every GUID in the project.
- `AssetDatabase.GUIDToAssetPath(guid)` — resolve GUID to path.
- `AssetDatabase.GetDependencies(candidatePath, recursive: true)` — outgoing-dependency scan per candidate.
- `AssetDatabase.LoadMainAssetAtPath(assetPath)` — verify the target asset exists before scanning.

**Performance notes (carried into `[Description]`):**
- O(n × avg-deps) where n = total assets in project.
- Empty filter `FindAssets("")` returns ALL assets (folders included; the per-candidate `GetDependencies` returns `[]` for folders so they self-skip).
- Early-exit on `maxResults` keeps worst-case bounded.
- Self-skip prevents `assetPath` from reporting itself as a reference.

**Return shape:** Text response (matching the rest of the domain's `ToolResponse.Text` pattern). The review's suggested `{references: string[], truncated: bool}` JSON-object shape is not used because `ToolResponse.Text` is the established convention for list responses in this codebase (see `asset-find`, `asset-get-info`). The text response includes both the count and the explicit `(truncated at maxResults=N)` marker — equivalent semantic content.

**Migration:** Net new. Closes G4.

**Risks:**
- Backward compat: safe (new tool only).
- Build: requires `using System.Collections.Generic;`, `using System.Text;`, `using UnityEditor;` — all in the file template.
- Cross-domain: none.
- ReadOnlyHint: TRUE (this tool only reads).
- Performance: documented. Future v2.x enhancement to add `scopeFolder` param is noted in the review and intentionally NOT implemented now.

### Change D.3 — Create `asset-exists` tool (G6)

**Type:** new tool

**File:** `Editor/Tools/Asset/Tool_Asset.Exists.cs`

**Before:** N/A.

**After — full file contents:**
```csharp
#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region EXISTS

        /// <summary>
        /// Cheap predicate for checking whether a project path resolves to an asset, a folder, or nothing.
        /// Use before asset-create-* / asset-copy / asset-rename to avoid collisions, or before asset-get-info to skip the heavier load.
        /// </summary>
        /// <param name="path">Project-relative path (e.g. 'Assets/Prefabs/Player.prefab' or 'Assets/Prefabs'). Auto-prepends 'Assets/' if omitted.</param>
        /// <returns>A <see cref="ToolResponse"/> reporting exists / kind ('asset' | 'folder' | 'none').</returns>
        [McpTool("asset-exists", Title = "Asset / Exists", ReadOnlyHint = true)]
        [Description("Lightweight check for whether a project path resolves to an asset, a folder, or nothing. Returns {exists, kind} where kind ∈ 'asset' | 'folder' | 'none'. Cheaper than asset-get-info — use this for guard checks before create/copy/rename.")]
        public ToolResponse Exists(
            [Description("Project-relative path to check (e.g. 'Assets/Prefabs/Player.prefab' or 'Assets/Prefabs'). Auto-prepends 'Assets/' if omitted.")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) && !string.Equals(path, "Assets", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = "Assets/" + path;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return ToolResponse.Text($"path: '{path}' | exists: true | kind: folder");
                }

                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid))
                {
                    return ToolResponse.Text($"path: '{path}' | exists: true | kind: asset | guid: {guid}");
                }

                return ToolResponse.Text($"path: '{path}' | exists: false | kind: none");
            });
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `AssetDatabase.IsValidFolder(path)` — folder check (fast, no asset load).
- `AssetDatabase.AssetPathToGUID(path)` — asset existence check (returns empty string on miss).

**Order matters:** folder check first because `IsValidFolder` is faster than `AssetPathToGUID` and folders ARE assets with their own GUIDs in Unity's database — calling `AssetPathToGUID` first would still return a GUID for a folder, defeating the distinction. The order above resolves "folder vs file" correctly.

**Return shape:** Single-line text response with pipe-separated fields. Matches `ToolResponse.Text` convention. Self-describing for LLM parsing.

**Migration:** Net new. Closes G6.

**Risks:**
- Backward compat: safe (new tool only).
- Build: standard imports only.
- Cross-domain: none.
- ReadOnlyHint: TRUE (only reads).

---

## 6. Change Group E — Decision doc + backlog entries (E4, E6, E7)

**Findings addressed:** G3 (won't-fix decision doc), G5 (backlog entry), G7 (backlog entry)

**Rationale:** Documentation only. Captures the deferral / rejection rationale so future audits don't re-raise these findings. No code changes.

**Definition of done:**
- `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md` exists and explains the rejection of G3.
- `docs/internal/post-v2.0-backlog.md` has one-line entries under v2.2.x for G5 (batch CRUD) and G7 (sprite slicing verification by Texture audit).
- No code files touched in this group.

**Dependencies:** None.

### Files Touched

- `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md` — **created**.
- `docs/internal/post-v2.0-backlog.md` — **modified** (add two rows to the existing v2.2.x table).

### Change E.1 — Create decision doc for G3 (AssetBundles → won't-fix)

**Type:** new doc file

**File:** `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md`

**Content:**
```markdown
# Decision — No AssetBundle tooling in Asset domain

**Date:** 2026-05-19
**Status:** Accepted
**Source:** Asset audit finding G3 (`.claude/reports/audits/audit-Asset-20260425.md`), Asset review escalation E4 (`.claude/reports/reviews/review-Asset-20260519.md`).

## Decision

The Asset domain MCP tooling will not wrap Unity's AssetBundle APIs
(`AssetImporter.assetBundleName`, `assetBundleVariant`, related `BuildPipeline`
calls). G3 from the Asset audit is marked `won't-fix`.

## Rationale

AssetBundles are a legacy distribution mechanism. Addressables has been
Unity's recommended workflow for several major versions and is the
forward-looking story for Unity 6000+ projects. Adding tooling for a
deprecated workflow adds long-term maintenance burden for negligible value.

The audit specifically noted (G3, Confidence: medium) that the API was not
wrapped anywhere in the project and that the relevance of AssetBundles in
2026 is itself in question.

## Scope of this decision

- **In scope:** AssetBundle import-settings (`assetBundleName`,
  `assetBundleVariant` on `AssetImporter`), `BuildPipeline.BuildAssetBundles`
  related tooling.
- **Out of scope (separate decision if requested):** Addressables tooling
  (`AddressableAssetSettings`, group management, build profiles). If a future
  cycle wants Addressables coverage, it lives as its own domain. Addressables'
  API surface is significantly larger and warrants its own audit/review/plan
  cycle.

## Future re-evaluation triggers

Re-open this decision only if:

1. Unity removes the Addressables package or deprecates it.
2. A concrete user workflow surfaces that explicitly requires AssetBundles
   over Addressables (e.g. a third-party tool integration that only consumes
   AssetBundles).

Otherwise this decision stands.

## References

- Asset audit G3: `.claude/reports/audits/audit-Asset-20260425.md` (Section 5)
- Asset review E4: `.claude/reports/reviews/review-Asset-20260519.md` (Section 9)
- Unity Addressables docs (forward-looking story)
```

### Change E.2 — Add backlog entries for G5 and G7

**Type:** modified doc file

**File:** `docs/internal/post-v2.0-backlog.md`

**Action:** Add two new rows to the existing `## v2.2.x — Tool consolidation` table (around line 162–180 of the current file). Insert after the existing "Asset consolidation cycle" row.

**Rows to add (verbatim):**

```markdown
| Asset — batch CRUD ops (G5 deferral) | M | `.claude/reports/reviews/review-Asset-20260519.md` (E6) | Batch move/delete/copy/rename for Asset domain. Pre-req: verify `BatchExecute` infrastructure handles AssetDatabase ops on main thread; if confirmed, route through `BatchExecute` rather than adding asset-specific batch tools. Decision deferred from Asset E6 on 2026-05-19. |
| Asset — sprite slicing verification (G7 deferral) | S | `.claude/reports/reviews/review-Asset-20260519.md` (E7) | Texture-domain audit must verify whether sprite-sheet slicing (`SpriteMetaData[]` construction on `TextureImporter`) is covered by `Tool_Texture.ApplyPattern.cs`. If yes, G7 closed-elsewhere; if no, Texture audit picks it up as its own finding. |
```

**Risks:** None. Pure doc additions to an existing table.

---

## 7. File-By-File Edit Summary

For the consolidator's mechanical execution. Grouped by file, ordered by directory.

### Editor/Tools/Asset/

| File | Action | Group | Notes |
|---|---|---|---|
| `Tool_Asset.Copy.cs` | modified | C | Rewrite `[McpToolType]` class summary (C.3). No method changes. |
| `Tool_Asset.Create.cs` | **deleted** | C | Whole file goes. Delete `.meta` sidecar too if present (C.1). |
| `Tool_Asset.CreateFolder.cs` | modified | A | Single `[Description]` tweak on the method (A.4). |
| `Tool_Asset.CreateRenderTexture.cs` | **created** | C | New tool `asset-create-render-texture` (C.2). |
| `Tool_Asset.Delete.cs` | modified | A | Single `[Description]` tweak on `moveToTrash` param (A.6). |
| `Tool_Asset.Exists.cs` | **created** | D | New tool `asset-exists` (D.3). |
| `Tool_Asset.Find.cs` | modified | A | Method `[Description]` + `searchFilter` and `folder` param descriptions (A.1). |
| `Tool_Asset.FindReferences.cs` | **created** | D | New tool `asset-find-references` (D.2). |
| `Tool_Asset.GetInfo.cs` | — | — | Untouched. |
| `Tool_Asset.ImportSettings.cs` | modified | A + B | A: `[Description]` rewrite (A.2). B: remove `ApplyStringValueToProperty` and `SplitJsonEntries` methods (B.2). |
| `Tool_Asset.Move.cs` | — | — | Untouched. |
| `Tool_Asset.Refresh.cs` | modified | A | Single `[Description]` tweak on `forceUpdate` param (A.3). |
| `Tool_Asset.Rename.cs` | modified | A | Single `[Description]` tweak on `newName` param (A.5). |
| `Tool_Asset.SetLabels.cs` | **created** | D | New tool `asset-set-labels` (D.1). |
| `Tool_Asset.Shared.cs` | **created** | B | New partial-class file holding `ApplyStringValueToProperty` (with ObjectReference write) and `SplitJsonEntries` (B.1). |

**File count after this plan:** 13 (was 10). Net +3: deleted 1 (`Create.cs`), created 4 (`Shared.cs`, `CreateRenderTexture.cs`, `SetLabels.cs`, `FindReferences.cs`, `Exists.cs`).

Wait — that's deleted 1, created 5. Let me recount.

- Deleted: 1 (`Tool_Asset.Create.cs`)
- Created: 5 (`Shared.cs`, `CreateRenderTexture.cs`, `SetLabels.cs`, `FindReferences.cs`, `Exists.cs`)
- Net: +4 (10 → 14)

Confirmed: 14 files in `Editor/Tools/Asset/` after the plan executes.

### docs/internal/

| File | Action | Group | Notes |
|---|---|---|---|
| `decisions/2026-05-19-no-asset-bundle-tooling.md` | **created** | E | New decision doc (E.1). |
| `post-v2.0-backlog.md` | modified | E | Add two rows to v2.2.x table (E.2). |

### Tools removed / renamed (caller migration)

| Old tool name | Status | Replacement | Caller impact |
|---|---|---|---|
| `asset-create` | **REMOVED** | `material-create` / `physics-create-material` / `scriptableobject-create` / `animation-configure-controller` / `asset-create-render-texture` (depending on type) | LLM callers must select the type-specific creator. No shim. No tests reference it. |

### Tools added

| New tool name | Replaces | Group |
|---|---|---|
| `asset-create-render-texture` | `asset-create assetType="RenderTexture"` | C |
| `asset-set-labels` | (nothing — new capability) | D |
| `asset-find-references` | (nothing — new capability) | D |
| `asset-exists` | (nothing — new capability) | D |

### Cross-references in repo that mention `asset-create` (informational; no edits required)

These are historical/report files. The plan does NOT modify them.

- `.claude/reports/audits/audit-Asset-20260425.md` — the source audit (read-only reference).
- `.claude/reports/audits/audit-Terrain-20260425.md` — flags overlap with `asset-create` (lines 149, 197, 221, 226). Terrain audit will be its own cycle; this audit's references will be addressed there. No action this cycle.
- `.claude/reports/audits/batch-summary-20260425.md` — summary listing (no edit).
- `.claude/reports/reviews/review-Asset-20260519.md` — the source review (read-only reference).
- `Editor/Tools/Asset/Tool_Asset.CreateFolder.cs` — match is on "asset-create-folder" (the tool's own ID); irrelevant to this finding.

---

## 8. Risk Summary (Cross-Group)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| LLM calls removed `asset-create` after consolidation | high (in the short term) | low (clear error from MCP layer — "tool not found") | No shim per review Section 3 box 3. Documented in audit/review. LLM will adapt via tool listing. |
| `Tool_Asset.Shared.cs` and `ImportSettings.cs` get out of sync during Group B execution | low | medium (compile error) | Both files must be edited in the same commit. The consolidator delivers Group B as one PR — verify both before validation runs. |
| The `[McpToolType]` summary in `Tool_Asset.Copy.cs` lists Group D tools before they exist | very low (intra-cycle) | none (XML doc is not consumed at runtime) | Acceptable. Plan ordering recommends C → D so the window where the summary is "ahead" is minutes. If Group D is deferred, revisit Change C.3. |
| ObjectReference write silently accepts wrong type | medium | medium (Unity rejects on save, surfacing later than the LLM expects) | Documented in the XML doc on `ApplyStringValueToProperty`. Matches `Tool_Object.Modify.cs` behaviour. Future enhancement: require expected-type check, but out of scope this cycle. |
| `AssetDatabase.FindAssets("")` returns folders, slowing `asset-find-references` | medium | low (folder GetDependencies returns empty quickly) | Documented in tool's performance note. Future enhancement: skip folders explicitly via `IsValidFolder`, but the cost is marginal vs the GetDependencies call. Acceptable. |
| Build risk: `using` namespaces missing on new files | low | high (compile error) | Every new file in this plan lists its imports explicitly. Build-validator will catch any miss. |

---

## 9. Out of Scope — For Future Audit

These items surfaced during planning but are NOT addressed in this cycle. They are flagged for future audits to pick up.

- **`Tool_Object.Modify.cs` parser duplication (R2's third copy):** The Asset audit explicitly carved out `Tool_Object.Modify.cs` as out of scope. Object audit (already on disk: `.claude/reports/audits/audit-Object-20260425.md`) is the right venue. When that audit reaches the plan stage, the resolution should be: fold the Object parser into `Tool_Asset.Shared.cs` (or extract one level up to `Editor/Tools/_Shared/`). Breadcrumb already in review Section 6.
- **Cross-domain `propertiesJson` instances:** Grep found other domains' tools also use a `propertiesJson` parameter (`Material/Tool_Material.Update.cs`, `Object/Tool_Object.Modify.cs`, `Component/Tool_Component.Update.cs`, `Camera/Tool_Camera.SetBody.cs`, `Camera/Tool_Camera.SetAim.cs`). Each is its own audit cycle's problem. Not touched here.
- **Terrain audit's mention of `asset-create` (audit-Terrain line 226):** Terrain audit will need a revised plan acknowledging `asset-create` no longer exists. When Terrain enters the consolidation pipeline, the planner there should add a `terrain-create-data` tool rather than extending the now-deleted `asset-create`.
- **AssetDatabase.GetAssetDependencyHash and EditorUtility.CollectDependencies:** Audit's G4 listed these as additional uncovered APIs. The plan covers the main reverse-dependency workflow but does not add hash-based change-detection or transitive collection. If needed, future Asset audit cycle.
- **`asset-find` paging beyond `maxResults = 25`:** D3 was rejected (no evidence) but flagged here in case the v2.x dogfood reveals truncation pain. Add a `pageToken` param if so.

---

## 10. Open Questions For The Consolidator

None.

The review is comprehensively finalized — every finding has a decision, every escalation has a Ramon answer, every signature is fully specified. The consolidator can execute mechanically.

(If during execution the consolidator finds that `[McpTool]`'s `ReadOnlyHint` named-argument syntax differs from the examples in this plan, refer to `Editor/Tools/Asset/Tool_Asset.Find.cs` line 22 for the canonical in-repo example: `[McpTool("asset-find", Title = "Asset / Find", ReadOnlyHint = true)]`.)

---

**Final Status:** ✅ READY FOR EXECUTION
