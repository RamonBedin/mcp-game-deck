# Review — Asset

**Audit reviewed:** `.claude/reports/audits/audit-Asset-20260425.md`
**Reviewer:** Ramon (drafted by auto-reviewer agent; escalations answered by Ramon 2026-05-19)
**Date:** 2026-05-19
**Status:** ✅ READY FOR PLANNING

---

## 0. Auto-Decision Summary

This review was drafted by the auto-reviewer agent and finalized after Ramon answered the 7 escalations on 2026-05-19.

**Findings auto-decided by agent:** 11 (10 accepted, 1 rejected, 0 deferred)
**Findings resolved via Ramon's escalation answers:** 12 (across E1–E7)
**Total findings:** 23

**Headline decisions:**

- **`asset-create` is deleted entirely** (E1 Option 1). Replaced by `asset-create-render-texture` for the one type without a dedicated creator. `propertiesJson` dies with the tool. Per-type creators (`material-create`, `physics-create-material`, `scriptableobject-create`, `animation-configure-controller`) remain the canonical path.
- **Asset-scoped parser extraction** (E2 Option 2) into a new `Tool_Asset.Shared.cs` partial; ObjectReference write support added (closes G8). `Tool_Object.Modify.cs` is NOT touched in this cycle.
- **New tools added:** `asset-set-labels` (E3 Option 1), `asset-find-references` (E5), `asset-exists` (E5), `asset-create-render-texture` (E1).
- **Deferred:** G3 AssetBundles (won't-fix, documented in `docs/internal/decisions/`), G5 batch ops (deferred to v2.x post `BatchExecute` validation), G7 sprite slicing (deferred to Texture-domain audit).

**Backward compatibility:** Break freely. E1 deletes `asset-create` (signature-breaking tool removal); E3 / E5 add new tools. Internal refactor; package at v1.1; no external consumer claims in audit. Matches the GameObject precedent.

Section 9 (Resolved Escalations) below preserves Ramon's full reasoning and implementation notes from the original escalation block as an audit trail for the planner.

---

## 1. Decisions Per Finding

| Finding ID | Decision | Notes |
|-----------|----------|-------|
| R1 | accept (per E1) | `asset-create` is deleted entirely. Per-type creators are the canonical path. Cross-domain reach in this cycle: zero — `material-create`, `physics-create-material`, `scriptableobject-create`, `animation-configure-controller` are untouched. The Asset audit just stops claiming to do their job. See E1 in Section 9. |
| R2 | accept-with-modification (per E2) | Extract shared parser within Asset domain only. New file `Editor/Tools/Asset/Tool_Asset.Shared.cs` (partial class continuation, no `[McpToolType]` summary). `Tool_Object.Modify.cs` is NOT touched — the audit did not deep-read it and Section 3 bars touching it. Cross-domain consolidation deferred to Object-domain audit. See E2 in Section 9. |
| A1 | accept-with-modification (per E1) | Rewrite Asset domain summary to no longer claim "create any asset". Domain covers find/get-info/import-settings/copy/move/delete/rename/create-folder + `asset-create-render-texture` and `asset-create-folder`. Type-specific creation lives in the type's own domain. |
| A2 | accept | Description-only fix. With E1's Option 1, `propertiesJson` dies with `asset-create` — A2 is automatically resolved (the misleading param no longer exists). If any residual reference to `propertiesJson` survives in docs/comments, remove it. |
| A3 | accept | Description-only fix, audit recommends enumerating filter prefixes (`t:`, `l:`, `b:`, `ref:`) in `asset-find searchFilter` `[Description]`. |
| A4 | accept | Description-only fix, audit recommends "use this when X, not Y" disambiguation between `asset-set-import-settings` and `object-modify`. Mention the `SaveAndReimport` side-effect as the distinguishing behavior. |
| A5 | accept | Description-only fix, audit recommends warning that `forceUpdate=true` reimports the entire project (minutes for medium projects). |
| A6 | accept | Description-only fix. Add "Returns success when the folder already exists (idempotent)" to `asset-create-folder` `[Description]`. |
| A7 | accept | Description-only fix, audit recommends warning that `newName` must be unique within the destination folder. |
| D1 | accept (per E1) | Resolved by deletion of `asset-create`. The silent `assetType="Material"` default disappears along with the tool. |
| D2 | accept | Description-only fix, audit recommends clarifying that default `"Assets"` is a project-wide search. |
| D3 | reject | Auto-decided: audit itself recommends "No action required unless evidence shows the LLM is re-issuing requests because of truncation." No such evidence exists — leave default `maxResults = 25` as-is. |
| D4 | accept | Description-only fix, audit recommends explicit warning that `moveToTrash=false` is unrecoverable. No code change. |
| D5 | accept (per E1) | Resolved by deletion of `asset-create`. The `propertiesJson` default empty string disappears along with the tool. |
| G1 | accept (per E3) | New tool `asset-set-labels(assetPath, labelsJson, clearExisting)`. `clearExisting` is plain `bool` (default `false` = append), NOT a sentinel — see E3 in Section 9 for Ramon's correction of the agent's draft. `labelsJson` is JSON array of strings. `ReadOnlyHint = false`. Use `AssetDatabase.SetLabels` + `GetLabels` for merge. Wrap in `MainThreadDispatcher.Execute`. Apply `path` discipline. |
| G2 | accept (per E1) | Resolved by deletion of `asset-create`. The "no ScriptableObject case" issue becomes moot because `scriptableobject-create` was always the right path. |
| G3 | reject (per E4) | Won't-fix. AssetBundles are legacy; Addressables is the recommended workflow for Unity 6000+. Document the rejection rationale in `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md` so future audits don't re-raise G3. Add one-line entry to `post-v2.0-backlog.md` for potential future Addressables domain. No code changes. |
| G4 | accept (per E5) | New tool `asset-find-references(assetPath, maxResults = 100)`. `ReadOnlyHint = true`. Implementation enumerates `AssetDatabase.FindAssets("")`, calls `AssetDatabase.GetDependencies(candidate, recursive: true)` per candidate. O(n × avg-deps); document performance warning in `[Description]`. Returns `{references: string[], truncated: bool}`. Early-exit at cap. |
| G5 | defer (per E6) | Batch CRUD ops deferred to v2.x. Add entry to `post-v2.0-backlog.md`: "Batch CRUD ops for Asset domain. Decision deferred from Asset E6 (2026-05-19). Pre-req: verify `BatchExecute` handles AssetDatabase ops on main thread; if confirmed, route through `BatchExecute` rather than adding asset-specific batch tools." No code changes in this cycle. |
| G6 | accept (per E5) | New tool `asset-exists(path)` returns `{exists: bool, kind: "asset" \| "folder" \| "none"}`. `ReadOnlyHint = true`. Use `AssetDatabase.AssetPathToGUID` for cheap exists check; on hit, additionally call `AssetDatabase.IsValidFolder` to distinguish kind. Apply `path` discipline. |
| G7 | defer (per E7) | Sprite slicing deferred to Texture-domain audit. The name `Tool_Texture.ApplyPattern.cs` strongly suggests sprite-sheet slicing is already addressed there. Texture audit will verify whether `SpriteMetaData[]` construction on TextureImporter is covered; if yes, G7 is closed-elsewhere; if no, Texture audit picks it up as its own finding. Matches GameObject precedent (E6 deferred G4/G6 to Component/Transform audits). |
| G8 | accept (per E2) | Add `ObjectReference` case to the extracted parser in `Tool_Asset.Shared.cs`: when target property is `SerializedPropertyType.ObjectReference`, treat input string as asset path, call `AssetDatabase.LoadAssetAtPath(path, expectedType)`, return error on null. Document type-mismatch behavior in `[Description]`. |
| G9 | accept | Code hygiene, audit Confidence: high. Remove dead `using SimpleJSON`, `Unity.VisualScripting.YamlDotNet.Core.Tokens`, `static UnityEngine.EventSystems.EventTrigger`, `static UnityEngine.GraphicsBuffer` from `Tool_Asset.Create.cs`. NOTE: with E1's deletion of `asset-create`, the entire file `Tool_Asset.Create.cs` is removed — G9 is subsumed by E1. If any code from `Tool_Asset.Create.cs` is salvaged for `Tool_Asset.CreateRenderTexture.cs`, ensure the new file does not carry the dead `using` directives forward. |

---

## 2. Open Questions Answered

The audit's Section 7 lists four open questions for the reviewer.

| Question | Answer |
|----------|--------|
| Is `asset-create` intended to remain a general dispatcher or should it be replaced entirely by per-type creators? This drives whether R1 is "consolidate by absorbing" or "consolidate by deletion". | **Consolidate by deletion.** Delete `asset-create` entirely; add `asset-create-render-texture` for the one type without a dedicated creator. Per-type creators are the canonical path. (E1 Option 1) |
| Is AssetBundle support (G3) still in scope for v1.x, or has it been deprecated in favour of Addressables? If the latter, demote G3 to "won't fix". | **Deprecated in favour of Addressables.** Demote G3 to won't-fix. Document the rejection in `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md`. (E4 Option 2) |
| Should batch operations (G5) be domain-local or routed through `BatchExecute`? Decision affects the planner's signature design. | **Defer to v2.x.** `BatchExecute` is the right architectural home but its current production-readiness has not been verified by the audit. Re-evaluate when `BatchExecute` is validated. (E6 Option 4) |
| The `propertiesJson` parameter on `asset-create` (A2 / D5) — was this intentionally wired through but still being designed, or is the description outdated? If the former, document the intended scope; if the latter, fix the description. | **Moot — `propertiesJson` dies with `asset-create`** (E1 Option 1 deletes the tool). The new `asset-create-render-texture` uses strongly-typed params (no JSON blob). |

---

## 3. Constraints For The Plan

### Backward Compatibility

- [ ] Must preserve all existing tool names (no breaking renames)
- [ ] May rename tools but must add deprecation aliases
- [x] **May break tool names freely (this is internal refactor)**

E1 deletes `asset-create` outright; E3 / E5 add four new tools (`asset-set-labels`, `asset-find-references`, `asset-exists`, `asset-create-render-texture`). Internal refactor; package at v1.1; no external consumer claims in audit. Matches the GameObject precedent.

### Code Style

- Follow existing CLAUDE.md C# standards strictly (braces on all `if`, no empty catches, `EntityIdToObject` not `InstanceIDToObject`, no `obj?.prop = x` null-conditional assignment, partial-class single-summary rule).
- All new tool descriptions must come from `[System.ComponentModel.Description]` attribute on the method (NOT `toolAttr.Description`, which is always empty — see CLAUDE.md "Critical").
- **Sentinel convention from GameObject cycle:** Boolean parameters that need a "leave unchanged" sentinel use the string form `"true" | "false" | ""`. Do NOT introduce new int tri-state booleans. **Applicability in this cycle:** none. Per Ramon's E3 correction, `asset-set-labels.clearExisting` is a plain `bool` (not a sentinel) because every label-write call must commit to overwrite-or-append — there is no third state to represent. No other new tools introduce optional booleans.
- Mark inspection-only / read-only tools with `ReadOnlyHint = true` on `[McpTool(...)]`. **Required for:** `asset-find-references`, `asset-exists`. Confirm `asset-set-labels` has `ReadOnlyHint = false`.

### Scope Limits

- Do NOT touch the Material, Physics, ScriptableObject, or Animation domains in this cycle. Per E1's resolution, `asset-create` is deleted (zero cross-domain reach); the per-type creators in those domains are untouched.
- Do NOT touch `Tool_Object.Modify.cs` for R2's shared-parser extraction. Per E2 Option 2, the extraction is Asset-scoped only. Carry a breadcrumb to the Object-domain audit: "Tool_Object.Modify.cs holds a third copy of the parser. When that audit lands, fold into `Tool_Asset.Shared.cs` (or extract one level up to `Editor/Tools/_Shared/`)."
- Do NOT touch the Texture domain. Per E7, G7 (sprite slicing) is deferred to the Texture-domain audit.
- Do NOT pull v2.0 features into this cycle — chat UI, orchestrator, plans tab, etc. are out of scope per `docs/internal/roadmap.md`. (Reminder: per `CLAUDE.md`, tool consolidation is paused until v2.1.x. This review exists as input for that future cycle; the planner should not assume immediate execution.)
- Do NOT edit `CLAUDE.md` as part of this cycle. Any new project conventions surfaced here become Ramon's follow-up commit.

### Preferences

- Description-only fixes (A2 — subsumed by E1, A3, A4, A5, A6, A7, D2, D4, D5 — subsumed by E1) are quick wins — group them into one PR (see Group A in Section 5).
- Match the existing `path` discipline ("auto-prepend `Assets/`") consistently across new tools (`asset-set-labels`, `asset-find-references`, `asset-exists`, `asset-create-render-texture`).
- Maintain `ReadOnlyHint = true` discipline on all new read-only tools (`asset-find-references`, `asset-exists`).
- Prefer single-purpose tools over action-dispatch when the tool has one verb (per Ramon's GameObject precedent and reaffirmed in E3 / E5 / E6 answers). Action-dispatch reserved for tools with genuinely different verbs.

---

## 4. Priority Override

Use audit ranking as-is.

Top-priority bundle (R1 / A1 / D1 / G2 / A2 / D5) collapses into a single change set — the deletion of `asset-create` and addition of `asset-create-render-texture` — and remains #1 priority. Relative ordering of remaining items is unaffected.

---

## 5. Change Group Hints (Optional)

The planner is free to regroup; here is the suggested grouping after all escalation answers are applied.

### Group A — Description polish (quick win, ship first)
- Findings: A3, A4, A5, A6, A7, D2, D4
- Rationale: Pure description edits on tools that survive this cycle. No behavioral impact, no signature changes, no risk. Land first to clear noise. Independent of all other groups.
- Note: A2 and D5 are subsumed by Group C (deletion of `asset-create`); G9 is subsumed by Group C (file deletion).

### Group B — Shared parser + ObjectReference write (E2)
- Findings: R2, G8
- Rationale: Cohesive refactor. New file `Tool_Asset.Shared.cs` (partial), move `ApplyStringValueToProperty` + value-coercion out of `Tool_Asset.ImportSettings.cs`, add ObjectReference write case. Closes G8 as part of the extraction. Self-contained within Asset domain.

### Group C — `asset-create` deletion + `asset-create-render-texture` addition (E1)
- Findings: R1, A1, D1, A2, D5, G2, G9
- Rationale: All driven by E1 Option 1. Delete `Tool_Asset.Create.cs`. Add `Tool_Asset.CreateRenderTexture.cs` with signature `(assetPath, width, height, depth = 24, format = "ARGB32", filterMode = "Bilinear")`. Rewrite Asset domain summary on the partial-class `[McpToolType]` file (A1). All sub-findings (A2 propertiesJson, D5 propertiesJson default, D1 silent assetType default, G2 no ScriptableObject case, G9 dead usings) are resolved by the deletion.

### Group D — New capability tools (E3, E5)
- Findings: G1, G4, G6
- Rationale: Three new read/write tools that all follow the same conventions (`path` discipline, `MainThreadDispatcher.Execute`, `[Description]` performance warnings where relevant). Can ship together as a coherent "new capabilities" PR.

### Group E — Decision documentation (E4)
- Findings: G3
- Rationale: Single doc file creation: `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md`. Optional one-line addition to `post-v2.0-backlog.md`. No code changes.

### Group F — Backlog entries (E6, E7)
- Findings: G5, G7
- Rationale: Doc-only. Add one-line entries to `post-v2.0-backlog.md` for batch ops (G5) and sprite slicing verification (G7 — flagged to Texture audit). Can be a single tiny commit.

### Deferred (NOT in this cycle, by Ramon's escalation answers)
- G3 (AssetBundles) — won't-fix per E4
- G5 (batch ops) — deferred to v2.x per E6
- G7 (sprite slicing) — deferred to Texture-domain audit per E7

---

## 6. Notes For The Planner

**Cross-domain heads-up (carry forward to other domain audits):**

- **Material / Physics / ScriptableObject / Animation:** With E1 Option 1 chosen, `asset-create` is deleted and these domains are untouched. The per-type creators remain canonical. When those domains get their own audits, no cross-domain coordination is needed — `asset-create` will already be gone.
- **Object domain (`Tool_Object.Modify.cs`):** Carries a third copy of the SerializedObject-write parser (R2). The Asset audit did NOT deep-read it. When the Object domain is audited, expect R2 to surface there; resolution should fold into `Tool_Asset.Shared.cs` (or extract one level up to `Editor/Tools/_Shared/`).
- **Texture domain:** `Tool_Texture.Configure.cs`, `Tool_Texture.Inspect.cs`, `Tool_Texture.ApplyPattern.cs` were not deep-read. G7 (sprite slicing) deferred to Texture audit per E7. Texture audit must verify whether sprite-sheet slicing (`SpriteMetaData[]` construction on TextureImporter) is covered by `Tool_Texture.ApplyPattern.cs`. If yes, G7 is closed-elsewhere; if no, Texture audit picks it up as its own finding. The Asset audit also noted potential overlap with import-settings for textures specifically (`textureType`, `spriteImportMode` flags) — flag for Texture audit to determine whether `asset-set-import-settings` should defer texture-specific writes to dedicated tools.
- **Meta domain (`Tool_Meta.*.cs`):** Audit noted these exist but didn't deep-read them. Meta files overlap with import settings; flag for Meta audit to check for `.meta`-file workflows that may duplicate import-settings tooling.
- **`BatchExecute` infrastructure:** Per E6, batch ops in Asset domain are deferred until `BatchExecute` is validated as production-ready. When `BatchExecute` gets its own validation/audit, revisit G5.

**Pattern reuse:**

- `path` discipline ("auto-prepend `Assets/`"): consistent in 9 of 10 tools today. Apply to all four new tools (`asset-create-render-texture`, `asset-set-labels`, `asset-find-references`, `asset-exists`).
- `ReadOnlyHint = true` discipline: required on `asset-find-references` and `asset-exists`. Confirm `asset-set-labels` and `asset-create-render-texture` do NOT have it (both mutate).
- `MainThreadDispatcher.Execute(...)`: all AssetDatabase calls must be wrapped per CLAUDE.md. Verify on every new tool.
- Partial-class rule: `Tool_Asset.Shared.cs` (E2) and `Tool_Asset.CreateRenderTexture.cs` (E1), `Tool_Asset.SetLabels.cs` (E3), `Tool_Asset.FindReferences.cs` (E5), `Tool_Asset.Exists.cs` (E5) — all are partial-class continuations and must NOT carry an `[McpToolType]` summary (that lives on exactly one file in the domain per CLAUDE.md).

**E1 cascade — fully resolved:**

- G2 (no ScriptableObject case) is moot: `scriptableobject-create` was always the right path.
- A2 / D5 (`propertiesJson`) are moot: the param dies with the tool.
- D1 (silent `assetType="Material"` default) is moot: the param dies with the tool.
- G9 (dead usings in `Tool_Asset.Create.cs`) is subsumed: the file is deleted. If any code is salvaged for `Tool_Asset.CreateRenderTexture.cs`, ensure dead usings are not carried forward.

**Audit Quality Caveats to respect (from audit Section 0):**

- 10/10 files in Asset domain were analyzed; no gaps within the domain.
- Cross-domain duplication (R2, R1) was confirmed by Grep but the foreign files were not deep-read. Planner must read foreign files before touching them — and per Section 3, the planner does NOT touch them in this cycle.
- Absence claims (e.g. "no `SetLabels` tool exists anywhere") were verified by project-wide Grep — these are reliable inputs.

**Out-of-scope reminders (from roadmap.md and CLAUDE.md):**

- Tool consolidation is paused until v2.1.x per `CLAUDE.md`. This review is being drafted as input for that future cycle; the planner should not assume immediate execution.
- v2.0 features (chat UI, orchestrator, plans tab, rules page) are out of scope.
- Ramon owns all git operations. The consolidator agent edits files only.
- Filesystem MCP is unreliable on `@` paths (Unity PackageCache). If the planner needs to reference paths, use repo paths under `Editor/Tools/Asset/`.

---

## 7. Approval

**Status:** ✅ READY FOR PLANNING

All 23 audit findings have concrete decisions. The 7 escalations were answered by Ramon on 2026-05-19 and merged into Sections 1–6 above. Section 9 below preserves the full escalation Q&A as an audit trail for the planner.

Next step: invoke `consolidation-planner` with this review file as input.

---

## 9. Resolved Escalations (Audit Trail)

The original escalation block has been resolved. Ramon's answers are preserved verbatim below as context for the planner. The decisions are already merged into Sections 1–6; this section exists so the planner can see the full reasoning if a detail in the merged decisions is ambiguous.

---

### E1 — `asset-create` identity: dispatcher, narrow, or deprecate? — RESOLVED

**Resolution:** **Option 1** — delete `asset-create` entirely, add `asset-create-render-texture` as the dedicated creator for the one type without a home.

**Ramon's rationale:** Per-type creators are demonstrably better (Material with shader detection, PhysicMaterial with friction/bounciness, ScriptableObject already exists). Keeping `asset-create` as a narrow fallback (Option 2) preserves the misleading name and reopens the dispatcher identity problem the moment a second type without a dedicated creator appears. Option 1 enforces a clean rule: one asset type = one creator. If a new type X needs creation, add `asset-create-X`. No fallback semantics, no propertiesJson ambiguity.

**Sub-decisions:**
- `propertiesJson`: dies with `asset-create`.
- `asset-create-render-texture` signature: `(assetPath, width, height, depth = 24, format = "ARGB32", filterMode = "Bilinear")`. Strongly typed params, no JSON blob. Add `MainThreadDispatcher.Execute` wrap per CLAUDE.md.
- G2 (no ScriptableObject case in `asset-create`) becomes moot — `scriptableobject-create` was always the right path.
- Cross-domain reach in this cycle: ZERO. `material-create`, `physics-create-material`, `scriptableobject-create`, `animation-configure-controller` are untouched. The Asset audit just stops claiming to do their job.
- A1: rewrite the Asset domain summary to no longer claim "create any asset"; the domain covers find/get-info/import-settings/copy/move/delete/rename/create-folder + the new `asset-create-render-texture` and `asset-create-folder`. Type-specific creation lives in the type's own domain.

---

### E2 — Shared SerializedObject-write parser + ObjectReference write gap — RESOLVED

**Resolution:** **Option 2** — Asset-scoped extraction + G8 fix in `Tool_Asset.Shared.cs` partial. Do not touch `Tool_Object.Modify.cs`.

**Ramon's rationale:** The audit did not deep-read `Tool_Object.Modify.cs`, and Section 3 of this review explicitly bars touching it. A cross-domain helper extraction without reading the foreign callsite is exactly the kind of move that produces silent regressions. Option 2 fixes G8 (adds ObjectReference write support — string path → `AssetDatabase.LoadAssetAtPath` → `SerializedProperty.objectReferenceValue`), consolidates the parser within Asset, and leaves a clean breadcrumb for the eventual Object-domain audit to fold its copy into the shared helper.

**Implementation notes for the planner:**
- New file: `Editor/Tools/Asset/Tool_Asset.Shared.cs` (partial class continuation, no `[McpToolType]` summary per CLAUDE.md partial-class rule).
- Move `ApplyStringValueToProperty` + value-coercion logic out of `Tool_Asset.ImportSettings.cs` into the shared file.
- Add `ObjectReference` case: when target property is `SerializedPropertyType.ObjectReference`, treat input string as asset path, call `AssetDatabase.LoadAssetAtPath(path, expectedType)`, return error on null. Document type-mismatch behavior in `[Description]`.
- With E1 Option 1, `asset-create`'s copy of the parser dies with the tool — only `ImportSettings` keeps its callsite.
- Carry a note to the Object-domain audit: "Tool_Object.Modify.cs holds a third copy of the parser. When that audit lands, fold into `Tool_Asset.Shared.cs` (or extract one level up to `Editor/Tools/_Shared/`)."

---

### E3 — Label-write capability (G1) — RESOLVED

**Resolution:** **Option 1** — single tool `asset-set-labels(assetPath, labelsJson, clearExisting)`.

**Ramon's rationale:** Simplest tool covering add / replace / clear. Follows the GameObject precedent of single-verb tools when one verb suffices (GameObject E5 `gameobject-set-sibling-index`).

**Signature corrections to the agent's draft:**
- `clearExisting` is a plain `bool` with default `false` (append). **NOT a string sentinel.** The sentinel convention from the GameObject E3 cycle applies only to params that need a "leave unchanged" state — every label-write call must commit to overwrite-or-append, so there is no third option to represent.
- `labelsJson` is a JSON array of strings. Document the format in `[Description]`.
- `ReadOnlyHint = false`.
- "Clear all labels" workflow: pass `labelsJson = "[]"` with `clearExisting = true`.

No paired getter — `asset-get-info` already returns labels.

**Implementation notes:**
- Use `AssetDatabase.SetLabels(asset, labels)` which replaces the full set. When `clearExisting = false`, prepend current labels (`AssetDatabase.GetLabels`) to the new ones before calling `SetLabels`. Deduplicate.
- Wrap in `MainThreadDispatcher.Execute` per CLAUDE.md.
- Add `path` discipline (auto-prepend `Assets/`) to match the rest of the domain.

---

### E4 — AssetBundle support (G3): keep, demote, or document-as-won't-fix? — RESOLVED

**Resolution:** **Option 2** — demote G3 to won't-fix. Document the rejection in `docs/internal/decisions/`.

**Ramon's rationale:** AssetBundles are legacy; Addressables is the recommended workflow for Unity 6000+ projects and has been for several versions. Adding tooling for a workflow being phased out adds long-term maintenance burden for negligible value. If an Addressables tooling story comes up in a future cycle, it lives as its own domain (Addressables has its own AssetDatabase-level API surface — `AddressableAssetSettings`, etc. — which is significantly larger than the AssetBundle importer fields).

**Action items:**
- Create `docs/internal/decisions/2026-05-19-no-asset-bundle-tooling.md` documenting the rejection rationale so future audits don't re-raise G3.
- Add a one-line entry to `post-v2.0-backlog.md` if Addressables tooling is wanted as a future domain (otherwise nothing).
- No code changes in this cycle.

---

### E5 — Read-only query tools: incoming references (G4) + asset-exists (G6) — RESOLVED

**Resolution:** **Option 1** — two separate tools: `asset-find-references(assetPath, maxResults = 100)` and `asset-exists(path)`.

**Ramon's rationale:** Matches the GameObject precedent of single-purpose tools over multi-flag dispatchers. Both are clear read-only queries with well-defined outputs. `asset-exists` is a 5-line wrapper around `AssetDatabase.AssetPathToGUID` that lets the LLM short-circuit before `create`/`copy`/`rename` calls. `asset-find-references` closes the reverse-dependency gap.

**Implementation notes for the planner:**
- `asset-exists(path)` returns `{exists: bool, kind: "asset" | "folder" | "none"}`. `ReadOnlyHint = true`. Use `AssetDatabase.AssetPathToGUID` for cheap exists check; if hit, additionally call `AssetDatabase.IsValidFolder` to distinguish kind. Apply `path` discipline.
- `asset-find-references(assetPath, maxResults = 100)` — performance warning required in `[Description]`: Unity has no reverse-dependency API, so the implementation must enumerate `AssetDatabase.FindAssets("")`, call `AssetDatabase.GetDependencies(candidate, recursive: true)` per candidate, and check if `assetPath` appears in results. This is O(n × avg-deps) in the project; medium projects can take several seconds. Cap with `maxResults` (default 100, document the cap). Add early-exit when cap is hit.
- `ReadOnlyHint = true` on both.
- Return object for `asset-find-references`: `{references: string[], truncated: bool}` where `truncated` flips to true when `maxResults` was hit.

**Performance note for the plan:** if `asset-find-references` proves unacceptably slow in real workflows, a v2.x improvement could add a folder-scope param (`scopeFolder = "Assets"`) to limit the search. Not in this cycle.

---

### E6 — Batch CRUD ops (G5): domain-local or `BatchExecute`? — RESOLVED

**Resolution:** **Option 4** — defer batch operations to v2.x. Re-evaluate when `BatchExecute` infrastructure is verified production-ready.

**Ramon's rationale:** 268 tools already; adding 4 batch variants (Option 1) compounds the surface problem. A single domain-local meta-tool (Option 2) is action-dispatch which we're discouraging unless verbs differ. `BatchExecute` (Option 3) is the right architectural home but the audit did not verify its current state — committing to it without that verification risks shipping a broken integration. Deferring lets `BatchExecute` get its own audit/validation before becoming a public dependency for the LLM.

**Action items:**
- Add an entry to `post-v2.0-backlog.md` under "v2.1.x — Tool Consolidation Continuation": "Batch CRUD ops for Asset domain. Decision deferred from Asset E6 (2026-05-19). Pre-req: verify `BatchExecute` handles AssetDatabase ops on main thread; if confirmed, route through `BatchExecute` rather than adding asset-specific batch tools."
- No code changes in this cycle.

[Ramon's contingency note: "If you know today that BatchExecute is production-ready and correctly handles AssetDatabase + MainThreadDispatcher: flip this to Option 3 with a description-only update to BatchExecute's docs covering asset operations." — left as Option 4 pending that verification.]

---

### E7 — Sprite slicing capability (G7) — RESOLVED

**Resolution:** **Option 1** — defer G7 to Texture-domain audit.

**Ramon's rationale:** The audit explicitly did not deep-read `Tool_Texture.Configure.cs` and `Tool_Texture.ApplyPattern.cs`. The name `Tool_Texture.ApplyPattern.cs` is a strong hint that sprite-sheet slicing is already addressed there. Cross-domain capability claims should be verified by the target domain's own audit, not cherry-picked here. Matches the GameObject precedent (E6 deferred G4 to Component audit and G6 to Transform audit for the same reason).

**Action items:**
- Mark G7 as `defer` with a Section 6 note carried forward: "Texture audit must verify whether sprite-sheet slicing (`SpriteMetaData[]` construction on TextureImporter) is covered by `Tool_Texture.ApplyPattern.cs`. If yes, G7 is closed-elsewhere. If no, Texture audit picks it up as its own finding."
- No Asset-domain work in this cycle for G7.

---
