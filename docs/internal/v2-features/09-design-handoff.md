# Feature 09 — Claude Design / UI Prototyping

## Status

`agreed` — workflow decided, no code consequence.

## Problem

The v2.0 external app needs a UI from scratch — chat panel, plans tab, rules tab, settings, autocomplete dropdowns, plan question cards, Editor pin visual. Designing UI from a blank canvas is expensive in solo-dev time, and homemade dev-art tends to look generic.

Ramon doesn't have a designer. Hiring one for v2.0 is overkill. But shipping a v2.0 with weak visual design undersells the engineering work.

## Proposal

Use Claude (specifically the visual / design-oriented mode — sometimes called "Claude Design" or generated via the Visualizer tool) to:

1. Generate **reference mockups** for each major screen of the external app
2. Use the mockups as visual targets — Ramon implements in React + CSS, matching the look but writing the real logic
3. Iterate quickly: when a screen needs revisiting, regenerate the mockup with refined prompts

**Important:** Claude-generated mockups are **references, not production code**. The output may have inline styles, mock data, fake interactions — the implementation is Ramon's actual React app, just visually aligned to the reference.

## Scope IN

- Mockups generated for the major v2.0 screens:
  - Chat main view (with delegation tree, ask_user cards, mode switcher, slash command autocomplete)
  - Plans tab (list view + open/edit view)
  - Rules tab (list view + open/edit view)
  - Settings panel
  - Editor pin (small element, fewer concerns)
  - First-run / connection-failed states
- Each mockup committed to `docs/internal/mockups/` as static HTML or screenshot, with the prompt that generated it (so it can be regenerated when needed)
- Implementation in `App~/src/` matches the mockup visually, but uses real state, real components, real data flow

## Scope OUT

- Using Claude-generated code as production code
- Complex animations / micro-interactions (those are dev-art territory; mockups can illustrate end states only)
- Asset generation (logos, icons) — different tooling

## Dependencies

- **Feature 01 (External app)** — implementation target. Mockups can ship before app code, useful as visual spec.

## Cost estimate

**Small** — mostly thinking time, not coding time.

- Defining the screens and use cases for mockups: ~2 days
- Iterating on mockup prompts to get strong references: ~3-5 days (iterative)
- Documenting mockups + their prompts: ~1 day

Total: ~1 week, can be done in parallel with other v2.0 work.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Mockup looks great but is impractical to implement | medium | Constrain prompts: "use Tailwind utility classes", "no custom animations", etc |
| Visual style is generic AI aesthetic | medium | Provide style references in prompts ("dark mode like Linear", "clean type like Notion") |
| Mockups become a procrastination trap (endless tweaking) | high | Time-box mockup phase. Cut over to implementation by week 2 max. |
| Implementation drifts from mockup over time | low | Acceptable — mockups are starting point, real product evolves |

## Milestone

v2.0 — pre-implementation phase.

## Open questions

1. **Style anchor** — what existing product visually matches the vibe Ramon wants? Linear? Notion? Cursor? Helps anchor prompts.
2. **Light + dark mode at start, or dark only for v2.0?** Light mode adds cost; dark only is a fine v2.0 default if Ramon prefers it (most Unity devs work in dark).
3. **Where to commit mockups?** Probably `docs/internal/mockups/<screen>.html` (static HTML) plus a `<screen>.prompt.md` with the prompt used. Reproducible.

## Notes

- This is not a feature in the product sense. It's a working method for designing the v2.0 UI without a human designer.
- Claude-generated UI is good for reference, weak for production. The discipline is using the output as a target, not a deliverable.
- Document the prompts used. Future regeneration / iteration depends on this.
