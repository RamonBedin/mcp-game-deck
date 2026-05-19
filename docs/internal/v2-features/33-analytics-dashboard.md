# Feature 33 — Analytics Dashboard

## Status

`proposed` — exploratory. Specs to be authored when v2.3 begins.

## Problem

After 12+ months of dogfooding the maintainer has accumulated thousands of turns, hundreds of tool calls, dozens of plan executions — and no insight into the patterns. Questions like:

- "Which tools fail most often in my projects?"
- "How much am I spending per session on Claude?"
- "Which rules actually trigger model behavior changes — and which are dead weight?"
- "What's my typical session length? Tool call density?"

…are unanswerable without instrumenting and surfacing. The data exists implicitly in conversation history, supervisor logs, F22 lifecycle records. It just isn't aggregated or shown.

For the user, this is missing self-awareness about their own AI-assisted workflow. For the maintainer, it's missing signal on which features matter and which don't.

## Proposal

A local-only Analytics page (Settings → Analytics or top-level depending on user reaction). All data stays on the user's machine — no telemetry by default. Optional opt-in for anonymized aggregate sharing (separate F33 follow-up, not v2.3 initial).

**Five sections:**

1. **Usage overview** — sessions count, total turns, total tool calls, total time spent (time-range filtered). Sparkline charts (last 7 / 30 / 90 days).
2. **Tool stats** — top-N most-called tools, success / failure rate per tool, average duration per tool. Failures expandable to see error patterns.
3. **Provider costs** (when F21 ships) — token usage per provider, USD estimate (per-provider current pricing), per-session breakdown.
4. **Plan execution** — plans ranked by run count, success rate per plan, average duration per step. Identifies "the plan I run weekly" vs "the plan that always fails halfway".
5. **Rule effectiveness** (when F26 ships rules tracking) — which rules were "active" during turns, frequency of activation. Helps identify dead-weight rules to delete.

Data source: a lightweight SQLite (or JSON-lines) log at `Library/MCPGameDeck/analytics.db`. Supervisor writes log entries asynchronously per turn; front queries on Analytics page open. No active background processing.

## Scope IN

- **Analytics storage:**
  - SQLite via `tauri-plugin-sql` or sqljs (decide based on bundle size)
  - Schema: sessions, turns, tool_calls, plan_runs, rule_activations (subset depending on F26 tracking)
  - Append-only writes from supervisor; never blocks turn execution
  - Retention: 1 year rolling by default, configurable
- **Analytics page (Settings → Analytics):**
  - Time-range filter (7d / 30d / 90d / all)
  - Five sections as above
  - Charts via `recharts` (already in artifact deps; likely already in main app or easy to add)
- **Export:** "Export to CSV" per section for power-user offline analysis
- **Clear data:** "Clear analytics" affordance (resets the db)
- **Opt-out:** master toggle in Settings → Privacy to disable analytics collection entirely (deletes db, stops writes)

## Scope OUT (deferred to v2.4+ or wontfix)

- **Cloud aggregation / opt-in telemetry** — local-only in v2.3. Cloud is a separate trust + infra discussion.
- **Per-rule trigger detection** beyond "was this rule in the active bundle when the turn ran" — semantic detection of "did the rule actually influence the answer" is hard; skip.
- **Cost optimization recommendations** — "Switch to gpt-4o-mini for these workflows to save 60%" — too presumptuous; surface raw data, let user decide.
- **Team / organization analytics aggregation** — single-user view only.
- **Real-time dashboard** — page refreshes on open / manual refresh button, not live-updating.
- **Anomaly detection / alerts** — "your token usage doubled this week" — leave to user inspection.

## Dependencies

- **F21 (Multi-LLM)** — recommended before. Cost stats require per-provider pricing knowledge; without F21 we have only Claude.
- **F22 (Process hardening)** — recommended. Restart events / crash data are part of "session reliability" insights.
- **F26 (Rules functional)** — recommended. Rule effectiveness section relies on F26's per-agent bundle compilation telling us which rules were active.

F33 functional without these but degraded — partial sections show "data unavailable yet" if dependency unshipped.

## Risks

- **Performance impact of analytics writes** — appending per-turn records on every turn. Mitigation: async writes via channel, never block turn loop. Profile to confirm <5ms overhead per turn.
- **Schema migration** — if analytics schema changes between versions, old db must migrate or be reset. Mitigation: versioned schema, migration scripts for each version bump.
- **Disk space growth** — heavy users could accumulate hundreds of MB of analytics data over years. Mitigation: 1-year rolling retention default + manual clear affordance.
- **Privacy perception** — even local-only analytics may make users uncomfortable. Mitigation: prominent Settings → Privacy section, master opt-out at install time prompt.

## Open questions

1. **Default retention period?**
   - Recommendation: 1 year. Long enough for trend analysis, short enough that disk usage stays under ~50 MB even for power users.
2. **Should the Analytics page be top-level nav or Settings → Analytics?**
   - Recommendation: Settings → Analytics for v2.3. Top-level nav promotion (`📊` icon) considered in v2.4 if users discover and engage.
3. **Should "session" be defined as "app open" or "active conversation"?**
   - Recommendation: active conversation. App-open sessions are weak signal (user opens, makes coffee, types nothing).
