# Feature 24 — Plans Autocomplete Polish

## Status

`proposed` — design pending Ramon approval. Picks up F06 (Plans CRUD) deferrals. Companion specs (`24-plans-autocomplete-spec.md` + `24-plans-autocomplete-tasks.md`) will follow when execution starts.

## Problem

F06 (Plans CRUD) shipped a Plans page in the app where users save and manage plan markdown files at `ProjectSettings/GameDeck/plans/`. They invoke plans via `/plan-execute <name>`. After 6 months of dogfooding (per the v2.1 timeline), the workflow exposes three friction points that the F06 design explicitly deferred:

**(a) Alphabetical order in autocomplete is wrong.** When the user types `/plan-execute ` the picker shows plans alphabetically. After accumulating 20+ plans, the one used 5 minutes ago is buried beneath ones not touched in months. Selection requires scrolling/filtering even when the obvious choice is "the same plan I ran before this one".

**(b) No preview in the picker.** Hovering or selecting a plan in the autocomplete shows just the name. To know what the plan actually does, the user has to either cancel, switch to the Plans page, click the plan to view, then come back. Three context switches for one piece of information.

**(c) Strict-prefix matching only.** `/plan-execute snake-` matches plans starting with `snake-`, but `/plan-execute 2d-snake` doesn't match `setup-2d-snake-roguelike` — even though the user clearly meant that plan. Users learn the prefix structure of their plan names instead of just typing whatever they remember.

These are all polish items. The plans system works. But every dogfood session generates the same micro-frictions.

## Proposal

Three small UX additions to the `/plan-execute` autocomplete + picker UI.

**(a) MRU ordering.** Track recent-usage timestamps in a `Library/MCPGameDeck/plans-mru.json` file (per-project, gitignored). On `/plan-execute <name>` execution, bump that plan's `lastUsed` timestamp. Picker orders by `lastUsed` descending, with a separator after the top 5 followed by alphabetical for the rest. Plans with no `lastUsed` (never run) sort alphabetically at the bottom.

**(b) Inline preview.** The picker has two columns: name on left (fixed 240px), preview pane on right. As the user moves through the list (keyboard arrows or hover), the preview shows the first ~10 lines of the plan body (frontmatter stripped). For long plans, this is the description / first step — enough context to confirm.

**(c) Fuzzy search.** Replace prefix matching with fuzzy substring matching scored by (in order of priority): substring location (earlier = better), match density, and last-used recency. Use `fuse.js` or similar lightweight fuzzy library. Threshold: don't show matches below a relevance score, to avoid noise.

## Scope IN

- **MRU tracking:**
  - `Library/MCPGameDeck/plans-mru.json` schema: `{ "version": 1, "entries": [{"name": "snake-roguelike", "lastUsed": "2026-05-19T14:23:45Z"}] }`
  - Bump on plan execution (skill writes via Rust command `bump_plan_mru(name)`)
  - Picker sort: MRU top-5 → separator → alphabetical
- **Inline preview:**
  - Picker UI restructured: name list (left) + preview pane (right)
  - Preview reads plan body (first ~10 lines after frontmatter)
  - Keyboard navigation (↑↓) and mouse hover update preview
  - Cached preview on first read (small files; full re-read per selection is fine)
- **Fuzzy search:**
  - `fuse.js` integration in `App~/src/components/PlansPicker.tsx`
  - Scoring: substring location > density > MRU recency tiebreaker
  - Relevance threshold to suppress noise matches
  - Real-time filtering as user types

## Scope OUT (deferred to v2.3+)

- **Pinned plans** (always at top regardless of MRU) — F30 (Plans templates) might revisit.
- **Plan categories / folders** — flat namespace continues.
- **Plan preview with full markdown rendering** — preview pane shows plain text; no rich markdown render. v2.3 if visual richness is needed.
- **Search across plan body content** (not just names) — name-only search continues.
- **Plan run history beyond MRU** (full log of past runs) — separate F30 plans feature.
- **Per-team MRU sharing** — local-only tracking; team members each have their own MRU.

## Dependencies

None. Independent of other v2.1 features. F12 (Plan execution events) must be shipped (which it is, in v2.0) — but that's a v2.0 dependency, not v2.1.

## Risks

- **Fuse.js performance with large plan counts** — minimal risk at <1000 plans. If a power user accumulates more, fuzzy match is still O(n) per keystroke, well under 16ms.
- **MRU file corruption** — concurrent writes from multiple app instances on the same project (unlikely but possible). Mitigation: atomic write (temp + rename); on parse error, treat as empty MRU and rebuild.
- **Preview pane visual clutter** — 10 lines might be too few or too many depending on plan style. Spec phase tunes the line count; user setting if needed.
- **Fuzzy match surprises** — score might rank an unexpected plan first. Mitigation: relevance threshold + MRU tiebreaker; if user complaint, refine scoring.

## Open questions

1. **Should the MRU sort be the default, or a setting?**
   - Recommendation: default. Settings overload for tiny features. If a user wants alphabetical-only, an opt-out in v2.3.
2. **What if the user renames a plan? Does MRU carry over?**
   - Recommendation: no carry-over (different name = different entry). Renamed plans drop their MRU history.
3. **Preview pane width — fixed or responsive?**
   - Recommendation: responsive (50% of picker dialog width). Picker dialog is itself ~600px wide on most displays.

## Related notes

F06 (Plans CRUD) shipped the core plans system; these are polish layers on top of an already-working workflow. Implementation surface is small: one new JSON file, one Rust command, one React component refactor (`PlansPicker` becomes 2-column with fuzzy search). Estimated 3–4 days.

Coordination with F30 (Plans templates + sharing, v2.3+): MRU and preview don't conflict with templates; both layers compose. Fuzzy search is also future-proof.
