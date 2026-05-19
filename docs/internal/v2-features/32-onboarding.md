# Feature 32 — Onboarding Flow

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

A first-time user installs MCP Game Deck and opens it. The current experience: black chat screen, a composer, and pills indicating Unity / Supervisor connection status. No tour. No "try this example". No explanation of what Plans / Rules / Settings do. No detection that the user might not have a Unity project open yet.

Power users figure it out (they're motivated). Casual evaluators bounce — "I don't know what to do with this, the chat just sits there". Studio leads evaluating for team adoption can't onboard a junior developer without manual hand-holding.

The lack of onboarding is the single biggest gate to broader v2.3+ adoption beyond the maintainer's immediate dogfooding circle.

## Proposal

A multi-step onboarding flow shown on first launch, with skip-everywhere affordances for power users. Five steps:

**Step 1 — Welcome screen.** "Welcome to MCP Game Deck. This tool connects Claude (or another AI) to your Unity Editor so you can ask questions about your project, automate tedious tasks, and execute plans you've authored." Skip / Next.

**Step 2 — Unity connection wizard.** Detect whether a Unity project is open. If yes, confirm "Detected: <ProjectName> — connect?". If no, show a guide: "Open your Unity project, then click Refresh below". Continues only when connection is established.

**Step 3 — Quick tour.** Highlight tour overlay on four key features (4 mini-screens):
1. The chat composer + tool calling
2. The Plans page ("authored sequences of steps you can re-run")
3. The Rules page ("project conventions Claude follows")
4. Settings → Connection (where to manage Unity port, provider, etc.)

Each step has a "Try it" link that opens that route. Skip / Next.

**Step 4 — Sample plan.** Offer to create a sample plan ("Inspect the active scene's GameObjects and report findings"). Optionally execute it on the user's project as a guided demo.

**Step 5 — Done.** "You're set. Need help? Docs at <link>." Onboarding mark stored in `preferences.json::onboarding_completed: true`. Settings → Help has a "Re-run onboarding" affordance.

## Scope IN

- Multi-step onboarding component (`App~/src/components/onboarding/`)
- Five steps above
- Skip affordance on every step
- Persistence: `onboarding_completed` flag in `preferences.json`
- Re-run onboarding from Settings → Help
- Unity connection wizard with detect / guide / refresh
- Sample plan creation (from F30 templates if shipped; otherwise inline-defined sample)
- Tour overlay using a lightweight library (e.g., `react-joyride` or hand-rolled)
- Localized via F29 (i18n) when available

## Scope OUT (deferred to v2.4+ or wontfix)

- **Onboarding A/B testing infrastructure** — too small a userbase to A/B; intuition-driven design
- **Telemetry on onboarding completion** — F33 (analytics dashboard) is opt-in; onboarding stats stay local until that's stable
- **Interactive code-along tutorials** — guided "write your first rule" tutorial — F32 follow-up if onboarding-completion data shows it's needed
- **Video tutorials embedded in onboarding** — link out to YouTube playlist instead
- **Per-feature first-use tooltips** — single onboarding gate; no recurring per-feature tooltips
- **Re-run on major version updates** — opt-in only via Settings; updates don't surprise the user

## Dependencies

- **F25 (Per-project window isolation)** — recommended. Onboarding interacts with project-detection logic; if F25 is shipped, the wizard step works in multi-project context. If not, single-project simplification.
- **F29 (Localization)** — recommended. Onboarding text is high-visibility; bad English / pt-BR / es translations hurt impressions. Ship onboarding *with* localization, not before.

## Risks

- **Onboarding-fatigue** — over-engineering onboarding makes power users grumpy. Mitigation: skip everywhere, defaults are biased toward "the user knows what they're doing" — no enforced steps.
- **Sample plan side effects** — even a "harmless" inspection plan could break user expectations if it analyzes the wrong scene. Mitigation: sample plan is opt-in; user clicks "Try it" explicitly.
- **Onboarding state drift** — what if a user completes onboarding, then deletes `preferences.json`? Onboarding re-runs. Acceptable; no surprises.
- **Tour overlay z-index hell** — overlay components fighting with chat panels, dropdowns. Mitigation: dedicated overlay layer via Tauri webview's portal pattern.

## Open questions

1. **Should Step 4 (sample plan) be a forced step or fully skippable?**
   - Recommendation: skippable. Some users have a project they care about and don't want a sample plan littering their plans folder.
2. **Tour overlay library — build or buy?**
   - Recommendation: hand-rolled. `react-joyride` is heavy and customization-resistant; a 200-line component matches our needs and design system.
3. **Should the onboarding be re-shown on major releases (e.g., v3.0)?**
   - Recommendation: no, manual re-run only. Surprising re-shows feel paternalistic. New features announced via banner / release notes instead.
