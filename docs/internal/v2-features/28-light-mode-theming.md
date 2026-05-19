# Feature 28 — Light Mode + Custom Theming

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

The app is dark-only. Three groups of users feel this friction:

- Users working in bright environments (daylight near windows, outdoor coworking) where dark mode strains rather than helps
- Users with light-sensitivity / migraine triggers who specifically need bright UI for accessibility
- Studios where IDE / Editor / Slack / etc. are all configured light-themed and the dark app feels visually jarring next to them

A secondary need: studios sometimes want subtle branding (accent color matching their game's palette, or matching the studio's brand identity). Not a deep customization need, but currently zero customization exists.

## Proposal

Token-driven theme switching. Three modes: `light`, `dark` (current), `system` (follows OS preference). Persisted in `Library/MCPGameDeck/preferences.json` per user (not per project).

Design tokens in `App~/src/styles/tokens.css` (or wherever F09 landed them) refactored to use CSS custom properties scoped via `[data-theme="light"]` / `[data-theme="dark"]`. Components consume tokens unchanged; the theme switch is a single attribute flip on `<html>`.

Accent color customization: a single user-chosen accent (default cyan, but selectable from a palette of 6–8 sane options). Stored alongside theme preference. Used for primary buttons, focus rings, active-state highlights.

Power-user theme files (custom token overrides via a `Library/MCPGameDeck/custom-theme.css` that the app reads if present) — out of scope for v2.3 initial ship; revisit later if requests come.

## Scope IN

- Three-mode toggle (light / dark / system) in Settings → Appearance
- Token refactor for light/dark parity — every dark token gets a light equivalent
- Accent color picker (6–8 preset palette)
- Persistence in `preferences.json`
- System-mode listener for OS preference changes (`matchMedia('(prefers-color-scheme: dark)')`)
- Color-contrast verification in light mode — WCAG AA on all text/bg pairs
- Smooth transition between modes (CSS transition on `color`, `background-color` — 150ms)

## Scope OUT (deferred to v2.4+ or wontfix)

- Custom CSS theme files (user-provided) — power-user, low demand projection
- Per-route theme overrides — global theme only
- Auto-switch based on time of day — system mode covers most users
- High-contrast mode (separate from dark) — accessibility-specific, would be a follow-up
- Custom font families — Inter / system font continues

## Dependencies

- **F27 (Design system polish)** — strongly recommended before. F27 audits / tightens tokens; F28 then refactors token-system without re-auditing.

## Risks

- **Token churn breaking components silently** — a few components may have inlined colors that bypass tokens. Audit pass needed. Mitigation: search for hex values / `rgb()` in component files during spec phase.
- **Image / icon contrast in light mode** — icons currently rendered as white-on-dark. Need automatic inversion or per-theme icon sets. Mitigation: design icon set as SVG with `currentColor`, theme-agnostic.
- **Third-party components** (e.g., chart libraries) — hardcoded dark themes that don't follow tokens. Mitigation: configure theme per-chart at instance level using accent + theme.

## Open questions

1. **Default for new installs — `system` or `dark`?**
   - Recommendation: `system`. Most users have a preference set OS-wide; honoring it is the most respectful default.
2. **Should accent color affect destructive actions (e.g., delete buttons)?**
   - Recommendation: no. Destructive stays red regardless of accent. Accent affects primary affordances only.
3. **What if the user's system is light but they manually pick dark — should dark persist after system flips?**
   - Recommendation: yes. Manual selection wins; only `system` mode follows OS.
