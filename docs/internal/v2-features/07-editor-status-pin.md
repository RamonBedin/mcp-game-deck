> ⚠️ **ADR-001 applies.** See `docs/internal/architecture/ADR-001-claude-code-sdk-as-engine.md`.
> **Status post-ADR:** `unchanged` — Editor side, independent of the chat engine. The 7 locked decisions all stand. The dependency on Feature 02 was removed (Feature 02 was superseded by ADR-001); cleanup behavior is unchanged.

# Feature 07 — Editor Status Pin

## Status

`design locked` — all 7 design decisions resolved (April 2026). Ready to generate `07-editor-status-pin-spec.md` (executable spec) and `07-editor-status-pin-tasks.md` (decomposed task list for Claude Code).

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

**Click (left)** → open / focus the chat. Default action.

**Right-click** → context menu with: Settings · Copy MCP Server URL · Show install folder · About. Detailed in Locked decision #6.

## Scope IN

- Editor toolbar widget (UIElements / UI Toolkit, persistent across reloads)
- Polling logic that updates pin color based on:
  - MCP server status (TcpListener bound / not)
  - External app process status (running / not — pin can't directly know, infers from connection heartbeat)
  - Unity editor state (compiling, play mode entering)
- Click handler — launches or focuses external app
- Right-click context menu (4 items, see decision #6)
- First-run binary download flow (from GitHub Release attach)
- Auto-update of binary when package version changes (binary version follows package version)
- **Env-var injection on app launch** — Unity is the parent process when the pin spawns the Tauri app, so the project root path can be passed as an environment variable. This avoids requiring users to set `UNITY_PROJECT_PATH` manually before running Tauri.
- **Cleanup of v1.x ChatUI code** — pin replaces the in-Unity chat panel, so the old code is removed in this feature (see "Cleanup scope" section).
- **Port-collision tooltip** — when MCP Server fails to bind because another process is on the configured port (likely a second Unity project), pin turns red and tooltip explains the cause + how to fix in Project Settings.
- **CLI route argument support in Tauri** — the app accepts `--route=/path` to land directly on a specific tab (used by the Settings menu item).

## Scope OUT (deferred)

- Notification badges (e.g. "3 messages" if app gets a message while user was in Unity) — v2.1
- Customizable pin position (top-right vs top-left) — v2.1
- Multiple pins for multiple Unity projects with different states
- Migration of `Server~/src/index.ts` from WebSocket to stdio+JSON-RPC transport — under ADR-001, `index.ts` is removed (not migrated) along with the rest of the custom Agent SDK Server. Feature 07 leaves it on disk untouched; the v2.0 cleanup pass (post-ADR) deletes it.
- **Automatic port-collision recovery** — pin does not auto-pick a free port. User changes manually in Project Settings (see decision #4). Auto-fallback could land in v2.x if it becomes a real pain point.
- **Final product icon** — Feature 07 ships with a placeholder icon. The real MCP Game Deck logo (and the matching Tauri app icon) is designed in Feature 09 (Claude Design hand-off) and dropped in afterwards.
- **"Force re-download app" menu item** — useful for debugging corrupt downloads, but rare for end users. Could be added in v2.x if support tickets reveal need.
- **"Disable pin" menu item** — Toolbar Overlays already support hiding via Unity's overlay menu. No need to duplicate.
- **"Restart Server" menu item** — ambiguous (which server?), and the Tauri Settings tab already has a Restart Node SDK button. Not added.
- **Auto-close orphan Tauri instances when Unity changes project** — explicit user action required. See decision #7.

## Dependencies

- **Feature 01 (External app)** — pin's whole purpose is to launch / monitor it. Done as of April 2026.
- **Feature 02 (Orchestrator)** — ~~replaces the Server~/index.ts WebSocket transport with stdio+JSON-RPC~~ **superseded by ADR-001.** The custom Agent SDK Server is removed entirely; Claude Code orchestrates natively. Feature 07 still deletes `Editor/ChatUI/` per its own cleanup scope; `Server~/src/index.ts` and siblings are removed in the v2.0 cleanup pass that follows ADR-001 (not by Feature 07).
- **Feature 09 (Claude Design hand-off)** — produces the final product icon used by both the pin and the Tauri app. Feature 07 ships with a placeholder; Feature 09 swaps it.
- The current chat window UI Toolkit panel **is removed** as part of this feature. Pin replaces it.

---

## Locked decision #1 — Binary distribution and updates

**Decided:** April 2026.

### Where the Tauri binary lives

- **Distribution channel:** GitHub Release attach. Each tagged release of the package (`v0.1.0`, `v0.2.0`, ...) attaches a `mcp-game-deck-app-<version>.exe` (and `.app.zip` / `.AppImage` later) plus a sibling `.sha256` file for integrity check.
- **Local install path:** `%APPDATA%\MCPGameDeck\bin\<version>\mcp-game-deck-app.exe` on Windows. (macOS: `~/Library/Application Support/MCPGameDeck/bin/<version>/`. Linux: `~/.local/share/MCPGameDeck/bin/<version>/`.)
- **Versioned by subfolder.** Multiple Unity projects with different package versions can each have their matching binary side-by-side. No version conflict possible.
- **Not bundled in the package.** The package contains zero binaries — keeps git repo lean, no LFS needed. `App~/dist/` is gitignored.

### Why GitHub Release attach (and not bundle in package)

- Open-source repo, bundling binaries in git inflates history permanently (~9 MB × N versions, forever).
- GitHub Releases are first-class storage, served by CDN, free.
- Binary distribution stays decoupled from source distribution — useful for dogfooding pre-releases without touching `main`.

### How the pin discovers which binary to download

- The pin reads `PackageInfo.version` (from `UnityEditor.PackageManager.PackageInfo.FindForAssembly`), which mirrors `package.json::version` of the installed package.
- The download URL is **convention-based**, no separate manifest file:
  - `https://github.com/RamonBedin/mcp-game-deck/releases/download/v{version}/mcp-game-deck-app-{version}.exe`
  - `https://github.com/RamonBedin/mcp-game-deck/releases/download/v{version}/mcp-game-deck-app-{version}.exe.sha256`
- This means the package version and the binary version **always match** — no decoupled versioning, no compatibility matrix.

### Auto-update flow when package is updated

- User updates package via UPM (e.g. `0.1.0` → `0.2.0`).
- Next time pin is clicked:
  1. Pin reads `PackageInfo.version` = `0.2.0`.
  2. Pin checks `%APPDATA%\MCPGameDeck\bin\0.2.0\mcp-game-deck-app.exe` — not found.
  3. Pin downloads from convention-based URL, validates SHA256, writes to that path.
  4. Pin spawns the new binary.
- Old versions in `%APPDATA%\MCPGameDeck\bin\0.1.0\` are left in place. Cleanup of orphaned versions is deferred (would require a "active version" registry; not worth it for a few MB).

### Recovery from download failures

- **Network error / GitHub unreachable:** pin shows a dialog with the convention-based download URL clickable, instruction to download manually and place in `%APPDATA%\MCPGameDeck\bin\<version>\`, plus retry button.
- **SHA256 mismatch:** pin deletes the corrupt download, shows error dialog (suspected tampering or partial download), offers retry.
- **Binary fails to launch:** pin captures `Process.ExitCode`, logs to console, shows dialog with link to issue tracker.
- **Offline first-run:** pin offers manual download flow (open URL in browser, drop file into folder).

### Update notification UX (where users hear about new versions)

- **`Editor/Utils/UpdateChecker.cs` is kept** but stripped down:
  - ✅ Continues fetching `https://api.github.com/repos/RamonBedin/mcp-game-deck/releases/latest` every 24h.
  - ✅ Continues populating `EditorPrefs` with latest version + release URL.
  - ❌ `Debug.Log` removed (no console spam).
  - ❌ Banner in `GameDeckSettingsProvider` removed (Settings page becomes config-only, no nag).
- **The pin** reads the `EditorPrefs` and shows a subtle dot/badge if `IsUpdateAvailable == true`.
- **The Tauri app** receives env vars on spawn:
  - `MCP_GAME_DECK_UPDATE_AVAILABLE` (`"true"` / `"false"`)
  - `MCP_GAME_DECK_LATEST_VERSION` (e.g. `"0.3.0"`)
  - `MCP_GAME_DECK_RELEASE_URL` (full URL to the release page)
- **The Tauri app**, on boot, if `MCP_GAME_DECK_UPDATE_AVAILABLE == "true"`, displays a persistent banner at the top of the window: "Update available: v0.3.0 [View release]".
- This way: the user who only uses Cursor + MCP server (never opens Tauri) still gets the update signal via the pin badge in Unity. The user who lives in the Tauri app gets the banner there. Single source of truth (`UpdateChecker.cs`), two display surfaces.

### Env-var contract with the Tauri app (locked)

When the pin spawns the Tauri app process, it MUST pass these environment variables. The Tauri side reads them once at startup and caches the values for the app's lifetime — re-launching from a different Unity project is the only way to switch.

| Var | Value | Used by | Notes |
|-----|-------|---------|-------|
| `UNITY_PROJECT_PATH` | Unity project root (folder containing `Library/`, `Assets/`, `ProjectSettings/`) | `unity_client::load_auth_token()` reads `<path>/Library/GameDeck/auth-token` | Already consumed by Feature 01 task 4.2. |
| `UNITY_MCP_HOST` | `127.0.0.1` (or whatever the user's Project Settings has) | `unity_client::connection` to override the default loopback | Optional. |
| `UNITY_MCP_PORT` | port number from Project Settings (default `8090`) | `unity_client::connection` | Optional. |
| `MCP_GAME_DECK_UPDATE_AVAILABLE` | `"true"` / `"false"` | Tauri app banner | New in Feature 07. |
| `MCP_GAME_DECK_LATEST_VERSION` | semver string (e.g. `"0.3.0"`) | Tauri app banner | New in Feature 07. Only meaningful if `MCP_GAME_DECK_UPDATE_AVAILABLE=true`. |
| `MCP_GAME_DECK_RELEASE_URL` | full URL to the GitHub release page | Tauri app banner "View release" link | New in Feature 07. |

Reference: the legacy in-Unity `Editor/ChatUI/ServerProcessManager.cs` already follows this exact pattern (passing `PROJECT_CWD`, `MCP_SERVER_URL`, `MODEL` etc as env vars to a child Node process). Feature 07 generalizes the pattern to the Tauri spawn.

**Manual override during dev:** before Feature 07 ships, developers run Tauri standalone and must set `UNITY_PROJECT_PATH` themselves (e.g. via `[Environment]::SetEnvironmentVariable(...)` on Windows). The pin removes this friction for end users.

---

## Locked decision #2 — Cleanup scope (what gets deleted, modified, kept)

**Decided:** April 2026 — full audit of `Editor/ChatUI/` and `Server~/src/` performed.

### 🗑️ Deleted entirely (Editor/ChatUI/)

The whole `Editor/ChatUI/` folder goes away. Specifically:

- `ChatWindow.cs` + 6 partials (Attachments, Commands, Connection, Helpers, Messages, Sessions, Setup)
- `ChatWindow.uxml`, `ChatWindow.uss`, `SetupScreen.uxml`
- `MessageRenderer.cs`
- `ServerProcessManager.cs` — Tauri's `node_supervisor` (Feature 01) handles process management now
- `WebSocketClient.cs` — Tauri uses stdio+JSON-RPC, not WebSocket
- `ChatConstants.cs` — most constants become dead. Any genuinely shared constants migrate to a new home before deletion (likely `Editor/Settings/` or a new `Editor/Pin/` folder).

### 🔧 Modified, kept

- **`Editor/Utils/UpdateChecker.cs`:** remove `LogUpdateAvailable()` body (kill the `Debug.Log`). Keep EditorPrefs population. Pin will read these prefs.
- **`Editor/Settings/GameDeckSettingsProvider.cs`:** remove the `if (Utils.UpdateChecker.IsUpdateAvailable) { ... }` block (lines ~92-103). Keep the rest (host/port/timeout/model config still valid).
- **`Editor/Settings/GameDeckSettings.cs`:** audit fields when implementing — anything chat-UI-only (`_defaultModel` if it was just for the dropdown) gets removed. Network config stays.

### ✅ Untouched (intentional)

- `Editor/MCP/` — C# MCP Server, the engine. Zero changes.
- `Editor/Tools/` — 268 tools, all intact.
- `Editor/Resources/` — audit on implementation; remove only icons/assets that ChatUI was the sole consumer of.
- `Editor/Prompts/` — prompts continue.
- `Server~/src/agents.ts` — agent loader, useful for Feature 02.
- `Server~/src/sessions.ts` — session persistence, useful for Feature 02.
- `Server~/src/system-prompt.ts` — keeps.
- `Server~/src/mcp-proxy.ts` — stdio→HTTP MCP proxy, keeps.
- `Agents~/`, `Skills~/`, `KnowledgeBase~/`, `Prompts/` — all kept.

### 🟡 Defer to Feature 02 (don't touch in F07)

- `Server~/src/index.ts` — currently a WebSocket server. Feature 02 will rewrite this to be a stdio+JSON-RPC server compatible with Tauri's `node_supervisor`. F07 leaves it alone.
- `Server~/src/messages.ts` — defines WebSocket protocol types. F02 redesigns these as JSON-RPC types.
- `Server~/src/config.ts` — the `port` field becomes obsolete (stdio has no port) but rest stays. F02 cleans up.
- `Server~/src/constants.ts` — `DEFAULT_AGENT_PORT = 9100` becomes obsolete. F02 cleans up.

### Cleanup ordering (within Feature 07 task breakdown)

The cleanup happens **at the END** of Feature 07 — after the pin is built, the launch flow is validated, and an end-to-end test passes. Reasoning: while the pin is being built, the legacy `ChatUI` is the only fallback for manually verifying the MCP server is alive. Deleting early creates a no-fallback gap.

Suggested task ordering:

1. Build the pin (UI mínima, polling, color states, right-click menu).
2. Build the launch logic (download binary, SHA256 verify, env var injection, spawn Tauri).
3. End-to-end smoke test — click pin → Tauri opens connected → echo round-trip works.
4. **Only after 1–3 work:** delete `Editor/ChatUI/`, strip `UpdateChecker`, strip Settings banner.
5. Verify Unity compiles clean — no orphan references.

---

## Locked decision #3 — Single-instance detection (focus existing vs spawn new)

**Decided:** April 2026.

### The behavior

- **First click:** pin checks if Tauri is already running for the current Unity project. Not running → downloads binary if needed, spawns new instance.
- **Subsequent click:** Tauri already running → focuses the existing window. Does not spawn a second instance.
- **Crash recovery:** if the running Tauri died unexpectedly (e.g. user killed it via Task Manager), the next pin click detects the stale state and spawns a fresh instance.

### How it's implemented — `tauri-plugin-single-instance`

The Tauri side uses the official `tauri-plugin-single-instance` plugin. When a second Tauri process tries to start with the same instance ID, the plugin:

1. Detects the existing instance via OS-level lock (named pipe on Windows, Unix socket on macOS/Linux).
2. Kills its own process (the second one) early in `setup()`.
3. Notifies the first instance via a callback, which calls `window.set_focus()` on the main window.

Net effect: from the pin's perspective, it can simply **always call spawn**. If an instance is already running, the new process self-terminates and the old one comes to the front. No process scanning, no PID tracking from the C# side.

### Why this approach

- **Cross-platform built-in.** Works on Windows, macOS, and Linux without OS-specific code.
- **Maintained by Tauri team.** Follows their conventions and stays compatible with future Tauri versions.
- **Multi-project compatible.** The instance ID is computed from `UNITY_PROJECT_PATH`, so each Unity project has its own lock space. Two Unity projects open simultaneously each get their own Tauri.
- **Crash-resistant.** OS-level locks are released when a process dies, so a crashed Tauri doesn't block a new launch.
- **Simpler pin logic.** Pin just spawns; the binary handles the dedup itself. No process scanning, no `Process.GetProcessesByName` calls (which are slow, OS-specific, and unreliable under sandboxing/AV).

### Why NOT the alternatives

- **Process scanning:** OS-specific (different APIs per platform), can be blocked by antivirus, fragile under sandbox, and can't distinguish multiple Unity projects (both running same exe name).
- **MCP-server-based detection (asking Unity if a client is connected):** doesn't distinguish Tauri from other MCP clients (Cursor, Cline). Also requires Unity to push state to the pin, which means polling — already what the pin does for status, but it doesn't tell you "is THIS app open" vs "is SOME app open".

### Implementation details

**Tauri side (Rust, in `src-tauri/src/lib.rs`):**

```rust
use tauri_plugin_single_instance::init as single_instance;

tauri::Builder::default()
    .plugin(single_instance(|app, _args, _cwd| {
        // A second instance tried to launch — focus the existing window.
        if let Some(win) = app.get_webview_window("main") {
            let _ = win.set_focus();
            let _ = win.unminimize();
        }
    }))
    // ...rest of builder
```

**Pin side (C#):** no special logic. Pin just calls `Process.Start()` with the binary path and env vars. If an instance is running, the new process self-exits; if not, it stays and runs.

**Multi-project instance ID:**

The plugin uses a default instance ID based on the app identifier (`com.mcpgamedeck.app` from `tauri.conf.json`). To make it per-project, the Tauri app at startup hashes `UNITY_PROJECT_PATH` and uses that hash as the instance ID:

```rust
let project_path = std::env::var("UNITY_PROJECT_PATH").unwrap_or_default();
let project_hash = compute_short_hash(&project_path);
let instance_id = format!("com.mcpgamedeck.app.{}", project_hash);
```

Then the plugin is initialized with this dynamic ID. Two Unity projects → two different IDs → both Tauri instances can coexist, each focusing only when its matching project's pin is clicked.

### What "focus" looks like

When the plugin callback fires:

- If the window is minimized → unminimize.
- If the window is hidden → show.
- Always → bring to foreground (`set_focus()`).
- Optional polish (deferred): flash the taskbar icon briefly so user sees something happened.

### Risks

| Risk | Mitigation |
|------|------------|
| `tauri-plugin-single-instance` behavior changes between Tauri 2.x minor versions | Pin Tauri version in `Cargo.toml` (already done as `tauri = "2"`); audit on each upgrade. |
| Hash collision between two Unity project paths | Use SHA-256 truncated to 12 chars — collision space is 2^48, overwhelmingly safe for two paths on one machine. |
| User has same project at two different paths (e.g. via symlink or copy) | Both get separate instances; mild surprise but not a bug. Documented behavior. |

---

## Locked decision #4 — Multi-project handling (MCP port collisions)

**Decided:** April 2026.

### The scenario

User has 2 Unity projects open simultaneously. Each Unity tries to bind its C# MCP Server to the configured port (default `8090`). The second one to start gets `EADDRINUSE` and the server fails to bind.

Already handled by decision #3: each Unity project's Tauri app is a separate instance (project-scoped instance ID). So the **app side** is fine. The problem is purely on the **MCP server side** — only one of the two Unity projects can use port `8090`.

### What we do — manual port config + clear tooltip

1. **No automatic port-fallback in the C# Server.** The server keeps trying `_mcpPort` from `GameDeckSettings` and fails loudly if the port is taken. (Touching the C# Server is out of scope for Feature 07 per decision #2.)
2. **The pin detects the bind failure** and turns red.
3. **The pin's tooltip explains the cause:**
   > "MCP Server failed to bind on port 8090. Another process (likely another Unity project) is using it. Open Project Settings → Game Deck → MCP Server Port and change to a free port (e.g. 8091)."
4. **No dialog popup** — just tooltip + red color. Power-user friendly, no nag.

### Why this approach

- **Zero changes to the C# MCP Server.** Decision #2 keeps `Editor/MCP/` untouched. Auto-fallback would require changes there.
- **Multi-project is a rare case for Unity devs.** Most workflows are one project at a time. Building automatic recovery for a 5%-of-users scenario isn't worth the complexity.
- **Solution is discoverable.** User who hits this once learns it, never encounters again.
- **Project Settings already exposes the port** (`GameDeckSettingsProvider` has the `MCP Server Port` field). No new UI needed.

### How the pin detects the bind failure

When the C# MCP Server fails to bind, it logs an error to the Unity Console (existing behavior) and the `TcpListener` doesn't enter "listening" state. Pin's polling logic already checks "is the TCP socket bound on `_mcpPort`?" — if not, status goes red.

The pin **distinguishes "port collision" from "server not started yet"** by checking:

- Is the configured port currently bound by **any process** (e.g. via `TcpClient.ConnectAsync` to `localhost:port`)? If yes but C# Server log shows bind failure → another process owns the port → tooltip explains collision.
- If no → server hasn't started or is initializing → standard "starting" / "not running" message.

### Implementation note

This logic lives in the pin's polling code (Feature 07 scope). It does NOT modify the C# MCP Server. A small helper in pin reads the latest log entries from the Unity Console (or listens to `Application.logMessageReceived`) to catch bind-failure messages and surface them in the tooltip.

### Future work (deferred)

If multi-project becomes common feedback, v2.x can add automatic port-fallback in the C# Server (`8090` → try `8091` → `8092`...) and write the resolved port to `Library/GameDeck/runtime-port.txt`. Pin reads the runtime port from that file. But not for v2.0.

---

## Locked decision #5 — Pin UI placement and styling

**Decided:** April 2026. **Revised:** April 2026 (after task 1.2 validation revealed UX issue with Toolbar Overlay placement).

### Placement — global Editor toolbar (left slot, via reflection)

- **Where:** the global Editor toolbar at the very top of the Unity window, on the **left side**, immediately after the existing project picker / cloud account dropdowns (e.g. between `Asset Store ▾` and the next dropdown, or at the rightmost end of the left cluster).
- **Why this slot:** always visible regardless of which view (Scene / Game / Console) the user is in. Matches the placement convention established by Benzi.ai, ParrelSync, and similar Editor tooling — users already expect status pins to live there.
- **Implementation:** reflection into `UnityEditor.Toolbar` (internal class) to inject a custom `OnGUI` handler into the left toolbar zone. Pattern is well-established in the Asset Store ecosystem; multiple OSS implementations exist as reference.
- **Why NOT Toolbar Overlay (`[Overlay]` + `IconToolbarOverlay`):** initially chosen for being the documented Unity 6 API, but in practice Toolbar Overlays only appear inside the Scene view panel and require manual user activation per project. UX is wrong for a always-on status indicator — user shouldn't have to open Scene view and toggle a checkbox just to see whether the app is connected.
- **Trade-off accepted:** reflection into internal Unity APIs is not officially supported and may break across major Unity versions (e.g. Unity 7). Mitigation:
  - Wrap reflection in a try/catch that logs (once) and gracefully no-ops if the internal layout shifts.
  - Test on each Unity major release before bumping the package's `unity` version field.
  - The pin's functionality (status, launch, menu) does not depend on the toolbar mount working — if reflection fails, the pin is invisible but the C# MCP Server, the Tauri app, and Project Settings continue to work.
  - Document the `unity` field constraint in the package manifest (currently `6000.3+`).
- **Drag-to-reposition:** not supported. The pin sits at a fixed slot. Acceptable trade-off for the gain of being always visible.

### Visual — icon + status dot + tooltip (unchanged)

- **Icon:** small product icon (~16×16 px) on the left of the pin. Smaller than originally specified (was 20×20) because the global toolbar slot is tighter — Unity's other items are typically 16 px.
- **Status dot:** ~6×6 px colored circle in the bottom-right corner of the icon (overlay).
  - 🟢 green `#22c55e` — connected and ready
  - 🟡 yellow `#eab308` — Unity busy (compiling, entering play mode, asset import)
  - 🔴 red `#ef4444` — app not running OR MCP server bind failure
  - ⚫ gray `#6b7280` — no app installed (first run)
- **Update badge:** when `UpdateChecker.IsUpdateAvailable == true`, the pin draws a tiny blue dot (~4×4 px) in the **top-right** corner of the icon (so it doesn't collide with the status dot in the bottom-right). Subtle, no animation.
- **No text label.** Recognition is via icon + color. Saves toolbar real estate — the global toolbar is already crowded.
- **Total pin footprint:** ~20×20 px including padding (was 24×24 in the Toolbar Overlay version).

### Tooltip text per state

Hover shows tooltip explaining current state and (when relevant) what to do:

| State | Tooltip |
|-------|---------|
| 🟢 connected | `MCP Game Deck connected. Click to open chat.` |
| 🟡 busy | `Unity is busy ({reason: compiling / entering play mode / importing assets}). App still connected.` |
| 🔴 not running | `MCP Game Deck app is not running. Click to launch.` |
| 🔴 bind failure (port collision) | `MCP Server failed to bind on port {port}. Another process is using it. Open Project Settings → Game Deck → MCP Server Port and change to a free port (e.g. {port+1}).` |
| 🔴 download failure | `Could not download MCP Game Deck app. Check your internet connection. Right-click → Manual Install for fallback.` |
| ⚫ first run | `First time? Click to install MCP Game Deck app (~9 MB download).` |
| update badge present (any state) | Tooltip appended: `\n\nUpdate available: v{version}. View release in app or in Settings.` |

### Icon — placeholder for now, real one comes from Feature 09

- **Feature 07 ships with a placeholder.** A simple monochrome generic icon (e.g. a stylized "MCP" wordmark, or a generic chat-bubble glyph) — anything recognizable but not the final brand.
- **Stored as a Unity Editor resource** under `Editor/Resources/pin-icon-placeholder.png` (and dark-mode variant if needed) — loaded via `UnityEngine.Resources.Load<Texture2D>("pin-icon-placeholder")`.
- **The Tauri app icon (`src-tauri/icons/`) is also a placeholder** for now (Feature 01 generated it from a default template).
- **Both real icons land in Feature 09 (Claude Design hand-off):**
  - Feature 09 produces the brand assets (logo, color palette, typography).
  - Final pin icon replaces `pin-icon-placeholder.png`.
  - Final Tauri app icon set replaces the contents of `App~/src-tauri/icons/`.
  - Both are simple file swaps — no code changes needed if the placeholder code reads from the same paths.

### Why these choices

- **Global toolbar over Scene-view overlay:** always visible, matches user expectations from similar Editor tools, no per-project activation friction.
- **Reflection trade-off accepted:** the alternative (Toolbar Overlay) gave wrong UX. Reflection is fragile but well-trodden — multiple production tools rely on the same pattern across Unity versions.
- **Icon + status dot over plain colored dot:** brand recognition (when icon lands in F09) + status at a glance. Works equally well in Unity's light and dark Editor themes.
- **No text label:** the global toolbar is tight on space; icon + color is sufficient given the tooltip.
- **Update badge in opposite corner from status dot:** prevents visual collision, keeps both signals readable simultaneously.
- **Placeholder icon now, real icon in F09:** unblocks Feature 07 work without waiting on design, and avoids a half-finished brand. Swap is trivial later.

### Risks

| Risk | Mitigation |
|------|------------|
| Unity bumps a major version and breaks the reflection mount | Wrap reflection in try/catch; pin no-ops gracefully. Audit on each Unity major before updating the package's `unity` field. |
| Status dot too small to see on high-DPI displays | Use `EditorGUIUtility.pixelsPerPoint` to scale dot size. Test on 4K monitors. |
| Placeholder icon ships in v2.0 release if F09 isn't ready | Acceptable. Document in release notes that icon is a placeholder; brand lands in a follow-up patch. |
| Update badge confusable with status dot | Different corner (top-right vs bottom-right) and different color (blue vs status color). Unique enough. |
| Toolbar reflection conflicts with another package using the same hook | Defensive subscription (idempotent unsubscribe-then-subscribe), check for existing children before injecting. |

### Reference implementations to study

- ParrelSync's status indicator (open-source Asset Store package)
- Benzi.ai's pin (proprietary but visible in their public videos)
- Several free "Toolbar Extender" snippets on GitHub Gist that show the reflection path — use them as reference for which internal Unity classes / fields to look up. Do NOT vendor them; write fresh code that documents what each reflected member is for.

---

## Locked decision #6 — Right-click menu items

**Decided:** April 2026.

### The menu — 4 items

```
[ Right-click on pin ]
├── Settings
├── ─────────────
├── Copy MCP Server URL
├── Show install folder
├── ─────────────
└── About
```

### Item 1 — Settings

- **Label:** `Settings`
- **Action:** launches (or focuses) the Tauri app and lands directly on the Settings tab — bypasses the default Chat tab.
- **Implementation:** pin spawns the binary with an extra arg `--route=/settings`. The Tauri app (in `App~/src/main.tsx`) parses CLI args via Tauri's `getMatches()` API and uses the `route` value as `MemoryRouter`'s `initialEntries`.
- **Behavior when app is already running:** the single-instance plugin callback receives the args from the new spawn attempt; the running instance reads `--route` and navigates to that tab before focusing.
- **Why useful:** quick path to status / config without going through Chat first.

### Item 2 — Copy MCP Server URL

- **Label:** `Copy MCP Server URL`
- **Action:** copies the full server URL (e.g. `http://127.0.0.1:8090`) to the system clipboard.
- **Implementation:** C# uses `EditorGUIUtility.systemCopyBuffer = $"http://{host}:{port}"`, reading host + port from `GameDeckSettings`.
- **Feedback:** brief tooltip / status bar message: "MCP Server URL copied".
- **Why useful:** the C# MCP Server stays open to any MCP-compatible client (Cursor, Cline, Claude Desktop, etc). Power users want to plug it into other tools — this is the one-click way to grab the URL. Reinforces the "MCP server is universal, Tauri app is one client" message.

### Item 3 — Show install folder

- **Label:** `Show install folder`
- **Action:** opens `%APPDATA%\MCPGameDeck\` in the OS file explorer (Windows Explorer / Finder / xdg-open).
- **Implementation:** C# uses `EditorUtility.RevealInFinder(installFolderPath)` (cross-platform).
- **Behavior when folder doesn't exist yet** (first run never happened): fall back to opening the parent `%APPDATA%`. Or create the folder empty and reveal — TBD on implementation, low impact.
- **Why useful:** debug aid. If the binary won't launch or download is suspected corrupt, user can inspect the folder, delete it, retry. Also useful for support (user can zip the folder and send it for diagnosis).

### Item 4 — About

- **Label:** `About`
- **Action:** shows a Unity Editor dialog (`EditorUtility.DisplayDialog` or a small `EditorWindow` if richer formatting is needed) with version info.
- **Content:**
  - Package version: `v{PackageInfo.version}` (read at runtime)
  - App version: `v{installed binary version, or "not installed"}` (checks if `%APPDATA%\MCPGameDeck\bin\<version>\` exists)
  - Update status: `Up to date` / `Update available: v{latest}` (from `UpdateChecker.LatestVersion`)
  - Link button: `View on GitHub` → `https://github.com/RamonBedin/mcp-game-deck`
- **Why useful:** quick sanity check ("am I on the right version?"), discoverable link to the repo, doesn't require the Tauri app to be running.
- **Why C# dialog and not a Tauri route:** the About dialog should work even if Tauri can't launch (e.g. download failed, OS issue). Keeping it native to Unity avoids that dependency.

### Items deliberately NOT in the menu

- **`Open Chat`** — left-click already does this. Adding it as a menu item duplicates the primary action.
- **`Restart Server`** — ambiguous (which of the three servers? C# MCP / Node Agent SDK / Tauri itself?). The Tauri Settings tab already exposes a "Restart Node SDK" button (Feature 01 task 3.3); the C# MCP Server is owned by Unity's lifecycle (assembly reload restarts it).
- **`Force re-download app`** — useful for debugging corrupt downloads but rare for end users. Could be added in v2.x if support tickets reveal need. For now, "Show install folder" + manual delete is the workaround.
- **`Disable pin`** — Toolbar Overlays already support hiding via Unity's overlay menu (right-click empty toolbar area → toggle). Duplicating this in the pin's own right-click menu is redundant.

### Why this menu shape

- **Minimal.** Four items only, two separators. No nested submenus.
- **No duplication with the Tauri app's own UI.** The Tauri app handles its own runtime concerns (chat, plans, rules, restart Node SDK). The pin's menu handles concerns that exist *outside* the app: copying the URL for external use, opening the install folder for debugging, showing version info that works even when the app is down.
- **Discoverable via right-click muscle memory.** Power users will find it; casual users will use the left-click and never see it.

### Risks

| Risk | Mitigation |
|------|------------|
| Tauri's CLI args parsing is finicky on macOS `.app` bundles | Test the `--route=/settings` spawn on macOS during F07 implementation. Fallback: set an env var (`MCP_GAME_DECK_INITIAL_ROUTE`) which the app reads at boot. |
| Clipboard copy silently fails in headless Unity (e.g. CI) | Pin's right-click menu is Editor-only; CI doesn't open the Editor toolbar. Not a real-world risk. |
| `EditorUtility.RevealInFinder` opens wrong folder on edge cases (UNC paths, mapped drives) | Test on Windows with `%APPDATA%` resolved through different paths. Fallback: open the parent if specific folder fails. |

---

## Locked decision #7 — Tauri app behavior across Unity restarts

**Decided:** April 2026.

### Sub-cenários considerados

**A) User restarts the same Unity project.** Tauri's `unity_client` heartbeat (Feature 01 task 4.1) detects MCP server back online within ~30 s; status flips from `disconnected` to `connected`. Already works as built. Nothing to add.

**B) User closes Unity and opens a *different* Unity project, leaving the Tauri app from the original project still running.** Two outcomes are now possible:

- The original Tauri keeps polling its now-gone MCP server and stays in `disconnected`.
- When the new Unity's pin is clicked, the new Tauri spawn gets a different instance ID (decision #3 — hash of `UNITY_PROJECT_PATH`), so it launches as a fresh process. Two Tauri windows now coexist: one alive (new project), one zombie (old project, cached env vars are stale).

**C) User does Unity → File → Open Project (Editor restarts in place).** The C# MCP server of the new project rebinds the same port. The old Tauri's heartbeat reconnects, but the cached auth token is from the previous project — every tool call returns 401. The Tauri app shows its own error path (auth failed). Already covered by Feature 01 design; no new logic needed in F07.

### What we do — leave the orphan Tauri alone

For sub-cenário **B** specifically: the orphan Tauri **stays running until the user closes it manually**. No timeout-and-self-close. No "Unity ausente — fechar?" prompt.

### Why not auto-close

- **Surprise closes are worse than visible orphans.** A user might intentionally leave Tauri open for several minutes (long Unity build, switching machines, AFK, comparing notes). Closing on a timer breaks legitimate workflows for an edge case.
- **Resource cost is negligible.** A Tauri instance with a disconnected MCP heartbeat is ~30–80 MB RAM and ~0% CPU. Not worth optimizing for.
- **Visible orphans are self-documenting.** The orphan window's red `disconnected` status tells the user immediately: "this is an old session, close it." A close-on-timer would silently disappear something the user might still want to look at.
- **YAGNI.** No real-world signal yet that this matters. If users complain that orphans clutter their taskbar, v2.x can revisit (timer with a confirmation dialog, or "Close on Unity exit" toggle in Settings).

### Documentation

The release notes / docs explicitly call this out:

> "If you switch between Unity projects, close the previous MCP Game Deck app window manually. Each Unity project gets its own app instance."

Pin tooltip on the orphan-side Tauri stays informative: `🔴 disconnected — MCP Server not reachable on port {port}.` Already accurate and actionable from decisions #4–#5.

### Edge case explicitly accepted

When two Tauri windows are open (one live, one orphan from a previous Unity session) and both pins exist (one per Unity project): each pin focuses its own Tauri thanks to project-scoped instance IDs (decision #3). User clicking an orphan project's pin (if its Unity is gone) won't focus anything — the pin would be in the now-closed Unity. Not a real-world flow.

### Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Users accumulate many orphan Tauri windows over weeks | low | Each restart of the OS clears them. If user complains, v2.x adds a "Close on Unity exit" toggle. |
| Orphan window stays auth-stale after sub-cenário C (same port, different project) | medium | Tauri's existing 401 handling (Feature 01 task 4.2) surfaces clear error. User restarts Tauri via window-close → pin click. Documented in release notes. |
| User confused why one Tauri shows old project's data | low | Tab labels and Settings tab show project path. Recognizable. |

---

## Cost estimate

**Small.** (Refined after locking decisions #1–#7.)

- Toolbar widget UI (Toolbar Overlay + icon + status dot + update badge): ~3 days
- Polling logic + state machine for color (including port-collision detection): ~2.5 days
- Process launch with env var injection: ~2 days
- Right-click menu (4 items: Settings / Copy URL / Show folder / About): ~1.5 days
- CLI route argument support in Tauri (`--route=/settings`): ~0.5 day
- Binary download flow (HTTP fetch, SHA256, retry, error dialogs): ~3 days
- Tauri-side single-instance integration (plugin setup + project-scoped instance ID): ~1 day
- Cleanup (delete ChatUI, strip Settings/UpdateChecker, verify compile): ~2 days

Total: ~2 weeks.

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Toolbar overlay disappears after Unity update | medium | Stick to documented APIs (UIElements toolbar). Test on Unity 6000.x major versions. |
| Pin polling causes Unity perf hit | low | Poll at 1-2Hz, very cheap operation (TCP socket existence check) |
| Binary download fails silently | high | Surface clear error in pin menu + dialog with manual download URL. Log to console. |
| User confused by why chat panel disappeared after upgrading | high | Release notes + first-run prompt: "MCP Game Deck v2.0 moved chat to a desktop app. Click the pin to open it." |
| Pin state is stale (says green when app actually crashed) | medium | Shorten heartbeat timeout. App sends heartbeat to MCP server; pin reads heartbeat freshness. |
| Env var not propagated on macOS when launching `.app` bundle from Unity | medium | macOS Process API needs explicit `EnvironmentVariables` set on `ProcessStartInfo`; verify on macOS during Feature 07 implementation. |
| Cleanup deletes something Feature 02 needed | low | Cleanup task list explicitly defers index.ts, messages.ts, config.ts, constants.ts to F02. |
| GitHub Release URL convention breaks (file renamed, missing) | low | Pin shows clear error dialog with full URL — user can verify manually. CI publishes binary as part of the same workflow that creates the release tag, so naming is automated. |
| `tauri-plugin-single-instance` plugin behavior shifts on Tauri upgrade | low | Pin Tauri version, audit on upgrade. |
| User runs two Unity projects, second fails to bind MCP port | medium | Pin tooltip explains and points to Project Settings → Game Deck → MCP Server Port. Not auto-recovered. |
| Placeholder icon never replaced (Feature 09 slips) | low | Even ugly placeholder doesn't break functionality; release notes set expectation. |
| `--route=` CLI arg fails on a platform | low | Fallback env var (`MCP_GAME_DECK_INITIAL_ROUTE`) parsed at boot if CLI parsing isn't available. |
| Orphan Tauri windows accumulate after Unity project switches | low | Documented behavior. User closes manually. v2.x revisits if it's a real complaint. |

## Milestone

v2.0.

## Notes

- Keep the pin minimal. Resist the temptation to add inline chat in the pin tooltip or quick-action buttons. The point is to be unobtrusive when not needed and one click away when needed.
- Reference Benzi.ai's pin for visual inspiration. Don't copy pixel-for-pixel; we have our own brand (designed in Feature 09).
- The v2.0 architecture (Tauri as separate process) inherently fixes the v1.x bug where the in-Unity ChatUI didn't notice MCP server crashes. Tauri's heartbeat (5s, see Feature 01 task 4.1) flips status to disconnected within 10s of any failure. No special handling needed in Feature 07 for this.
