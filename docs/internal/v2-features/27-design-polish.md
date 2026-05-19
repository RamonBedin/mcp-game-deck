# Feature 27 — Design System Polish

## Status

`proposed` — design pending Ramon approval. Picks up F09 (Design Handoff) deferrals. Companion specs (`27-design-polish-spec.md` + `27-design-polish-tasks.md`) will follow when execution starts.

## Problem

F09 (Design Handoff) established the core design system in v2.0: tokens, components, layout grid, motion guidelines. After 6 months of v2.0 dogfooding, two specific UX gaps stand out as worth closing in v2.1, plus a handful of small polish items the design doc explicitly deferred:

**(a) No keyboard shortcut customization.** Built-in shortcuts (Cmd-K command palette, Cmd-N new chat, ESC cancel, etc.) are hard-wired. Power users on alternative keyboard layouts (Dvorak, AZERTY) or with conflicting muscle memory from other apps can't remap. v2.0 chose hard-wired for simplicity; v2.1 should expose at least the high-traffic shortcuts.

**(b) No HUD project switcher.** With multiple Unity projects (and F25's per-project windows), the chat HUD shows the current project as a static pill. To switch projects, the user has to bring up the right window manually. A click-to-switch dropdown on the HUD pill is much faster.

**(c) Various small polish items:** consistent loading skeletons across pages, transition between routes feeling abrupt, missing accessibility labels on a few controls, dark-mode color-contrast outliers on the Tools page.

## Proposal

Three coordinated polish passes.

**(a) Keyboard shortcut customization.** Add a Settings → Keyboard page with a list of every customizable shortcut, current binding, "Edit" button that captures a key combo. Storage in `Library/MCPGameDeck/shortcuts.json` (local, per-user). Conflict detection — can't bind the same combo to two actions. Reset-to-defaults button.

Shortcut categories:
- Chat: New chat, Cancel turn, Focus composer, Insert mode toggle
- Navigation: Switch route (Chat / Plans / Rules / Settings), Toggle sidebar
- Misc: Open command palette, Toggle DevTools (debug builds only)

Implementation uses a central `shortcutsStore` (Zustand) + `useShortcut(name)` hook that subscribes the right keydown listener. Bindings respond live to settings changes (no app restart).

**(b) HUD project switcher.** The current "Project: <name>" pill becomes a dropdown:
- Click → menu of all detected Unity projects
- Active project marked with a check
- Each project shows connection status (green/yellow/red dot)
- "Open in new window" affordance per project (when F25 is shipped)
- "Refresh project list" at the bottom

If F25 hasn't shipped, the switcher still works — clicking another project reloads the current window with that project's session (with confirmation modal "Switch projects? Current session will close"). If F25 has shipped, clicking another project focuses or spawns its window.

**(c) Polish backlog from F09:**
- Audit loading skeletons across Plans, Rules, Settings — standardize to the F09-designed `Skeleton` component
- Route transitions: replace abrupt swap with 150ms cross-fade
- Accessibility audit: missing `aria-label` on icon-only buttons, missing keyboard nav on certain dropdowns, ensure focus rings visible on all interactive elements
- Color-contrast audit: a few text-on-bg pairs in Tools page measured below WCAG AA on dark mode; fix in tokens or add per-component overrides

## Scope IN

- **Keyboard shortcut system:**
  - `shortcutsStore` (Zustand) + `useShortcut(name)` hook
  - `Library/MCPGameDeck/shortcuts.json` storage
  - Settings → Keyboard page with edit + conflict detection + reset
  - ~12 customizable actions documented in spec phase
- **HUD project switcher:**
  - `ProjectSwitcherDropdown` component
  - Reads project list from existing Unity-detection logic
  - F25-aware: opens window if shipped, otherwise reloads current window with confirmation
  - "Refresh" affordance to re-scan
- **Polish backlog:**
  - Skeleton standardization across all routes
  - 150ms cross-fade route transitions
  - Accessibility audit + fixes (target: 95+ Lighthouse a11y score across all routes)
  - Color-contrast audit + fixes (WCAG AA on all text)

## Scope OUT (deferred to v2.3+)

- **Theme customization** (custom accent colors, custom backgrounds) — F28 (Light mode + theming) territory.
- **Layout customization** (resize sidebar, rearrange panels) — fixed layout continues.
- **Animation preferences** (reduced motion toggle) — v2.3 if requests come (system preference is auto-honored via CSS `prefers-reduced-motion`, which v2.1 should respect — included in a11y audit).
- **Internationalization** — F29 (Localization PT-BR) territory.
- **Onboarding tour** — F32 (Onboarding) territory.
- **Custom CSS injection / theming via user files** — power-user feature, v2.3+ at earliest.

## Dependencies

- **F25 (Per-project window isolation)** — strongly recommended. The HUD project switcher composes cleanly with multi-window; without F25, the switcher works in single-window with a confirmation flow but loses much of its value.

## Risks

- **Shortcut conflicts with OS-level bindings** — user can rebind to Cmd-Q, Cmd-W, etc. Mitigation: blocklist OS-reserved shortcuts in the edit flow (warning + allow override anyway).
- **Project list freshness** — if the user opens a new Unity project while the app is open, does the switcher detect it? Mitigation: auto-refresh on focus + manual "Refresh" affordance.
- **Polish scope creep** — once auditing begins, every minor UI imperfection becomes a temptation. Mitigation: ruthlessly time-box polish to 2 days max; defer anything bigger to v2.3.

## Open questions

1. **Should shortcuts be shareable across projects (single Library file) or per-project?**
   - Recommendation: per-user (Library), not per-project. Shortcuts are about the user's muscle memory, not project conventions.
2. **Switcher: should "all projects" view also include closed Unity projects (recent-projects list)?**
   - Recommendation: not in v2.1. Only currently-running Unity projects appear. Recent-projects belongs to OS / Unity Hub.
3. **Polish backlog: is there a checklist somewhere of all known F09 imperfections?**
   - Recommendation: spec phase builds the checklist from a fresh app audit. No persistent backlog tracker (would be over-engineering).

## Related notes

F09 (Design Handoff) established the design foundation; F27 closes its known gaps and adds two genuine functional features (shortcuts, project switcher) within the design system's existing language. Estimated 4–6 days, of which 2 days is the polish audit + fixes.

Coordinate with F28 (Light mode + theming, v2.3+) timing — both touch the design tokens; if F27 lands first, F28 builds on its updated baseline. If F28 is on a parallel branch, careful merge planning.
