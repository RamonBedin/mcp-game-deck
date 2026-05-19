# Feature 09 — v2.0 UX Pass

> **Status:** `ready for execution` — design system locked, components drafted, backend asks scoped.
> **Predecessor:** the v1 of this doc described a *working method* ("use Claude to generate reference mockups"). This v2 replaces it with a *delivery spec*: the visual system, the rebuilt components, and the backend work each one expects.
>
> **Owner:** Ramon (execution). The design was produced by Claude (Sonnet) as Game Deck Design in Apr–May 2026; artifacts live in the design project — Audit, Design Vision, hi-fi mockups, and reference React components.

---

## Problem

The v2.0 external app shipped with the architecture correct — process isolation, supervisor, MCP, request cards, watchers — but with placeholder UI: Tailwind `slate-*` defaults, `sky-700` CTAs, generic chat layout, no brand identity, no first-class status surfacing.

Concretely, v1.1 has three classes of UX gaps:

1. **Identity** — the product looks like a generic AI chat; nothing in the UI signals "control deck for Unity, with a deep plugin behind it." The brand mark exists only on the pin icon.
2. **State legibility** — Unity busy, supervisor crashed, queue waiting, agent thinking, sub-agent delegating: each one is invisible in the user's main view. Errors appear as red lines *after* a failure, not as continuous status.
3. **Surface investment** — 10 specialists, 22 slash commands, 16 knowledge docs all live as autocomplete entries only. The plugin's value is invisible from the app's main surfaces.

Shipping v2.0 against generic Linear/Notion defaults undersells the engineering and the brand. This feature closes that gap.

## Proposal

Apply a full visual system pass plus six pieces of UX work that the audit (see design project) identified as high-impact. Replace the previous "use Claude to draw mockups" framing with a concrete design system + rebuilt components + backend asks.

Three artifact sets are produced:

1. **Design system** — tokens (colors, type, space, radii, motion), defined once in `tokens.css` and mirrored into `tailwind.config.js`. Replaces the ad-hoc `slate-*` palette.
2. **Component rewrites and additions** — atoms, shell, request cards, route updates, and one new route (Library). Listed below.
3. **Backend asks (B.01 → B.11)** — small set of supervisor / Rust / C# items that unlock the cleanest behavior. Each is scoped and optional in the sense that the UI degrades gracefully without it.

## Design system

Locked in `02 — Design Vision.html` and `tokens.css`. Highlights below — full token list is in the CSS.

### Tone

> Console of command for games.
> Density of Linear, precision of Raycast, brand warmth of Cursor, industrial-gamer vibe of Warp.
> Dark-only in v2.0 (light deferred to v2.3+).

### Color (named by USE, not by hex)

| Family | Tokens | Notes |
|---|---|---|
| Surface | `--bg-0` … `--bg-5` | Cool dark-violet base (not pure gray). `bg-0` = deepest, `bg-5` = elevated/hover-active. |
| Text | `--txt-1` … `--txt-5` | 1 = brightest, 5 = faintest. Five steps cover the entire hierarchy. |
| Brand | `--violet`, `--cyan`, `--grad-brand` | Gradient reserved for **generative moments** (avatar Claude, active session, primary CTAs, splash). Never decorative. |
| Semantic | `--ok`, `--warn`, `--bad`, `--info` | Fixed meaning. No `warning`-variant button — status is conveyed by Pill/StatusDot. |
| Permission tier | `--tier-read`, `--tier-write`, `--tier-destr` | Used in `PermissionRequestCard`. |
| Per-agent accent | `--ag-shader`, `--ag-ui`, `--ag-dots`, `--ag-perf`, `--ag-gameplay`, `--ag-systems`, `--ag-techart`, `--ag-addr`, `--ag-qa`, `--ag-unity` | One color per specialist. **Never invent a new color**; new agents fall back to `--ag-unity`. |

### Typography

| Family | When to use |
|---|---|
| `Orbitron` (`--f-hud`) | HUD strip text, page eyebrows, section labels, brand mark. **Never for body copy.** |
| `Inter`     (`--f-body`) | Body copy, UI controls, headings, message text. Base size 14px. |
| `JetBrains Mono` (`--f-mono`) | Code, paths, tool names, timestamps, meta lines, kbd hints. |

Size scale: `10–11 / 12.5 / 14 / 15–16 / 20–22 / 28–32 / 40–56`. Mobile/responsive is not a v2.0 concern.

### Motion

- 3 durations: `120ms` (hover/focus), `200ms` (open/close), `360ms` (page-level / success). Default easing `cubic-bezier(0.16, 1, 0.3, 1)` (ease-out-soft).
- Things that **may** move: status dot pulse (`busy` only), avatar gradient during active turn, working strip slide-in, permission card slide-down + accent pulse 1x.
- Things that **must not** move: hover bounces, all-page slides, decorative parallax/shimmer, fade-in on UI controls.

## Five principles

These are decision criteria, not aspirations. When two designs compete, the one that better serves these principles wins.

1. **Status is first-class, not hidden.** The HUD strip exposes Unity + supervisor + permission mode + project name at all times. Never put status behind a click.
2. **Narrative beats JSON.** Permission cards lead with `"Claude wants to delete Assets/Player.prefab"`, not a `toolName + { input }` dump. Raw payload moves to a collapsible.
3. **Density beats whitespace.** Audience is senior devs. Tight, well-hierarchized information > generous padding. Typography does the lifting.
4. **Brand is language, not skin.** Gradient appears only in generative moments (Claude avatar, primary CTAs, splash, focus). Everywhere else is silent.
5. **Failures are UX opportunities, not red boxes.** Unity offline → queue counter, not a red alert. Supervisor crash → "interrupted" caption on pending cards, not a stack trace.

## Information architecture

Five primary destinations + global HUD strip. New surface: **Library**.

```
┌─────────────────────────────────────────────────────────────────┐
│ [hex] jurassic-survivors ▾ · ● UNITY · ● SUPERVISOR · MODE·ASK ...│  ← HUD strip (36px, always on)
├──────────┬──────────────────────────────────────────────────────┤
│ WORKSPACE│                                                       │
│  Chat ●  │                                                       │
│  Plans 3 │             <Outlet />                                │
│  Rules…  │                                                       │
│ KNOWLEDGE│                                                       │
│  Library │                                                       │
│ SYSTEM   │                                                       │
│  Settings│                                                       │
└──────────┴──────────────────────────────────────────────────────┘
```

Route additions for `App~/src/main.tsx`:

```tsx
<Route path="library" element={<LibraryRoute />} />
```

## Components delivered

Located in the design project under `handoff/src/`. Each file carries a JSDoc header explaining its purpose and any `@requires-backend B.0X` dependencies.

### Atoms (`components/atoms/`)

- `BrandHex.tsx` + `BrandGradientDefs` — the hex mark. Three modes (gradient/mono/white). `BrandGradientDefs` mounts once at root.
- `StatusDot.tsx` — ternary status indicator with glow + pulse for `busy`. Used in HUD, Settings, Library, etc.
- `Avatar.tsx` — squared 2-letter initials avatar with 11 variants (10 agents + user). Each variant maps to a fixed `--ag-*` color.
- `Pill.tsx` — tag/badge with 8 variants including the 3 tier ones.
- `Button.tsx` — 5 variants: `default`, `primary` (brand gradient), `ghost`, `danger` (filled red), `destructive` (outlined red). 3 sizes.
- `IconButton.tsx` — 28×28 transparent button for refresh/close/expand.

### Shell (`components/shell/`)

- `HudStrip.tsx` — the global top bar. Reads `connectionStore`, `conversationStore`, `settingsStore` directly. Single source of truth for "what state is the app in".
- `NavRail.tsx` — replaces the existing `App.tsx` `<aside>`. Five items with section headers, badges, brand-violet active accent.

### Updated root (`src/App.tsx`)

Full rewrite. Keeps all four cross-cutting effects (install poll, connection poll, supervisor fast path, route-requested forwarder) verbatim — they're not visual code. Replaces the sidebar layout with `HudStrip + NavRail + <Outlet />`.

### Chat (`components/chat/`)

- `WorkingStrip.tsx` — "Claude is working…" footer with optional agent avatar + cancel. Requires **B.01** for real activity text, **B.02** for cancel.
- `ToolCallNarrativeBlock.tsx` — replaces v1's `ToolUseBlock` + `ToolResultBlock` pair. One narrative row per call; expandable for raw JSON. Status icon (queued / running / done / failed) + duration.
- `ToolCallGroup.tsx` — collapses 3+ consecutive same-family calls into a single card with member rows when expanded.
- `ChatLaunchpad.tsx` — empty-state replacement. Brand hex + welcome + 4 workflow cards + 5 specialist rows.

### Updated `routes/ChatRoute.tsx`

Wires the new chat atoms together. Pairs `tool-use` + `tool-result` blocks by `toolUseId` so each call renders once. Shows `ChatLaunchpad` when `messages.length === 0`. Shows `WorkingStrip` during streaming.

The existing `ChatInput` is reused unchanged — composer logic doesn't change.

### Request cards (`components/requests/`)

Full rewrite of all three files (`RequestCard`, `PermissionRequestCard`, `QuestionCard`) plus a new `toolTier.ts` helper.

- `toolTier.ts` — tier inference table. Until **B.04** ships, classifies tools by name pattern (`asset-delete*` → destr, `*-get-info` → read, etc).
- `RequestCard.tsx` — shared chrome. Tier-aware accent (border + left bar). Auto-allowed branch synthesizes a compact inline caption.
- `PermissionRequestCard.tsx` — narrative-first body ("Claude wants to `<verb>` `<target>`"). Decision reason as a styled quote. Raw inputs collapsible. Tier-colored Allow button (destructive = red).
- `QuestionCard.tsx` — options rendered as cards (not radio/checkbox). Brand violet selection. Free-text fallback. Header shows `0/2 answered`.

### Library (`routes/LibraryRoute.tsx`, `components/library/`)

**Brand new route** — closes audit M.01.

- `LibraryRoute.tsx` — 3-tab layout. Filters by search query.
- `LibraryTabs.tsx` — tab strip with counts.
- `AgentCard.tsx` — agent grid card. "Open in chat" prefills `@agent-name `.
- `KnowledgeReader.tsx` — markdown reader for the 16-doc knowledge base. **Requires B.10** — a placeholder card ships meanwhile.
- Inline `CommandCard` inside `LibraryRoute` — slash-command grid card. "Open in chat" prefills `/cmd `.

### Plans · Rules · Settings · First-run

Not yet in the `handoff/` folder — the existing routes still work, but these are the agreed direction for the v2.0 polish pass after the chat surface lands. Specs:

- **Plans execution view** — when a plan is executing, the route header changes to "Executing `<name>` · step 3/12" and a third column appears (Live progress) with per-step status. **Requires B.05.**
- **Rules Active Bundle panel** — third column showing the exact text injected into the system prompt, with Combined / By-rule tabs. **Requires B.06.**
- **First-run wizard** — 3-step (Claude Code · Auth · SDK) with step indicator + brand hex hero + animated grid backdrop. Drops the standalone-card pattern.
- **Settings reorganized** — vertical sub-nav with 5 panels: Connection / Appearance / Claude Code / Plugin / About. Dev tools move into About → Diagnostics (still gated by `import.meta.env.DEV`).

Each has a wireframe in `02 — Design Vision.html` §10 and a hi-fi mockup in `03 — Mockups.html`.

## Backend asks

Eleven items, in scope-of-effort order. Each component file that depends on one tags it with `@requires-backend B.0X` in the JSDoc header — search for that tag to find call sites.

| ID | What | Area | Effort | Required for |
|---|---|---|---|---|
| **B.01** | Supervisor activity stream — events `turn-started`, `tool-call-queued`, `subagent-started{name,task}`, `subagent-finished{name,summary}` | Supervisor | M | `WorkingStrip` activity text, sub-agent indicator |
| **B.02** | `cancel_current_turn()` Tauri command — sends interrupt to `claude` CLI | Rust + Supervisor | S | Cancel button in `WorkingStrip` |
| **B.03** | Tool metadata catalog — `{ toolName, humanLabel, category, riskTier, exampleInputs }` exposed from the C# server | C# Unity | M | Narrative line in `ToolCallNarrativeBlock`, tier in `PermissionRequestCard`, sample query in `AgentCard` |
| **B.04** | Per-tool risk tier (subset of B.03) — if B.03 is too heavy as a single delivery, ship `riskTier` alone first | C# Unity or Front | S | Tier accent in `PermissionRequestCard` (front-side fallback in `toolTier.ts` works until then) |
| **B.05** | Plan execution events — `plan-step-started{idx,title}`, `plan-step-completed{idx,outcome}` emitted by `/plan-execute` | Plugin skill | M | Plans execution view |
| **B.06** | `preview_rules_bundle()` Tauri command — returns the final string injected into `--append-system-prompt` | Rust | S | Rules Active Bundle panel |
| **B.07** | Chat history mining — "you've done X 8x; create a rule?" suggestions | Rust or Supervisor | L | Rules suggestions (deferred to v2.1) |
| **B.08** | Recent / favorite commands cache — JSON in `ProjectSettings/GameDeck/` | Rust | S | "Recent" section in `ChatLaunchpad` |
| **B.09** | Connection-aware queue — when Unity = busy/disconnected, app queues calls; replays on reconnect | Rust + Supervisor | L | Unity-offline UX (toast + HUD queue counter) |
| **B.10** | Knowledge docs reader — Tauri commands `list_knowledge_docs()`, `read_knowledge_doc(id)` exposing `Plugin~/knowledge/` | Rust | S | Library Knowledge tab |
| **B.11** | Theme tokens layer — already addressed in `tokens.css` and `tailwind.config.patch.js` (this delivery) | Front | M | ✓ done |

**Recommendation:** ship B.04, B.08, B.10, B.06 in any v2.0.x patch — all are < 1 day each and unlock visible UX wins. B.01 + B.02 are the most impactful pair (working strip becomes real). B.05 unlocks the Plans execution view. B.09 is the biggest project — defer to v2.0.2 or v2.1 unless Unity-offline is a common pain point.

## Scope IN

- Design system tokens (`tokens.css`, `tailwind.config.patch.js`) — drop-in.
- All atoms + shell + chat + request-card rewrites — `handoff/src/` ready to copy.
- Library route (new) with Agents + Commands tabs working off existing catalog hooks.
- **Raster icon pack** (`handoff/icons/`) — Unity pin placeholder + Tauri bundle (32, 128, 128@2x, 512) + Windows multi-resolution `.ico`. Drops into `Editor/Resources/` and `App~/src-tauri/icons/` unchanged.
- Backend asks documented with effort estimates and tags in code.

## Scope OUT (deferred)

- Plans execution view, Rules Active Bundle, First-run wizard, Settings reorg — designs locked (mockups + Vision); React-side delivery pending after chat lands and backend B.05/B.06 ship.
- Light mode — v2.3+. `tokens.css` is structured so a future `:root.theme-light { … }` overlay is a one-screen change.
- Localization — same deferral. Copy is in English in keeping with v1 + Claude Code conventions.
- Custom keyboard shortcuts beyond ⇧⏎ / ⌫ / esc — v2.1.
- Knowledge reader full implementation — depends on B.10.

## Dependencies

- Feature 01 (external app) — ✓ shipped Apr 2026
- Feature 02 (Claude Code supervisor) — ✓ shipped Apr 2026
- Feature 04 (interactive approvals & questions) — ✓ shipped Apr 2026; the request cards in this pass are full rewrites of that work
- Feature 06 (plans CRUD + slash/@ autocomplete) — design-locked May 2026; the plans-execution view in this pass extends F06 once it ships
- Feature 07 (Editor pin) — ✓ shipped Apr 2026; the pin's status colors should mirror `HudStrip`'s status dots (post-merge sweep)
- Feature 08 (Rules page) — pending revision under ADR-001; this pass updates only the visual layer

## Cost estimate

**Medium** — most of the cost is review + integration, not authoring.

| Phase | Effort |
|---|---|
| Drop `tokens.css` + Tailwind patch, audit `slate-*` references | 0.5 day |
| Atoms + shell (drop-in, no semantic changes) | 1 day |
| `App.tsx` + nav rail behavioral verification (install / poll / supervisor) | 0.5 day |
| Chat atoms + `ChatRoute` rewrite + visual regression | 2 days |
| Request cards + `toolTier.ts` + verify against existing wire payloads | 1.5 days |
| Library route (Agents + Commands tabs working; Knowledge placeholder) | 1.5 days |
| Backend B.04, B.06, B.08, B.10 (each ~0.5–1 day) | 2.5 days |
| Plans execution view (depends on B.05) | 1.5 days after B.05 |
| Rules Active Bundle (depends on B.06) | 1 day after B.06 |
| First-run wizard rewrite | 1 day |
| Settings reorg | 1 day |

Total to ship the chat-centric pass: ~6–7 days. Full pass including Plans/Rules/Settings/FirstRun: ~10–11 days plus backend.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Tailwind JIT misses some tokens (CSS-variable color classes can be eager-loaded incorrectly) | medium | Tokens are defined as `colors: { "bg-2": "var(--bg-2)" }` in the patch — Tailwind expands the variable at runtime. Adding `@layer components` for any genuinely problematic class is the escape hatch. |
| `toolTier.ts` heuristic classifies a write tool as destructive (or vice versa) | medium | B.04 fully resolves it. Until then, the patterns in `TOOL_TIER_PATTERNS` are conservative (false negatives, not false positives — unknown tools default to `write`). |
| Working strip activity text feels generic before B.01 ships | medium | Default fallback ("Claude is working…") is acceptable; the strip's presence alone is the key UX win, real text is incremental. |
| Per-agent colors (`--ag-*`) feel arbitrary; agents added later have no color | low | New agents fall back to `--ag-unity` (neutral). The 5 explicit colors in the launchpad cover the most-mentioned specialists; the rest inherit neutral cleanly. |
| Density of the HUD strip on narrow Tauri window sizes | low | Layout: project switcher · status · status · mode · session · version. At minWidth 800px the right group can collapse to just `claude vX` if space is tight; not yet implemented — track as v2.0.1 polish. |

## Open questions

1. **Should we keep both `allow` / `allow-always` as separate buttons, or move `allow-always` into a menu?** Current design keeps both visible (audit F.08 fix). The footer hint mentions ⇧⏎ for default allow. Reconsider after dogfood.
2. **Should the HUD's project switcher actually be implemented in v2.0, or is it a v2.1 follow-up?** Designed as if functional (`▾` chevron). If single-Unity-project assumption holds, the chevron can be visually-only for v2.0 (no menu) — the affordance still reads. **Suggest:** dropdown lists open Unity projects (read from `Library/GameDeck/` siblings); switching emits a new app instance. Defer to v2.1 if F07's project isolation is the answer.
3. **Working-strip behavior when a `permission-requested` is pending.** Today the strip would show during the wait. UX says the strip should **hide** when the user holds the turn (the user is the bottleneck, not Claude). Recommend: strip is suppressed while any block in `messages` has `state: "pending"`.

## Notes

- The design project (`game deck` in the org) contains four artifacts that may be useful when this lands:
  - `01 — Audit.html` — 13 findings + 11 backend asks (this doc's predecessor).
  - `02 — Design Vision.html` — full design system documentation (principles, tokens, IA, voice, lo-fi wireframes).
  - `03 — Mockups.html` — hi-fi navigable mockups for every screen this pass touches.
  - `handoff/` folder — the React components referenced here, plus this `.md`.
- The mockups are not production code (no IPC, no stores). They're visual targets. The `handoff/src/` components are production-shaped — they consume the existing stores and IPC functions, follow the project's coding style, and are typed against the existing `types.ts`.
- Anything in the handoff that violates the project's coding standards (Allman, JSDoc, etc) is a bug — flag it and it'll be fixed in the design project.

---

**Generated 2026-05-15** by Claude Sonnet 4.5 acting as Game Deck Design.