# Audit Report — PlayerSettings

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/PlayerSettings/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/PlayerSettings/Tool_PlayerSettings.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Both files analyzed end-to-end. Absence claims about the PlayerSettings domain (e.g. "no tool to set application identifier in this domain") are made based on full coverage. Absence claims that span the whole repo were verified via targeted Grep over `Editor/Tools/`.

**Reviewer guidance:**
- This is one of the smallest domains in the project (2 tools). Most findings revolve around (a) heavy cross-domain overlap with `Tool_Build.GetSettings` / `Tool_Build.SetSettings` and (b) a very thin slice of the PlayerSettings API actually being exposed.
- The `Get` tool emits two "use editor-get-pref" hints that are factually wrong — `editor-get-pref` reads `EditorPrefs`, not `PlayerSettings`, and scripting backend / API compatibility level are not stored there. This will mislead the LLM. Treat as a high-impact correctness bug.
- The redundancy with the Build domain is the dominant strategic question for this audit; the planner should decide whether PlayerSettings is the canonical owner or whether it should be deprecated/merged into Build.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `player-settings-get` | PlayerSettings / Get | `Tool_PlayerSettings.Get.cs` | 0 | yes |
| `player-settings-set` | PlayerSettings / Set | `Tool_PlayerSettings.Set.cs` | 7 | no |

**Internal Unity API surface used:**

- `Tool_PlayerSettings.Get`: `PlayerSettings.companyName`, `productName`, `bundleVersion`, `colorSpace`, `runInBackground`, `defaultScreenWidth`, `defaultScreenHeight`, `fullScreenMode`, `PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application)`.
- `Tool_PlayerSettings.Set`: `PlayerSettings.companyName`, `productName`, `bundleVersion`, `colorSpace`, `runInBackground`, `defaultScreenWidth`, `defaultScreenHeight`. (Note: `Set` does NOT touch `fullScreenMode` despite `Get` reporting it, and does NOT touch icons.)

**Parameter detail — `player-settings-set`:**

| Param | Type | Default | Description |
|-------|------|---------|-------------|
| `companyName` | string | `""` | "Company name. Empty = unchanged." |
| `productName` | string | `""` | "Product name. Empty = unchanged." |
| `version` | string | `""` | "Bundle version (e.g. '1.0.0'). Empty = unchanged." |
| `colorSpace` | string | `""` | "Color space: 'Linear' or 'Gamma'. Empty = unchanged." |
| `runInBackground` | int | `-1` | "Run in background. -1 = unchanged, 0 = false, 1 = true." |
| `screenWidth` | int | `0` | "Default screen width. 0 = unchanged." |
| `screenHeight` | int | `0` | "Default screen height. 0 = unchanged." |

---

## 2. Redundancy Clusters

### Cluster R1 — PlayerSettings / Build cross-domain overlap (significant)
**Members:** `player-settings-get`, `player-settings-set`, `build-get-settings`, `build-set-settings`
**Overlap:**
- `build-get-settings` (`Editor/Tools/Build/Tool_Build.GetSettings.cs` lines 33-47) already reports `PlayerSettings.productName`, `companyName`, `bundleVersion`, `applicationIdentifier`, `Android.bundleVersionCode`, `iOS.buildNumber`, scripting backend and scripting defines — i.e. a strict superset of `player-settings-get` for the shared fields, plus extras the PlayerSettings tool cannot read.
- `build-set-settings` (`Editor/Tools/Build/Tool_Build.SetSettings.cs` lines 39-82) writes `productName`, `companyName`, `bundleVersion`, `applicationIdentifier`, `scripting_backend`, `defines` — i.e. it overlaps `player-settings-set` on the first three properties and exposes settings that `player-settings-set` doesn't.
- The two domains use different parameter shapes for the same effect: `player-settings-set` uses one slot per setting with sentinel defaults; `build-set-settings` uses the `action`-style `(property, value)` dispatch. An LLM asked "set product name to X" has two valid tools and no disambiguation hint in either description.

**Impact:** High. Any time the LLM is asked to change a name/version/bundle id, it must guess between two domains, and `build-set-settings` is strictly more powerful (covers bundle_id, scripting backend, defines). Result: behavior depends on LLM coin flip; PlayerSettings tools may be invoked for tasks Build tools handle better, or vice versa.

**Confidence:** high (verified by Grep for `PlayerSettings\.` across `Editor/Tools/`; Build is the only other consumer.)

### Cluster R2 — Get/Set as a single conceptual tool with `action` dispatch
**Members:** `player-settings-get`, `player-settings-set`
**Overlap:** Each tool covers a fixed list of fields; the Set tool covers a near-subset of Get (Get reads icons + fullscreen mode, Set writes neither). A single property-dispatch tool (`player-settings(action: get|set, property: ..., value: ...)`) would unify both and fix the field-drift between them — but this is a stylistic choice, not a defect.
**Impact:** Low — present pair already works for the LLM. Only worth consolidating if the broader R1 decision is "keep PlayerSettings as a domain and grow it".
**Confidence:** medium (depends on the strategic decision in R1).

---

## 3. Ambiguity Findings

### A1 — `Get` description is incomplete and lists fields the tool does not return
**Location:** `player-settings-get` — `Tool_PlayerSettings.Get.cs` line 28
**Issue:** The method `[Description]` advertises "scripting backend, API level, etc." but the implementation explicitly does NOT return those — it returns the literal string `"(use editor-get-pref)"` for both. The description sets an expectation the tool fails to meet, and (worse) the body redirects to a tool that cannot satisfy the request either (see A2).
**Evidence:**
```
[Description("Returns current Player Settings: company name, product name, version, scripting backend, API level, etc.")]
```
vs. line 41-42 of the body:
```
sb.AppendLine($"  Scripting Backend: (use editor-get-pref)");
sb.AppendLine($"  API Compatibility: (use editor-get-pref)");
```
**Confidence:** high

### A2 — Body emits factually wrong redirect to `editor-get-pref`
**Location:** `player-settings-get` — `Tool_PlayerSettings.Get.cs` lines 41-42
**Issue:** `editor-get-pref` (`Tool_Editor.Preferences.cs`) reads `EditorPrefs`, not `PlayerSettings`. `PlayerSettings.GetScriptingBackend(...)` and `PlayerSettings.GetApiCompatibilityLevel(...)` are not surfaced via EditorPrefs at all. The hint will drive the LLM into a dead end — it will call `editor-get-pref` with some guessed key, find nothing, and either give up or invent a key. Meanwhile `build-get-settings` already reports the correct scripting backend (Build/Tool_Build.GetSettings.cs line 46), which is the correct redirect target.
**Evidence:** `Tool_Editor.Preferences.cs` line 41 description: "Gets an EditorPrefs value by key." `Tool_Build.GetSettings.cs` line 46: `PlayerSettings.GetScriptingBackend(namedTarget)`.
**Confidence:** high

### A3 — Missing disambiguation between PlayerSettings and Build domains
**Location:** Both `player-settings-get` and `player-settings-set`
**Issue:** When two tools (here, two domains) overlap, descriptions should contain a "use this when X, not Y" clause. Neither PlayerSettings tool mentions the existence of `build-get-settings` / `build-set-settings`, and neither Build tool mentions PlayerSettings. The LLM has no signal to choose between them.
**Evidence:**
- `player-settings-get` description: "Returns current Player Settings: …" — no mention of `build-get-settings`.
- `player-settings-set` description: "Modifies Player Settings. Only non-empty values are applied." — no mention of `build-set-settings`.
**Confidence:** high

### A4 — `colorSpace` parameter accepts magic strings without enumerating in the param description
**Location:** `player-settings-set` param `colorSpace`
**Issue:** The param description does enumerate the values ("'Linear' or 'Gamma'"), so this is borderline OK, but the method-level description does not. An LLM scanning the tool list (which often shows method-level description first) sees only "Modifies Player Settings. Only non-empty values are applied." and has no signal that `colorSpace="srgb"` will fail until invocation time.
**Evidence:** Method `[Description]` line 31; per-param description line 36. No enumeration at method level.
**Confidence:** medium

### A5 — `runInBackground` tri-state encoding is non-obvious without example
**Location:** `player-settings-set` param `runInBackground`
**Issue:** Encoding is `-1 = unchanged, 0 = false, 1 = true`. This is documented in the param description but is the only param using a sentinel-int instead of a sentinel-string-empty pattern, which is inconsistent with the surrounding params and easy for an LLM to miss when only the method-level description is visible.
**Evidence:** Line 37: `int runInBackground = -1`.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — Sentinel-default encoding scheme is inconsistent within the same tool
**Location:** `player-settings-set`, all params
**Issue:** The "leave unchanged" sentinel is `""` for strings, `-1` for `runInBackground`, and `0` for `screenWidth`/`screenHeight`. Three different sentinel conventions across seven params. `0` is also a technically valid (if useless) value for `screenHeight` that the tool silently treats as "unchanged" — there's no way for a caller to deliberately set the screen height to 0, and no way to detect that intent vs. omission.
**Current:**
```
string companyName = "", string productName = "", string version = "",
string colorSpace = "", int runInBackground = -1, int screenWidth = 0, int screenHeight = 0
```
**Suggested direction:** Use nullable types (`string?`, `int?`, `bool?`) so omission is unambiguous; or move to an `action`-dispatch shape like Build's `(property, value)`, which side-steps the sentinel problem entirely.
**Confidence:** high

### D2 — `runInBackground` should be `bool?` not tri-state int
**Location:** `player-settings-set` param `runInBackground`
**Issue:** Underlying Unity API is a `bool`. The tri-state int is a workaround for a missing nullable bool, and produces a worse interface for the LLM.
**Current:** `int runInBackground = -1`
**Suggested direction:** `bool? runInBackground = null` (consistent with D1).
**Confidence:** high

### D3 — `Get` accepts no params; cannot scope to a specific NamedBuildTarget
**Location:** `player-settings-get`
**Issue:** The tool hardcodes `NamedBuildTarget.Unknown` for icon retrieval (line 38). A caller that wants to inspect Android-specific or iOS-specific PlayerSettings has no way to do so. There's no platform parameter.
**Current:** `Get()` — zero params.
**Suggested direction:** Add an optional `platform` param (default current/active build target) so the LLM can ask about PlayerSettings on a specific NamedBuildTarget.
**Confidence:** medium (this overlaps with capability gap G3 below).

---

## 5. Capability Gaps

### G1 — Cannot set `applicationIdentifier` (bundle id) via PlayerSettings domain
**Workflow:** "Set the bundle identifier for this project to com.studio.game."
**Current coverage:** `player-settings-set` does not expose `applicationIdentifier`. `build-set-settings` does (Build/Tool_Build.SetSettings.cs line 54, `case "bundle_id"`).
**Missing:** Either a `bundleId` param on `player-settings-set`, or an explicit description note redirecting the LLM to `build-set-settings`. As written, an LLM that picked the PlayerSettings domain has no path forward and may invent a workaround.
**Evidence:** `Tool_PlayerSettings.Set.cs` parameter list lines 32-40 — no `applicationIdentifier`. `Tool_Build.SetSettings.cs` line 54 implements it.
**Confidence:** high

### G2 — Cannot set or read application icon
**Workflow:** "Set the application icon to this Texture2D asset."
**Current coverage:** `player-settings-get` reports a one-line summary "Set" / "Not set" for the default icon. Nothing more.
**Missing:** No tool wraps `PlayerSettings.SetIcons(NamedBuildTarget, Texture2D[], IconKind)`. Confirmed via Grep for `SetIcons` across `Editor/Tools/` — zero matches in any tool. Without it, configuring the project icon requires a manual Editor action.
**Evidence:** Grep for `SetIcons` in `Editor/Tools/` returned zero matches. `Tool_PlayerSettings.Get.cs` line 38 only reads icons; no Set partial exists.
**Confidence:** high

### G3 — Cannot set or read scripting backend or API compatibility from PlayerSettings domain
**Workflow:** "Switch this project to IL2CPP for Android and set API Compatibility Level to .NET Standard 2.1."
**Current coverage:** `build-set-settings` covers scripting backend (`il2cpp`/`mono`) for the **active** build target group. There is no tool, in any domain, that wraps `PlayerSettings.SetApiCompatibilityLevel(...)` (verified via Grep — zero matches in `Editor/Tools/`). `Tool_PlayerSettings.Get.cs` claims this is available "via editor-get-pref", which is false (see A2).
**Missing:** (a) API compatibility level setter/getter wrapper around `PlayerSettings.SetApiCompatibilityLevel`/`GetApiCompatibilityLevel`. (b) A scripting-backend tool that targets a specific NamedBuildTarget rather than the currently selected one. (c) Truthful redirect text in `Tool_PlayerSettings.Get.cs`.
**Evidence:** Grep for `SetApiCompatibilityLevel` across `Editor/Tools/` — zero matches. `Tool_Build.SetSettings.cs` line 36-37 hardcodes `EditorUserBuildSettings.selectedBuildTargetGroup`, so cross-platform configuration requires switching the active target first.
**Confidence:** high

### G4 — Cannot configure Android- or iOS-specific PlayerSettings (versionCode, buildNumber, etc.)
**Workflow:** "Bump the Android `bundleVersionCode` to 42."
**Current coverage:** `build-get-settings` reads `PlayerSettings.Android.bundleVersionCode` and `PlayerSettings.iOS.buildNumber` (Build/Tool_Build.GetSettings.cs lines 37, 40), so the LLM can see the values. Nothing writes them.
**Missing:** No tool, in any domain, writes platform-specific PlayerSettings sub-objects (`PlayerSettings.Android.*`, `PlayerSettings.iOS.*`, `PlayerSettings.WebGL.*`). Verified via Grep for `PlayerSettings\.` — only reads (Get-style) of these sub-objects exist; no writes.
**Evidence:** `Tool_PlayerSettings.Set.cs` parameter list — no platform-specific params. `Tool_Build.SetSettings.cs` switch — no `version_code` / `build_number` cases.
**Confidence:** high

### G5 — Cannot configure splash screen
**Workflow:** "Disable the Unity splash screen and set a custom logo."
**Current coverage:** None.
**Missing:** No tool, in any domain, wraps `PlayerSettings.SplashScreen.*`. Confirmed via Grep for `splashScreen|SplashScreen` in `Editor/Tools/` — zero matches.
**Evidence:** Same Grep as G2 / G3 / G4.
**Confidence:** high

### G6 — Cannot set/get `defaultScreenWidth`/`Height` to "windowed defaults" (vs. fullscreen mode)
**Workflow:** "Configure the standalone player to start at 1920x1080 in windowed mode."
**Current coverage:** `player-settings-set` writes `defaultScreenWidth`/`Height` and `player-settings-get` reads `fullScreenMode`. But `player-settings-set` does NOT expose `fullScreenMode`, so the workflow is half-complete.
**Missing:** Set-side coverage of `PlayerSettings.fullScreenMode` (a `FullScreenMode` enum). Currently only readable via Get, not writable.
**Evidence:** `Tool_PlayerSettings.Get.cs` line 46 reads `fullScreenMode`; `Tool_PlayerSettings.Set.cs` parameter list lines 32-40 has no corresponding setter.
**Confidence:** high

---

## 6. Priority Ranking

| # | Finding ID | Category | Impact (1-5) | Effort (1-5) | Priority | Confidence | Summary |
|---|-----------|----------|--------------|--------------|----------|-----------|---------|
| 1 | A2 | Ambiguity (correctness) | 5 | 1 | 25 | high | `Get` redirects to `editor-get-pref` for scripting backend / API level — factually wrong, EditorPrefs has nothing to do with PlayerSettings. Drives LLM into dead end. |
| 2 | A1 | Ambiguity | 4 | 1 | 20 | high | `Get` description advertises fields ("scripting backend, API level") it does not actually return. |
| 3 | R1 | Redundancy (cross-domain) | 5 | 3 | 15 | high | PlayerSettings vs Build domain overlap: 4 tools, no disambiguation, Build is strictly more powerful. Strategic call needed. |
| 4 | A3 | Ambiguity (disambiguation) | 4 | 1 | 20 | high | Neither PlayerSettings tool mentions Build counterparts. Trivial doc fix; large LLM-routing payoff. |
| 5 | G1 | Capability Gap | 4 | 2 | 16 | high | `applicationIdentifier` (bundle id) not exposed by `player-settings-set`; only by Build. Inconsistent surface. |
| 6 | G3 | Capability Gap | 4 | 2 | 16 | high | API Compatibility Level unreachable in any domain. Scripting backend only via Build's "active target" wrapper. |
| 7 | G6 | Capability Gap | 3 | 2 | 12 | high | `fullScreenMode` readable but not writable. Asymmetric Get/Set coverage in same domain. |
| 8 | D1 | Default Values | 3 | 2 | 12 | high | Three different sentinel conventions in 7 params; 0-as-unchanged means deliberate 0 cannot be expressed. |
| 9 | G4 | Capability Gap | 3 | 3 | 9 | high | Android/iOS sub-object PlayerSettings (bundleVersionCode, buildNumber) readable via Build, not writable anywhere. |
| 10 | G2 | Capability Gap | 2 | 3 | 6 | high | Cannot set application icon (`PlayerSettings.SetIcons`). Lower frequency but workflow-blocking when needed. |
| 11 | D2 | Default Values | 2 | 1 | 10 | high | `runInBackground` should be `bool?` not tri-state int. Trivial cleanup, lands with D1. |
| 12 | A4 | Ambiguity | 2 | 1 | 10 | medium | Method-level description for `Set` doesn't enumerate `colorSpace` values. |
| 13 | G5 | Capability Gap | 1 | 4 | 3 | high | Splash screen unconfigurable. Niche but a clear gap. |

Top-of-stack actions for the planner: **A2 + A1 + A3** (description fixes, near-zero effort, immediate LLM correctness gains) and **R1** (strategic decision: is PlayerSettings the canonical owner, deprecated in favor of Build, or merged?).

---

## 7. Notes

**Cross-domain dependencies noticed:**
- `Tool_Build.GetSettings.cs` and `Tool_Build.SetSettings.cs` are the de facto canonical writers/readers for many PlayerSettings fields. Whichever direction the planner picks, the *other* domain needs explicit cross-references in its descriptions.
- `Tool_Editor.Preferences.cs` (`editor-get-pref` / `editor-set-pref`) is mistakenly referenced by `Tool_PlayerSettings.Get.cs` lines 41-42. These tools have no relationship to PlayerSettings. The redirect must be removed or replaced with a pointer to `build-get-settings`.

**Open questions for the reviewer (Ramon):**
1. **R1 strategic call:** Should the PlayerSettings domain be (a) kept and grown into the canonical owner, with Build delegating; (b) deprecated in favor of Build's property-dispatch; or (c) kept as-is and just better disambiguated? The audit doesn't pre-suppose an answer; the consolidation-planner agent will need this decision.
2. **D1/D2:** Is the project ready to switch sentinel-defaults to nullable types (`string?`, `bool?`, `int?`) on tool method signatures, or has the MCP attribute pipeline been observed to misbehave with nullable value types? If nullable types are off-limits, an `action`/`property` dispatch shape (like Build's) is the only clean fix.
3. **G3/G4:** Is platform-specific PlayerSettings coverage (Android, iOS, WebGL) a v1.2 priority or deferred? It's a real gap but a large surface area to wrap.

**Workflows intentionally deferred:**
- Graphics API selection (`PlayerSettings.SetGraphicsAPIs`), color gamut, HDR display output, VR/XR settings — all unwrapped, but feel out of scope for a "PlayerSettings" audit aimed at common Unity workflows. Flagged here only so the planner is aware they exist.

**Limits of this audit:**
- Domain has only 2 files, so coverage is exhaustive within PlayerSettings. Cross-domain claims about Build, Editor, and "no tool anywhere wraps X" were verified by targeted Grep over `Editor/Tools/` — these are presence-of-API claims, not behavioral claims, and could in principle miss a tool that wraps the API under a non-obvious method name. None observed, but if the reviewer knows of one, the relevant gap finding should be downgraded.
