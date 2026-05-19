# Feature 14 — Connection Panel Reactive

## Status

`proposed` — design pending Ramon approval. Companion specs (`14-connection-panel-spec.md` + `14-connection-panel-tasks.md`) will follow when execution starts.

## Problem

`Settings → Connection` panel today is mostly read-only or mockup. Three rows of intended functionality are not wired through:

1. **MCP port input** — appears as a number field in the mockup, but the C# server reads `_mcpPort` from `ProjectSettings/GameDeckSettings.json` only on startup. There's no way to change the port from the app.
2. **Request timeout input** — exists in the settings file (`_requestTimeoutSeconds = 30`) but the C# `McpRequestHandler` doesn't actually honor it. The field is decorative.
3. **Auth token row** — should display the path to `Library/GameDeck/auth-token` with a Copy button. Currently absent.

There's a deeper coupling problem: even if the user changes the MCP port via UI or by editing JSON directly, the Rust client (`UnityClient`) caches the port on startup and never re-reads it. The user follows the help-text instruction "restart Unity + app" — and the app still doesn't reconnect on the new port. Workaround discovered: restart the supervisor process separately. But help-text doesn't mention this, so users experience the editor as broken until they figure it out.

## Proposal

Three editable rows in the Connection panel, all wired end-to-end, plus a fix to make the Rust client respect port changes without out-of-band supervisor restart.

**Row 1 — Auth Token:** read-only display of `Library/GameDeck/auth-token` path. Copy button copies the path (not the token value — token stays hidden for safety). New Rust command `get_auth_token_path()`.

**Row 2 — Request Timeout:** number input clamped 5–300 seconds. On blur, writes to `ProjectSettings/GameDeckSettings.json` via a new Rust command `update_unity_server_settings(key, value)`. C# `GameDeckSettings.cs` gains `LoadIfChanged()` with mtime-based cache so the next request reads the new timeout. `McpRequestHandler` (or wherever Unity tool dispatch happens) uses the value via `MainThreadDispatcher.Execute<T>(func, timeout)`.

**Row 3 — MCP Port:** number input clamped 1024–65535. Reuses the `update_unity_server_settings` plumbing from Row 2 to persist. **Critical:** the Rust client must re-read the port from settings on each connect attempt (Option 1 from the cycle 2 investigation — the lightest fix). After change, help-text indicates "Restart Unity Editor to apply" — and that should be sufficient. No separate supervisor restart needed.

## Scope IN

- **Auth Token row:**
  - Rust command `get_auth_token_path()` returns the absolute path
  - UI row in `ConnectionPanel` with path text + Copy button (`"Copied!"` feedback for 2s)
- **Request Timeout row:**
  - Rust commands `read_unity_setting(key)` and `update_unity_server_settings(key, value)` that read/write `ProjectSettings/GameDeckSettings.json` directly (no TCP round-trip)
  - C# side: `GameDeckSettings.LoadIfChanged()` with mtime cache so the next request sees the new value without restart
  - C# side: `MainThreadDispatcher.Execute<T>(func, timeout)` overload that respects the configured timeout per call
  - `McpRequestHandler` uses the timeout-aware dispatcher
  - UI row: `NumberInput` 5–300, blur to save, "saved" check mark feedback
- **MCP Port row:**
  - Reuses `update_unity_server_settings` from Row 2
  - UI row: `NumberInput` 1024–65535, blur to save, help-text `"Requires Unity Editor restart"`
  - **Reactive port read** in Rust: `UnityClient` re-reads port from settings on each connect attempt (not cached on startup)
- **Behavioral fix:** changing the port and restarting Unity must work end-to-end without supervisor restart. This is the core validation criterion.
- **Validation:** end-to-end test — change port to 8091 via UI, restart Unity (server now on 8091), close+reopen app, status goes green on 8091 without manual supervisor restart.

## Scope OUT (deferred to v2.1+)

- **Hot-reload port without Unity restart** — too risky (C# TCP listener rebind mid-session can collide with other Editor activity). Stick with "restart Unity" message.
- **Pre-validate port collision before save** — let the user discover via reconnect failure if the port is occupied.
- **Token rotation UI** — only displaying the path; rotation lives on the C# side and isn't user-facing in v2.0.
- **Settings encryption** — JSON is plaintext in `ProjectSettings/`, same as Unity defaults.
- **Per-environment configuration** (dev/staging/prod connection profiles) — single profile per project for now.

## Dependencies

None. F14 is independent. The C# side touches `GameDeckSettings.cs`, `MainThreadDispatcher.cs`, and `McpRequestHandler.cs`; the Rust side adds two commands; the React side adds three rows. All loosely coupled.

## Risks

- **Reactive port read race condition** — if the user changes the port *while* the Rust client is mid-reconnect, the client might read the old or new value depending on timing. Mitigation: the reconnect loop already retries; worst case the user sees one failed attempt before it picks up the new value. Acceptable.
- **`LoadIfChanged()` mtime cache** — the mtime check has to happen on every request, not once at startup. If implemented as a startup snapshot it defeats the purpose. Spec phase verifies the check is per-call.
- **Timeout enforcement is multi-layer** — if the Unity tool itself blocks indefinitely (e.g., infinite loop in a script), the dispatcher timeout cancels the wait but the Unity-side thread may keep running. Out of scope for v2.0; just makes the chat usable when slow tools happen.

## Open questions

1. **Should the Auth Token row include a "Rotate" button?**
   - Recommendation: not in v2.0. Token rotation is rare (typically only on suspected compromise) and adding a button risks accidental rotation breaking the running supervisor. Defer to v2.1+ if a real use case appears.
2. **What should the timeout default be after this lands?**
   - Recommendation: keep 30 seconds. Most tools are sub-second; the long tail (lightmap bake, build) is what burns time. Users adjust per-project as needed.
3. **Should there be a "Reset to defaults" link in the Connection panel?**
   - Recommendation: not in v2.0. Three rows is too small to need a reset affordance; if it grows in v2.1+, reconsider.

## Related cycle 2 attempt notes

The cycle 2 attempt shipped working versions of all three rows individually (item 1 + item 2 + item 5). The fatal gap was item 5: the reactive port read was missing, so changing the port left the app permanently offline until manual supervisor restart. This feature's core delta over cycle 2 is the reactive read fix.

The Rust commands, C# `LoadIfChanged`, and dispatcher timeout overload from the `cycle-2-attempt-1` branch are reusable as reference. The missing piece — `UnityClient` reading port from settings per connect — is a single point of change in `commands/connection.rs` (or equivalent).
