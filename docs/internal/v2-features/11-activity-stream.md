# Feature 11 — Supervisor Activity Stream

## Status

`proposed` — design pending Ramon approval. Companion specs (`11-activity-stream-spec.md` + `11-activity-stream-tasks.md`) will follow when execution starts.

## Problem

The `WorkingStrip` component (the thin status bar above the chat composer) only ever shows `"Claude is thinking…"` regardless of what the assistant is actually doing. During a long turn that fires several tool calls — say, a delegation that spawns a subagent and the subagent reads 10 files — the user sees no progression, no signal that work is happening, no indication of which subagent is active. They sit and wonder if the app froze.

The component was designed in F09 with three states in mind: idle, thinking, calling a specific tool, and delegating to a subagent (with avatar). All the rendering plumbing exists. What's missing is the event stream from the supervisor that tells the front-end *what* is happening *when*.

## Proposal

The supervisor process (Node.js, drives the Claude Agent SDK) emits structured stdio events to its parent (the Rust Tauri host) at three lifecycle moments:

- `turn-started` — the moment a user message enters the `query()` loop. Carries no payload (or just a turn ID for correlation).
- `subagent-started` — emitted when the SDK signals it's spawning a Task subagent. Payload: `{subagent_type, turn_id}`.
- `subagent-finished` — emitted when the subagent's response completes (success or error). Payload: `{subagent_type, turn_id, status}`.

(`tool-call-queued` and text-delta-driven "Writing response…" are derived front-side from existing message streaming, no new events needed.)

Rust forwards these as Tauri events (`EVT_TURN_STARTED`, `EVT_SUBAGENT_STARTED`, `EVT_SUBAGENT_FINISHED`). A new `activityStore` (Zustand) subscribes and exposes a `currentActivity` selector. `WorkingStrip` reads from the store and renders the right text + (for subagent) an avatar circle with the specialist's initials.

State machine for the strip:
- `idle` → empty
- `turn-started` arrives → `"Claude is thinking…"`
- `tool_use` block arrives in stream → `"Calling <humanLabel of tool>"` (uses F10 catalog)
- `subagent-started` arrives → `"Delegating to <subagent_type>"` + avatar circle showing 2-letter initials (`technical-artist` → `TA`)
- `subagent-finished` arrives → back to `"Claude is thinking…"`
- text delta starts streaming → `"Writing response…"`
- turn closes (last `result` message) → back to `idle`

## Scope IN

- **Supervisor events:** `sdk_entry.js` emits the 3 events at the right SDK lifecycle moments. Detection of `subagent-started`/`finished` may need heuristic detection of `Task` tool calls with a `subagent_type` arg since the SDK might not expose delegation as a first-class event — investigate during execution and document findings.
- **Rust forwarder:** 3 new event constants in `events.rs`, forwarder in `commands/supervisor.rs` (or wherever supervisor stdio is parsed) that decodes the structured events and emits them to the frontend.
- **TS types:** `TurnStartedEvent`, `SubagentStartedEvent`, `SubagentFinishedEvent` in `ipc/events.ts`.
- **Store:** `activityStore` (Zustand) with subscription to the 3 events. Exposes a single `currentActivity` selector returning `{text, subagent?}`.
- **Hook:** `useActivitySubscription()` mounts subscribers on app boot.
- **WorkingStrip wiring:** reads `currentActivity` from the store. Renders text + (when `subagent` present) avatar circle component with initials.

## Scope OUT (deferred to v2.1+)

- **Activity history persistence** — only the current state is rendered; no scrollable log of past activity.
- **Per-tool-call timing info** — strip doesn't show "this took 3.2s"; just shows what's happening now.
- **Subagent nested delegation** — if a subagent itself delegates further, the strip just shows the outer subagent. Nested visualization is a F25 (per-project window isolation) follow-up.
- **Message-count notification badges** — F07 follow-up; this feature only handles in-conversation activity, not "X new messages while you were in Unity".

## Dependencies

- **F10 (Tool Metadata Catalog)** — required for `humanLabel` in "Calling <tool>" text. F11 will look wrong without it (would show raw `asset-get-info` instead of "Asset / Get Info"). Block F11 execution until F10 ships.

## Risks

- **SDK delegation detection** — the Claude Agent SDK may not expose subagent spawn/end as first-class lifecycle events. If we have to detect via `Task` tool_use parsing in the stream, edge cases (e.g. parallel Tasks) may break the simple "one active subagent at a time" model. Mitigation: investigate SDK API early in spec phase; if delegation isn't observable, scope subagent-started/finished out of v2.0 and ship `turn-started` + tool-driven "Calling X" alone.
- **WorkingStrip re-renders on every event** — at 60Hz of stream deltas this could chatter. Mitigation: debounce text-delta-driven `"Writing response…"` transitions (only flip *into* writing on first delta, hold it until something else fires).
- **Subagent avatar collisions** — `technical-artist` and `tank-architect` both produce `TA`. Mitigation: in v2.0 don't worry about it (only 10 specialists, manually verified unique initials in F08); if a name collision appears later, add a discriminator suffix in the avatar component.

## Open questions

1. **Should `tool-call-queued` be its own supervisor event, or derived from stream?**
   - Recommendation: derive from stream. The supervisor already sees `tool_use` blocks in the assistant message — front can re-derive the same info without a new event channel. Less plumbing.
2. **What happens during the (small) window between `turn-started` and the first tool/text delta?**
   - Recommendation: show `"Claude is thinking…"` (matches today's static behavior). It's only milliseconds in most cases.
3. **Should subagent failure (`status: error`) show a distinct strip state?**
   - Recommendation: not for v2.0. Failure is rare; the chat itself surfaces the error. Strip just clears.

## Related cycle 2 attempt notes

The cycle 2 attempt shipped a working version of this (item 4 sub-test A passed). The remaining gap — sub-test B (subagent delegation) — was blocked not by F11 but by F13 (subagent capabilities — agents had no MCP access). Once F13 lands and F11 lands cleanly, end-to-end delegation flow + WorkingStrip subagent state should work together.

Code from the `cycle-2-attempt-1` branch is reusable as a reference, but tasks should validate from scratch given delegation infrastructure is being rebuilt in F13.
