# ADR 001 — Tauri over Electron for the v2.0 external app

**Date:** 2026-04-17
**Status:** agreed
**Decided by:** Ramon

## Context

v2.0 moves the chat UI out of the Unity Editor into a dedicated cross-platform desktop app. Two viable frameworks: Tauri (Rust + system WebView) or Electron (Node + bundled Chromium).

## Decision

Tauri.

## Rationale

**Bundle size** is the deciding factor for this project specifically.

The app needs to ship inside the Unity package as `App~/dist/`, downloaded by users via git URL or registry. Electron's per-platform binary is ~150MB. Three platforms × 150MB = ~450MB added to the package. Unacceptable for an open-source Unity package.

Tauri's per-platform binary is ~10MB. Three platforms × 10MB = ~30MB. Manageable.

Secondary reasons:
- Lower memory footprint (no bundled Chromium)
- Faster startup
- Native OS feel via system WebView

## Trade-offs accepted

- **Rust learning curve.** Ramon is a programmer (his words), no greenfield language is a blocker. Tauri commands are straightforward; the Rust side does process supervision and file IO, no complex Rust patterns required.
- **WebView inconsistencies.** Edge WebView2 on Windows, WKWebView on macOS, WebKitGTK on Linux. Minor CSS/JS differences. Mitigated by sticking to broadly supported features.
- **Smaller npm ecosystem alignment.** Some Electron-specific libraries don't have Tauri equivalents. We don't need any of them — chat UI doesn't require deep OS integration beyond what Tauri provides.

## Alternatives considered

- **Electron** — rejected on bundle size.
- **Native (C++/Qt or C#/Avalonia)** — rejected on dev cost. UI iteration speed in WebView ecosystem is too valuable.
- **Web app + browser** — rejected because user doesn't want to manage a browser tab tied to a Unity project. Pin-from-Editor needs a real desktop app.

## Consequences

- App~/src-tauri/ holds Rust backend
- App~/src/ holds React frontend
- Build pipeline emits per-platform binaries to App~/dist/
- Node Agent SDK Server runs as a child process spawned by Tauri (Rust supervises it)
