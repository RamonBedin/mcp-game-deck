# Feature 07 — Editor Status Pin

## Status

`agreed` — design pattern from Benzi.ai, scope clear.

## Problem

When the chat moves to the external app (Feature 01), the user still needs:

1. A visible indicator inside Unity that the system is connected and ready
2. A way to launch / focus the external app from Unity
3. Quick visual feedback when something changes (Unity entering play mode, MCP server going down, app crashing)

A persistent, small UI element solves all three.

## Proposal

Add a small status pin to the Unity Editor toolbar (top of the editor). The pin shows connection state with color:

- 🟢 **green** — external app is open and connected to MCP server, ready
- 🟡 **yellow** — Unity is busy (compiling, entering play mode, asset import)
- 🔴 **red** — external app is not running, or MCP server failed to bind
- ⚫ **gray** — no app installed / first run

Clicking the pin:

- If app is running → focuses the app window
- If app is closed but installed → launches the app
- If first run → triggers first-run flow (extract binary from `App~/dist/`, then launch)

Right-clicking shows a small menu:

- Open Chat (default action)
- Restart Server
- Settings (opens app to settings tab)
- About / Version info

## Scope IN

- Editor toolbar widget (UIElements / UI Toolkit, persistent across reloads)
- Polling logic that updates pin color based on:
  - MCP server status (TcpListener bound / not)
  - External app process status (running / not — pin can't directly know, infers from connection heartbeat)
  - Unity editor state (compiling, play mode entering)
- Click handler — launches or focuses external app
- Right-click context menu
- First-run binary extraction trigger
- **Env-var injection on app launch** — Unity is the parent process when the pin spawns the Tauri app, so the project root path can be passed as an environment variable. This avoids requiring users to set `UNITY_PROJECT_PATH` manually before running Tauri.

## Scope OUT (deferred)

- Notification badges (e.g. "3 messages" if app gets a message while user was in Unity) — v2.1
- Customizable pin position (top-right vs top-left) — v2.1
- Multiple pins for multiple Unity projects with different states

## Dependencies

- **Feature 01 (External app)** — pin's whole purpose is to launch / monitor it. Pin can ship before app exists, but useful only after.
- The current chat window UI Toolkit panel **is removed** as part of this feature. Pin replaces it.

## Env-var contract with the Tauri app (locked)

When the pin spawns the Tauri app process, it MUST pass these environment variables. The Tauri side reads them once at startup and caches the values for the app's lifetime — re-launching from a different Unity project is the only way to switch.

| Var | Value | Used by | Notes |
|-----|-------|---------|-------|
| `UNITY_PROJECT_PATH` | Unity project root (folder containing `Library/`, `Assets/`, `ProjectSettings/`) | `unity_client::load_auth_token()` reads `<path>/Library/GameDeck/auth-token` | Already consumed by Feature 01 task 4.2. Without this Tauri can't authenticate to the MCP server. |
| `UNITY_MCP_HOST` | `127.0.0.1` (or whatever the user's Project Settings has) | `unity_client::connection` to override the default loopback | Optional. Only set if user has a non-default host. |
| `UNITY_MCP_PORT` | port number from Project Settings (default `8090`) | `unity_client::connection` | Optional. Default works for the standard install. |

Reference: the legacy in-Unity `Editor/ChatUI/ServerProcessManager.cs` already follows this exact pattern (passing `PROJECT_CWD`, `MCP_SERVER_URL`, `MODEL` etc as env vars to the Node SDK child) — Feature 07 generalizes the pattern to the Tauri spawn. Use `ChatConstants.ENV_*` keys as inspiration, but rename to `UNITY_*` in the Tauri-side contract for clarity (the Tauri app is product-facing, the Node SDK was internal).

**Manual override during dev:** before Feature 07 ships, developers run Tauri standalone and must set `UNITY_PROJECT_PATH` themselves (e.g. via `[Environment]::SetEnvironmentVariable(...)` on Windows or `.env` file). The pin removes this friction for end users.

## Cost estimate

**Small.**

- Toolbar widget UI: ~3 days (UI Toolkit toolbar overlays are well-documented)
- Polling logic + state machine for color: ~2 days
- Process launch (cross-platform) **with env var injection**: ~3 days
- Right-click menu: ~1 day
- First-run extraction flow: ~3-5 days (this is the trickiest piece — see Open Questions)
- Removing the old chat window code: ~2 days

Total: ~2 weeks.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Toolbar overlay disappears after Unity update | medium | Stick to documented APIs (UIElements toolbar). Test on Unity 6000.x major versions. |
| Pin polling causes Unity perf hit | low | Poll at 1-2Hz, very cheap operation (TCP socket existence check) |
| First-run binary extraction fails silently | high | Surface clear error in pin menu. Log to console. Provide manual "extract" command. |
| User confused by why chat panel disappeared after upgrading | high | Release notes + first-run prompt: "MCP Game Deck v2.0 moved chat to a desktop app. Click the pin to open it." |
| Pin state is stale (says green when app actually crashed) | medium | Shorten heartbeat timeout. App sends heartbeat to MCP server; pin reads heartbeat freshness. |
| Env var not propagated on macOS when launching `.app` bundle from Unity | medium | macOS Process API needs explicit `EnvironmentVariables` set on `ProcessStartInfo`; verify on macOS during Feature 07 implementation. Same trick the existing `ServerProcessManager` already uses. |

## Milestone

v2.0.

## Open questions

1. **First-run binary extraction** — when user clicks pin first time, extract from `App~/dist/`. To where? `%APPDATA%/MCPGameDeck/bin/`? Per-Unity-project location? Trade-off: per-project means re-extract per project (slow), global means version mismatches between projects.
2. **What if user's Unity is on a path with spaces or special chars?** Process launch needs to handle this. Test on Windows with username "User With Space" and macOS apps in `~/Documents/Unity Projects/`.
3. **Does the pin need to know which project's app to launch?** If user has two Unity projects open (rare but possible), each pin should launch its own app instance pointing at its own server. Each app needs to know which port. Solved by Unity passing the port via env var or CLI arg to the spawned app — see "Env-var contract" section above.
4. **Behavior when app is running but Unity restarts** — the app stays running, just shows "Unity disconnected". When Unity comes back up, app reconnects automatically. Pin in the new Unity instance shows green if app is already running and connected. This needs careful state sync.

## Notes

- Keep the pin minimal. Resist the temptation to add inline chat in the pin tooltip or quick-action buttons. The point is to be unobtrusive when not needed and one click away when needed.
- Reference Benzi.ai's pin for visual inspiration. Don't copy pixel-for-pixel; we have our own brand.
