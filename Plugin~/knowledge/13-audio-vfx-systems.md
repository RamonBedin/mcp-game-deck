# 13 — Audio, VFX & Game Feel Systems for Unity

> Practical guide to audio systems architecture, visual effects, and "juice" for Unity games, with a focus on mobile performance and mass combat.

---

## Table of Contents

1. [Audio System](#1-audio-system)
2. [VFX System](#2-vfx-system)
3. [Juice / Game Feel](#3-juice--game-feel)
4. [Performance & Budgets](#4-performance--budgets)
5. [Recommended Assets & Packages](#5-recommended-assets--packages)
6. [Sources & References](#6-sources--references)

---

## 1. Audio System

### 1.1 Audio Manager Pattern (ScriptableObject-Based)

The recommended architecture separates **audio data** (clips, volumes, pitch ranges) from **playback logic**. ScriptableObjects serve as configuration containers, while a MonoBehaviour singleton manages execution.

**Pattern structure:**

```
AudioManager (MonoBehaviour Singleton)
├── DontDestroyOnLoad()
├── References configuration ScriptableObjects
├── Manages AudioSource pool
└── Exposes public API: Play(), Stop(), FadeIn(), FadeOut()

SoundData (ScriptableObject)
├── AudioClip reference
├── Volume range (min/max)
├── Pitch range (min/max)
├── Priority (0-255)
├── Spatial blend (2D/3D)
├── Mixer group assignment
└── Loop flag
```

**Implementation example — SoundData ScriptableObject:**

```csharp
[CreateAssetMenu(menuName = "Audio/Sound Data")]
public class SoundData : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitchMin = 0.9f;
    [Range(0.1f, 3f)] public float pitchMax = 1.1f;
    [Range(0, 255)] public int priority = 128;
    [Range(0f, 1f)] public float spatialBlend = 0f;
    public AudioMixerGroup mixerGroup;
    public bool loop = false;
}
```

**Example — AudioManager Singleton:**

```csharp
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField] private int poolSize = 16;
    private Queue<AudioSource> sourcePool;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializePool();
    }

    private void InitializePool()
    {
        sourcePool = new Queue<AudioSource>();
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject($"AudioSource_{i}");
            go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            go.SetActive(false);
            sourcePool.Enqueue(source);
        }
    }

    public void Play(SoundData data, Vector3? position = null)
    {
        if (sourcePool.Count == 0) return;

        var source = sourcePool.Dequeue();
        source.gameObject.SetActive(true);
        source.clip = data.clip;
        source.volume = data.volume;
        source.pitch = Random.Range(data.pitchMin, data.pitchMax);
        source.priority = data.priority;
        source.spatialBlend = data.spatialBlend;
        source.outputAudioMixerGroup = data.mixerGroup;
        source.loop = data.loop;

        if (position.HasValue)
            source.transform.position = position.Value;

        source.Play();
        if (!data.loop)
            StartCoroutine(ReturnToPool(source, data.clip.length));
    }

    private IEnumerator ReturnToPool(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
        source.gameObject.SetActive(false);
        sourcePool.Enqueue(source);
    }
}
```

**Advantages of the ScriptableObject pattern:**
- Designers edit sounds without touching code
- Data persists across scenes and is reusable
- Testable — swap SOs to test variations
- Decoupled — MonoBehaviour does not know the details of individual sounds

### 1.2 Audio Pooling (AudioSource Pools)

Instantiating and destroying AudioSources at runtime causes **allocation spikes** and **GC pressure**, producing stuttering — especially on mobile.

**Pool rules:**
- Pre-instantiate N disabled AudioSources in Awake
- Use Queue<AudioSource> (FIFO) for distribution
- On play: dequeue → configure → activate → play
- On finish: deactivate → enqueue back
- Each AudioSource consumes ~3,500 bytes of memory minimum

**Limitations and solutions:**
- AudioSource has no "stopped" event — use a coroutine with `WaitForSeconds(clip.length)` or poll `isPlaying`
- A fixed pool can be exhausted — implement a fallback: steal the source with the lowest priority/volume
- For many SFX variations, use a generic pool + separate configuration data (SoundData SO)

### 1.3 Mixer Groups: Music, SFX, UI, Ambient

**Recommended hierarchy in the AudioMixer:**

```
Master
├── Music       (background music, loops)
├── SFX         (gameplay sounds, hits, explosions)
├── UI          (clicks, hovers, notifications)
└── Ambient     (environment, wind, rain, crowds)
```

**Setup in Unity:**
1. Window → Audio → Audio Mixer → Create
2. Add child groups to Master
3. On each AudioSource, assign the correct `outputAudioMixerGroup`
4. Expose parameters to script: right-click on the group volume → "Expose to script"
5. Name them clearly: `MusicVolume`, `SFXVolume`, etc.

**Script control:**

```csharp
// Game options slider
public void SetMusicVolume(float sliderValue)
{
    // Convert linear (0-1) to dB (-80 to 0)
    float dB = sliderValue > 0.001f
        ? Mathf.Log10(sliderValue) * 20f
        : -80f;
    audioMixer.SetFloat("MusicVolume", dB);
}
```

### 1.4 Ducking, Crossfade & Priority System

**Ducking** — automatically reduce the volume of one group when another plays:

1. On the Music group: Add Effect → Duck Volume
2. On the SFX group: Add Effect → Send → target = Music Duck Volume
3. Configure: Attack Time = 0s, Release Time = ~0.5s, Threshold, Ratio

**Music crossfade:**

```csharp
public IEnumerator CrossfadeMusic(AudioClip newClip, float duration = 2f)
{
    AudioSource fadeOut = currentMusicSource;
    AudioSource fadeIn = GetAlternateSource();

    fadeIn.clip = newClip;
    fadeIn.volume = 0f;
    fadeIn.Play();

    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        fadeOut.volume = Mathf.Lerp(1f, 0f, t);
        fadeIn.volume = Mathf.Lerp(0f, 1f, t);
        yield return null;
    }

    fadeOut.Stop();
    currentMusicSource = fadeIn;
}
```

**Priority System:**
- Range: 0 (highest) to 255 (lowest)
- When the voice limit is reached, Unity "virtualizes" (silences but tracks) lower-priority sounds
- If priorities are equal, the quietest sound is virtualized first
- Recommendations: Music = 0, UI = 32, important SFX = 64, ambient SFX = 128, distant SFX = 200+

### 1.5 Spatial Audio (3D Sound, Rolloff)

**Basic configuration:**
- `spatialBlend = 1f` for full 3D sound
- `minDistance`: distance at which attenuation begins (default: 1m)
- `maxDistance`: distance at which volume reaches minimum (default: 500m)

**Rolloff modes:**
- **Logarithmic** (default): Natural, realistic falloff. Good for most cases.
- **Linear**: Constant falloff. More predictable, good for gameplay.
- **Custom Curve**: Full control via Animation Curve in the Inspector.

**Recommendations by type:**
- Footsteps/voices: minDist = 1, maxDist = 15-20, Logarithmic
- Explosions: minDist = 5, maxDist = 100, Logarithmic
- Ambient loops: minDist = 2, maxDist = 30, Custom (smooth plateau)
- UI: spatialBlend = 0 (always 2D)

**HRTF (Head-Related Transfer Function) for VR/immersion:**
- Steam Audio: binaural rendering via HRTF, supports custom SOFA files
- Microsoft HRTF: included in Windows Mixed Reality
- Enable: check "Spatialize" on the AudioSource + Spatial Blend = 1

### 1.6 Audio Snapshots by Game State

Snapshots capture the complete state of all AudioMixer parameters and allow smooth transitions between game states.

**Typical usage:**

```csharp
[SerializeField] private AudioMixerSnapshot gameplaySnapshot;
[SerializeField] private AudioMixerSnapshot pauseSnapshot;
[SerializeField] private AudioMixerSnapshot lowHealthSnapshot;
[SerializeField] private AudioMixerSnapshot menuSnapshot;

public void OnGameStateChanged(GameState newState)
{
    float transitionTime = 0.5f;
    switch (newState)
    {
        case GameState.Gameplay:
            gameplaySnapshot.TransitionTo(transitionTime);
            break;
        case GameState.Paused:
            pauseSnapshot.TransitionTo(transitionTime);
            break;
        case GameState.LowHealth:
            lowHealthSnapshot.TransitionTo(0.2f); // fast transition
            break;
        case GameState.Menu:
            menuSnapshot.TransitionTo(1f);
            break;
    }
}
```

**Common snapshots:**
- **Gameplay**: normal volumes on all groups
- **Paused**: music low, SFX muted, UI normal
- **Low Health**: low-pass filter on Master, music and ambient reduced
- **Menu**: music normal, SFX and ambient muted
- **Cutscene**: music high, selective SFX, ambient low

**Performance note:** Snapshots affect all mixer parameters — for complex projects, consider multiple smaller AudioMixers instead of one large one.

### 1.7 Performance: Voices, Compression & Mobile

**Simultaneous voice limit:**
- Default: **32 voices** (Project Settings → Audio → Max Real Voices)
- Configurable up to ~256, but mobile rarely needs more than 24-32
- Each "voice" = one AudioSource playing (PlayOneShot counts as a separate voice)

**Compression format — when to use each:**

| Format | Ratio | CPU Decode | Recommended Use |
|--------|-------|------------|-----------------|
| **Vorbis** | ~10:1 | Medium | Default for everything on mobile, music, long SFX |
| **ADPCM** | 3.5:1 | Low | Short, frequent SFX (shots, footsteps), no decoding spike |
| **PCM** | 1:1 | None | Very short SFX (<1s) where quality is critical |
| **MP3** | ~10:1 | Medium | Legacy, avoid in new projects |

**Load Type:**
- **Decompress On Load**: best for short SFX (< 200KB compressed), no CPU cost on play
- **Compressed In Memory**: best for long music/ambient, uses CPU when playing
- **Streaming**: best for very large files (> 1MB), low RAM usage

**Mobile budget:**
- Total audio: **~2ms** per frame
- Max simultaneous voices: **16-24** for low-end mobile
- Use Vorbis quality ~50-70% for a size/quality balance
- Mono for 3D SFX (stereo is wasteful for spatial audio)

---

## 2. VFX System

### 2.1 Particle System vs VFX Graph — When to Use Each

| Aspect | Built-in Particle System | Visual Effect Graph |
|--------|--------------------------|---------------------|
| **Processing** | CPU | GPU |
| **Particles** | Thousands (practical limit) | **Millions** |
| **Editor** | Inspector modules | Visual node graph |
| **Scripting** | Full C# per-particle | Limited to the graph |
| **Physics** | Gravity, collisions, triggers | No Physics integration |
| **Mobile** | Works on all devices | Requires modern GPU (not recommended for low-end mobile) |
| **Render Pipeline** | Built-in, URP, HDRP | **URP (6+) and HDRP only** |

**Use Built-in Particle System when:**
- Target is **mobile** or low-end hardware
- You need **physics collision** (particles bouncing off objects)
- Effects with **a few thousand particles** are sufficient
- You want direct **C# per-particle control**
- The project uses the **Built-in Render Pipeline**

**Use VFX Graph when:**
- You need **millions of particles** (volumetric smoke, dense rain)
- Target is **PC/Console** with a modern GPU
- You want complex **visual node-based authoring**
- Mass combat with **hundreds of simultaneous explosions**
- The project uses **HDRP** or **URP 6+**

### 2.2 Object Pooling of Particle Systems

Same logic as audio pooling — avoid Instantiate/Destroy at runtime.

```csharp
public class VFXPool : MonoBehaviour
{
    [SerializeField] private ParticleSystem prefab;
    [SerializeField] private int poolSize = 20;
    private Queue<ParticleSystem> pool;

    private void Awake()
    {
        pool = new Queue<ParticleSystem>();
        for (int i = 0; i < poolSize; i++)
        {
            var ps = Instantiate(prefab, transform);
            ps.gameObject.SetActive(false);
            pool.Enqueue(ps);
        }
    }

    public ParticleSystem Get(Vector3 position, Quaternion rotation)
    {
        if (pool.Count == 0) return null;

        var ps = pool.Dequeue();
        ps.transform.SetPositionAndRotation(position, rotation);
        ps.gameObject.SetActive(true);
        ps.Clear();           // Clear residual particles
        ps.Simulate(0, true); // Full simulation reset
        ps.Play();

        StartCoroutine(ReturnAfterDuration(ps));
        return ps;
    }

    private IEnumerator ReturnAfterDuration(ParticleSystem ps)
    {
        yield return new WaitUntil(() => !ps.isPlaying);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ps.gameObject.SetActive(false);
        pool.Enqueue(ps);
    }
}
```

**Critical tips:**
- Always call `Clear()` + `Simulate(0)` before reusing — prevents ghost bursts
- Each ParticleSystem uses ~3,500 bytes of memory minimum
- For many variations, pool a generic system and change parameters via `ParticleSystem.MainModule`
- Unity 2021+ has a built-in `ObjectPool<T>` — consider using it as a base

### 2.3 VFX for Mass Combat

For games with mass combat (simultaneous hits, explosions, projectiles), the key is **batching and LOD**:

**Layered strategy:**

```
Layer 1 — Close impacts (< 10m from camera)
├── Full particle system with submeshes
├── Distortion shader, temporary point light
├── Audio with high priority
└── Budget: 50-200 particles per impact

Layer 2 — Mid-range impacts (10-30m)
├── Simplified particle system
├── No distortion, no light
├── Audio with medium priority
└── Budget: 10-30 particles per impact

Layer 3 — Distant impacts (> 30m)
├── Single billboard sprite or 4-8 frame flipbook
├── No particles, no individual audio
├── Batched in sprite atlas
└── Budget: 1 quad per impact
```

**Optimization techniques for mass combat:**
- **GPU Instancing**: enable on particle materials (Particles/Standard Surface shader)
- **Sprite Atlas**: combine all VFX textures into one atlas to reduce draw calls
- **VFX Graph Instancing**: a single VFX Graph with multiple spawn events is more efficient than multiple graphs
- **Mesh Particles**: for projectiles, a single instanced mesh is cheaper than a particle trail
- **Aggressive culling**: disable VFX outside the frustum and beyond the maximum distance

### 2.4 Shader Graph — Essential Effects

#### Dissolve Shader

```
Noise Texture (Perlin/Voronoi)
    → Sample Texture 2D
    → Compare with Threshold (float 0-1)
    → Alpha Clip: pixels above the threshold are discarded
    → Optional: edge color (emissive) on pixels near the threshold
```

**Simplified implementation:**
- `_DissolveAmount` property (Range 0-1) controls progression
- Noise texture defines the dissolution pattern
- Edge glow: `step(noise, threshold) - step(noise, threshold - edgeWidth)` × emissive color

#### Hit Flash / Damage Shader

```csharp
// In the script, on receiving damage:
material.SetFloat("_FlashAmount", 1f);
// Tween back to 0 in ~0.1s
DOTween.To(() => 1f, x => material.SetFloat("_FlashAmount", x), 0f, 0.1f);
```

**In Shader Graph:**
- Lerp between original albedo and flash color (white) using `_FlashAmount`
- `Lerp(originalColor, flashColor, _FlashAmount)` → Base Color

#### Outline Effect

Two approaches:
1. **Two-pass**: render the object scaled with a solid color + normal object on top (simple but doubles draw calls)
2. **Post-process**: Sobel edge detection on the depth/normal buffer (more efficient, affects everything)

**Two-pass in Shader Graph:**
- Pass 1: vertex position += normal × outlineWidth, color = outlineColor, cull front faces
- Pass 2: normal render

#### Damage Shader (Progressive)

Combine dissolve + red tint + progressive emission as HP decreases:

```
Normalized HP (0-1) as input
├── Tint: Lerp(originalColor, red, 1 - hp)
├── Emission: intensity increases as hp decreases
├── Dissolve: activates below 20% HP
└── Crack texture: opacity = 1 - hp
```

### 2.5 Sprite-Based VFX vs GPU Particles

| Aspect | Sprite Flipbook VFX | GPU Particles |
|--------|---------------------|---------------|
| **Mobile performance** | Excellent (texture playback only) | Depends on GPU |
| **Memory** | Texture atlas (fixed) | Dynamic buffers |
| **Dynamism** | None (pre-baked animation) | Full (real-time simulation) |
| **Scalability** | Limited (fixed resolution) | Excellent |
| **Art style** | Perfect for pixel art / cartoon | Better for realistic/volumetric |
| **Production cost** | High (each frame drawn/rendered) | Low (configure parameters) |

**Practical recommendation:**
- **Mobile casual/pixel art**: 90% sprite flipbooks + 10% simple particle systems
- **Mobile mid-core**: 60% sprite + 40% CPU particles
- **PC/Console**: 30% sprite (stylized) + 70% GPU particles (VFX Graph)
- **Best of both worlds**: flipbook for small/frequent effects, particles for large/rare effects

### 2.6 Screen Effects: Shake, Chromatic Aberration, Vignette

**Setup with URP Post Processing:**

1. Add a Volume component to a GameObject
2. Create a Volume Profile
3. Add overrides: Chromatic Aberration, Vignette, Bloom, etc.

**Screen Shake via Cinemachine (recommended):**

```csharp
using Cinemachine;

public class ScreenShakeManager : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;

    public void ShakeOnHit(float intensity = 1f)
    {
        impulseSource.GenerateImpulse(intensity);
    }

    public void ShakeOnExplosion(Vector3 position, float intensity = 3f)
    {
        impulseSource.GenerateImpulseAt(position,
            Vector3.one * intensity);
    }
}
```

**Cinemachine Impulse has 3 types:**
- **PerlinShake**: continuous noise (earthquake tremor)
- **BounceShake**: bouncing movement (impact)
- **KickShake**: single directional impulse (weapon recoil)

**Dynamic chromatic aberration:**

```csharp
using UnityEngine.Rendering.Universal;

public void PulseAberration(float intensity = 0.5f, float duration = 0.2f)
{
    volume.profile.TryGet(out ChromaticAberration ca);
    ca.intensity.Override(intensity);
    DOTween.To(() => intensity, x => ca.intensity.Override(x), 0f, duration);
}
```

**Vignette for damage/low health:**

```csharp
public void UpdateHealthVignette(float hpNormalized)
{
    volume.profile.TryGet(out Vignette vignette);
    // Intensify vignette as HP decreases
    float intensity = Mathf.Lerp(0.45f, 0.15f, hpNormalized);
    vignette.intensity.Override(intensity);
    // Change color to red at low HP
    vignette.color.Override(Color.Lerp(Color.red, Color.black, hpNormalized));
}
```

---

## 3. Juice / Game Feel

### 3.1 Tweening — DOTween, LeanTween, PrimeTween

| Lib | GC Alloc | Performance | Ecosystem | Status |
|-----|----------|-------------|-----------|--------|
| **DOTween** | Some | Good | Huge (tutorials, Pro editor) | Mature, active |
| **LeanTween** | Zero (internal pooling) | Good | Moderate | Mature, maintenance |
| **PrimeTween** | **Zero** | **Best** | Growing | **Recommended for new projects** |
| **LitMotion** | **Zero** | **Best** | Small | Emerging |

**Recommendation:**
- **New project**: PrimeTween (zero-alloc, safe if the object is destroyed mid-tween)
- **Existing project with DOTween**: keep DOTween (migrating is not worth the effort)
- **Mobile performance-critical**: PrimeTween or LitMotion

**Examples with DOTween (most documented):**

```csharp
using DG.Tweening;

// Scale punch (hit feedback)
transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 10, 1f);

// Move with ease
transform.DOMove(targetPos, 0.5f).SetEase(Ease.OutBack);

// Juice sequence
Sequence seq = DOTween.Sequence();
seq.Append(transform.DOScale(1.2f, 0.1f));          // grow
seq.Append(transform.DOScale(0.9f, 0.1f));          // shrink
seq.Append(transform.DOScale(1f, 0.15f));            // settle
seq.Join(spriteRenderer.DOColor(Color.white, 0.1f)); // flash
```

**Links:**
- DOTween: https://dotween.demigiant.com/
- PrimeTween: https://github.com/KyryloKuzyk/PrimeTween
- LitMotion: https://github.com/AnnulusGames/LitMotion

### 3.2 Squash & Stretch, Anticipation, Follow-Through

The 12 principles of animation applied to gameplay:

**Squash & Stretch:**
- Golden rule: **preserve volume** (stretch Y → flatten X)
- Jump: vertical stretch during ascent → squash at apex → strong squash on landing
- Attack: stretch in the attack direction → squash on impact

```csharp
// Example: landing squash & stretch
public IEnumerator LandingSquash()
{
    // Squash on impact
    transform.DOScale(new Vector3(1.3f, 0.7f, 1.3f), 0.05f);
    yield return new WaitForSeconds(0.05f);
    // Bounce back
    transform.DOScale(new Vector3(0.9f, 1.1f, 0.9f), 0.08f);
    yield return new WaitForSeconds(0.08f);
    // Settle
    transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutElastic);
}
```

**Anticipation (Telegraphing):**
- Preparatory movement **before** the main action
- In games: attack windup, crouch before a jump, recoil before a dash
- For enemies: the more lethal the attack, **the longer the anticipation** (gives the player time to react)
- Typically 2-6 frames (~33-100ms) for fast actions, 10-30 frames for heavy attacks

**Follow-Through:**
- Secondary elements **continue** after the main action stops
- Hair, clothing, weapon swaying after movement
- In games: slight overshoot + settle back (ease out elastic/back)
- Caution: overly long follow-through makes the game feel "unresponsive"

### 3.3 Hit Stop / Freeze Frame

"Hit stop" is the 2-8 frame pause at the moment of impact — an iconic technique from fighting games and action titles.

**Basic implementation:**

```csharp
public class HitStopManager : MonoBehaviour
{
    public static HitStopManager Instance;
    private bool isStopped = false;

    public void DoHitStop(float duration = 0.05f) // ~3 frames at 60fps
    {
        if (isStopped) return;
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isStopped = true;
        Time.timeScale = 0f;

        // WaitForSecondsRealtime ignores timeScale
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        isStopped = false;
    }
}
```

**Recommended durations:**
- Light hit: 0.03-0.05s (~2-3 frames)
- Medium hit: 0.05-0.08s (~3-5 frames)
- Heavy hit / kill: 0.08-0.15s (~5-9 frames)
- Ultra/finisher: 0.15-0.3s (dramatic)
- Above 0.2s starts to feel "frozen" — use with care

**Caveats with Time.timeScale = 0:**
- Animations stop (unless they use `Animator.updateMode = UnscaledTime`)
- Physics halts completely
- UI must use `Time.unscaledDeltaTime`
- Alternative: instead of a global timeScale, slow down only the attacker and target via animation

### 3.4 Camera Punch / Kick

**Camera Kick** = directional impulse of the camera in the direction of the impact.

```csharp
// With Cinemachine Impulse:
public void CameraKick(Vector3 direction, float force = 0.5f)
{
    // direction = vector from attacker to target
    impulseSource.GenerateImpulse(direction.normalized * force);
}

// Without Cinemachine (manual):
public IEnumerator CameraKickRoutine(Vector3 direction,
    float force = 0.3f, float returnSpeed = 10f)
{
    Vector3 originalPos = cam.transform.localPosition;
    Vector3 kickPos = originalPos + direction.normalized * force;

    cam.transform.localPosition = kickPos; // Instant snap

    // Smooth return
    float t = 0f;
    while (t < 1f)
    {
        t += Time.deltaTime * returnSpeed;
        cam.transform.localPosition = Vector3.Lerp(kickPos, originalPos,
            Mathf.SmoothStep(0, 1, t));
        yield return null;
    }
    cam.transform.localPosition = originalPos;
}
```

**Ideal feedback combination per impact:**

```
Hit Connect:
├── Hit Stop (0.05s)
├── Camera Kick (direction of the strike)
├── Camera Shake (light, 0.1s)
├── Hit Flash (white sprite, 0.05s)
├── Chromatic Aberration pulse (0.1s)
├── Particle burst (blood/sparks)
├── SFX with randomized pitch
└── Squash & stretch on the target
```

---

## 4. Performance & Budgets

### 4.1 Particle Count Budgets — Mobile

**General budget: ~2ms per frame for all particle effects combined.**

| Device Tier | Max On-Screen Particles | Max Active Particle Systems | Notes |
|-------------|-------------------------|-----------------------------|-------|
| **Low-end** (< 2GB RAM) | 200-500 | 5-10 | Prefer sprite flipbooks |
| **Mid-range** (2-4GB) | 500-2,000 | 10-20 | Mix sprites + particles |
| **High-end** (> 4GB) | 2,000-5,000 | 20-40 | Full CPU particles |

**Practical rule per effect:**
- Simple impact: 5-15 particles, 0.3-0.5s duration
- Small explosion: 20-50 particles
- Large explosion: 50-150 particles
- Continuous smoke/fire: 10-30 particles active simultaneously
- Projectile trail: 5-10 particles

**Aggressive optimization (real case):** a smoke effect was reduced from 1,000 to 30 particles while maintaining acceptable visual quality through larger textures and better lifetime/size curve configuration.

### 4.2 GPU vs CPU Particles

| Aspect | CPU Particles (Built-in) | GPU Particles (VFX Graph) |
|--------|--------------------------|---------------------------|
| **Throughput** | ~10K particles without severe impact | ~100K-1M+ particles |
| **Collision** | Yes (physics engine) | No (forces only in the graph) |
| **Per-particle logic** | Full C# | Limited to the graph |
| **Mobile** | Recommended | Not recommended (low-end) |
| **Render pipeline** | All | URP 6+ / HDRP |
| **GPU Instancing** | Yes (enable on material) | Native |

**To enable GPU Instancing on the Built-in Particle System:**
1. Create a material with the "Particles/Standard Surface" shader
2. Enable "Enable GPU Instancing" on the material
3. Assign it to the Particle System Renderer

### 4.3 Shader Complexity on Mobile

Mobile is **fillrate-bound**: `fillrate = pixels on screen × shader complexity × overdraw`

**Golden rules:**
- Use **Unlit** whenever possible for VFX (particles don't need lighting)
- URP/Simple Lit is cheaper than Standard/Lit
- Avoid: `pow()`, `exp()`, `log()`, `sin()`, `cos()`, `tan()` — use **lookup textures**
- Move expensive calculations to the **vertex shader** (runs N times per vertex, not per pixel)
- Minimize **texture samples** per shader (ideal: 1-2 for mobile)
- **Overdraw** is the biggest enemy in VFX — overlapping transparent particles multiply the cost

**Relative complexity:**

```
Unlit (1x base cost)
└── Simple Lit (~2-3x)
    └── Standard Lit (~4-6x)
        └── Custom with multiple samples (~8-15x)
            └── Custom with complex math (~15-30x)
```

**Analysis tool:** Mali Offline Compiler (ARM) to measure the real cost of shaders on mobile GPUs.

### 4.4 VFX LOD (Level of Detail)

**Implementation for particles:**

```csharp
public class VFXLodController : MonoBehaviour
{
    [SerializeField] private ParticleSystem fullDetailPS;     // LOD0
    [SerializeField] private ParticleSystem reducedDetailPS;  // LOD1
    [SerializeField] private float lodDistance = 20f;
    [SerializeField] private float cullDistance = 40f;

    private Transform cam;

    private void Update()
    {
        float dist = Vector3.Distance(transform.position, cam.position);

        if (dist > cullDistance)
        {
            fullDetailPS.gameObject.SetActive(false);
            reducedDetailPS.gameObject.SetActive(false);
        }
        else if (dist > lodDistance)
        {
            fullDetailPS.gameObject.SetActive(false);
            reducedDetailPS.gameObject.SetActive(true);
        }
        else
        {
            fullDetailPS.gameObject.SetActive(true);
            reducedDetailPS.gameObject.SetActive(false);
        }
    }
}
```

**LOD strategies for VFX:**
- **LOD0** (close): full particles + submeshes + distortion + light
- **LOD1** (medium): 50% fewer particles, no distortion, no light
- **LOD2** (far): single billboard sprite or completely disabled
- **VFX Graph**: use the "Resolution" block to control quality
- **Most common approach**: simply disable/pause VFX beyond a certain distance

---

## 5. Recommended Assets & Packages

### Audio

| Package | Type | Price | Link |
|---------|------|-------|------|
| **FMOD** | Full middleware | Free < $200K budget | https://www.fmod.com/unity |
| **Wwise** | Professional middleware | Free < 200 assets | https://www.audiokinetic.com/products/wwise/ |
| **Master Audio 2024** | Asset Store manager | Paid | Asset Store |

### Tweening & Game Feel

| Package | Type | Price | Link |
|---------|------|-------|------|
| **DOTween** | Tweening | Free (Pro paid) | https://dotween.demigiant.com/ |
| **PrimeTween** | Zero-alloc tweening | Free | https://github.com/KyryloKuzyk/PrimeTween |
| **LitMotion** | Zero-alloc tweening | Free | https://github.com/AnnulusGames/LitMotion |
| **Feel (MMFeedbacks)** | Complete game feel | Paid | https://feel.moremountains.com/ |
| **Juice (KaiClavier)** | Screen shake, hitstop | Paid | https://assetstore.unity.com/packages/tools/particles-effects/super-game-feel-effects-screenshake-kickback-hitstop-88790 |

### VFX & Shaders

| Package | Type | Price | Link |
|---------|------|-------|------|
| **VFX Graph Samples** | Official examples | Free | https://github.com/Unity-Technologies/VisualEffectGraph-Samples |
| **Keijiro VfxGraphAssets** | Advanced subgraphs | Free | https://github.com/keijiro/VfxGraphAssets |
| **Shader Graph Experiments** | URP effects | Free | https://github.com/gamedevserj/Shader-Graph-Experiments |
| **UNI VFX: Missiles & Explosions** | Combat VFX | Paid | https://assetstore.unity.com/packages/vfx/particles/uni-vfx-missiles-explosions-for-visual-effect-graph-249364 |
| **URP Smoke Lighting** | Smoke shaders | Free | https://github.com/peeweek/Unity-URP-SmokeLighting |

### Camera

| Package | Type | Price | Link |
|---------|------|-------|------|
| **Cinemachine** | Camera system | Free (Unity package) | Package Manager |
| **Camera-Shake** | Shake library | Free | https://github.com/gasgiant/Camera-Shake |

---

## 6. Sources & References

### Official Unity Documentation
- [Audio Mixer Manual](https://docs.unity3d.com/Manual/AudioMixer.html)
- [Choosing Your Particle System](https://docs.unity3d.com/6000.3/Documentation/Manual/ChoosingYourParticleSystem.html)
- [Audio Compression](https://docs.unity3d.com/6000.3/Documentation/Manual/AudioFiles-compression.html)
- [Optimize Shaders](https://docs.unity3d.com/6000.3/Documentation/Manual/SL-ShaderPerformance.html)
- [LOD Introduction](https://docs.unity3d.com/6000.2/Documentation/Manual/LevelOfDetail.html)
- [Cinemachine Noise/Shake](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html)

### Unity Learn
- [Audio Mixing](https://learn.unity.com/tutorial/audio-mixing-1)
- [Optimizing Particle Effects for Mobile](https://learn.unity.com/tutorial/optimizing-particle-effects-for-mobile-applications)
- [Optimizing Shaders for Mobile](https://learn.unity.com/course/3d-art-optimization-for-mobile-gaming-5474)
- [Working with LODs](https://learn.unity.com/tutorial/working-with-lods-2019-3)
- [Best Practices Immersive Audio VR](https://learn.unity.com/tutorial/best-practices-for-immersive-audio-in-vr)

### Community & Tutorials
- [ScriptableObject Audio Management — Medium](https://medium.com/audio-implementation-2023/scriptable-objects-gateway-to-data-driven-unity-audio-management-c0102fa48528)
- [10 Unity Audio Optimisation Tips — Game Dev Beginner](https://gamedevbeginner.com/10-unity-audio-optimisation-tips/)
- [Dissolve Shader — Daniel Ilett](https://danielilett.com/2020-04-15-tut5-4-urp-dissolve/)
- [12 Principles of Animation in Games — Game Anim](https://www.gameanim.com/2019/05/15/the-12-principles-of-animation-in-video-games/)
- [FMOD vs Wwise 2024 — DrCodes](https://drcodes.com/posts/fmod-vs-wwise-2024-best-audio-middleware-for-aa-game-studios)
- [DOTween vs LeanTween vs PrimeTween — Omitram](https://omitram.com/unity-tweening-guide-dotween-leantween-primetween/)
- [Mobile Game Optimization Tips Part 2 — Unity](https://unity.com/how-to/mobile-game-optimization-tips-part-2)

### GitHub Repositories
- [Unity Audio Pooling — adammyhre](https://github.com/adammyhre/Unity-Audio-Pooling)
- [MMFeedbacks — MoreMountains](https://github.com/reunono/MMFeedbacksPublic)
- [Awesome Unity Open Source](https://github.com/baba-s/awesome-unity-open-source-on-github)
- [Tween Performance Benchmarks](https://github.com/AnnulusGames/TweenPerformance)

---

> **Note:** This guide was compiled as a practical reference for developing audio, VFX, and game feel systems in Unity. Performance budgets are estimates — always profile on real target hardware using the Unity Profiler and Frame Debugger.
