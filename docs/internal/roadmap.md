# MCP Game Deck — Roadmap

> Living document. Update as features ship or scope shifts.

## Current version

**v1.1.0** — published Apr 2026. See `package.json`.

## Strategy at a glance

The chat window inside Unity is being deprecated in v2.0. Doing tool quality work *before* the new home exists means refactoring against a target that's about to disappear — descriptions calibrated for the wrong UX, validation done in a buggy lifecycle environment, throwaway integration testing.

So the order is:

1. **v2.0 first** — build the new home (external Tauri app, orchestrator agent, plans/rules tabs)
2. **v2.1.x** — tool consolidation runs in the new home, where each refactored tool is validated against its real production context

The 41 tool audits already exist (`.claude/reports/audits/`). They are not thrown away — they are the input to v2.1.x consolidation cycles. They may be re-run if code drifts significantly during v2.0 (a fresh batch audit is ~1-2h of compute, cheap to redo).

---

## Milestones

### v2.0 — External app + orchestrator architecture (PRIORITY)

**Theme:** chat moves out of the Unity Editor entirely. Unity package becomes a thin runtime + connector. UX gets a proper home where lifecycle disruptions don't kill it.

**Goal:** make the tool genuinely usable for sustained work — no more losing the chat to assembly reloads, no more juggling chat instances to switch agents, no more bugs that come from being inside Unity's domain.

**In scope** (each has its own doc in `v2-features/`):

| # | Feature | Doc |
|---|---------|-----|
| 1 | External app (Tauri + React) bundled in package as `App~/` | `01-external-app.md` |
| 2 | Orchestrator agent — single chat, multiple subagents | `02-orchestrator-agent.md` |
| 3 | Slash commands customizable by user | `03-slash-commands.md` |
| 4 | Interactive plan mode — agent can ask user before finishing plan | `04-interactive-plan-mode.md` |
| 5 | Permission system fix (auto / plan / ask actually respected) | `05-permission-system-fix.md` |
| 6 | Plans CRUD with markdown storage in `ProjectSettings/GameDeck/plans/` | `06-plans-crud.md` |
| 7 | Editor pin status (replaces chat window inside Unity) | `07-editor-status-pin.md` |
| 8 | Rules page (user-defined behavior constraints) | `08-rules-page.md` |
| 9 | Claude Design used to prototype UI | `09-design-handoff.md` |

**What v2.0 deletes from the current code:**

- Chat window UI Toolkit panel inside Unity Editor
- Multi-window agent switching code paths
- All the docking / layout fixes for the chat window
- Any session lifecycle code that tries to survive assembly reload

**What v2.0 keeps:**

- C# MCP Server (`TcpListener`, `ReuseAddress`) — unchanged
- 268 tools as they are today — **no consolidation yet**, that's v2.1.x
- TypeScript MCP Proxy in `Server~/` — possibly modified to also serve the external app
- Curated knowledge layer in `Editor/Tools/UnityDocs/` and `Editor/Tools/UIToolkit/` — unchanged

**Out of scope (deferred to v2.1+):**

- Tool consolidation work — runs in the new home, not before it exists
- Plans CRUD beyond basic save/list/open
- Rules page advanced features
- Onboarding / first-run experience polish
- Theming
- Localization

**Success criteria:**

- External app launches from Unity pin, survives Unity restart, reconnects cleanly
- Single chat handles agent delegation transparently
- Plans visible and editable from a dedicated tab
- All UX bugs from v1.1.0 resolved as side effects of the new architecture (assembly reload, agent switching, permission modes, docking)

---

### v2.1.x — Tool consolidation in the new home

**Theme:** with v2.0 shipped, the 268 tools get refactored against their real production context. Each consolidation cycle ships as its own patch (`v2.1.1`, `v2.1.2`, ...).

**Why now (and not v1.2):** consolidating tools requires:
- Calibrating descriptions for the LLM's real usage pattern (better seen with v2.0's orchestrator delegation)
- Validating macro tools against real workflows (clearer in v2.0's plans tab)
- Avoiding throwaway work on tooling whose UX is being deprecated

**Pipeline (already built in `.claude/agents/`):**

```
audit (cached from April 2026)
  → review (auto-reviewer + Ramon's escalations)
    → plan (consolidation-planner)
      → consolidate (tool-consolidator)
        → validate (build-validator)
          → Ramon commits via VS Code
```

**Audit freshness check first.** Before each consolidation cycle, verify the existing audit (in `.claude/reports/audits/`) still matches the current code. If significant drift since April 2026, re-run `tool-auditor` for that domain. Re-running the full batch is cheap (~1-2h) if drift is widespread.

**Suggested order** (per the original batch summary's priority ranking):

1. GameObject (already has review escalations answered as of April 2026 — pick up here)
2. Prefab + AddAssetToScene (cross-cutting decision: merge or fold)
3. Asset
4. Script
5. Component
6. Editor
7. Scene
8. Selection
9. Build + PlayerSettings (cross-cutting decision: merge or keep separate)
10. ...remaining 30+ domains opportunistically

**Cross-cutting decisions surfaced by the April 2026 batch** (still pending Ramon's call when each domain's review starts):

- PlayerSettings ↔ Build merge?
- AddAssetToScene → fold into Prefab?
- Object ↔ ScriptableObject ↔ Component generic-modifier triangle
- Reflect ↔ Type merge?
- 2D support strategic question (sprite GameObject creation, 2D physics, URP Light2D, sprite slicing)
- `EditorUtility.InstanceIDToObject` deprecation sweep (cross-domain helper fix)

**Sentinel convention** (already decided in GameObject review draft, April 2026): nullable string `"true" | "false" | ""` for "leave unchanged" booleans. Apply across all consolidations.

**Success criteria:**

- 8-10 highest-priority domains consolidated and shipped (Tier 1 from the April 2026 batch summary)
- Prompt caching enabled on consolidated tool definitions (significant token cost reduction)
- Each shipped patch has a corresponding plan + validation in `.claude/reports/`
- Remaining ~30 domains have audits committed; consolidation continues opportunistically into v2.1.x and v2.2

---

### v2.2+ — Personalization + power features

**Theme:** features that make power users productive, after the core product (v2.0) and tool quality work (v2.1.x) are stable.

**Likely scope:**

- Plans: templates, sharing, versioning
- Rules page: conditional rules, per-domain scoping, rule libraries
- Onboarding flow for new installs
- Theming (light/dark, custom)
- Localization (PT-BR first)
- Possible analytics dashboard (token usage trends, tool call patterns)
- Tail of tool consolidations not covered by v2.1.x

**Not committed yet** — exact scope decided after v2.0 ships and real usage signals appear.

---

## Skipped: v1.2

There is no v1.2 release planned.

Originally scoped as "tool quality fixes shipped before v2.0", reconsidered in April 2026: refactoring tools against a soon-to-be-deprecated UX is throwaway work. Tool consolidation moves to v2.1.x where it can be done in the production context.

**Work already invested in v1.2 planning that carries forward:**

- 41 tool audits in `.claude/reports/audits/` — input to v2.1.x cycles
- Animation review draft and GameObject review (with escalations answered) — pick up directly when those domains' v2.1.x cycles start
- Pipeline agents (`tool-auditor`, `auto-reviewer`, `consolidation-planner`, `tool-consolidator`, `build-validator`, `audit-batch-runner`) all built and tested
- Sentinel convention decision (string `"true" | "false" | ""`) made
- CLAUDE.md C# standards documented

None of this is wasted — it accelerates v2.1.x when tool work resumes.

---

## How features move between milestones

Sometimes a v2.1 feature gets pulled into v2.0 because it turns out simpler than expected. Sometimes a v2.0 feature gets pushed because it's harder. When that happens:

1. Update this doc — move the row
2. If the change is meaningful, add an ADR in `decisions/`
3. Update the feature doc's `Milestone` field

Don't let the roadmap silently drift.

## What's shipping next

**Immediate:** v2.0 work starts. Begin with Feature 07 (Editor pin) and Feature 01 (external app scaffolding) since they unblock the rest.

**Tool consolidation:** paused until v2.0 ships. The Animation review draft and GameObject review (with escalations) sit in `.claude/reports/reviews/` waiting for v2.1.1 to pick them up.
