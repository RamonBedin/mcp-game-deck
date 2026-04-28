# Feature 07 — Editor Status Pin — Tasks

> **Companion to:** `07-editor-status-pin-spec.md`. Read that first.
> **Execution model:** one task per Claude Code session. Ramon validates per checks below, commits via VS Code, returns to chat for next task.

## How to read this doc

- **S** = Small (~30 min – 1 h focused work). **M** = Medium (1–3 h). **L** = Large (3+ h, consider splitting if it grows).
- **Status column** updated by Ramon as tasks complete: ✅ done / 🔄 in progress / ⏳ pending.
- **Refs** point at the spec section that motivates the task.

---

## Status table

| # | Task | Size | Status | Date | Notes |
|---|------|------|--------|------|-------|
| 1.1 | Toolbar element registration via [MainToolbarElement] | M | ✅ | 2026-04-28 | Implemented via Unity 6 official API (PinToolbarElement.cs); replaces the original Toolbar Overlay and reflection-mount plans. |
| 1.2 | Pin icon + status dot rendering | S | ✅ | 2026-04-28 | Procedural Color32 rendering; PNG asset (1.3) superseded. |
| 1.3 | Placeholder icon asset in Resources | S | ✅ | 2026-04-28 | PNG generated via PinPlaceholderIconGenerator (deleted post-generation); Read/Write enabled for GetPixels32 sampling. |
| 2.1 | Polling loop wiring (Editor update tick) | S | ✅ | 2026-04-28 | PinPolling subscribes EditorApplication.update with 500ms throttle; PinToolbarElement reads CurrentStatus (stub NOT_INSTALLED → gray). |
| 2.2 | TCP probe + base state machine (gray / red / green) | M | ✅ | 2026-04-28 | TcpClient.ConnectAsync (200ms timeout); state machine: connected→CONNECTED, !connected+binary→NOT_RUNNING, !connected+!binary→NOT_INSTALLED. MainToolbar.Refresh(path) on state transition (dedup'd). |
| 2.3 | Yellow state — Unity busy detection | S | ✅ | 2026-04-28 | EditorApplication.isCompiling/isPlayingOrWillChangePlaymode/isUpdating → BUSY when MCP recently connected; gray stays gray when offline. |
| 2.4 | Port-collision detection via log listener | M | ⏳ | | |
| 2.5 | Tooltip text per state | S | ⏳ | | |
| 2.6 | Update badge (blue dot from EditorPrefs) | S | ⏳ | | |
| 3.1 | Right-click context menu — Settings + Copy URL | S | ⏳ | | |
| 3.2 | Right-click — Show install folder + About | S | ⏳ | | |
| 4.1 | Cross-platform install path helpers | S | ⏳ | | |
| 4.2 | Binary discovery (does `<path>/<version>/exe` exist?) | S | ⏳ | | |
| 4.3 | Binary download + SHA256 verification | M | ⏳ | | |
| 4.4 | Download error dialogs (network / mismatch / launch fail) | M | ⏳ | | |
| 4.5 | Process spawn with env vars + click handler wiring | M | ⏳ | | |
| 5.1 | Tauri: add single-instance plugin with project-scoped ID | M | ⏳ | | |
| 5.2 | Tauri: add CLI plugin + `--route=` parsing in main.tsx | M | ⏳ | | |
| 5.3 | Tauri: UpdateBanner component + env var read | M | ⏳ | | |
| 6.1 | End-to-end smoke test (fresh state, all paths) | M | ⏳ | | |
| 7.1 | Cleanup: delete Editor/ChatUI/ folder | S | ⏳ | | |
| 7.2 | Cleanup: strip UpdateChecker log + Settings banner | S | ⏳ | | |
| 7.3 | Cleanup: audit GameDeckSettings for dead fields | S | ⏳ | | |
| 7.4 | Final smoke test post-cleanup | S | ⏳ | | |

23 tasks total. Cleanup intentionally last — see spec section "Cleanup phase".

---

## Group 1 — Pin UI base

> Goal: pin appears in Unity, draws icon + colored dot, but does nothing functional yet (always shows gray). This unblocks visual iteration before lifecycle complexity lands.

### Task 1.1 — Toolbar element registration via [MainToolbarElement]

**Size:** M
**Refs:** spec "File layout", design decision #5 (revised)

**Context:** the original 1.1 used Unity 6's `[Overlay]` attribute to attach the pin to the Scene view. UX validation revealed this requires manual user activation and only shows inside Scene view — wrong fit for an always-on status indicator. Decision #5 was revised: pin now mounts on the **global Editor toolbar** via the official Unity 6 `[MainToolbarElement]` attribute (in `UnityEditor.Toolbars`). The previous `PinOverlay.cs` is replaced by `PinToolbarElement.cs` — no reflection into internal APIs, fully supported public surface.

**Output:**

- **Delete** `Editor/Pin/PinOverlay.cs` (and its `.meta`).
- New file `Editor/Pin/PinToolbarElement.cs`:
  - Static class. No `[InitializeOnLoad]` needed — Unity invokes the factory automatically via the attribute.
  - Constants:
    - `ELEMENT_PATH = "MCP Game Deck/Pin"` — required by `[MainToolbarElement]`.
    - `TOOLTIP = "MCP Game Deck"` — placeholder; per-state tooltip lands in 2.5.
  - Static fields for the hardcoded test values: `_testStatus = EPinStatus.CONNECTED`, `_testUpdateAvailable = false`. Real polling wiring lands in Group 2.
  - `[MainToolbarElement(ELEMENT_PATH, defaultDockPosition = MainToolbarDockPosition.Left)]`-decorated factory `CreatePin()` returning a `MainToolbarElement`:
    - Builds the icon via `PinIcon.BuildComposite(_testStatus, _testUpdateAvailable)`.
    - Wraps in a `MainToolbarContent(icon, TOOLTIP)`.
    - Returns a `MainToolbarButton(content, OnPinClicked)`.
  - Stub `OnPinClicked()` logs `[MCP] Pin clicked.` (real launch lands in task 4.5).

**Why no reflection:** Unity 6 ships `[MainToolbarElement]` (`UnityEditor.Toolbars` namespace) as the official API for pinning custom widgets to the global toolbar. No internal-API risk, no per-Unity-version maintenance, no reflection probing — the original plan to reflect into `UnityEditor.Toolbar` is obsolete.

**Validation:**

1. Compile clean. No errors in Console at startup.
2. **Pin appears in the Unity Editor's main toolbar**, at the position chosen by `defaultDockPosition`. User can drag-reposition via the toolbar overflow menu.
3. Pin shows the icon + status dot (hardcoded green from `_testStatus = CONNECTED`) + optional update badge (hardcoded false). All produced by `PinIcon.BuildComposite`.
4. Recompile (edit any C# file). Pin remains visible — Unity's `[MainToolbarElement]` machinery handles re-registration automatically.
5. Restart Unity. Pin appears as soon as the toolbar finishes initializing.
6. **No regression to other Editor toolbar items.** Project selector, cloud account, play/pause/step, Layout dropdown all still functional.

**Commit:**

```
feat(v2): register pin on Editor toolbar via [MainToolbarElement]

Replaces the Toolbar Overlay approach (Editor/Pin/PinOverlay.cs,
now deleted) with PinToolbarElement.cs that registers a custom
toolbar entry via the official Unity 6 [MainToolbarElement] attribute
(UnityEditor.Toolbars). Per decision #5 (revised): always-visible
status indicator on the global Editor toolbar — and the public API
removes the per-Unity-version maintenance cost the original
reflection plan would have carried.

CreatePin returns a MainToolbarButton with icon built by
PinIcon.BuildComposite. Hardcoded test values (_testStatus = CONNECTED,
_testUpdateAvailable = false) keep the visuals deterministic until
the polling wiring lands in Group 2.

Refs: 07-editor-status-pin-tasks.md (task 1.1, revised)
```

---

### Task 1.2 — Pin icon + status dot rendering

**Size:** S
**Refs:** spec "Pin state machine" (visual section)

**Output:**

- New file `Editor/Pin/PinIcon.cs`
- Static method `BuildComposite(EPinStatus status, bool updateAvailable)` returning a freshly-built 20×20 RGBA `Texture2D` containing:
  - Background icon (placeholder loaded once via `Resources.Load<Texture2D>("pin-icon-placeholder")`, cached statically; falls back to a low-alpha gray fill when missing)
  - Status dot bottom-right (color from `EPinStatus`)
  - Update badge top-right (small blue square) only if `updateAvailable`
- `EPinStatus` enum in same file: `CONNECTED, BUSY, NOT_RUNNING, BIND_FAILURE, NOT_INSTALLED` (SCREAMING_SNAKE_CASE per project C# conventions)
- Color values declared as `Color32` constants (faster path through Unity's pixel APIs)
- `PinToolbarElement.CreatePin()` calls `PinIcon.BuildComposite(_testStatus, _testUpdateAvailable)` and feeds the result into `MainToolbarContent`

**Validation:**

1. Compile clean.
2. Pin in the Editor toolbar shows the placeholder icon + a colored dot. Hardcode `_testStatus = EPinStatus.CONNECTED` to verify green; change to `BIND_FAILURE` to verify red; etc.
3. Toggle `_testUpdateAvailable = true` → blue badge visible top-right; `false` → badge gone.
4. Status dot is in bottom-right, update badge in top-right — they don't overlap.

**Commit:**

```
feat(v2): pin icon + status dot rendering

PinIcon.BuildComposite produces a 20x20 Texture2D combining the
placeholder background icon, a colored status dot (bottom-right)
and an optional blue update badge (top-right). EPinStatus enum
covers all five states. Hardcoded test status in PinToolbarElement
confirms all colors render. Real status wiring lands in Group 2.

Refs: 07-editor-status-pin-tasks.md (task 1.2)
```

---

### Task 1.3 — Placeholder icon asset

**Size:** S
**Refs:** spec "Open questions deferred — exact placeholder icon design"

**Output:**

- New file `Editor/Resources/pin-icon-placeholder.png` (64×64 px, monochrome, transparent background — placeholder until Feature 09 ships the real brand icon)
- `.png.meta` configured via `TextureImporter`: `textureType = GUI`, `npotScale = None`, `alphaIsTransparency = true`, `mipmapEnabled = false`, `isReadable = true` (required because `PinIcon.DrawBackground` samples via `GetPixels32()`)
- `PinIcon.cs` already loads via `Resources.Load<Texture2D>("pin-icon-placeholder")` (in place since 1.2); when the asset is missing it falls back to a low-alpha gray fill so the pin still renders during dev
- One-shot helper `Editor/Pin/PinPlaceholderIconGenerator.cs` produces the PNG via a menu item (`MCP Game Deck > Internal > Generate Pin Placeholder Icon`) and applies the importer settings above. **Delete this file after committing the generated PNG + `.meta`** — the icon ships baked into the package; no need to keep the generator around.

**Validation:**

1. Run the menu item; PNG appears at `Editor/Resources/pin-icon-placeholder.png` with sibling `.meta`.
2. Confirm `Read/Write Enabled = true` on the texture (Inspector → Advanced). If false, `PinIcon.DrawBackground` will throw at runtime.
3. Pin renders with the actual icon (not a pink "missing texture", not the gray fallback).
4. Icon legible at 20×20 after Unity scales it down — verify it doesn't turn into mush.
5. Delete `PinPlaceholderIconGenerator.cs` (and its `.meta` if Unity created one); recompile; menu item gone, pin still renders.

**Commit:**

```
feat(v2): placeholder icon for pin

64x64 monochrome PNG in Editor/Resources/, Read/Write enabled
because PinIcon.DrawBackground samples it via GetPixels32. Generated
once via PinPlaceholderIconGenerator (deleted post-generation per its
own doc). PinIcon loads via Resources.Load. Final brand icon swaps
in via Feature 09.

Refs: 07-editor-status-pin-tasks.md (task 1.3)
```

---

## Group 2 — Polling + state machine

> Goal: pin's color reflects real connection state. Click still does nothing — that's Group 4.

### Task 2.1 — Polling loop wiring

**Size:** S
**Refs:** spec "Pin state machine"

**Output:**

- New file `Editor/Pin/PinPolling.cs`
- Static class with `[InitializeOnLoadMethod]` that subscribes to `EditorApplication.update`
- Throttle: only run actual polling logic every ~500 ms (track `EditorApplication.timeSinceStartup`)
- Polling logic for now: just sets a static `PinPolling.CurrentStatus = EPinStatus.NOT_INSTALLED` and bumps a counter
- `PinToolbarElement.CreatePin()` reads `PinPolling.CurrentStatus` instead of `_testStatus`. Note: because the toolbar API is static (factory runs once), the visible icon won't refresh yet — that wires up in 2.2 alongside the redraw mechanism described in spec section "Pin redraw mechanism"
- Subscribe defensively: `EditorApplication.update -= Tick; EditorApplication.update += Tick;` (idempotent on assembly reload)

**Validation:**

1. Compile clean.
2. Pin shows gray (matches hardcoded `NOT_INSTALLED`) on next domain reload.
3. Add temporary `McpLogger.Info` inside the throttled tick (Collapse OFF in Console) — appears every ~500 ms.
4. Trigger assembly reload (edit a .cs file) — no double-subscription, log frequency stays the same.

**Commit:**

```
feat(v2): pin polling loop wired to EditorApplication.update

Subscribes once via [InitializeOnLoadMethod], throttles to ~500 ms,
defensively unsubscribes-then-subscribes to survive assembly reload.
PinPolling.CurrentStatus is read by PinToolbarElement.CreatePin
on toolbar build; logic still stub (always returns NOT_INSTALLED).
The redraw mechanism that lets status changes propagate live to the
visible icon lands in task 2.2.

Refs: 07-editor-status-pin-tasks.md (task 2.1)
```

---

### Task 2.2 — TCP probe + base state machine

**Size:** M
**Refs:** spec "Pin state machine"

**Output:**

- `PinPolling.cs` extended:
  - Reads `GameDeckSettings._host` and `_mcpPort`
  - On each tick (throttled to ~1 s for TCP polling specifically), runs `TcpClient.ConnectAsync` with 200 ms timeout
  - Result → state:
    - Success → `EPinStatus.CONNECTED` (green)
    - Fail → check if binary exists at `PinPaths.GetBinaryPath()` (which won't exist yet — task 4.1 builds it; for now stub: assume always missing → `EPinStatus.NOT_INSTALLED`)
  - Add `PinPolling.UpdateAvailable` (stub returns false; real read lands in 2.6)
- Stub for `PinPaths.GetBinaryPath()` returning `null` for now
- `PinToolbarElement.CreatePin()` refactored: returns a custom `VisualElement` (instead of a plain `MainToolbarButton`) that owns its own `Image` child. Uses `schedule.Execute(RebuildIfChanged).Every(500)` to poll `PinPolling.CurrentStatus` and `PinPolling.UpdateAvailable`, comparing against the last-rendered tuple cached on the element. On change: disposes the previous `Texture2D`, calls `PinIcon.BuildComposite`, assigns the new texture to `Image.image`. See spec section "Pin redraw mechanism".
- Old hardcoded `_testStatus` / `_testUpdateAvailable` fields removed from `PinToolbarElement.cs`.

**Validation:**

1. Open Unity with the package; C# MCP Server starts (existing behavior). Pin should turn **green** within ~2 s.
2. In `GameDeckSettings`, change `_mcpPort` to e.g. `9999` (unused). Pin should turn **gray** (NOT_INSTALLED stub) within ~2 s.
3. Restore port. Pin returns green.
4. **Pin redraws live** as state changes — no Editor restart needed. Verified by toggling port back-and-forth in steps 2–3 with the pin visible: color flips within ~1.5 s of each change. Confirms the redraw mechanism is wired.
5. No exceptions in console even when TCP probe fails (timeout / refused).

**Commit:**

```
feat(v2): TCP probe drives pin status (green vs gray) + live redraw

PinPolling now opens a TcpClient.ConnectAsync against the configured
host/port every ~1s. Success -> CONNECTED (green). Fail -> NOT_INSTALLED
stub (binary-existence check stub, real check in task 4.2).

PinToolbarElement refactored: returns a custom VisualElement that
schedules its own redraw via schedule.Execute(...).Every(500), reading
PinPolling state and rebuilding the icon Texture2D only when the
cached (status, updateAvailable) tuple changes. See spec section
"Pin redraw mechanism" for rationale (avoids global MainToolbar.Refresh).

Refs: 07-editor-status-pin-tasks.md (task 2.2)
```

---

### Task 2.3 — Yellow state (Unity busy)

**Size:** S
**Refs:** spec "Pin state machine" (state priority section)

**Output:**

- `PinPolling.Tick()` extended:
  - Before TCP probe result is interpreted, check `EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating`
  - If busy AND TCP succeeded → status = `EPinStatus.BUSY` (yellow)
  - If busy AND TCP failed → still `EPinStatus.BUSY` (yellow takes priority over `NOT_INSTALLED` only when MCP is reachable; verify both behaviors with Ramon — recommend yellow only if MCP was last seen connected)

**Decision in task:** keep simple — yellow ONLY when `EditorApplication.isCompiling || isPlayingOrWillChangePlaymode || isUpdating` AND last successful TCP probe within 10 s. Otherwise the yellow→red transition during play mode entry is confusing.

**Validation:**

1. Pin green. Click Play in Unity. Pin turns yellow within ~1 s. Click Stop. Pin returns green.
2. Pin green. Edit a .cs file → recompile. Pin turns yellow during compile, then green again.
3. Pin gray (port wrong). Click Play. Pin stays gray (no MCP, yellow doesn't apply).

**Commit:**

```
feat(v2): yellow state for Unity busy (compiling/play/asset import)

PinPolling reads EditorApplication.isCompiling +
isPlayingOrWillChangePlaymode + isUpdating. Yellow shown only
when MCP was recently connected — avoids confusing yellow→red
transitions on offline projects.

Refs: 07-editor-status-pin-tasks.md (task 2.3)
```

---

### Task 2.4 — Port-collision detection

**Size:** M
**Refs:** spec "Pin state machine" (port collision row), `07-editor-status-pin.md` decision #4

**Output:**

- `PinPolling.cs` adds `Application.logMessageReceivedThreaded` listener
- Looks for log lines containing the literal `"EADDRINUSE"` or `"address already in use"` AND mentioning the configured port
- When detected, sets a flag `BindFailureDetected = true` with a timestamp; flag clears after 30 s of no new bind-failure messages OR after a successful TCP probe
- State machine update:
  - If TCP probe **fails** AND `BindFailureDetected` recent → `EPinStatus.BIND_FAILURE` (red, but tooltip will explain)
  - If TCP probe **fails** AND no bind failure → existing logic (`EPinStatus.NOT_INSTALLED` if no binary, `EPinStatus.NOT_RUNNING` if binary exists)

**Validation:**

1. Open project A in Unity (binds 8090). Pin green.
2. Open project B in another Unity instance with same port 8090. C# Server in B fails to bind, logs error.
3. Pin in B should turn red AND `PinPolling.BindFailureDetected` is true (verify via temp `Debug.Log`).
4. Change port in B settings. C# Server rebinds. Within 30 s flag clears, pin reflects new state.

If two Unity instances aren't easy: simulate by manually `Debug.LogError("EADDRINUSE bind failure on port 8090")` in a one-off script and verify flag flips.

**Commit:**

```
feat(v2): pin detects MCP port collisions via log listener

PinPolling subscribes to logMessageReceivedThreaded and watches for
EADDRINUSE / "address already in use" messages mentioning the configured
port. When detected, status flips to BindFailure (red, with distinct
tooltip in 2.5). Flag auto-clears after 30s of no bind errors or on
successful TCP probe.

Refs: 07-editor-status-pin-tasks.md (task 2.4)
```

---

### Task 2.5 — Tooltip text per state

**Size:** S
**Refs:** spec "Pin state machine" (tooltip table from decision #5)

**Output:**

- New file `Editor/Pin/PinTooltip.cs`
- Static method `GetText(EPinStatus status, int port, bool updateAvailable, string updateVersion)` returning the tooltip string
- Texts per the spec table (decision #5 in design doc)
- The pin's redraw callback (added in 2.2) sets `element.tooltip = PinTooltip.GetText(...)` whenever the cached state tuple changes

**Validation:**

1. Hover pin in each state, verify tooltip matches spec text:
   - Green → `MCP Game Deck connected. Click to open chat.`
   - Yellow → `Unity is busy (...). App still connected.`
   - Red (NotRunning) → `MCP Game Deck app is not running. Click to launch.`
   - Red (BindFailure) → port-collision message with port number interpolated
   - Gray → `First time? Click to install MCP Game Deck app (~9 MB download).`
2. Trigger update available (manually `EditorPrefs.SetBool("MCPGameDeck.UpdateAvailable", true)`) → tooltip appends update line.

**Commit:**

```
feat(v2): pin tooltip text per state

PinTooltip.GetText returns spec-defined text for each PinStatus,
including dynamic interpolation of port number and update version.
Verified across all 5 states + update badge variant.

Refs: 07-editor-status-pin-tasks.md (task 2.5)
```

---

### Task 2.6 — Update badge from EditorPrefs

**Size:** S
**Refs:** spec "Pin state machine" (Update available row)

**Output:**

- `PinPolling.UpdateAvailable` (already stubbed in 2.2; now wire it for real)
- Read from `EditorPrefs.GetBool("MCPGameDeck.UpdateAvailable", false)` once per tick
- The redraw callback in `PinToolbarElement` already passes both `CurrentStatus` and `UpdateAvailable` to `PinIcon.BuildComposite` — no further wiring needed in the toolbar element
- Verify `Editor/Utils/UpdateChecker.cs` already populates this key (check existing code; if not, add a minimal write — but prefer to keep UpdateChecker untouched until cleanup task 7.2)

**Validation:**

1. `EditorPrefs.SetBool("MCPGameDeck.UpdateAvailable", true)` (run from Editor menu / temporary script).
2. Within ~1 s, blue badge appears top-right of pin.
3. `EditorPrefs.SetBool("MCPGameDeck.UpdateAvailable", false)` → badge disappears.
4. Tooltip shows update info when badge present (verifies wiring of 2.5).

**Commit:**

```
feat(v2): pin shows update badge from UpdateChecker EditorPrefs

PinPolling reads MCPGameDeck.UpdateAvailable each tick, exposes via
public static. PinOverlay forwards to PinIcon. Tooltip integrates
update version when badge active.

Refs: 07-editor-status-pin-tasks.md (task 2.6)
```

---

## Group 3 — Right-click menu

> Goal: right-click pin shows the 4 menu items, each works. Settings still won't actually launch (Group 4) but copies a placeholder URL.

### Task 3.1 — Menu scaffold + Settings + Copy URL

**Size:** S
**Refs:** spec "Right-click menu", design decision #6

**Output:**

- New file `Editor/Pin/PinContextMenu.cs`
- Static method `Show(Vector2 mousePos)` that builds a `GenericMenu` with:
  - `Settings` → calls `PinLauncher.LaunchOrFocus(route: "/settings")` (stub for now: just `McpLogger.Info("[Pin] Settings clicked")`)
  - `Copy MCP Server URL` → `EditorGUIUtility.systemCopyBuffer = $"http://{host}:{port}"`, then `EditorWindow.focusedWindow?.ShowNotification(new GUIContent("MCP Server URL copied"))`
- The pin's `VisualElement` (built in 2.2) gets a `ContextualMenuManipulator` or a `MouseDownEvent` listener that fires on right-click and calls `PinContextMenu.Show(evt.mousePosition)`

**Validation:**

1. Right-click pin → menu appears with `Settings`, `Copy MCP Server URL`.
2. Click `Copy MCP Server URL` → notification appears briefly. Paste in Notepad: `http://127.0.0.1:8090` (or whatever the configured host/port).
3. Click `Settings` → console logs `[Pin] Settings clicked`. (Real launch in Group 4.)

**Commit:**

```
feat(v2): pin right-click menu (Settings + Copy URL)

PinContextMenu builds a GenericMenu with two items so far. Settings
stubbed (logs only); real launch wires up after Group 4. Copy URL
uses EditorGUIUtility.systemCopyBuffer + a brief HUD notification.

Refs: 07-editor-status-pin-tasks.md (task 3.1)
```

---

### Task 3.2 — Show install folder + About

**Size:** S
**Refs:** spec "Right-click menu" (decision #6 items 3 + 4)

**Output:**

- `PinContextMenu.cs` extended with:
  - Separator
  - `Show install folder` → `EditorUtility.RevealInFinder(PinPaths.InstallRoot)`. If folder doesn't exist, create it empty first then reveal.
  - Separator
  - `About` → opens an `EditorWindow` (or `EditorUtility.DisplayDialog` if simpler) with:
    - Package version: `PackageInfo.FindForAssembly(...).version`
    - App version: stub for now (`"not installed"`) — real check after task 4.2
    - Update status: from `EditorPrefs`
    - Button "View on GitHub" → `Application.OpenURL("https://github.com/RamonBedin/mcp-game-deck")`
- Stub `PinPaths.InstallRoot` returns `Path.Combine(Application.dataPath, "..", "TempPinInstall")` for now (real path in 4.1)

**Validation:**

1. Right-click pin → menu has 4 items now.
2. Click `Show install folder` → file explorer opens at the temp folder (which was created empty).
3. Click `About` → dialog/window shows package version (real) + "not installed" + GitHub link button.
4. Click GitHub link → browser opens.

**Commit:**

```
feat(v2): pin right-click menu — Show install folder + About

Show install folder uses EditorUtility.RevealInFinder, creates the
folder empty if missing. About reads PackageInfo.version + EditorPrefs
update status, links to GitHub repo. App version still stub
("not installed") — real check after task 4.2 wires PinPaths.

Refs: 07-editor-status-pin-tasks.md (task 3.2)
```

---

## Group 4 — Launch flow

> Goal: clicking the pin actually launches Tauri. Binary downloads on first run, runs from %APPDATA% on subsequent. SHA256 verified. Errors handled.

### Task 4.1 — Cross-platform install paths

**Size:** S
**Refs:** spec "Binary distribution" (Local install section)

**Output:**

- New file `Editor/Pin/PinPaths.cs`
- Static properties:
  - `InstallRoot` — `%APPDATA%\MCPGameDeck` on Windows, `~/Library/Application Support/MCPGameDeck` on macOS, `~/.local/share/MCPGameDeck` on Linux
  - `BinFolder(version)` — `<InstallRoot>/bin/<version>/`
  - `BinaryPath(version)` — `<BinFolder>/mcp-game-deck-app.exe` on Windows, `mcp-game-deck-app` on Unix
  - `Sha256Path(version)` — `<BinaryPath>.sha256`
- Use `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` etc. — no hardcoded paths
- `PinContextMenu` updates "Show install folder" to use `PinPaths.InstallRoot`

**Validation:**

1. Add temp `Debug.Log(PinPaths.InstallRoot)` — verify path is correct for current OS.
2. Right-click pin → Show install folder → opens correct location.
3. On Windows, path is `C:\Users\<user>\AppData\Roaming\MCPGameDeck`.

**Commit:**

```
feat(v2): cross-platform install path helpers

PinPaths centralizes %APPDATA% / ~/Library / ~/.local resolution
and per-version subfolder paths. Already wired into "Show install
folder" menu. Used by binary discovery + download next.

Refs: 07-editor-status-pin-tasks.md (task 4.1)
```

---

### Task 4.2 — Binary discovery

**Size:** S
**Refs:** spec "Binary distribution"

**Output:**

- New file `Editor/Pin/PinBinaryManager.cs`
- Static methods:
  - `IsInstalled(string version)` → `File.Exists(PinPaths.BinaryPath(version))`
  - `GetCurrentVersion()` → `PackageInfo.FindForAssembly(typeof(PinToolbarElement).Assembly).version`
- `PinPolling.Tick()` updates: when TCP probe fails, check `PinBinaryManager.IsInstalled(GetCurrentVersion())`:
  - exists → `EPinStatus.NOT_RUNNING` (red, "click to launch")
  - missing → `EPinStatus.NOT_INSTALLED` (gray, "click to install")
- `PinContextMenu` "About" updates: shows real app version (or "not installed") via `PinBinaryManager.IsInstalled`

**Validation:**

1. Pin currently shows gray (no binary anywhere). Manually create a fake file at `<InstallRoot>/bin/<version>/mcp-game-deck-app.exe` (just `echo "test" > path`). Pin should turn red within ~2 s (status `NOT_RUNNING`).
2. Delete the fake file → pin returns to gray.
3. About dialog reflects "not installed" / "v0.1.0 installed" correctly.

**Commit:**

```
feat(v2): binary discovery — gray vs red distinguishes install state

PinBinaryManager.IsInstalled checks File.Exists at the per-version
path. PinPolling uses it to differentiate NotInstalled (gray, click
to download) from NotRunning (red, click to launch). About dialog
shows real install status.

Refs: 07-editor-status-pin-tasks.md (task 4.2)
```

---

### Task 4.3 — Binary download + SHA256

**Size:** M
**Refs:** spec "Binary distribution" (download flow), design decision #1

**Output:**

- `PinBinaryManager.cs` extended with `async Task<DownloadResult> DownloadAsync(string version, IProgress<float> progress, CancellationToken ct)`:
  - Build URLs: `https://github.com/RamonBedin/mcp-game-deck/releases/download/v{version}/mcp-game-deck-app-{version}.exe` + `.sha256`
  - GET both with `HttpClient` (60 s timeout)
  - Stream binary to a temp file, reporting progress via `IProgress<float>`
  - Compute SHA256 of downloaded file (use `SHA256.Create().ComputeHash` over the stream)
  - Compare with downloaded `.sha256` file content (trim whitespace, compare hex case-insensitive)
  - On match: move temp file to `PinPaths.BinaryPath(version)` (creating parent dirs); on Unix, `chmod +x`
  - Returns `DownloadResult` enum: `Success`, `NetworkError`, `HashMismatch`
- No UI yet — error dialogs in 4.4

**Validation:**

1. Add a temporary editor menu item "MCP Game Deck → Test Download" that calls `DownloadAsync(GetCurrentVersion(), ...)` and logs result.
2. Run it. Verify file appears at `PinPaths.BinaryPath(...)`.
3. Verify pin turns red (`NotRunning`) once binary is in place.
4. Manually corrupt the binary (open in hex editor, change a byte). Run download again — should re-download (after deleting first) or log mismatch warning if logic re-checks.
5. Test offline: disable network, run download. Should return `NetworkError` cleanly (no exception spam).

**Commit:**

```
feat(v2): binary download + SHA256 verification

PinBinaryManager.DownloadAsync streams the .exe from GitHub Release,
verifies via sibling .sha256 file, atomically moves to the per-version
install path. Reports progress, handles network errors and hash
mismatches gracefully. UI integration in task 4.4.

Refs: 07-editor-status-pin-tasks.md (task 4.3)
```

---

### Task 4.4 — Download error dialogs

**Size:** M
**Refs:** spec "Binary distribution" (recovery section), design decision #1

**Output:**

- New file `Editor/Pin/PinDownloadDialog.cs`
- Static methods:
  - `ShowProgress(IProgress<float> progress)` → returns a `EditorWindow` showing a progress bar; updates as `progress.Report(value)` is called; closes on completion
  - `ShowNetworkError(string url)` → dialog with the URL clickable + "Retry" button + "Open in browser" button
  - `ShowHashMismatch()` → dialog explaining integrity check failed, "Retry" button (auto-deletes corrupt file)
  - `ShowLaunchFailed(int exitCode)` → dialog with exit code + link to issue tracker

**Validation:**

1. Trigger network error (disable wifi mid-download) → dialog appears with retry button. Re-enable wifi, click retry, succeeds.
2. Manually corrupt the `.sha256` content remotely... easier: hardcode a wrong hash temporarily in the download function → verify mismatch dialog appears.
3. Click "Open in browser" → opens GitHub releases page in default browser.

**Commit:**

```
feat(v2): download error dialogs (network / hash / launch)

PinDownloadDialog renders progress bar EditorWindow during download,
plus three error dialogs: network failure (retry + open in browser),
hash mismatch (retry + auto-delete corrupt file), launch failure
(exit code + issue tracker link). All recoverable; no silent failures.

Refs: 07-editor-status-pin-tasks.md (task 4.4)
```

---

### Task 4.5 — Process spawn + click handler

**Size:** M
**Refs:** spec "Env-var contract on launch"

**Output:**

- New file `Editor/Pin/PinLauncher.cs`
- Static methods:
  - `LaunchOrFocus(string route = "/chat")` → main entry point
  - Logic:
    1. Check `PinBinaryManager.IsInstalled(version)`.
    2. If not, show progress dialog and call `DownloadAsync`. On any error, surface dialog and return.
    3. Build `ProcessStartInfo` with binary path, args `"--route={route}"`, `UseShellExecute = false`.
    4. Set env vars per spec table (`UNITY_PROJECT_PATH`, `UNITY_MCP_HOST`, `UNITY_MCP_PORT`, `MCP_GAME_DECK_*`).
    5. `Process.Start()`. If new instance, fine. If single-instance plugin (Tauri side, task 5.1) catches it, the new process self-exits — also fine.
    6. On launch failure (exception or quick exit code != 0), show `PinDownloadDialog.ShowLaunchFailed(exitCode)`.
- `PinOverlay` adds a left-click `ClickEvent` handler on its root that calls `PinLauncher.LaunchOrFocus()`.
- `PinContextMenu`'s "Settings" item calls `PinLauncher.LaunchOrFocus("/settings")`.

**Validation:**

1. Pin gray (no binary). Click pin → progress dialog → download → Tauri opens. (Tauri version from F01 will work even without single-instance plugin yet — that's task 5.1.)
2. Tauri shows correct Unity status (connected) — meaning env vars propagated correctly.
3. Click pin again → second Tauri instance opens (until task 5.1 makes it single-instance). Close both.
4. Right-click → Settings → Tauri opens (still on Chat tab — `--route=/settings` parsing comes in 5.2).
5. Try with Tauri binary at the path but corrupted (e.g. truncated). `Process.Start` should fail or process exit with non-zero immediately → ShowLaunchFailed dialog appears.

**Commit:**

```
feat(v2): pin click launches Tauri with env vars

PinLauncher.LaunchOrFocus orchestrates: check installed, download if
missing, build ProcessStartInfo with all required env vars per spec,
spawn process, surface launch errors. Wired to left-click on pin and
to "Settings" menu item with route override.

Note: until task 5.1, multiple clicks spawn duplicate Tauri instances —
single-instance plugin lands on the Tauri side next.

Refs: 07-editor-status-pin-tasks.md (task 4.5)
```

---

## Group 5 — Tauri side (App~)

> Goal: Tauri app gains single-instance behavior, CLI route arg parsing, and the update banner UI.

### Task 5.1 — Single-instance plugin with project-scoped ID

**Size:** M
**Refs:** spec "Tauri side changes — Single-instance with project-scoped ID", design decision #3

**Output:**

- `App~/src-tauri/Cargo.toml` — add `tauri-plugin-single-instance = "2"` and `sha2 = "0.10"`, `hex = "0.4"`
- `App~/src-tauri/src/lib.rs`:
  - `fn compute_instance_id() -> String` per spec (SHA-256 of `UNITY_PROJECT_PATH`, take first 12 hex chars, format as `com.mcpgamedeck.app.{}`)
  - Register `tauri_plugin_single_instance::init(callback)` BEFORE other plugins; callback receives `(app, args, cwd)`:
    - Calls `window.set_focus()` + `unminimize()`
    - If `args` contains `--route=/path`, emit `route-requested` event with the route
- `App~/src-tauri/capabilities/default.json` — add `"core:event:allow-emit"` if not already present (for the route-requested emit)

**Validation:**

1. `pnpm tauri dev` from PC where you have the repo. Window opens normally. Status connects to Unity normally.
2. Run `pnpm tauri build` — produces MSI as before.
3. Install MSI. Launch from Start Menu. Window opens.
4. Click pin in Unity (with `UNITY_PROJECT_PATH` env var pointing at same Unity project). Tauri tries to spawn — but the plugin detects existing instance, focuses it. No second window.
5. Open a different Unity project in another Unity instance. Click that project's pin. New Tauri opens (different instance ID). Two Tauri windows now coexist.
6. Close both. Verify no zombie processes.

**Commit:**

```
feat(v2): Tauri single-instance plugin with project-scoped IDs

Adds tauri-plugin-single-instance + sha2/hex deps. Instance ID
computed from SHA-256 of UNITY_PROJECT_PATH (first 12 chars).
Each Unity project's app gets its own lock space; same-project
re-launches focus the existing window. Plugin callback also
emits "route-requested" if args include --route= (consumed in 5.2).

Refs: 07-editor-status-pin-tasks.md (task 5.1)
```

---

### Task 5.2 — CLI plugin + `--route=` parsing

**Size:** M
**Refs:** spec "Tauri side changes — CLI route argument", design decision #6 (Settings item)

**Output:**

- `App~/src-tauri/Cargo.toml` — add `tauri-plugin-cli = "2"`
- `App~/src-tauri/tauri.conf.json` — add CLI plugin config under `plugins.cli` declaring the `route` arg:
  ```json
  "cli": {
    "args": [
      { "name": "route", "takesValue": true, "longName": "route" }
    ]
  }
  ```
- `App~/src-tauri/capabilities/default.json` — grant `cli:default`
- `App~/src-tauri/src/lib.rs` — register `tauri_plugin_cli::init()`
- `App~/src/main.tsx`:
  - Async function `getInitialRoute()` that calls `getMatches()` from `@tauri-apps/plugin-cli`, returns `matches.args.route?.value as string ?? "/chat"`
  - Use the result as `MemoryRouter`'s `initialEntries`
- `App~/src/App.tsx`:
  - Listen for `route-requested` event (from 5.1's plugin callback)
  - When received, navigate using `useNavigate()` to the new route
- `App~/package.json` — add `@tauri-apps/plugin-cli` JS dep, `pnpm install`

**Validation:**

1. `pnpm tauri dev` — runs without errors.
2. From terminal, simulate the CLI: `pnpm tauri dev -- -- --route=/settings` (or however Tauri passes args in dev). App should land on Settings tab directly.
3. Build, install, then run `mcp-game-deck-app.exe --route=/settings` from PowerShell. Window opens on Settings.
4. With app already running on Chat, run the same command again. Existing window focuses AND navigates to Settings (5.1's plugin callback emits route-requested, 5.2's listener handles it).
5. Right-click pin in Unity → Settings → app focuses on Settings tab.

**Commit:**

```
feat(v2): Tauri CLI --route= argument support

Adds tauri-plugin-cli + JS @tauri-apps/plugin-cli. main.tsx reads
--route= via getMatches() and seeds MemoryRouter. App.tsx listens
for "route-requested" events from the single-instance callback to
navigate within the running instance. Wires up the pin's "Settings"
menu item to land directly on the Settings tab.

Refs: 07-editor-status-pin-tasks.md (task 5.2)
```

---

### Task 5.3 — Update banner

**Size:** M
**Refs:** spec "Tauri side changes — Update banner", design decision #1 (Update notification UX)

**Output:**

- `App~/src-tauri/src/commands.rs` (or new file) — Tauri command `get_env_var(name: String) -> Option<String>` that reads `std::env::var(name).ok()`. Register in `lib.rs`.
- `App~/src/ipc/commands.ts` — TS wrapper `getEnvVar(name: string): Promise<string | null>`
- New file `App~/src/components/UpdateBanner.tsx`:
  - On mount, calls `getEnvVar("MCP_GAME_DECK_UPDATE_AVAILABLE")`, `_LATEST_VERSION`, `_RELEASE_URL`
  - If `MCP_GAME_DECK_UPDATE_AVAILABLE === "true"`, render banner: `Update available: v{version}` + button `View release` (opens URL via `tauri-plugin-shell::open`)
  - Dismissable via X button (state lives in component, dismissal is per-session)
- `App~/src/App.tsx` — render `<UpdateBanner />` above the sidebar+main layout
- `App~/src-tauri/Cargo.toml` — add `tauri-plugin-shell = "2"` if not already there
- `App~/src-tauri/capabilities/default.json` — `"shell:allow-open"` permission

**Validation:**

1. Run app with env var `MCP_GAME_DECK_UPDATE_AVAILABLE=true`, `MCP_GAME_DECK_LATEST_VERSION=0.2.0`, `MCP_GAME_DECK_RELEASE_URL=https://github.com/RamonBedin/mcp-game-deck/releases/tag/v0.2.0`. Banner appears at top. Click "View release" → browser opens. Click X → banner dismisses for the session.
2. Run app without the env vars → no banner.
3. Banner doesn't shift layout in a janky way (stays sticky at top, content scrolls beneath).
4. Verify pin (Unity side) sets these env vars correctly via `Process.Start` — back-end-to-end check.

**Commit:**

```
feat(v2): Tauri update banner driven by env vars from pin

UpdateBanner.tsx reads MCP_GAME_DECK_UPDATE_AVAILABLE (and friends)
via a new get_env_var Tauri command. Renders a sticky banner above
the layout with a "View release" button that opens the URL in the
default browser via tauri-plugin-shell. Dismissable per session.

Refs: 07-editor-status-pin-tasks.md (task 5.3)
```

---

## Group 6 — End-to-end validation

### Task 6.1 — Smoke test

**Size:** M
**Refs:** spec "Definition of done"

**Output:**

- Walk through every state and every menu item, document any bugs found.
- Test on a fresh-state machine if possible (delete `%APPDATA%\MCPGameDeck\` to simulate first run).
- Update `07-editor-status-pin-tasks.md` Status column for any task that needs revisiting.

**Validation checklist:**

1. **First run:** delete `%APPDATA%\MCPGameDeck\`. Open Unity. Pin gray. Tooltip shows install message. Click pin. Progress dialog. Download succeeds. Tauri opens. Pin turns green. Chat echo round-trips.
2. **Second run:** close Tauri. Pin red ("not running"). Click pin. Tauri opens. (No download — binary cached.)
3. **Re-click while running:** Tauri minimized. Click pin. Tauri unminimizes and focuses.
4. **Right-click → Settings:** Tauri focuses on Settings tab.
5. **Right-click → Copy URL:** notification, paste in Notepad, correct URL.
6. **Right-click → Show install folder:** Explorer opens at `%APPDATA%\MCPGameDeck\`.
7. **Right-click → About:** dialog shows correct versions + working GitHub link.
8. **Yellow state:** click Play in Unity. Pin turns yellow. Stop. Pin returns green.
9. **Port collision:** open second Unity project with same port. Pin in second project shows red bind-failure tooltip with port info.
10. **Update available:** `EditorPrefs.SetBool("MCPGameDeck.UpdateAvailable", true)` + version + URL. Pin shows blue badge. Click pin, Tauri opens, banner appears at top.
11. **Hash mismatch:** truncate the downloaded `.exe` by 1 byte. Click pin. Hash mismatch dialog. Retry → re-downloads.
12. **Network error:** disable wifi. Delete cached binary. Click pin. Network error dialog with manual download URL.

**Commit:**

```
docs(v2): F07 smoke test — all states + menu items validated

Verified end-to-end on fresh state and steady state per spec
"Definition of done". No regressions, no surprises. Group 7
(cleanup) unblocked.

Refs: 07-editor-status-pin-tasks.md (task 6.1)
```

---

## Group 7 — Cleanup

> Goal: Editor/ChatUI/ deleted, UpdateChecker stripped, GameDeckSettings audited. Strictly LAST so the legacy UI is the fallback during pin development.

### Task 7.1 — Delete Editor/ChatUI/ folder

**Size:** S
**Refs:** spec "Cleanup phase", design decision #2

**Output:**

- Delete the entire `Editor/ChatUI/` directory (all `.cs`, `.uxml`, `.uss` files plus their `.meta` files). Use VS Code or Explorer; Claude Code can issue `rm -rf` if needed (but note: settings.json denies `rm -rf` — Ramon performs the deletion via VS Code Source Control + manual file delete).
- Search the rest of the codebase for any `using GameDeck.Editor.ChatUI` or namespace references; remove them.
- Some constants from `ChatConstants.cs` may have been used by `UpdateChecker`, settings, or other kept code — if any are still referenced, migrate them to a sensible new home (likely `Editor/Pin/PinConstants.cs` or inline if used once).

**Validation:**

1. `Editor/ChatUI/` no longer exists.
2. Unity recompiles cleanly. Console shows zero compile errors.
3. Pin still works end-to-end (left click launches, etc).
4. Try opening `Window → MCP Game Deck → Chat` — the menu item should be gone (it lived in `ChatWindow.cs`).

**Commit:**

```
chore(v2): delete Editor/ChatUI/ — replaced by pin (F07)

Removes the legacy in-Unity chat window, related partials, UXML/USS,
ServerProcessManager (Tauri's node_supervisor handles process mgmt),
WebSocketClient (Tauri uses stdio JSON-RPC), MessageRenderer, and
ChatConstants. The Tauri app + pin replace this functionality.

Any constants referenced elsewhere were migrated to PinConstants
or inlined. Verified Unity compiles clean post-delete.

Refs: 07-editor-status-pin-tasks.md (task 7.1)
```

---

### Task 7.2 — Strip UpdateChecker log + Settings banner

**Size:** S
**Refs:** spec "Cleanup phase", design decision #1 (Update notification UX)

**Output:**

- `Editor/Utils/UpdateChecker.cs`:
  - Remove the `Debug.Log` call inside `LogUpdateAvailable()` (or the whole method if it's only used once and only logs)
  - Keep all `EditorPrefs` writes intact
  - Verify no other consumers besides the pin's badge + the env vars passed to Tauri
- `Editor/Settings/GameDeckSettingsProvider.cs`:
  - Remove the `if (Utils.UpdateChecker.IsUpdateAvailable) { ... }` block (around lines 92–103 in current code, but verify by Grep)
  - Keep host/port/timeout/model fields rendering

**Validation:**

1. `UpdateChecker.cs` no longer logs to console even when an update is detected. Verify by setting `EditorPrefs.SetString("MCPGameDeck.LatestVersion", "999.0.0")` and forcing a check (or waiting 24h with a fake version) — pin badge appears, console silent.
2. Open `Project Settings → Game Deck` — no banner at the top, just the config fields.
3. Pin still reads `EditorPrefs` correctly (badge appears as expected).
4. Tauri receives `MCP_GAME_DECK_UPDATE_AVAILABLE=true` and shows banner.

**Commit:**

```
chore(v2): strip UpdateChecker log + Settings banner

UpdateChecker keeps populating EditorPrefs but no longer spams the
Console. GameDeckSettingsProvider drops the update banner block;
the pin badge + Tauri banner are now the only update surfaces.

Refs: 07-editor-status-pin-tasks.md (task 7.2)
```

---

### Task 7.3 — Audit GameDeckSettings for dead fields

**Size:** S
**Refs:** spec "Cleanup phase" (item 4)

**Output:**

- Open `Editor/Settings/GameDeckSettings.cs` and identify each field. For each, Grep the codebase post-cleanup to confirm at least one consumer remains.
- Likely candidates for removal:
  - `_defaultModel` if it was only consumed by the deleted ChatUI dropdown
  - Any chat-specific timeout, polling interval, etc
- Keep these for sure: `_host`, `_mcpPort`, `_requestTimeoutSeconds`
- Remove dead fields. Update `GameDeckSettings.json` schema (auto-cleared on next save).
- Remove corresponding UI in `GameDeckSettingsProvider` if present.

**Validation:**

1. Compile clean.
2. Open `Project Settings → Game Deck` — only the still-relevant fields show.
3. `GameDeckSettings.json` on disk re-saves without the removed fields (delete the file, let Unity regenerate).
4. Pin still reads `_host` and `_mcpPort` correctly (state machine works).

**Commit:**

```
chore(v2): audit GameDeckSettings, drop chat-only fields

Post-ChatUI deletion, swept GameDeckSettings.cs for unreferenced
fields. Removed [list of fields here, e.g. _defaultModel]. Kept
network config (_host, _mcpPort, _requestTimeoutSeconds) — consumed
by C# MCP Server and pin polling. UI in GameDeckSettingsProvider
trimmed to match.

Refs: 07-editor-status-pin-tasks.md (task 7.3)
```

---

### Task 7.4 — Final smoke test post-cleanup

**Size:** S
**Refs:** spec "Definition of done"

**Output:**

- Re-run the smoke test from task 6.1 in full.
- Document any regressions. None should exist; if any do, fix in a follow-up task.

**Validation:**

- All 12 checklist items from task 6.1 pass exactly as before.
- Plus: verify `Editor/ChatUI/` truly gone, no orphan menu items, no compile warnings about unused namespaces.

**Commit:**

```
docs(v2): F07 final smoke test post-cleanup — green

Re-validated all 12 spec scenarios after ChatUI delete + UpdateChecker
strip + Settings audit. No regressions. Feature 07 done.

Refs: 07-editor-status-pin-tasks.md (task 7.4)
```

---

## Notes for execution

- **Branch:** `feature/07-editor-status-pin` — already created from `develop/v2.0`.
- **Commit cadence:** one commit per task. Use the suggested message; adjust the bullets if implementation details shifted from spec.
- **Validation discipline:** every task has explicit "validation" steps. Run them before commit. If a step fails, fix in the same task — don't move on hoping the next task fixes it.
- **PR target:** `develop/v2.0`. After 7.4 done, open the PR with the same template style as Feature 01's PR.
- **No git operations from Claude Code** — Ramon owns git per CLAUDE.md.
- **C# coding standards** still apply (XML docs with `<param>`/`<returns>`, braces on `if`, partial class doc on one file only, etc).
- **macOS / Linux:** spec mentions cross-platform paths but full multi-OS validation is deferred. v2.0 ships Windows-validated; macOS/Linux smoke comes when first non-Windows user reports.
