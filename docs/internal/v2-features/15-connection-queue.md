# Feature 15 — Connection-Aware Queue + Cancel UX

## Status

`proposed` — design pending Ramon approval. Companion specs (`15-connection-queue-spec.md` + `15-connection-queue-tasks.md`) will follow when execution starts.

## Problem

Two related symptoms hurt chat reliability:

**(a) No queueing when Unity is offline.** If the user closes the Unity Editor mid-conversation and tries to send another message, the message either errors out, hangs, or — worse — gets dropped silently. The user has no signal that "the message can't go through right now". They re-send, get the same nothing, eventually realize Unity is closed.

**(b) Cancel/ESC doesn't update UI state.** The cycle 2 attempt landed a `cancel_current_turn()` Rust command and supervisor logic that correctly interrupts the running turn on first call. But the front-end never receives confirmation of cancellation. The user presses ESC, nothing visibly changes in the chat (turn still appears active), so they press again. The supervisor logs each subsequent press as `no active turn — ignored`. Observed: 14+ ignored-cancel lines in stderr after a single broken plan flow, all because the UI doesn't reflect that the first cancel already worked.

## Proposal

Two coordinated patches, both on the conversation lifecycle.

**Queue path.** `conversationStore.sendMessage()` checks the current Unity connection status before sending. If disconnected or busy, the message is appended to an in-memory `pendingQueue` with `{text, attachments, queuedAt}` and the chat shows the bubble with a tag like `(queued — waiting for Unity)`. An inline pill above the composer reads `2 queued`. The NavRail badge next to "Chat" shows `2`.

A `useQueueDrainWatcher` hook subscribes to `connection-status-changed` events. On transition into `connected`, OR on `turn-complete` if the watcher is mid-drain, it drains the queue one message at a time (sequential — each turn must complete before the next dispatches). Pill and badge clear progressively.

**Cancel path.** Supervisor emits a new `turn-cancelled` event on the first successful `cancel_current_turn()`. Rust forwards as `EVT_TURN_CANCELLED`. `conversationStore` listens; on receipt, sets the active turn status to `cancelled`, clears streaming/loading indicators, resets the `WorkingStrip` activity text, and disables the Cancel button until a new turn starts. The front-side ESC/Cancel handler checks `turn.status === "in-progress"` before round-tripping to the supervisor — subsequent presses while no turn is active are dropped client-side, no more `no active turn — ignored` log spam.

## Scope IN

- **Queue + drain:**
  - `conversationStore` adds `pendingQueue: QueuedMessage[]` state
  - `sendMessage()` checks `unityStatus`; queues if `disconnected` or `busy`
  - Bubble renderer shows `(queued — waiting for Unity)` for messages in queue
  - Inline pill above composer when queue is non-empty
  - NavRail Chat badge when queue is non-empty
  - `useQueueDrainWatcher` hook: drains on `connection-status-changed → connected` OR `turn-complete` (whichever comes first while there's queue)
- **Cancel UX:**
  - Supervisor: emit `turn-cancelled` event after first successful `cancel_current_turn`. Subsequent calls return without emitting.
  - Rust: `EVT_TURN_CANCELLED` constant + forwarder
  - `conversationStore`: listener resets turn state on event
  - `WorkingStrip`: clears activity on turn cancellation
  - ESC/Cancel handler: only round-trips to supervisor if `turn.status === "in-progress"`; otherwise drops silently client-side
- **Validation:**
  - Queue: close Unity, send 2 messages, verify both queued with pill+badge; reopen Unity, verify both fire in sequence (one full turn, then the next).
  - Cancel: start long turn, press ESC once, verify UI transitions to "cancelled" immediately and supervisor stderr shows exactly one `[cancel]` line.

## Scope OUT (deferred to v2.1+)

- **Persistent queue across app restarts** — in-memory only; quitting the app drops pending messages.
- **Cancel-and-keep vs cancel-and-discard nuance** — only "interrupt now" semantics; if the user wants the partial response, they can read what's already streamed and start a new turn.
- **Per-message retry on transient Unity failure** — if a tool call fails mid-turn, that's a Unity-side error surfaced to the chat normally; no automatic retry.
- **Global toast component extraction** — `RulesRoute` has its own local toast; this feature uses inline pill and badge, not toast. Extracting a global toast component waits until a third consumer materializes (no value in extracting for 2 use sites).
- **Queue ordering re-prioritization** — strict FIFO, no manual reorder of pending messages.
- **HUD notification on reconnect** — only the inline pill and badge; no separate "Unity is back" toast.

## Dependencies

- **F11 (Supervisor Activity Stream)** — required for cancel UX to clear `WorkingStrip` correctly. F15 can technically ship before F11 with a fallback that clears `WorkingStrip` to empty, but the integration is cleaner if F11 lands first.

## Risks

- **Queue + multi-message context shift** — if the user queues message A, then later message B that references "the previous answer", and Unity comes back online, A executes first (no prior context), then B (now has A's response). Acceptable; matches FIFO expectation. Tag the bubble visually so the user knows which order.
- **Drain interrupted mid-stream** — if Unity disconnects again while message 1 is mid-turn, message 1 fails, message 2 still pending. Drain watcher should re-evaluate on the new disconnect, not blindly continue. Spec phase verifies.
- **Cancel button race** — if the user spams ESC at exactly the moment a new turn starts, the front-side gate (`turn.status === "in-progress"`) may briefly allow a stray cancel. Mitigation: defer the gate check by a tick or use a turn-start lock; acceptable risk to ship without if the race window is small.

## Open questions

1. **Should queued messages show a remove/edit affordance?**
   - Recommendation: not in v2.0. User can wait for reconnect and delete the bubble after if needed. Edit while queued is over-engineering.
2. **Should we drop very old queued messages on reconnect (e.g., queued > 1 hour ago)?**
   - Recommendation: not in v2.0. Trust the user; they'll cancel manually if they don't want the message anymore. Drop heuristic is brittle.
3. **What if the user closes the app with messages queued?**
   - Recommendation: lose them silently. Persistence is out of scope. If users complain, persistence is a focused v2.1+ task.

## Related cycle 2 attempt notes

The cycle 2 attempt shipped the queue + drain successfully (item 6 passed validation). The gap was the cancel UX (KI-012 — UI didn't reflect cancellation, accumulated ignored signals). The fix scope for cancel UX is small: one new supervisor event, one Rust forwarder, one store listener, one button-state gate.

The queue + drain implementation from the `cycle-2-attempt-1` branch is reusable as reference. Cancel UX is new work for this feature.
