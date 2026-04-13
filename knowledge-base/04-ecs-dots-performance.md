# Unity DOTS/ECS — Complete Technical Guide (2025-2026)

> **Context:** This document covers the current state of the Data-Oriented Technology Stack (DOTS) in Unity 6, including fundamental concepts, performance, architecture decisions, and the official roadmap following Unite Barcelona (November 2025).

---

## 1. What is DOTS

DOTS is a set of Unity technologies that replaces the object-oriented paradigm (MonoBehaviour/GameObject) with a **data-oriented** model optimized for cache usage and multithreading. The three pillars are:

- **Entity Component System (ECS)** — organization of data into entities, components, and systems
- **Burst Compiler** — an LLVM-based compiler that transforms C# into highly optimized native code
- **C# Job System** — safe multithreading without manual locks

Together, these three pillars enable performance gains of **10x to 100x** in scenarios with thousands of entities compared to traditional MonoBehaviour.

---

## 2. Fundamental ECS Concepts

### 2.1 Entity

An Entity is not an object — it is simply an **ID** (a lightweight index) that points to a set of components. It has no methods, no inheritance. Think of it as a "row" in a database table.

```csharp
// Create entity via EntityManager
EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
Entity entity = entityManager.CreateEntity();

// Add component to entity
entityManager.AddComponentData(entity, new MoveSpeed { Value = 5f });
entityManager.AddComponentData(entity, new Health { Current = 100, Max = 100 });
```

### 2.2 Component (IComponentData)

Components are **pure structs** that contain only data — no logic, no methods, no managed references. This allows Burst to compile them and the Job System to parallelize them.

```csharp
// Simple component — data only
public struct MoveSpeed : IComponentData
{
    public float Value;
}

public struct Health : IComponentData
{
    public float Current;
    public float Max;
}

// Tag component (no data, just marks the entity)
public struct EnemyTag : IComponentData { }

// Buffer element (dynamic array per entity)
[InternalBufferCapacity(8)]
public struct DamageBufferElement : IBufferElementData
{
    public float Value;
}

// Enableable component (toggle on/off without moving between archetypes)
public struct Stunned : IComponentData, IEnableableComponent { }
```

**Available component types:**

| Type | Interface | Usage |
|------|-----------|-----|
| Unmanaged component | `IComponentData` | Gameplay data (position, health, speed) |
| Shared component | `ISharedComponentData` | Shared data (material, mesh) — groups chunks |
| Buffer element | `IBufferElementData` | Dynamic list per entity (inventory, waypoints) |
| Tag component | `IComponentData` (empty) | Marker without data (IsEnemy, IsPlayer) |
| Enableable | `IEnableableComponent` | Toggle on/off without changing archetype |
| Cleanup component | `ICleanupComponentData` | Survives entity destruction |

### 2.3 System (ISystem)

Systems contain all the logic. They run every frame (or on demand) and operate on component queries.

```csharp
// ISystem — Burst-compatible, zero allocation (PREFERRED)
[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Direct foreach — Burst compiles this internally into jobs
        foreach (var (transform, speed) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
        {
            transform.ValueRW.Position +=
                new float3(0, 0, speed.ValueRO.Value * deltaTime);
        }
    }
}

// SystemBase — allows managed code (useful for UI, interop)
public partial class UIHealthSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Can access MonoBehaviours, managed objects, etc.
        foreach (var (health, entity) in
            SystemAPI.Query<RefRO<Health>>().WithEntityAccess())
        {
            // Update UI with ECS data
        }
    }
}
```

**Practical rule:** use `ISystem` + `[BurstCompile]` by default. Reserve `SystemBase` only when you need managed types (UI, strings, references to MonoBehaviours).

### 2.4 World

The World is the container that groups entities, components, and systems. Unity automatically creates a `DefaultGameObjectInjectionWorld`, but you can create separate worlds (useful for server/client simulations).

```csharp
// Default world
World defaultWorld = World.DefaultGameObjectInjectionWorld;

// Create a custom world (e.g.: server-side simulation)
World serverWorld = new World("ServerWorld");
var simGroup = serverWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
```

### 2.5 Archetype

An Archetype defines the **memory layout** of entities that share the same set of components. Entities with the same components live together in contiguous **chunks** of 16 KB in memory.

```
Archetype A: [LocalTransform, MoveSpeed, Health]
  → Chunk 0: Entity 0-127
  → Chunk 1: Entity 128-255

Archetype B: [LocalTransform, MoveSpeed, Health, EnemyTag]
  → Chunk 0: Entity 0-98
```

**Why it matters:** when a System runs a query, it iterates sequentially over chunks. This maximizes cache hits and allows Burst to vectorize with SIMD. Adding/removing components moves the entity between archetypes (an expensive operation — avoid in hot loops).

---

## 3. Burst Compiler

### 3.1 What it Does

Burst is an ahead-of-time compiler based on LLVM that transforms C# code (the HPC# subset) into optimized native code with auto-vectorization via SIMD (SSE, AVX, NEON).

### 3.2 How to Use it

```csharp
using Unity.Burst;
using Unity.Mathematics;

// In Systems
[BurstCompile]
public partial struct PhysicsIntegrationSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // All of this code will be compiled with LLVM
        foreach (var (transform, velocity) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Velocity>>())
        {
            transform.ValueRW.Position += velocity.ValueRO.Value * SystemAPI.Time.DeltaTime;
        }
    }
}

// In standalone Jobs (outside ECS)
[BurstCompile]
public struct CalculateDamageJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> BaseDamage;
    [ReadOnly] public NativeArray<float> Multipliers;
    public NativeArray<float> Results;

    public void Execute(int index)
    {
        Results[index] = BaseDamage[index] * Multipliers[index];
    }
}
```

### 3.3 Burst Limitations (HPC# subset)

Burst **does not support**:

| Prohibited | Alternative |
|----------|-------------|
| `string`, `class`, managed types | Use `FixedString64Bytes`, structs, NativeContainers |
| Allocation with `new` (heap) | Use `NativeArray`, `NativeList` |
| `try/catch` | Validate inputs beforehand; use error codes |
| Virtual methods / interfaces with boxing | Use function pointers (`FunctionPointer<T>`) |
| LINQ | Manual loops with `for`/`foreach` |
| Static mutable state | Use `SharedStatic<T>` |
| Generics with references | Use generics with `unmanaged` constraints |

### 3.4 Typical Performance

| Scenario | C# Mono | Burst | Speedup |
|---------|---------|-------|---------|
| Transform updates (10K) | 8.2ms | 0.3ms | ~27x |
| Physics integration (50K) | 42ms | 1.8ms | ~23x |
| Pathfinding (1K agents) | 15ms | 0.8ms | ~19x |
| Boid simulation (100K) | 128ms | 6ms | ~21x |

---

## 4. C# Job System

### 4.1 Concept

The Job System provides **safe-by-design** multithreading — no locks, no race conditions. The system guarantees safety at compile-time via `[ReadOnly]`, `[WriteOnly]` attributes and NativeContainer ownership.

### 4.2 IJobEntity (Preferred in ECS)

```csharp
[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    // The signature automatically defines the query
    void Execute(ref LocalTransform transform, in MoveSpeed speed)
    {
        transform.Position += new float3(0, 0, speed.Value * DeltaTime);
    }
}

// Schedule in a System
[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var moveJob = new MoveJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime
        };
        moveJob.ScheduleParallel(); // Distributes across worker threads
    }
}
```

### 4.3 IJobChunk (Granular control)

```csharp
[BurstCompile]
public struct DamageJob : IJobChunk
{
    public ComponentTypeHandle<Health> HealthHandle;
    [ReadOnly] public ComponentTypeHandle<DamageBufferElement> DamageBufferHandle;
    public float DeltaTime;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var healths = chunk.GetNativeArray(ref HealthHandle);
        var damageBuffers = chunk.GetBufferAccessor(ref DamageBufferHandle);

        for (int i = 0; i < chunk.Count; i++)
        {
            var health = healths[i];
            var buffer = damageBuffers[i];
            for (int d = 0; d < buffer.Length; d++)
            {
                health.Current -= buffer[d].Value;
            }
            healths[i] = health;
        }
    }
}
```

### 4.4 When to Use Each Job Type

| Type | When to Use |
|------|-------------|
| `IJobEntity` | 90% of cases — automatic query, simple, Burst-friendly |
| `IJobChunk` | Needs access to the entire chunk, enabled masks, complex buffers |
| `IJobParallelFor` | Processing arrays outside ECS (e.g.: procedural generation) |
| `IJob` | Single-threaded work that needs to run on a worker thread |

---

## 5. Collections (NativeContainers)

NativeContainers are unmanaged memory containers — the garbage collector does not see them, and Burst can compile them.

```csharp
using Unity.Collections;

// NativeArray — fixed size, most performant
var positions = new NativeArray<float3>(1000, Allocator.TempJob);

// NativeList — dynamic size
var results = new NativeList<int>(100, Allocator.TempJob);
results.Add(42);

// NativeHashMap — key-value lookup
var entityLookup = new NativeHashMap<int, Entity>(256, Allocator.Persistent);
entityLookup.Add(entityId, entity);

// NativeQueue — FIFO, thread-safe for producers
var commandQueue = new NativeQueue<DamageEvent>(Allocator.TempJob);

// NativeParallelHashMap — for parallel writes in jobs
var spatialGrid = new NativeParallelMultiHashMap<int, Entity>(1000, Allocator.TempJob);
```

**Available allocators:**

| Allocator | Lifetime | Usage |
|-----------|---------|-----|
| `Allocator.Temp` | 1 frame | Temporary calculations within a method |
| `Allocator.TempJob` | 4 frames | Duration of a job |
| `Allocator.Persistent` | Manual (`Dispose()`) | Data that lives across frames |

**Golden rule:** always call `.Dispose()` on NativeContainers when finished. In jobs, use `[DeallocateOnJobCompletion]` or schedule a `.Dispose(jobHandle)`.

---

## 6. Hybrid Approach: MonoBehaviour + ECS Side by Side

### 6.1 How it Works

Unity allows using GameObjects and ECS in the same project. The bridge is made through **SubScenes** and **Bakers**:

1. **SubScene** — A container that converts GameObjects into entities at runtime
2. **Baker** — A class that transforms an "authoring" MonoBehaviour into ECS components
3. **Companion Components** — MonoBehaviours that survive conversion (for managed systems)

```csharp
// 1. Authoring component (MonoBehaviour in the Editor)
public class MoveSpeedAuthoring : MonoBehaviour
{
    public float Speed = 5f;
}

// 2. Baker — converts to ECS during baking
public class MoveSpeedBaker : Baker<MoveSpeedAuthoring>
{
    public override void Bake(MoveSpeedAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new MoveSpeed { Value = authoring.Speed });
    }
}
```

### 6.2 ECS ↔ MonoBehaviour Communication Pattern

```csharp
// System that reads data from ECS and updates UI (MonoBehaviour)
public partial class SyncHealthToUISystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Find the UI Manager reference (managed)
        var uiManager = Object.FindFirstObjectByType<HealthUIManager>();
        if (uiManager == null) return;

        foreach (var (health, entity) in
            SystemAPI.Query<RefRO<Health>>()
                     .WithAll<PlayerTag>()
                     .WithEntityAccess())
        {
            uiManager.UpdateHealthBar(health.ValueRO.Current, health.ValueRO.Max);
        }
    }
}
```

### 6.3 Gradual Adoption Strategy

```
Existing Project (MonoBehaviour)
│
├── Phase 1: Install DOTS packages, use Burst in MonoBehaviours
│            (already gains ~10x on [BurstCompile] methods)
│
├── Phase 2: Move "hot" systems to Jobs
│            (movement, enemy AI, mass spawning)
│
├── Phase 3: SubScenes for mass entities
│            (projectiles, particles, foliage, simple NPCs)
│
└── Phase 4: Core systems in pure ECS
             (physics, netcode, deterministic simulation)
```

---

## 7. Comparison Table: MonoBehaviour vs DOTS

| Aspect | MonoBehaviour | DOTS/ECS |
|---------|--------------|----------|
| **Performance (10K entities)** | ~8 FPS | ~165 FPS |
| **Memory layout** | Scattered (heap) | Contiguous (16KB chunks) |
| **Multithreading** | Manual (error-prone) | Job System (safe by design) |
| **Compilation** | Mono/IL2CPP | Burst (LLVM, SIMD) |
| **Learning curve** | Low (~2 weeks) | High (~3-6 months) |
| **Prototyping** | Fast | Slow (boilerplate, bakers) |
| **Debugging** | Direct Inspector | Entity Debugger (limited) |
| **Networking** | Mirror, Fishnet | Netcode for Entities (deterministic) |
| **UI** | uGUI, UI Toolkit | Needs managed bridge |
| **Asset Store** | Vast ecosystem | Few compatible assets |
| **Documentation** | Mature, abundant | Improving, but gaps remain |
| **Max entities @60FPS** | ~1,000-5,000 | ~100,000-500,000+ |
| **Hot reload** | Yes | Limited (requires re-bake) |
| **Inheritance/polymorphism** | Natural | Does not exist (pure composition) |

---

## 8. Decision Framework: When to Use DOTS

### Decision Flowchart (Text)

```
START: Does your project need to process many similar entities?
│
├── NO → Use MonoBehaviour (95% of indie/mobile games)
│
└── YES → How many simultaneous entities?
    │
    ├── < 1,000 → MonoBehaviour is sufficient
    │               (consider standalone Jobs + Burst for hot paths)
    │
    ├── 1,000 - 10,000 → Hybrid approach
    │                      Jobs + Burst on hot paths
    │                      ECS for mass entities
    │                      MonoBehaviour for gameplay/UI
    │
    └── > 10,000 → Pure DOTS/ECS is the way to go
                    Burst + Jobs + full ECS
                    Networking: Netcode for Entities
```

### DOTS Sweet Spots

DOTS shines when you have:

- **Mass entities (>1,000 similar):** RTS with hundreds of units, bullet hell with thousands of projectiles, crowd/traffic simulations, interactive foliage/vegetation
- **Simulation-heavy:** games with large-scale custom physics, economic/ecological simulations, weather systems with thousands of particles
- **Determinism:** multiplayer with lockstep/rollback, frame-perfect replays, reproducible scientific simulations
- **Mobile-optimized:** games that need to extract maximum performance from limited hardware, battery-sensitive (fewer clock cycles = less battery drain)

### When It Is Not Worth It

- **UI-heavy apps:** DOTS has no UI system — you will need a bridge to uGUI/UI Toolkit
- **Quick prototypes / game jams:** the boilerplate of bakers, SubScenes, and IComponentData components kills iteration speed
- **Small teams without ECS experience:** 3-6 month learning curve before real productivity; the mental model is radically different from OOP
- **Narrative / adventure games:** few systems benefit from data-oriented design when the bottleneck is content, not performance
- **Projects dependent on the Asset Store:** most assets assume MonoBehaviour/GameObject

---

## 9. State of DOTS in Unity 6 (Roadmap post-Unite Barcelona, Dec 2025)

### Current Package Status

| Package | Version (Unity 6) | Status |
|--------|-------------------|--------|
| `com.unity.entities` | 1.4.x | Verified (Prod-ready) |
| `com.unity.burst` | 1.8.x+ | Verified |
| `com.unity.collections` | 2.4.x | Verified |
| `com.unity.mathematics` | 1.3.x | Verified |
| `com.unity.jobs` | 0.70.x+ | Verified |
| `com.unity.physics` | 1.4.x | Verified |
| `com.unity.rendering.hybrid` | (in transition) | Evolving → `Entities Graphics` |
| `com.unity.netcode` (Entities) | 1.x | Verified |

### Roadmap Announced at Unite Barcelona (Nov 2025)

1. **Unity 6.4:** ECS becomes a **core engine package** (no longer an optional add-on)
2. **Unified Transforms:** ECS components will be attachable directly to GameObjects — without re-architecting existing projects
3. **Unity 6.7 LTS (2026):** next Long Term Support release with fully integrated DOTS
4. **Quarterly cadence:** incremental updates from 6.3 → 6.4 → 6.5 → 6.6 → 6.7 LTS
5. **CoreCLR migration:** replacement of Mono with CoreCLR (better base performance for all C#)

### What This Means in Practice

Unity's direction is clear: **incremental convergence**, not a rewrite. GameObjects and MonoBehaviours will not be deprecated. The idea is that ECS and GameObjects will coexist ever more seamlessly, enabling gradual adoption without a "big bang refactor".

---

## 10. Realistic Learning Curve

```
Week 1-2:   Concepts (Entity, Component, System, World)
              → Understand the data-oriented mental model
              → Official HelloCube tutorial

Week 3-4:   Burst + standalone Jobs
              → [BurstCompile] on existing MonoBehaviours
              → NativeArray, NativeList basics

Month 2:    SubScenes + Bakers
              → Convert GameObjects → Entities
              → Authoring workflow
              → Entity Debugger

Month 3:    Advanced queries + IJobEntity
              → SystemAPI.Query with filters
              → Parallel jobs
              → EntityCommandBuffer (creating/destroying entities)

Month 4-5:  Production patterns
              → Shared components, buffer elements
              → Enableable components
              → Structural change optimization
              → Prefab instantiation via ECS

Month 6:    Full integration
              → Fluent hybrid workflow
              → Profiling with Unity Profiler + Entity Debugger
              → Netcode for Entities (if multiplayer)
```

**Tip:** Burst + standalone Jobs (without ECS) delivers 70% of the performance gains with 20% of the complexity. Start there.

---

## 11. Setup — Required Packages

### Installation via Package Manager

```json
// Packages/manifest.json — add to dependencies:
{
  "dependencies": {
    "com.unity.entities": "1.4.5",
    "com.unity.entities.graphics": "1.4.5",
    "com.unity.burst": "1.8.18",
    "com.unity.collections": "2.4.3",
    "com.unity.mathematics": "1.3.2",
    "com.unity.physics": "1.4.5"
  }
}
```

### Burst Configuration

1. **Edit → Project Settings → Burst AOT Settings**
2. Enable "Enable Burst Compilation"
3. Development: Safety Checks = ON
4. Release: Safety Checks = OFF, Optimization = Maximum

### Recommended Project Structure

```
Assets/
├── Scripts/
│   ├── Authoring/           # MonoBehaviours for the Editor
│   │   ├── MoveSpeedAuthoring.cs
│   │   └── HealthAuthoring.cs
│   ├── Components/          # IComponentData structs
│   │   ├── MoveSpeed.cs
│   │   └── Health.cs
│   ├── Systems/             # ISystem / SystemBase
│   │   ├── MoveSystem.cs
│   │   └── DamageSystem.cs
│   ├── Jobs/                # Standalone Jobs (if any)
│   │   └── TerrainGenerationJob.cs
│   └── MonoBehaviours/      # Traditional gameplay/UI
│       └── HealthUIManager.cs
├── SubScenes/               # SubScenes for baking
│   ├── EnemySubScene.unity
│   └── EnvironmentSubScene.unity
└── Prefabs/                 # ECS Prefabs (inside SubScenes)
```

---

## 12. Sources and References

- [Unity DOTS — Official Page](https://unity.com/dots)
- [Unity ECS — Official Page](https://unity.com/ecs)
- [DOTS Packages — Unity](https://unity.com/dots/packages)
- [ECS Development Status — December 2025 (Unity Discussions)](https://discussions.unity.com/t/ecs-development-status-december-2025/1699284)
- [ECS Development Status — March 2025 (Unity Discussions)](https://discussions.unity.com/t/ecs-development-status-milestones-march-2025/1615810)
- [November 2025 ECS Stack Review (Unity Discussions)](https://discussions.unity.com/t/november-2025-ecs-stack-review/1694077)
- [Unity Engine Roadmap — Unite 2025 (Unity Discussions)](https://discussions.unity.com/t/the-unity-engine-roadmap-unite-2025/1696495)
- [Unity's 2026 Roadmap — Digital Production](https://digitalproduction.com/2025/11/26/unitys-2026-roadmap-coreclr-verified-packages-fewer-surprises/)
- [Burst Compiler — Complete Performance Guide (Generalist Programmer)](https://generalistprogrammer.com/tutorials/unity-burst-compiler-complete-performance-optimization-guide)
- [ECS Complete Tutorial (Generalist Programmer)](https://generalistprogrammer.com/tutorials/entity-component-system-complete-ecs-architecture-tutorial)
- [Unity DOTS & ECS 2025 Intermediate Guide](https://quickunitytips.blogspot.com/2025/11/unity-dots-ecs-2025-guide.html)
- [What is Unity DOTS? Is it Worth Learning in 2026? (Darko Unity)](https://darkounity.com/blog/what-is-unity-dots)
- [EntityComponentSystemSamples — GitHub (Unity Technologies)](https://github.com/Unity-Technologies/EntityComponentSystemSamples)
- [Burst and Kernel Theory of Game Performance (Sebastian Schöner)](https://blog.s-schoener.com/2024-12-12-burst-kernel-theory-game-performance/)
- [Entities Package Documentation (1.4.x)](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/ecs-workflow-example-authoring-baking.html)
