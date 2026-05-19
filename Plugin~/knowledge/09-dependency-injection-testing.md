# 09 — Dependency Injection & Testing in Unity (2025-2026)

> Complete guide on Dependency Injection and Testing in Unity projects: frameworks (VContainer, Zenject/Extenject), injection patterns in MonoBehaviours, Unity Test Framework with NUnit, mocking with NSubstitute, and how DI + Testing enhances AI-assisted workflows.

---

## 1. Why DI Matters in Unity

### 1.1 The Problem Without DI

In traditional Unity projects, MonoBehaviours frequently fetch their own dependencies via `FindObjectOfType`, `GetComponent`, static Singletons, or direct Inspector references. This creates tight coupling between systems:

```csharp
// ❌ Anti-pattern: hardcoded dependencies
public class PlayerCombat : MonoBehaviour
{
    private void Attack()
    {
        // Direct coupling to singletons
        var weapon = InventoryManager.Instance.GetEquippedWeapon();
        var damage = DamageCalculator.Instance.Calculate(weapon);
        AudioManager.Instance.PlaySFX("attack");
        AnalyticsManager.Instance.TrackEvent("player_attack");
    }
}
```

This pattern causes several problems: it is impossible to test `PlayerCombat` without instantiating all singletons; swapping the audio implementation requires changes across dozens of classes; singleton initialization order is fragile and causes intermittent bugs; and the code is hard to understand because dependencies are hidden.

### 1.2 The Three Pillars of DI in Gamedev

**Testability** — When dependencies are injected via interfaces, replacing them with mocks in tests is trivial. Testing combat logic does not need a real AudioManager.

**Modularity** — Systems connect via contracts (interfaces), not concrete implementations. Swapping `FMODAudioService` for `WwiseAudioService` requires only one line in the container.

**Maintainability** — The dependency graph is explicit in the Composition Root, rather than scattered across hundreds of scripts. New developers understand the architecture by looking at a single point.

### 1.3 DI Fixing the Example

```csharp
// ✅ With DI: explicit dependencies via interfaces
public class PlayerCombat : MonoBehaviour
{
    private IInventoryService _inventory;
    private IDamageCalculator _damage;
    private IAudioService _audio;
    private IAnalyticsService _analytics;

    // VContainer injects via [Inject] method
    [Inject]
    public void Construct(
        IInventoryService inventory,
        IDamageCalculator damage,
        IAudioService audio,
        IAnalyticsService analytics)
    {
        _inventory = inventory;
        _damage = damage;
        _audio = audio;
        _analytics = analytics;
    }

    private void Attack()
    {
        var weapon = _inventory.GetEquippedWeapon();
        var dmg = _damage.Calculate(weapon);
        _audio.PlaySFX("attack");
        _analytics.TrackEvent("player_attack");
    }
}
```

Now each dependency is visible, swappable, and individually testable.

---

## 2. DI Frameworks for Unity

### 2.1 VContainer

VContainer is a DI library optimized for Unity, created by hadashiA. It is 5-10x faster than Zenject for resolve operations and produces zero GC allocations during resolution (no spawned instances).

#### Installation

**Via UPM (manifest.json):**
```json
{
  "dependencies": {
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
  }
}
```

**Via OpenUPM:**
```bash
openupm add jp.hadashikick.vcontainer
```

#### LifetimeScope — The Composition Root

`LifetimeScope` is a MonoBehaviour that serves as the entry point for configuring the DI container. It defines which types are available and how they are resolved:

```csharp
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    // Inspector references (prefabs, ScriptableObjects)
    [SerializeField] private GameSettings _settings;
    [SerializeField] private ActorsView _actorsViewPrefab;

    protected override void Configure(IContainerBuilder builder)
    {
        // Register POCO classes
        builder.Register<IDamageCalculator, DamageCalculator>(Lifetime.Singleton);
        builder.Register<IInventoryService, InventoryService>(Lifetime.Scoped);
        builder.Register<ILootTableResolver, LootTableResolver>(Lifetime.Transient);

        // Register existing instance (ScriptableObject)
        builder.RegisterInstance(_settings).As<IGameSettings>();

        // Register MonoBehaviour in hierarchy
        builder.RegisterComponentInHierarchy<ActorsView>();

        // Register Entry Points (classes with lifecycle hooks)
        builder.RegisterEntryPoint<GameFlowController>();
        builder.RegisterEntryPoint<EnemySpawnSystem>();

        // Factory pattern
        builder.Register<EnemyFactory>(Lifetime.Singleton);
    }
}
```

#### Entry Points — Lifecycle Without MonoBehaviour

VContainer provides interfaces to hook into the game loop without inheriting from MonoBehaviour:

```csharp
using VContainer.Unity;

public class GameFlowController : IStartable, ITickable, IDisposable
{
    private readonly IInventoryService _inventory;
    private readonly IAnalyticsService _analytics;

    // Constructor injection (preferred for POCO classes)
    public GameFlowController(
        IInventoryService inventory,
        IAnalyticsService analytics)
    {
        _inventory = inventory;
        _analytics = analytics;
    }

    public void Start()
    {
        // Called once when the scope is built
        _analytics.TrackEvent("game_started");
    }

    public void Tick()
    {
        // Called every frame (like Update)
        _inventory.ProcessPendingTransactions();
    }

    public void Dispose()
    {
        // Cleanup when the scope is destroyed
        _analytics.Flush();
    }
}
```

Available interfaces: `IStartable`, `IPostStartable`, `ITickable`, `IPostTickable`, `IFixedTickable`, `ILateTickable`, `IDisposable`, `IAsyncStartable`.

#### Nested Scopes (Scoped Lifetimes)

```csharp
// Parent scope: lives for the entire application
public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<IAudioService, FMODAudioService>(Lifetime.Singleton);
        builder.Register<ISaveService, CloudSaveService>(Lifetime.Singleton);
    }
}

// Child scope: lives during a gameplay scene
public class BattleLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Inherits IAudioService and ISaveService from parent
        builder.Register<IBattleSystem, TurnBasedBattleSystem>(Lifetime.Scoped);
        builder.Register<IEnemyAI, BehaviorTreeAI>(Lifetime.Scoped);
        builder.RegisterEntryPoint<BattleFlowController>();
    }
}
```

### 2.2 Zenject / Extenject

Zenject is the most established DI framework for Unity, created by modesttree. Extenject is an actively maintained fork with support for newer Unity versions.

#### Installation

**Via Asset Store:** Search for "Extenject Dependency Injection IOC" on the Unity Asset Store.

**Via GitHub (Extenject):**
```json
{
  "dependencies": {
    "com.svermeulen.extenject": "https://github.com/Mathijs-Bakker/Extenject.git?path=UnityProject/Assets/Plugins/Zenject/Source"
  }
}
```

#### Installers — The Composition Root

Zenject uses `MonoInstaller` (or `ScriptableObjectInstaller`) to configure bindings:

```csharp
using Zenject;

public class GameInstaller : MonoInstaller
{
    [SerializeField] private GameSettings _settings;

    public override void InstallBindings()
    {
        // Bind interface → implementation
        Container.Bind<IDamageCalculator>()
            .To<DamageCalculator>()
            .AsSingle();

        Container.Bind<IInventoryService>()
            .To<InventoryService>()
            .AsSingle()
            .NonLazy();  // Instantiate immediately

        // Bind existing instance
        Container.Bind<IGameSettings>()
            .FromInstance(_settings);

        // Bind with factory
        Container.BindFactory<EnemyType, Enemy, Enemy.Factory>()
            .FromComponentInNewPrefab(_settings.EnemyPrefab)
            .UnderTransformGroup("Enemies");

        // Bind all interfaces
        Container.Bind(typeof(IInitializable), typeof(ITickable), typeof(IDisposable))
            .To<GameFlowController>()
            .AsSingle();
    }
}
```

#### Factories in Zenject

Zenject has robust factory support for dynamic object creation:

```csharp
public class Enemy : MonoBehaviour
{
    private IHealthService _health;
    private EnemyType _type;

    [Inject]
    public void Construct(IHealthService health, EnemyType type)
    {
        _health = health;
        _type = type;
    }

    // Nested factory — Zenject idiomatic pattern
    public class Factory : PlaceholderFactory<EnemyType, Enemy> { }
}

// Usage:
public class EnemySpawner : ITickable
{
    private readonly Enemy.Factory _enemyFactory;

    public EnemySpawner(Enemy.Factory enemyFactory)
    {
        _enemyFactory = enemyFactory;
    }

    public void Tick()
    {
        if (ShouldSpawn())
        {
            // Factory automatically resolves all dependencies
            Enemy enemy = _enemyFactory.Create(EnemyType.Goblin);
        }
    }
}
```

#### ScriptableObject Installer (Data-Driven Configuration)

```csharp
[CreateAssetMenu(menuName = "Installers/AudioInstaller")]
public class AudioInstaller : ScriptableObjectInstaller<AudioInstaller>
{
    [SerializeField] private AudioConfig _config;

    public override void InstallBindings()
    {
        Container.Bind<IAudioService>()
            .To<FMODAudioService>()
            .AsSingle();

        Container.BindInstance(_config).AsSingle();
    }
}
```

### 2.3 VContainer vs Zenject Comparison

| Aspect | VContainer | Zenject/Extenject |
|---|---|---|
| **Performance (Resolve)** | 5-10x faster | Baseline |
| **GC Allocation** | Zero on Resolve | Allocates during Resolve |
| **Startup** | Does not scan GameObjects | Scans all GameObjects via reflection |
| **Code size** | ~5K LOC, few internal types | ~50K+ LOC, many internal types |
| **API** | Simple and direct | Rich and verbose |
| **Factories** | Basic support, uses `IObjectResolver` | Powerful and flexible `PlaceholderFactory<>` |
| **Scenes** | `LifetimeScope` with automatic parent | `SceneContext`, `ProjectContext`, `GameObjectContext` |
| **Signals/Events** | Not included (use MessagePipe) | Integrated `SignalBus` |
| **Decorators** | Not natively supported | `BindInterfacesTo` + decorators |
| **Maturity** | Since 2020, growing community | Since 2015, large community |
| **Documentation** | Good, smaller volume | Extensive, many examples |
| **Source Generator** | Yes (optional, speeds up reflection) | No |
| **UniTask** | Native integration | Via community extensions |
| **ECS/DOTS** | Beta support | Not supported |

**When to choose VContainer:**

- Mobile projects where performance and GC matter a lot
- Teams that prefer simple APIs with less "magic"
- New projects without Zenject legacy
- When using UniTask and wanting native integration

**When to choose Zenject:**

- Existing projects already using Zenject
- Need for complex factories (PlaceholderFactory, SubContainers)
- Want integrated signals
- The team already has solid Zenject experience

### 2.4 Manual DI (Poor Man's DI)

For small projects or as an introduction to the concept, DI can be done without any framework:

```csharp
// Interfaces define contracts
public interface IDamageCalculator
{
    float Calculate(WeaponData weapon, float multiplier);
}

public interface IAudioService
{
    void PlaySFX(string clipName);
}

// Concrete implementations
public class DamageCalculator : IDamageCalculator
{
    public float Calculate(WeaponData weapon, float multiplier)
    {
        return weapon.BaseDamage * multiplier * Random.Range(0.9f, 1.1f);
    }
}

// Manual Composition Root — MonoBehaviour that sets everything up
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameSettings _settings;
    [SerializeField] private PlayerCombat _playerCombat;
    [SerializeField] private AudioSource _audioSource;

    private void Awake()
    {
        // Build the dependency graph manually
        var audioService = new UnityAudioService(_audioSource);
        var damageCalc = new DamageCalculator();
        var inventory = new InventoryService(_settings.StartingItems);
        var analytics = new FirebaseAnalyticsService();

        // Inject via public method
        _playerCombat.Initialize(inventory, damageCalc, audioService, analytics);

        // Set up other systems...
        var spawner = new EnemySpawner(damageCalc, _settings);
        var battleSystem = new BattleSystem(spawner, audioService);
    }
}
```

**Advantages of manual DI:** no external dependencies, easy to understand, good for rapid prototyping, works with any Unity version.

**Disadvantages:** lots of boilerplate in large projects, no automatic lifetime management, no child scopes, maintaining the Composition Root becomes cumbersome.

---

## 3. DI Patterns in Unity

### 3.1 Constructor Injection vs Method Injection vs Property Injection

```csharp
// ✅ Constructor Injection — PREFERRED for POCO classes
// Required, immutable dependencies, validated at creation time
public class CombatSystem
{
    private readonly IDamageCalculator _damage;
    private readonly IBuffSystem _buffs;

    public CombatSystem(IDamageCalculator damage, IBuffSystem buffs)
    {
        _damage = damage ?? throw new ArgumentNullException(nameof(damage));
        _buffs = buffs ?? throw new ArgumentNullException(nameof(buffs));
    }
}

// ✅ Method Injection — PREFERRED for MonoBehaviours
// MonoBehaviours do not have controllable constructors
public class EnemyView : MonoBehaviour
{
    private IHealthService _health;
    private IVFXService _vfx;

    [Inject]  // VContainer and Zenject both recognize this attribute
    public void Construct(IHealthService health, IVFXService vfx)
    {
        _health = health;
        _vfx = vfx;
    }
}

// ⚠️ Property Injection — AVOID when possible
// Dependencies remain mutable and can be null
public class UIManager : MonoBehaviour
{
    [Inject] public ILocalizationService Localization { get; set; }
    // Problem: nothing guarantees injection happened before use
}
```

**Practical rule:** Use constructor injection for POCO classes (services, systems, controllers). Use method injection (`[Inject] Construct(...)`) for MonoBehaviours. Avoid property/field injection except for optional dependencies.

### 3.2 Composition Root in Unity

The Composition Root is the only place where the container is configured. In Unity, it typically lives in:

```
Recommended scene structure:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"Bootstrap" scene (loaded first, never unloaded)
  └─ RootLifetimeScope          ← Global services (audio, save, analytics)

"MainMenu" scene
  └─ MainMenuLifetimeScope      ← UI services, menu controllers
      └─ (inherits from RootScope)

"Gameplay" scene
  └─ GameplayLifetimeScope      ← Combat, AI, spawning
      └─ (inherits from RootScope)

"BattleArena" scene (additive)
  └─ BattleLifetimeScope        ← Battle-specific systems
      └─ (inherits from GameplayScope)
```

**VContainer:** Each scene has a `LifetimeScope`. The parent is found automatically or configured via `parentReference`.

```csharp
public class RootLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Global singletons — live for the entire application
        builder.Register<IAudioService, FMODAudioService>(Lifetime.Singleton);
        builder.Register<ISaveService, CloudSaveService>(Lifetime.Singleton);
        builder.Register<IAnalyticsService, FirebaseAnalytics>(Lifetime.Singleton);
    }
}

// In another scene:
[RequireComponent(typeof(LifetimeScope))]
public class GameplayLifetimeScope : LifetimeScope
{
    // VContainer finds the parent scope automatically
    // Or configure via Inspector: Parent Reference → RootLifetimeScope

    protected override void Configure(IContainerBuilder builder)
    {
        // Scoped — live while this scene is loaded
        builder.Register<IEnemySpawner, WaveSpawner>(Lifetime.Scoped);
        builder.Register<ICombatSystem, RealtimeCombatSystem>(Lifetime.Scoped);
        builder.RegisterEntryPoint<GameplayLoop>();
    }
}
```

**Zenject:** Uses `ProjectContext` (global), `SceneContext` (per scene), and `GameObjectContext` (per prefab):

```csharp
// ProjectContext installer — lives in Resources/ProjectContext.prefab
public class ProjectInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<IAudioService>().To<FMODAudioService>().AsSingle();
        Container.Bind<ISaveService>().To<CloudSaveService>().AsSingle();
    }
}

// SceneContext installer — in the gameplay scene
public class GameplayInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        // Inherits bindings from ProjectContext automatically
        Container.Bind<ICombatSystem>().To<RealtimeCombatSystem>().AsSingle();
    }
}
```

### 3.3 Lifetime Management

| Lifetime | Description | Typical Use |
|---|---|---|
| **Singleton** | One instance per container (or global if in root) | Stateless services, caches, pools |
| **Transient** | New instance on every resolve | DTOs, commands, queries |
| **Scoped** | One instance per LifetimeScope (VContainer) | Scene- or phase-specific systems |

```csharp
// VContainer
builder.Register<IAudioService, FMODAudioService>(Lifetime.Singleton);
builder.Register<IDamageCalculator, DamageCalculator>(Lifetime.Transient);
builder.Register<IBattleState, BattleState>(Lifetime.Scoped);

// Zenject
Container.Bind<IAudioService>().To<FMODAudioService>().AsSingle();      // Singleton
Container.Bind<IDamageCalculator>().To<DamageCalculator>().AsTransient(); // Transient
Container.Bind<IBattleState>().To<BattleState>().AsCached();             // Scoped
```

### 3.4 Injection into MonoBehaviours

MonoBehaviours are instantiated by Unity (via `Instantiate`, scene loading, etc.), so they have no controllable constructor. The solutions are:

**Approach 1 — Method Injection with `[Inject]`:**

```csharp
public class HealthBar : MonoBehaviour
{
    private IHealthService _health;
    private ILocalizationService _localization;

    [Inject]
    public void Construct(IHealthService health, ILocalizationService localization)
    {
        _health = health;
        _localization = localization;
    }

    private void Update()
    {
        // Safe to use _health and _localization here
    }
}
```

For this to work, the MonoBehaviour must be registered in the container:

```csharp
// VContainer — register existing MonoBehaviour in the hierarchy
builder.RegisterComponentInHierarchy<HealthBar>();

// VContainer — register MonoBehaviour from a prefab
builder.RegisterComponentInNewPrefab(_healthBarPrefab, Lifetime.Transient);

// Zenject — register existing component in the scene
Container.Bind<HealthBar>().FromComponentInHierarchy().AsSingle();
```

**Approach 2 — Resolve manually (for runtime-spawned objects):**

```csharp
// VContainer
public class EnemySpawner
{
    private readonly IObjectResolver _resolver;
    private readonly EnemyConfig _config;

    public EnemySpawner(IObjectResolver resolver, EnemyConfig config)
    {
        _resolver = resolver;
        _config = config;
    }

    public Enemy Spawn(Vector3 position)
    {
        // Instantiate + Inject automatically
        var enemy = _resolver.Instantiate(_config.EnemyPrefab, position, Quaternion.identity);
        return enemy.GetComponent<Enemy>();
    }
}
```

---

## 4. Testing in Unity

### 4.1 Unity Test Framework (NUnit)

Unity Test Framework integrates a customized version of NUnit. Tests live in dedicated assemblies with references to `nunit.framework.dll`.

#### Initial Setup

1. In Unity, go to **Window → General → Test Runner**
2. Create a `Tests/` folder in your project
3. Create assembly definitions for your tests:

```
Assets/
├── Scripts/
│   ├── Runtime/
│   │   ├── MyGame.Runtime.asmdef     ← game code
│   │   ├── Combat/
│   │   ├── Inventory/
│   │   └── AI/
│   └── Editor/
│       └── MyGame.Editor.asmdef       ← editor tools
├── Tests/
│   ├── EditMode/
│   │   └── MyGame.Tests.EditMode.asmdef
│   └── PlayMode/
│       └── MyGame.Tests.PlayMode.asmdef
```

**Assembly Definition for Edit Mode Tests (`MyGame.Tests.EditMode.asmdef`):**
```json
{
    "name": "MyGame.Tests.EditMode",
    "rootNamespace": "MyGame.Tests.EditMode",
    "references": [
        "MyGame.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```

**Assembly Definition for Play Mode Tests (`MyGame.Tests.PlayMode.asmdef`):**
```json
{
    "name": "MyGame.Tests.PlayMode",
    "rootNamespace": "MyGame.Tests.PlayMode",
    "references": [
        "MyGame.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```

### 4.2 Edit Mode Tests vs Play Mode Tests

| Aspect | Edit Mode | Play Mode |
|---|---|---|
| **Runs in** | Editor, without entering Play mode | Enters Play Mode (or standalone build) |
| **Speed** | Fast (milliseconds) | Slow (needs to start Play Mode) |
| **Access** | Editor APIs + Runtime APIs | Runtime APIs only |
| **Coroutines** | `[UnityTest]` runs via `EditorApplication.update` | `[UnityTest]` runs as a real coroutine |
| **MonoBehaviours** | Can be tested, but without full lifecycle | Full lifecycle (Awake, Start, Update) |
| **Ideal use** | Pure logic, validators, editor tools | Integration, physics, coroutines, gameplay flow |

### 4.3 What to Test

**Pure logic (Edit Mode — high priority):**

```csharp
// Damage system with buffs — pure logic, zero Unity dependency
public class DamageCalculator : IDamageCalculator
{
    private readonly IBuffSystem _buffs;

    public DamageCalculator(IBuffSystem buffs)
    {
        _buffs = buffs;
    }

    public DamageResult Calculate(AttackData attack, DefenseData defense)
    {
        float raw = attack.BaseDamage * attack.Multiplier;
        float buffed = _buffs.ApplyAttackBuffs(raw, attack.AttackerID);
        float mitigated = Mathf.Max(0, buffed - defense.Armor);
        bool isCrit = attack.CritChance > Random.value;
        float final_dmg = isCrit ? mitigated * attack.CritMultiplier : mitigated;

        return new DamageResult
        {
            RawDamage = raw,
            FinalDamage = final_dmg,
            IsCritical = isCrit,
            DamageType = attack.DamageType
        };
    }
}
```

**ScriptableObject Validators (Edit Mode):**

```csharp
[Test]
public void AllWeaponSO_HaveValidDamageRange()
{
    var weapons = Resources.LoadAll<WeaponData>("Weapons");

    foreach (var weapon in weapons)
    {
        Assert.Greater(weapon.BaseDamage, 0f,
            $"Weapon '{weapon.name}' has invalid BaseDamage: {weapon.BaseDamage}");
        Assert.Greater(weapon.AttackSpeed, 0f,
            $"Weapon '{weapon.name}' has invalid AttackSpeed: {weapon.AttackSpeed}");
        Assert.IsNotNull(weapon.Icon,
            $"Weapon '{weapon.name}' is missing an icon sprite");
    }
}

[Test]
public void AllEnemySO_HaveUniqueIDs()
{
    var enemies = Resources.LoadAll<EnemyData>("Enemies");
    var ids = enemies.Select(e => e.EnemyID).ToList();
    var duplicates = ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);

    Assert.IsEmpty(duplicates.ToList(),
        $"Duplicate enemy IDs found: {string.Join(", ", duplicates)}");
}
```

**Systems and integration (Play Mode):**

```csharp
[UnityTest]
public IEnumerator EnemyDiesWhenHealthReachesZero()
{
    // Arrange
    var enemy = Object.Instantiate(Resources.Load<GameObject>("Prefabs/Enemy_Goblin"));
    var health = enemy.GetComponent<HealthComponent>();
    var initialHP = health.CurrentHP;

    // Act
    health.TakeDamage(initialHP + 10f); // Overkill
    yield return null; // Wait one frame to process

    // Assert
    Assert.IsTrue(health.IsDead);
    Assert.AreEqual(0f, health.CurrentHP);

    // Cleanup
    Object.Destroy(enemy);
}

[UnityTest]
public IEnumerator ProjectileTravelsAndHitsTarget()
{
    var shooter = new GameObject("Shooter").transform;
    var target = new GameObject("Target");
    target.transform.position = new Vector3(10, 0, 0);
    target.AddComponent<BoxCollider>();
    var hitDetector = target.AddComponent<HitDetector>();

    var projectile = Object.Instantiate(
        Resources.Load<GameObject>("Prefabs/Projectile"),
        shooter.position, Quaternion.identity);

    // Wait up to 3 seconds for the hit
    float elapsed = 0f;
    while (!hitDetector.WasHit && elapsed < 3f)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }

    Assert.IsTrue(hitDetector.WasHit, "Projectile did not hit target within 3 seconds");

    Object.Destroy(shooter.gameObject);
    Object.Destroy(target);
}
```

### 4.4 Mocking with NSubstitute

NSubstitute is a mocking library for .NET that works well with Unity. It creates substitutes for interfaces that let you control return values and verify calls.

#### Installation

**Via NuGet (copy DLL):**
1. Download NSubstitute from NuGet
2. Copy `NSubstitute.dll` and `Castle.Core.dll` to `Assets/Plugins/TestDependencies/`
3. Mark both as "Editor only" in the Inspector

**Via Unity Package (Thundernerd):**
```json
{
  "dependencies": {
    "net.tnrd.nsubstitute": "https://github.com/Thundernerd/Unity3D-NSubstitute.git"
  }
}
```

#### Real Test Examples with Mocking

```csharp
using NSubstitute;
using NUnit.Framework;

[TestFixture]
public class DamageCalculatorTests
{
    private IDamageCalculator _calculator;
    private IBuffSystem _mockBuffs;

    [SetUp]
    public void SetUp()
    {
        // Create mock of IBuffSystem
        _mockBuffs = Substitute.For<IBuffSystem>();

        // Configure default behavior
        _mockBuffs.ApplyAttackBuffs(Arg.Any<float>(), Arg.Any<int>())
            .Returns(callInfo => callInfo.ArgAt<float>(0)); // Returns damage unmodified

        _calculator = new DamageCalculator(_mockBuffs);
    }

    [Test]
    public void Calculate_WithArmor_ReducesDamage()
    {
        var attack = new AttackData
        {
            BaseDamage = 100f,
            Multiplier = 1f,
            CritChance = 0f, // No crit
            DamageType = DamageType.Physical
        };
        var defense = new DefenseData { Armor = 30f };

        var result = _calculator.Calculate(attack, defense);

        Assert.AreEqual(70f, result.FinalDamage, 0.01f);
        Assert.IsFalse(result.IsCritical);
    }

    [Test]
    public void Calculate_WithBuff_AppliesBuffMultiplier()
    {
        // Configure mock: buff doubles the damage
        _mockBuffs.ApplyAttackBuffs(100f, Arg.Any<int>())
            .Returns(200f);

        var attack = new AttackData
        {
            BaseDamage = 100f,
            Multiplier = 1f,
            CritChance = 0f,
            AttackerID = 42
        };
        var defense = new DefenseData { Armor = 0f };

        var result = _calculator.Calculate(attack, defense);

        Assert.AreEqual(200f, result.FinalDamage, 0.01f);
        // Verify that the buff system was called with the correct arguments
        _mockBuffs.Received(1).ApplyAttackBuffs(100f, 42);
    }
}
```

**Inventory test with multiple mocks:**

```csharp
[TestFixture]
public class InventoryServiceTests
{
    private InventoryService _inventory;
    private ISaveService _mockSave;
    private IAnalyticsService _mockAnalytics;
    private IItemDatabase _mockItemDB;

    [SetUp]
    public void SetUp()
    {
        _mockSave = Substitute.For<ISaveService>();
        _mockAnalytics = Substitute.For<IAnalyticsService>();
        _mockItemDB = Substitute.For<IItemDatabase>();

        // Configure item database with test data
        _mockItemDB.GetItem("sword_01").Returns(new ItemData
        {
            ID = "sword_01",
            Name = "Iron Sword",
            MaxStack = 1,
            Category = ItemCategory.Weapon
        });

        _mockItemDB.GetItem("potion_hp").Returns(new ItemData
        {
            ID = "potion_hp",
            Name = "Health Potion",
            MaxStack = 99,
            Category = ItemCategory.Consumable
        });

        _inventory = new InventoryService(_mockItemDB, _mockSave, _mockAnalytics, maxSlots: 20);
    }

    [Test]
    public void AddItem_WhenInventoryHasSpace_ReturnsTrue()
    {
        bool added = _inventory.AddItem("sword_01", quantity: 1);

        Assert.IsTrue(added);
        Assert.AreEqual(1, _inventory.GetItemCount("sword_01"));
        _mockAnalytics.Received(1).TrackEvent("item_added",
            Arg.Is<Dictionary<string, object>>(d => d["item_id"].Equals("sword_01")));
    }

    [Test]
    public void AddItem_StackableItem_StacksCorrectly()
    {
        _inventory.AddItem("potion_hp", quantity: 10);
        _inventory.AddItem("potion_hp", quantity: 5);

        Assert.AreEqual(15, _inventory.GetItemCount("potion_hp"));
    }

    [Test]
    public void AddItem_WhenInventoryFull_ReturnsFalse()
    {
        // Fill inventory with unique (non-stackable) items
        for (int i = 0; i < 20; i++)
        {
            string itemId = $"unique_item_{i}";
            _mockItemDB.GetItem(itemId).Returns(new ItemData
            {
                ID = itemId,
                MaxStack = 1,
                Category = ItemCategory.Misc
            });
            _inventory.AddItem(itemId, 1);
        }

        bool added = _inventory.AddItem("sword_01", 1);

        Assert.IsFalse(added);
    }

    [Test]
    public void RemoveItem_TriggersSave()
    {
        _inventory.AddItem("potion_hp", 5);
        _inventory.RemoveItem("potion_hp", 3);

        Assert.AreEqual(2, _inventory.GetItemCount("potion_hp"));
        _mockSave.Received().SaveData(Arg.Any<string>(), Arg.Any<InventorySaveData>());
    }
}
```

**State machine test with NSubstitute:**

```csharp
[TestFixture]
public class EnemyAIStateMachineTests
{
    private EnemyAIStateMachine _ai;
    private ISensorSystem _mockSensors;
    private INavigationService _mockNav;
    private ICombatSystem _mockCombat;

    [SetUp]
    public void SetUp()
    {
        _mockSensors = Substitute.For<ISensorSystem>();
        _mockNav = Substitute.For<INavigationService>();
        _mockCombat = Substitute.For<ICombatSystem>();

        _ai = new EnemyAIStateMachine(_mockSensors, _mockNav, _mockCombat,
            new EnemyAIConfig
            {
                DetectionRange = 10f,
                AttackRange = 2f,
                PatrolSpeed = 3f,
                ChaseSpeed = 6f
            });
    }

    [Test]
    public void WhenPlayerDetected_TransitionsFromPatrolToChase()
    {
        _ai.SetState(AIState.Patrol);
        _mockSensors.DetectPlayer(Arg.Any<float>())
            .Returns(new DetectionResult { Detected = true, Distance = 8f });

        _ai.Update(0.016f); // Simulate one frame

        Assert.AreEqual(AIState.Chase, _ai.CurrentState);
        _mockNav.Received(1).SetDestination(Arg.Any<Vector3>());
        _mockNav.Received(1).SetSpeed(6f); // Chase speed
    }

    [Test]
    public void WhenPlayerInAttackRange_TransitionsToAttack()
    {
        _ai.SetState(AIState.Chase);
        _mockSensors.DetectPlayer(Arg.Any<float>())
            .Returns(new DetectionResult { Detected = true, Distance = 1.5f });

        _ai.Update(0.016f);

        Assert.AreEqual(AIState.Attack, _ai.CurrentState);
        _mockCombat.Received(1).StartAttack(Arg.Any<AttackData>());
    }

    [Test]
    public void WhenPlayerLost_ReturnsToPatrolAfterTimeout()
    {
        _ai.SetState(AIState.Chase);
        _mockSensors.DetectPlayer(Arg.Any<float>())
            .Returns(new DetectionResult { Detected = false });

        // Simulate 5 seconds of "player lost"
        for (int i = 0; i < 300; i++)
            _ai.Update(0.016f);

        Assert.AreEqual(AIState.Patrol, _ai.CurrentState);
    }
}
```

### 4.5 TDD in Gamedev: Is It Worth It?

**When TDD works well in games:**

- Pure business logic: damage calculation, economy systems, crafting recipes, skill trees, quest state machines
- Deterministic systems: turn-based combat, card game rules, puzzle validation
- Serialization and data pipelines: save/load, network protocol parsing, asset validation
- Editor tools and build pipelines: asset importers, custom inspectors, build scripts

**When TDD is not practical:**

- Rapid prototyping: when you don't yet know what you're building, tests slow down iteration
- Visual/feel code: "does the jump feel right?" is not testable with assert
- Physics and animation: results depend on framerate and floating point; tests become fragile
- Throwaway game jam code

**Recommended pragmatic approach:**

```
Game Logic Layer    →  Aggressive TDD (interfaces + tests first)
Systems Layer       →  Tests after implementation stabilizes
MonoBehaviour Layer →  Integration tests in Play Mode (selective)
Visual/Feel Layer   →  Manual tests + QA
```

---

## 5. DI + Testing in the AI Workflow

### 5.1 Testable Code = Code That AI Understands Better

When systems use DI and interfaces, the code follows patterns that LLMs recognize and generate with high quality:

```csharp
// ✅ AI generates excellent implementations for clear contracts
public interface ILootSystem
{
    LootDrop GenerateLoot(EnemyData enemy, float luckMultiplier);
    bool ValidateLootTable(LootTable table);
}

// AI can generate both the implementation AND the tests
// because the contract is explicit and self-contained
```

**Why it works:**

- Interfaces define contracts with clear inputs/outputs — perfect for code generation
- Existing tests serve as executable specifications that AI uses as context
- DI enforces separation of concerns, resulting in smaller, more focused classes
- Mocks in tests document how systems interact with each other

### 5.2 Clear Contracts Between Systems

```csharp
// Contract between Combat and Inventory
public interface IEquipmentProvider
{
    WeaponData GetEquippedWeapon(int characterID);
    ArmorData GetEquippedArmor(int characterID, ArmorSlot slot);
    float GetTotalStatBonus(int characterID, StatType stat);
}

// Contract between AI and Navigation
public interface IPathfinder
{
    NavPath FindPath(Vector3 from, Vector3 to, NavQueryFilter filter);
    bool IsReachable(Vector3 target, float maxDistance);
    float EstimateDistance(Vector3 from, Vector3 to);
}

// Contract between Economy and UI
public interface ICurrencyService
{
    int GetBalance(CurrencyType type);
    TransactionResult TrySpend(CurrencyType type, int amount, string reason);
    event Action<CurrencyType, int> OnBalanceChanged;
}
```

When each system has a well-defined contract (interface), AI can generate implementations, tests, and integrations confidently because it understands exactly what each system expects and provides. Code reviews also become simpler — you only need to verify that the implementation satisfies the interface.

### 5.3 Practical Workflow with AI

```
1. Define interfaces (human or AI)
      ↓
2. Generate tests based on the interface (AI)
      ↓
3. Review tests — are they the spec? (human)
      ↓
4. Generate implementation (AI)
      ↓
5. Run tests — do they pass? (automated)
      ↓
6. Refine (human + AI in pair)
```

This cycle is extremely efficient because tests serve as automatic validation of AI output, and interfaces serve as high-quality prompts for code generation.

---

## 6. Step-by-Step Setup Guide

### 6.1 VContainer Setup (From Scratch)

**Step 1 — Install VContainer:**
```json
// Packages/manifest.json
{
  "dependencies": {
    "jp.hadashikick.vcontainer": "https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer#1.17.0"
  }
}
```

**Step 2 — Create interfaces for your systems:**
```csharp
// Assets/Scripts/Runtime/Interfaces/
public interface ICombatService
{
    DamageResult ProcessAttack(int attackerID, int defenderID);
}

public interface IWaveSystem
{
    void StartWave(int waveNumber);
    event Action<WaveCompleteData> OnWaveComplete;
}
```

**Step 3 — Create implementations:**
```csharp
public class CombatService : ICombatService
{
    private readonly IEquipmentProvider _equipment;
    private readonly IBuffSystem _buffs;

    public CombatService(IEquipmentProvider equipment, IBuffSystem buffs)
    {
        _equipment = equipment;
        _buffs = buffs;
    }

    public DamageResult ProcessAttack(int attackerID, int defenderID)
    {
        var weapon = _equipment.GetEquippedWeapon(attackerID);
        var armor = _equipment.GetEquippedArmor(defenderID, ArmorSlot.Chest);
        // ... combat logic
        return new DamageResult { /* ... */ };
    }
}
```

**Step 4 — Create the LifetimeScope:**
```csharp
using VContainer;
using VContainer.Unity;

public class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        // Services
        builder.Register<ICombatService, CombatService>(Lifetime.Singleton);
        builder.Register<IEquipmentProvider, EquipmentService>(Lifetime.Singleton);
        builder.Register<IBuffSystem, BuffSystem>(Lifetime.Singleton);
        builder.Register<IWaveSystem, WaveSystem>(Lifetime.Scoped);

        // Entry Points
        builder.RegisterEntryPoint<GameplayLoop>();

        // MonoBehaviours in the scene
        builder.RegisterComponentInHierarchy<PlayerView>();
        builder.RegisterComponentInHierarchy<HUDController>();
    }
}
```

**Step 5 — Add the LifetimeScope to the scene:** Create an empty GameObject → Add Component → `GameLifetimeScope`.

**Step 6 — Configure tests:**
```csharp
// Assets/Tests/EditMode/CombatServiceTests.cs
using NUnit.Framework;
using NSubstitute;

[TestFixture]
public class CombatServiceTests
{
    private CombatService _combat;
    private IEquipmentProvider _mockEquipment;
    private IBuffSystem _mockBuffs;

    [SetUp]
    public void SetUp()
    {
        _mockEquipment = Substitute.For<IEquipmentProvider>();
        _mockBuffs = Substitute.For<IBuffSystem>();
        _combat = new CombatService(_mockEquipment, _mockBuffs);
    }

    [Test]
    public void ProcessAttack_ReturnsPositiveDamage()
    {
        _mockEquipment.GetEquippedWeapon(1).Returns(new WeaponData { BaseDamage = 50 });
        _mockEquipment.GetEquippedArmor(2, ArmorSlot.Chest).Returns(new ArmorData { Defense = 10 });
        _mockBuffs.ApplyAttackBuffs(Arg.Any<float>(), 1).Returns(50f);

        var result = _combat.ProcessAttack(attackerID: 1, defenderID: 2);

        Assert.Greater(result.FinalDamage, 0f);
    }
}
```

### 6.2 Zenject Setup (From Scratch)

**Step 1 — Install Extenject** via Asset Store or Git URL.

**Step 2 — Create SceneContext:** On the root GameObject in the scene, add the `SceneContext` component.

**Step 3 — Create MonoInstaller:**
```csharp
using Zenject;

public class GameplayInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        Container.Bind<ICombatService>().To<CombatService>().AsSingle();
        Container.Bind<IEquipmentProvider>().To<EquipmentService>().AsSingle();
        Container.Bind<IBuffSystem>().To<BuffSystem>().AsSingle();

        Container.BindInterfacesAndSelfTo<GameplayLoop>().AsSingle();
    }
}
```

**Step 4 — Drag the Installer** to the "Mono Installers" field of the SceneContext in the Inspector.

**Step 5 — Tests are identical** to the VContainer example (same interfaces, same mocks), because the tested logic is independent of the DI framework.

---

## 7. Sources and References

- [VContainer — Official Documentation](https://vcontainer.hadashikick.jp/)
- [VContainer — GitHub (hadashiA)](https://github.com/hadashiA/VContainer)
- [VContainer — Comparison with Zenject](https://vcontainer.hadashikick.jp/comparing/comparing-to-zenject)
- [Zenject — GitHub (modesttree)](https://github.com/modesttree/Zenject)
- [Extenject — GitHub (Mathijs-Bakker)](https://github.com/Mathijs-Bakker/Extenject)
- [Extenject — Unity Asset Store](https://assetstore.unity.com/packages/tools/utilities/extenject-dependency-injection-ioc-157735)
- [Unity Test Framework — Edit Mode vs Play Mode](https://docs.unity3d.com/6000.4/Documentation/Manual/test-framework/edit-mode-vs-play-mode-tests.html)
- [Unity — How to Run Automated Tests](https://unity.com/how-to/automated-tests-unity-test-framework)
- [NSubstitute for Unity — Thundernerd](https://github.com/Thundernerd/Unity3D-NSubstitute)
- [The Joy of Mocking with NSubstitute (ilkinulas)](http://ilkinulas.github.io/programming/unity/2016/02/25/mocking-with-nsubstitute.html)
- [Practical Unit Testing in Unity3D (Medium)](https://medium.com/xrpractices/practical-unit-testing-in-unity3d-f8d5f777c5db)
- [DI with VContainer (DEV Community)](https://dev.to/longchau/dependency-injection-with-vcontainer-n9i)
- [DI Libraries for Unity — Comparison (GitHub Gist)](https://gist.github.com/dogfuntom/6aa1e7cb6e9bf3e482b6a6e790e28776)
