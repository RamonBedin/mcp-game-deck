# Feature 07 — Editor Status Pin — Spec

> **Status:** `agreed` — design decisions locked April 2026; spec revised 2026-04-28 to reflect actual mounting strategy (Unity 6 `[MainToolbarElement]` API, replacing the originally-planned reflection mount).
> **Companion:** `07-editor-status-pin-tasks.md` (decomposed work breakdown for Claude Code execution).

## What this is

A small toolbar pin inside the Unity Editor that:

1. Shows live connection status of the MCP Game Deck app (color-coded).
2. Launches (or focuses) the Tauri app on click, downloading the binary on first run.
3. Opens a dropdown menu with the primary action (Open Chat) plus power-user actions (Settings, Copy URL, Show folder, About) when clicked.
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
| Pin UI | Unity 6 main toolbar API (`[MainToolbarElement]` in `UnityEditor.Toolbars`) | Decision #5 — always-visible status indicator on the global Editor toolbar. Replaces the originally-planned reflection mount; no internal-API risk. |
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
├── PinToolbarElement.cs        ← [MainToolbarElement] entry; builds the VisualElement that hosts the icon
├── PinIcon.cs                  ← Color32 composite renderer: bg icon + status dot + update badge
├── PinPolling.cs               ← state machine: status from TCP probe / EditorPrefs / busy flags
├── PinTooltip.cs               ← tooltip text per state
├── PinDropdownMenu.cs          ← dropdown menu (Open Chat / Settings / Copy URL / Show folder / About)
├── PinLauncher.cs              ← spawn Tauri with env vars + --route arg
├── PinBinaryManager.cs         ← discover / download / verify the Tauri binary
├── PinPaths.cs                 ← cross-platform path helpers (%APPDATA% / ~/.local/...)
└── PinDownloadDialog.cs        ← error dialogs for download failures

Editor/Resources/
└── pin-icon-placeholder.png   ← placeholder, Read/Write enabled (real icon comes from Feature 09)
```

**Generated once and removed:**

- `Editor/Pin/PinPlaceholderIconGenerator.cs` — editor menu helper that produced the placeholder PNG; deleted after the asset was committed.

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

## Pin redraw mechanism

The Unity 6 main toolbar API is static by default — Unity invokes the
`[MainToolbarElement]`-decorated factory once at toolbar creation, and the
returned `VisualElement` lives for the rest of the editor session. There
is no built-in change-notification: a status flip in `PinPolling.CurrentStatus`
will not update the visible icon unless the pin redraws itself.

The pin therefore returns a custom `VisualElement` (not a plain
`MainToolbarButton`) that owns its own `Image` child and schedules its own
redraw via `schedule.Execute(RebuildIfChanged).Every(500)`. The callback:

1. Reads `PinPolling.CurrentStatus` and `PinPolling.UpdateAvailable`.
2. Compares against the last-rendered tuple cached on the element.
3. On change: disposes the previous `Texture2D`, calls
   `PinIcon.BuildComposite(...)`, assigns the new texture to `Image.image`,
   updates the cached tuple.
4. On no change: noop — avoids per-tick texture rebuilds.

Tooltip is updated in the same callback via `element.tooltip = PinTooltip.GetText(...)`.

Avoids `MainToolbar.Refresh()` (the global path) because it would recreate
every toolbar element on every tick — expensive and visually janky on
unrelated items (Layout dropdown, cloud account button, etc.).

The redraw mechanism is wired in task 2.2, alongside the first dynamic
state source (TCP probe). Tasks 1.1–1.3 used a one-shot `MainToolbarButton`
because the status was stub-fixed and no redraw was needed.

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

Pin also passes `--route={path}` CLI arg when launched from the dropdown's "Settings" menu item (`--route=/settings`).

## Tauri side changes (App~)

### Single-instance (app-global)

`App~/src-tauri/src/lib.rs` registers `tauri-plugin-single-instance` with default behavior — the lock space is keyed off `tauri.conf.json`'s `identifier` (`com.mcpgamedeck.app`), so a single Tauri window exists machine-wide regardless of which Unity project spawned it.

When a second invocation happens (any pin click while the window is alive), the plugin's callback fires on the running instance:

- Receives the new `args` (which may include `--route=/settings`)
- Calls `window.set_focus()` + `window.unminimize()`
- If `--route=` present, emits a Tauri `route-requested` event that the React side handles by navigating

**Known limitation — per-project isolation deferred:** the original design called for an instance ID derived from `SHA-256(UNITY_PROJECT_PATH)` so each Unity project owned its own Tauri window. The official `tauri-plugin-single-instance` v2 (in `tauri-apps/plugins-workspace`) reads the lock-space identifier directly from `app.config().identifier` and exposes no API to inject a runtime-computed ID. Implementing per-project isolation requires either forking the plugin or rolling a custom named-pipe / Unix-socket lock — both out of scope for v2.0. Workflow assumption today: users run a single Unity project at a time. With two Unity projects open simultaneously, the second pin click focuses the first project's Tauri window; the Unity connection still points at the first project (env vars are read once at Tauri startup). Per-project isolation is on the v2.1+ backlog.

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

## Pin dropdown menu

The pin is implemented as a `MainToolbarDropdown` (not `MainToolbarButton`). Clicking the pin opens a `GenericMenu` populated in `PinDropdownMenu.cs`. This single-click-to-menu UX replaced the originally-planned "left-click launches, right-click menu" design after research into the Unity 6 main toolbar API revealed that `MainToolbarButton` is a descriptor (not a `VisualElement`) and the internal `MainToolbarElement.CreateElement()` cannot be subclassed publicly — making it impossible to register a `RegisterCallback<MouseDownEvent>` or `ContextualMenuManipulator` on the toolbar entry without reflection (which decision #5 explicitly forbids).

Menu items (in order):

- **Open Chat** (primary action) → `PinLauncher.LaunchOrFocus()` (no route override → defaults to `/chat`)
- separator
- **Settings** → `PinLauncher.LaunchOrFocus(route: "/settings")`
- **Copy MCP Server URL** → `EditorGUIUtility.systemCopyBuffer = $"http://{host}:{port}"` + brief HUD
- separator
- **Show install folder** → `EditorUtility.RevealInFinder(PinPaths.InstallRoot)`
- **About** → `EditorUtility.DisplayDialog(...)` with package + app version + update status + "View on GitHub" link

The trade-off (one extra click to open the app vs. the originally-planned single-click launch) is accepted because (a) the Unity 6 toolbar API leaves no other option without reflection, (b) all actions become discoverable from one entry point instead of being hidden behind a right-click affordance, (c) it removes the need for users to know about right-click on a small toolbar element.

`MainToolbarDropdown` accepts a `Rect → void` callback that fires when the user clicks the dropdown. The callback receives the screen rect of the button and calls `menu.DropDown(rect)` to anchor the menu correctly under the pin.

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
3. Clicking the pin opens a dropdown showing 5 items (Open Chat, Settings, Copy MCP Server URL, Show install folder, About), each working as specified.
4. Selecting "Open Chat" launches the Tauri app, downloading the binary on first run if absent.
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
