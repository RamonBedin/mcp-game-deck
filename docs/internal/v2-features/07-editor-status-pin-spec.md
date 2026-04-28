# Feature 07 — Editor Status Pin — Spec

> **Status:** `agreed` — design decisions locked April 2026 (see `07-editor-status-pin.md` for full rationale).
> **Companion:** `07-editor-status-pin-tasks.md` (decomposed work breakdown for Claude Code execution).

## What this is

A small toolbar pin inside the Unity Editor that:

1. Shows live connection status of the MCP Game Deck app (color-coded).
2. Launches (or focuses) the Tauri app on click, downloading the binary on first run.
3. Provides a right-click menu for power-user actions (Settings, Copy URL, Show folder, About).
4. Replaces the in-Unity `Editor/ChatUI/` panel — that whole folder is deleted when the pin works end-to-end.

After this feature, MCP Game Deck has a complete user-facing v2.0 workflow: install package → click pin → app downloads/opens → chat works against the live Unity project.

## Architecture overview

```
┌─────────────────────────┐       Process.Start          ┌──────────────────────────┐
│   UNITY EDITOR          │  + env vars (UNITY_PROJECT_  │   TAURI APP              │
│                         │    PATH, MCP_GAME_DECK_*)    │                          │
│  ┌──────────────────┐   │ ──────────────────────────►  │  - Existing from F01     │
│  │ Toolbar Overlay  │   │                              │  - + single-instance     │
│  │ (Pin)            │   │                              │    plugin                │
│  │  - polls TCP     │   │                              │  - + CLI --route= arg    │
│  │  - reads prefs   │   │                              │  - + update banner       │
│  │  - right-click   │   │                              │    on env var            │
│  │    menu          │   │                              │                          │
│  └──────────────────┘   │                              └──────────────────────────┘
│                         │
│  ┌──────────────────┐   │
│  │ UpdateChecker.cs │   │  (existing, stripped of log + banner)
│  │  EditorPrefs ◄──┼───┼── Pin reads prefs for update badge
│  └──────────────────┘   │
└─────────────────────────┘
```

The C# MCP Server (`Editor/MCP/`) and the rest of the Tauri app are unchanged from Feature 01.

## Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| Pin UI | Reflection mount on global Editor toolbar (`UnityEditor.Toolbar`) | Decision #5 — always-visible status indicator. Trade-off: internal API, must be tested per Unity major. |
| Pin polling | `EditorApplication.update` callback at ~2 Hz | Cheap TCP socket existence check. |
| Binary download | C# `HttpClient` (already in Unity .NET BCL) | No new dependencies. |
| Hashing | C# `System.Security.Cryptography.SHA256` | Already available. |
| Process launch | `System.Diagnostics.Process` + `ProcessStartInfo.EnvironmentVariables` | Same pattern as legacy `ServerProcessManager.cs`. |
| Tauri single-instance | `tauri-plugin-single-instance = "2"` | Adds Cargo dep. |
| Tauri CLI args | `tauri-plugin-cli = "2"` | Adds Cargo dep. Reads `--route=`. |

## File layout

**New files:**

```
Editor/Pin/
├── PinToolbarMount.cs          ← [InitializeOnLoad] reflection-injects pin into global Editor toolbar (left slot)
├── PinPolling.cs               ← state machine: status colors based on TCP / EditorPrefs
├── PinIcon.cs                  ← icon + status dot + update badge rendering (already from task 1.2)
├── PinTooltip.cs               ← tooltip text per state
├── PinContextMenu.cs           ← right-click menu (Settings / Copy URL / Show folder / About)
├── PinLauncher.cs              ← spawn Tauri with env vars + --route arg
├── PinBinaryManager.cs         ← discover / download / verify the Tauri binary
├── PinPaths.cs                 ← cross-platform path helpers (%APPDATA% / ~/.local/...)
└── PinDownloadDialog.cs        ← error dialogs for download failures

Editor/Resources/
└── pin-icon-placeholder.png   ← placeholder (real icon comes from Feature 09)
```

**Modified files:**

```
Editor/Utils/UpdateChecker.cs                    ← strip Debug.Log
Editor/Settings/GameDeckSettingsProvider.cs      ← remove update banner block
Editor/Settings/GameDeckSettings.cs              ← audit chat-only fields
App~/src-tauri/Cargo.toml                        ← add tauri-plugin-single-instance, tauri-plugin-cli
App~/src-tauri/src/lib.rs                        ← register plugins, instance ID from UNITY_PROJECT_PATH hash, route arg → MemoryRouter, update banner from env vars
App~/src-tauri/capabilities/default.json         ← grant cli + single-instance permissions
App~/src/main.tsx                                ← read --route arg, init MemoryRouter
App~/src/components/UpdateBanner.tsx (new)       ← persistent banner if MCP_GAME_DECK_UPDATE_AVAILABLE=true
App~/src/App.tsx                                 ← mount UpdateBanner above sidebar+main
```

**Deleted entirely (cleanup at end of feature):**

```
Editor/ChatUI/                  ← whole folder
  ├── ChatWindow.cs (+ 6 partials)
  ├── ChatWindow.uxml, ChatWindow.uss, SetupScreen.uxml
  ├── MessageRenderer.cs
  ├── ServerProcessManager.cs
  ├── WebSocketClient.cs
  └── ChatConstants.cs
```

## Pin state machine

Status colors driven by C# polling (~2 Hz) of three signals:

| Signal | How read | Used for |
|--------|----------|----------|
| MCP Server bound? | `TcpClient.ConnectAsync(host, port)` with 200 ms timeout | yellow vs red |
| Unity busy? | `EditorApplication.isCompiling`, `EditorApplication.isPlayingOrWillChangePlaymode` | yellow override |
| App installed? | File exists at `<install-folder>/<version>/mcp-game-deck-app.exe` | gray vs red on disconnected |
| Update available? | `EditorPrefs.GetBool("MCPGameDeck.UpdateAvailable")` | blue badge top-right |

State priority (highest wins):

1. **Yellow** — `EditorApplication.isCompiling` OR `EditorApplication.isPlayingOrWillChangePlaymode` (and MCP is connected)
2. **Green** — TCP connect to `localhost:{port}` succeeds
3. **Red (port collision)** — TCP connect succeeds but C# Server log shows bind failure (read via `Application.logMessageReceived` listener)
4. **Red (not running)** — TCP connect fails AND binary exists on disk
5. **Gray** — TCP connect fails AND binary doesn't exist on disk

Tooltip text per state defined in design doc (decision #5).

## Binary distribution

**Convention-based URL** — no manifest file:

```
https://github.com/RamonBedin/mcp-game-deck/releases/download/v{version}/mcp-game-deck-app-{version}.exe
https://github.com/RamonBedin/mcp-game-deck/releases/download/v{version}/mcp-game-deck-app-{version}.exe.sha256
```

**Local install:**

```
Windows: %APPDATA%\MCPGameDeck\bin\<version>\mcp-game-deck-app.exe
macOS:   ~/Library/Application Support/MCPGameDeck/bin/<version>/mcp-game-deck-app
Linux:   ~/.local/share/MCPGameDeck/bin/<version>/mcp-game-deck-app
```

**Version source:** `PackageInfo.FindForAssembly(typeof(PinOverlay).Assembly).version`. Pin uses package version verbatim — no parsing, no compatibility matrix.

**Download flow on click when binary missing:**

1. Pin shows progress overlay ("Downloading MCP Game Deck app... 4.2 MB / 9.0 MB")
2. `HttpClient.GetAsync` to the convention URL (with `IfModifiedSince` for caching).
3. `HttpClient.GetAsync` to `.sha256` URL.
4. Compute local SHA256 of downloaded file, compare.
5. On match: write to install path, launch.
6. On mismatch / network error: open error dialog (decision #1).

**Concurrent click protection:** if a download is already in progress, second click does nothing (overlay already visible).

## Env-var contract on launch

Pin always passes these env vars when calling `Process.Start()`:

| Var | Required? | Source |
|-----|-----------|--------|
| `UNITY_PROJECT_PATH` | always | `Application.dataPath` parent |
| `UNITY_MCP_HOST` | always | `GameDeckSettings._host` |
| `UNITY_MCP_PORT` | always | `GameDeckSettings._mcpPort` |
| `MCP_GAME_DECK_UPDATE_AVAILABLE` | always | `EditorPrefs.GetBool("MCPGameDeck.UpdateAvailable") ? "true" : "false"` |
| `MCP_GAME_DECK_LATEST_VERSION` | when above is true | `EditorPrefs.GetString("MCPGameDeck.LatestVersion")` |
| `MCP_GAME_DECK_RELEASE_URL` | when above is true | `EditorPrefs.GetString("MCPGameDeck.ReleaseUrl")` |

Pin also passes `--route={path}` CLI arg when launched from the right-click "Settings" menu item (`--route=/settings`).

## Tauri side changes (App~)

### Single-instance with project-scoped ID

`App~/src-tauri/src/lib.rs` initializes `tauri-plugin-single-instance` with an instance ID computed at runtime from `UNITY_PROJECT_PATH`:

```rust
fn compute_instance_id() -> String {
    let path = std::env::var("UNITY_PROJECT_PATH").unwrap_or_default();
    let mut hasher = sha2::Sha256::new();
    hasher.update(path.as_bytes());
    let hash = hex::encode(hasher.finalize());
    format!("com.mcpgamedeck.app.{}", &hash[..12])
}
```

When a second instance tries to launch with the same ID, the plugin's callback fires on the running instance:

- Receives the new `args` (which may include `--route=/settings`)
- Calls `window.set_focus()` + `window.unminimize()`
- If `--route=` present, sends a Tauri event `route-requested` that the React side handles by navigating

### CLI route argument

`App~/src/main.tsx` parses `--route=` via Tauri's `getMatches()` API:

```typescript
import { getMatches } from "@tauri-apps/plugin-cli";

const matches = await getMatches();
const initialRoute =
  (matches.args.route?.value as string | undefined) ?? "/chat";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <MemoryRouter initialEntries={[initialRoute]}>
    {/* ... */}
  </MemoryRouter>,
);
```

Plus a runtime listener for `route-requested` events from the single-instance callback.

### Update banner

`App~/src/components/UpdateBanner.tsx` — persistent strip at the top of the window, shown only when env vars indicate an update is available:

```tsx
const showBanner = import.meta.env.MCP_GAME_DECK_UPDATE_AVAILABLE === "true"
  // ...read from Tauri command get_env_var() that returns process.env values
```

Body: `Update available: v{version}` + button `View release` opening `MCP_GAME_DECK_RELEASE_URL` via `tauri-plugin-shell` (which is already capability-granted). Dismissable per session.

## Right-click menu

4 items. Implemented as `GenericMenu` populated in `PinContextMenu.cs`:

- **Settings** → `PinLauncher.LaunchOrFocus(route: "/settings")`
- **Copy MCP Server URL** → `EditorGUIUtility.systemCopyBuffer = $"http://{host}:{port}"` + brief HUD
- **Show install folder** → `EditorUtility.RevealInFinder(PinPaths.InstallFolder)`
- **About** → `EditorUtility.DisplayDialog(...)` with package + app version + update status + "View on GitHub" link

## Cleanup phase (last group of tasks)

Order strictly enforced — no early deletion:

1. `Editor/ChatUI/` deleted entirely (folder + all `.cs`, `.cs.meta`, `.uxml`, `.uxml.meta`, `.uss`, `.uss.meta`).
2. `Editor/Utils/UpdateChecker.cs` — `LogUpdateAvailable()` body emptied (or method removed if only called from itself). `Debug.Log` gone.
3. `Editor/Settings/GameDeckSettingsProvider.cs` — `if (Utils.UpdateChecker.IsUpdateAvailable) { ... }` block removed (~lines 92–103 in current version).
4. `Editor/Settings/GameDeckSettings.cs` — audit pass: any field used only by deleted ChatUI code is removed (likely `_defaultModel` if it was ChatUI-only; verify by Grep before deleting).
5. Verify Unity compiles cleanly. No orphan `using` statements, no missing references.
6. Verify pin still works end-to-end after cleanup.

## Definition of done for Feature 07

The feature is "done" when ALL of these hold:

1. Pin appears in Unity's toolbar overlay (default position, draggable).
2. Pin shows correct color for each state (verified manually by triggering each state).
3. Right-click menu shows the 4 items, each working as specified.
4. Click on pin (left) launches the Tauri app, downloading the binary on first run if absent.
5. Re-clicking the pin while app is running focuses the existing window (does not spawn a second instance).
6. Closing Unity and reopening with the same project: pin reconnects automatically; existing Tauri (if running) shows `disconnected` then `connected` again.
7. SHA256 verification of downloaded binary works (intentional corruption triggers re-download dialog).
8. `Editor/ChatUI/` is deleted and Unity compiles without errors.
9. `UpdateChecker.cs` no longer logs to console; `GameDeckSettingsProvider` no longer shows update banner.
10. Tauri update banner appears when env vars indicate update available, links work.
11. `pnpm tauri build` still produces a working `.msi` (no regression from Feature 01).
12. End-to-end smoke test: fresh Unity install + click pin + send chat message → echo round-trips.

## Out of scope reminders

- The C# MCP Server (`Editor/MCP/`) is **NOT** modified — port collision is surfaced via tooltip but not auto-recovered.
- `Server~/src/index.ts` migration to stdio + JSON-RPC stays in Feature 02.
- Final brand icon comes from Feature 09; placeholder ships in F07.
- No notification badges, no auto-close of orphan Tauri windows.

## Open questions deferred to implementation

1. **Exact placeholder icon design** — pick something simple at task time (suggested: monochrome chat-bubble glyph at 64×64 px, scales to 20×20).
2. **Whether `EditorUtility.RevealInFinder` works correctly with non-existent target on macOS** — fallback is to create the folder empty before reveal.
3. **Best HTTP timeout values for download on slow connections** — start with 60s, adjust if testing reveals issues.

These are noted; do not re-litigate during implementation.
