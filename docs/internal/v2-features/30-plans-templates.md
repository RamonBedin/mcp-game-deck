# Feature 30 — Plans Templates + Sharing + Versioning

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

After F24 (plans autocomplete polish), users have a working plans system with MRU, preview, and fuzzy search. Three structural gaps remain that limit growth beyond solo dogfooding:

**(a) No templates.** Every plan starts from a blank file. Common patterns ("set up a new 2D project", "audit URP renderer settings", "regenerate addressables") get recreated by each user from memory. Templates would let "save this as a starting point" workflows exist.

**(b) No sharing.** A plan that works perfectly for one user lives forever in their `ProjectSettings/GameDeck/plans/` and nowhere else. Sharing happens via "I'll email you the .md file" — which is exactly what the modern web should not require.

**(c) No versioning beyond git.** Users can keep plans in git, but there's no in-app concept of "this plan worked, let me snapshot it before I experiment". Editing a plan to try something new risks losing the known-good version.

These gaps gate the leap from "one-developer productivity tool" to "team / community asset".

## Proposal

Three layered capabilities, each independently useful and shippable.

**(a) Templates.** Built-in template library + user-defined templates. Built-in: shipped with the app (e.g., `setup-2d-project`, `audit-urp-renderer`, `clean-build-cache`). User-defined: any plan marked `template: true` in frontmatter shows in the "New plan from template" picker.

**(b) Sharing — export / import.** Right-click a plan → "Export as `.plan.json`" (markdown body + frontmatter + optional preset arguments). Drag a `.plan.json` onto the Plans page → "Import" dialog with preview, conflict resolution if name exists.

**(c) Versioning.** Per-plan snapshot history stored in `Library/MCPGameDeck/plan-snapshots/<plan-name>/` (gitignored, local). Auto-snapshot on each edit (debounced) + manual "Save as snapshot with label" affordance. Snapshot view: side-by-side diff vs current, "Restore this version" button.

**Sharing infrastructure beyond export / import** (community library, package-manager-like discovery) — out of scope for v2.3 initial. Revisit in v2.4+ if export/import gets traction.

## Scope IN

- **Templates:**
  - `template: true` frontmatter field
  - Built-in templates shipped in `Plugin~/templates/plans/` (curated by maintainer)
  - "New from template" picker integrating built-ins + user templates
  - Template instantiation: copy with name prompt, optional placeholder substitution
- **Export / Import:**
  - `.plan.json` format: `{version, plan_name, body, frontmatter, exported_at}`
  - Export menu item per plan
  - Import flow: file dialog or drag-drop, preview, conflict resolution
- **Versioning:**
  - Auto-snapshot on edit (debounced, max 1 snapshot per minute)
  - Manual "Save snapshot" with optional label
  - Snapshot history view per plan
  - Diff vs current (markdown diff renderer)
  - "Restore this snapshot" replaces current
  - Snapshot purge: keep last 20 per plan; user-configurable cap

## Scope OUT (deferred to v2.4+ or wontfix)

- **Community library / marketplace** — discoverable shared plans via a central registry
- **Signed templates** (trust model) — built-in are trusted; user-imported are user's responsibility
- **Template parameter system** — placeholders `<project_name>` substituted on instantiate (basic case ships; complex parameter forms deferred)
- **Diff across branches / forks** — no branching; flat snapshots
- **Snapshot encryption** — local-only, plaintext sufficient
- **Auto-publish to a sharing platform** — manual export only
- **Template "from this conversation"** — generate plan from current chat session

## Dependencies

- **F24 (Plans autocomplete polish)** — recommended before. Templates show in the new picker UI; without F24's MRU + preview + fuzzy, the picker is harder to navigate with more entries.

## Risks

- **Snapshot storage growth** — 20 snapshots/plan × 50 plans × 5 KB each = 5 MB per project. Acceptable. Cap is configurable; surface storage usage in Settings.
- **Import security** — `.plan.json` is markdown + frontmatter. Worst case: malicious plan instructs Claude to do harmful things. F23 permission rules contain blast radius. Mitigation: import preview shows full content; user reviews before importing.
- **Built-in templates becoming stale** — Unity API surface changes; a `setup-2d-project` template using deprecated APIs ages badly. Mitigation: built-in templates versioned with app releases; review pass each major Unity version.
- **Snapshot vs git confusion** — power users have plans in git; redundant snapshots add noise. Mitigation: snapshots are local-only (gitignored), positioned as "experimental safety net" rather than "version control".

## Open questions

1. **Should the snapshot diff use word-level or line-level granularity?**
   - Recommendation: line-level for v2.3 (matches markdown structure better, simpler implementation). Word-level if requests come.
2. **Template instantiation: copy or symlink-like reference?**
   - Recommendation: copy. Users edit the copy without affecting the template. Updates to the template don't propagate to instances (predictable, no surprises).
3. **What's the snapshot cap default?**
   - Recommendation: 20. Roughly two months of moderate use. Users with heavy versioning needs adjust upward.
