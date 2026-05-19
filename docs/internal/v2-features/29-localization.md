# Feature 29 — Localization (PT-BR primary, ES/i18n framework)

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

The entire app UI is in English. This excludes:

- **Brazilian developers** — substantial Unity community in Brazil; the founder/maintainer ships PT-BR-first. English UI feels foreign in projects where the whole team speaks Portuguese natively.
- **Spanish-speaking devs** — Latin America Unity community is large; English creates a friction tax on every interaction.
- **Other locales as they come** — French, German, Japanese, Chinese... each represents a community currently unable to onboard team members who don't read English fluently.

Beyond aesthetics: localization is a hard requirement for educational and accessibility contexts. Schools / training programs adopting Unity Editor tooling for non-English-native students need their tools to match the language of instruction.

## Proposal

Adopt `i18next` + `react-i18next` for runtime locale switching. Extract all UI strings to translation JSON files per locale. Ship with three baseline locales:

- `en` (English, current strings, source of truth)
- `pt-BR` (Portuguese — Brazilian)
- `es` (Spanish — Latin American baseline; specific country dialects can layer later)

Locale picker in Settings → Language. Auto-detect on first launch via OS locale (`navigator.language`); user overrides anytime. Stored in `preferences.json` per user.

Contribution model: translation files in `App~/src/i18n/locales/<lang>.json`. Community PRs add new locales. Each locale has a "completion %" auto-computed (missing keys fall back to English).

**Out of scope for v2.3:**
- Localized plan / rule content (those are user-authored; we don't translate)
- Localized C# server-side error messages (logged in English; user-facing errors via the chat are localized in v2.4 if requested)
- RTL languages (Arabic, Hebrew) — needs layout direction support; future work
- Date/number formatting beyond standard `Intl` (timezone display, currency, etc.)

## Scope IN

- `i18next` + `react-i18next` integration
- String extraction pass: every visible `t(...)` call replacing hardcoded strings
- Three baseline locales (`en`, `pt-BR`, `es`)
- Locale picker in Settings → Language
- Auto-detect on first launch (system locale)
- Persistence in `preferences.json`
- Pluralization rules per locale (`i18next` plural-keys mechanism)
- Date / number formatting via `Intl.DateTimeFormat` / `Intl.NumberFormat` with locale arg
- Completion % indicator per locale in the picker ("PT-BR: 100% / FR: 64%")
- Fallback chain: requested locale → English

## Scope OUT (deferred to v2.4+ or wontfix)

- RTL layout support
- Server-side error message localization
- User-authored content translation (plans, rules)
- Locale-specific tutorial / onboarding scripts (F32 will use the localized UI but tutorial copy itself stays English in v2.3)
- Crowdsourcing platform integration (Crowdin, Lokalise) — manual PR contribution suffices initially
- Region-specific dialects beyond first (e.g., pt-PT separate from pt-BR) — accept community PR if it happens

## Dependencies

None. Independent of other v2.3 features. Recommended after F28 (theming) only to avoid concurrent token-touching merge headaches.

## Risks

- **String length variance breaking layouts** — PT-BR strings often 20–40% longer than English equivalents. Mitigation: text-sizing tests; flexible layouts during F27 polish should accommodate.
- **Translation drift** — adding a new English string means it's missing in other locales. Mitigation: auto-show completion % in picker; English fallback guarantees no broken UI.
- **Compound strings** — e.g., `"<N> plans saved"` requires plural rules per locale. `i18next` handles this if the keys are structured right; spec phase verifies the convention.
- **Embedded strings in non-React contexts** — Rust commands logging messages, supervisor stderr — those stay English (server-side). Front-side error display localizes the user-facing text.

## Open questions

1. **Should the locale picker include in-progress locales (50%+ complete)?**
   - Recommendation: yes, but with a "partial translation" badge. Some translation is better than none for users.
2. **What about the Unity-side C# logs?**
   - Recommendation: stays English. C# logs are for developers; not user-facing.
3. **Locale of `Plugin~/skills/*.md` and `Plugin~/agents/*.md`?**
   - Recommendation: stays English. These are LLM-facing content; the LLM is multilingual; user doesn't typically read them. If demand exists, per-locale skill / agent overrides in v2.4+.
