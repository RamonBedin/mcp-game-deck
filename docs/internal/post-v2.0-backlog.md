# Post-v2.0 backlog

> Living document. Aggregates everything mapped during v2.0 (Features 01 → 09) as
> deferred to v2.1+, organised by area. Source-of-truth links into the original
> feature docs are kept so context isn't lost.
>
> Use this together with `roadmap.md` — the roadmap defines the milestones
> (v2.1 / v2.2.x / v2.3+); this doc enumerates the concrete items inside each.
>
> **Last update:** 2026-05-18 — initial compilation after F06/F08/F09 ship and
> all v2.0 backend asks (B.02, B.06, B.08, B.10) land.

---

## How to read this

Each row has:

- **Item** — short title
- **Effort** — S (≤ 1 day) / M (2–5 days) / L (1+ week)
- **Milestone hint** — proposed v2.1 / v2.2 / v2.3 / v3+ slot
- **Source** — feature doc + section where the deferral was logged
- **Notes** — dependency hints, unblock conditions

Effort estimates assume the v2.0 codebase as it stands today; if a dependency
slips (e.g. a backend ask doesn't ship), the dependent UI work slides with it.

---

## v2.1 — Multi-LLM + dogfood polish

**Theme:** open the agent layer beyond Claude (the headline of v2.1), but ship
alongside the high-signal polish items that surfaced during v2.0 dogfood.

### Multi-LLM provider abstraction (headline)

| Item | Effort | Source | Notes |
|---|---|---|---|
| `ChatProvider` interface in Node SDK Server | M | `roadmap.md::v2.1` | One method per capability — send turn, stream, register tools, handle calls, abort |
| `OpenAIProvider` adapter (`openai` npm) | M | `roadmap.md::v2.1` | Function-calling normalised to internal tool-block shape |
| `GeminiProvider` adapter (`@google/generative-ai`) | M | `roadmap.md::v2.1` | Same normalisation pass |
| Per-provider model dropdown in Settings → Claude Code panel | S | `roadmap.md::v2.1` | Auto-populated from provider's listed models |
| Per-provider API key fields in Settings | S | `roadmap.md::v2.1` | Separate validation per provider |
| Tool-call protocol normalisation | M | `roadmap.md::v2.1` | Claude blocks ↔ OpenAI functions ↔ Gemini functions |
| System-prompt handling normalisation | S | `roadmap.md::v2.1` | Each provider accepts system prompt differently |
| `provider:` frontmatter field on subagent defs | S | `roadmap.md::v2.1` | Defaults to main agent's provider |
| **Subagent `tools:` frontmatter audit** | M | `roadmap.md::v2.1` + `02-claude-code-supervisor-tasks.md:3.4` | Each of the 10 specialists declares MCP tools in prose body but only built-ins in YAML — bug: a specialist invoked via Task can't actually call MCP tools the prompt promises. Plugin-side polish, can be advanced into v2.0.x if desired. |

### Process / lifecycle hardening (F02 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Job Object on Windows for process-tree management | M | `02-supervisor-tasks.md:6.3` | More robust than `taskkill /T /F` — survives Tauri being killed via Task Manager. Requires `windows` crate + `unsafe` blocks. |
| Orphan cleanup when Tauri is killed externally | M | `02-supervisor-tasks.md:6.3` | Covered by the Job Object above |
| Unix process-group based shutdown (`process_group(0)` + `kill -pgid`) | S | `02-supervisor-tasks.md:6.3` | Today Unix uses `kill_on_drop` only. Wait for macOS/Linux user demand. |

### Permission system (F04 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Cross-session "Allow Always" persistence | S | `04-interactive-approvals.md::Scope OUT` | Write `Map<toolName, inputShape>` to `ProjectSettings/GameDeck/permissions.json`; load on supervisor spawn |
| Per-tool / per-scope rule library | M | `04-interactive-approvals.md::Scope OUT` | "always allow Read in subdir X", "never allow Bash" beyond one session |
| Hooks-based `PreToolUse` / `PostToolUse` blanket allow/deny | M | `04-interactive-approvals.md::Scope OUT` | F04 only wires `canUseTool`; the SDK exposes hook lifecycle but v2.0 doesn't surface it |
| "Allow Always" granularity loosening — `(toolName, input shape)` instead of `(toolName, exact input)` | S | `04-interactive-approvals.md::Open questions` | Loosen if v2.0 users complain |
| "Deny & Stop" button (`interrupt: true` in callback) | S | `04-interactive-approvals-spec.md:235` | Today we only expose Deny-and-continue |

### Plans + autocomplete (F06 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| `last-run` timestamp tracking on plans | S | `06-plans-crud.md` | Requires `/plan-execute` skill to Write back mid-conversation |
| Plan templates with `<placeholder>` syntax | M | `06-plans-crud.md` | Parametrized plans |
| Plans tab rename flow | S | `06-plans-crud.md` | Uses the same auto-suffix logic as v2.0's collision handling |
| Mtime-based conflict detection (chat skill vs UI editor) | S | `06-plans-crud.md` | Today the watcher reloads, losing chat-side writes silently in some cases |
| MRU / pinned / favourites in slash dropdown | S | `06-plans-crud.md` | Initial sort is alphabetical in v2.0 |
| Recently-used files at top of `@` picker | S | `06-plans-crud.md` | Same MRU treatment for files |
| File-content preview on hover in `@` picker | S | `06-plans-crud.md` | Tooltip with first N lines |
| Fuzzy search inside dropdowns | S | `06-plans-crud.md` | v2.0 uses substring/prefix |
| User-configurable `@` picker exclusions | S | `06-plans-crud.md` | Today the exclusion list is hard-coded (`Library/`, `Temp/`, etc) |
| Lazy / streaming file index for huge projects | M | `06-plans-crud.md` | Only matters if reports of >50k file projects surface |
| "+ New Session" mid-flight race fix | S | `06-plans-crud-tasks.md:7.1` | Race window when user clicks +New while A is still streaming, then sends B |

### Editor pin (F07 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Per-project Tauri window isolation | L | `07-editor-status-pin-spec.md::Known limitation` + `-tasks.md:5.1` | `tauri-plugin-single-instance` v2 reads lock id from `app.config().identifier` at build time, no runtime injection. Requires forking the plugin or rolling custom named-pipe / Unix-socket lock. |
| Notification badges (e.g. "3 messages" while user was in Unity) | M | `07-editor-status-pin.md` | Needs a message-count event from supervisor to pin polling |

### Rules page (F08 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Functional `applies-to` per-subagent filtering | M | `08-rules-page.md::Scope OUT` | Claude Code spawns subagents with fixed toolsets, no clean hook to inject per-agent system prompts. Field is parsed + round-tripped today — v2.1 only needs the compiler change. |
| Conditional rules (only apply if X) | M | `08-rules-page.md::Scope OUT` | Query-language territory; do not over-engineer |
| Rule conflict detection | M | `08-rules-page.md::Risks` | Surface "rule A contradicts rule B" warnings |
| Auto-suggestion ("save as rule?") | M | `08-rules-page.md::Scope OUT` + `08-rules-page.md:81` | Heuristic on recent normative instructions; too fuzzy in v2.0 |
| `/save-rule` chat skill | S | `08-rules-page.md::Scope OUT` | F06's `/save-plan` had `ExitPlanMode` as clean marker; rules don't |
| Multi-line `applies-to` editing UI | S | `08-rules-page.md::Scope OUT` | Hand-edit in frontmatter for v2.0 |
| "Stale rule" prompts (rule drift from project reality) | S | `08-rules-page.md::Risks` | Use `last_modified` already visible per row |
| Real tokenizer (tiktoken) instead of `chars/4` | S | `08-rules-page.md::Scope OUT` | Only matters if cost signal becomes wrong at v2.0 scale |

### Design system + UX polish (F09 deferrals)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Custom keyboard shortcuts beyond ⇧⏎ / ⌫ / esc | M | `09-design-handoff.md::Scope OUT` | F3 in Library search, Cmd+K palette, etc. |
| ToolCallGroup wiring — 3+ consecutive same-family calls collapse | S | `09-design-handoff.md` + `App~/src/components/chat/ToolCallGroup.tsx` (orphan today) | Component already exists, needs grouping logic in `ChatRoute.pairToolBlocks` |
| Re-run dogfood on "Allow Always" UX (two-button vs menu) | trivial | `09-design-handoff.md::Open questions` | Decision after F04 sees real usage |
| HUD project switcher dropdown | M | `09-design-handoff.md::Open questions` + F07 dep | Designed with `▾` chevron; depends on F07 per-project isolation |
| Path-quoting bug in agent-generated bash with spaces | S | `02-supervisor-tasks.md:3.4` | Agent generates `cd Assets\TextMesh Pro\…` without quoting; permission system rejects |

---

## v2.0.x — Patch backlog (post-ship, pre-v2.1)

**Theme:** the backend asks the F09 design handoff itemised that didn't fit
into the v2.0 chat-centric pass. Each is small enough to ship as a patch
release.

### Remaining F09 backend asks (B.*)

| ID | Item | Effort | Status today | Unlocks |
|---|---|---|---|---|
| **B.01** | Supervisor activity stream — events `turn-started`, `tool-call-queued`, `subagent-started`, `subagent-finished` | M | not started | Real `WorkingStrip` activity text + sub-agent indicator |
| **B.03** | Tool metadata catalog — `{toolName, humanLabel, category, riskTier, exampleInputs}` exposed from C# server | M | not started | Narrative line in `ToolCallNarrativeBlock`, sample query in `AgentCard` |
| **B.04** | Per-tool risk tier (subset of B.03) | S | front-side heuristic in `toolTier.ts` covers conservative cases | Tier accent on `PermissionRequestCard` is authoritative |
| **B.05** | Plan execution events — `plan-step-started`, `plan-step-completed` emitted by `/plan-execute` skill | M | not started | Wires `PlanExecutionPanel` + `StepRow` (already-built orphans) into `PlansRoute` |
| **B.07** | Chat history mining — "you've done X 8x; create a rule?" | L | not started | Rules suggestions feed |
| **B.09** | Connection-aware queue — when Unity busy/disconnected, app queues calls and replays on reconnect | L | not started | Unity-offline UX (toast + HUD queue counter) |

**Status of the already-shipped asks** (no action needed, just for reference):

- **B.02** ✓ — `cancel_current_turn()` Rust command + sdk-entry.js handler shipped
- **B.06** ✓ — `preview_rules_bundle()` reads `Library/MCPGameDeck/rules-bundle.md`
- **B.08** ✓ — recent commands cache reads/writes `ProjectSettings/GameDeck/recent-commands.json`
- **B.10** ✓ — `list_knowledge_docs()` + `read_knowledge_doc()` + `read_all_knowledge_docs()` (full-text search)
- **B.11** ✓ — design tokens layer (`tokens.css`, `tailwind.config.patch.js`)

### Auto-update (F01 deferral)

| Item | Effort | Source | Notes |
|---|---|---|---|
| Tauri auto-update flow | M | `01-external-app-spec.md::Scope OUT` | v2.0 has the `UpdateBanner` reading env vars set by the pin; actual self-update is deferred to v2.x |

### Connection panel wire-ups (F09 follow-ups)

The Connection panel in Settings has a few read-only rows today; the mockup
showed editable controls that aren't wired to anything:

| Item | Effort | Notes |
|---|---|---|
| MCP port input — actually applies to C# server | M | Today the port is hardcoded 8090 on C# side. Editing requires a C# server change + restart |
| Request timeout input | S | No backend for timeout config exists; the input is mockup-only |
| Auth token row (read-only) | S | Token rotation lives on the C# side; could surface `Library/GameDeck/auth-token` |

---

## v2.2.x — Tool consolidation

**Theme:** the 268 tools across ~38 domains get refactored against the v2.0
production context. Pipeline already built in `.claude/agents/`.

| Item | Effort | Source | Notes |
|---|---|---|---|
| Audit freshness check (re-run `tool-auditor` for drifted domains) | S each | `roadmap.md::v2.2.x` | 41 audits cached April 2026 |
| GameObject consolidation cycle | M | `roadmap.md::v2.2.x` + `.claude/reports/reviews/` | Review with escalations already answered (April 2026) — pick up here |
| Prefab + AddAssetToScene cycle (cross-cutting decision: merge vs fold) | M | `roadmap.md::v2.2.x::Cross-cutting decisions` | |
| Asset consolidation cycle | M | `roadmap.md::v2.2.x` | |
| Asset — batch CRUD ops (G5 deferral) | M | `.claude/reports/reviews/review-Asset-20260519.md` (E6) | Batch move/delete/copy/rename for Asset domain. Pre-req: verify `BatchExecute` infrastructure handles AssetDatabase ops on main thread; if confirmed, route through `BatchExecute` rather than adding asset-specific batch tools. Decision deferred from Asset E6 on 2026-05-19. |
| Asset — sprite slicing verification (G7 deferral) | S | `.claude/reports/reviews/review-Asset-20260519.md` (E7) | Texture-domain audit must verify whether sprite-sheet slicing (`SpriteMetaData[]` construction on `TextureImporter`) is covered by `Tool_Texture.ApplyPattern.cs`. If yes, G7 closed-elsewhere; if no, Texture audit picks it up as its own finding. |
| Script consolidation cycle | M | `roadmap.md::v2.2.x` | |
| Component consolidation cycle | M | `roadmap.md::v2.2.x` | Object ↔ ScriptableObject ↔ Component generic-modifier triangle |
| Editor consolidation cycle | M | `roadmap.md::v2.2.x` | |
| Scene consolidation cycle | M | `roadmap.md::v2.2.x` | |
| Selection consolidation cycle | M | `roadmap.md::v2.2.x` | |
| Build + PlayerSettings cycle (cross-cutting: merge or keep separate) | M | `roadmap.md::v2.2.x::Cross-cutting decisions` | |
| Reflect ↔ Type merge decision | S | `roadmap.md::v2.2.x::Cross-cutting decisions` | |
| 2D support strategic question (sprite GameObject, 2D physics, URP Light2D, sprite slicing) | M | `roadmap.md::v2.2.x::Cross-cutting decisions` | |
| `EditorUtility.InstanceIDToObject` deprecation sweep | S | `roadmap.md::v2.2.x::Cross-cutting decisions` | Cross-domain helper fix |
| Tail of ~30 remaining domains | L | `roadmap.md::v2.2.x` | Opportunistic into v2.2.x and beyond |
| Prompt caching on consolidated tool definitions | S | `roadmap.md::v2.2.x::Success criteria` | Significant token cost reduction |

**Sentinel convention** (already decided April 2026 in GameObject review draft):
nullable string `"true" | "false" | ""` for "leave unchanged" booleans. Apply
across all consolidations.

---

## v2.3+ — Personalisation + power features

**Theme:** features that make power users productive, after the core product,
multi-LLM, and tool quality are stable.

| Item | Effort | Source |
|---|---|---|
| Light mode theme | M | `09-design-handoff.md::Scope OUT` — `tokens.css` already namespaces the dark palette; a `:root.theme-light` overlay is the one-screen change |
| Localization (PT-BR first) | M | `09-design-handoff.md::Scope OUT` |
| Plans templates | M | `roadmap.md::v2.3+` |
| Plans sharing | M | `roadmap.md::v2.3+` |
| Plans versioning beyond git | M | `06-plans-crud.md` |
| Cross-project plan library | M | `06-plans-crud.md` |
| Plan dry-run mode | M | `06-plans-crud.md` |
| Plan branching / conditionals | M | `06-plans-crud.md` |
| Rule libraries / cross-project sharing | M | `08-rules-page.md::Scope OUT` |
| Rule versioning beyond git | M | `08-rules-page.md::Scope OUT` |
| Onboarding flow for new installs | M | `roadmap.md::v2.3+` |
| Custom theming (beyond light/dark switch) | M | `roadmap.md::v2.3+` |
| Analytics dashboard (token usage trends, tool call patterns) | M | `roadmap.md::v2.3+` |
| Local model providers (Ollama, llama.cpp) | L | `roadmap.md::v2.3+` |
| Anthropic/OpenAI/Google enterprise endpoints (Bedrock, Azure, Vertex) | M | `roadmap.md::v2.1::Not committed` — moved to v2.3+ unless demand surfaces |

---

## Maintenance backlog (any release)

Small items without a strong milestone tie. Pick up opportunistically:

| Item | Effort | Source | Notes |
|---|---|---|---|
| Wire `Library/GameDeck/auth-token` into Settings → Connection | S | mockup `03 - Mockups.html` | Read-only row showing token path |
| `ToolCallGroup` grouping logic | S | `App~/src/components/chat/ToolCallGroup.tsx` | Component is an orphan today; depends on B.03 for tool family names |
| Subagent `tools:` audit (10 specialists) | S | already noted in v2.1 — could ship early as plugin polish | Each specialist's prose claims MCP access but YAML only lists built-ins |
| Path-quoting fix for agent bash with spaces | S | `02-supervisor-tasks.md:3.4` | One-line fix in agent prompt or bash wrapper |

---

## Cross-references

- **Source-of-truth roadmap:** `roadmap.md` — milestones, themes, success criteria
- **Feature docs:** `v2-features/01-*.md` … `v2-features/09-*.md` — each has its
  own `Scope OUT` section and `Open questions` block
- **Architecture decisions:** `architecture/ADR-001-claude-code-sdk-as-engine.md`
- **Tool audits (input to v2.2.x):** `.claude/reports/audits/` — 41 domains
  audited April 2026
- **Tool consolidation pipeline:** `.claude/agents/` — `tool-auditor`,
  `auto-reviewer`, `consolidation-planner`, `tool-consolidator`, `build-validator`,
  `audit-batch-runner`

---

## Update protocol

When something moves between buckets (e.g. a v2.1 item gets pulled forward,
or a v2.0.x item slips to v2.1):

1. Update the row in this file
2. Update `roadmap.md` if the change affects milestone scope
3. Update the source feature doc's `Scope OUT` / `Open questions` block
4. If the change is meaningful, add an ADR in `decisions/`

Don't let the backlog silently drift — these items have context that's
expensive to reconstruct.
