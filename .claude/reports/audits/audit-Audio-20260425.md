# Audit Report — Audio

**Date:** 2026-04-25
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Audio/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 2 (via Glob `Editor/Tools/Audio/Tool_Audio.*.cs`)
- `files_read`: 2
- `files_analyzed`: 2

**Balance:** ✅ balanced

**Errors encountered during audit:**
- None.

**Files not analyzed (if any):**
- None.

**Absence claims in this report:**
- Coverage is balanced (2/2), so absence claims are admissible. They are scoped to: (a) within the Audio domain only, or (b) cross-domain checks performed via specific Grep queries documented inline (e.g. `AudioMixerGroup`, `outputAudioMixerGroup`, `AudioImporter`, `PlayClipAtPoint`, `ReadOnlyHint` — all returned zero matches in `Editor/Tools/`).

**Reviewer guidance:**
- The Audio domain is one of the smallest in the project (2 tools). The audit surface is correspondingly small but the gap surface is large — Unity's audio API has many facets (mixer routing, import settings, listener config, runtime preview) that are entirely unwrapped.
- Both existing tools share a common pattern (`-1`/`0` sentinel ints to mean "unchanged" / boolean). This is uncommon elsewhere in the project and worth a targeted reviewer decision.
- Cross-domain note: `AudioListener` is implicitly created by `camera-create` (see `Tool_Camera.Create.cs` line 64); there is no dedicated tool to add/remove/configure an `AudioListener`. The Audio domain does not own this — flagged as an informational gap.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `audio-create` | Audio / Create | `Tool_Audio.Create.cs` | 8 | no |
| `audio-configure` | Audio / Configure | `Tool_Audio.Configure.cs` | 9 | no |

**Internal Unity API surface used:**
- `audio-create`: `new GameObject(...)`, `GameObject.AddComponent<AudioSource>()`, `AssetDatabase.LoadAssetAtPath<AudioClip>`, `Undo.RegisterCreatedObjectUndo`, `Selection.activeGameObject`.
- `audio-configure`: `EditorUtility.EntityIdToObject`, `GameObject.Find`, `GameObject.TryGetComponent<AudioSource>`, `Undo.RecordObject`, `EditorUtility.SetDirty`, direct `AudioSource` property writes (`volume`, `pitch`, `spatialBlend`, `minDistance`, `maxDistance`, `playOnAwake`, `loop`).

**Parameter detail — `audio-create`:**
- `name: string = "AudioSource"`
- `clipPath: string = ""`
- `posX/posY/posZ: float = 0`
- `playOnAwake: bool = true`
- `loop: bool = false`
- `volume: float = 1f`

**Parameter detail — `audio-configure`:**
- `instanceId: int = 0`
- `objectPath: string = ""`
- `volume/pitch/spatialBlend/minDistance/maxDistance: float = -1f` (sentinel "skip")
- `playOnAwake: int = -1` (sentinel; 0/1 are the meaningful values)
- `loop: int = -1` (sentinel; 0/1 are the meaningful values)

**Read-only tool count:** 0 in domain. Confirmed via Grep for `ReadOnlyHint` in `Editor/Tools/Audio/` — zero matches.

---

## 2. Redundancy Clusters

No redundancy clusters identified. The two tools have clearly distinct responsibilities (creation vs. mutation of an existing component) and their parameter sets only overlap on the four properties any AudioSource exposes (`playOnAwake`, `loop`, `volume`, plus `clip` on create only). The `audio-configure` tool already handles the "edit only what was supplied" pattern via sentinel values, which is the right shape — collapsing them would not reduce LLM ambiguity, it would only widen the surface area of `audio-create`.

---

## 3. Ambiguity Findings

### A1 — `audio-configure` uses int sentinels for booleans without a typed boolean alternative
**Location:** `audio-configure` — `Tool_Audio.Configure.cs` lines 44–45, 97–106
**Issue:** `playOnAwake` and `loop` are typed as `int` with the convention `-1 = skip / 0 = false / 1 = true`. This is a tri-state-via-int that the LLM has to remember and that the schema doesn't enforce. A caller passing `bool true` (which the JSON-RPC layer would coerce to `1`) "happens to work" by coincidence; a caller passing `2` or `-2` is silently ignored. The pattern is also asymmetric with `audio-create`, which uses real `bool` for the same fields.
**Evidence:** `Tool_Audio.Configure.cs` line 35: `"playOnAwake/loop: -1 = skip, 0 = false, 1 = true."` — and lines 97 / 103: `if (playOnAwake == 0 || playOnAwake == 1) { ... }`. Compare with `Tool_Audio.Create.cs` line 48: `bool playOnAwake = true`.
**Confidence:** high

### A2 — `audio-configure` uses `-1` sentinel for `volume` / `spatialBlend`, which silently rejects valid input
**Location:** `audio-configure` — `Tool_Audio.Configure.cs` lines 39, 41, 67–83
**Issue:** The sentinel `-1f` is used to mean "unchanged" for floats. For `volume` and `spatialBlend`, valid values are `[0, 1]`, so `-1` is unambiguous as a sentinel. But the gating condition is `>= 0f`, which accepts `0` as "apply zero" — that's correct. However, the description does not warn the LLM that `-2`, `-0.5`, or any other negative value is equivalent to `-1` (they all skip). This is a magic-default with non-obvious behaviour.
**Evidence:** `Tool_Audio.Configure.cs` line 67: `if (volume >= 0f)`. Description on line 39: `"Volume (0–1). -1 to leave unchanged."` — does not state that any negative value skips.
**Confidence:** medium

### A3 — `pitch` sentinel `-1` collides with a valid Unity value
**Location:** `audio-configure` — `Tool_Audio.Configure.cs` lines 40, 73–77
**Issue:** Unity's `AudioSource.pitch` accepts negative values (it plays the clip in reverse; the inspector range is -3 to 3). Using `-1f` as a "skip" sentinel makes it impossible to set a reverse-playback pitch of exactly -1 through this tool — the gate is `pitch >= 0f`, which silently drops every negative pitch. This is a real capability loss disguised as a default.
**Evidence:** `Tool_Audio.Configure.cs` line 73: `if (pitch >= 0f)` — every negative pitch is silently ignored. Unity docs: pitch range `[-3, 3]`.
**Confidence:** high

### A4 — `audio-create` defaults `volume = 1f` but does not document that values above 1 are clamped
**Location:** `audio-create` — `Tool_Audio.Create.cs` line 50, 63
**Issue:** `volume` is described as "0 to 1" but the LLM may pass higher values trying to amplify; `Mathf.Clamp01` silently clamps. The description doesn't warn about the clamp. Same pattern in `audio-configure` (line 69).
**Evidence:** `Tool_Audio.Create.cs` line 50: `[Description("Playback volume in the range 0 to 1. Default 1.")]` — no clamp warning. Line 63: `source.volume = Mathf.Clamp01(volume);`.
**Confidence:** medium

### A5 — `audio-create` description does not disambiguate against `audio-configure`
**Location:** `audio-create` — `Tool_Audio.Create.cs` line 41
**Issue:** With only two tools in the domain, disambiguation is low-stakes, but the description does not contain a "use this when X, not Y" clause. An LLM asked to "set the volume of an AudioSource" could plausibly invoke `audio-create` (passing only `volume`) and end up creating an extra GameObject. A line like "Use `audio-configure` to modify an existing AudioSource" would prevent this.
**Evidence:** `Tool_Audio.Create.cs` line 41: description ends at "registered with Undo." with no cross-reference.
**Confidence:** medium

### A6 — Both tools accept `clipPath` / no clip-related field on configure, but the description of `audio-configure` does not say "clip cannot be changed here"
**Location:** `audio-configure` — `Tool_Audio.Configure.cs` line 35
**Issue:** The configure tool intentionally omits `clipPath`. An LLM trying to "swap the clip on an existing AudioSource" will not be told this is unsupported by the configure tool — it will discover this through trial and error or hallucinate a parameter. A short note "To change the clip, remove and re-add the AudioSource via `component-add`, or use `audio-create` for a new instance" would close the loop.
**Evidence:** `Tool_Audio.Configure.cs` line 35: parameter list explicitly does not include any clip field; description does not mention this.
**Confidence:** medium

---

## 4. Default Value Issues

### D1 — `audio-create.clipPath` empty default produces a silent AudioSource
**Location:** `audio-create` param `clipPath`
**Issue:** Default is `""`, which leads to creating an AudioSource with no clip. This is a valid use case (template / runtime-assigned clips) but probably not the most common one — most callers want to attach a specific clip. The current behaviour is correct (don't fail), but the description should state that a missing or invalid path produces a warning rather than an error (the code already does this on lines 72–75; the description does not).
**Current:** `string clipPath = ""`
**Suggested direction:** Keep the empty default. Strengthen the description to say "leave empty to create a clip-less template; an invalid path produces a warning, not an error."
**Confidence:** medium

### D2 — `audio-configure` requires either `instanceId` or `objectPath`, but neither is marked required
**Location:** `audio-configure` params `instanceId` (default 0) and `objectPath` (default "")
**Issue:** Both have defaults that resolve to "no target". Calling the tool with no arguments returns the error `"Provide instanceId or objectPath to identify the target GameObject."` (line 170). This is a reasonable runtime check, but the schema makes both look optional, and neither parameter description explicitly says "one of these is required". The LLM may pass empty strings expecting "configure the currently selected AudioSource" semantics.
**Current:** `int instanceId = 0, string objectPath = ""`
**Suggested direction:** Either (a) document explicitly in both param descriptions "exactly one of `instanceId` / `objectPath` is required", or (b) accept a "use current selection" mode, or (c) wrap both in a single union parameter. No code recommendation here — that's the planner's call.
**Confidence:** high

### D3 — `audio-configure` sentinel `-1` for `pitch` is a default that doesn't match the common case
**Location:** `audio-configure` param `pitch`
**Issue:** As noted in A3, pitch `-1` is a valid Unity value (reverse playback at full speed). Using it as a sentinel is a *wrong* default for a meaningful Unity input. Even if no caller has hit this yet, it's a latent correctness bug.
**Current:** `float pitch = -1f`
**Suggested direction:** Change the sentinel for pitch (and arguably for all "skip" cases) to a value that is unambiguously out-of-domain, e.g. `float.NaN`, or use a nullable type if the framework supports it. Decision belongs to the planner.
**Confidence:** high

### D4 — `audio-configure` boolean params declared as `int` instead of `bool?`
**Location:** `audio-configure` params `playOnAwake`, `loop`
**Issue:** As noted in A1, these are `int` with tri-state encoding. If the MCP framework supports nullable booleans, a `bool?` (default `null`) is the idiomatic representation. If it does not, a string enum (`"true"|"false"|"skip"`) is at least self-documenting. The current shape is the worst of both worlds: typed as a number, used as a tri-state.
**Current:** `int playOnAwake = -1, int loop = -1`
**Suggested direction:** Confirm whether the MCP framework supports `bool?`. If yes, switch. If no, document the encoding more aggressively in the param description (per A1).
**Confidence:** high

---

## 5. Capability Gaps

### G1 — Cannot route AudioSource to an AudioMixerGroup
**Workflow:** Configure a music/SFX/UI bus structure: create an `AudioMixer` asset, route AudioSources to specific `AudioMixerGroup`s so the developer can balance master/music/SFX volumes globally.
**Current coverage:** None. `audio-create` and `audio-configure` do not expose `AudioSource.outputAudioMixerGroup`. Grep for `AudioMixerGroup` and `outputAudioMixerGroup` across `Editor/Tools/` returned zero matches — this capability does not exist anywhere in the project.
**Missing:** A way to set `AudioSource.outputAudioMixerGroup` (typed as `UnityEngine.Audio.AudioMixerGroup`). Also missing: tools to create / load / inspect `AudioMixer` and `AudioMixerGroup` assets.
**Evidence:** `Tool_Audio.Configure.cs` lines 36–46: parameter list contains no mixer-related field. Grep `AudioMixerGroup` returned zero hits across `Editor/Tools/`.
**Confidence:** high

### G2 — Cannot configure 3D rolloff curve / advanced spatial properties
**Workflow:** Set up positional 3D audio with non-default falloff: change `rolloffMode` (Logarithmic / Linear / Custom), `dopplerLevel`, `spread`, `reverbZoneMix`, `spatialize`. These are standard inspector fields a developer would expect to script.
**Current coverage:** Only `spatialBlend`, `minDistance`, `maxDistance` are exposed by `audio-configure`.
**Missing:** No tool exposes `rolloffMode`, `dopplerLevel`, `spread`, `reverbZoneMix`, `priority`, `stereoPan`, `bypassEffects`, `bypassListenerEffects`, `bypassReverbZones`, `mute`, `ignoreListenerVolume`, `ignoreListenerPause`, custom `AnimationCurve` for rolloff (`SetCustomCurve`).
**Evidence:** `Tool_Audio.Configure.cs` lines 36–46 enumerates the entire surface; none of the above fields are present.
**Confidence:** high

### G3 — No read-only inspector tool for AudioSource state
**Workflow:** "What's currently set on this AudioSource?" — debugging or context gathering before edits. Without this, the LLM has to guess current state or call `audio-configure` blindly.
**Current coverage:** None. `audio-create` and `audio-configure` are both write tools (no `ReadOnlyHint = true` in domain — verified via Grep).
**Missing:** An `audio-inspect` (or equivalent) that returns volume / pitch / clip path / mixer routing / spatial settings of an existing AudioSource.
**Evidence:** Grep `ReadOnlyHint` in `Editor/Tools/Audio/` returned zero matches. The two existing tools both call `Undo.RecordObject` or `Undo.RegisterCreatedObjectUndo` — they are mutation tools by design.
**Confidence:** high

### G4 — No tool to play / preview an AudioClip in the editor
**Workflow:** "Play this clip so I can verify it sounds right" — useful when iterating on SFX or testing a prefab. Unity exposes this via `AudioSource.PlayClipAtPoint` and the internal `AudioUtil` reflection API for editor-time preview.
**Current coverage:** None. Grep for `PlayClipAtPoint`, `AudioSource.Play`, `source.Play` across `Editor/Tools/` returned zero matches.
**Missing:** Editor-time clip preview / stop. This is a niche capability but high LLM-utility for iteration loops.
**Evidence:** Grep `PlayClipAtPoint|AudioSource\.Play|source\.Play` in `Editor/Tools/` — zero matches.
**Confidence:** high

### G5 — No tool to configure AudioClip import settings
**Workflow:** Adjust how AudioClips are imported: `forceToMono`, `loadType` (DecompressOnLoad / CompressedInMemory / Streaming), `preloadAudioData`, `compressionFormat` (PCM / Vorbis / ADPCM), platform-specific overrides. This is essential for memory budgeting and is a standard Unity workflow.
**Current coverage:** None. Grep `AudioImporter|forceToMono|loadType|preloadAudioData|compressionFormat` across `Editor/Tools/` returned zero matches.
**Missing:** A wrapper around `AudioImporter` (similar in spirit to other importer-based domains).
**Evidence:** Zero matches for the above identifiers across `Editor/Tools/`.
**Confidence:** high

### G6 — No dedicated AudioListener tool
**Workflow:** Add / remove / move an `AudioListener` (typically attached to the active camera or to the player). Multiple listeners in a scene cause a Unity warning, so being able to enumerate and toggle them is useful.
**Current coverage:** Indirect. `Tool_Camera.Create.cs` line 64 implicitly adds an `AudioListener` when creating a camera. `component-add` in the Component domain can add an `AudioListener` by type name, and `component-remove` can remove it. There is no Audio-domain-specific tool.
**Missing:** Dedicated coverage is arguably out of scope (Component covers the basic add/remove). However, there is no way to find/inspect existing listeners in a scene, and no way to detect the "multiple listeners" anti-pattern.
**Evidence:** `Tool_Camera.Create.cs` line 64: `go.AddComponent<AudioListener>();`. No matches for AudioListener-specific configuration tools.
**Confidence:** medium (gap is real, but ownership boundary between Audio and Component domains is a reviewer call)

### G7 — Cannot batch-create multiple AudioSources or assign clip(s) by GUID/AssetReference
**Workflow:** Set up a music system with N tracks pre-loaded into N AudioSources (one per layer), or set up a sound bank where each clip is referenced by GUID rather than by string path (which breaks if the asset is moved).
**Current coverage:** None — `audio-create` accepts a single `clipPath` string and creates one source.
**Missing:** GUID-based clip resolution (asset paths break on rename); batch creation.
**Evidence:** `Tool_Audio.Create.cs` lines 42–51: signature accepts a single string `clipPath`; `AssetDatabase.LoadAssetAtPath<AudioClip>` resolves by path only.
**Confidence:** medium (batch creation is a "macro tool" nice-to-have; GUID resolution is more important and is a wider project pattern, not Audio-specific)

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort).

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | A3 / D3 | Ambiguity / Default | 4 | 1 | 20 | high | `pitch = -1` sentinel collides with a valid Unity reverse-playback value; silently drops legitimate input. |
| 2 | G1 | Capability Gap | 5 | 2 | 20 | high | No mixer routing — entire `outputAudioMixerGroup` workflow is unwrapped. |
| 3 | G3 | Capability Gap | 4 | 1 | 20 | high | No read-only inspector tool for AudioSource state; LLM must mutate to learn. |
| 4 | A1 / D4 | Ambiguity / Default | 4 | 2 | 16 | high | `playOnAwake` / `loop` typed as `int` tri-state; `audio-create` uses `bool` — inconsistency is LLM-confusing. |
| 5 | G2 | Capability Gap | 4 | 2 | 16 | high | Spatial / rolloff / bypass / priority / stereoPan — large unwrapped surface on `AudioSource`. |
| 6 | D2 | Default | 3 | 1 | 15 | high | `audio-configure` requires one of `instanceId`/`objectPath` but neither is documented as required. |
| 7 | G5 | Capability Gap | 3 | 3 | 9 | high | No `AudioImporter` tool; clip import settings cannot be scripted. |
| 8 | A5 / A6 | Ambiguity | 2 | 1 | 10 | medium | `audio-create` and `audio-configure` lack disambiguation wording; `audio-configure` doesn't say "clip not editable here". |
| 9 | G4 | Capability Gap | 2 | 3 | 6 | high | No editor-time clip preview / play. |
| 10 | G7 | Capability Gap | 2 | 4 | 4 | medium | No GUID-based clip resolution; no batch create. |
| 11 | G6 | Capability Gap | 2 | 4 | 4 | medium | No dedicated AudioListener tool — partially covered by Component domain. |
| 12 | A4 | Ambiguity | 2 | 1 | 10 | medium | `volume` clamping not documented. |

---

## 7. Notes

- **Cross-domain dependency:** Adding an `AudioSource` to an existing GameObject is currently done via `component-add` (Component domain), not Audio. `audio-create` only handles the "new GameObject + AudioSource together" path. Reviewers should decide whether the Audio domain should also expose an "add AudioSource to existing GO" tool, or whether the cross-domain pattern is acceptable.
- **Sentinel pattern is unusual:** the `-1` / `0` / `1` tri-state encoding in `audio-configure` is, as far as I checked, not common in this codebase. A wider project-level decision on how to express "skip / unchanged" semantics would benefit more domains than just Audio. Out of scope for this audit but worth flagging to the planner.
- **Open question for the reviewer:** does the MCP framework's parameter schema layer support `bool?` / nullable types? If yes, D4 is trivially fixable. If no, the encoding decision is harder. I did not investigate this — it belongs to the planner / framework owner.
- **Audit limit:** I did not run a full Unity-API surface diff against `AudioSource`. The "missing fields" list in G2 is drawn from the standard Unity inspector, not from a reflection-based exhaustive list. There may be additional fields (e.g. `panStereo`, `velocityUpdateMode`) not enumerated above. For a comprehensive list, a separate API-surface comparison would be needed.
