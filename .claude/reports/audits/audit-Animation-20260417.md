# Audit Report — Animation

**Date:** 2026-04-17
**Auditor:** tool-auditor agent
**Scope:** `Editor/Tools/Animation/`
**Status:** ✅ COMPLETE

---

## 0. Audit Quality Caveats

**File accounting:**
- `files_found`: 4 (via Glob of `Editor/Tools/Animation/Tool_Animation.*.cs`)
- `files_read`: 4
- `files_analyzed`: 4

**Balance:** ✅ balanced

**Errors encountered during audit:** None.

**Files not analyzed (if any):** None.

**Absence claims in this report:**
- Balance is clean, so absence claims are in force. Key absences verified by cross-domain Grep (all `Editor/Tools`):
  - `SetObjectReferenceCurve` → 0 matches (no sprite-frame animation support).
  - `AddAnimationEvent` / `SetAnimationEvents` → 0 matches in write path (only `GetAnimationEvents` read in `GetInfo`).
  - `AnimatorControllerParameter` / `AddParameter` → 0 matches (no Bool/Int/Float/Trigger parameter tooling).
  - `AddCondition`, `AnimatorStateTransition`, `BlendTree`, `hasExitTime`, `exitTime` → 0 matches (transitions are created but never parameterized).
  - `RemoveState`, `RemoveTransition`, `AddLayer`/`RemoveLayer` against AnimatorController → 0 matches (only `Tool_Editor.Tags.cs` AddLayer, which handles Unity **tag/sorting layers**, not animator layers).

**Reviewer guidance:**
- The domain is small (4 files, 4 public tools) and internally consistent in style. Findings concentrate on (a) capability gaps in write paths the LLM will hit the moment it tries to automate a real animation workflow, and (b) minor default/description issues. No redundancy clusters exist in this domain — each tool has a distinct role.
- Cross-domain dependency confirmed: attaching an `Animator` to a GameObject relies on the **Component** domain (`component-add`), which is out of scope for this audit but referenced in G3.

---

## 1. Inventory

| Tool ID | Title | File | Params | ReadOnly |
|---------|-------|------|--------|----------|
| `animation-add-keyframe` | Animation / Add Keyframe | `Tool_Animation.AddKeyframe.cs` | 5 | no |
| `animation-configure-controller` | Animation / Configure Controller | `Tool_Animation.ConfigureController.cs` | 7 (action-dispatched) | no |
| `animation-create-clip` | Animation / Create Clip | `Tool_Animation.CreateClip.cs` | 3 | no |
| `animation-get-info` | Animation / Get Info | `Tool_Animation.GetInfo.cs` | 2 | **yes** |

Unity API surface observed:
- `AssetDatabase.LoadAssetAtPath`, `CreateAsset`, `SaveAssets`, `Refresh`, `IsValidFolder`
- `AnimationUtility.GetEditorCurve` / `SetEditorCurve` / `GetCurveBindings` / `GetAnimationEvents` / `SetAnimationClipSettings`
- `EditorCurveBinding`, `AnimationCurve`, `Keyframe`
- `AnimatorController.CreateAnimatorControllerAtPath`, `layers[0].stateMachine`, `AddState`, `AddTransition`, `defaultState`
- `AnimationClip` constructor, `AnimationClipSettings`
- `System.Type` resolution via reflection (UnityEngine assembly + AppDomain scan)

---

## 2. Redundancy Clusters

No redundancy clusters identified. The four tools partition the domain cleanly:
- `create-clip` creates `.anim` assets.
- `add-keyframe` mutates curves inside a clip.
- `configure-controller` is already a consolidated action-dispatched tool covering 4 operations on `.controller` assets (this is the **reference quality** noted in the auditor brief — do not complain about it).
- `get-info` is the sole read-only introspection tool.

---

## 3. Ambiguity Findings

### A1 — `action` valid values partially enumerated but not disambiguated
**Location:** `animation-configure-controller` — `Tool_Animation.ConfigureController.cs` line 44–47
**Issue:** Method-level `[Description]` enumerates `create | add-state | add-transition | set-default`, but the param-level `[Description]` on `action` re-lists them without clarifying **which other params become required per action**. The LLM has to cross-read the method description and individual param notes to figure out that `stateName` does double-duty as "controller name when action == create". That dual role is documented only in XML docs (line 75: "controllerName: Name for the controller (derived from stateName param)"), not in the `[Description]` attributes the LLM actually sees.
**Evidence:** `[Description("State name. Required for add-state, add-transition (fromState/toState), and set-default.")] string stateName = ""` — omits the "also used as controller filename when action=create" behavior that is present in `ExecuteCreate` line 80–83.
**Confidence:** high

### A2 — `propertyPath` description relies on Unity-internal naming without guidance
**Location:** `animation-add-keyframe` — `Tool_Animation.AddKeyframe.cs` line 44
**Issue:** Three examples (`localPosition.x`, `localScale.y`, `m_IsActive`) are given, but the serialized-field convention (`m_IsActive` vs `isActive`) is not explained. LLMs routinely guess wrong between C# property name and serialized field name. There is no note that property paths for `GameObject.activeSelf` must be `m_IsActive`, that color components use `.r/.g/.b/.a`, or that material properties need `material._MainColor` form.
**Evidence:** Description verbatim: `"Animated property path (e.g. 'localPosition.x', 'localScale.y', 'm_IsActive')."`
**Confidence:** high

### A3 — `objectType` reflection scope opaque
**Location:** `animation-add-keyframe` — param `objectType` line 47
**Issue:** Description says "Component type that owns the property" with examples `Transform`, `MeshRenderer`. But `ResolveUnityType` (lines 119–156) falls back to scanning every loaded AppDomain assembly, so user-defined `MonoBehaviour` types also resolve. The description doesn't mention this, so the LLM may not try a user type like `PlayerController` even when that's the correct answer.
**Evidence:** Description: `"Component type that owns the property (e.g. 'Transform', 'MeshRenderer'). Default 'Transform'."`
**Confidence:** medium

### A4 — `GetInfo` "supply clipPath or controllerPath" — behavior when BOTH are supplied is undocumented
**Location:** `animation-get-info` — `Tool_Animation.GetInfo.cs` line 29
**Issue:** Implementation short-circuits to `clipPath` if both are provided (line 42–47). Description doesn't tell the LLM this, so if an LLM passes both paths hoping to get combined info, it will silently receive only the clip info.
**Evidence:** Description: `"Returns metadata for an AnimationClip ... or an AnimatorController ... Supply clipPath or controllerPath."` — "or" is ambiguous.
**Confidence:** medium

### A5 — `duration` unit not linked to frame rate
**Location:** `animation-create-clip` — param `duration` line 34
**Issue:** Description says "Duration of the clip in seconds. Default 1.0." It does not mention that `AnimationClip.frameRate` defaults to 60fps in Unity and that `duration` × `frameRate` gives frame count. LLMs translating "8-frame run cycle" to seconds need this mental model.
**Evidence:** `"Duration of the clip in seconds. Default 1.0."`
**Confidence:** low (borderline — arguably not the tool's job to teach frame math, but the tool also doesn't expose `frameRate` as a param, which is G5 below).
**Confidence:** low

---

## 4. Default Value Issues

### D1 — `action` defaults to `"create"` which is the most destructive option
**Location:** `animation-configure-controller` — param `action` line 47
**Issue:** If the LLM calls the tool with only `stateName` populated (trying to add a state), it silently triggers `create` with the state name becoming the controller name — writing a new `.controller` asset to `Assets/Animations/`. A safer default would either be no default (force explicit), or the neutral read-path. `create` being the default is a trap for a tool whose other three actions all require `controllerPath`.
**Current:** `string action = "create"`
**Suggested direction:** Make `action` required (remove default), so LLM must name the operation. Alternatively, default to an inspection-like `"list-states"` that doesn't mutate anything — but that action doesn't currently exist.
**Confidence:** high

### D2 — `savePath = "Assets/Animations"` assumes a folder the user may not have
**Location:** `animation-create-clip` param `savePath` (line 33) and `animation-configure-controller` param `savePath` (line 52)
**Issue:** Both tools default to `Assets/Animations`. The tools auto-create the folder via `Directory.CreateDirectory(folder) + AssetDatabase.Refresh()`, so this is not a runtime error — but it silently pollutes the project with a new folder if the user's convention is `Assets/Art/Animations` or `Assets/_Project/Anim`. The default is reasonable, but the implicit folder creation side-effect isn't mentioned in any description.
**Current:** `string savePath = "Assets/Animations"`
**Suggested direction:** Document the auto-create behavior in the param description. Not a code change — a description tweak.
**Confidence:** medium

### D3 — `objectType = "Transform"` is the correct default but fragile
**Location:** `animation-add-keyframe` param `objectType` line 47
**Issue:** `"Transform"` is the right default for `localPosition.*`, `localScale.*`, `localRotation.*`, which is ~70% of keyframe use. But it's wrong for `m_IsActive` (which needs `GameObject`), and wrong for any renderer property. The default is pragmatic; however, there is no validation that objectType matches propertyPath, so a call like `(propertyPath: "m_IsActive", objectType: "Transform")` builds a curve Unity silently ignores.
**Current:** `string objectType = "Transform"`
**Suggested direction:** Keep the default. Add a short "common pairings" table to the method description (e.g. `m_IsActive → GameObject`, `material._Color.r → Renderer`).
**Confidence:** medium

### D4 — `duration = 1.0f` does not reflect the common 2D case
**Location:** `animation-create-clip` param `duration` line 34
**Issue:** For 2D sprite animations (a primary use case per the project's test game "Jurassic Survivors — 2D URP roguelike"), clip duration is usually derived from frame-count ÷ fps and varies widely. `1.0s` is a placeholder. No evidence this creates a bug — it's just a weak default. Fine to keep.
**Current:** `float duration = 1.0f`
**Suggested direction:** Leave as-is. Mentioning here only for completeness.
**Confidence:** low

---

## 5. Capability Gaps

### G1 — 2D sprite-frame animation cannot be authored
**Workflow:** Create a 2D sprite animation for a character (e.g. PlayerRun): make AnimationClip with 8 sprite frames → create AnimatorController with state referencing the clip → attach Animator to prefab root.
**Current coverage:** `animation-create-clip` creates the empty clip. `animation-configure-controller` creates the controller + state + transition. `component-add` (Component domain) handles Animator attachment (referenced, not audited here).
**Missing:** **No tool places sprite frames into the clip.** `animation-add-keyframe` only exposes `float value` and calls `AnimationUtility.SetEditorCurve`, which handles numeric curves only. 2D sprite animation in Unity requires `AnimationUtility.SetObjectReferenceCurve` with `ObjectReferenceKeyframe[]` holding `Sprite` references (or raw `UnityEngine.Object` references). Grep across `Editor/Tools` confirms **zero** occurrences of `SetObjectReferenceCurve`.
**Evidence:** `Tool_Animation.AddKeyframe.cs` line 45–46: `[Description("Time in seconds for the keyframe.")] float time, [Description("Value of the property at the specified time.")] float value`. Line 96–99: `var keyframe = new Keyframe(time, value); curve.AddKey(keyframe); AnimationUtility.SetEditorCurve(clip, binding, curve);` — numeric-only API path.
**Confidence:** high (balanced accounting + cross-domain grep confirms absence)

### G2 — Animator parameters cannot be created, read, or used
**Workflow:** Drive a state machine with a `Speed` float parameter or `Jump` trigger: add parameter to controller → make transitions conditional on parameter value.
**Current coverage:** None. `configure-controller` `add-transition` action (lines 202–253) creates a transition but never sets a condition on it.
**Missing:** No access to `AnimatorController.AddParameter(...)`, `RemoveParameter`, `parameters[]` mutation. No way to set `AnimatorCondition` on transitions (`AnimatorStateTransition.AddCondition`). Grep confirms **zero** matches for `AddParameter`, `AnimatorControllerParameter`, `AddCondition`, `AnimatorStateTransition` in write paths. The only hit is `GetInfo` reading `controller.parameters` for display.
**Evidence:** `Tool_Animation.ConfigureController.cs` line 247: `from.AddTransition(to);` — returns an `AnimatorStateTransition`, but the return value is discarded. There is no follow-up tool to configure conditions, exit time, duration, or interruption sources.
**Confidence:** high

### G3 — Transition behavior settings are unreachable
**Workflow:** Create a transition that triggers immediately (no exit-time), with zero duration, and is interruptible.
**Current coverage:** `configure-controller` `add-transition` creates the edge.
**Missing:** No way to set `hasExitTime`, `exitTime`, `duration`, `offset`, `interruptionSource`, `orderedInterruption`, or `canTransitionToSelf`. The default Unity `AddTransition` produces a transition with `hasExitTime = true` and `duration = 0.25s`, which is rarely what's wanted for gameplay (ActionRPG / roguelike style transitions typically want `hasExitTime = false`).
**Evidence:** `Tool_Animation.ConfigureController.cs` line 247: `from.AddTransition(to);` — zero configuration applied. Grep confirms `hasExitTime`, `exitTime` appear **zero** times in the codebase.
**Confidence:** high

### G4 — No mutation beyond add: cannot remove states, transitions, parameters, layers
**Workflow:** Iterate on a controller: rename a state, delete a misnamed state, remove a stale transition, add a second layer for upper-body overrides.
**Current coverage:** None. All `configure-controller` actions are additive or set-once (`set-default`).
**Missing:** No `remove-state`, `remove-transition`, `rename-state`, `add-layer`, `remove-layer` actions. Grep confirms no such tooling exists (the `AddLayer`/`RemoveLayer` hits in `Tool_Editor.Tags.cs` are for **Unity tag/sorting layers**, a completely different system).
**Evidence:** `Tool_Animation.ConfigureController.cs` action switch (line 57–64) covers only `create | add-state | add-transition | set-default`. No delete path.
**Confidence:** high

### G5 — Clip creation cannot set frame rate, loop, or wrap mode
**Workflow:** Create a 2D clip that loops at 12 fps (common for pixel-art).
**Current coverage:** `animation-create-clip` takes `clipName`, `savePath`, `duration`.
**Missing:** `frameRate` is not exposed (the Unity default is 60, which is wrong for most 2D). `loopTime` is hard-coded to `false` at line 82 of `CreateClip.cs`. `wrapMode` is never set. The LLM has no way to author a looping animation in a single step.
**Evidence:** `Tool_Animation.CreateClip.cs` lines 79–91: `AnimationClipSettings { stopTime = duration, loopTime = false, ... }` — every flag is literal/false. No tool edits clip settings after creation either.
**Confidence:** high

### G6 — No way to add or edit `AnimationEvent`s (method-call frames)
**Workflow:** Call `OnFootstep()` at t=0.25s during a walk cycle.
**Current coverage:** `animation-get-info` counts events (line 79). That is the only event-related surface.
**Missing:** No write path for `AnimationUtility.SetAnimationEvents(clip, AnimationEvent[])`. Grep confirms `SetAnimationEvents`, `AddAnimationEvent` match **zero** times anywhere.
**Evidence:** `Tool_Animation.GetInfo.cs` line 70: `var events = AnimationUtility.GetAnimationEvents(clip);` — read-only usage, mirrored nowhere in a write tool.
**Confidence:** high

### G7 — No blend tree support
**Workflow:** Create a 1D blend tree on a state driven by a `Speed` parameter to blend Idle ↔ Walk ↔ Run.
**Current coverage:** None.
**Missing:** No `BlendTree` creation, no way to assign a blend tree as a state's `motion` in place of a clip. Grep confirms `BlendTree` matches **zero** times.
**Evidence:** `Tool_Animation.ConfigureController.cs` line 178: `newState.motion = clip;` — only single-clip motions are assignable.
**Confidence:** high

### G8 — Multi-keyframe batch insertion is impossible
**Workflow:** Place an 8-keyframe curve at t = 0, 0.125, 0.25, ... on one property in one call.
**Current coverage:** `animation-add-keyframe` adds **one** keyframe per invocation.
**Missing:** The LLM must chain 8 tool calls for an 8-frame curve, and there's no batch form. This is a fragmentation gap — each individual call works, but the workflow is a tool-call storm.
**Evidence:** `Tool_Animation.AddKeyframe.cs` line 42–48: signature accepts a single `(time, value)` pair.
**Confidence:** high

---

## 6. Priority Ranking

Priority = Impact × (6 − Effort). Higher = do first.

| # | Finding ID | Category | Impact | Effort | Priority | Confidence | Summary |
|---|-----------|----------|--------|--------|----------|-----------|---------|
| 1 | G1 | Capability Gap | 5 | 3 | 15 | high | No 2D sprite-frame animation path (`SetObjectReferenceCurve` absent). Blocks the primary test-project workflow. |
| 2 | G2 | Capability Gap | 5 | 3 | 15 | high | No animator parameters + no transition conditions = state machines are decorative, not functional. |
| 3 | G3 | Capability Gap | 4 | 2 | 16 | high | Transitions created with Unity defaults (`hasExitTime=true`, `duration=0.25`), unreachable — breaks gameplay transitions. |
| 4 | D1 | Default Issue | 4 | 1 | 20 | high | `action="create"` default on `configure-controller` silently creates controllers on ambiguous calls. |
| 5 | G5 | Capability Gap | 4 | 2 | 16 | high | `create-clip` can't set `frameRate` / `loopTime`; 60fps non-looping is the wrong default for 2D. |
| 6 | G8 | Capability Gap | 3 | 2 | 12 | high | No batch keyframe insert; 8-frame curves require 8 tool calls. |
| 7 | G4 | Capability Gap | 3 | 3 | 9 | high | Cannot remove/rename states, transitions, or layers — iteration workflows blocked. |
| 8 | G6 | Capability Gap | 3 | 2 | 12 | high | No `AnimationEvent` write path (footsteps, hit frames). |
| 9 | A1 | Ambiguity | 3 | 1 | 15 | high | `configure-controller.action` dispatch table doesn't tell LLM that `stateName` doubles as controller filename on `create`. |
| 10 | A2 | Ambiguity | 2 | 1 | 10 | high | `propertyPath` needs serialized-field guidance (`m_IsActive` vs `isActive`). |
| 11 | G7 | Capability Gap | 2 | 4 | 4 | high | No blend tree support. Lower priority — niche for 2D roguelikes. |
| 12 | A3 | Ambiguity | 2 | 1 | 10 | medium | `objectType` reflection scope includes user types; not documented. |
| 13 | A4 | Ambiguity | 2 | 1 | 10 | medium | `get-info` behavior when both paths supplied is undocumented (short-circuits to clip). |
| 14 | D2 | Default Issue | 2 | 1 | 10 | medium | `savePath` auto-creates folders without mentioning it in description. |
| 15 | D3 | Default Issue | 2 | 1 | 10 | medium | `objectType="Transform"` is unvalidated against `propertyPath`. |
| 16 | A5 | Ambiguity | 1 | 1 | 5 | low | `duration` unit/frameRate relationship unstated. |
| 17 | D4 | Default Issue | 1 | 1 | 5 | low | `duration=1.0f` default is weak for 2D but harmless. |

Top decision for the reviewer: items 1–5 collectively prevent the LLM from authoring a functional animation in the test project. G1 + G2 + G3 are the minimum viable bundle for any real gameplay animation; D1 is a one-character fix (remove the default) that eliminates a silent-corruption footgun.

---

## 7. Notes

**Cross-domain dependencies noted (out of scope for this report, but relevant when consolidation is planned):**
- `component-add` (Component domain) is required to attach `Animator` to a prefab or GameObject. An eventual workflow macro "animate-prefab" would span Animation + Component + Prefab domains.
- `Tool_Editor.Tags.cs` has `AddLayer`/`RemoveLayer` but these are **tag/sorting layers**, unrelated to `AnimatorControllerLayer`. Naming collision is worth a mental flag when/if animator layer tooling is added — consider `animation-add-animator-layer` to avoid ambiguity.

**Style consistency observations (not findings, just context):**
- All 4 files follow the same structure: `#region` header, XML docs on the tool method in the `[McpToolType]`-bearing file (AddKeyframe.cs), single-method-per-file partitioning. Consistent with CLAUDE.md conventions.
- `ConfigureController.cs` is a model citizen for action-dispatched consolidation — keep it as a template when proposing new consolidated tools (e.g. `animation-configure-clip` could follow the same pattern to absorb `create-clip`, `add-keyframe`, future `add-event`, future `set-settings` into one action-routed tool).

**Open questions for the reviewer:**
- Is 2D sprite animation (G1) an explicit v1.2 target, or deferred? That decides whether G1 dominates the next sprint.
- Blend trees (G7) — keep deferred or add for completeness?
- Should `get-info` grow a `layer` / `states-detail` mode, or should controller introspection become its own tool?

**Limits of this audit:**
- No runtime behavior tested; all findings are static analysis of source.
- Cross-domain Greps were targeted (sprite curves, parameters, conditions, events, transitions, layers); I did not exhaustively audit the Component domain's support for Animator attachment — only confirmed `Tool_Component.Add.cs` exists.
- I did not inspect the TypeScript MCP proxy or the `MainThreadDispatcher` implementation; all findings are at the tool-surface level Claude sees.
