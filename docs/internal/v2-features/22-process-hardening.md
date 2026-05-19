# Feature 22 — Process / Lifecycle Hardening

## Status

`proposed` — design pending Ramon approval. Picks up F02 (Claude Code Supervisor) deferrals. Companion specs (`22-process-hardening-spec.md` + `22-process-hardening-tasks.md`) will follow when execution starts.

## Problem

The supervisor process (Node.js child of the Tauri Rust host) has several known fragility points in its current implementation that surface as user-visible bugs:

- **Orphan supervisor processes on app crash (Windows).** If the Tauri app crashes or is force-killed, the Node supervisor child can survive as an orphan, holding the MCP port and preventing the app from reconnecting cleanly on next launch. Users see "port already in use" until they manually kill the orphan via Task Manager.
- **Same on Unix, plus subprocess sprawl.** The supervisor itself spawns sub-processes (the Claude Agent SDK CLI binary, occasional helper scripts). On Unix, a panicked parent leaves descendants holding terminal sessions, sockets, and memory.
- **Supervisor crash with no auto-restart.** If the Node supervisor process exits unexpectedly mid-session (out-of-memory, unhandled exception, panic from an SDK bug), the app silently goes offline. User has to restart the app manually.
- **No graceful shutdown.** Closing the app with `X` button doesn't give the supervisor time to flush state. In-flight tool calls are dropped, the SDK doesn't get a chance to clean up its API session, future cancellation requests may hit zombie state on Anthropic's side.

These are the classic "process hygiene" problems that every desktop app with a child process eventually has to harden. F02 left them as deferrals because v2.0 prioritized correct-when-things-work over robustness-when-they-don't.

## Proposal

Three coordinated hardening passes.

**(a) Atomic process trees via OS-native primitives.**
- **Windows:** Use a Job Object (`CreateJobObject` + `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`) that wraps the supervisor and its descendants. When the Tauri host exits — clean shutdown, crash, or force-kill — the OS kernel kills the entire job atomically. No orphans possible.
- **Unix:** Use process groups (`setpgid`) so the supervisor and its children share a PGID. On host shutdown, send `SIGTERM` to the group, then `SIGKILL` after a grace period. The kernel ensures even uncooperative descendants die.

**(b) Supervisor auto-restart with backoff.**
The Rust host watches the supervisor child's exit code via the existing IPC channel. On unexpected exit (non-zero, not from clean shutdown), restart it with exponential backoff (1s, 2s, 4s, 8s, capped at 30s). Surface a brief HUD notice ("Supervisor restarted") and reset connection state. After 5 consecutive crashes in 5 minutes, stop auto-restarting and surface a banner asking the user to restart the app manually (probably a real bug, not transient).

**(c) Graceful shutdown sequence.**
Wire the Tauri "close requested" lifecycle hook to send a `shutdown` signal to the supervisor over its IPC. Supervisor:
1. Stops accepting new turns
2. Cancels any in-flight turn (existing `cancel_current_turn` plumbing)
3. Closes the Unity TCP connection cleanly
4. Exits with code 0

Host waits up to 3s for clean exit before falling back to the Job Object / process group kill. User experience: closing the app doesn't leave any process behind, and any half-running tool calls are explicitly cancelled rather than abandoned.

## Scope IN

- **Windows Job Object integration:**
  - Wrap supervisor spawn in Tauri Rust code with `windows-rs` `CreateJobObjectW` + `AssignProcessToJobObject` calls
  - `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` flag for atomic cleanup
  - Verify on intentional `taskkill /F`, `End Task`, and panic-induced exit
- **Unix process group integration:**
  - `setpgid(0, 0)` in spawn (creates new session)
  - On host shutdown: `kill(-pgid, SIGTERM)`, wait, `kill(-pgid, SIGKILL)`
  - Verify on `kill -9` and panic
- **Supervisor auto-restart:**
  - Watcher loop in Rust observes supervisor exit code
  - Exponential backoff: 1s → 2s → 4s → 8s → 16s → 30s (cap)
  - Reset connection state on each restart attempt
  - Surface HUD notice via existing event channel
  - After 5 crashes in 5 min, escalate to user-visible banner with "Restart app" CTA
- **Graceful shutdown:**
  - Tauri `before_quit` (or equivalent) handler sends `shutdown` to supervisor IPC
  - Supervisor implements `shutdown` handler: stops accepting, cancels in-flight, closes connections, exits 0
  - Host waits up to 3s for clean exit
  - Fallback: force-kill via Job Object / process group
- **Logging:** every restart, shutdown, force-kill event logs with timestamp + cause to a rolling log file in `Library/MCPGameDeck/logs/`.

## Scope OUT (deferred to v2.3+)

- **Crash dump capture** — minidumps on Windows, core dumps on Unix. Useful for debugging but adds platform-specific complexity. v2.3 if crashes become a meaningful issue.
- **Telemetry of crash frequency** — opt-in crash analytics. Requires F33 (analytics dashboard) infrastructure first.
- **Multi-supervisor support** (one per project window) — single supervisor architecture continues; multi is gated on F25 (per-project window isolation).
- **Hot reload of supervisor on plugin update** — supervisor only restarts on crash or app restart, not on `tauri-plugin-claude-code-sdk` updates mid-session.
- **Watchdog for stuck supervisor (not crashed, just unresponsive)** — health-check pings exist in v2.0 connection layer; if they fail repeatedly, that's reconnection state, not a "supervisor is alive but hung" detection. v2.3 if observed.

## Dependencies

None. Hardening can land anytime. Recommended to ship alongside or before F21 (Multi-LLM), because F21 introduces more diverse provider SDK behavior which raises the marginal probability of supervisor crashes.

## Risks

- **Job Object permission edge cases** — under some corporate Windows policies, JO creation requires elevated permissions. Mitigation: check creation; fall back to "manual process tracking + sweep on shutdown" if JO unavailable (less atomic but functional).
- **Process group complications on macOS** — terminal-tty interactions can interfere. Verify on actual macOS install during spec phase; mitigation patterns are well documented (most editors and shells handle this).
- **Backoff masks real bugs** — if a deterministic crash bug exists, auto-restart cycles 5 times in 5s and then surfaces a banner — but the user sees no Claude responses for that period. Mitigation: the banner appears earlier (after 2 quick consecutive crashes) instead of after 5.
- **Graceful shutdown deadlock** — supervisor doesn't respond to `shutdown` within 3s, host force-kills, but the SDK may be holding an unflushed HTTP connection to Anthropic that lingers on the network side. Mitigation: 3s timeout is sufficient for realistic shutdown work; lingering HTTPS doesn't block local exit.

## Open questions

1. **Should the auto-restart counter persist across app sessions?**
   - Recommendation: no, reset on every app boot. A crash-on-launch loop is detected by other means (the user observes the app behaving strangely on the first try) and persisting state risks false-positive escalation.
2. **What if the user disables auto-restart?**
   - Recommendation: not configurable. Auto-restart is always on; users who don't want it can close the app. No "expert mode" toggle that adds surface for confusion.
3. **Should graceful shutdown wait for the current turn to finish (not cancel it)?**
   - Recommendation: cancel by default. Waiting could hang shutdown indefinitely on a slow tool. Users who care can manually wait + cancel before closing.

## Related notes

F02 (Claude Code Supervisor) shipped the working supervisor; F22 hardens its lifecycle. The deferrals from F02's design doc map directly to this feature's three pillars.

Implementation surface is small relative to v2.0 features: most code is in `App~/src-tauri/src/supervisor.rs` (or wherever the spawn lives) + small additions to the supervisor's `sdk_entry.js` for the `shutdown` handler. Estimated 3–5 days including testing.
