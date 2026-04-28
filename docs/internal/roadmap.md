# MCP Game Deck — Roadmap

> Living document. Update as features ship or scope shifts.

## Current version

**v1.1.0** — published Apr 2026. See `package.json`.

## Strategy at a glance

The chat window inside Unity is being deprecated in v2.0. Doing tool quality work *before* the new home exists means refactoring against a target that's about to disappear — descriptions calibrated for the wrong UX, validation done in a buggy lifecycle environment, throwaway integration testing.

So the order is:

1. **v2.0 first** — build the new home (external Tauri app, orchestrator agent, plans/rules tabs)
2. **v2.1 — multi-LLM** — abstract the agent layer so OpenAI / Gemini / others can be plugged in alongside Claude
3. **v2.2.x** — tool consolidation runs in the new home, where each refactored tool is validated against its real production context

The 41 tool audits already exist (`.claude/reports/audits/`). They are not thrown away — they are the input to v2.2.x consolidation cycles. They may be re-run if code drifts significantly during v2.0 (a fresh batch audit is ~1-2h of compute, cheap to redo).

---

## Milestones

### v2.0 — External app + orchestrator architecture (PRIORITY, IN PROGRESS)

**Theme:** chat moves out of the Unity Editor entirely. Unity package becomes a thin runtime + connector. UX gets a proper home where lifecycle disruptions don't kill it.

**Goal:** make the tool genuinely usable for sustained work — no more losing the chat to assembly reloads, no more juggling chat instances to switch agents, no more bugs that come from being inside Unity's domain.

**Provider scope (locked April 2026):** v2.0 ships **Claude-only** via the official `@anthropic-ai/claude-agent-sdk`. Multi-provider support (OpenAI, Gemini, etc) is deliberately deferred to v2.1 — see that milestone for rationale.

**In scope** (each has its own doc in `v2-features/`):

| # | Feature | Doc | Status |
|---|---------|-----|--------|
| 1 | External app (Tauri + React) bundled in package as `App~/` | `01-external-app-spec.md` + `01-external-app-tasks.md` | ✅ done (Apr 2026) |
| 2 | Orchestrator agent — single chat, multiple subagents | `02-orchestrator-agent.md` | ⏳ pending |
| 3 | Slash commands customizable by user | `03-slash-commands.md` | ⏳ pending |
| 4 | Interactive plan mode — agent can ask user before finishing plan | `04-interactive-plan-mode.md` | ⏳ pending |
| 5 | Permission system fix (auto / plan / ask actually respected) | `05-permission-system-fix.md` | ⏳ pending |
| 6 | Plans CRUD with markdown storage in `ProjectSettings/GameDeck/plans/` | `06-plans-crud.md` | ⏳ pending |
| 7 | Editor pin status (replaces chat window inside Unity) | `07-editor-status-pin.md` | 🟡 in progress |
| 8 | Rules page (user-defined behavior constraints) | `08-rules-page.md` | ⏳ pending |
| 9 | Claude Design used to prototype UI | `09-design-handoff.md` | ⏳ pending |

**What v2.0 deletes from the current code:**

- Chat window UI Toolkit panel inside Unity Editor
- Multi-window agent switching code paths
- All the docking / layout fixes for the chat window
- Any session lifecycle code that tries to survive assembly reload

**What v2.0 keeps:**

- C# MCP Server (`TcpListener`, `ReuseAddress`) — unchanged
- 268 tools as they are today — **no consolidation yet**, that's v2.2.x
- The MCP-standard interface of the C# server stays open: any MCP-compatible client (Claude Desktop, Cursor, Cline, etc) can keep connecting directly. The Tauri app is just one client among many.
- TypeScript MCP Proxy in `Server~/` — possibly modified to also serve the external app
- Curated knowledge layer in `Editor/Tools/UnityDocs/` and `Editor/Tools/UIToolkit/` — unchanged

**Out of scope (deferred to v2.1+):**

- Multi-LLM agent abstraction (v2.1)
- Tool consolidation work (v2.2.x)
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

### v2.1 — Multi-LLM provider abstraction

**Theme:** open the agent layer beyond Claude. Same UX, different model behind the scenes.

**Why now (and not in v2.0):**

- v2.0 ships Claude-only because the `@anthropic-ai/claude-agent-sdk` already implements the turn loop, tool-use management, and streaming. Reusing it shaves weeks off v2.0.
- Building a provider-agnostic abstraction *first* is speculative work — without v2.0 in production, we don't yet know which LLM behaviors actually matter for the Unity workflow. Better to validate the product, then abstract from real signal.
- Refactor cost from "Claude-only" → "abstracted N-providers" is well-known territory (multiple OSS projects have done it: Continue, aider, Cline). Estimated ~1-2 weeks of focused work after v2.0 lands.

**Likely scope:**

- Internal `ChatProvider` interface in the Node SDK Server with one method per capability (send turn, stream response, register tools, handle tool calls, abort, etc).
- Concrete adapters: `ClaudeProvider` (wraps the existing Agent SDK code), `OpenAIProvider` (`openai` npm package), `GeminiProvider` (`@google/generative-ai`).
- Per-provider model dropdown in Settings (auto-populated from the provider's available models).
- Per-provider API key management in Settings (separate fields, separate validation).
- Tool-call protocol normalization — Claude's tool blocks vs OpenAI's function-calling vs Gemini's function calls all map to a single internal shape.
- System-prompt handling normalization — providers vary in how they accept system prompts.
- Subagent definitions in `Agents~/` get an optional `provider` field (default: same as main agent's provider).

**Risks:**

- Tool-use semantics differ enough between providers that a "lowest common denominator" loses functionality.
- Streaming protocols differ; abstracting without losing token-level latency is non-trivial.
- Mitigation: launch v2.1 supporting all 3 providers but explicitly mark anything provider-specific that doesn't translate (e.g. "extended thinking only on Claude").

**Not committed:**

- Local model providers (Ollama, llama.cpp) — interesting but adds a different class of complexity (model lifecycle, hardware checks). Could come in v2.x as a separate effort.
- Anthropic/OpenAI/Google enterprise endpoints (Bedrock, Azure, Vertex) — worth doing eventually but not blocking the headline v2.1 scope.

**Success criteria:**

- A user with only an OpenAI API key can install MCP Game Deck, configure their key, and chat with GPT-4 against the Unity MCP tools.
- Same for Gemini.
- Existing Claude users see no behavior change — the Claude path stays the default and the canonical reference.
- All Feature 01-09 UX (orchestrator delegation, slash commands, plans, rules) works identically across providers.

---

### v2.2.x — Tool consolidation in the new home

**Theme:** with v2.0 shipped (and v2.1's multi-LLM as a nice-to-have but not blocking), the 268 tools get refactored against their real production context. Each consolidation cycle ships as its own patch (`v2.2.1`, `v2.2.2`, ...).

**Why this milestone (and not v1.2):** consolidating tools requires:
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
- Remaining ~30 domains have audits committed; consolidation continues opportunistically into v2.2.x and beyond

---

### v2.3+ — Personalization + power features

**Theme:** features that make power users productive, after the core product (v2.0), the multi-LLM expansion (v2.1), and tool quality (v2.2.x) are stable.

**Likely scope:**

- Plans: templates, sharing, versioning
- Rules page: conditional rules, per-domain scoping, rule libraries
- Onboarding flow for new installs
- Theming (light/dark, custom)
- Localization (PT-BR first)
- Possible analytics dashboard (token usage trends, tool call patterns)
- Local model providers (Ollama, llama.cpp)
- Tail of tool consolidations not covered by v2.2.x

**Not committed yet** — exact scope decided after v2.0/2.1/2.2 ship and real usage signals appear.

---

## Skipped: v1.2

There is no v1.2 release planned.

Originally scoped as "tool quality fixes shipped before v2.0", reconsidered in April 2026: refactoring tools against a soon-to-be-deprecated UX is throwaway work. Tool consolidation moves to v2.2.x where it can be done in the production context.

**Work already invested in v1.2 planning that carries forward:**

- 41 tool audits in `.claude/reports/audits/` — input to v2.2.x cycles
- Animation review draft and GameObject review (with escalations answered) — pick up directly when those domains' v2.2.x cycles start
- Pipeline agents (`tool-auditor`, `auto-reviewer`, `consolidation-planner`, `tool-consolidator`, `build-validator`, `audit-batch-runner`) all built and tested
- Sentinel convention decision (string `"true" | "false" | ""`) made
- CLAUDE.md C# standards documented

None of this is wasted — it accelerates v2.2.x when tool work resumes.

---

## How features move between milestones

Sometimes a v2.1 feature gets pulled into v2.0 because it turns out simpler than expected. Sometimes a v2.0 feature gets pushed because it's harder. When that happens:

1. Update this doc — move the row
2. If the change is meaningful, add an ADR in `decisions/`
3. Update the feature doc's `Milestone` field

Don't let the roadmap silently drift.

## What's shipping next

**Just shipped:** Feature 01 (External Tauri app) — merged to `develop/v2.0` April 2026. End-to-end echo round-trip working, MSI 2.93 MB.

**Immediate:** Feature 07 (Editor pin) on branch `feature/07-editor-status-pin`. The pin replaces the in-Unity ChatWindow with a small toolbar widget that launches the Tauri app and resolves the two production caveats from Feature 01 (Node SDK path resolution and `UNITY_PROJECT_PATH` env var injection).

**Next after 07:** Feature 02 (Orchestrator agent) — replaces the echo stub in Node SDK with real Claude Agent SDK conversations and subagent delegation. After 02, Features 03-06, 08, 09 in roadmap order.

**Tool consolidation:** paused until v2.0 ships. The Animation review draft and GameObject review (with escalations) sit in `.claude/reports/reviews/` waiting for v2.2.1 to pick them up.
