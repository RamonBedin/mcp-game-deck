# Complete Guide to Unity Mobile Game Optimization (Android/iOS)

> **Version:** 2.0 — April 2026
> **Engine:** Unity 6 / Unity 2022 LTS+ with URP
> **Target audience:** Gameplay programmers and tech artists on mobile projects

---

## Table of Contents

1. [Profiling — Tools and Workflow](#1-profiling--tools-and-workflow)
2. [CPU Optimization](#2-cpu-optimization)
3. [GPU Optimization](#3-gpu-optimization)
4. [Memory Management](#4-memory-management)
5. [Render Pipeline — URP for Mobile](#5-render-pipeline--urp-for-mobile)
6. [Build Settings](#6-build-settings)
7. [Thermal Throttling and Battery](#7-thermal-throttling-and-battery)
8. [Final Release Checklist](#8-final-release-checklist)
9. [Sources and References](#9-sources-and-references)

---

## 1. Profiling — Tools and Workflow

### 1.1 Unity Profiler

The Unity Profiler is the first line of analysis. Key points:

- **CPU Module:** identifies frame time spikes. Mobile target: **< 16.6 ms** (60 FPS) or **< 33.3 ms** (30 FPS).
- **GPU Module:** available via `FrameTimingManager`. Requires URP and a compatible device.
- **Memory Module:** shows managed vs native allocations. Watch for "GC.Alloc" in the timeline.
- **Deep Profile:** enables full instrumentation of *every* method call. Reduces performance by ~10×, so use it only to track specific allocations — never for measuring absolute timing.

**Remote Profiling on a real device:**

```
// 1. Enable in Build Settings:
//    Development Build = ON
//    Autoconnect Profiler = ON
//    Script Debugging = OFF (lower overhead)

// 2. Connect via USB cable or the same Wi-Fi network
// 3. In the Profiler window: "Target" dropdown → select device
```

> **Tip:** On Android, if the Wi-Fi connection fails, use `adb forward tcp:34999 localabstract:Unity-<package>` for USB profiling.

### 1.2 Memory Profiler (Package)

Install via Package Manager (`com.unity.memoryprofiler`). Allows:

- Snapshot comparisons between scenes (find leaks).
- Tree view of native objects (Textures, Meshes, AudioClips).
- **Budget check:** compare "Total Used Memory" against your budget per tier.

### 1.3 Frame Debugger

Essential for understanding what the GPU is drawing:

- Check the number of draw calls per frame. **Mobile target: < 100–200 draw calls.**
- Identify broken batches (different shader variant, material properties, etc.).
- Look for `RenderLoopNewBatcher` entries to confirm the SRP Batcher is active.

### 1.4 Xcode Instruments (iOS)

For native profiling on iOS:

- **Time Profiler:** CPU hotspots in native code (IL2CPP).
- **Allocations:** native memory that the Unity Profiler does not show.
- **Metal System Trace:** GPU utilization, vertex/fragment throughput, thermal state.
- **Energy Log:** diagnoses battery drain.

Connect the device via USB, open Instruments, and select the game process.

### 1.5 Android Studio Profiler

For Android:

- **CPU Profiler:** thread tracing, identify main thread stalls.
- **Memory Profiler:** native heap + Java heap.
- **GPU Inspector (AGI):** from Google, allows Vulkan/GLES frame capture. Download separately at [developer.android.com/agi](https://developer.android.com/agi).

```bash
# Connect profiler via adb
adb forward tcp:34999 localabstract:Unity-com.yourpackage.yourgame
```

---

## 2. CPU Optimization

### 2.1 Object Pooling

Instantiating and destroying objects is expensive (alloc + GC). Use pooling for everything that spawns frequently: projectiles, VFX, enemies, UI popups.

**Unity Built-in (Unity 2021+): `UnityEngine.Pool`**

```csharp
using UnityEngine.Pool;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] private Bullet prefab;

    private ObjectPool<Bullet> _pool;

    private void Awake()
    {
        _pool = new ObjectPool<Bullet>(
            createFunc:      () => Instantiate(prefab),
            actionOnGet:     b  => b.gameObject.SetActive(true),
            actionOnRelease: b  => b.gameObject.SetActive(false),
            actionOnDestroy: b  => Destroy(b.gameObject),
            collectionCheck: false,
            defaultCapacity: 20,   // pre-warm
            maxSize:         100   // cap to avoid memory explosion
        );
    }

    public Bullet Get()  => _pool.Get();
    public void Return(Bullet b) => _pool.Release(b);
}
```

**Rule of thumb:** if an object lives < 5 seconds and spawns > 5×/min, it should be pooled.

### 2.2 Centralized Update Pattern

Each `MonoBehaviour.Update()` has native→managed call overhead (~0.1 ms per 1000 scripts). On mobile this matters.

**Solution: Manager-based Update**

```csharp
public interface ITickable
{
    void Tick(float deltaTime);
}

public class UpdateManager : MonoBehaviour
{
    private static readonly List<ITickable> _tickables = new(256);

    public static void Register(ITickable t)   => _tickables.Add(t);
    public static void Unregister(ITickable t) => _tickables.Remove(t);

    private void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _tickables.Count; i++)
            _tickables[i].Tick(dt);
    }
}

// In your scripts:
public class Enemy : MonoBehaviour, ITickable
{
    private void OnEnable()  => UpdateManager.Register(this);
    private void OnDisable() => UpdateManager.Unregister(this);

    public void Tick(float deltaTime)
    {
        // logic here, without Update() overhead
    }
}
```

**Benchmark:** with 500+ active objects, this approach saves **2–5 ms/frame** on low-end devices.

### 2.3 Garbage Collection — Causes and Prevention

The Mono/IL2CPP GC is **stop-the-world** (Boehm). A GC spike on mobile can cost **5–50 ms**.

**Common allocation causes in hot paths:**

| Cause | Example | Solution |
|-------|---------|---------|
| String concatenation | `"HP: " + hp` | `StringBuilder` or `string.Create` |
| LINQ in Update | `.Where().ToList()` | Manual loop with `for` |
| Boxing of value types | `object o = myInt` | Generics, constrained interfaces |
| Closures / Lambdas | `list.Sort((a,b) => ...)` with capture | Cache the delegate or use a static method |
| `foreach` on legacy collections | `foreach(var x in dict)` | `for` with index or `GetEnumerator()` |
| `params` arrays | `MyMethod(params object[])` | Fixed overloads or `Span<T>` |
| `GetComponent<T>()` in a loop | called every frame | Cache on initialization |

**Incremental GC:**

```
// Project Settings → Player → Other Settings
// ✅ Use Incremental GC

// Adjusts the maximum GC time per frame (ms):
GarbageCollector.incrementalTimeSliceNanoseconds = 3_000_000; // 3ms
```

Incremental GC spreads collection across multiple frames. It does **not eliminate** the problem — it only smooths out spikes. The goal remains **zero allocations in hot paths**.

### 2.4 Job System + Burst Compiler

For heavy calculations (pathfinding, custom physics simulation, mass AI), offload to worker threads with Jobs + Burst.

**Measured real-world performance:** 15–30× speedup on iPhone 13 Pro (A15) and Galaxy S22 (Snapdragon 8 Gen 1).

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct BoidJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> Positions;
    [ReadOnly] public NativeArray<float3> Velocities;
    public NativeArray<float3> NewVelocities;
    public float DeltaTime;
    public float NeighborRadius;

    public void Execute(int index)
    {
        float3 separation = float3.zero;
        float3 alignment  = float3.zero;
        float3 cohesion   = float3.zero;
        int neighbors = 0;

        for (int j = 0; j < Positions.Length; j++)
        {
            if (j == index) continue;
            float dist = math.distance(Positions[index], Positions[j]);
            if (dist < NeighborRadius)
            {
                separation += (Positions[index] - Positions[j]) / dist;
                alignment  += Velocities[j];
                cohesion   += Positions[j];
                neighbors++;
            }
        }

        if (neighbors > 0)
        {
            alignment /= neighbors;
            cohesion = (cohesion / neighbors) - Positions[index];
        }

        NewVelocities[index] = Velocities[index]
            + (separation * 2f + alignment * 1f + cohesion * 1f) * DeltaTime;
    }
}

// Schedule:
var job = new BoidJob
{
    Positions     = positionsNative,
    Velocities    = velocitiesNative,
    NewVelocities = newVelocitiesNative,
    DeltaTime     = Time.deltaTime,
    NeighborRadius = 5f
};
JobHandle handle = job.Schedule(count, 64); // batch size 64
handle.Complete();
```

> **Important:** Burst on mobile generates optimized ARM NEON SIMD code. Use `float3`/`float4` from `Unity.Mathematics` (not `Vector3`) to enable vectorization.

---

## 3. GPU Optimization

### 3.1 Draw Call Batching

| Technique | When to use | Requirements |
|---------|-------------|------------|
| **Static Batching** | Objects that never move | Mark as `Static`; increases memory (copies mesh) |
| **Dynamic Batching** | Meshes with < 300 verts and < 900 attribs | Same material; disabled by default in URP |
| **SRP Batcher** | Everything using compatible shaders | Same shader variant; material properties may differ |
| **GPU Instancing** | Many identical objects (trees, grass) | Same mesh + same material; incompatible with SRP Batcher |

**Priority in URP Mobile:**
1. SRP Batcher ON (default) — highest overall impact.
2. GPU Instancing for massively repeated objects.
3. Static Batching for static scenes.

**Check in Frame Debugger:** look for `SRP Batch` in draw call names. If you see "shader variant mismatch", unify the shader variants.

### 3.2 Texture Compression

| Platform | Recommended format | Quality | Size (1024²) |
|------------|---------------------|-----------|-----------------|
| **Android** | ASTC 6×6 | Great balance | ~230 KB |
| **Android (low-end)** | ETC2 | Good, more compatible | ~340 KB |
| **iOS** | ASTC 6×6 | Standard | ~230 KB |
| **iOS (high quality)** | ASTC 4×4 | Maximum | ~520 KB |

**Best practices:**

- **Always enable mipmaps** on 3D textures. Reduces bandwidth for distant objects.
- **Disable mipmaps** on UI (sprites always displayed at a fixed size).
- **Texture Atlas:** combine small sprites into an atlas (Unity Sprite Atlas or TexturePacker). Reduces draw calls and improves batching.
- **Power of 2:** dimensions like 512, 1024, 2048 compress more efficiently.
- **Max Size per platform:** limit textures to 1024 on mobile, 2048 only for heroes/main character.

### 3.3 Shader Optimization

```
// AVOID on mobile:
// - Standard Shader (Built-in) — too heavy
// - Tessellation
// - Geometry shaders
// - Multiple passes (multi-pass)

// USE:
// - URP/Lit with minimal complexity
// - URP/SimpleLit for secondary objects
// - URP/Unlit for UI and particles
// - Custom shaders with optimized Shader Graph
```

**Shader Variants — the hidden enemy:**

- Each active keyword multiplies variants. 10 keywords = 1024 possible variants.
- Use `#pragma shader_feature_local` instead of `#pragma multi_compile` when possible.
- Check with **Edit → Project Settings → Graphics → Shader Stripping**.
- Target: **< 50 variants** per shader on mobile.

### 3.4 Overdraw Reduction

Overdraw occurs when the GPU paints the same pixel multiple times. On mobile (tile-based GPUs such as Mali, Adreno, Apple GPU), overdraw is **especially** expensive.

**Checklist:**

- [ ] Set `Z Write` OFF on transparent shaders that do not require fine sorting.
- [ ] Reduce particle area: large particles = massive overdraw.
- [ ] Use the correct `Render Queue`: opaques first, transparents last.
- [ ] Enable **Occlusion Culling** (Window → Rendering → Occlusion Culling) for indoor scenes or scenes with many occluders.
- [ ] **Frustum Culling** is automatic, but ensure meshes have correct bounding boxes.

**Visualize overdraw:** Scene View → render mode dropdown → "Overdraw". Green = 1×, red = 4×+.

### 3.5 Optimized URP Settings for Mobile

In the **URP Asset** (create a mobile-specific one):

```
Rendering:
  Render Scale:           0.85 (low-end) / 1.0 (high-end)
  Upscaling Filter:       FSR (if available) or Bilinear

Lighting:
  Main Light:             Per Pixel
  Additional Lights:      Disabled (low) / Per Vertex (mid)
  Max Additional Lights:  0–2
  Cast Shadows:           OFF for additional lights

Shadows:
  Max Distance:           20–30m
  Shadow Resolution:      512 (low) / 1024 (mid) / 2048 (high)
  Cascade Count:          1–2 (NEVER 4 on mobile)
  Soft Shadows:           OFF (low) / ON (high)

Quality:
  HDR:                    OFF (low) / ON (high, if necessary)
  MSAA:                   Disabled (low) / 2x (mid-high)
  LOD Cross Fade:         OFF on low-end (uses alpha test, which is expensive)

Post Processing:
  Avoid: Motion Blur, Depth of Field, SSAO
  OK: Color Grading (LUT), Vignette, Bloom (mobile-friendly)
```

---

## 4. Memory Management

### 4.1 Budgets per Device Tier

| Resource | Low-end (<3GB RAM) | Mid-range (4–6GB) | High-end (8GB+) |
|---------|--------------------|--------------------|-----------------|
| **Total App Memory** | < 400 MB | < 600 MB | < 900 MB |
| **Textures** | < 100 MB | < 200 MB | < 400 MB |
| **Meshes** | < 30 MB | < 60 MB | < 100 MB |
| **Audio** | < 20 MB | < 40 MB | < 60 MB |
| **Managed Heap** | < 50 MB | < 80 MB | < 120 MB |
| **Max Texture Size** | 512–1024 | 1024–2048 | 2048 |
| **Draw Calls / Frame** | < 80 | < 150 | < 250 |

> **Golden rule:** on Android, if your app exceeds ~1 GB, the OS starts killing background processes and eventually your own. On iOS, the limit varies by model, but aim for < 75% of the physical RAM on your lowest target device.

### 4.2 Addressables vs Resources

| Aspect | Resources | Addressables |
|---------|-----------|--------------|
| Loading | Synchronous (`Resources.Load`) | Asynchronous by default |
| Build time | Everything packed into a single blob | Separate bundles per group |
| Memory at startup | Serializes the index of **all** assets | Loads only a lightweight catalog |
| Unloading | Manual (`Resources.UnloadUnusedAssets`) | Automatic ref-counting |
| DLC / Updates | Not possible | Native support for remote bundles |
| **Recommendation** | Legacy, avoid | New projects standard |

**Real impact:** projects with >10,000 assets in the Resources folder can spend **several seconds** on startup on low-end devices just to build the index.

```csharp
// Load with Addressables
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

AsyncOperationHandle<GameObject> handle =
    Addressables.LoadAssetAsync<GameObject>("Prefabs/Enemy_Orc");

handle.Completed += op =>
{
    if (op.Status == AsyncOperationStatus.Succeeded)
    {
        Instantiate(op.Result);
    }
};

// IMPORTANT: release when no longer needed
// Addressables.Release(handle);
```

### 4.3 Texture Streaming

Reduces texture memory by loading only the mip levels needed based on camera distance.

```
// Project Settings → Quality → Texture Streaming
// ✅ Enable
// Memory Budget: 128 MB (low) / 256 MB (mid) / 512 MB (high)
```

**Caveats:**

- Requires mipmaps enabled on textures.
- Can cause visual "pop-in" if the budget is too aggressive.
- Monitor "Texture Streaming → Wanted" vs "Budget" in the Memory Profiler.

### 4.4 Audio Compression

| Audio type | Format | Load Type | Compression |
|---------------|---------|-----------|------------|
| **Short SFX** (< 2s) | PCM or ADPCM | Decompress On Load | — |
| **Medium SFX** (2–10s) | Vorbis | Compressed In Memory | Quality: 70% |
| **Music / Ambience** | Vorbis | Streaming | Quality: 50–60% |
| **Dialogue** | Vorbis | Compressed In Memory | Quality: 60% |

> **Force Mono:** enable on everything except stereo music. Halves memory usage.

### 4.5 Read/Write Enabled — The Pitfall

When `Read/Write Enabled` is ON on a Mesh or Texture, Unity keeps **two copies**: one in RAM (CPU) and one in VRAM (GPU).

**Checklist:**

- [ ] Meshes: disable R/W unless you modify vertices at runtime.
- [ ] Textures: disable R/W unless you use `GetPixel`/`SetPixel`.
- [ ] Check with Memory Profiler: sort textures/meshes by size and look for duplicates.

**Typical savings:** in medium-sized projects, disabling R/W where unnecessary saves **50–150 MB**.

---

## 5. Render Pipeline — URP for Mobile

### 5.1 URP vs Built-in for Mobile

| Aspect | Built-in | URP |
|---------|----------|-----|
| SRP Batcher | No | Yes |
| Shader Graph | Partial | Yes — Complete |
| Optimized mobile shaders | Limited | Yes — SimpleLit, BakedLit |
| Render Graph | No | Yes (Unity 6+) |
| Mobile performance | Baseline | **20–30% better** in typical scenarios |
| **Recommendation** | Legacy | Standard for mobile |

> **Unity 6 note:** The URP Render Graph enables automatic render pass optimizations (merge, culling). Use it if you are on Unity 6+.

### 5.2 LOD System

Configure LODs to reduce polygon count with distance:

| LOD Level | Polycount (% of original) | Typical distance |
|-----------|---------------------------|-------------------|
| LOD 0 | 100% | 0–10m |
| LOD 1 | 50% | 10–25m |
| LOD 2 | 25% | 25–50m |
| LOD 3 / Cull | 10% or culled | 50m+ |

**Best practices:**

- Use the **LOD Group** component in Unity.
- Generate LODs with Simplygon, InstaLOD, or Unity's built-in LOD generator.
- **Disable LOD Cross Fade** on low-end (uses clip/alpha test which is expensive on tile-based GPUs).
- Use impostors for very distant vegetation.

### 5.3 Baked vs Realtime Lighting

| Type | CPU Cost | GPU Cost | Quality | Usage |
|------|----------|----------|-----------|-----|
| **Baked (Lightmaps)** | Zero at runtime | Lightmap sample only | Static | Static scenes |
| **Mixed** | Low | Medium | Good | Scenes with 1 dynamic light |
| **Realtime** | High | High | Dynamic | Avoid on mobile |

**Mobile recommendation:**

- Bake all static lighting. Use lightmaps with **10–20 texels/unit** resolution for mobile.
- Maximum **1 realtime light** (directional/sun). Shadows from that light only.
- Additionally: Light Probes for dynamic objects receiving baked lighting.
- Reflection Probes: use baked, resolution 128 or 256 max.

---

## 6. Build Settings

### 6.1 IL2CPP vs Mono

| Aspect | Mono | IL2CPP |
|---------|------|--------|
| Build time | Fast | Slow (transpiles to C++) |
| Runtime performance | Baseline | **1.5–3× faster** |
| Build size | Smaller | Larger (native code) |
| iOS compatibility | Not supported | Required |
| Code stripping | Limited | Aggressive |
| **Recommendation** | Dev builds | Release builds |

**Stripping configuration (IL2CPP):**

```
Player Settings → Other Settings:
  Scripting Backend:     IL2CPP
  Managed Stripping Level: Medium (safe) or High (test thoroughly!)
  Strip Engine Code:     ON

// If High causes crashes, create link.xml to preserve types used via reflection:
// <linker>
//   <assembly fullname="Assembly-CSharp">
//     <type fullname="MyNamespace.MyReflectedType" preserve="all"/>
//   </assembly>
// </linker>
```

### 6.2 API Level and Architecture

**Android:**

```
Player Settings → Other Settings:
  Target API Level:      API 33+ (Android 13+)
  Minimum API Level:     API 24 (Android 7.0) — balance between reach and features
  Target Architectures:  ARM64 (remove ARMv7 if possible — reduces build size)
  Install Location:      Automatic
```

**iOS:**

```
Player Settings → Other Settings:
  Target minimum iOS Version: 15.0+
  Architecture:               ARM64
  Target SDK:                 Device SDK (not Simulator for release)
```

### 6.3 Vulkan vs OpenGL ES

| Aspect | Vulkan | OpenGL ES 3.0 |
|---------|--------|---------------|
| Performance | Better (lower driver overhead) | Good |
| Compatibility | Android 7.0+ with support | Universal |
| Compute shaders | Complete | Limited |
| Graphics Jobs | Yes | No |
| **Recommendation** | First in list + GLES fallback | Fallback |

**Recommended configuration (Android):**

```
Player Settings → Other Settings → Graphics APIs:
  1. Vulkan        (preferred)
  2. OpenGLES3     (fallback)
  Remove OpenGLES2 (too limited)

  Auto Graphics API: OFF (manual control of order)
```

**iOS:** uses Metal automatically. No choice required.

---

## 7. Thermal Throttling and Battery Optimization

### 7.1 The Problem

Mobile devices perform **thermal throttling** when they overheat: they reduce CPU/GPU clock by up to 50%, causing a sharp FPS drop. This typically happens after 5–15 minutes of heavy gameplay.

### 7.2 Adaptive Performance (Samsung + Unity)

```csharp
// Install: com.unity.adaptiveperformance
// + provider: com.unity.adaptiveperformance.samsung.android

using UnityEngine.AdaptivePerformance;

void Update()
{
    var ap = Holder.Instance;
    if (ap == null || !ap.Active) return;

    var thermal = ap.ThermalStatus;

    // thermal.ThermalMetrics.WarningLevel:
    // NoWarning → Throttling → ThrottlingImminent

    switch (thermal.ThermalMetrics.WarningLevel)
    {
        case WarningLevel.NoWarning:
            QualitySettings.SetQualityLevel(2); // High
            Application.targetFrameRate = 60;
            break;

        case WarningLevel.ThrottlingImminent:
            QualitySettings.SetQualityLevel(1); // Medium
            Application.targetFrameRate = 45;
            break;

        case WarningLevel.Throttling:
            QualitySettings.SetQualityLevel(0); // Low
            Application.targetFrameRate = 30;
            break;
    }
}
```

### 7.3 Mitigation Strategies

| Strategy | Impact | Implementation |
|------------|---------|---------------|
| **Target 30 FPS** instead of 60 | Reduces heat by ~40% | `Application.targetFrameRate = 30` |
| **Dynamic Resolution** | Reduces GPU load under stress | URP: Dynamic Resolution + FSR upscaling |
| **Adaptive quality tiers** | Auto-adjustment based on thermal state | Adaptive Performance or custom |
| **Throttle VFX** | Fewer particles when hot | Reduce `maxParticles` and `emission rate` |
| **Pause non-essential systems** | CPU headroom | Disable complex AI, secondary physics |

### 7.4 Battery-Friendly Practices

- **Do not poll sensors** (gyro, GPS, accelerometer) at 60 Hz if you do not need to. Reduce the sample rate.
- **Reduce network calls** on idle screens (menus, pause).
- **Screen brightness:** do not force maximum brightness via API.
- **`Application.targetFrameRate = 30`** in menus and static screens.
- **OnDemandRendering:** for UI-only screens:

```csharp
// Renders every 3 frames (~20 FPS at 60 Hz target)
OnDemandRendering.renderFrameInterval = 3;

// Return to normal during gameplay:
OnDemandRendering.renderFrameInterval = 1;
```

---

## 8. Final Release Checklist

### Profiling

- [ ] Run profiler on a real device (not the editor) for the low-end target
- [ ] Frame time < 33.3 ms (30 FPS) or < 16.6 ms (60 FPS) consistently
- [ ] Zero GC.Alloc in the gameplay loop (verify with Deep Profile)
- [ ] Total memory < budget for the target tier
- [ ] No memory leaks between scenes (snapshot comparison)

### CPU

- [ ] Object pooling for all frequently spawned objects
- [ ] Centralized update for 100+ active scripts
- [ ] Zero LINQ / string concatenation / boxing in hot paths
- [ ] Jobs + Burst for heavy calculations (pathfinding, simulations)
- [ ] Coroutines with cached yield objects (`WaitForSeconds` reused)

### GPU

- [ ] SRP Batcher active and working (verify in Frame Debugger)
- [ ] Draw calls < 100 (low) / 200 (high)
- [ ] Textures in ASTC 6×6 (or ETC2 for compatibility)
- [ ] Mipmaps ON for 3D, OFF for UI
- [ ] Overdraw < 2.5× average in the main scene
- [ ] Shader variants stripped (< 50 per shader)
- [ ] No Standard Shader; everything in URP/Lit or SimpleLit

### Memory

- [ ] Read/Write Enabled OFF on all meshes/textures where possible
- [ ] Addressables (not Resources) for asset loading
- [ ] Texture Streaming enabled with the correct budget
- [ ] Audio: Force Mono, compressed Vorbis, streaming for music
- [ ] No texture > 2048 (except justified exceptions)

### Build

- [ ] IL2CPP with Managed Stripping Level Medium+
- [ ] ARM64 only (Android) / ARM64 (iOS)
- [ ] Vulkan first + OpenGL ES 3.0 fallback (Android)
- [ ] Strip Engine Code ON
- [ ] Development Build OFF in release

### Thermal and Battery

- [ ] Adaptive Performance or manual quality tier implemented
- [ ] `OnDemandRendering` in menus / static screens
- [ ] Reduced target frame rate in menus
- [ ] Tested with a 15+ minute session on a real device without severe throttling

---

## 9. Sources and References

### Official Unity Documentation

- [Unity 6 — Optimize performance for mobile, XR & web](https://unity.com/resources/mobile-xr-web-game-performance-optimization-unity-6) — Complete official guide for Unity 6
- [Unity Manual — Optimize for mobile](https://docs.unity3d.com/6000.2/Documentation/Manual/iphone-iOS-Optimization.html)
- [URP — Configure for better performance](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/configure-for-better-performance.html)
- [SRP Batcher Manual](https://docs.unity3d.com/Manual/SRPBatcher.html)
- [Vulkan in Unity](https://docs.unity3d.com/6000.3/Documentation/Manual/vulkan.html)
- [Addressables Memory Management](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/MemoryManagement.html)

### Unity Blog & Guides

- [Enhancing mobile performance with the Burst compiler](https://unity.com/blog/engine-platform/enhancing-mobile-performance-with-the-burst-compiler)
- [Tales from the optimization trenches: Saving memory with Addressables](https://unity.com/blog/technology/tales-from-the-optimization-trenches-saving-memory-with-addressables)
- [Art optimization tips for mobile — Part 1](https://unity.com/how-to/mobile-game-optimization-tips-part-1)
- [Art optimization tips for mobile — Part 2](https://unity.com/how-to/mobile-game-optimization-tips-part-2)
- [Simplify content management with Addressables](https://unity.com/how-to/simplify-your-content-management-addressables)
- [Unity E-Book: Optimize your mobile game performance](https://unity.com/resources/unity-e-book-optimize-your-mobile-game-performance)

### External Resources

- [Unity Mobile Optimization — GitHub repo](https://github.com/GuardianOfGods/unity-mobile-optimization) — Community repository with practical examples
- [Android GPU Inspector (AGI)](https://developer.android.com/agi) — GPU profiling for Android
- [ARM Developer — Using Neon intrinsics to optimize Unity on Android](https://learn.arm.com/learning-paths/mobile-graphics-and-gaming/using-neon-intrinsics-to-optimize-unity-on-android/5-the-optimizations/)
- [Developing games with Vulkan in Unity — ARM](https://developer.arm.com/documentation/102339/latest/Using-Vulkan-in-Unity)
- [Android Developers — URP Asset Settings Optimization](https://developer.android.com/develop/xr/unity/performance/urp-asset-settings)

---

> **Last updated:** April 2026. Based on Unity 6 (6000.x) and Unity 2022 LTS. Review periodically — Unity updates its recommendations with each major release.
