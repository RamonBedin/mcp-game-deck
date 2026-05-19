# Consolidation Plan — Prefab

**Date:** 2026-05-18
**Planner:** consolidation-planner agent
**Audit input:** `.claude/reports/audits/audit-Prefab-20260425.md`
**Review input:** `.claude/reports/reviews/review-Prefab-20260518.md`
**Status:** READY FOR EXECUTION

---

## 0. Plan Quality Caveats

**Inputs verified:**
- [x] Audit file present (`audit-Prefab-20260425.md`)
- [x] Review file present (`review-Prefab-20260518.md`)
- [x] Review marked `READY FOR PLANNING` (Section 8)
- [x] All 19 audit findings (R1, R2, A1–A7, D1–D4, G1–G6) have decisions in review Section 1

**Findings included in plan:**
- Accepted: A1, A2, A3, A4, A5, A6, A7, D1, D2, D3, D4, G3, G5
- Accepted with modification: R1, R2, G1, G2, G4, G6
- Excluded (rejected / deferred): none — every finding is in scope this cycle.

**Constraints applied (from review Section 3):**
- Backward compat box 3: may break tool names freely. Clean breaks confirmed for A6 (sentinel migration), D2 (default removal), and `add-asset-to-scene` deletion. No deprecation shims.
- Code style: CLAUDE.md C# standards strictly enforced — braces on every `if`, no empty catches, `EntityIdToObject` not `InstanceIDToObject` for NEW code, no `obj?.prop = x` null-conditional assignment, single XML `<summary>` on the partial-class file containing `[McpToolType]`.
- `McpLogger` has only `Info` and `Error` — any new warning paths use `Debug.LogWarning` explicitly.
- String-sentinel convention for nullable booleans (`"true" | "false" | ""`). Do NOT introduce new int tri-state booleans anywhere in new code.
- Tool descriptions come from `[System.ComponentModel.Description]` on the method (NOT `toolAttr.Description`).
- Do NOT touch Asset / Component / Script / Scene / Transform domains except cite-only references.
- `Tool_Transform.FindGameObject` dependency stays as-is (its `EditorUtility.InstanceIDToObject` usage is flagged for Transform audit; do NOT migrate here).
- Do NOT pull v2.0 features into this cycle.
- `AddAssetToScene` domain (1 file + 1 `.meta`) is in-scope ONLY for deletion under Group C, folded into `prefab-instantiate`.

**Reviewer notes carried forward (from review Sections 5–7):**
- E1: clean-break delete `add-asset-to-scene`, fold its features into `prefab-instantiate` (non-prefab fallback via `PrefabUtility.GetPrefabAssetType` → `Object.Instantiate`; rotation parity via `rotX/rotY/rotZ`; keep `parentPath`).
- E2: `prefab-modify-contents` stays unified and grows via action-dispatch (6th action = `set-component-field`).
- E3: `prefab-get-info` gains `maxDepth`, nested-prefab annotation, and `isVariant` flag.
- E4: new `prefab-create-variant`; the read side gets a free ride on the `isVariant` flag from E3.
- E5: action-dispatched `prefab-override` with 3 actions (`list`, `apply-instance`, `revert-instance`). Per-property actions deferred.
- E6: G4 (`set-component-field`) is the 6th action on `prefab-modify-contents` (primitives + ObjectReference only, with FUTURE CONSOLIDATION comment). G6 (`prefab-unpack-instance`) is a brand-new tool.
- E7: description-only cross-link to `asset-find`.
- The single `[McpToolType]` summary lives in `Editor/Tools/Prefab/Tool_Prefab.Close.cs` (lines 10–13). All new partial files (`Tool_Prefab.CreateVariant.cs`, `Tool_Prefab.UnpackInstance.cs`, `Tool_Prefab.Override.cs`) MUST NOT duplicate that class-level summary. Update the `Close.cs` summary in Group F/E/G to reflect the new surface.

### Ambiguity resolutions (the 4 the task asked me to lock)

1. **A1 placement (Group A vs Group B):** **Stay in Group A.** Rationale: A1 is a pure `[Description]` edit on `prefab-modify-contents` (method-level and an internal XML doc). Group B's signature changes on the same file (A6 sentinel migration + D2 default removal) are independent of description text. Co-locating A1 into Group B would mix description and signature concerns in one PR for no clean-diff benefit — Group A's policy is "any tool gets all its description-only fixes in one PR regardless of which other groups touch that file later." Group B will reapply its own description text on the affected params (`action`, `isActive`) without conflict, since A1 expands the *method-level* description and Group B touches *parameter-level* descriptions on the changed params. Concrete coordination: Group B's after-text for `action` and `isActive` `[Description]` strings is specified in §3 so Group A's wording for adjacent params (`targetChild`, `deleteChild`, etc.) and Group B's wording on `action`/`isActive` compose cleanly.

2. **`add-asset-to-scene` consumer survey:** Done. Findings below in §0.5.

3. **D1 / D4 file location:** Both `savePath` and `keepConnection` `[Description]` attributes live in **`Editor/Tools/Prefab/Tool_Prefab.Create.cs`** (lines 31 and 32 respectively). Group A's edits target that exact file.

4. **`prefab-override` warning logger:** Locked to `Debug.LogWarning`. Reflected in Group G change G.1 below. `McpLogger.Warning` does not exist; using it would not compile.

### 0.5 — `add-asset-to-scene` consumer survey result

**Survey scope:** `Editor/Tools/`, `Editor/Prompts/`, `Editor/MCP/`, `Server~/`, `Agents~/`, `Plugin~/`, `KnowledgeBase~/`, `Skills~/`, plus root prompts.

**String-literal `add-asset-to-scene` matches (production / runtime code):**

| File | Line | Type | Consumer impact |
|------|------|------|-----------------|
| `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` | 32 | `[McpTool(...)]` declaration | Removed under Group C (file deleted). |
| `Editor/Prompts/Prompt_GameObjectHandling.cs` | 35 | Prompt body string referencing the tool name | **Must update under Group C** — replace with `prefab-instantiate`. |
| `Editor/Prompts/Prompt_PrefabWorkflow.cs` | 39, 45, 57 | Prompt body string (3 occurrences) | **Must update under Group C** — replace with `prefab-instantiate`. |
| `Server~/prompts/core-system-prompt.md` | 53 | Markdown tool-catalog reference | **Must update under Group C** — string substitution `add-asset-to-scene` → `prefab-instantiate` in the Scene catalog line, and verify the resulting line still reads sensibly (it does — `prefab-instantiate` now covers non-prefab assets too). |
| `Plugin~/agents/gameplay-programmer.md` | 37 | Agent-definition markdown reference | **Must update under Group C** — same substitution. |
| `Plugin~/agents/unity-specialist.md` | 44 | Agent-definition markdown reference | **Must update under Group C** — same substitution. |
| `Plugin~/agents/unity-ui-specialist.md` | 18 | Agent-definition markdown reference | **Must update under Group C** — same substitution. |
| `Plugin~/agents/unity-addressables-specialist.md` | 22 | Agent-definition markdown reference | **Must update under Group C** — same substitution. |
| `Plugin~/skills/prototype/SKILL.md` | 15 | Skill body referencing the tool name | **Must update under Group C** — same substitution. |

**`AddAssetToScene` class-name matches:**
- `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` (declaration — deleted under Group C).
- `docs/internal/post-v2.0-backlog.md`, `docs/internal/roadmap.md`, `.claude/state/audit-batch-progress.json`, audit / batch-summary files in `.claude/reports/audits/` — **all are docs / state-tracking files. Do NOT edit under this plan** (they're historical artifacts; updating them is Ramon's call when he closes out the audit/state files post-consolidation). The consolidator flags this as "documentation drift" only; no production code is affected.

**Conclusion:** Deletion is safe from a runtime perspective. There are 9 prompt / skill / agent-definition references in non-`.cs` and `.cs` prompt files that must be string-substituted under Group C to avoid telling the LLM about a tool that no longer exists. Group C's scope grows by 9 file-touches beyond just the deletion + `prefab-instantiate` modification. No deviation candidates surfaced — all 9 consumers are mechanical text edits.

---

## 1. File Inventory

### Files to READ ONLY (cite-only references, no edits)

- `Editor/Tools/Transform/Tool_Transform.cs` — `Tool_Transform.FindGameObject` helper used by new tools in Groups E, F, G. **No edits.**
- `Editor/Tools/Asset/Tool_Asset.Find.cs` — referenced by Group H description text. **No edits.**

### Files to MODIFY

| # | File | Group(s) | Reason |
|---|------|----------|--------|
| 1 | `Editor/Tools/Prefab/Tool_Prefab.Create.cs` | A | Description-only updates: A2 method, D1 `savePath`, D4 `keepConnection`. |
| 2 | `Editor/Tools/Prefab/Tool_Prefab.Save.cs` | A | Description-only update: A3 method. |
| 3 | `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs` | A, B, E | A: A1 + A5 description text. B: A6 sentinel migration on `isActive` + D2 default removal on `action`. E: G4 `set-component-field` as 6th action. |
| 4 | `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs` | C, H | C: add `rotX/rotY/rotZ` + non-prefab fallback. H: description cross-link to `asset-find`. |
| 5 | `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs` | D, H | D: `maxDepth`, nested-prefab annotation, `isVariant` flag, plain-text format note. H: description cross-link to `asset-find`. |
| 6 | `Editor/Tools/Prefab/Tool_Prefab.Close.cs` | F, E, G | Update `[McpToolType]` class-level XML `<summary>` to reflect new tools added (variant, unpack, override). **No code changes**, summary text only. |
| 7 | `Editor/Prompts/Prompt_GameObjectHandling.cs` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 35 prompt body). |
| 8 | `Editor/Prompts/Prompt_PrefabWorkflow.cs` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (3 occurrences: lines 39, 45, 57). |
| 9 | `Server~/prompts/core-system-prompt.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 53 tool-catalog reference). |
| 10 | `Plugin~/agents/gameplay-programmer.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 37). |
| 11 | `Plugin~/agents/unity-specialist.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 44). |
| 12 | `Plugin~/agents/unity-ui-specialist.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 18). |
| 13 | `Plugin~/agents/unity-addressables-specialist.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 22). |
| 14 | `Plugin~/skills/prototype/SKILL.md` | C | Replace `add-asset-to-scene` → `prefab-instantiate` (line 15). |

### Files to CREATE

| # | File | Group | Reason |
|---|------|-------|--------|
| 15 | `Editor/Tools/Prefab/Tool_Prefab.CreateVariant.cs` | F | New tool `prefab-create-variant`. |
| 16 | `Editor/Tools/Prefab/Tool_Prefab.UnpackInstance.cs` | E | New tool `prefab-unpack-instance`. |
| 17 | `Editor/Tools/Prefab/Tool_Prefab.Override.cs` | G | New action-dispatched tool `prefab-override`. |

### Files to DELETE

| # | File | Group | Reason |
|---|------|-------|--------|
| 18 | `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` | C | Folded into `prefab-instantiate`. |
| 19 | `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs.meta` | C | Companion `.meta` (Unity asset metadata). |
| 20 | `Editor/Tools/AddAssetToScene/` (directory) | C | Empty after the 2 file deletions; remove the folder. The `.meta` for the directory itself, if any, is removed as well. |

**Totals:** Read-only: 2 · Modified: 14 · Created: 3 · Deleted: 3 (1 directory + 2 files).

### Group → Files matrix

| Group | Modify | Create | Delete | Notes |
|-------|--------|--------|--------|-------|
| A | 3 (Create, Save, ModifyContents) | 0 | 0 | Description-only. |
| B | 1 (ModifyContents) | 0 | 0 | Signature break on `action` + `isActive`. |
| C | 9 (Instantiate, GameObjectHandling, PrefabWorkflow, core-system-prompt, 4 agent .md, prototype SKILL.md) | 0 | 3 (AddAssetToScene file + .meta + folder) | Cross-domain surface deletion + consumer fixups. |
| D | 1 (GetInfo) | 0 | 0 | Additive: new param `maxDepth`, output extensions. |
| E | 1 (ModifyContents) | 1 (UnpackInstance) | 0 | G4 as 6th action + G6 as new tool. |
| F | 1 (Close — summary text only) | 1 (CreateVariant) | 0 | New variant tool. |
| G | 1 (Close — summary text only) | 1 (Override) | 0 | New override tool. |
| H | 2 (GetInfo, Instantiate) | 0 | 0 | Description cross-link. |

Note: Groups F and G each touch `Close.cs` for the same purpose (updating the `[McpToolType]` class-level `<summary>`). The consolidator should batch the two summary edits when executing Groups F and G — there's no real conflict, but each group's "definition of done" includes its name showing up in the summary.

---

## Summary

| # | Change Group | Findings | Files Touched | Priority | Order |
|---|--------------|----------|---------------|----------|-------|
| A | Description polish | A1, A2, A3, A5, D1, D4 | 3 (modified) | high (cheap) | 1 |
| B | Sentinel + default migrations on `prefab-modify-contents` | A6, D2 | 1 (modified) | high | 2 |
| C | Delete `add-asset-to-scene`; fold features into `prefab-instantiate` | R1, A4, D3 | 9 modified + 3 deleted | high | 3 |
| D | `prefab-get-info` enrichment | A7, G3, + isVariant fold | 1 (modified) | high | 4 |
| E | Headless workflow extensions (G4 action + G6 new tool) | G4, G6 | 1 modified + 1 created | high | 5 |
| F | Prefab variants | G1 | 1 created + 1 summary update | medium | 6 |
| G | Prefab overrides (action-dispatched) | G2 | 1 created + 1 summary update | high (biggest scope) | 7 |
| H | Discoverability cross-link | G5 | 2 (modified) | low | 8 |

**Recommended order:** A → B → C → D → E → F → G → H

Rationale: low-risk-first (pure description polish, then signature break on one file, then cross-domain deletion which is the biggest "blast radius" item, then enrichment, then additions). Group H is last because it's the cheapest, lets the new descriptions on `prefab-instantiate` (touched by Group C) and `prefab-get-info` (touched by Group D) settle before adding the cross-link line.

No group has a hard dependency on a later group. Groups B and E both modify `Tool_Prefab.ModifyContents.cs`; the consolidator should land B first so the sentinel/default work is established before the 6th action is added under E. Group E's new `set-component-field` case uses the post-B sentinel pattern for any of its own params, although as specified it uses only string/non-nullable types and won't introduce new tri-states.

---

## 2. Change Group A — Description polish (ship first)

**Findings addressed:** A1, A2, A3, A5, D1, D4

**Rationale:** Pure `[Description]` attribute and XML doc comment edits. No signature changes. No behavioral impact. Cheap PR, low risk, lands immediately. A1 method-level description on `prefab-modify-contents` adds the per-action required-param map; A5 tightens `targetChild` / `deleteChild` parameter descriptions on the same file — colocated for one diff.

**Definition of done:**
- All 6 findings' description tweaks applied verbatim.
- No signature changes anywhere; the project compiles cleanly with `dotnet build` and `tsc --noEmit` (build-validator confirms).
- XML doc comments updated to match new `[Description]` text where the doc paraphrases it (consistency).

**Dependencies:** None.

### Files Touched

- `Editor/Tools/Prefab/Tool_Prefab.Create.cs` — A2 (method-level), D1 (`savePath`), D4 (`keepConnection`).
- `Editor/Tools/Prefab/Tool_Prefab.Save.cs` — A3 (method-level).
- `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs` — A1 (method-level), A5 (`targetChild`, `deleteChild` params).

### Change A.1 — `prefab-create` source-object precedence (A2)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.Create.cs`

**Before (line 27):**
```csharp
[Description("Creates a Prefab asset from a scene GameObject and saves it to the project.")]
```

**After:**
```csharp
[Description("Creates a Prefab asset from a scene GameObject and saves it to the project. " + "One of 'instanceId' or 'objectPath' is required. Prefer 'instanceId' if known from a recent tool call; otherwise use 'objectPath'.")]
```

XML doc summary on lines 15–17 already reads "Creates a Prefab asset from an existing GameObject in the scene." — leave unchanged (consistent with the new `[Description]`).

### Change A.2 — `prefab-create` `savePath` implicit default (D1)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.Create.cs`

**Before (line 31):**
```csharp
[Description("Asset path to save the prefab (e.g. 'Assets/Prefabs/Player.prefab').")] string savePath = "",
```

**After:**
```csharp
[Description("Asset path to save the prefab (e.g. 'Assets/Prefabs/Player.prefab'). Default empty: resolves to 'Assets/{go.name}.prefab' at the project root.")] string savePath = "",
```

Also update the corresponding XML `<param name="savePath">` on line 20:

**Before (line 20):**
```csharp
/// <param name="savePath">Asset path to save the prefab (e.g. "Assets/Prefabs/Player.prefab"). Defaults to "Assets/{name}.prefab".</param>
```

**After:**
```csharp
/// <param name="savePath">Asset path to save the prefab (e.g. "Assets/Prefabs/Player.prefab"). Default empty: resolves to "Assets/{go.name}.prefab" at the project root.</param>
```

### Change A.3 — `prefab-create` `keepConnection` rationale (D4)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.Create.cs`

**Before (line 32):**
```csharp
[Description("Keep prefab connection on the scene object. Default true.")] bool keepConnection = true
```

**After:**
```csharp
[Description("Keep prefab connection on the scene object so future edits to the prefab asset propagate. Default true. Set false only when you want a one-shot snapshot with no link back.")] bool keepConnection = true
```

XML `<param name="keepConnection">` on line 21 — update for consistency:

**Before (line 21):**
```csharp
/// <param name="keepConnection">When true, connects the scene object to the new prefab asset. Default true.</param>
```

**After:**
```csharp
/// <param name="keepConnection">Keep prefab connection on the scene object so future edits to the prefab asset propagate. Default true. Set false only when you want a one-shot snapshot with no link back.</param>
```

### Change A.4 — `prefab-save` precondition (A3)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.Save.cs`

**Before (line 26):**
```csharp
[Description("Saves the Prefab currently open in Prefab Edit Mode back to disk. " + "No-ops and returns an error when no Prefab Edit Mode stage is active.")]
```

**After:**
```csharp
[Description("Saves the Prefab currently open in Prefab Edit Mode back to disk. " + "Call after 'prefab-open' and your modifications. Do NOT call after 'prefab-modify-contents' (which saves on its own). " + "No-ops and returns an error when no Prefab Edit Mode stage is active.")]
```

### Change A.5 — `prefab-modify-contents` method-level per-action map (A1)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs`

**Before (line 40):**
```csharp
[Description("Modifies the contents of a Prefab asset without entering Prefab Mode. " + "Actions: set-position, add-component, remove-component, delete-child, set-active. " + "Changes are saved back to disk immediately.")]
```

**After:**
```csharp
[Description("Modifies the contents of a Prefab asset without entering Prefab Mode (headless one-shot edit; auto-saves on success). " + "For multi-step interactive edits across Component/Transform/GameObject tools, use 'prefab-open' / 'prefab-save' instead. " + "Actions and the params each one uses: " + "'set-position' uses posX/posY/posZ; " + "'add-component' uses componentType; " + "'remove-component' uses componentType; " + "'delete-child' uses deleteChild; " + "'set-active' uses isActive. " + "Changes are saved back to disk immediately.")]
```

NOTE: Group B will further amend this string when adding the 6th action under Group E (`set-component-field`). Group E's change appends `"'set-component-field' uses fieldName + one of fieldValue* / fieldValueObject."` to this list. Group A's diff stops at the 5-action wording above so the consolidator can apply A and B/E independently.

### Change A.6 — `prefab-modify-contents` `targetChild` / `deleteChild` symmetry (A5)

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs`

**Before (line 43):**
```csharp
[Description("Child path relative to Prefab root (e.g. 'Body/Head'). Empty for root.")] string targetChild = "",
```

**After:**
```csharp
[Description("Child path relative to Prefab root (e.g. 'Body/Head'). Empty for root. Ignored for 'delete-child' action (use 'deleteChild' instead).")] string targetChild = "",
```

**Before (line 49):**
```csharp
[Description("Relative child path to destroy for delete-child action.")] string deleteChild = "",
```

**After:**
```csharp
[Description("Relative child path to destroy. Only used for 'delete-child' action.")] string deleteChild = "",
```

XML `<param>` tags on lines 30 and 36 — update for consistency:

**Before (line 30):**
```csharp
/// <param name="targetChild">Path relative to the Prefab root to a child Transform (e.g. 'Body/Head'). Empty means the root.</param>
```

**After:**
```csharp
/// <param name="targetChild">Path relative to the Prefab root to a child Transform (e.g. 'Body/Head'). Empty means the root. Ignored for 'delete-child' action.</param>
```

**Before (line 36):**
```csharp
/// <param name="deleteChild">Relative child path to destroy for delete-child action.</param>
```

**After:**
```csharp
/// <param name="deleteChild">Relative child path to destroy. Only used for 'delete-child' action.</param>
```

**Risks for Group A:**
- Backward compat: zero (description-only).
- Build: zero risk; no API surface change.
- Cross-domain: none.
- Test gap: no tests to update (descriptions are not asserted).

---

## 3. Change Group B — Sentinel + default migrations on `prefab-modify-contents`

**Findings addressed:** A6 (int tri-state `isActive` → string sentinel), D2 (`action` default removed).

**Rationale:** Both findings touch the same file (`Tool_Prefab.ModifyContents.cs`) and both are signature-level breaks. Co-located in one PR so callers see a single break diff. Establishes the CLAUDE.md string-sentinel convention on the Prefab domain (already enforced on GameObject).

**Definition of done:**
- `action` no longer has a default value. Calling without a value triggers a clear error listing the 5 valid actions (Group E will later expand this list to 6 when adding `set-component-field`).
- `isActive` changed from `int isActive = -1` to `string isActive = ""`, accepting `"true"`, `"false"`, or `""`.
- Empty `isActive` errors **only when** `action == "set-active"` (existing behavior parity); for other actions the empty sentinel is silently ignored.
- Project compiles cleanly.

**Dependencies:** Should land after Group A (which also touches this file but only descriptions). No hard dependency — Group A's edits and Group B's edits are on different lines / different concerns and merge cleanly. Order A→B keeps the diff per PR small.

### Files Touched

- `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs` — both findings.

### Change B.1 — Remove default on `action` (D2)

**Type:** signature break

**File:** `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs`

**Before (line 44):**
```csharp
[Description("Action: set-position, add-component, remove-component, delete-child, set-active.")] string action = "set-position",
```

**After:**
```csharp
[Description("Action to perform on the prefab contents. Required. One of: 'set-position', 'add-component', 'remove-component', 'delete-child', 'set-active'. Empty returns an error listing the valid values.")] string action = "",
```

**Body change** — replace the empty-action handling. Currently, an unknown action falls through to the `default:` case at line 191. After D2 we want a clearer early-return for `action = ""`. Add **between line 96 (the `sb.AppendLine("Modified prefab '{prefabPath}':")` declaration) and line 97 (`string actionNorm = action.Trim().ToLowerInvariant();`)**:

```csharp
if (string.IsNullOrWhiteSpace(action))
{
    PrefabUtility.UnloadPrefabContents(root);
    return ToolResponse.Error("'action' is required. Valid values: set-position, add-component, remove-component, delete-child, set-active.");
}
```

(Group E will later expand the error string to include `set-component-field`. Group B ships with the 5-action error.)

The existing `default:` case at line 191 stays as a safety net for genuinely unknown actions (typos, etc.).

### Change B.2 — Migrate `isActive` from int tri-state to string sentinel (A6)

**Type:** signature break

**File:** `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs`

**Before (line 50):**
```csharp
[Description("Active state for set-active: 1=active, 0=inactive, -1=skip. Default -1.")] int isActive = -1
```

**After:**
```csharp
[Description("Active state for 'set-active' action. One of 'true', 'false', or '' (skip). Default ''. Required for 'set-active'; ignored for all other actions.")] string isActive = ""
```

**XML `<param name="isActive">` on line 37 — update for consistency:**

**Before (line 37):**
```csharp
/// <param name="isActive">Set active state for set-active: 1=active, 0=inactive. -1 to skip.</param>
```

**After:**
```csharp
/// <param name="isActive">Active state for set-active. One of "true", "false", or "" (skip). Required for set-active; ignored otherwise.</param>
```

Also update the bullet point in the method-level XML summary list at line 26:

**Before (line 26):**
```csharp
///   <item><c>set-active</c> — sets the active state of the target (0=false, 1=true via <paramref name="isActive"/>).</item>
```

**After:**
```csharp
///   <item><c>set-active</c> — sets the active state of the target via <paramref name="isActive"/> ("true" / "false" / "").</item>
```

**Body change** — replace the `set-active` case (lines 178–189). The case currently reads:

**Before (lines 178–189):**
```csharp
case "set-active":
{
    if (isActive == -1)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("isActive must be 0 or 1 for set-active action.");
    }
    bool active = isActive != 0;
    target.gameObject.SetActive(active);
    sb.AppendLine($"  Set active={active} on '{target.name}'.");
    break;
}
```

**After:**
```csharp
case "set-active":
{
    string norm = isActive.Trim().ToLowerInvariant();

    if (string.IsNullOrEmpty(norm))
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("isActive is required for the 'set-active' action. Pass 'true' or 'false'.");
    }

    bool active;

    if (norm == "true")
    {
        active = true;
    }
    else if (norm == "false")
    {
        active = false;
    }
    else
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error($"isActive must be 'true', 'false', or '' (skip). Got '{isActive}'.");
    }

    target.gameObject.SetActive(active);
    sb.AppendLine($"  Set active={active} on '{target.name}'.");
    break;
}
```

**Risks for Group B:**
- Backward compat: HARD BREAK on two signatures. Per review Section 3 box 3, no shim. Existing callers passing `isActive=1` (int) will fail with a type mismatch at the MCP boundary; callers omitting `action` will receive the clear error.
- Build: zero risk; the `int` → `string` change does not affect any other callsite in the codebase (no internal C# caller — this is invoked only via MCP).
- Cross-domain: none (the tool surface change ripples only through MCP clients).
- Test gap: no automated tests; manual smoke after consolidator finishes.

---

## 4. Change Group C — Delete `add-asset-to-scene`; fold features into `prefab-instantiate`

**Findings addressed:** R1, A4, D3 (per review E1).

**Rationale:** Eliminates 80%+ parameter-overlap redundancy between two tools that solve the same problem (instantiate-asset-into-scene). Folds `add-asset-to-scene`'s two unique features into `prefab-instantiate`: (a) `rotX/rotY/rotZ` rotation, (b) non-prefab GameObject asset fallback via `PrefabUtility.GetPrefabAssetType` → `Object.Instantiate`. Deletes the orphan domain. Follows GameObject precedent (no shim).

**Definition of done:**
- `prefab-instantiate` accepts `rotX, rotY, rotZ` (defaults 0).
- `prefab-instantiate` accepts non-prefab GameObject assets (e.g. FBX) and falls back to `Object.Instantiate` when `PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab`.
- `prefab-instantiate` keeps the `parentPath` name (no rename).
- `prefab-instantiate` description is updated to reflect non-prefab fallback and rotation support.
- `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs` is deleted along with its `.meta` and the parent folder.
- All 9 string-literal consumer references to `add-asset-to-scene` are substituted to `prefab-instantiate` in prompt / agent / skill files (see survey §0.5).
- Project compiles cleanly.

**Dependencies:** None functional. Consumer file edits (9 prompt/agent/skill files) can be done in the same PR as the deletion.

### Files Touched

- Modified: `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs`.
- Modified: `Editor/Prompts/Prompt_GameObjectHandling.cs` (line 35).
- Modified: `Editor/Prompts/Prompt_PrefabWorkflow.cs` (lines 39, 45, 57).
- Modified: `Server~/prompts/core-system-prompt.md` (line 53).
- Modified: `Plugin~/agents/gameplay-programmer.md` (line 37).
- Modified: `Plugin~/agents/unity-specialist.md` (line 44).
- Modified: `Plugin~/agents/unity-ui-specialist.md` (line 18).
- Modified: `Plugin~/agents/unity-addressables-specialist.md` (line 22).
- Modified: `Plugin~/skills/prototype/SKILL.md` (line 15).
- Deleted: `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs`.
- Deleted: `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs.meta`.
- Deleted: `Editor/Tools/AddAssetToScene/` (empty folder + its `.meta` if present).

### Change C.1 — Expand `prefab-instantiate` (R1 + A4 + D3)

**Type:** modified tool (additive params + new non-prefab fallback branch)

**File:** `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs`

**Before (line 26 — `[Description]`):**
```csharp
[Description("Loads a Prefab asset and instantiates it into the active scene as a linked prefab instance. " + "Supports world position, optional name override, and an optional parent GameObject.")]
```

**After:**
```csharp
[Description("Instantiates a prefab or other GameObject asset (FBX, model, plain .asset GameObject) into the active scene at a specified position and rotation. " + "When the asset is a prefab, creates a linked instance via PrefabUtility.InstantiatePrefab; otherwise falls back to Object.Instantiate. " + "Supports world position, world rotation (Euler), optional name override, and an optional parent GameObject (top-level name or hierarchy path).")]
```

(Group H will append a final cross-link sentence to this string. The Group C "After" stops at the description above so Group C and H can be applied independently.)

**Before (lines 28–35 — signature):**
```csharp
public ToolResponse Instantiate(
    [Description("Asset path of the prefab to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab').")] string prefabPath,
    [Description("Name for the new instance. Leave empty to keep the prefab's original name.")] string name = "",
    [Description("World-space X position. Default 0.")] float posX = 0f,
    [Description("World-space Y position. Default 0.")] float posY = 0f,
    [Description("World-space Z position. Default 0.")] float posZ = 0f,
    [Description("Hierarchy path of the parent GameObject (e.g. 'World/Enemies'). Empty for scene root.")] string parentPath = ""
)
```

**After:**
```csharp
public ToolResponse Instantiate(
    [Description("Asset path of the GameObject asset to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab' or 'Assets/Models/Tree.fbx').")] string prefabPath,
    [Description("Name for the new instance. Leave empty to keep the asset's original name.")] string name = "",
    [Description("World-space X position. Default 0.")] float posX = 0f,
    [Description("World-space Y position. Default 0.")] float posY = 0f,
    [Description("World-space Z position. Default 0.")] float posZ = 0f,
    [Description("World-space X rotation in degrees (Euler). Default 0.")] float rotX = 0f,
    [Description("World-space Y rotation in degrees (Euler). Default 0.")] float rotY = 0f,
    [Description("World-space Z rotation in degrees (Euler). Default 0.")] float rotZ = 0f,
    [Description("Parent GameObject — top-level name or full hierarchy path (e.g. 'World/Enemies'). Resolves via GameObject.Find. Empty for scene root.")] string parentPath = ""
)
```

**XML doc updates (lines 15–25):**

**Before:**
```csharp
/// <summary>
/// Loads a Prefab asset and instantiates it into the active scene as a linked prefab instance.
/// Supports setting an initial world position, custom name, and optional parent object.
/// </summary>
/// <param name="prefabPath">Asset path of the prefab to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab').</param>
/// <param name="name">Override name for the new instance. Keeps the prefab's name when empty.</param>
/// <param name="posX">World-space X position of the new instance. Default 0.</param>
/// <param name="posY">World-space Y position of the new instance. Default 0.</param>
/// <param name="posZ">World-space Z position of the new instance. Default 0.</param>
/// <param name="parentPath">Hierarchy path of the parent GameObject. Empty places instance at scene root.</param>
/// <returns>A <see cref="ToolResponse"/> with the new instance's name and ID, or an error.</returns>
```

**After:**
```csharp
/// <summary>
/// Instantiates a prefab or non-prefab GameObject asset into the active scene at a specified
/// position and rotation. Uses PrefabUtility.InstantiatePrefab when the asset is a prefab
/// (preserving prefab connection) and Object.Instantiate otherwise.
/// </summary>
/// <param name="prefabPath">Asset path of the GameObject asset (e.g. 'Assets/Prefabs/Enemy.prefab' or 'Assets/Models/Tree.fbx').</param>
/// <param name="name">Override name for the new instance. Keeps the asset's name when empty.</param>
/// <param name="posX">World-space X position of the new instance. Default 0.</param>
/// <param name="posY">World-space Y position of the new instance. Default 0.</param>
/// <param name="posZ">World-space Z position of the new instance. Default 0.</param>
/// <param name="rotX">World-space X rotation (Euler degrees). Default 0.</param>
/// <param name="rotY">World-space Y rotation (Euler degrees). Default 0.</param>
/// <param name="rotZ">World-space Z rotation (Euler degrees). Default 0.</param>
/// <param name="parentPath">Top-level name or full hierarchy path of the parent GameObject (resolved via GameObject.Find). Empty places the instance at scene root.</param>
/// <returns>A <see cref="ToolResponse"/> with the new instance's name and ID, or an error.</returns>
```

**Body change** — replace the instantiation branch (currently lines 70–82):

**Before (lines 70–82):**
```csharp
var instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;

if (instance == null)
{
    return ToolResponse.Error("PrefabUtility.InstantiatePrefab returned null.");
}

if (!string.IsNullOrWhiteSpace(name))
{
    instance.name = name;
}

instance.transform.position = new Vector3(posX, posY, posZ);
```

**After:**
```csharp
GameObject? instance;
PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);

if (assetType != PrefabAssetType.NotAPrefab)
{
    instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;

    if (instance == null)
    {
        return ToolResponse.Error("PrefabUtility.InstantiatePrefab returned null.");
    }
}
else
{
    instance = Object.Instantiate(prefabAsset, parent);

    if (instance == null)
    {
        return ToolResponse.Error("Object.Instantiate returned null for the non-prefab asset.");
    }
}

if (!string.IsNullOrWhiteSpace(name))
{
    instance.name = name;
}

var position = new Vector3(posX, posY, posZ);
var rotation = Quaternion.Euler(rotX, rotY, rotZ);
instance.transform.SetPositionAndRotation(position, rotation);
```

The existing rename on line 84 (`Selection.activeGameObject = instance;`) stays. The success-message line (currently line 87) is updated to include rotation:

**Before (line 87 — single line wrap):**
```csharp
return ToolResponse.Text($"Instantiated prefab '{prefabAsset.name}' as '{instance.name}' " + $"(ID: {instance.GetInstanceID()}) at ({posX}, {posY}, {posZ})." + (parent != null ? $" Parent: '{parentPath}'." : ""));
```

**After:**
```csharp
return ToolResponse.Text($"Instantiated '{prefabAsset.name}' as '{instance.name}' " + $"(ID: {instance.GetInstanceID()}) at position ({posX}, {posY}, {posZ}) rotation ({rotX}, {rotY}, {rotZ})." + (parent != null ? $" Parent: '{parentPath}'." : ""));
```

(Drops "prefab" from the leading word because we now handle non-prefab assets too.)

**Note on `parentPath` resolution:** the existing code at line 60 calls `GameObject.Find(parentPath)`. Unity's `GameObject.Find` already accepts both a top-level name (`"World"`) and a hierarchy path (`"World/Enemies"`) — the same call covers both shapes. No additional logic needed; the description on `parentPath` is updated to make this explicit.

### Change C.2 — Delete `add-asset-to-scene`

**Type:** removed tool (entire file + folder)

**Files removed:**
- `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs`
- `Editor/Tools/AddAssetToScene/Tool_AddAssetToScene.cs.meta`
- `Editor/Tools/AddAssetToScene/` (directory; remove its `.meta` if present at the parent-folder level)

**Migration:**

| Old tool call | New tool call | Param mapping | Notes |
|---|---|---|---|
| `add-asset-to-scene(assetPath, posX, posY, posZ, rotY, name, parentName)` | `prefab-instantiate(prefabPath, name, posX, posY, posZ, rotX=0, rotY, rotZ=0, parentPath)` | `assetPath` → `prefabPath`; `posX/Y/Z` 1:1; `rotY` → `rotY` (rotX/rotZ default 0); `name` 1:1; `parentName` → `parentPath` | Same `GameObject.Find` resolution. |

**EditorGUIUtility.PingObject special-case:** `add-asset-to-scene` calls `EditorGUIUtility.PingObject(instance);` after selection. `prefab-instantiate` does NOT currently ping. **Decision:** do not add ping to `prefab-instantiate`. Rationale: the rest of the Prefab domain (`prefab-create`) doesn't ping either, and `Selection.activeGameObject = instance;` is the canonical "draw user's attention" pattern. Pinging is a small UX nicety from a tool authored before the domain convention solidified; not worth replicating.

### Change C.3 — Replace `add-asset-to-scene` references in consumer files

**Type:** mechanical string substitution

For each of the 9 files listed in §0.5, replace the literal `add-asset-to-scene` with `prefab-instantiate`. The surrounding prose in those files generally describes the substituted tool's behavior accurately (instantiate prefab into scene); the consolidator should review each substitution in context to confirm the surrounding text still makes sense post-substitution.

**Specific edits:**

- `Editor/Prompts/Prompt_GameObjectHandling.cs:35`
  - Before: `- add-asset-to-scene — instantiate prefabs into the scene`
  - After: `- prefab-instantiate — instantiate prefabs or model assets into the scene`

- `Editor/Prompts/Prompt_PrefabWorkflow.cs:39`
  - Before: `2. Instantiate into scene with add-asset-to-scene`
  - After: `2. Instantiate into scene with prefab-instantiate`

- `Editor/Prompts/Prompt_PrefabWorkflow.cs:45`
  - Before: `1. Load base prefab with add-asset-to-scene`
  - After: `1. Load base prefab with prefab-instantiate`

- `Editor/Prompts/Prompt_PrefabWorkflow.cs:57`
  - Before: `- add-asset-to-scene — instantiate prefabs`
  - After: `- prefab-instantiate — instantiate prefabs or model assets`

- `Server~/prompts/core-system-prompt.md:53`
  - Before: `**Scene**: scene-create, -load, -save, -delete, -unload, -get-info, -list, -get-hierarchy, -view-frame, add-asset-to-scene`
  - After: `**Scene**: scene-create, -load, -save, -delete, -unload, -get-info, -list, -get-hierarchy, -view-frame`
  - Then ensure `prefab-instantiate` is listed under the Prefab section of the same file (verify before edit; if already present, no further edit needed; if missing, add it). The consolidator must read this file in full before editing and report back what the Prefab section currently looks like.

- `Plugin~/agents/gameplay-programmer.md:37`
  - Before: `- **Add Asset**: \`add-asset-to-scene\` — place prefabs in scene`
  - After: `- **Add Asset**: \`prefab-instantiate\` — place prefabs or model assets in scene`

- `Plugin~/agents/unity-specialist.md:44`
  - Before: `- **Add Asset**: \`add-asset-to-scene\` — instantiate assets in scene`
  - After: `- **Add Asset**: \`prefab-instantiate\` — instantiate prefabs or model assets in scene`

- `Plugin~/agents/unity-ui-specialist.md:18`
  - Before: `- **Add Asset**: \`add-asset-to-scene\` — attach UI to GameObjects`
  - After: `- **Add Asset**: \`prefab-instantiate\` — instantiate UI prefabs or attach to GameObjects`

- `Plugin~/agents/unity-addressables-specialist.md:22`
  - Before: `- **Add Asset**: \`add-asset-to-scene\` — instantiate addressable assets`
  - After: `- **Add Asset**: \`prefab-instantiate\` — instantiate addressable prefabs / assets`

- `Plugin~/skills/prototype/SKILL.md:15`
  - Before: `   - Use MCP tools to set up scene quickly (batch-execute, add-asset-to-scene)`
  - After: `   - Use MCP tools to set up scene quickly (batch-execute, prefab-instantiate)`

**Risks for Group C:**
- Backward compat: hard break — `add-asset-to-scene` tool name disappears. Per review Section 3, clean break confirmed.
- Build: `prefab-instantiate` signature break (3 new params with defaults — additive but new positions matter for ordered callers). Existing MCP callers using named args are unaffected; positional callers passing `parentPath` as the 6th positional arg will now bind that value to `rotX` instead. This is a known break per review box 3.
- Cross-domain: the 9 consumer files reference the old tool name. Edited as part of this group.
- Test gap: no automated tests.
- Documentation drift: `docs/internal/post-v2.0-backlog.md`, `roadmap.md`, `.claude/state/audit-batch-progress.json`, audit files in `.claude/reports/audits/` still reference `AddAssetToScene` class name and `add-asset-to-scene` tool name as a historical record. NOT edited under this plan (out of scope per task constraint 3 / §0.5). Flag for Ramon to update post-merge.

---

## 5. Change Group D — `prefab-get-info` enrichment

**Findings addressed:** A7 (depth limit + format documentation), G3 (nested-prefab annotation). Folded in from E4: `isVariant` flag in output.

**Rationale:** Single tool, contained scope, fully additive (new optional param with back-compat default, output gains a field without removing any). Closes the read-side observability gap that was on track to become a separate `prefab-is-variant` tool (avoided per E4).

**Definition of done:**
- `prefab-get-info` accepts a new `int maxDepth = -1` parameter (-1 = unlimited; existing behavior preserved when omitted).
- Output begins with a header line that documents the format and includes `isVariant: true/false`.
- `AppendHierarchy` prefixes nested-prefab-root rows with `[nested-prefab]`.
- `[Description]` updated to document `maxDepth`, the plain-text output format, and `isVariant`.
- Project compiles cleanly.

**Dependencies:** None.

### Files Touched

- `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs` — all changes.

### Change D.1 — Add `maxDepth` param + extend description (A7)

**Type:** additive param + description change

**File:** `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs`

**Before (line 25):**
```csharp
[Description("Loads a Prefab asset and returns its type, full hierarchy, and all components on each GameObject.")]
```

**After:**
```csharp
[Description("Loads a Prefab asset and returns its type, full hierarchy (with nested-prefab annotations), and all components on each GameObject. " + "Output is plain text with one header block followed by an indented hierarchy. Each hierarchy line has the form '[name] active=... components=[Comp1, Comp2, ...]' and is prefixed '[nested-prefab]' when the GameObject is a nested prefab instance root.")]
```

(Group H will append a final cross-link sentence to this string.)

**Before (lines 26–28 — signature):**
```csharp
public ToolResponse GetInfo(
    [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath
)
```

**After:**
```csharp
public ToolResponse GetInfo(
    [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath,
    [Description("Maximum hierarchy depth to traverse. -1 means unlimited (default; preserves existing behavior). 0 prints the root only.")] int maxDepth = -1
)
```

**XML doc updates (lines 16–22):**

**Before:**
```csharp
/// <summary>
/// Loads a Prefab asset and returns its hierarchy, components, and prefab type metadata.
/// </summary>
/// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
/// <returns>
/// A <see cref="ToolResponse"/> with the prefab hierarchy and component list,
/// or an error when the asset cannot be loaded.
/// </returns>
```

**After:**
```csharp
/// <summary>
/// Loads a Prefab asset and returns its type, hierarchy (with nested-prefab annotations),
/// components, and an isVariant flag.
/// </summary>
/// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
/// <param name="maxDepth">Maximum hierarchy depth to traverse. -1 = unlimited; 0 = root only.</param>
/// <returns>
/// A <see cref="ToolResponse"/> with the prefab hierarchy, component list, isVariant flag,
/// and nested-prefab annotations, or an error when the asset cannot be loaded.
/// </returns>
```

### Change D.2 — Add `isVariant` to output + propagate `maxDepth` to traversal (folded from E4)

**Type:** behavior change inside the method body

**File:** `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs`

**Before (lines 49–58 — header construction and traversal call):**
```csharp
PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(prefab);

var sb = new StringBuilder();
sb.AppendLine($"Prefab: {prefabPath}");
sb.AppendLine($"  Name:        {prefab.name}");
sb.AppendLine($"  PrefabType:  {prefabType}");

sb.AppendLine("  Hierarchy:");
AppendHierarchy(prefab.transform, sb, 1);

return ToolResponse.Text(sb.ToString());
```

**After:**
```csharp
PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(prefab);
bool isVariant = prefabType == PrefabAssetType.Variant;

var sb = new StringBuilder();
sb.AppendLine($"Prefab: {prefabPath}");
sb.AppendLine($"  Name:        {prefab.name}");
sb.AppendLine($"  PrefabType:  {prefabType}");
sb.AppendLine($"  isVariant:   {(isVariant ? "true" : "false")}");

sb.AppendLine("  Hierarchy:");
AppendHierarchy(prefab.transform, sb, 1, maxDepth);

return ToolResponse.Text(sb.ToString());
```

### Change D.3 — `AppendHierarchy` with depth bound + nested-prefab annotation (A7 + G3)

**Type:** helper signature change + body extension

**File:** `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs`

**Before (lines 67–100 — `AppendHierarchy` method):**
```csharp
/// <summary>
/// Recursively appends the transform hierarchy and component list to <paramref name="sb"/>.
/// </summary>
/// <param name="t">Transform to start from.</param>
/// <param name="sb">Target string builder.</param>
/// <param name="depth">Current indentation depth.</param>
private static void AppendHierarchy(Transform t, StringBuilder sb, int depth)
{
    string indent = new(' ', depth * 2);
    var components = t.GetComponents<UnityEngine.Component>();
    var compNames = new StringBuilder();

    for (int i = 0; i < components.Length; i++)
    {
        if (components[i] == null)
        {
            continue;
        }

        if (i > 0)
        {
            compNames.Append(", ");
        }

        compNames.Append(components[i].GetType().Name);
    }

    sb.AppendLine($"{indent}[{t.name}] active={t.gameObject.activeSelf}  components=[{compNames}]");

    for (int ci = 0; ci < t.childCount; ci++)
    {
        AppendHierarchy(t.GetChild(ci), sb, depth + 1);
    }
}
```

**After:**
```csharp
/// <summary>
/// Recursively appends the transform hierarchy and component list to <paramref name="sb"/>,
/// prefixing nested-prefab-root rows with "[nested-prefab]" and stopping traversal at
/// <paramref name="maxDepth"/> when that value is non-negative.
/// </summary>
/// <param name="t">Transform to start from.</param>
/// <param name="sb">Target string builder.</param>
/// <param name="depth">Current indentation depth (1 = direct child of the printed root).</param>
/// <param name="maxDepth">Maximum depth to traverse. -1 = unlimited.</param>
private static void AppendHierarchy(Transform t, StringBuilder sb, int depth, int maxDepth)
{
    string indent = new(' ', depth * 2);
    var components = t.GetComponents<UnityEngine.Component>();
    var compNames = new StringBuilder();

    for (int i = 0; i < components.Length; i++)
    {
        if (components[i] == null)
        {
            continue;
        }

        if (i > 0)
        {
            compNames.Append(", ");
        }

        compNames.Append(components[i].GetType().Name);
    }

    bool isNestedPrefabRoot = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);
    string nestedTag = isNestedPrefabRoot ? "[nested-prefab] " : string.Empty;

    sb.AppendLine($"{indent}{nestedTag}[{t.name}] active={t.gameObject.activeSelf}  components=[{compNames}]");

    if (maxDepth >= 0 && depth >= maxDepth)
    {
        if (t.childCount > 0)
        {
            sb.AppendLine($"{indent}  ... ({t.childCount} child{(t.childCount == 1 ? string.Empty : "ren")} omitted at maxDepth={maxDepth})");
        }

        return;
    }

    for (int ci = 0; ci < t.childCount; ci++)
    {
        AppendHierarchy(t.GetChild(ci), sb, depth + 1, maxDepth);
    }
}
```

Notes:
- `PrefabUtility.IsAnyPrefabInstanceRoot` returns false on the loaded prefab asset's outer root (which is the asset itself, not an *instance* of a prefab in some other context). It correctly flags nested-prefab roots inside a prefab asset's hierarchy, which is what G3 needs.
- The "(N children omitted)" tail when traversal truncates makes the depth bound visible to the LLM rather than silently dropping subtrees.

**Risks for Group D:**
- Backward compat: additive only. `maxDepth = -1` preserves existing unbounded behavior. Output gains an `isVariant` line and `[nested-prefab]` tags but loses nothing — LLM consumers that parse line-by-line stay valid.
- Build: zero risk. `PrefabUtility.IsAnyPrefabInstanceRoot` exists across Unity 6000.0+.
- Cross-domain: none.
- Test gap: no automated tests.

---

## 6. Change Group E — Headless workflow extensions

**Findings addressed:** G4 (`set-component-field` action on `prefab-modify-contents`), G6 (`prefab-unpack-instance` new tool). Per review E6.

**Rationale:** Two distinct capability gaps that both extend the headless prefab workflow surface. G4 follows the action-dispatch commitment from E2 (6th action on `prefab-modify-contents`); G6 is a brand-new tool wrapping `PrefabUtility.UnpackPrefabInstance`. Both fully isolated.

**Definition of done:**
- `prefab-modify-contents` accepts `action = "set-component-field"` and the new params required for it (`fieldName`, plus a discriminated set of value params).
- The new action supports primitives (`int`, `float`, `bool`, `string`, `Vector2/3/4`, `Color`, `Quaternion`, `enum`) and `ObjectReference` (other Unity Object). Other field types return a clear error.
- The implementation uses `SerializedObject` + `FindProperty(fieldName)` + type-specific setter + `ApplyModifiedPropertiesWithoutUndo`.
- A `// TODO(v2.1.x):` comment block cites the post-v2.0 backlog's Object/SO/Component generic-modifier triangle.
- `prefab-unpack-instance` exists as a new tool wrapping `PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction)` with `unpackMode = "outermost" | "completely"`.
- `prefab-unpack-instance` uses `Tool_Transform.FindGameObject(instanceId, objectPath)` for lookup.
- Project compiles cleanly.

**Dependencies:** Land Group B first (which establishes the empty-action validation pattern + 5-action error message — Group E expands that message to 6 actions).

### Files Touched

- `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs` — G4 action add.
- `Editor/Tools/Prefab/Tool_Prefab.UnpackInstance.cs` — new file for G6.

### Change E.1 — Add `set-component-field` as 6th action on `prefab-modify-contents` (G4)

**Type:** new action on existing action-dispatched tool

**File:** `Editor/Tools/Prefab/Tool_Prefab.ModifyContents.cs`

**Description update** — extend the method-level `[Description]` (post-Group-A wording) to enumerate the 6th action. The string built by Group A had this final fragment:

```
... 'set-active' uses isActive. Changes are saved back to disk immediately.
```

Group E inserts the 6th action *before* the "Changes are saved" sentence:

**After (Group E):**
```csharp
[Description("Modifies the contents of a Prefab asset without entering Prefab Mode (headless one-shot edit; auto-saves on success). " + "For multi-step interactive edits across Component/Transform/GameObject tools, use 'prefab-open' / 'prefab-save' instead. " + "Actions and the params each one uses: " + "'set-position' uses posX/posY/posZ; " + "'add-component' uses componentType; " + "'remove-component' uses componentType; " + "'delete-child' uses deleteChild; " + "'set-active' uses isActive; " + "'set-component-field' uses componentType + fieldName + one of (fieldValueString / fieldValueInt / fieldValueFloat / fieldValueBool / fieldValueObject). " + "Changes are saved back to disk immediately.")]
```

**Signature change** — add new parameters at the end of the existing signature. The required + value params come after `isActive` (which Group B turned into a string). The full post-E signature is:

```csharp
public ToolResponse ModifyContents(
    [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath,
    [Description("Child path relative to Prefab root (e.g. 'Body/Head'). Empty for root. Ignored for 'delete-child' action (use 'deleteChild' instead).")] string targetChild = "",
    [Description("Action to perform on the prefab contents. Required. One of: 'set-position', 'add-component', 'remove-component', 'delete-child', 'set-active', 'set-component-field'. Empty returns an error listing the valid values.")] string action = "",
    [Description("X position for set-position. Default 0.")] float posX = 0f,
    [Description("Y position for set-position. Default 0.")] float posY = 0f,
    [Description("Z position for set-position. Default 0.")] float posZ = 0f,
    [Description("Component type name for 'add-component', 'remove-component', or 'set-component-field' (e.g. 'Rigidbody').")] string componentType = "",
    [Description("Relative child path to destroy. Only used for 'delete-child' action.")] string deleteChild = "",
    [Description("Active state for 'set-active' action. One of 'true', 'false', or '' (skip). Default ''. Required for 'set-active'; ignored for all other actions.")] string isActive = "",
    [Description("Field name on the target component for 'set-component-field' (case-sensitive; matches the serialized property name). Required for 'set-component-field'.")] string fieldName = "",
    [Description("String value for 'set-component-field' when the field is a string, Vector2/3/4 (\"x,y[,z[,w]]\"), Color (\"r,g,b[,a]\"), Quaternion (\"x,y,z,w\"), or enum (literal name). Empty for non-string fields.")] string fieldValueString = "",
    [Description("Int value for 'set-component-field' when the field is an int or enum (numeric). Sentinel -2147483648 (int.MinValue) means 'not provided'.")] int fieldValueInt = int.MinValue,
    [Description("Float value for 'set-component-field' when the field is a float. Sentinel float.NegativeInfinity means 'not provided'.")] float fieldValueFloat = float.NegativeInfinity,
    [Description("Bool value for 'set-component-field' when the field is a bool. One of 'true', 'false', or '' (not provided).")] string fieldValueBool = "",
    [Description("Object-reference asset path for 'set-component-field' when the field is a Unity Object reference (e.g. 'Assets/Materials/Red.mat'). Empty means 'not provided'.")] string fieldValueObject = ""
)
```

Note the discriminated value parameters: only one of `fieldValueString` / `fieldValueInt` / `fieldValueFloat` / `fieldValueBool` / `fieldValueObject` should be supplied per call. The handler validates this and returns a clear error if zero or multiple are set. The float sentinel uses `float.NegativeInfinity` (a value not realistically used as a component field) so callers can still legitimately pass `0f` or any large finite negative; the int sentinel uses `int.MinValue` for the same reason.

**Body change** — add a new `case "set-component-field":` branch inside the `switch (actionNorm)` block, between the `"delete-child"` and `"set-active"` cases (or anywhere — order doesn't matter at runtime). Use this body:

```csharp
case "set-component-field":
{
    if (string.IsNullOrWhiteSpace(componentType))
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("componentType is required for 'set-component-field'.");
    }

    if (string.IsNullOrWhiteSpace(fieldName))
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("fieldName is required for 'set-component-field'.");
    }

    System.Type? type = FindTypeByName(componentType);

    if (type == null)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error($"Component type '{componentType}' not found.");
    }

    UnityEngine.Component? component = target.gameObject.GetComponent(type);

    if (component == null)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error($"Component '{componentType}' not found on '{target.name}'. Add it first with 'add-component'.");
    }

    // TODO(v2.1.x): align this implementation with the canonical pattern resolved by the
    // post-v2.0 backlog's "Object ↔ ScriptableObject ↔ Component generic-modifier triangle"
    // (see docs/internal/post-v2.0-backlog.md → Component consolidation cycle). When the
    // triangle is resolved, extract a shared helper or refactor this block to match the
    // canonical SerializedObject-based field-setter.
    var so = new SerializedObject(component);
    SerializedProperty? prop = so.FindProperty(fieldName);

    if (prop == null)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error($"Field '{fieldName}' not found on '{componentType}'. Use the serialized name (case-sensitive).");
    }

    // Count provided values to enforce "exactly one".
    int providedCount = 0;

    if (!string.IsNullOrEmpty(fieldValueString))
    {
        providedCount++;
    }

    if (fieldValueInt != int.MinValue)
    {
        providedCount++;
    }

    if (!float.IsNegativeInfinity(fieldValueFloat))
    {
        providedCount++;
    }

    if (!string.IsNullOrEmpty(fieldValueBool))
    {
        providedCount++;
    }

    if (!string.IsNullOrEmpty(fieldValueObject))
    {
        providedCount++;
    }

    if (providedCount == 0)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("'set-component-field' requires exactly one of: fieldValueString, fieldValueInt, fieldValueFloat, fieldValueBool, fieldValueObject.");
    }

    if (providedCount > 1)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error("'set-component-field' accepts exactly one value param. Multiple were provided.");
    }

    string? setError = ApplyFieldValue(prop, fieldValueString, fieldValueInt, fieldValueFloat, fieldValueBool, fieldValueObject);

    if (setError != null)
    {
        PrefabUtility.UnloadPrefabContents(root);
        return ToolResponse.Error(setError);
    }

    so.ApplyModifiedPropertiesWithoutUndo();
    sb.AppendLine($"  Set '{componentType}.{fieldName}' on '{target.name}'.");
    break;
}
```

**Helper to add** in the `#region PRIVATE HELPERS` section of the same file (alongside `FindTypeByName`):

```csharp
/// <summary>
/// Applies a value to a SerializedProperty using the first non-empty value param.
/// Supports primitives (int, float, bool, string, Vector2/3/4, Color, Quaternion, enum)
/// and ObjectReference. Returns an error string when the field type is unsupported or
/// the supplied value can't be parsed; returns null on success.
/// </summary>
private static string? ApplyFieldValue(
    SerializedProperty prop,
    string fieldValueString,
    int fieldValueInt,
    float fieldValueFloat,
    string fieldValueBool,
    string fieldValueObject)
{
    switch (prop.propertyType)
    {
        case SerializedPropertyType.Integer:
        {
            if (fieldValueInt != int.MinValue)
            {
                prop.intValue = fieldValueInt;
                return null;
            }

            if (!string.IsNullOrEmpty(fieldValueString) && int.TryParse(fieldValueString, out int parsed))
            {
                prop.intValue = parsed;
                return null;
            }

            return $"Field '{prop.name}' is Integer; provide fieldValueInt or a numeric fieldValueString.";
        }

        case SerializedPropertyType.Float:
        {
            if (!float.IsNegativeInfinity(fieldValueFloat))
            {
                prop.floatValue = fieldValueFloat;
                return null;
            }

            return $"Field '{prop.name}' is Float; provide fieldValueFloat.";
        }

        case SerializedPropertyType.Boolean:
        {
            string norm = fieldValueBool.Trim().ToLowerInvariant();

            if (norm == "true")
            {
                prop.boolValue = true;
                return null;
            }

            if (norm == "false")
            {
                prop.boolValue = false;
                return null;
            }

            return $"Field '{prop.name}' is Boolean; provide fieldValueBool as 'true' or 'false'.";
        }

        case SerializedPropertyType.String:
        {
            prop.stringValue = fieldValueString;
            return null;
        }

        case SerializedPropertyType.Enum:
        {
            if (fieldValueInt != int.MinValue)
            {
                prop.enumValueIndex = fieldValueInt;
                return null;
            }

            if (!string.IsNullOrEmpty(fieldValueString))
            {
                int idx = System.Array.IndexOf(prop.enumNames, fieldValueString);

                if (idx >= 0)
                {
                    prop.enumValueIndex = idx;
                    return null;
                }

                return $"Enum value '{fieldValueString}' not found on field '{prop.name}'. Valid: [{string.Join(", ", prop.enumNames)}].";
            }

            return $"Field '{prop.name}' is Enum; provide fieldValueInt (index) or fieldValueString (literal name).";
        }

        case SerializedPropertyType.Vector2:
        {
            if (TryParseVector(fieldValueString, 2, out float[] parts))
            {
                prop.vector2Value = new Vector2(parts[0], parts[1]);
                return null;
            }

            return $"Field '{prop.name}' is Vector2; provide fieldValueString as 'x,y'.";
        }

        case SerializedPropertyType.Vector3:
        {
            if (TryParseVector(fieldValueString, 3, out float[] parts))
            {
                prop.vector3Value = new Vector3(parts[0], parts[1], parts[2]);
                return null;
            }

            return $"Field '{prop.name}' is Vector3; provide fieldValueString as 'x,y,z'.";
        }

        case SerializedPropertyType.Vector4:
        {
            if (TryParseVector(fieldValueString, 4, out float[] parts))
            {
                prop.vector4Value = new Vector4(parts[0], parts[1], parts[2], parts[3]);
                return null;
            }

            return $"Field '{prop.name}' is Vector4; provide fieldValueString as 'x,y,z,w'.";
        }

        case SerializedPropertyType.Color:
        {
            if (TryParseVector(fieldValueString, 3, out float[] rgb))
            {
                prop.colorValue = new Color(rgb[0], rgb[1], rgb[2], 1f);
                return null;
            }

            if (TryParseVector(fieldValueString, 4, out float[] rgba))
            {
                prop.colorValue = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                return null;
            }

            return $"Field '{prop.name}' is Color; provide fieldValueString as 'r,g,b' or 'r,g,b,a'.";
        }

        case SerializedPropertyType.Quaternion:
        {
            if (TryParseVector(fieldValueString, 4, out float[] parts))
            {
                prop.quaternionValue = new Quaternion(parts[0], parts[1], parts[2], parts[3]);
                return null;
            }

            return $"Field '{prop.name}' is Quaternion; provide fieldValueString as 'x,y,z,w'.";
        }

        case SerializedPropertyType.ObjectReference:
        {
            if (string.IsNullOrEmpty(fieldValueObject))
            {
                return $"Field '{prop.name}' is ObjectReference; provide fieldValueObject as an asset path (e.g. 'Assets/Materials/Red.mat').";
            }

            Object? assetObj = AssetDatabase.LoadAssetAtPath<Object>(fieldValueObject);

            if (assetObj == null)
            {
                return $"Object reference asset not found at '{fieldValueObject}'.";
            }

            prop.objectReferenceValue = assetObj;
            return null;
        }

        default:
        {
            return $"Field '{prop.name}' has unsupported type '{prop.propertyType}'. Supported: Integer, Float, Boolean, String, Enum, Vector2/3/4, Color, Quaternion, ObjectReference.";
        }
    }
}

/// <summary>
/// Parses a comma-separated float vector. Returns false when the count doesn't match.
/// </summary>
private static bool TryParseVector(string input, int expectedCount, out float[] parts)
{
    parts = System.Array.Empty<float>();

    if (string.IsNullOrWhiteSpace(input))
    {
        return false;
    }

    string[] tokens = input.Split(',');

    if (tokens.Length != expectedCount)
    {
        return false;
    }

    var result = new float[expectedCount];

    for (int i = 0; i < expectedCount; i++)
    {
        if (!float.TryParse(tokens[i].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result[i]))
        {
            return false;
        }
    }

    parts = result;
    return true;
}
```

**`using` directives:** the file already has `using UnityEditor;` and `using UnityEngine;`. No new directives required (`SerializedObject`, `SerializedProperty`, `SerializedPropertyType`, `AssetDatabase` are all in `UnityEditor`).

**Error-message update for the empty `action` validation added in Group B** — the error string now lists 6 actions:

**Group E updates (in place of Group B's 5-action string at the empty-action guard added near line 96):**
```csharp
return ToolResponse.Error("'action' is required. Valid values: set-position, add-component, remove-component, delete-child, set-active, set-component-field.");
```

**Also update the existing `default:` case error string (currently line 194):**

**Before (line 194):**
```csharp
return ToolResponse.Error($"Unknown action '{action}'. Valid values: set-position, add-component, " + "remove-component, delete-child, set-active.");
```

**After:**
```csharp
return ToolResponse.Error($"Unknown action '{action}'. Valid values: set-position, add-component, remove-component, delete-child, set-active, set-component-field.");
```

**XML doc** — extend the bullet list in the method-level XML summary (after the `set-active` bullet, line 26):

```csharp
///   <item><c>set-component-field</c> — sets a component's serialized field via SerializedObject. Requires <paramref name="componentType"/>, <paramref name="fieldName"/>, and exactly one of <paramref name="fieldValueString"/> / <paramref name="fieldValueInt"/> / <paramref name="fieldValueFloat"/> / <paramref name="fieldValueBool"/> / <paramref name="fieldValueObject"/>.</item>
```

And add the new `<param>` tags after the existing ones:

```csharp
/// <param name="fieldName">Serialized property name on the target component (case-sensitive).</param>
/// <param name="fieldValueString">String value for set-component-field (string, vector, color, quaternion, enum name).</param>
/// <param name="fieldValueInt">Int value for set-component-field (int field or enum index). Sentinel int.MinValue = not provided.</param>
/// <param name="fieldValueFloat">Float value for set-component-field. Sentinel float.NegativeInfinity = not provided.</param>
/// <param name="fieldValueBool">Bool value ("true" / "false" / "") for set-component-field.</param>
/// <param name="fieldValueObject">Asset path of an Object reference for set-component-field. Empty = not provided.</param>
```

### Change E.2 — New `prefab-unpack-instance` tool (G6)

**Type:** new tool (new partial file)

**File:** `Editor/Tools/Prefab/Tool_Prefab.UnpackInstance.cs` (NEW)

**Maps to Unity API:** `PrefabUtility.UnpackPrefabInstance(GameObject, PrefabUnpackMode, InteractionMode)`.

**Full file content:**

```csharp
#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Unpacks a prefab instance in the current scene, severing its prefab connection
        /// so subsequent edits no longer track the source prefab.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the prefab instance root. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the prefab instance root. Used when instanceId is 0.</param>
        /// <param name="unpackMode">Unpack mode: "outermost" (default) unpacks only the outermost prefab; "completely" unpacks all nested prefabs.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the unpack, or an error.</returns>
        [McpTool("prefab-unpack-instance", Title = "Prefab / Unpack Instance")]
        [Description("Unpacks a prefab instance in the current scene, severing its prefab connection. After unpack, the GameObject is a plain scene object with no link back to the prefab asset. Use 'outermost' (default) to unpack only the outermost prefab and keep nested prefabs intact, or 'completely' to unpack all nested prefabs in the hierarchy.")]
        public ToolResponse UnpackInstance(
            [Description("Instance ID of the prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the prefab instance root (e.g. 'World/Enemies/Goblin'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Unpack mode. One of 'outermost' (default; unpacks only the outermost prefab) or 'completely' (unpacks all nested prefabs).")] string unpackMode = "outermost"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Only prefab instance roots can be unpacked.");
                }

                string norm = unpackMode.Trim().ToLowerInvariant();
                PrefabUnpackMode mode;

                if (norm == "outermost")
                {
                    mode = PrefabUnpackMode.OutermostRoot;
                }
                else if (norm == "completely")
                {
                    mode = PrefabUnpackMode.Completely;
                }
                else if (string.IsNullOrEmpty(norm))
                {
                    return ToolResponse.Error("unpackMode is required. Valid values: 'outermost', 'completely'.");
                }
                else
                {
                    return ToolResponse.Error($"Unknown unpackMode '{unpackMode}'. Valid values: 'outermost', 'completely'.");
                }

                PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);

                return ToolResponse.Text($"Unpacked prefab instance '{go.name}' (mode: {mode}).");
            });
        }

        #endregion
    }
}
```

Notes:
- No class-level `<summary>` — the `[McpToolType]` summary lives only in `Close.cs`.
- Uses `Tool_Transform.FindGameObject(instanceId, objectPath)` per the domain convention.
- Validation order: GameObject existence → prefab-instance-root check → unpackMode validation. Empty `unpackMode` produces an error (per task constraint 7), but since the param has a default of `"outermost"`, a caller would have to explicitly pass `""` to hit that branch.

**Risks for Group E:**
- Backward compat: additive only. G4 adds a new action and new params (all with defaults); existing actions are unchanged. G6 is a brand-new tool.
- Build: `SerializedPropertyType` enum members used (`Integer`, `Float`, `Boolean`, `String`, `Enum`, `Vector2`, `Vector3`, `Vector4`, `Color`, `Quaternion`, `ObjectReference`) all exist on Unity 6000.0+. `PrefabUnpackMode.OutermostRoot` / `Completely` and `PrefabUtility.UnpackPrefabInstance` are stable APIs since Unity 2018.3.
- Cross-domain: cite-only reference to `Tool_Transform.FindGameObject`.
- Test gap: no automated tests.

---

## 7. Change Group F — Prefab variants

**Findings addressed:** G1 (per review E4).

**Rationale:** Adds the missing prefab-variant creation surface. The read side (`isVariant`) is already covered by Group D's enrichment of `prefab-get-info`, so there's no separate `prefab-is-variant` tool — variant query is folded into the read tool.

**Definition of done:**
- New `prefab-create-variant(instanceId, objectPath, savePath)` tool exists.
- Validates that the target is a prefab instance root via `PrefabUtility.IsAnyPrefabInstanceRoot`.
- Saves via `PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool success)`. Unity auto-creates a Variant because the input is a prefab instance.
- The class-level `<summary>` in `Close.cs` is updated to mention `prefab-create-variant`.
- Project compiles cleanly.

**Dependencies:** None hard. Land after Group D so the `prefab-get-info` `isVariant` flag is in place and callers can verify the result of the create.

### Files Touched

- Created: `Editor/Tools/Prefab/Tool_Prefab.CreateVariant.cs`.
- Modified: `Editor/Tools/Prefab/Tool_Prefab.Close.cs` (class-level `<summary>` only).

### Change F.1 — New `prefab-create-variant` tool

**Type:** new tool (new partial file)

**File:** `Editor/Tools/Prefab/Tool_Prefab.CreateVariant.cs` (NEW)

**Maps to Unity API:** `PrefabUtility.SaveAsPrefabAsset(GameObject instance, string assetPath, out bool success)`. When `instance` is a prefab instance root, Unity automatically creates a Variant asset whose source is the instance's prefab.

**Full file content:**

```csharp
#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a Prefab Variant asset from a scene prefab instance. The target GameObject
        /// must already be a prefab instance root (it has a source prefab); Unity then
        /// automatically marks the new asset as a Variant of that source.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the scene prefab instance root. Pass 0 to use objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the scene prefab instance root. Used when instanceId is 0.</param>
        /// <param name="savePath">Asset path to save the new variant (e.g. 'Assets/Prefabs/Enemy_Boss.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the variant name and saved path, or an error.</returns>
        [McpTool("prefab-create-variant", Title = "Prefab / Create Variant")]
        [Description("Creates a Prefab Variant asset from a scene prefab instance. The target GameObject must already be a prefab instance root; Unity auto-marks the new asset as a Variant of the source prefab. To create a regular (non-variant) prefab from a scene GameObject, use 'prefab-create' instead.")]
        public ToolResponse CreateVariant(
            [Description("Instance ID of the scene prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the scene prefab instance root (e.g. 'World/Enemies/BossInstance'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Asset path to save the new variant (e.g. 'Assets/Prefabs/Enemy_Boss.prefab').")] string savePath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Variants can only be created from existing prefab instances.");
                }

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    return ToolResponse.Error("savePath is required (e.g. 'Assets/Prefabs/Enemy_Variant.prefab').");
                }

                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/' (e.g. 'Assets/Prefabs/Enemy_Variant.prefab').");
                }

                string folder = System.IO.Path.GetDirectoryName(savePath) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
                GameObject? variant = PrefabUtility.SaveAsPrefabAsset(go, savePath, out bool success);

                if (!success || variant == null)
                {
                    return ToolResponse.Error($"Failed to create prefab variant at '{savePath}'.");
                }

                return ToolResponse.Text($"Created prefab variant '{variant.name}' at {savePath}.");
            });
        }

        #endregion
    }
}
```

Notes:
- No class-level `<summary>` — single-summary rule.
- Reuses the savePath-folder-creation pattern from `Tool_Prefab.Create.cs` (Group A's Create.cs file remains the canonical reference).
- `savePath` is required here (no implicit default), because the LLM has no good basis to invent a name for a variant — the user typically wants a distinct name like `Enemy_Boss.prefab`.

### Change F.2 — Update class-level `<summary>` in `Close.cs`

**Type:** XML doc update (no code change)

**File:** `Editor/Tools/Prefab/Tool_Prefab.Close.cs`

**Before (lines 10–13):**
```csharp
/// <summary>
/// MCP tools for creating, opening, editing, saving, instantiating, and closing Unity Prefabs.
/// Covers prefab asset creation, prefab stage management, content modification, and scene instantiation.
/// </summary>
```

**After (after Group F):**
```csharp
/// <summary>
/// MCP tools for creating, opening, editing, saving, instantiating, and closing Unity Prefabs.
/// Covers prefab asset creation (including variants), prefab stage management, content modification,
/// scene instantiation, and unpack.
/// </summary>
```

(Group G will further amend this string to mention overrides; the Group F "After" stops at the variant + unpack additions.)

**Risks for Group F:**
- Backward compat: additive — new tool, no existing surface change.
- Build: `PrefabUtility.SaveAsPrefabAsset(GameObject, string, out bool)` is stable since Unity 2018.3.
- Cross-domain: cite-only `Tool_Transform.FindGameObject`.
- Test gap: no automated tests.

---

## 8. Change Group G — Prefab overrides (action-dispatched)

**Findings addressed:** G2 (per review E5).

**Rationale:** Adds the override-management surface that was the audit's #1 priority finding. Action-dispatched (`prefab-override` with `action ∈ {list, apply-instance, revert-instance}`) per the in-domain precedent (`prefab-modify-contents`) and project precedent (`Tool_Animation.ConfigureController`). Per-property actions are deferred to v2.1.x.

**Definition of done:**
- New `prefab-override` tool with `action` parameter and 3 supported actions.
- All actions validate the target is a prefab instance root via `PrefabUtility.IsAnyPrefabInstanceRoot`.
- `list` action uses `PrefabUtility.HasPrefabInstanceAnyOverrides` as a cheap pre-check, then `PrefabUtility.GetObjectOverrides` for the detailed list.
- `apply-instance` uses `PrefabUtility.ApplyPrefabInstance(InteractionMode.AutomatedAction)`.
- `revert-instance` uses `PrefabUtility.RevertPrefabInstance(InteractionMode.AutomatedAction)`.
- Any warning paths use `Debug.LogWarning` (NOT `McpLogger.Warning` which does not exist).
- The class-level `<summary>` in `Close.cs` is updated to mention overrides.
- Project compiles cleanly.

**Dependencies:** None hard. Land after Group F so the `Close.cs` summary updates compose (Group G amends the string Group F produced).

### Files Touched

- Created: `Editor/Tools/Prefab/Tool_Prefab.Override.cs`.
- Modified: `Editor/Tools/Prefab/Tool_Prefab.Close.cs` (class-level `<summary>` only).

### Change G.1 — New `prefab-override` tool

**Type:** new tool (new partial file, action-dispatched)

**File:** `Editor/Tools/Prefab/Tool_Prefab.Override.cs` (NEW)

**Maps to Unity API:**
- `PrefabUtility.IsAnyPrefabInstanceRoot(GameObject)` — guard.
- `PrefabUtility.HasPrefabInstanceAnyOverrides(GameObject, bool)` — cheap pre-check for `list`.
- `PrefabUtility.GetObjectOverrides(GameObject, bool includeDefaultOverrides = false)` — enumerated override list.
- `PrefabUtility.ApplyPrefabInstance(GameObject, InteractionMode)` — apply.
- `PrefabUtility.RevertPrefabInstance(GameObject, InteractionMode)` — revert.

**Full file content:**

```csharp
#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Manages overrides on a prefab instance in the scene. Supports listing the current
        /// overrides, applying them back to the prefab asset, or reverting them.
        /// Per-property actions (apply-property, revert-property) and added/removed
        /// component/gameobject deltas are deferred to a later cycle.
        /// </summary>
        /// <param name="action">One of "list", "apply-instance", "revert-instance".</param>
        /// <param name="instanceId">Unity instance ID of the prefab instance root. Pass 0 to use objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the prefab instance root. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> describing the result, or an error.</returns>
        [McpTool("prefab-override", Title = "Prefab / Override")]
        [Description("Manages overrides on a scene prefab instance. Actions: 'list' enumerates the instance's current property overrides (cheap pre-check via HasPrefabInstanceAnyOverrides); 'apply-instance' applies all overrides back to the source prefab asset; 'revert-instance' discards all overrides on the instance. Per-property apply/revert and added/removed component/gameobject deltas are deferred to a later cycle.")]
        public ToolResponse Override(
            [Description("Action to perform. Required. One of 'list', 'apply-instance', 'revert-instance'. Empty returns an error listing the valid values.")] string action = "",
            [Description("Instance ID of the prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the prefab instance root (e.g. 'World/Enemies/Goblin'). Used when instanceId is 0.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(action))
                {
                    return ToolResponse.Error("'action' is required. Valid values: 'list', 'apply-instance', 'revert-instance'.");
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Only prefab instance roots can be targets of override actions.");
                }

                string actionNorm = action.Trim().ToLowerInvariant();

                switch (actionNorm)
                {
                    case "list":
                    {
                        bool hasAny = PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

                        if (!hasAny)
                        {
                            return ToolResponse.Text($"Prefab instance '{go.name}' has no overrides.");
                        }

                        var overrides = PrefabUtility.GetObjectOverrides(go, false);

                        var sb = new StringBuilder();
                        sb.AppendLine($"Overrides on prefab instance '{go.name}':");
                        sb.AppendLine($"  Total: {overrides.Count}");

                        for (int i = 0; i < overrides.Count; i++)
                        {
                            var o = overrides[i];
                            string targetName = o.instanceObject != null ? o.instanceObject.name : "<null>";
                            string targetType = o.instanceObject != null ? o.instanceObject.GetType().Name : "<null>";
                            sb.AppendLine($"  [{i}] {targetType} on '{targetName}'");
                        }

                        return ToolResponse.Text(sb.ToString());
                    }

                    case "apply-instance":
                    {
                        try
                        {
                            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                            return ToolResponse.Text($"Applied all overrides on '{go.name}' back to the source prefab.");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[prefab-override apply-instance] {ex.Message}");
                            return ToolResponse.Error($"ApplyPrefabInstance failed on '{go.name}': {ex.Message}");
                        }
                    }

                    case "revert-instance":
                    {
                        try
                        {
                            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
                            return ToolResponse.Text($"Reverted all overrides on '{go.name}'.");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[prefab-override revert-instance] {ex.Message}");
                            return ToolResponse.Error($"RevertPrefabInstance failed on '{go.name}': {ex.Message}");
                        }
                    }

                    default:
                    {
                        return ToolResponse.Error($"Unknown action '{action}'. Valid values: 'list', 'apply-instance', 'revert-instance'.");
                    }
                }
            });
        }

        #endregion
    }
}
```

Notes:
- No class-level `<summary>` — single-summary rule.
- All warning paths use `Debug.LogWarning` per CLAUDE.md (`McpLogger.Warning` does not exist).
- `PrefabUtility.GetObjectOverrides` returns `List<ObjectOverride>` where `ObjectOverride.instanceObject` is the overridden Unity Object on the scene instance and `coupledOverride` (if non-null) couples to a paired override. We surface only the instance side in `list` for brevity; per-property granularity is the deferred work.
- `apply-instance` and `revert-instance` wrap the API calls in try/catch and log warnings (NOT errors via `McpLogger.Error`) because the user-visible error path is already covered by the `ToolResponse.Error(...)` return. The `Debug.LogWarning` augments the Unity console with stack-relevant context. This matches the CLAUDE.md guidance to use `Debug.LogWarning` for warning-class messages.

### Change G.2 — Update class-level `<summary>` in `Close.cs`

**Type:** XML doc update

**File:** `Editor/Tools/Prefab/Tool_Prefab.Close.cs`

**Before Group G (post-F state):**
```csharp
/// <summary>
/// MCP tools for creating, opening, editing, saving, instantiating, and closing Unity Prefabs.
/// Covers prefab asset creation (including variants), prefab stage management, content modification,
/// scene instantiation, and unpack.
/// </summary>
```

**After Group G:**
```csharp
/// <summary>
/// MCP tools for creating, opening, editing, saving, instantiating, and closing Unity Prefabs.
/// Covers prefab asset creation (including variants), prefab stage management, content modification,
/// scene instantiation, unpack, and override management (list / apply / revert).
/// </summary>
```

**Risks for Group G:**
- Backward compat: additive — new tool, no existing surface change.
- Build: `PrefabUtility.GetObjectOverrides` returns `List<ObjectOverride>` from `UnityEditor.SceneManagement` namespace? No — confirmed: `PrefabUtility.GetObjectOverrides` lives in `UnityEditor` (the same namespace as `PrefabUtility`); `ObjectOverride` is in `UnityEditor.SceneManagement`. The current `using` directives in the new file include `using UnityEditor;` which exposes `PrefabUtility`, but accessing `ObjectOverride.instanceObject` as a member through the returned list works without an explicit using for the `SceneManagement` namespace because the type is only used implicitly (we never name it). If the consolidator hits a compile error here, add `using UnityEditor.SceneManagement;`. Flagged but not preemptively added — `var o = overrides[i];` should let type inference handle it.
- Cross-domain: cite-only `Tool_Transform.FindGameObject`.
- Test gap: no automated tests.

---

## 9. Change Group H — Discoverability cross-link

**Findings addressed:** G5 (per review E7).

**Rationale:** The Asset domain's `asset-find` already covers prefab enumeration via `t:Prefab` filter. The gap is discoverability, not capability. Two description appends solve it without duplicated enumeration code.

**Definition of done:**
- `prefab-get-info` `[Description]` ends with: "To enumerate or search for prefabs by path/filter, use 'asset-find' with t:Prefab."
- `prefab-instantiate` `[Description]` ends with the same sentence.
- Project compiles cleanly.

**Dependencies:** Should land after Group C (which restructures `prefab-instantiate`'s description) and Group D (which restructures `prefab-get-info`'s description) so the cross-link sentence appends to the final post-C / post-D wording rather than the original audit-time wording.

### Files Touched

- `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs` — description suffix.
- `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs` — description suffix.

### Change H.1 — `prefab-instantiate` cross-link

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.Instantiate.cs`

**After Group C the `[Description]` ends with `...optional parent GameObject (top-level name or hierarchy path).` Group H appends the cross-link sentence:**

**Before Group H (post-Group-C state, line 26):**
```csharp
[Description("Instantiates a prefab or other GameObject asset (FBX, model, plain .asset GameObject) into the active scene at a specified position and rotation. " + "When the asset is a prefab, creates a linked instance via PrefabUtility.InstantiatePrefab; otherwise falls back to Object.Instantiate. " + "Supports world position, world rotation (Euler), optional name override, and an optional parent GameObject (top-level name or hierarchy path).")]
```

**After Group H:**
```csharp
[Description("Instantiates a prefab or other GameObject asset (FBX, model, plain .asset GameObject) into the active scene at a specified position and rotation. " + "When the asset is a prefab, creates a linked instance via PrefabUtility.InstantiatePrefab; otherwise falls back to Object.Instantiate. " + "Supports world position, world rotation (Euler), optional name override, and an optional parent GameObject (top-level name or hierarchy path). " + "To enumerate or search for prefabs by path/filter, use 'asset-find' with t:Prefab.")]
```

### Change H.2 — `prefab-get-info` cross-link

**Type:** description-only

**File:** `Editor/Tools/Prefab/Tool_Prefab.GetInfo.cs`

**Before Group H (post-Group-D state, line 25):**
```csharp
[Description("Loads a Prefab asset and returns its type, full hierarchy (with nested-prefab annotations), and all components on each GameObject. " + "Output is plain text with one header block followed by an indented hierarchy. Each hierarchy line has the form '[name] active=... components=[Comp1, Comp2, ...]' and is prefixed '[nested-prefab]' when the GameObject is a nested prefab instance root.")]
```

**After Group H:**
```csharp
[Description("Loads a Prefab asset and returns its type, full hierarchy (with nested-prefab annotations), and all components on each GameObject. " + "Output is plain text with one header block followed by an indented hierarchy. Each hierarchy line has the form '[name] active=... components=[Comp1, Comp2, ...]' and is prefixed '[nested-prefab]' when the GameObject is a nested prefab instance root. " + "To enumerate or search for prefabs by path/filter, use 'asset-find' with t:Prefab.")]
```

**Risks for Group H:**
- Backward compat: zero (description-only).
- Build: zero risk.
- Cross-domain: cite-only reference to `asset-find` (no Asset-domain edits).

---

## 10. Open Questions For The Consolidator

None. Every decision is locked.

The single soft caveat that the consolidator should be aware of (already documented inline above):

- **`UnityEditor.SceneManagement` using directive on `Override.cs`:** if `var o = overrides[i];` does not resolve cleanly at compile time, add `using UnityEditor.SceneManagement;` to `Editor/Tools/Prefab/Tool_Prefab.Override.cs`. Type inference *should* handle this without the explicit using, but if the build fails specifically on that line, add the directive. This is a mechanical adjustment, not a design decision.

---

## 11. Out Of Scope / Future Audits

Items deferred from this cycle, flagged for later audits or v2.1.x batches:

1. **Per-property override actions** (`apply-property`, `revert-property`) on `prefab-override` — deferred per E5. When added, requires `ApplyObjectOverride(assetPath)` rather than `ApplyPrefabInstance` because of nested-prefab ambiguity.
2. **Added/removed component/gameobject delta actions** on `prefab-override` (`PrefabUtility.GetAddedComponents`, `GetRemovedComponents`, `GetAddedGameObjects`) — deferred per E5.
3. **Variant-aware operations beyond create** — `prefab-create-variant` ships this cycle, but tools that *modify the variant relationship* (re-parent a variant, detect variant chain depth, list variants of a base) are deferred.
4. **`prefab-navigate-nested` tool** — a workflow tool to "open into" a nested prefab from a parent stage. Deferred per E3 Option-3 choice.
5. **`set-component-field` field-type expansion** — this cycle scopes to primitives + ObjectReference. Custom struct, AnimationCurve, AnimationClip-typed fields, generic `List<T>` / `T[]` with complex element types, and Gradient are all explicitly rejected with a clear error. Expand when the Object/SO/Component generic-modifier triangle (post-v2.0 backlog, `docs/internal/post-v2.0-backlog.md` line 171) is resolved.
6. **Object ↔ ScriptableObject ↔ Component generic-modifier triangle** — flagged in `Tool_Prefab.ModifyContents.cs` via a `// TODO(v2.1.x):` comment. The triangle's resolution is a cross-cutting v2.1.x deliverable that will replace or unify the `SerializedObject`-based field-setter scattered across domains. Re-audit `prefab-modify-contents.set-component-field` at that time.
7. **`Tool_Transform.FindGameObject` migration off `EditorUtility.InstanceIDToObject`** — flagged for the Transform audit. The current Prefab code (`Tool_Prefab.Create.cs:37` and the new `CreateVariant.cs`, `UnpackInstance.cs`, `Override.cs`) all route through this helper; once Transform migrates, Prefab gets the upgrade for free.
8. **Audit-history / state-tracking documentation drift** — `.claude/state/audit-batch-progress.json`, `.claude/reports/audits/audit-AddAssetToScene-20260425.md`, `batch-summary-20260425.md`, `docs/internal/post-v2.0-backlog.md`, and `docs/internal/roadmap.md` still reference the `AddAssetToScene` class and `add-asset-to-scene` tool as historical state. Ramon's call whether to update these as part of the post-merge close-out or leave them as point-in-time records.
9. **`prefab-create` `keepConnection = false` consequence** — `PrefabUtility.SaveAsPrefabAsset(go, savePath)` (the disconnected branch) does NOT preserve any link between the scene object and the new prefab. The tool's description (post-Group-A) now reads "Set false only when you want a one-shot snapshot with no link back." which is correct, but no follow-up validation or warning is added. If misuse surfaces in production, a future cycle can add a Unity-console hint when `keepConnection=false` is used inside an active prefab stage (currently no such warning).
10. **`prefab-instantiate` model-import nuance** — non-prefab GameObject assets (FBX, OBJ) instantiated via `Object.Instantiate` create a **disconnected** scene object (no model-prefab connection). Unity supports model prefabs as a distinct asset type (`PrefabAssetType.Model`) which `PrefabUtility.InstantiatePrefab` would handle correctly. The fallback as specified treats anything non-prefab the same way; a more precise implementation could check `PrefabAssetType.Model` and route to `PrefabUtility.InstantiatePrefab` rather than `Object.Instantiate`. Flag for re-audit if model-import workflows surface complaints. The current Group C wording already treats `PrefabAssetType.Model` as "is a prefab" because the guard is `assetType != PrefabAssetType.NotAPrefab`, so model imports DO route through `PrefabUtility.InstantiatePrefab` already. The fallback only fires for `PrefabAssetType.NotAPrefab` — i.e. plain GameObject `.asset` files. **Self-correction noted: the fallback is narrower than the audit's prose suggested.** Documenting here for transparency.

---

## 12. Notes For Ramon Before READY FOR EXECUTION

Items that warrant Ramon's eye before kicking the consolidator off, beyond the locked decisions above:

1. **Consumer-survey scope expansion in Group C:** the survey turned up 9 prompt/agent/skill files referencing `add-asset-to-scene` that all need string substitution. None are non-trivial (all mechanical text edits), but the scope of Group C grew from "1 modify + 3 delete" to "9 modify + 3 delete." This is consistent with task constraint 5's "flag them as deviation candidates" guidance — no design deviation, just scope-of-change deviation. Confirming you're fine with the consolidator touching `Plugin~/agents/*.md`, `Plugin~/skills/prototype/SKILL.md`, and `Server~/prompts/core-system-prompt.md` in this cycle.
2. **`Server~/prompts/core-system-prompt.md` line 53 split:** the current line lists `add-asset-to-scene` under the **Scene** catalog section. Group C removes it from there. The line `**Prefab**: ...` (if it exists in the same file) may already list `prefab-instantiate`; the consolidator must read the file end-to-end to confirm. If `prefab-instantiate` is missing from the Prefab catalog, the consolidator should add it — but this is a small content judgment, not a signature change. The consolidator is empowered to make this call without re-escalating.
3. **`SerializedPropertyType.AnimationCurve` and similar:** the `set-component-field` action's `default:` branch returns a clear error for unsupported types. AnimationCurve, Gradient, BoundsInt, RectInt, LayerMask, and Hash128 all fall into this bucket. If you want any of these supported this cycle, flag now; otherwise they're explicitly deferred per the FUTURE CONSOLIDATION note.
4. **`ObjectReference` resolution by asset path only:** the `fieldValueObject` parameter takes an asset path (e.g. `Assets/Materials/Red.mat`). It does **not** support scene-object references (a component's serialized field referencing another scene GameObject in the same prefab). That's a separate workflow — prefab-internal references are unusual outside of the prefab editing stage, and the headless `LoadPrefabContents` flow doesn't have a clean way to resolve a scene-instance reference back into the loaded prefab contents. Deferring scene-ref support is consistent with the "primitives + ObjectReference" scope from E6. Flagging for visibility.
5. **No automated test coverage:** the Prefab domain has no `.cs` test files (verified via Glob over `Editor/Tests/Tools/Prefab*` returning empty in prior batches — Ramon can confirm or rerun). All validation is manual smoke testing through the MCP surface after the consolidator finishes and `build-validator` confirms compilation. Flag for visibility; not a blocker per the v2.0-priority-window policy.
6. **`Tool_Transform.FindGameObject` still uses deprecated `EditorUtility.InstanceIDToObject`** (under `#pragma warning disable CS0618`). Three new tools added this cycle (`prefab-create-variant`, `prefab-unpack-instance`, `prefab-override`) inherit that path. Stays as-is per constraint 6; flagging because more callsites now ride on that helper, which strengthens the case for the Transform audit migrating it to `EditorUtility.EntityIdToObject` in its own cycle.
7. **`SaveAsPrefabAsset(go, savePath)` vs `SaveAsPrefabAsset(go, savePath, out bool)`:** `Tool_Prefab.Create.cs` uses the two-arg overload (returns null on failure). `Tool_Prefab.CreateVariant.cs` uses the three-arg overload (returns `out bool success`). Both are valid; the explicit `out bool` in CreateVariant is slightly more robust because the API guarantees `success` reflects the operation result. Group F's choice is intentional but inconsistent with Create.cs — Ramon may want a follow-up to unify on the three-arg overload in `Create.cs` (one-line tweak), but that's out of scope unless requested.

---

## 13. Approval

Plan complete. All groups specified with concrete signatures, migration tables, and edge cases. Every decision locked. No open questions for the consolidator.

**Status: READY FOR EXECUTION**
