# Review — GameObject

**Audit reviewed:** `.claude/reports/audits/audit-GameObject-20260425.md`
**Reviewer:** Ramon (drafted by auto-reviewer agent, finalized after Ramon answered escalations)
**Date:** 2026-04-25
**Status:** ✅ READY FOR PLANNING

---

## 0. Auto-Decision Summary

This review was drafted by the auto-reviewer agent and finalized after Ramon answered the escalation block.

**Findings auto-decided by agent:** 10 (10 accepted, 0 rejected, 0 deferred)
**Findings decided by Ramon via escalations:** 14 (across 6 escalation bundles E1–E6)

**Auto-decision rationale (initial draft):**

The audit is well-structured: 14 findings, most carrying explicit recommendations or "Confidence: high" tags. The agent auto-accepted the 10 findings that are pure description-only fixes (A2, A3, A4, A5, A7, A8, D5, D6, R3, plus A1 which is a description tag tied to the larger D1 escalation) — these all have a single defensible direction (apply the audit's wording suggestion) and no design alternatives.

The remaining 14 findings were grouped into 6 escalations because each carries a strategic decision Ramon should own. All six have now been answered (see Section 1 for resolved per-finding decisions).

**Resolution summary of the escalations (Ramon's final answers):**

- **E1** (R1, R2, A6): Option 3 — deprecate `gameobject-select`, keep `gameobject-move-relative`, add disambiguation; collapse `objectName`+`objectPath` into single `target` param during deprecation window.
- **E2** (G3, D2, D3, D4, R4): Option 3 (maximal) — expand `gameobject-create` with `parentInstanceId`, `tag`, `layer`, `isActive`, `isStatic`, `worldPositionStays`. Keep as single-verb tool. Use string sentinel from E3 for booleans.
- **E3** (D1, A1): Option 2 — switch to string sentinel `"true" | "false" | ""` for booleans across the project. Update CLAUDE.md to document the convention.
- **E4** (G1): Option 2 — new dedicated tool `gameobject-create-sprite` with sprite-specific param block.
- **E5** (G2): Option 1 — single new tool `gameobject-set-sibling-index` with `index = -1` for last; defer before/after-sibling action-dispatch as YAGNI.
- **E6** (G4, G5, G6): G4 → defer to Component-domain audit; G5 → add `searchAllScenes = false` param to `gameobject-find`; G6 → defer to Transform-domain audit.

**Backward compatibility resolution:** Ramon's E1 answer deprecates `gameobject-select` (a tool name removal), so backward compat is set to "May break tool names freely (internal refactor)." See Section 3.

---

## 1. Decisions Per Finding

| Finding ID | Decision | Notes |
|-----------|----------|-------|
| R1 | accept-with-modification | Per E1 (Option 3): keep both `gameobject-move-relative` and `transform-move`. Add cross-reference disambiguation in `[Description]` of each. The named-direction frame ("forward", "up") is a real LLM ergonomic affordance worth preserving. |
| R2 | accept | Per E1 (Option 3): deprecate `gameobject-select` — `selection-set` cleanly subsumes it and auto-select side effects on `create`/`duplicate` cover the "keep newly-created object selected" case. |
| R3 | accept-with-modification | Auto-decided: description-only fix. Add disambiguation sentence to both `gameobject-look-at` and `transform-rotate` `[Description]` ("Use look-at when you have a target position; use rotate when you have explicit Euler angles"). |
| R4 | accept | Per E2 (Option 3 maximal): resolved by expanding `gameobject-create` to accept tag, layer, active, static, parent, `worldPositionStays`. Create-vs-update coverage seam closes; `gameobject-create` becomes a one-call macro for the common populated-GameObject workflow. |
| A1 | accept | Auto-decided: description-only fix. Apply description tweaks consistently with the new string-sentinel convention from E3 ("`true` / `false` / empty leaves unchanged"). |
| A2 | accept | Auto-decided: description-only fix recommended directly by audit. Add "case-insensitive" to the `primitiveType` `[Description]`. |
| A3 | accept | Auto-decided: description-only fix, low-confidence finding but cheap to apply. Add "(layer names with spaces are accepted; typo'd names return not-found)" to `searchTerm` description for `by_layer` mode. |
| A4 | accept | Auto-decided: description-only fix recommended directly by audit. Tighten both `parentInstanceId` and `parentPath` param descriptions in `gameobject-set-parent` to make joint emptiness explicit ("To unparent, leave BOTH this and parentPath empty"). |
| A5 | accept | Auto-decided: description-only fix recommended directly by audit. Add "Ambiguous names target the first match in undefined hierarchy order — use a full path for deterministic targeting" to `targetName`. |
| A6 | accept-with-modification | Per E1 (Option 3): since `gameobject-select` is being deprecated, collapse `objectName`+`objectPath` into a single `target` param during the deprecation window for any callers that still hit it. |
| A7 | accept | Auto-decided: description-only fix recommended directly by audit. Move the 3-tier priority (referenceObject > worldSpace > self) from XML `<summary>` into the `[Description]` attribute that the LLM actually sees. |
| A8 | accept | Auto-decided: description-only fix. Add "Searches active scene only — additively-loaded scenes are not traversed" to the `gameobject-find` `[Description]`. (The deeper additive-scene fix is G5 → resolved via param addition.) |
| D1 | accept | Per E3 (Option 2): switch boolean params from int tri-state (`1/0/-1`) to string sentinel (`"true" | "false" | ""`). Unifies with the existing `""` convention used for `name` and `tag`. Project-wide convention; `CLAUDE.md` update is a follow-up. |
| D2 | accept | Per E2 (Option 3 maximal): add `tag = ""`, `layer = -1`, `isActive = ""`, `isStatic = ""` to `gameobject-create`. (Booleans use E3's string sentinel.) |
| D3 | accept | Per E2: add `parentInstanceId` to `gameobject-create`. Audit's high-confidence recommendation, also part of the maximal expansion. |
| D4 | accept | Per E2 sub-question: expose `worldPositionStays = true` (additive, default preserves current behavior). Document that `false` causes `posX/Y/Z` to be interpreted as local relative to the parent. |
| D5 | accept | Auto-decided: description-only fix recommended directly by audit. Document the 500 cap explicitly in `maxResults` `[Description]`. Behavior change (warn-on-clip) is deferred. |
| D6 | accept-with-modification | Auto-decided: description-only fix is safe and addresses the surprise. Add "When no `targetName` and no explicit target coords are provided, the source object will look at world origin (0,0,0)" to `gameobject-look-at` `[Description]`. Behavior change (require explicit target mode) NOT applied. |
| G1 | accept | Per E4 (Option 2): new dedicated tool `gameobject-create-sprite(name, posX, posY, posZ, parentPath = "", parentInstanceId = -1, spritePath = "", sortingLayer = "Default", orderInLayer = 0)`. Symmetric to `gameobject-create` but with sprite-specific params; both tools live side by side. Non-negotiable scope for v1.2 (Jurassic Survivors is 2D). |
| G2 | accept-with-modification | Per E5 (Option 1): single new tool `gameobject-set-sibling-index(instanceId, objectPath, index)` where `index = -1` means last and `index = 0` means first. Action-dispatch (before/after-sibling) deferred as YAGNI; purely additive if needed later. |
| G3 | accept | Per E2 (Option 3 maximal): atomic create-with-properties resolved by signature expansion. |
| G4 | defer | Per E6: defer to Component-domain audit. Anchored-subtree component search genuinely belongs in another domain's audit pass; forcing it here would likely produce code the next audit tears out. |
| G5 | accept-with-modification | Per E6: scope down to a single param addition. Add `searchAllScenes = false` to `gameobject-find`. Stays entirely within GameObject domain, no domain-bleed. |
| G6 | defer | Per E6: defer to Transform-domain audit. Lightweight transform-get clearly belongs at `transform-get`, not as a duplicated slice in GameObject. Note carried forward in Section 6 as a heads-up for the Transform plan. |

---

## 2. Open Questions Answered

The audit's Section 7 lists three open questions for the reviewer.

| Question | Answer |
|----------|--------|
| Should `transform-move` and `gameobject-move-relative` be merged, or should one explicitly cite the other in its `[Description]`? | Keep both — add cross-reference disambiguation in each tool's `[Description]`. The named-direction frame in `gameobject-move-relative` is a real LLM ergonomic affordance worth preserving and would be lost as a flag inside `transform-move`'s already-large signature. (E1 / Option 3.) |
| Should `gameobject-select` be deprecated in favor of `selection-set` plus auto-select side effects? Or should `selection-set` absorb the `ping` flag? | Deprecate `gameobject-select`. `selection-set` cleanly subsumes its functionality, and auto-select side effects on `create`/`duplicate` cover the common "keep newly-created object selected" case. Collapse `objectName`+`objectPath` into a single `target` param during the deprecation window. (E1 / Option 3.) |
| Is the int-tri-state convention in `gameobject-update` deliberate (transport constraint) or organic? | Organic, not a transport-layer requirement. Switch booleans to string sentinel `"true" | "false" | ""`, unifying with the existing `""` convention used for `name` and `tag`. This is a project-wide decision applicable to all 41 domains; update `CLAUDE.md` as a follow-up to document the convention. (E3 / Option 2.) |

---

## 3. Constraints For The Plan

### Backward Compatibility

- [ ] Must preserve all existing tool names (no breaking renames)
- [ ] May rename tools but must add deprecation aliases
- [x] **May break tool names freely (this is internal refactor)**

**Resolution:** Ramon's E1 answer deprecates `gameobject-select` (a tool name removal). E4 adds a new tool name (`gameobject-create-sprite`). E5 adds a new tool name (`gameobject-set-sibling-index`). E2 expands `gameobject-create`'s signature with new params (purely additive — defaults preserve current behavior, no breakage). E3 changes the type of `isActive`/`isStatic` params on `gameobject-update` from `int` to `string` — this IS a signature break. Box 3 ("may break freely") is therefore the correct setting.

**Note for the planner on the deprecation window for `gameobject-select`:** Since box 3 permits free breakage, the planner may either delete `gameobject-select` outright or keep a thin shim that forwards to `selection-set` (with a `[Obsolete]` attribute). Default to deletion unless the planner sees a callsite-survey reason to shim.

### Code Style

- Follow existing CLAUDE.md C# standards strictly (braces on all `if`, no empty catches, `EntityIdToObject` not `InstanceIDToObject`, no `obj?.prop = x` null-conditional assignment, partial-class single-summary rule).
- Prefer action-dispatched consolidation when a tool grows beyond ~5 params with sentinels — see `Tool_Animation.ConfigureController.cs` pattern as exemplar. (Note: Ramon explicitly rejected action-dispatch for `gameobject-create` in E2 — keep it as a single-verb tool. Action-dispatch is reserved for tools with genuinely different verbs.)
- All new tool descriptions must come from `[System.ComponentModel.Description]` attribute on the method (NOT from `toolAttr.Description`, which is always empty — see CLAUDE.md "Critical").
- **New convention from E3:** Boolean parameters that need a "leave unchanged" sentinel use the string form `"true" | "false" | ""`. Do NOT introduce new int tri-state booleans. Migrate `gameobject-update`'s `isActive`/`isStatic` to this form as part of this cycle. The CLAUDE.md update documenting this convention is a follow-up commit, not part of the consolidator's scope.

### Scope Limits

- Do NOT touch the Transform domain in this cycle — it has its own audit pending. Specifically: do NOT migrate `Tool_Transform.FindGameObject` off `EditorUtility.InstanceIDToObject` here; flag for Transform audit instead. (Audit Section 7 calls this out.)
- Do NOT touch the Component, Selection, or Scene domains. Cross-domain references in this plan must be read-only (cite-only).
- G4 (anchored subtree search) and G6 (lightweight transform-get) are explicitly deferred to other domains' audits per E6 — do NOT attempt to fix them here even partially.
- Do NOT pull v2.0 features into this cycle — chat UI, orchestrator, plans tab, etc. are out of scope per `docs/internal/roadmap.md`.
- The CLAUDE.md update documenting the new string-sentinel convention (from E3) is a separate follow-up — the consolidator should NOT edit CLAUDE.md as part of this cycle.

### Preferences

- Description-only fixes (A2, A3, A4, A5, A7, A8, D5, D6, R3, plus A1, A8) are quick wins — group them into one PR if the planner finds it sensible. See Section 6 for the suggested batch.
- Prefer macro tools that wrap full workflows (E2's create-with-properties direction) over chained micro-tools. Ramon explicitly chose the maximal expansion of `gameobject-create` over action-dispatch.
- Maintain symmetry across the domain: every tool that targets an existing GameObject should accept both `instanceId` and `path`. The shared helper `Tool_Transform.FindGameObject(int, string)` is the canonical entry point. The new `gameobject-create` and `gameobject-create-sprite` tools should align here too (D3 adds `parentInstanceId` to create).
- Prefer single-purpose tools over action-dispatch when the tool has one verb (per Ramon's E2/E5 reasoning). Reserve action-dispatch for tools with genuinely different verbs.

---

## 4. Priority Override

Use audit ranking as-is.

(All escalations are now resolved, so the planner can use the audit's priority list directly. The audit's top-3 — D3, G3/D2, G1/R1/G2 — are all in scope for this cycle.)

---

## 5. Change Group Hints

The planner is free to regroup, but here is a suggested grouping that reflects Ramon's escalation answers and supports incremental shipping:

### Group A — Description polish (quick win, ship first)
- Findings: A1, A2, A3, A4, A5, A7, A8, D5, D6, R3
- Rationale: Pure description edits with no behavioral impact. Cheap PR, no signature changes, no risk. Land this first to clear the noise.

### Group B — `gameobject-create` maximal expansion + new sprite tool
- Findings: D2, D3, D4, G1, G3, R4
- Rationale: All touch the create surface. E2 + E4 land together; the new `gameobject-create-sprite` shares the parent-resolution helper with the expanded `gameobject-create`.

### Group C — Sentinel migration on `gameobject-update`
- Findings: D1
- Rationale: Project-wide convention change (string sentinel for booleans). Isolate so the diff is easy to review and the pattern is clear for other domains to follow. Note: this IS a signature break on `isActive`/`isStatic`.

### Group D — `gameobject-select` deprecation + `gameobject-find` extension
- Findings: R1, R2, A6, G5
- Rationale: Cross-tool disambiguation work. R1 adds `[Description]` cross-references between `move-relative` and `transform-move`; R2/A6 deprecate `gameobject-select` and collapse its params; G5 adds `searchAllScenes` to `gameobject-find`.

### Group E — New sibling reorder tool
- Findings: G2
- Rationale: Single new tool, isolated, easy to review.

### Deferred (NOT in this cycle)
- G4 — defer to Component-domain audit
- G6 — defer to Transform-domain audit

---

## 6. Notes For The Planner

**Cross-domain heads-up (carry forward to Transform audit):**
- `Tool_Transform.FindGameObject(int, string)` is used by 9 of 10 GameObject tools but uses deprecated `EditorUtility.InstanceIDToObject` with `#pragma warning disable CS0618`. CLAUDE.md mandates `EntityIdToObject`. **Do NOT migrate here** — flag for Transform audit. Selection domain (`Selection/Tool_Selection.Set.cs:67`) is already migrated and is the reference pattern.
- `gameobject-create` currently bypasses the shared `Tool_Transform.FindGameObject` helper (uses raw `GameObject.Find(parentPath)`). When implementing E2's expansion, route the new `parentInstanceId` lookup through `FindGameObject` to align with the rest of the domain. Same for the new `gameobject-create-sprite`.
- **G6 deferral note for Transform audit:** when Transform domain is consolidated, a `transform-get` tool is the right home for the lightweight transform-read use case. Do NOT add a duplicated slice in GameObject domain.
- **G4 deferral note for Component audit:** anchored-subtree component search (start from a node, not scene roots) is the missing capability. Component-domain may already have an analog via `component-list` recursion — verify in that audit.

**E3 follow-up (out of scope for this cycle):**
- After the consolidator lands, update `CLAUDE.md` to document the string-sentinel convention `"true" | "false" | ""` for nullable boolean params. Future audits should flag any domain still using int tri-state booleans as a finding to migrate. The consolidator does NOT edit `CLAUDE.md` — that's Ramon's commit.

**Description-only quick-win batch (Group A):**
The auto-decided description fixes (A1, A2, A3, A4, A5, A7, A8, D5, D6, R3) are mechanical edits with no behavioral impact. Group A in Section 5 captures these for cheap shipping ahead of the larger structural changes from Groups B–E.

**Pattern reuse:**
- Symmetric `instanceId` + `path` lookup pattern: every GameObject tool except `create` (today). After E2 lands, `gameobject-create` and `gameobject-create-sprite` should also follow this pattern via `Tool_Transform.FindGameObject`.
- Action-dispatch pattern reference: `Editor/Tools/Animation/Tool_Animation.ConfigureController.cs` (per CLAUDE.md). Ramon explicitly opted OUT of action-dispatch for both `gameobject-create` (E2) and the new sibling-reorder tool (E5) — those stay as single-verb tools.

**Signature changes that ARE breakage (per Section 3 box 3):**
- `gameobject-update`: `isActive`/`isStatic` change from `int` to `string` (E3). Existing callers passing `1`/`0`/`-1` will fail.
- `gameobject-select`: deprecated/removed (E1).
- `gameobject-create`: signature expansion is purely additive (new params with defaults that preserve current behavior); not breakage.

**Out-of-scope reminders (from roadmap.md and CLAUDE.md):**
- v1.2 only. No v2.0 architecture work bleeds in.
- Ramon owns all git operations. The consolidator agent edits files only.
- Filesystem MCP is unreliable on `@` paths (Unity PackageCache). If the planner needs to reference paths, use repo paths under `Editor/Tools/GameObject/`.

---

## 7. Approval

**Status:** ✅ READY FOR PLANNING

All escalations resolved. The `consolidation-planner` agent can now be invoked with this review file as input.
