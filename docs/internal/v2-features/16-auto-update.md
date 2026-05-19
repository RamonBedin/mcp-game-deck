# Feature 16 — Auto-update Infrastructure

## Status

`proposed` — design pending Ramon approval. Companion specs (`16-auto-update-spec.md` + `16-auto-update-tasks.md`) will follow when execution starts.

## Problem

The Tauri app has no self-update capability today. F01 designed an `UpdateBanner` that reads environment variables set by the Unity Editor pin process (`MCP_GAME_DECK_UPDATE_AVAILABLE`, `..._LATEST_VERSION`, `..._RELEASE_URL`) — but the banner is purely informational. There's no download, no install, no version comparison logic in the app itself.

Result: when Ramon publishes a new version, users see the banner (if Unity is open and the pin pushed env vars), but to actually update they must manually go to GitHub Releases, download the installer, close the app, run the installer. The flow defeats the purpose of an auto-updater, and Unity isn't even guaranteed to be open.

A related cosmetic issue: after the cycle 2 attempt at integrating `tauri-plugin-updater`, the app icon disappeared from the Windows taskbar and titlebar. The icon files in `App~/src-tauri/icons/` are present; the `bundle.icon` array in `tauri.conf.json` is configured; but something about the plugin or capabilities config breaks icon resolution. This must be regression-tested.

## Proposal

Integrate `tauri-plugin-updater` (v2) and `tauri-plugin-process` (for restart). App generates updater artifacts at build time, hosts the manifest (`latest.json`) on GitHub Releases, signs the manifest and binary with an Ed25519 key (minisign — generated locally, public half embedded in `tauri.conf.json`, private half in GitHub Secrets). On boot and every 4h thereafter, the app checks the manifest; if a newer version exists, the `UpdateBanner` shows three states (`available` → `Download` button → `downloading` with progress → `ready-to-install` → `Install & restart` button).

The existing env-var-driven banner stays as a fallback — Unity may know about a new version before the app's 4h timer fires, and the env-var path remains the fast signal.

**Critical:** the icon regression observed in the cycle 2 attempt must not recur. Spec phase includes a checkpoint to verify the icon resolution before and after plugin integration.

**Out of scope but acknowledged:** Windows code signing. Without code signing, the installer triggers SmartScreen `"publisher unknown"` warnings. The plan is to apply to SignPath Foundation in parallel (free OSS sponsorship; `CODE_SIGNING.md` policy already drafted from cycle 2 attempt). Application takes weeks of review; v2.0 ships unsigned; signing integration is a v2.1+ task tracked separately.

## Scope IN

- **Plugin integration:**
  - Add `tauri-plugin-updater = "2"` to `App~/src-tauri/Cargo.toml`
  - Add `@tauri-apps/plugin-updater` and `@tauri-apps/plugin-process` to `App~/package.json`
  - Register plugins in `lib.rs` / `main.rs`
  - Add capabilities permissions: `updater:default`, `process:allow-restart`
- **Configuration:**
  - `tauri.conf.json` `bundle.createUpdaterArtifacts: true`
  - `tauri.conf.json` `plugins.updater.endpoints` pointing to `https://github.com/RamonBedin/mcp-game-deck/releases/latest/download/latest.json`
  - `tauri.conf.json` `plugins.updater.pubkey` embedded
- **Rust commands** in `commands/updater.rs`:
  - `check_for_update()` — returns `Result<Option<UpdateInfo>, String>`. **Critical:** if the manifest returns 404 (no release yet), this must return `Ok(None)`, not throw. Only genuine errors (DNS, TLS, malformed JSON) should `Err`.
  - `download_and_install()` — wraps the SDK download, emits `update-progress` events `{downloaded, total}` periodically
  - `restart_app()` — wraps `tauri_plugin_process::restart`
- **`UpdateBanner` state machine** (extends current env-var banner):
  - `idle` → invisible
  - `available` → "Update available" + Download button
  - `downloading` → progress bar reading `update-progress` events
  - `ready-to-install` → "Install & restart" button → triggers `download_and_install` + `restart_app`
- **4-hour background check:** `useEffect` with `setInterval` of 4h, also on first mount. Result feeds into the state machine. Env-var path takes precedence if both are signalling.
- **GitHub Actions workflow:** `.github/workflows/release.yml` using `tauri-apps/tauri-action@v0` with `includeUpdaterJson: true`, secrets `TAURI_SIGNING_PRIVATE_KEY` + `TAURI_SIGNING_PRIVATE_KEY_PASSWORD`.
- **Icon regression check:** verify after plugin integration that the app icon still appears in the Windows taskbar and titlebar. If broken, isolate the cause (likely missing window-level `icon` config or capabilities side effect).
- **ADR-002:** document the strategy — Ed25519 signing, GitHub Releases endpoint, 4h cadence, deferred Windows code signing via SignPath Foundation.

## Scope OUT (deferred to v2.1+)

- **Windows code signing** — deferred to SignPath Foundation approval timeline (weeks). v2.1 task to integrate the `SignPath/github-action-submit-signing-request` action when approval arrives.
- **Staged rollouts / release channels** (beta/stable) — single endpoint pointing to `latest`. Channels are a v2.3+ feature.
- **Auto-download without user click** — banner requires the user to click Download. No silent updates.
- **Rollback / downgrade** — if the user installs a bad version, they download an older release manually. No in-app downgrade.
- **Delta updates** — full binary on each update.
- **macOS / Linux release pipelines** — Windows is the primary target. macOS/Linux release configuration is parallel work if/when those platforms become priority.

## Dependencies

None. F16 is independent. Manual prerequisites that Ramon executes outside the CC workflow:
1. Generate signing key: `npx --package=@tauri-apps/cli@latest tauri signer generate -w ~/.tauri/mcp-game-deck.key` (store password in password manager)
2. Paste public key into `tauri.conf.json` `plugins.updater.pubkey`
3. Add GitHub secrets `TAURI_SIGNING_PRIVATE_KEY` (contents of `.key` file) and `TAURI_SIGNING_PRIVATE_KEY_PASSWORD`

These are documented in the spec phase. Ramon does them once; they persist.

## Risks

- **Icon regression** — the cycle 2 attempt broke the Windows taskbar / titlebar icon. Cause unknown. Mitigation: explicit verification step in the spec; if regression recurs, isolate by adding `"icon": "icons/icon.ico"` to `app.windows[0]` config (Tauri 2 sometimes requires window-level icon when plugins modify window builder behavior). Worst case, defer plugin integration until isolated.
- **`check_for_update` throwing on missing manifest** — observed in cycle 2 (KI-013). The SDK propagates the "manifest not found" error as a throw; consumers need try/catch boilerplate. Mitigation: explicit try/catch in `commands/updater.rs::check_for_update` that maps 404 to `Ok(None)` and propagates only real errors. `UpdateBanner.tsx` already had a try/catch fallback that logs `[update-banner] sdk check failed` warning — that warning should disappear after the fix.
- **Signing key loss** — if Ramon loses the private key, future releases can't be signed and existing installs will reject the update. Mitigation: password manager storage + documented in ADR-002. If lost, recovery requires rotating the public key in `tauri.conf.json` and shipping one unsignable transition release.
- **SmartScreen warning on first install** — known limitation without code signing. Acceptable for v2.0; users click "More info → Run anyway". SignPath Foundation application running in parallel.

## Open questions

1. **Should the 4h check be configurable by the user?**
   - Recommendation: not in v2.0. 4h is a reasonable default for technical users; if "I want to check more often" requests come, add a setting in v2.1.
2. **What if the user dismisses the banner — does it reappear?**
   - Recommendation: re-appear on next 4h check if the same update is still available. If dismissed twice within 24h, suppress until the next version. v2.0 just keeps it showing until user clicks Download or a newer release supersedes.
3. **Should download support pause/resume?**
   - Recommendation: not in v2.0. Tauri updater's default streaming download is fine for ~50–100MB binaries. Resume becomes important only with very large updates.

## Related cycle 2 attempt notes

The cycle 2 attempt shipped the plugin integration, Rust commands, banner state machine, GitHub Actions workflow, ADR-002, AND the manual prerequisites (signing key generated, public key in `tauri.conf.json`, GitHub secrets configured). The implementation is reusable as reference from the `cycle-2-attempt-1` branch.

Two known issues from that attempt to address explicitly in this feature:
- Icon regression in Windows taskbar/titlebar
- `check_for_update` throws on missing manifest instead of returning null

Both addressed in Scope IN above.

The signing key and GitHub secrets that Ramon already generated are still valid — no need to regenerate. Once F16 ships, cutting v0.1.1 with `tauri-action` populating `latest.json` is the final smoke test of the full flow (manual test, not part of automated cycle).
