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

## Scope OUT (deferred)

- Notification badges (e.g. "3 messages" if app gets a message while user was in Unity) — v2.1
- Customizable pin position (top-right vs top-left) — v2.1
- Multiple pins for multiple Unity projects with different states

## Dependencies

- **Feature 01 (External app)** — pin's whole purpose is to launch / monitor it. Pin can ship before app exists, but useful only after.
- The current chat window UI Toolkit panel **is removed** as part of this feature. Pin replaces it.

## Cost estimate

**Small.**

- Toolbar widget UI: ~3 days (UI Toolkit toolbar overlays are well-documented)
- Polling logic + state machine for color: ~2 days
- Process launch (cross-platform): ~3 days
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

## Milestone

v2.0.

## Open questions

1. **First-run binary extraction** — when user clicks pin first time, extract from `App~/dist/`. To where? `%APPDATA%/MCPGameDeck/bin/`? Per-Unity-project location? Trade-off: per-project means re-extract per project (slow), global means version mismatches between projects.
2. **What if user's Unity is on a path with spaces or special chars?** Process launch needs to handle this. Test on Windows with username "User With Space" and macOS apps in `~/Documents/Unity Projects/`.
3. **Does the pin need to know which project's app to launch?** If user has two Unity projects open (rare but possible), each pin should launch its own app instance pointing at its own server. Each app needs to know which port. Solved by Unity passing the port via env var or CLI arg to the spawned app.
4. **Behavior when app is running but Unity restarts** — the app stays running, just shows "Unity disconnected". When Unity comes back up, app reconnects automatically. Pin in the new Unity instance shows green if app is already running and connected. This needs careful state sync.

## Notes

- Keep the pin minimal. Resist the temptation to add inline chat in the pin tooltip or quick-action buttons. The point is to be unobtrusive when not needed and one click away when needed.
- Reference Benzi.ai's pin for visual inspiration. Don't copy pixel-for-pixel; we have our own brand.
