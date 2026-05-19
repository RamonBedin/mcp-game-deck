# Feature 25 — Per-Project Window Isolation

## Status

`proposed` — design pending Ramon approval. Picks up F07 (Editor Status Pin) L-item deferral. Companion specs (`25-per-project-window-spec.md` + `25-per-project-window-tasks.md`) will follow when execution starts.

## Problem

A user with multiple Unity projects open simultaneously (e.g., a main game and a tools/utilities project) gets a single MCP Game Deck app window that shows the most recently focused project. Switching between Unity projects forces a context switch in the chat — old messages from project A are replaced by project B's, and there's no way to keep a parallel chat going for each.

In practice this means:
- A user inspecting an asset bug in project A can't ask Claude a quick question about project B without losing the A conversation
- Notification of activity (e.g., a long build finished in project B while the user was in project A) doesn't surface — no visual signal of "something happened in the other project"
- Sessions accidentally interleave when the user switches projects mid-conversation, leading to confused replies that reference the wrong project context

F07 designed the Editor status pin (the small Unity-side overlay showing connection state). F07 explicitly deferred per-project window isolation as a "Large item" — too much surface to fit in v2.0.

## Proposal

One app window per active Unity project, with project state tracked at the OS process level.

**Architecture:**
- The Tauri host detects multiple Unity Editor connections (each binds to a different port, configured per-project)
- For each detected project, spawn a separate window with its own session, supervisor child, and `conversationStore` instance
- Window title shows the project name (e.g., "MCP Game Deck — JurassicSurvivors")
- Windows persist position + size in `Library/MCPGameDeck/windows.json` keyed by project path

**Cross-window state:**
- A new "Projects" panel in each window (collapsible sidebar) lists all active projects with connection status
- Switching projects from the panel doesn't change the current window's session — it raises (focus) the other window or opens it
- A new project tab/window opens when a Unity project pin connects for the first time

**Notification badges:**
- Each project window tracks unread message count since last focus
- When the window is in the background or minimized, incoming assistant messages increment a counter
- The OS taskbar shows the badge (Windows: overlay icon; macOS: dock badge)
- Returning focus to the window clears the badge

## Scope IN

- **Multi-window spawn:** Tauri host detects multiple Unity connections; spawns one window per project
- **Per-window state isolation:**
  - Independent `conversationStore` per window
  - Independent supervisor child process per window (or shared supervisor with per-window routing — TBD in spec)
  - Independent connection state per window
- **Window title + project name:** title bar shows `MCP Game Deck — <ProjectName>`
- **Window position persistence:** `Library/MCPGameDeck/windows.json` schema:
  ```json
  {
    "version": 1,
    "windows": [
      { "project_path": "G:\\Unity-Projects\\Jurassic", "position": {"x": 100, "y": 100, "w": 1200, "h": 800} }
    ]
  }
  ```
- **Projects panel:**
  - Collapsible sidebar in every window
  - Lists all detected active Unity projects
  - Connection status per project (green/yellow/red dot)
  - Click → focus that project's window (or spawn it if not yet open)
- **Notification badges:**
  - Per-window `unreadCount` state (incremented on incoming assistant message when window unfocused)
  - OS taskbar badge: Tauri 2's `set_badge_count` for macOS/Linux, `setTaskbarOverlay` for Windows
  - Cleared on window focus event
- **Single-supervisor multiplexing OR per-window supervisor — decide in spec phase:**
  - **Option A:** one supervisor process handles all projects, routes messages by `project_id`
  - **Option B:** each window spawns its own supervisor
  - Trade-off: A is lighter on memory; B is cleaner isolation. Lean A.

## Scope OUT (deferred to v2.3+)

- **Window groups / project clusters** — no UI to "group these three projects" — flat project list.
- **Cross-project conversation references** — can't say "ask the chat in project B about its build settings" from project A's chat.
- **Shared chat history search across projects** — search scoped to current window.
- **OS native push notifications** — taskbar badges only, no toast or system notification.
- **Per-project provider selection** (when F21 ships) — F21's per-session lock continues; cross-project provider choice is a v2.3+ topic.

## Dependencies

- **F22 (Process / Lifecycle Hardening)** — strongly recommended before F25. Multiple supervisors (or a multi-routing supervisor) raise the surface area for orphan processes and crash-restart bugs. Hardening first makes per-project window isolation safe to ship.

## Risks

- **Memory footprint with many projects** — each Tauri window + WebView is ~50–150MB. 5 windows = ~500MB. Mitigation: documented limit (recommended max 3–4 projects open), graceful degradation if user opens 10.
- **Supervisor routing complexity (Option A)** — if one supervisor handles all projects, message routing logic gets non-trivial. Bug: a turn for project A could surface in project B. Mitigation: explicit `project_id` field on every IPC message; supervisor refuses cross-project mixing.
- **Window position persistence on multi-monitor setups** — restoring a position from a monitor that's now disconnected lands the window off-screen. Mitigation: clamp restored position to current visible bounds.
- **Badge resets on background tasks** — if the user is in project A and an assistant message arrives in project B, badge should appear. But if the user has the project B window's content visible (just not focused), should it still badge? Mitigation: badge only on "window not focused", regardless of visibility.

## Open questions

1. **What happens to an already-open window when the corresponding Unity Editor closes?**
   - Recommendation: window stays open, status goes red (`UNITY DISCONNECTED`); the queue from F15 handles "send anyway" UX. User can close the window manually.
2. **Closing a window: does it close the project's supervisor too, or keep it running?**
   - Recommendation: closes the supervisor (clean lifecycle). If user reopens, fresh session.
3. **What about a single Unity project with multiple open scenes / sub-projects?**
   - Recommendation: still one window per Unity project root. Sub-project isolation is over-engineering for v2.1.

## Related notes

This is one of the larger v2.1 features in terms of surface area, despite being polish over F07. Touches: Tauri window management, supervisor lifecycle, per-window state stores, OS taskbar integration on 2+ platforms, multi-window UX patterns.

Implementation estimated 8–10 days. Significant manual testing across Windows + macOS for window/badge behavior. Coordinate with F22 (process hardening) for stable rollout.
