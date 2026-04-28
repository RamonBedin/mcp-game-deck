# Audit Batch Summary

**Date:** 2026-04-25 (with batch 7/8 spilling into 2026-04-26)
**Runner:** audit-batch-runner agent
**State file:** `.claude/state/audit-batch-progress.json`

---

## Results

**Total domains:** 41
**Completed:** 41
**Skipped (no McpTool):** 0
**Failed:** 0

Notes:
- `Animation` was a pre-existing audit from 2026-04-17, not re-run.
- `Terrain` and `Texture` agents in batch 7 returned a usage-limit message in their final-message frame, but the audit reports were written to disk in full (verified 231 and 255 lines respectively, with completed Section 7 / Open Questions blocks). They are recorded as `completed` with a note in the state file.

---

## Completed Audits (sorted by findings count, highest first)

| Domain | Findings | Audit File |
|--------|----------|------------|
| Physics | 26 | [audit-Physics-20260425.md](audit-Physics-20260425.md) |
| Camera | 24 | [audit-Camera-20260425.md](audit-Camera-20260425.md) |
| Graphics | 23 | [audit-Graphics-20260425.md](audit-Graphics-20260425.md) |
| ProBuilder | 23 | [audit-ProBuilder-20260425.md](audit-ProBuilder-20260425.md) |
| Asset | 21 | [audit-Asset-20260425.md](audit-Asset-20260425.md) |
| Profiler | 21 | [audit-Profiler-20260425.md](audit-Profiler-20260425.md) |
| GameObject | 20 | [audit-GameObject-20260425.md](audit-GameObject-20260425.md) |
| UIToolkit | 20 | [audit-UIToolkit-20260425.md](audit-UIToolkit-20260425.md) |
| Reflect | 19 | [audit-Reflect-20260425.md](audit-Reflect-20260425.md) |
| Transform | 19 | [audit-Transform-20260425.md](audit-Transform-20260425.md) |
| Animation | 17 | [audit-Animation-20260417.md](audit-Animation-20260417.md) |
| Audio | 17 | [audit-Audio-20260425.md](audit-Audio-20260425.md) |
| Build | 17 | [audit-Build-20260425.md](audit-Build-20260425.md) |
| Editor | 17 | [audit-Editor-20260425.md](audit-Editor-20260425.md) |
| Material | 17 | [audit-Material-20260425.md](audit-Material-20260425.md) |
| Prefab | 17 | [audit-Prefab-20260425.md](audit-Prefab-20260425.md) |
| Scene | 17 | [audit-Scene-20260425.md](audit-Scene-20260425.md) |
| Screenshot | 17 | [audit-Screenshot-20260425.md](audit-Screenshot-20260425.md) |
| Script | 17 | [audit-Script-20260425.md](audit-Script-20260425.md) |
| Texture | 17 | [audit-Texture-20260425.md](audit-Texture-20260425.md) |
| BatchExecute | 16 | [audit-BatchExecute-20260425.md](audit-BatchExecute-20260425.md) |
| Package | 16 | [audit-Package-20260425.md](audit-Package-20260425.md) |
| Component | 15 | [audit-Component-20260425.md](audit-Component-20260425.md) |
| FindInFile | 15 | [audit-FindInFile-20260425.md](audit-FindInFile-20260425.md) |
| ScriptableObject | 15 | [audit-ScriptableObject-20260425.md](audit-ScriptableObject-20260425.md) |
| Terrain | 15 | [audit-Terrain-20260425.md](audit-Terrain-20260425.md) |
| UnityDocs | 15 | [audit-UnityDocs-20260425.md](audit-UnityDocs-20260425.md) |
| AddAssetToScene | 14 | [audit-AddAssetToScene-20260425.md](audit-AddAssetToScene-20260425.md) |
| Console | 14 | [audit-Console-20260425.md](audit-Console-20260425.md) |
| Light | 14 | [audit-Light-20260425.md](audit-Light-20260425.md) |
| Object | 14 | [audit-Object-20260425.md](audit-Object-20260425.md) |
| PlayerSettings | 14 | [audit-PlayerSettings-20260425.md](audit-PlayerSettings-20260425.md) |
| Tests | 13 | [audit-Tests-20260425.md](audit-Tests-20260425.md) |
| RecompileScripts | 12 | [audit-RecompileScripts-20260425.md](audit-RecompileScripts-20260425.md) |
| Shader | 12 | [audit-Shader-20260425.md](audit-Shader-20260425.md) |
| NavMesh | 11 | [audit-NavMesh-20260425.md](audit-NavMesh-20260425.md) |
| Selection | 11 | [audit-Selection-20260425.md](audit-Selection-20260425.md) |
| Meta | 10 | [audit-Meta-20260425.md](audit-Meta-20260425.md) |
| Type | 9 | [audit-Type-20260425.md](audit-Type-20260425.md) |
| VFX | 9 | [audit-VFX-20260425.md](audit-VFX-20260425.md) |
| Ping | 8 | [audit-Ping-20260425.md](audit-Ping-20260425.md) |

This sort surfaces the noisiest domains first — likely best candidates for early consolidation cycles. Note that finding count is a proxy for issue density, not always for impact: small read-only domains (Selection, Meta, Ping) can still hide high-priority correctness bugs.

---

## Skipped Domains (no MCP tools detected)

None — every audited directory contains at least one `[McpTool]`.

---

## Failed Audits

None.

---

## Reviewer Guidance

The next step is **per-domain review cycles**, not batch reviews. For each domain you want to consolidate:

1. Read the audit at `.claude/reports/audits/audit-[domain]-[date].md`
2. Invoke `auto-reviewer` on that domain
3. Answer the escalation block in the review file
4. Re-invoke `auto-reviewer` to finalize
5. Continue with `consolidation-planner` → `tool-consolidator` → `build-validator`

### Suggested priority order

Ranking blends finding count, Tier-1 status (per CLAUDE.md), correctness-bug severity, and cross-domain impact. Top candidates:

1. **GameObject** (20 findings) — Tier 1; sister-helper still uses deprecated `EditorUtility.InstanceIDToObject` (CLAUDE.md violation), 2D Sprite creation gap blocks the Jurassic Survivors test project, no `SetSiblingIndex`. Consolidating early unblocks downstream domains.
2. **Prefab** (17 findings) — Tier 1; 80%+ overlap with `add-asset-to-scene` (cross-domain redundancy), no override apply/revert/inspect tools, footgun default action. Resolving R1 between Prefab and AddAssetToScene must precede AddAssetToScene's own cycle.
3. **Asset** (21 findings) — Tier 1; `asset-create` overlaps three dedicated creators, dead `propertiesJson` description, no asset-label writer. Touches every other domain that creates assets.
4. **Script** (17 findings) — Tier 1; `script-validate` reports false-positive successes, silent-overwrite + silent-skip footguns in `script-create`/`script-apply-edits`. High blast radius for the LLM authoring loop.
5. **Component** (15 findings) — Tier 1; `component-update` only handles 4 of 17+ types displayed by `component-get`, no enable/disable verb, silent-warn-but-success failure pattern.
6. **Editor** (17 findings) — Tier 1; duplicate state tools (`editor-get-state` vs `editor-info`), no Tag/Layer list reads, hidden `GameDeck_` prefix on prefs. Likely also absorbs RecompileScripts.
7. **Scene** (17 findings) — Tier 1; missing `SetActiveScene` and cross-scene `MoveGameObjectToScene`, Build Settings logic duplicated with `build-manage-scenes`.
8. **Selection** (11 findings) — Tier 1; cross-domain redundancy with `gameobject-select`, cannot clear selection, cannot select assets by path. Small but high-leverage.

After Tier 1 is clean, suggested next wave (high-value non-Tier-1):

9. **Camera** (24 findings) — `camera-get-brain-status` has a real correctness bug (loops VCams but never appends them), `camera-set-target` silently nukes Follow on default args, dead `aimType`/`bodyType` parameters.
10. **Physics** (26 findings) — Densest noise; `layerMask` documentation across the cast/overlap family, R2 consolidation candidate, getter/setter asymmetry, plus the strategic 2D-physics question for Jurassic Survivors.

### Cross-cutting decisions surfaced by the batch

These are flagged in multiple audits and warrant a single decision before per-domain cycles begin:

- **PlayerSettings ↔ Build domain merge?** Both audits surface heavy 4-tool overlap with no disambiguation. Decide ownership before starting either cycle.
- **AddAssetToScene → folded into Prefab?** Single-tool domain, 80% overlap with `prefab-instantiate`. Decide before either cycle.
- **Object ↔ ScriptableObject ↔ Component generic-modifier triangle.** Three tools wrap `SerializedObject` editing with subtle behavioural drift (Undo, AssetDatabase.Save, supported types). Consider a unified plan.
- **Reflect ↔ Type domain merge?** Type's `type-get-json-schema` largely duplicates `reflect-get-type` with worse output (invalid JSON, no statics).
- **2D support strategic question.** Sprite GameObject creation, 2D physics, URP `Light2D`, sprite slicing — all gaps. This is a roadmap-scope question, not a per-domain fix; align with Ramon before Tier-1 cycles touch any of these.
- **`EditorUtility.InstanceIDToObject` deprecation sweep.** Identified in GameObject, Object, and Transform audits (shared `FindGameObject` helper). One-shot cross-domain cleanup likely cheaper than per-domain.

---

## Total

41 / 41 domains audited. Total ~676 findings raised. Pipeline ready for per-domain review cycles starting with Tier 1.
