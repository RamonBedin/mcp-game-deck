# Consolidation Plan — GameObject

**Date:** 2026-04-25
**Planner:** consolidation-planner agent
**Audit input:** `.claude/reports/audits/audit-GameObject-20260425.md`
**Review input:** `.claude/reports/reviews/review-GameObject-20260425.md`
**Status:** ✅ READY FOR EXECUTION

---

## 0. Plan Quality Caveats

**Inputs verified:**
- ✅ Audit file present
- ✅ Review file present
- ✅ Review marked READY FOR PLANNING (Section 7)
- ✅ All 24 audit findings have decisions in review Section 1 (R1–R4, A1–A8, D1–D6, G1–G6)

**Findings included in plan:**
- Accepted: R2, R4, A1, A2, A3, A4, A5, A7, A8, D2, D3, D4, D5, G1, G3
- Accepted with modification: R1, R3, A6, D1, D6, G2, G5
- Excluded (rejected/deferred): G4 (deferred to Component-domain audit), G6 (deferred to Transform-domain audit)

**Constraints applied (from review Section 3):**
- Backward compat: free to break tool names (box 3). No deprecation shims required.
- Code style: CLAUDE.md C# standards (braces on all `if`, no empty catches, `EntityIdToObject` not `InstanceIDToObject`, no `obj?.prop = x`, single XML summary on the partial-class file containing `[McpToolType]`).
- New project-wide convention from E3: nullable boolean params use string sentinel `"true" | "false" | ""` (NOT int tri-state). Migrate `gameobject-update`'s `isActive`/`isStatic` accordingly.
- All tool descriptions must come from `[System.ComponentModel.Description]` on the method (not `toolAttr.Description`).
- Do NOT touch Transform, Component, Selection, or Scene domains beyond cite-only references.
- Do NOT migrate `Tool_Transform.FindGameObject` off `EditorUtility.InstanceIDToObject` — flag for Transform audit. Keep the existing `#pragma warning disable CS0618`.
- Do NOT edit CLAUDE.md (E3 follow-up is Ramon's separate commit).
- Do NOT pull v2.0 features into this cycle.

**Reviewer notes carried forward (from review Section 6):**
- `gameobject-create` currently bypasses the shared `Tool_Transform.FindGameObject` helper (uses raw `GameObject.Find(parentPath)`); when implementing E2's expansion, route the new `parentInstanceId` path through `FindGameObject` to align with the rest of the domain. Same alignment applies to the new `gameobject-create-sprite`.
- The single `[McpToolType]` summary lives in `Tool_GameObject.Create.cs` lines 12–16. New partial files MUST NOT duplicate that summary; existing files already follow the rule. Update the summary in `Create.cs` to mention the two new tools (`gameobject-create-sprite`, `gameobject-set-sibling-index`) and the removal of `gameobject-select`.
- `gameobject-select` deletion is the chosen path (no shim) per review Section 3 note ("Default to deletion unless ... callsite-survey reason to shim"). No callsite survey was requested.
- E3 (string-sentinel boolean convention) is being adopted in this cycle but only enforced on `gameobject-update`. Other domains' bool params stay as plain `bool` until their own audit cycles (per CLAUDE.md follow-up).

---

## 1. Summary

| # | Change Group | Findings | Files Touched | Priority |
|---|--------------|----------|---------------|----------|
| A | Description polish (quick win) | A1, A2, A3, A4, A5, A7, A8, D5, D6, R3 | 7 (modified) | high (cheap) |
| B | `gameobject-create` maximal expansion + new sprite tool | D2, D3, D4, G1, G3, R4 | 2 (1 modified, 1 created) | high |
| C | Sentinel migration on `gameobject-update` | D1 | 1 (modified) | medium |
| D | `gameobject-select` deprecation + cross-tool disambiguation + `gameobject-find` extension | R1, R2, A6, G5 | 4 (1 modified Find, 1 modified MoveRelative, 1 deleted Select, 1 cite-only Transform.Move) | high |
| E | New sibling reorder tool | G2 | 1 (created) | high |

**Recommended order:** A → B → C → D → E

Rationale for ordering:
- **A first:** purely cosmetic, no risk, ships value immediately, removes noise from later diffs.
- **B before C:** B introduces new params on `gameobject-create` using the new string-sentinel convention. Doing B first establishes the pattern before C migrates `gameobject-update`. (Order can be flipped if convenient; no hard dep.)
- **D after C:** D modifies `gameobject-find` (which Group A also touched at the description level). Doing D later avoids A/D conflicting on the same file.
- **E last:** isolated additive change, lowest coupling.

No group has a hard dependency on a later group. Groups can be reviewed and merged independently.

---

## 2. Change Group A — Description polish (quick win)

**Findings addressed:** A1, A2, A3, A4, A5, A7, A8, D5, D6, R3

**Rationale:** Pure description edits on existing `[Description]` attributes and XML doc comments. No signature changes. No behavioral impact. Cheap PR, low risk, lands immediately.

**Definition of done:**
- All 10 findings' description tweaks applied verbatim to the affected `[Description]` attributes.
- Project compiles cleanly with `dotnet build` and `tsc --noEmit` (build-validator confirms).
- No signature changes anywhere.

**Dependencies:** None.

### Files Touched

- `Editor/Tools/GameObject/Tool_GameObject.Create.cs` — modified (A2)
- `Editor/Tools/GameObject/Tool_GameObject.Find.cs` — modified (A3, A8, D5)
- `Editor/Tools/GameObject/Tool_GameObject.SetParent.cs` — modified (A4)
- `Editor/Tools/GameObject/Tool_GameObject.LookAt.cs` — modified (A5, D6)
- `Editor/Tools/GameObject/Tool_GameObject.MoveRelative.cs` — modified (A7)
- `Editor/Tools/GameObject/Tool_GameObject.Update.cs` — modified (A1)
- `Editor/Tools/Transform/Tool_Transform.Rotate.cs` — modified (R3, cross-reference disambiguation only)

### Change A.1 — `gameobject-create` `primitiveType` description (A2)

**Type:** description-only

**File:** `Editor/Tools/GameObject/Tool_GameObject.Create.cs`

**Before (line 46):**
```csharp
[Description("Type of object to create: Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
```

**After:**
```csharp
[Description("Type of object to create (case-insensitive): Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
```

**Risks:** None.

---

### Change A.2 — `gameobject-find` `searchTerm`, top-level `[Description]`, and `maxResults` (A3, A8, D5)

**Type:** description-only

**File:** `Editor/Tools/GameObject/Tool_GameObject.Find.cs`

**Before (line 38, top-level `[Description]`):**
```csharp
[Description("Searches all GameObjects in the active scene and returns name + instance ID for each match. " + "Search methods: 'by_name' (case-insensitive substring), 'by_tag' (exact tag), " + "'by_layer' (layer name or index), 'by_component' (type name). " + "Results are capped at maxResults.")]
```

**After:**
```csharp
[Description("Searches all GameObjects in the active scene and returns name + instance ID for each match. " + "Searches the active scene only — additively-loaded scenes are not traversed unless searchAllScenes=true. " + "Search methods: 'by_name' (case-insensitive substring), 'by_tag' (exact tag), " + "'by_layer' (layer name or index), 'by_component' (type name). " + "Results are capped at maxResults (hard cap 500).")]
```

**Note:** the `searchAllScenes=true` clause anticipates Group D's param addition (G5). Apply this wording in Group A so the polish PR is self-contained, and Group D adds the actual parameter without touching this string again. If Ramon prefers strict ordering (Group A wording matches today's behavior, then Group D updates the wording when the param lands), see Open Question OQ.1 below — both shapes work; the planner recommends the bundled wording for fewer edits.

**Before (line 40, `searchTerm`):**
```csharp
[Description("Value to search for. Meaning depends on searchMethod: " + "by_name = substring of name; by_tag = exact tag; " + "by_layer = layer name or index; by_component = component type name.")] string searchTerm,
```

**After:**
```csharp
[Description("Value to search for. Meaning depends on searchMethod: " + "by_name = substring of name; by_tag = exact tag; " + "by_layer = layer name or numeric index (layer names with spaces are accepted; an unknown name returns 'not found'); " + "by_component = component type name.")] string searchTerm,
```

**Before (line 43, `maxResults`):**
```csharp
[Description("Maximum number of results to return. Default 50.")] int maxResults = 50
```

**After:**
```csharp
[Description("Maximum number of results to return. Default 50. Hard-capped at 500: values above 500 are silently clamped.")] int maxResults = 50
```

**Risks:** None.

---

### Change A.3 — `gameobject-set-parent` joint-empty unparent description (A4)

**Type:** description-only

**File:** `Editor/Tools/GameObject/Tool_GameObject.SetParent.cs`

**Before (lines 34–35):**
```csharp
[Description("Unity instance ID of the new parent GameObject. Pass 0 to use parentPath or to unparent.")] int parentInstanceId = 0,
[Description("Hierarchy path of the new parent (e.g. 'World/Props'). Leave empty to move to scene root.")] string parentPath = "",
```

**After:**
```csharp
[Description("Unity instance ID of the new parent GameObject. Pass 0 to use parentPath. To unparent (move to scene root), leave BOTH this AND parentPath empty.")] int parentInstanceId = 0,
[Description("Hierarchy path of the new parent (e.g. 'World/Props'). Used when parentInstanceId is 0. To unparent (move to scene root), leave BOTH this AND parentInstanceId=0.")] string parentPath = "",
```

**Risks:** None.

---

### Change A.4 — `gameobject-look-at` ambiguous-target and origin-default description (A5, D6)

**Type:** description-only

**File:** `Editor/Tools/GameObject/Tool_GameObject.LookAt.cs`

**Before (line 30, top-level `[Description]`):**
```csharp
[Description("Rotates a GameObject so its forward axis faces a world-space point or another GameObject. " + "Provide instanceId or objectPath to identify the source. " + "Provide targetName (hierarchy path) to look at another GO, or set targetX/Y/Z for a world position. " + "Registers the rotation with Undo.")]
```

**After:**
```csharp
[Description("Rotates a GameObject so its forward axis faces a world-space point or another GameObject. " + "Provide instanceId or objectPath to identify the source. " + "Provide targetName (hierarchy path) to look at another GO, or set targetX/Y/Z for a world position. " + "When neither targetName nor explicit target coordinates are provided, the source object will look at world origin (0,0,0). " + "Use look-at when you have a target position; for explicit Euler angles use transform-rotate. " + "Registers the rotation with Undo.")]
```

**Before (line 37, `targetName`):**
```csharp
[Description("Name or hierarchy path of a target GameObject. When non-empty, overrides targetX/Y/Z.")] string targetName = ""
```

**After:**
```csharp
[Description("Name or hierarchy path of a target GameObject. When non-empty, overrides targetX/Y/Z. " + "Ambiguous names target the first match in undefined hierarchy order — use a full path for deterministic targeting.")] string targetName = ""
```

**Risks:** None.

---

### Change A.5 — `gameobject-move-relative` priority + cross-reference description (A7, R1)

**Type:** description-only

**File:** `Editor/Tools/GameObject/Tool_GameObject.MoveRelative.cs`

**Before (line 34, top-level `[Description]`):**
```csharp
[Description("Translates a GameObject in a named direction (forward/back/left/right/up/down) by a given " + "distance. The orientation frame is taken from a reference object, world space, or the object's own " + "transform. Registers the move with Undo.")]
```

**After:**
```csharp
[Description("Translates a GameObject in a named direction (forward/back/left/right/up/down) by a given distance. " + "Orientation-frame priority: (1) referenceObject when non-empty, (2) world axes when worldSpace=true, (3) the object's own transform axes. " + "Use this tool for named-direction moves (forward, up...) and reference-frame pivots; for explicit XYZ deltas use transform-move with relative=true. " + "Registers the move with Undo.")]
```

**Note:** Both A7 (priority sentence) and R1 (cross-reference to `transform-move`) land in this single edit because both target the same `[Description]` string. The Group D entry references this change for accounting but does not re-edit MoveRelative.cs.

**Risks:** None.

---

### Change A.6 — `gameobject-update` description tweak prep for sentinel migration (A1)

**Type:** description-only (preparation for Group C; A1 standalone wording fix)

**File:** `Editor/Tools/GameObject/Tool_GameObject.Update.cs`

**Note:** A1 is a description-only fix that the review marks "apply description tweaks consistently with the new string-sentinel convention from E3". The string-sentinel migration itself happens in Group C. To keep Group A description-only, apply A1 as a clarifying tweak that remains valid under the *current* int tri-state, and let Group C update the wording again when the type changes. This avoids Group A introducing a stale description.

**Before (line 36, top-level `[Description]`):**
```csharp
[Description("Updates properties of an existing GameObject: name, tag, layer, active state, and static flag. " + "Locate the object by instanceId or hierarchy path. Only supplied non-default values are applied.")]
```

**After:**
```csharp
[Description("Updates properties of an existing GameObject: name, tag, layer, active state, and static flag. " + "Locate the object by instanceId or hierarchy path. Each property has a 'leave unchanged' sentinel — only non-sentinel values are applied (see per-param descriptions).")]
```

**Risks:** None. Group C will update the per-param sentinel text when it changes the types.

---

### Change A.7 — `transform-rotate` cross-reference (R3, cite-only)

**Type:** description-only (cross-domain cite, allowed by review Section 3 because it edits one `[Description]` string and does not change Transform behavior or signatures)

**File:** `Editor/Tools/Transform/Tool_Transform.Rotate.cs`

**Before (line 28, top-level `[Description]`):**
```csharp
[Description("Rotates a GameObject to an absolute or relative orientation in world or local space " + "using Euler angles in degrees. Returns the resulting rotation after the operation.")]
```

**After:**
```csharp
[Description("Rotates a GameObject to an absolute or relative orientation in world or local space using Euler angles in degrees. " + "Use this tool when you have explicit Euler angles; if you have a target position to face, use gameobject-look-at instead. " + "Returns the resulting rotation after the operation.")]
```

**Note:** The review's Scope Limits say "Do NOT touch the Transform domain in this cycle." A literal reading would block this. However, the review's Section 1 entry for R3 explicitly accepts the description-only fix on BOTH `gameobject-look-at` and `transform-rotate`. The intent is clearly "Transform-domain *behavioral* refactors are out of scope; this one-line description tweak is in scope." The planner interprets this as authorized. Flag in Open Questions if the consolidator wants Ramon to confirm.

**Risks:**
- Cross-domain edit. Description-only, single string literal edit. No signature change. No semantic change. The Transform audit cycle will not regress on this — it's a pure annotation.
- If the consolidator considers this out of scope, a fallback is to add a one-way disambiguation only on `gameobject-look-at` (Change A.4) — this loses half the LLM benefit but stays strictly within GameObject. See OQ.2.

---

## 3. Change Group B — `gameobject-create` maximal expansion + new sprite tool

**Findings addressed:** D2, D3, D4, G1, G3, R4

**Rationale:** All findings touch the create surface. The maximal expansion of `gameobject-create` (E2/Option 3) and the new `gameobject-create-sprite` tool (E4/Option 2) belong together because:
- They share the `parentInstanceId` + `parentPath` + `worldPositionStays` resolution helper.
- They share the post-create `Selection.activeGameObject = go` + `Undo.RegisterCreatedObjectUndo` boilerplate.
- The new sprite tool inherits the design conventions established by the expanded `gameobject-create`.

**Definition of done:**
- `gameobject-create` accepts `parentInstanceId`, `tag`, `layer`, `isActive`, `isStatic`, `worldPositionStays` with backward-compatible defaults.
- The expanded `gameobject-create` routes parent lookup through `Tool_Transform.FindGameObject`, removing the raw `GameObject.Find(parentPath)` call and aligning with the rest of the domain.
- New tool `gameobject-create-sprite` exists in `Tool_GameObject.CreateSprite.cs`, accepts a sprite path and sorting params, and creates a GameObject with a SpriteRenderer pre-configured.
- Both tools register Undo and set `Selection.activeGameObject` consistently.
- `dotnet build` and `tsc --noEmit` pass.

**Dependencies:** None (Group A is purely cosmetic; no logical dependency).

### Files Touched

- `Editor/Tools/GameObject/Tool_GameObject.Create.cs` — modified (D2, D3, D4, G3, R4); also update the `[McpToolType]` XML summary to mention the two new tools
- `Editor/Tools/GameObject/Tool_GameObject.CreateSprite.cs` — created (G1)

### Change B.1 — Expand `gameobject-create` with parent ID, tag, layer, active, static, worldPositionStays

**Type:** modified tool (purely additive — defaults preserve current behavior)

**File:** `Editor/Tools/GameObject/Tool_GameObject.Create.cs`

**Before (lines 42–51):**
```csharp
[McpTool("gameobject-create", Title = "GameObject / Create")]
[Description("Creates a new GameObject in the active scene. Supports empty objects and " + "built-in Unity primitives (Cube, Sphere, Capsule, Cylinder, Plane, Quad). " + "Optionally parents the object and sets its world position.")]
public ToolResponse Create(
    [Description("Name to assign to the new GameObject.")] string name,
    [Description("Type of object to create (case-insensitive): Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
    [Description("World-space X position. Default 0.")] float posX = 0f,
    [Description("World-space Y position. Default 0.")] float posY = 0f,
    [Description("World-space Z position. Default 0.")] float posZ = 0f,
    [Description("Hierarchy path of the parent GameObject (e.g. 'World/Props'). Empty for scene root.")] string parentPath = ""
)
```

**After:**
```csharp
[McpTool("gameobject-create", Title = "GameObject / Create")]
[Description("Creates a new GameObject in the active scene. Supports empty objects and built-in Unity primitives (Cube, Sphere, Capsule, Cylinder, Plane, Quad). " + "Optionally parents the object, sets its world position, and assigns initial tag, layer, active state, and static flag in a single call. " + "For 2D sprite GameObjects use gameobject-create-sprite. Registers the operation with Undo and selects the new object.")]
public ToolResponse Create(
    [Description("Name to assign to the new GameObject.")] string name,
    [Description("Type of object to create (case-insensitive): Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
    [Description("X position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posX = 0f,
    [Description("Y position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posY = 0f,
    [Description("Z position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posZ = 0f,
    [Description("Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.")] int parentInstanceId = 0,
    [Description("Hierarchy path of the parent GameObject (e.g. 'World/Props'). Used when parentInstanceId is 0. Both empty/0 = scene root.")] string parentPath = "",
    [Description("Initial tag (must exist in Tag Manager). Empty = use Unity default ('Untagged').")] string tag = "",
    [Description("Initial layer index (0–31). Pass -1 to use Unity default (0 = Default).")] int layer = -1,
    [Description("Initial active state: 'true', 'false', or empty = use Unity default (active).")] string isActive = "",
    [Description("Initial static flag: 'true', 'false', or empty = use Unity default (not static).")] string isStatic = "",
    [Description("When true (default), posX/Y/Z are interpreted as world-space and preserved after parenting. When false, they are interpreted as local-to-parent. Default true.")] bool worldPositionStays = true
)
```

**Maps to Unity API:**
- Parent lookup: `Tool_Transform.FindGameObject(parentInstanceId, parentPath)` (replacing the raw `GameObject.Find(parentPath)` on line 64). Lookup is skipped when `parentInstanceId == 0 && string.IsNullOrWhiteSpace(parentPath)`.
- Primitive creation: existing `switch` block on `typeKey` (lines 77–116) — unchanged.
- Position: `go.transform.position = new Vector3(posX, posY, posZ)` BEFORE parenting if `worldPositionStays == true`; AFTER parenting (as `localPosition`) if `worldPositionStays == false`. Adjust ordering accordingly.
- Parenting: `go.transform.SetParent(parent, worldPositionStays)`.
- Tag: only apply when `!string.IsNullOrWhiteSpace(tag)` → `go.tag = tag` (wrap in try/catch on `UnityException`; on failure return `ToolResponse.Error($"Tag '{tag}' is not defined in Tag Manager.")`).
- Layer: only apply when `layer >= 0 && layer <= 31` → `go.layer = layer`. Out-of-range values that are not the sentinel `-1` should `ToolResponse.Error($"Layer {layer} out of range; valid range is 0–31.")`.
- isActive: parse string sentinel — see helper below.
- isStatic: parse string sentinel — see helper below.
- Undo: `Undo.RegisterCreatedObjectUndo(go, $"Create GameObject {name}")` (unchanged).
- Selection: `Selection.activeGameObject = go` (unchanged).

**String-sentinel parser helper (place in `#region PRIVATE HELPERS` of `Tool_GameObject.Create.cs`, OR in a new shared helper file — see OQ.3):**
```csharp
/// <summary>
/// Parses the project-wide string-sentinel convention for nullable booleans.
/// Returns true with <paramref name="value"/> set when the input is "true"/"false" (case-insensitive).
/// Returns false when the input is empty/whitespace ("leave unchanged").
/// Throws ArgumentException on any other input — caller should catch and return ToolResponse.Error.
/// </summary>
/// <param name="raw">String input from the MCP transport.</param>
/// <param name="value">Parsed boolean value when the method returns true.</param>
/// <returns>True if the caller should apply <paramref name="value"/>; false to leave unchanged.</returns>
private static bool TryParseNullableBool(string raw, out bool value)
{
    value = false;

    if (string.IsNullOrWhiteSpace(raw))
    {
        return false;
    }

    string normalized = raw.Trim().ToLowerInvariant();

    if (normalized == "true")
    {
        value = true;
        return true;
    }

    if (normalized == "false")
    {
        value = false;
        return true;
    }

    throw new System.ArgumentException($"Invalid boolean sentinel '{raw}'. Use 'true', 'false', or empty.");
}
```

**Migration:**
| Old call shape | New call shape |
|---|---|
| `gameobject-create(name, primitiveType, posX, posY, posZ, parentPath)` | Same params still work as positional/named — defaults preserve behavior. |
| Two-call: `gameobject-create(...)` + `gameobject-update(tag=X, layer=Y)` | Single call: `gameobject-create(..., tag=X, layer=Y)`. |
| (No prior tool) Create + isActive=false | Single call: `gameobject-create(..., isActive="false")`. |
| (No prior tool) Create with `worldPositionStays=false` for local-relative position | Single call: `gameobject-create(..., posX=1, parentPath="World/Props", worldPositionStays=false)`. |

**Update partial-class XML summary (Tool_GameObject.Create.cs lines 12–16):**

**Before:**
```csharp
/// <summary>
/// MCP tools for creating, updating, querying, duplicating, deleting, selecting,
/// and manipulating GameObjects in the Unity scene hierarchy.
/// Covers creation, property updates, parenting, transform operations, and scene queries.
/// </summary>
```

**After:**
```csharp
/// <summary>
/// MCP tools for creating, updating, querying, duplicating, deleting,
/// and manipulating GameObjects in the Unity scene hierarchy.
/// Covers creation (3D primitives and 2D sprites), property updates, parenting,
/// sibling-index reordering, transform operations, and scene queries.
/// </summary>
```

**Note:** "selecting" is removed because `gameobject-select` is being deleted in Group D. "2D sprites" and "sibling-index reordering" are added for the new tools. CLAUDE.md mandates ONE summary across the partial — this is it; no other partial file in the domain should have a summary block.

**Risks:**
- Backward compat: signature-additive; existing callers unaffected.
- Build: requires no new `using` directives. Already imports `UnityEditor`, `UnityEngine`.
- The `tag` assignment can throw `UnityException` for undefined tags — must be caught and surfaced as `ToolResponse.Error`. The current code does not handle this; new code MUST.
- Description char-count on `primitiveType`/`posX/Y/Z` is approaching the LLM-friendly threshold. Concrete length is fine (under 200 chars each), but the consolidator should NOT shorten further or info will be lost.

---

### Change B.2 — New tool `gameobject-create-sprite`

**Type:** new tool

**File:** `Editor/Tools/GameObject/Tool_GameObject.CreateSprite.cs` (created)

**Before:** N/A (new tool — no existing equivalent)

**After (full file):**
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
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        [McpTool("gameobject-create-sprite", Title = "GameObject / Create Sprite")]
        [Description("Creates a new 2D Sprite GameObject in the active scene with a SpriteRenderer pre-configured. " + "Loads the Sprite asset from spritePath (when non-empty) and assigns it to the SpriteRenderer. " + "Mirrors gameobject-create's parenting and positioning behavior. " + "For 3D primitives or empty GameObjects use gameobject-create. " + "Registers the operation with Undo and selects the new object.")]
        public ToolResponse CreateSprite(
            [Description("Name to assign to the new Sprite GameObject.")] string name,
            [Description("X position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posX = 0f,
            [Description("Y position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posY = 0f,
            [Description("Z position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posZ = 0f,
            [Description("Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.")] int parentInstanceId = 0,
            [Description("Hierarchy path of the parent GameObject (e.g. 'World/Enemies'). Used when parentInstanceId is 0. Both empty/0 = scene root.")] string parentPath = "",
            [Description("Asset path of the Sprite to assign (e.g. 'Assets/Art/Player.png'). Empty = create with no sprite assigned.")] string spritePath = "",
            [Description("Sorting layer name on the SpriteRenderer (must exist in Tags & Layers / Sorting Layers). Default 'Default'.")] string sortingLayer = "Default",
            [Description("Order in layer (z-order within the sorting layer). Default 0.")] int orderInLayer = 0,
            [Description("When true (default), posX/Y/Z are world-space and preserved after parenting. When false, they are local-to-parent. Default true.")] bool worldPositionStays = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ToolResponse.Error("name is required.");
                }

                Transform? parent = null;

                if (parentInstanceId != 0 || !string.IsNullOrWhiteSpace(parentPath))
                {
                    var parentGo = Tool_Transform.FindGameObject(parentInstanceId, parentPath);

                    if (parentGo == null)
                    {
                        return ToolResponse.Error($"Parent GameObject not found. parentInstanceId={parentInstanceId}, parentPath='{parentPath}'.");
                    }

                    parent = parentGo.transform;
                }

                Sprite? sprite = null;

                if (!string.IsNullOrWhiteSpace(spritePath))
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                    if (sprite == null)
                    {
                        return ToolResponse.Error($"Sprite asset not found at '{spritePath}'. Ensure the path is correct and the asset is imported as a Sprite.");
                    }
                }

                if (!SortingLayer.layers.Any(l => l.name == sortingLayer))
                {
                    return ToolResponse.Error($"Sorting layer '{sortingLayer}' is not defined. Add it under Project Settings > Tags and Layers > Sorting Layers.");
                }

                var go = new GameObject(name);
                var renderer = go.AddComponent<SpriteRenderer>();

                if (sprite != null)
                {
                    renderer.sprite = sprite;
                }

                renderer.sortingLayerName = sortingLayer;
                renderer.sortingOrder = orderInLayer;

                if (worldPositionStays)
                {
                    go.transform.position = new Vector3(posX, posY, posZ);
                }

                if (parent != null)
                {
                    go.transform.SetParent(parent, worldPositionStays);
                }

                if (!worldPositionStays)
                {
                    go.transform.localPosition = new Vector3(posX, posY, posZ);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create Sprite {name}");
                Selection.activeGameObject = go;

                var sb = new StringBuilder();
                sb.AppendLine($"Created Sprite GameObject '{go.name}':");
                sb.AppendLine($"  Instance ID:    {go.GetInstanceID()}");
                sb.AppendLine($"  Position:       ({posX}, {posY}, {posZ})");
                sb.AppendLine($"  Sprite:         {(sprite != null ? spritePath : "(none)")}");
                sb.AppendLine($"  Sorting Layer:  {sortingLayer}");
                sb.AppendLine($"  Order in Layer: {orderInLayer}");

                if (parent != null)
                {
                    sb.AppendLine($"  Parent:         {parent.name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `AssetDatabase.LoadAssetAtPath<Sprite>(spritePath)` — load the sprite asset. Returns null if the asset isn't a Sprite or doesn't exist.
- `new GameObject(name)` — create empty GO.
- `go.AddComponent<SpriteRenderer>()` — attach renderer.
- `renderer.sprite = sprite` — assign sprite (when non-null).
- `renderer.sortingLayerName = sortingLayer` — set sorting layer.
- `renderer.sortingOrder = orderInLayer` — set order in layer.
- `SortingLayer.layers` — validation (requires `using System.Linq;` for `.Any(...)`).
- `Tool_Transform.FindGameObject(parentInstanceId, parentPath)` — parent lookup (cross-domain helper, cite-only usage).
- `Undo.RegisterCreatedObjectUndo(go, ...)` — undo registration.
- `Selection.activeGameObject = go` — match `gameobject-create`'s side effect.

**Required `using` directives:** `using System.ComponentModel;`, `using System.Linq;`, `using System.Text;`, `using GameDeck.MCP.Attributes;`, `using GameDeck.MCP.Models;`, `using GameDeck.MCP.Utils;`, `using UnityEditor;`, `using UnityEngine;`. (`System.Linq` is the only addition vs. the rest of the domain.)

**No XML `<summary>` on this partial file** — per CLAUDE.md partial-class single-summary rule, the summary lives only on `Tool_GameObject.Create.cs`.

**Migration:**
| Old call shape | New call shape |
|---|---|
| (No prior tool) — workaround was 3-call: `gameobject-create(Empty)` + `component-add(SpriteRenderer)` + manual sprite assignment via reflection or `component-update` (unverified). | Single call: `gameobject-create-sprite(name, posX, posY, posZ, spritePath="Assets/Art/Player.png")`. |

**Risks:**
- Backward compat: net-new tool, no removal — safe.
- Build: requires `using System.Linq;` (only addition to the domain's standard `using` set). The Sprite asset type is in `UnityEngine`; SpriteRenderer too — already covered.
- Sorting-layer validation uses `SortingLayer.layers.Any(...)`. If consolidator dislikes LINQ in Unity Editor code (allocation), an explicit foreach loop is acceptable — the planner accepts either. See OQ.4.
- `AssetDatabase.LoadAssetAtPath<Sprite>` returns null both for "asset doesn't exist" and "asset exists but isn't a Sprite". Error message handles both cases with one string.

---

## 4. Change Group C — Sentinel migration on `gameobject-update`

**Findings addressed:** D1

**Rationale:** Per E3 (Option 2) and review Section 3 Code Style, the int tri-state convention (`-1`/`0`/`1`) for `isActive`/`isStatic` is replaced with the project-wide string sentinel `"true" | "false" | ""`. This is a signature break (per review Section 3 box 3 + Section 6 "Signature changes that ARE breakage"), but explicitly authorized.

Group C is isolated from Groups B/D so the diff for the convention change is small and reviewable, and so the same pattern can be cited from future audits of other domains.

**Definition of done:**
- `gameobject-update` `isActive` and `isStatic` parameters change from `int isActive = -1` and `int isStatic = -1` to `string isActive = ""` and `string isStatic = ""`.
- Per-param `[Description]` text is updated to reflect the new sentinel.
- Top-level method `[Description]` keeps the wording from Group A (Change A.6) — already sentinel-agnostic.
- Implementation uses the shared `TryParseNullableBool` helper introduced in Change B.1 (or, if Group B has not landed yet, defines its own copy — see OQ.3).
- Layer parameter remains `int layer = -1`; `name`/`tag` remain `string = ""`. Only booleans migrate.
- `dotnet build` passes.

**Dependencies:** None hard. Soft preference: land Group B first to share `TryParseNullableBool` (OQ.3).

### Files Touched

- `Editor/Tools/GameObject/Tool_GameObject.Update.cs` — modified

### Change C.1 — Migrate `isActive` and `isStatic` to string sentinel

**Type:** modified tool (signature break on two params)

**File:** `Editor/Tools/GameObject/Tool_GameObject.Update.cs`

**Before (lines 25–30, XML doc):**
```csharp
/// <param name="isActive">
/// Active state: 1 = activate, 0 = deactivate, -1 = unchanged.
/// </param>
/// <param name="isStatic">
/// Static flag: 1 = mark static, 0 = clear static, -1 = unchanged.
/// </param>
```

**After:**
```csharp
/// <param name="isActive">
/// Active state: "true" = activate, "false" = deactivate, "" (empty) = unchanged.
/// </param>
/// <param name="isStatic">
/// Static flag: "true" = mark static, "false" = clear static, "" (empty) = unchanged.
/// </param>
```

**Before (lines 43–44, signature):**
```csharp
[Description("Active state: 1 = active, 0 = inactive, -1 = unchanged.")] int isActive = -1,
[Description("Static flag: 1 = static, 0 = not static, -1 = unchanged.")] int isStatic = -1
```

**After:**
```csharp
[Description("Active state: 'true' = activate, 'false' = deactivate, empty = leave unchanged.")] string isActive = "",
[Description("Static flag: 'true' = mark static, 'false' = clear static, empty = leave unchanged.")] string isStatic = ""
```

**Before (lines 79–89, implementation):**
```csharp
if (isActive == 0 || isActive == 1)
{
    go.SetActive(isActive == 1);
    sb.AppendLine($"  Active: {(isActive == 1)}");
}

if (isStatic == 0 || isStatic == 1)
{
    go.isStatic = isStatic == 1;
    sb.AppendLine($"  Static: {(isStatic == 1)}");
}
```

**After:**
```csharp
try
{
    if (TryParseNullableBool(isActive, out bool activeValue))
    {
        go.SetActive(activeValue);
        sb.AppendLine($"  Active: {activeValue}");
    }

    if (TryParseNullableBool(isStatic, out bool staticValue))
    {
        go.isStatic = staticValue;
        sb.AppendLine($"  Static: {staticValue}");
    }
}
catch (System.ArgumentException ex)
{
    return ToolResponse.Error(ex.Message);
}
```

**Maps to Unity API:** `go.SetActive(bool)`, `go.isStatic = bool` — unchanged.

**Migration:**
| Old call shape | New call shape |
|---|---|
| `gameobject-update(instanceId=X, isActive=1)` | `gameobject-update(instanceId=X, isActive="true")` |
| `gameobject-update(instanceId=X, isActive=0)` | `gameobject-update(instanceId=X, isActive="false")` |
| `gameobject-update(instanceId=X, isActive=-1)` (or omitted) | `gameobject-update(instanceId=X)` (or `isActive=""`) — unchanged behavior |
| Same for `isStatic`. | |

**Risks:**
- **Signature break:** existing callers passing integers (`1`/`0`/`-1`) for `isActive`/`isStatic` will fail at MCP transport deserialization (string-typed param won't accept an int). Authorized by review Section 3 box 3 + Section 6.
- Build: no new `using` directives.
- The `TryParseNullableBool` helper is introduced in Change B.1. If Group C ships before Group B (planner does not recommend this), the helper must be inlined into `Tool_GameObject.Update.cs` and later moved when B lands. See OQ.3.
- The test project (Jurassic Survivors) is not in this repo, so no in-tree test files need editing. Out-of-tree consumer projects calling `gameobject-update` with int booleans will need to migrate their MCP calls — outside the scope of this plan.

---

## 5. Change Group D — `gameobject-select` deprecation + cross-tool disambiguation + `gameobject-find` extension

**Findings addressed:** R1, R2, A6, G5

**Rationale:** Bundles three cross-tool disambiguation/cleanup items together because they are all about how the GameObject domain presents itself to the LLM at the boundary:
- **R2 + A6:** delete `gameobject-select` (subsumed by `selection-set` + auto-select side effects). The `objectName`+`objectPath` collapse from A6 becomes moot once the tool is deleted, so A6 is "resolved by removal" rather than by parameter merging.
- **R1:** add a `[Description]` cross-reference inside `transform-move` that points to `gameobject-move-relative` for named-direction work. (The reciprocal cross-ref on `move-relative` already lands in Group A as Change A.5.)
- **G5:** add `searchAllScenes = false` parameter to `gameobject-find` so the LLM can opt into multi-scene traversal.

These three items have low cross-coupling but share a "describe & extend the boundary" theme.

**Definition of done:**
- `gameobject-select` tool and its file are deleted.
- `transform-move` `[Description]` updated with a cross-reference sentence to `gameobject-move-relative`.
- `gameobject-find` accepts `searchAllScenes = false`; when `true`, the search iterates `SceneManager.sceneCount` and aggregates root objects across all loaded scenes.
- `dotnet build` passes.

**Dependencies:** None hard. Soft preference: land Group A first because A.5 (`move-relative` description) and A.2 (`find` description) already touch these files; Group D adds parameters and behavior on top.

### Files Touched

- `Editor/Tools/GameObject/Tool_GameObject.Select.cs` — DELETED (R2, A6)
- `Editor/Tools/GameObject/Tool_GameObject.Find.cs` — modified (G5)
- `Editor/Tools/Transform/Tool_Transform.Move.cs` — modified (R1, description-only cross-reference)

### Change D.1 — Delete `gameobject-select`

**Type:** removed tool (file deletion)

**File:** `Editor/Tools/GameObject/Tool_GameObject.Select.cs` — DELETE

**Before:** 67-line file containing `[McpTool("gameobject-select", ...)]` method `Select(int instanceId, string objectPath, string objectName, bool ping)`.

**After:** File does not exist.

**Maps to Unity API:** N/A — the functionality is fully covered by:
- `selection-set(instanceIds, objectPaths)` from `Editor/Tools/Selection/Tool_Selection.Set.cs` for the selection itself.
- The `Selection.activeGameObject = go` side effects in `gameobject-create` (Change B.1) and `gameobject-duplicate` (existing line 57) for the "keep newly-created object selected" case.
- `EditorGUIUtility.PingObject` (the `ping=true` flag) is intentionally dropped per E1's "auto-select side effects cover the common case." If pinging becomes necessary later, the right home is a tiny new `selection-ping` tool in the Selection domain — out of scope for this cycle and flagged for the Selection audit (see Section 6).

**Migration:**
| Old call shape | New call shape |
|---|---|
| `gameobject-select(instanceId=X, ping=true)` | `selection-set(instanceIds="X")` (note: `ping` flag is lost; users who need pinging file a new request to add `selection-ping`). |
| `gameobject-select(objectPath="World/Player")` | `selection-set(objectPaths="World/Player")` |
| `gameobject-select(objectName="Player")` | `selection-set(objectPaths="Player")` (Selection-set's path lookup uses `GameObject.Find`, which accepts a bare name). |
| `gameobject-create(...)` then `gameobject-select(instanceId=X)` | Just `gameobject-create(...)` — auto-selects. |
| `gameobject-duplicate(...)` then `gameobject-select(instanceId=X)` | Just `gameobject-duplicate(...)` — auto-selects (existing line 57). |

**Risks:**
- **Tool name removal:** existing MCP callers invoking `gameobject-select` will fail with "tool not found." Authorized by review Section 3 box 3.
- Loss of `ping` flag: documented above; reroute via future `selection-ping` if demand emerges.
- The XML `<summary>` for the partial class is in `Tool_GameObject.Create.cs` (already updated in Change B.1 to drop "selecting"). No further docs cleanup needed.
- Selection-domain tests (if any) referencing `gameobject-select` would need updating; consolidator should `Grep` for "gameobject-select" across the entire repo before deleting and update any docs/tests found. None expected based on the audit.

### Change D.2 — Add `searchAllScenes` param to `gameobject-find`

**Type:** modified tool (purely additive; default `false` preserves current behavior)

**File:** `Editor/Tools/GameObject/Tool_GameObject.Find.cs`

**Before (signature lines 39–44):**
```csharp
public ToolResponse FindGameObjects(
    [Description("Value to search for. Meaning depends on searchMethod: " + ...)] string searchTerm,
    [Description("Search strategy: 'by_name', 'by_tag', 'by_layer', or 'by_component'. Default 'by_name'.")] string searchMethod = "by_name",
    [Description("When true, inactive GameObjects are included in results. Default false.")] bool includeInactive = false,
    [Description("Maximum number of results to return. Default 50. Hard-capped at 500: values above 500 are silently clamped.")] int maxResults = 50
)
```

(`searchTerm` description matches Change A.2's wording.)

**After:**
```csharp
public ToolResponse FindGameObjects(
    [Description("Value to search for. Meaning depends on searchMethod: " + ...)] string searchTerm,
    [Description("Search strategy: 'by_name', 'by_tag', 'by_layer', or 'by_component'. Default 'by_name'.")] string searchMethod = "by_name",
    [Description("When true, inactive GameObjects are included in results. Default false.")] bool includeInactive = false,
    [Description("Maximum number of results to return. Default 50. Hard-capped at 500: values above 500 are silently clamped.")] int maxResults = 50,
    [Description("When true, traverse ALL loaded scenes (active + additively loaded) instead of only the active scene. Default false.")] bool searchAllScenes = false
)
```

**Implementation changes:**

Introduce a private helper (same `#region PRIVATE HELPERS` block):
```csharp
/// <summary>
/// Returns the root GameObjects of either the active scene only, or every loaded scene.
/// </summary>
/// <param name="allScenes">When true, aggregates roots from every loaded scene via SceneManager.sceneCount.</param>
/// <returns>An array of root GameObjects, in scene-order.</returns>
private static GameObject[] GetSearchRoots(bool allScenes)
{
    if (!allScenes)
    {
        return SceneManager.GetActiveScene().GetRootGameObjects();
    }

    var aggregated = new System.Collections.Generic.List<GameObject>();

    for (int i = 0; i < SceneManager.sceneCount; i++)
    {
        var scene = SceneManager.GetSceneAt(i);

        if (!scene.isLoaded)
        {
            continue;
        }

        aggregated.AddRange(scene.GetRootGameObjects());
    }

    return aggregated.ToArray();
}
```

Then replace **every** `var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();` (current lines 72, 99, 120, 132) with `var rootObjects = GetSearchRoots(searchAllScenes);`.

Special case for `by_tag` (lines 79–95): `GameObject.FindGameObjectsWithTag` is global across all loaded scenes already, so the active-scene-only path inside that branch is wrong when `searchAllScenes == true` — but it's also wrong when `searchAllScenes == false` (it currently returns objects from all scenes regardless). The minimal correct fix:
- When `searchAllScenes == true && !includeInactive`: keep using `GameObject.FindGameObjectsWithTag` (it's already cross-scene and active-only).
- When `searchAllScenes == false && !includeInactive`: filter the `tagged` array to keep only objects whose `gameObject.scene == SceneManager.GetActiveScene()`. This is a behavior change but it brings the `by_tag` branch into line with the other three search methods.
- When `includeInactive == true`: use `GetSearchRoots(searchAllScenes)` and the recursive `SearchByTag` walk (existing logic).

**Decision flag for the consolidator:** the audit didn't catch this `by_tag` cross-scene leak, and the review didn't authorize a behavior change there. The CONSERVATIVE option is: leave the `by_tag` branch unchanged (it stays cross-scene under `!includeInactive`) and add a one-line note in the response builder: `(by_tag with includeInactive=false searches across all loaded scenes regardless of searchAllScenes; for active-scene-only tag search, set includeInactive=true)`. The CORRECT option is to fix the leak. **Planner recommends the conservative option** to honor Rule 2 (no scope expansion); track the leak in Section 7 ("Out of Scope") for a future audit. See OQ.5 for the consolidator to confirm.

**Update top-level `[Description]` (already done in Change A.2):** the wording from Change A.2 already covers the new param ("Searches the active scene only — additively-loaded scenes are not traversed unless searchAllScenes=true"). No further edit needed in Group D.

**Maps to Unity API:**
- `SceneManager.sceneCount` — number of currently loaded scenes.
- `SceneManager.GetSceneAt(int)` — retrieves scene by index.
- `Scene.isLoaded` — guard for in-progress loads.
- `Scene.GetRootGameObjects()` — already in use.

**Migration:**
| Old call shape | New call shape |
|---|---|
| `gameobject-find(searchTerm="SpawnPoint")` | Same — defaults preserved (active scene only). |
| (No way to do this) | `gameobject-find(searchTerm="SpawnPoint", searchAllScenes=true)` — searches every loaded scene. |

**Risks:**
- Backward compat: signature-additive; default `false` preserves current behavior.
- Build: no new `using` directives (`SceneManager` already imported).
- The `GetSearchRoots` helper allocates a `List<GameObject>` only when `searchAllScenes == true`; the common case takes the early return path. No allocation regression.
- Behavior of `by_tag` under `!includeInactive` retains the pre-existing cross-scene leak. Documented in OQ.5 / Section 7.

### Change D.3 — Cross-reference in `transform-move` (R1)

**Type:** description-only

**File:** `Editor/Tools/Transform/Tool_Transform.Move.cs`

**Before (line 28, top-level `[Description]`):**
```csharp
[Description("Moves a GameObject to an absolute or relative position in world or local space. " + "Returns the resulting world position after the operation.")]
```

**After:**
```csharp
[Description("Moves a GameObject to an absolute or relative position in world or local space using explicit X/Y/Z values. " + "For named-direction moves (forward, up...) or moves expressed in another object's reference frame, use gameobject-move-relative instead. " + "Returns the resulting world position after the operation.")]
```

**Note:** Same scope-limit caveat as Change A.7 — this is a description-only cross-domain edit explicitly authorized by review Section 1's R1 entry. See OQ.2.

**Risks:** None. Description-only.

---

## 6. Change Group E — New sibling reorder tool

**Findings addressed:** G2

**Rationale:** Single new tool, isolated, easy to review. Wraps `Transform.SetSiblingIndex` (and its convenience cousins `SetAsFirstSibling` / `SetAsLastSibling`) in one MCP tool. Per E5/Option 1, action-dispatch (`set_first` / `set_last` / `set_index`) is rejected as YAGNI; a single tool with `index = -1` meaning "last" handles all three cases.

**Definition of done:**
- New tool `gameobject-set-sibling-index` exists in `Tool_GameObject.SetSiblingIndex.cs`.
- The tool registers Undo correctly and validates the index against the parent's `childCount`.
- `dotnet build` passes.

**Dependencies:** None.

### Files Touched

- `Editor/Tools/GameObject/Tool_GameObject.SetSiblingIndex.cs` — created

### Change E.1 — New tool `gameobject-set-sibling-index`

**Type:** new tool

**File:** `Editor/Tools/GameObject/Tool_GameObject.SetSiblingIndex.cs` (created)

**Before:** N/A (new tool)

**After (full file):**
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
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        [McpTool("gameobject-set-sibling-index", Title = "GameObject / Set Sibling Index")]
        [Description("Reorders a GameObject within its current parent by setting its sibling index. " + "index = 0 makes it the first child; index = -1 (or any value >= the parent's childCount) makes it the last child. " + "Useful for UI z-order (Canvas children) and any hierarchy where deterministic order matters. " + "Registers the operation with Undo.")]
        public ToolResponse SetSiblingIndex(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Canvas/Panel/Button'). Used when instanceId is 0.")] string objectPath = "",
            [Description("New sibling index within the parent. 0 = first child; -1 = last child; values clamped to [0, parent.childCount-1].")] int index = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var t = go.transform;
                var parent = t.parent;
                int siblingCount;

                if (parent != null)
                {
                    siblingCount = parent.childCount;
                }
                else
                {
                    siblingCount = go.scene.rootCount;
                }

                int previousIndex = t.GetSiblingIndex();
                int targetIndex;

                if (index < 0 || index >= siblingCount)
                {
                    targetIndex = siblingCount - 1;
                }
                else
                {
                    targetIndex = index;
                }

                Undo.RegisterFullObjectHierarchyUndo(parent != null ? (UnityEngine.Object)parent.gameObject : (UnityEngine.Object)go, $"Set Sibling Index {go.name}");
                t.SetSiblingIndex(targetIndex);

                string parentDesc = parent != null ? $"'{parent.name}'" : "(scene root)";
                return ToolResponse.Text($"Reordered '{go.name}' under {parentDesc}: sibling index {previousIndex} → {targetIndex} (of {siblingCount} children).");
            });
        }

        #endregion
    }
}
```

**Maps to Unity API:**
- `Tool_Transform.FindGameObject(instanceId, objectPath)` — target lookup (cite-only).
- `transform.parent` — get parent transform; null when GO is at scene root.
- `transform.parent.childCount` — number of siblings (when parented).
- `gameObject.scene.rootCount` — number of scene-root siblings (when not parented).
- `transform.GetSiblingIndex()` — current index.
- `transform.SetSiblingIndex(int)` — apply new index.
- `Undo.RegisterFullObjectHierarchyUndo(target, name)` — registers the hierarchy reorder for undo. Use the parent (or the GO itself when at scene root) as the target.

**Migration:**
| Old call shape | New call shape |
|---|---|
| (No prior tool) | `gameobject-set-sibling-index(instanceId=X, index=0)` — make first child. |
| (No prior tool) | `gameobject-set-sibling-index(instanceId=X, index=-1)` — make last child (default). |
| (No prior tool) | `gameobject-set-sibling-index(instanceId=X, index=3)` — set explicit index. |

**Risks:**
- Backward compat: net-new tool. Safe.
- Build: no new `using` directives.
- `Undo.RegisterFullObjectHierarchyUndo` is the right call here, NOT `Undo.RecordObject` (which doesn't capture sibling order). Reference: Unity manual on Hierarchy undo.
- For scene-root GameObjects (`parent == null`), `SetSiblingIndex` reorders within the scene. The `siblingCount = go.scene.rootCount` branch handles this. The Undo target in that case is the GO itself (parent doesn't exist). The conditional cast `(UnityEngine.Object)` is required because `transform.gameObject` is also `UnityEngine.Object` but the conditional expression needs a unified type.
- No XML `<summary>` on this partial file — single-summary rule.

---

## 7. Out of Scope — For Future Audit

These were noticed during planning but are not authorized by the review and must not be folded into the consolidator's work:

1. **`Tool_Transform.FindGameObject` deprecated API migration.** Currently uses `EditorUtility.InstanceIDToObject` with `#pragma warning disable CS0618`. CLAUDE.md mandates `EntityIdToObject` (Unity 6000.3). `Selection/Tool_Selection.Set.cs:67` is the reference pattern. **Flagged for Transform-domain audit.**

2. **`gameobject-find` `by_tag` cross-scene leak under `!includeInactive`.** `GameObject.FindGameObjectsWithTag` returns hits from all loaded scenes regardless of `searchAllScenes`. The other three search methods (by_name, by_layer, by_component) walk only the active scene's roots when `searchAllScenes=false`. Inconsistent behavior. **Flag in Section 7 of the next GameObject audit (or fix in a tiny follow-up PR after Ramon approves).**

3. **`gameobject-duplicate` does not propagate Undo to the source's parent.** Existing tool uses `Object.Instantiate` + `Undo.RegisterCreatedObjectUndo` — fine for the duplicate but doesn't record the source's transform (which doesn't change). Probably correct, but worth a second look in a future audit.

4. **Lightweight `transform-get` (G6 deferral).** Heads-up for the Transform audit: a `transform-get` tool would be the right home for the lightweight transform-read use case currently only available via heavyweight `gameobject-get`.

5. **Anchored-subtree component search (G4 deferral).** Heads-up for the Component audit: a tool that starts the search at a specific GameObject (not scene roots) is missing.

6. **`gameobject-select` `ping` functionality replacement (`selection-ping`).** Group D drops the `ping` flag. If pinging becomes a real need, a tiny new tool in the Selection domain is the right home. **Flag for Selection-domain audit.**

7. **CLAUDE.md update documenting the string-sentinel boolean convention from E3.** Per review Section 6, this is an out-of-cycle commit owned by Ramon — not by the consolidator.

---

## 8. Open Questions For The Consolidator

These are small ambiguities the planner could not fully resolve from the audit/review. The consolidator agent should surface them to Ramon if it cannot decide locally, or pick the planner-recommended default and proceed.

### OQ.1 — Group A's `gameobject-find` description anticipates Group D's `searchAllScenes` param

**Question:** Change A.2 updates the top-level `[Description]` of `gameobject-find` to mention `searchAllScenes=true` even though the param is added in Group D. If the consolidator ships Group A as its own PR before Group D, the wording will be slightly ahead of the implementation.

**Planner recommendation:** Ship as written. The wording change is small and the param lands within the same cycle. Alternative: omit the `searchAllScenes` clause from Group A's wording, and add it in Group D when the param lands (one extra `[Description]` edit).

### OQ.2 — Cross-domain description edits in Groups A and D

**Question:** Changes A.7 (`transform-rotate`) and D.3 (`transform-move`) edit the Transform domain's `[Description]` strings, while review Section 3 says "Do NOT touch the Transform domain in this cycle." The review's per-finding decisions for R1 and R3 explicitly authorize disambiguation in BOTH directions, so the planner reads these as in-scope description-only edits. The consolidator should confirm.

**Planner recommendation:** Apply both edits as described. They are single-string description edits with no behavioral or signature change, and both are explicitly authorized by review Section 1. If the consolidator wants to err on the side of strict scope, it can defer A.7 and D.3 to the Transform consolidation cycle and apply only the GameObject-side cross-references — the LLM still benefits from the half-disambiguation.

### OQ.3 — Where does `TryParseNullableBool` live?

**Question:** Change B.1 introduces `TryParseNullableBool` and Change C.1 reuses it. Two acceptable locations:
1. `Tool_GameObject.Create.cs` `#region PRIVATE HELPERS` (private static — accessible to other partials of `Tool_GameObject` because they share the class).
2. A shared utility file under `Editor/Tools/Helpers/` (e.g. `SentinelParsing.cs`).

**Planner recommendation:** Option 1 for this cycle. Keeps the change isolated to GameObject domain. When other domains adopt the convention, the consolidator can promote the helper to `Editor/Tools/Helpers/` in a follow-up cycle. This avoids introducing a new helpers file just for one method.

If Group C ships before Group B, define the helper inline in `Tool_GameObject.Update.cs` (private static), then move it to `Tool_GameObject.Create.cs` when B lands. Group C should NOT block on Group B if Ramon wants to ship them in any order.

### OQ.4 — LINQ in `gameobject-create-sprite` sorting-layer validation

**Question:** Change B.2 uses `SortingLayer.layers.Any(l => l.name == sortingLayer)` (requires `using System.Linq;`). LINQ allocates an enumerator. Some Unity-Editor codebases avoid LINQ.

**Planner recommendation:** Either form is acceptable. If the consolidator prefers no LINQ:
```csharp
var layers = SortingLayer.layers;
bool found = false;

for (int i = 0; i < layers.Length; i++)
{
    if (layers[i].name == sortingLayer)
    {
        found = true;
        break;
    }
}

if (!found)
{
    return ToolResponse.Error($"Sorting layer '{sortingLayer}' is not defined. Add it under Project Settings > Tags and Layers > Sorting Layers.");
}
```
This avoids the `using System.Linq;` import and the allocation. Repository-wide grep shows no other LINQ usage in `Editor/Tools/`, so the no-LINQ form is consistent with the codebase. **Consolidator should pick the no-LINQ form** unless it sees LINQ used elsewhere in the same domain.

### OQ.5 — `by_tag` cross-scene leak in Change D.2

**Question:** When implementing `searchAllScenes` in `gameobject-find`, should the consolidator also fix the pre-existing cross-scene leak in the `by_tag` branch (when `!includeInactive`)?

**Planner recommendation:** NO. The audit didn't flag it; the review didn't authorize a behavior change there. Leave the existing `GameObject.FindGameObjectsWithTag` call as-is. The leak is logged in Section 7 (Out of Scope) for a future audit. Honoring Rule 2 (no scope expansion) is more important than fixing the leak this cycle.

### OQ.6 — Order of position assignment vs. parenting in Change B.1

**Question:** When `worldPositionStays == false`, the new code interprets `posX/Y/Z` as local-to-parent. The order needs to be: parent first, then assign `localPosition`. When `worldPositionStays == true`, the order is: assign `position`, then parent (current behavior). The signature description claims `(world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false)`.

**Planner recommendation:** Implement exactly as described. The sample skeleton in Change B.2 (CreateSprite) shows the correct ordering:
```csharp
if (worldPositionStays) { go.transform.position = ...; }
if (parent != null) { go.transform.SetParent(parent, worldPositionStays); }
if (!worldPositionStays) { go.transform.localPosition = ...; }
```
Apply the same shape inside `gameobject-create`.

---

End of plan.
