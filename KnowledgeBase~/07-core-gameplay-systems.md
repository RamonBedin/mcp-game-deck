# 07 — Core Gameplay Systems in Unity

> Research on architectures, patterns, and data-driven implementations for common gameplay systems in Unity games, with a focus on survivors/roguelikes and action games.

---

## Table of Contents

1. [Spawn System](#1-spawn-system)
2. [Weapon System](#2-weapon-system)
3. [Damage System](#3-damage-system)
4. [Health System](#4-health-system)
5. [Status Effects](#5-status-effects)
6. [Upgrade / Progression](#6-upgrade--progression)
7. [Loot System](#7-loot-system)
8. [Inventory System](#8-inventory-system)
9. [Experience / Leveling](#9-experience--leveling)
10. [Pickup System](#10-pickup-system)
11. [Camera System](#11-camera-system)
12. [System Relationship Diagram](#12-system-relationship-diagram)
13. [Sources and References](#13-sources-and-references)

---

## 1. Spawn System

### Recommended Architecture

The Spawn System is responsible for instantiating enemies, obstacles, and items into the game world. The ideal architecture combines three main patterns: **Object Pooling** for performance, **Strategy Pattern** for varying spawn behaviors, and **ScriptableObjects** for data-driven wave configuration.

The separation of responsibilities follows this model: a **SpawnManager** (MonoBehaviour) orchestrates the wave flow, a **PoolManager** manages object recycling, and **WaveConfig** (ScriptableObjects) define the data for each wave. This allows designers to create and adjust waves entirely through the Inspector, without touching code.

For survivors/endless-style games, the system must support both finite waves (boss waves) and continuous spawning with progressive difficulty scaling based on time or player level.

### Public Interface (API)

```csharp
// Wave configuration via ScriptableObject
[CreateAssetMenu(menuName = "Gameplay/Wave Config")]
public class WaveConfigSO : ScriptableObject
{
    public string waveId;
    public SpawnEntry[] spawnEntries;
    public float timeBetweenSpawns = 0.5f;
    public float delayBeforeNextWave = 3f;
    public WaveCompletionCondition completionCondition;

    [System.Serializable]
    public class SpawnEntry
    {
        public GameObject prefab;
        public int count;
        public float spawnWeight;           // for weighted random spawn
        public SpawnPatternSO spawnPattern; // circular, linear, random, etc.
    }
}

public enum WaveCompletionCondition { AllEnemiesKilled, TimerExpired, BossKilled }

// SpawnManager interface
public interface ISpawnManager
{
    void StartWave(WaveConfigSO config);
    void StopSpawning();
    void SetDifficultyMultiplier(float multiplier);
    event Action<int> OnWaveCompleted;      // wave index
    event Action OnAllWavesCompleted;
    int CurrentWaveIndex { get; }
    int ActiveEnemyCount { get; }
}

// Pool interface
public interface IObjectPool<T> where T : Component
{
    T Get();
    void Release(T obj);
    void Prewarm(int count);
}
```

### Data-Driven with ScriptableObjects

The configuration is hierarchical: a **WaveSequenceSO** contains a list of **WaveConfigSO**, each with multiple **SpawnEntry** entries. Each SpawnEntry references a prefab, count, weight, and a **SpawnPatternSO** that defines the spawn geometry (circular around the player, fixed map points, screen edges).

```
WaveSequenceSO (asset)
├── WaveConfigSO "Wave 1 - Easy"
│   ├── SpawnEntry: Zombie × 10, pattern: CircularAroundPlayer
│   └── SpawnEntry: Bat × 5, pattern: RandomInRadius
├── WaveConfigSO "Wave 2 - Medium"
│   ├── SpawnEntry: Zombie × 15, pattern: EdgeOfScreen
│   └── SpawnEntry: Skeleton × 8, pattern: CircularAroundPlayer
└── WaveConfigSO "Boss Wave"
    └── SpawnEntry: BossGoblin × 1, pattern: FixedPoint
```

For endless mode, an **EndlessScalingConfigSO** defines difficulty curves via `AnimationCurve`, controlling quantity, health, and enemy speed multipliers based on play time.

### Performance Considerations

**Object Pooling** is absolutely critical. Instantiating and destroying GameObjects on each spawn causes GC spikes and frame drops. From Unity 2021+, the `UnityEngine.Pool` namespace provides a native `ObjectPool<T>` with `defaultCapacity` and `maxSize` support. For survivors games with hundreds of simultaneous enemies, the pool must be prewarmed (`Prewarm`) during loading screens. Each enemy type should have its own separate pool, indexed by prefab ID. The `PoolManager` should use `Dictionary<int, ObjectPool<T>>` for O(1) lookup. Avoid `FindObjectsOfType` to count active enemies — maintain a `HashSet<Enemy>` updated via `OnGet/OnRelease` callbacks.

---

## 2. Weapon System

### Recommended Architecture

The Weapon System must be **modular and data-driven**, following the **Strategy Pattern** for attack behaviors and **Composition over Inheritance** for building weapons from components. The architecture separates data (ScriptableObjects) from behavior (MonoBehaviours/logic classes), allowing the creation of infinite weapon combinations without new classes.

The recommended pattern is: **WeaponDataSO** defines base stats (damage, cooldown, range), **WeaponBehaviour** (MonoBehaviour) manages the attack cycle, and **IAttackStrategy** defines how the attack is executed (projectile, melee sweep, AoE explosion). Modifiers (upgrades, buffs) are applied as layers on top of base stats.

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Weapon Data")]
public class WeaponDataSO : ScriptableObject
{
    public string weaponName;
    public WeaponType weaponType;        // Melee, Ranged, AoE, Beam
    public Sprite icon;
    public float baseDamage = 10f;
    public float attackCooldown = 1f;
    public float range = 5f;
    public float knockbackForce = 2f;
    public DamageType damageType;        // Physical, Fire, Ice, Lightning
    public int projectileCount = 1;
    public float projectileSpread = 0f;
    public GameObject projectilePrefab;
    public GameObject vfxPrefab;
    public AudioClip attackSound;
    public WeaponModifierSO[] baseModifiers;
}

public enum WeaponType { Melee, Ranged, AoE, Beam, Orbital, Summon }
public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Holy }

// Runtime weapon instance
public interface IWeapon
{
    WeaponDataSO Data { get; }
    int Level { get; }
    float GetModifiedDamage();
    float GetModifiedCooldown();
    void Attack(Transform origin, Vector3 direction);
    void Upgrade();
    void AddModifier(WeaponModifierSO modifier);
    void RemoveModifier(WeaponModifierSO modifier);
    event Action<IWeapon> OnAttack;
    event Action<IWeapon, int> OnLevelUp;
}

// Strategy for attack types
public interface IAttackStrategy
{
    void Execute(WeaponContext context);
}

public struct WeaponContext
{
    public Transform Origin;
    public Vector3 Direction;
    public float Damage;
    public float Range;
    public int ProjectileCount;
    public float Spread;
    public DamageType DamageType;
    public IObjectPool<Projectile> ProjectilePool;
}
```

### Data-Driven with ScriptableObjects

Each weapon is a **WeaponDataSO** asset. Upgrades are defined in **WeaponUpgradePathSO**, which lists the stats modified per level. Modifiers are composable **WeaponModifierSO** assets that alter stats (e.g. +20% damage, -10% cooldown, +1 projectile).

```
WeaponDataSO "Fire Wand"
├── baseDamage: 15
├── attackCooldown: 0.8
├── weaponType: Ranged
├── damageType: Fire
├── projectilePrefab: FireballProjectile
└── upgradePathSO:
    ├── Level 2: +5 damage, +1 projectile
    ├── Level 3: -0.1 cooldown, +10% area
    ├── Level 4: +10 damage, piercing = true
    └── Level 5: "Evolution" → merge with another item

WeaponModifierSO "Blessing of Speed"
├── cooldownMultiplier: 0.85
├── projectileSpeedMultiplier: 1.2
└── duration: -1 (permanent)
```

For the Vampire Survivors style, weapons attack automatically on the best target, using `ITargetingStrategy` (nearest, random, highest HP, lowest HP) which is also injected as a ScriptableObject.

### Performance Considerations

Projectiles must use **Object Pooling** without exception. For hundreds of simultaneous projectiles, consider moving movement logic to Jobs/Burst with `IJobParallelFor`. Avoid `GetComponent` every frame — cache references in the pool's `OnEnable`. For AoE weapons, use `Physics2D.OverlapCircleNonAlloc` (non-alloc version that avoids array allocation). Maintain a static reusable buffer for Physics query results.

---

## 3. Damage System

### Recommended Architecture

The Damage System acts as a **central mediator** between damage dealers and damage receivers. The main patterns are **Mediator Pattern** (the system receives damage requests and processes them), **Command Pattern** (each damage instance is a struct/object that can be inspected and modified by middlewares), and **interfaces** (`IDamageable`, `IDamageDealer`) to decouple sender and receiver.

The flow follows a pipeline: the attacker creates a `DamagePayload`, the system applies global modifiers (player damage buffs, target debuffs), calculates critical hits, applies target resistances, and finally delivers the final damage to the `IDamageable`.

### Public Interface (API)

```csharp
// Damage struct — value type to avoid GC
public struct DamagePayload
{
    public float BaseDamage;
    public float FinalDamage;           // after all calculations
    public DamageType Type;
    public bool IsCritical;
    public float CritMultiplier;
    public Vector3 HitPoint;
    public Vector3 HitDirection;
    public float KnockbackForce;
    public GameObject Source;            // who dealt the damage
    public GameObject Target;            // who received the damage
    public IWeapon SourceWeapon;         // weapon used
    public List<DamageModifier> Modifiers;
}

// Contract interfaces
public interface IDamageable
{
    void TakeDamage(DamagePayload payload);
    bool IsInvulnerable { get; }
    DamageResistances Resistances { get; }
}

public interface IDamageDealer
{
    float GetBaseDamage();
    DamageType GetDamageType();
    GameObject gameObject { get; }
}

// Configurable resistances
[System.Serializable]
public struct DamageResistances
{
    [Range(-1f, 1f)] public float physical;   // -1 = vulnerable, 0 = neutral, 1 = immune
    [Range(-1f, 1f)] public float fire;
    [Range(-1f, 1f)] public float ice;
    [Range(-1f, 1f)] public float lightning;
    [Range(-1f, 1f)] public float poison;

    public float GetResistance(DamageType type) => type switch
    {
        DamageType.Physical => physical,
        DamageType.Fire => fire,
        DamageType.Ice => ice,
        DamageType.Lightning => lightning,
        DamageType.Poison => poison,
        _ => 0f
    };
}

// Central damage service
public interface IDamageService
{
    DamagePayload CalculateDamage(IDamageDealer dealer, IDamageable target, WeaponContext ctx);
    void ApplyDamage(DamagePayload payload);
    void RegisterModifier(IDamageModifier modifier);   // global middlewares
    void UnregisterModifier(IDamageModifier modifier);
    event Action<DamagePayload> OnDamageDealt;
    event Action<DamagePayload> OnCriticalHit;
}

// Damage middleware (e.g. global +20% fire damage buff)
public interface IDamageModifier
{
    int Priority { get; }
    DamagePayload Modify(DamagePayload payload);
}
```

### Data-Driven with ScriptableObjects

Resistances for each enemy type are defined in **EnemyStatsSO**. Critical hit formulas and scaling are configurable via **DamageFormulaSO**, enabling balancing without recompiling.

```csharp
[CreateAssetMenu(menuName = "Gameplay/Damage Formula")]
public class DamageFormulaSO : ScriptableObject
{
    public float baseCritChance = 0.05f;         // 5%
    public float baseCritMultiplier = 1.5f;      // 150%
    public AnimationCurve levelScalingCurve;     // extra damage per level
    public bool allowNegativeResistance = true;  // vulnerabilities amplify damage

    public DamagePayload Calculate(DamagePayload raw, IDamageable target)
    {
        var payload = raw;

        // Critical hit roll
        float critRoll = UnityEngine.Random.value;
        payload.IsCritical = critRoll <= baseCritChance;
        payload.CritMultiplier = payload.IsCritical ? baseCritMultiplier : 1f;

        // Apply resistance
        float resistance = target.Resistances.GetResistance(payload.Type);
        float resistMult = allowNegativeResistance
            ? 1f - resistance          // -0.5 = 150% damage, 0.5 = 50% damage
            : Mathf.Max(0, 1f - resistance);

        payload.FinalDamage = payload.BaseDamage * payload.CritMultiplier * resistMult;
        return payload;
    }
}
```

### Performance Considerations

`DamagePayload` should be a **struct** (value type) to avoid heap allocation. The `OnDamageDealt` event can be invoked hundreds of times per second in survivors games — consider batch processing or throttling for visual effects (damage numbers). For massive collision scenarios, use layers and `ContactFilter2D` to filter only relevant collisions. Avoid boxing the struct when passing through interfaces — use generics where possible.

---

## 4. Health System

### Recommended Architecture

The Health System is a fundamental component that implements the **Observer Pattern** to notify HP changes to multiple listeners (UI, VFX, audio). The system is separated into: **HealthComponent** (MonoBehaviour on the GameObject), **HealthBarUI** (visual listener), and **DamageNumberSpawner** (listener for floating numbers). Communication is via C# events to maintain decoupling.

The system must support: invincibility frames (i-frames) after taking damage, health gates (prevent one-shot kills), shield/armor layers before actual HP, and passive regeneration.

### Public Interface (API)

```csharp
public interface IHealth
{
    float CurrentHP { get; }
    float MaxHP { get; }
    float HPPercent { get; }
    bool IsAlive { get; }
    bool IsInvulnerable { get; }

    void TakeDamage(DamagePayload payload);
    void Heal(float amount, bool allowOverheal = false);
    void SetMaxHP(float newMax, bool healToFull = false);
    void SetInvulnerable(float duration);

    event Action<DamagePayload> OnDamageTaken;
    event Action<float> OnHealed;               // amount
    event Action<float, float> OnHPChanged;     // current, max
    event Action OnDeath;
    event Action OnRevive;
}

public class HealthComponent : MonoBehaviour, IHealth, IDamageable
{
    [SerializeField] private CharacterStatsSO baseStats;
    [SerializeField] private float iFrameDuration = 0.1f;
    [SerializeField] private bool hasHealthGate = false;
    [SerializeField] private float healthGatePercent = 0.1f; // cannot die above 10% HP

    private float _currentHP;
    private float _maxHP;
    private float _iFrameTimer;
    private float _shieldAmount;

    // Implementation with i-frames
    public void TakeDamage(DamagePayload payload)
    {
        if (!IsAlive || IsInvulnerable) return;

        float damageToApply = payload.FinalDamage;

        // Absorb with shield first
        if (_shieldAmount > 0)
        {
            float absorbed = Mathf.Min(_shieldAmount, damageToApply);
            _shieldAmount -= absorbed;
            damageToApply -= absorbed;
        }

        // Health gate check
        if (hasHealthGate && _currentHP > _maxHP * healthGatePercent)
        {
            damageToApply = Mathf.Min(damageToApply,
                _currentHP - (_maxHP * healthGatePercent));
        }

        _currentHP = Mathf.Max(0, _currentHP - damageToApply);
        _iFrameTimer = iFrameDuration;

        OnDamageTaken?.Invoke(payload);
        OnHPChanged?.Invoke(_currentHP, _maxHP);

        if (_currentHP <= 0) OnDeath?.Invoke();
    }
    // ...
}
```

### Data-Driven with ScriptableObjects

Base health stats come from **CharacterStatsSO** (maxHP, regeneration, i-frame duration). Visual health bar configurations (colors per HP range, drain animation) live in **HealthBarConfigSO**. Damage number prefabs and their styles (color by damage type, scale for crits) live in **DamageNumberConfigSO**.

```
CharacterStatsSO "Player Base"
├── maxHP: 100
├── hpRegen: 0.5/s
├── iFrameDuration: 0.2s
├── shieldCapacity: 0
└── healthGatePercent: 0.1

DamageNumberConfigSO
├── normalColor: white
├── criticalColor: yellow, scale: 1.5x
├── healColor: green
├── fireColor: orange
├── poisonColor: purple
├── floatSpeed: 1.0
├── fadeTime: 0.8
└── randomOffsetRange: 0.3
```

### Performance Considerations

Damage numbers are a performance hotspot in survivors games. Use **Object Pooling** for TextMeshPro instances. Consider **sprite-based damage numbers** (pre-rendered digit sprites) instead of TMP for massive volume (500+ simultaneous numbers). For enemy health bars, use a **world-space Canvas with pooling** — do not create a Canvas per enemy. Alternatively, render health bars via **GPU instancing** with a custom shader for maximum scale. HP regeneration should use `Time.deltaTime` in `Update`, but consider tick-based (every 0.25s) to reduce overhead when hundreds of entities are regenerating.

---

## 5. Status Effects

### Recommended Architecture

The Status Effects system (buffs/debuffs) uses the **Composition Pattern** — each effect is an independent object with its own logic, duration, and stacking rules. The **ScriptableObject Factory Pattern** is ideal: the SO defines the effect data, and at runtime a `StatusEffectInstance` is created to track duration and stacks.

The architecture is: **StatusEffectSO** (data and configuration), **StatusEffectInstance** (runtime, timer, stacks), and **StatusEffectHandler** (MonoBehaviour that manages active effects on the entity). Each effect implements an `IStatusEffect` interface with hooks for Apply, Tick, Remove.

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Status Effect")]
public class StatusEffectSO : ScriptableObject
{
    public string effectId;
    public string displayName;
    public Sprite icon;
    public float duration = 5f;
    public float tickInterval = 1f;        // 0 = no tick, only applied on apply/remove
    public StackingRule stackingRule;
    public int maxStacks = 1;
    public StatusEffectType effectType;     // Buff or Debuff
    public StatModifier[] statModifiers;
    public VisualEffectReference vfxPrefab;

    [System.Serializable]
    public class StatModifier
    {
        public StatType stat;              // MaxHP, MoveSpeed, Damage, CritChance...
        public ModifierType modType;       // Flat, PercentAdd, PercentMult
        public float value;
    }
}

public enum StackingRule
{
    None,               // does not stack, ignores duplicates
    RefreshDuration,    // resets timer
    AddDuration,        // adds to duration
    AddStacks,          // adds stacks (each stack applies the effect again)
    Independent         // each application is tracked separately
}

public enum ModifierType { Flat, PercentAdd, PercentMult }
public enum StatType { MaxHP, MoveSpeed, Damage, AttackSpeed, CritChance, CritDamage, Armor, Regen }

// Handler on the entity
public interface IStatusEffectHandler
{
    IReadOnlyList<StatusEffectInstance> ActiveEffects { get; }
    bool Apply(StatusEffectSO effect, GameObject source);
    bool Remove(string effectId);
    void RemoveAll();
    void RemoveAllOfType(StatusEffectType type);
    bool HasEffect(string effectId);
    int GetStacks(string effectId);
    event Action<StatusEffectInstance> OnEffectApplied;
    event Action<StatusEffectInstance> OnEffectRemoved;
    event Action<StatusEffectInstance> OnEffectTick;
}

// Runtime instance
public class StatusEffectInstance
{
    public StatusEffectSO Data { get; }
    public float RemainingDuration { get; set; }
    public int CurrentStacks { get; set; }
    public GameObject Source { get; }

    public void Tick(float deltaTime) { /* decrement timer, apply tick effects */ }
    public bool IsExpired => RemainingDuration <= 0f;
}
```

### Data-Driven with ScriptableObjects

Each effect is an asset created in the project. For effects with custom logic (e.g. DoT that scales with stacks, freeze that stops movement), use **ScriptableObject polymorphism**: create subclasses such as `DamageOverTimeSO`, `SlowEffectSO`, `StunEffectSO` that override virtual methods of `StatusEffectSO`.

```
StatusEffectSO "Burning"
├── duration: 4s
├── tickInterval: 0.5s
├── stackingRule: AddStacks (max 5)
├── statModifiers: []
├── onTick: deal Fire damage = 3 * currentStacks
└── vfx: BurningParticles

StatusEffectSO "Frost Armor"
├── duration: 10s
├── tickInterval: 0 (passive)
├── stackingRule: RefreshDuration
├── statModifiers:
│   ├── Armor: +15 (Flat)
│   └── MoveSpeed: -0.1 (PercentAdd)  // small cost
└── onHit: apply "Chill" to the attacker
```

### Performance Considerations

To calculate modified stats, use a **Stat Aggregator** that caches the final value and only recalculates when the effects list changes (`isDirty` flag). Avoid iterating all effects every frame — use tick-based processing with `tickInterval`. The active effects list should be a pre-allocated `List<T>` (not `LinkedList`). Remove expired effects in batch at the end of the frame, not during iteration. For hundreds of entities with effects, consider a centralized **StatusEffectManager** that processes all ticks in batch via the Job System.

---

## 6. Upgrade / Progression

### Recommended Architecture

The progression system operates on two layers: **Run Progression** (upgrades within a run, like Vampire Survivors) and **Meta Progression** (permanent upgrades between runs). Both use the **Data-Driven Pattern** with ScriptableObjects and the **Observer Pattern** to notify dependent systems.

For level-up choices in the survivors style, the pattern is: **UpgradePoolSO** contains all possible upgrades, **UpgradeSelector** filters and offers N random options to the player, **UpgradeInstance** tracks the current level of each applied upgrade. The system needs eligibility rules (prerequisites, mutual exclusions, minimum level).

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Upgrade Definition")]
public class UpgradeDefinitionSO : ScriptableObject
{
    public string upgradeId;
    public string displayName;
    public string description;
    public Sprite icon;
    public Rarity rarity;
    public int maxLevel = 5;
    public UpgradeCategory category;     // Weapon, Passive, Utility
    public UpgradeLevelData[] levelData;
    public string[] prerequisites;       // IDs of required upgrades
    public string[] exclusions;          // IDs of mutually exclusive upgrades
    public string evolutionPartner;      // for fusion/evolution

    [System.Serializable]
    public class UpgradeLevelData
    {
        public string levelDescription;
        public StatModifier[] modifiers;
        public GameObject specialEffectPrefab; // visual effect for the upgrade
    }
}

public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
public enum UpgradeCategory { Weapon, Passive, Utility, Evolution }

// Progression service
public interface IUpgradeService
{
    IReadOnlyList<AppliedUpgrade> AppliedUpgrades { get; }
    UpgradeDefinitionSO[] GetRandomChoices(int count, UpgradeContext ctx);
    bool ApplyUpgrade(UpgradeDefinitionSO upgrade);
    int GetUpgradeLevel(string upgradeId);
    bool CanApply(UpgradeDefinitionSO upgrade);
    bool CheckEvolution(out EvolutionRecipeSO recipe);
    event Action<UpgradeDefinitionSO, int> OnUpgradeApplied; // def, new level
    event Action<EvolutionRecipeSO> OnEvolutionAvailable;
}

// Meta progression (between runs)
[CreateAssetMenu(menuName = "Gameplay/Meta Upgrade")]
public class MetaUpgradeSO : ScriptableObject
{
    public string upgradeId;
    public string displayName;
    public int maxLevel;
    public int[] costPerLevel;           // cost in permanent currency
    public StatModifier[] modifiersPerLevel;
}

public interface IMetaProgressionService
{
    int GetCurrency();
    bool PurchaseUpgrade(MetaUpgradeSO upgrade);
    int GetMetaLevel(string upgradeId);
    float GetTotalStatBonus(StatType stat); // sum of all meta upgrades
    void SaveProgress();
    void LoadProgress();
}
```

### Data-Driven with ScriptableObjects

**UpgradePoolSO** is the central asset that lists all available upgrades and their selection weights. **EvolutionRecipeSO** defines combinations (weapon + passive item = evolved weapon). Offer selection uses weighted random with rarity bias, and optionally a pity system to guarantee high rarities after many common selections.

```
UpgradePoolSO "Main Pool"
├── UpgradeDefinitionSO "Extra Projectile" (Common, weight: 100)
├── UpgradeDefinitionSO "Damage Up" (Common, weight: 100)
├── UpgradeDefinitionSO "Cooldown Reduction" (Uncommon, weight: 50)
├── UpgradeDefinitionSO "Critical Master" (Rare, weight: 20)
└── UpgradeDefinitionSO "Death Aura" (Legendary, weight: 5)

EvolutionRecipeSO "Holy Wand Evolution"
├── weapon: "Fire Wand" (level 5+)
├── passive: "Holy Cross" (level 5+)
└── result: "Divine Inferno Wand" (WeaponDataSO)

MetaUpgradeSO "Max HP Up"
├── maxLevel: 10
├── costPerLevel: [100, 200, 400, 800, 1600, ...]
└── modifierPerLevel: MaxHP +5 (Flat) per level
```

### Performance Considerations

Random upgrade selection should filter eligible upgrades before the weighted random — use a temporary `List<T>` with pooling (`ListPool<T>`) to avoid allocation. For meta progression, save data via `JsonUtility` + `PlayerPrefs` or a local file — keep the save lightweight (only IDs and levels, not full SOs). Stat recalculation after applying an upgrade should be lazy (dirty flag) and not eager, especially when multiple upgrades are applied in sequence.

---

## 7. Loot System

### Recommended Architecture

The Loot System uses **weighted random selection** with configurable tables. The architecture combines **Strategy Pattern** for different selection algorithms and **Flyweight Pattern** for shared item definitions. The system must support: nested drop tables (one table can reference another), minimum guarantees (pity), and contextual modifiers (luck stat, game area).

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Loot Table")]
public class LootTableSO : ScriptableObject
{
    public string tableId;
    public LootEntry[] entries;
    public int guaranteedDrops = 0;      // minimum items
    public int maxDrops = 3;             // maximum items
    public float nothingWeight = 50f;    // weight for "no drop"

    [System.Serializable]
    public class LootEntry
    {
        public ItemDefinitionSO item;
        public float weight = 10f;
        public Rarity rarity;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        public LootTableSO nestedTable;  // for nested tables
        public LootCondition[] conditions;
    }
}

[System.Serializable]
public class LootCondition
{
    public ConditionType type;           // MinPlayerLevel, HasItem, WaveNumber, etc.
    public string parameter;
    public float value;
}

// Loot service
public interface ILootService
{
    List<LootDrop> RollLoot(LootTableSO table, LootContext context);
    void RegisterPityTracker(string tableId);
    event Action<List<LootDrop>> OnLootGenerated;
}

public struct LootDrop
{
    public ItemDefinitionSO Item;
    public int Quantity;
    public Rarity ActualRarity;
    public Vector3 SpawnPosition;
}

public struct LootContext
{
    public int PlayerLevel;
    public float LuckModifier;           // 1.0 = normal, 2.0 = double chance for rares
    public int WaveNumber;
    public Vector3 DropPosition;
}

// Pity system
public class PityTracker
{
    private Dictionary<Rarity, int> _rollsSinceLastDrop;
    private Dictionary<Rarity, int> _pityThresholds;

    public bool ShouldForceRarity(Rarity rarity)
    {
        return _rollsSinceLastDrop[rarity] >= _pityThresholds[rarity];
    }

    public void RecordDrop(Rarity rarity)
    {
        _rollsSinceLastDrop[rarity] = 0;
        // Also reset lower rarities
        for (int i = 0; i < (int)rarity; i++)
            _rollsSinceLastDrop[(Rarity)i] = 0;
    }

    public void RecordMiss(Rarity rarity)
    {
        _rollsSinceLastDrop[rarity]++;
    }
}
```

### Data-Driven with ScriptableObjects

Loot tables are editable assets in the Inspector. Each enemy references a **LootTableSO** in its **EnemyStatsSO**. Bosses have special tables with guaranteed drops. The **PityConfigSO** defines thresholds per rarity globally.

```
LootTableSO "Common Enemy Drops"
├── LootEntry: Gold Coin (weight: 100, qty: 1-5)
├── LootEntry: Health Orb (weight: 30, qty: 1)
├── LootEntry: XP Gem Small (weight: 80, qty: 1-3)
├── LootEntry: nested → "Rare Equipment Table" (weight: 5)
└── nothingWeight: 50

PityConfigSO
├── Rare: 50 rolls without rare → force rare
├── Epic: 150 rolls without epic → force epic
└── Legendary: 500 rolls without legendary → force legendary
```

### Performance Considerations

The most efficient weighted random algorithm for static tables is the **Alias Method** (O(1) per roll after O(n) setup), ideal for tables that do not change frequently. For dynamic tables (modified by luck), use **prefix sum + binary search** (O(log n) per roll). Pre-compute total weights and cache them. For spawning loot items in the world, use Object Pooling for pickups. Avoid instantiating complex physics on each drop — use simple trigger colliders.

---

## 8. Inventory System

### Recommended Architecture

The Inventory System separates **Model** (inventory data), **View** (UI), and **Controller** (interaction logic) following the **MVC/MVP Pattern**. The model is UI-agnostic — a list or grid of slots containing item references and quantities. The **Observer Pattern** connects model changes to UI updates.

For survivors/roguelike games, the inventory tends to be simple (list of active weapons + passives). For RPGs, it can be grid-based (Diablo style) with typed equipment slots.

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Item Definition")]
public class ItemDefinitionSO : ScriptableObject
{
    public string itemId;
    public string displayName;
    public string description;
    public Sprite icon;
    public Rarity rarity;
    public ItemCategory category;
    public int maxStackSize = 99;
    public Vector2Int gridSize = Vector2Int.one; // for grid-based
    public EquipmentSlotType equipSlot;           // Head, Chest, Weapon, etc.
    public StatModifier[] passiveStats;
    public bool isConsumable;
    public StatusEffectSO consumeEffect;
}

public enum ItemCategory { Weapon, Armor, Consumable, Material, KeyItem }
public enum EquipmentSlotType { None, Head, Chest, Legs, Feet, MainHand, OffHand, Accessory1, Accessory2 }

// Inventory Model
public interface IInventory
{
    int Capacity { get; }
    int ItemCount { get; }
    bool AddItem(ItemDefinitionSO item, int quantity = 1);
    bool RemoveItem(string itemId, int quantity = 1);
    bool HasItem(string itemId, int requiredQuantity = 1);
    int GetQuantity(string itemId);
    ItemSlot GetSlot(int index);
    IReadOnlyList<ItemSlot> GetAllSlots();
    bool SwapSlots(int indexA, int indexB);
    event Action<int, ItemSlot> OnSlotChanged;   // slot index, new state
    event Action OnInventoryFull;
}

[System.Serializable]
public class ItemSlot
{
    public ItemDefinitionSO Item;
    public int Quantity;
    public bool IsEmpty => Item == null || Quantity <= 0;
}

// Equipment
public interface IEquipmentSystem
{
    bool Equip(ItemDefinitionSO item, EquipmentSlotType slot);
    ItemDefinitionSO Unequip(EquipmentSlotType slot);
    ItemDefinitionSO GetEquipped(EquipmentSlotType slot);
    IReadOnlyDictionary<EquipmentSlotType, ItemDefinitionSO> GetAllEquipped();
    float GetTotalStatBonus(StatType stat);
    event Action<EquipmentSlotType, ItemDefinitionSO> OnEquipmentChanged;
}
```

### Data-Driven with ScriptableObjects

Each item is an **ItemDefinitionSO**. For starting loadouts, use **StartingInventorySO** that lists items and quantities. Inventory configurations (capacity, layout type) live in **InventoryConfigSO**.

```
InventoryConfigSO "Player Backpack"
├── capacity: 24
├── layoutType: Grid (6 columns × 4 rows)
└── allowedCategories: [All]

InventoryConfigSO "Survivors Loadout"
├── weaponSlots: 6
├── passiveSlots: 6
└── layoutType: FixedSlots

StartingInventorySO "Default Start"
├── Item: "Wooden Sword" × 1
├── Item: "Health Potion" × 3
└── Item: "Gold" × 100
```

### Performance Considerations

For the UI, use **UI Toolkit** (Unity 6+) with a virtualized `ListView` for large inventories — only visible slots are rendered. If using uGUI, implement UI slot pooling manually. Inventory serialization for save/load should use IDs + quantities (not direct SO references) and resolve references via a registry/database on load. For grid-based inventories with multi-slot items (2×3), use a 2D bitmask for fast available space checks.

---

## 9. Experience / Leveling

### Recommended Architecture

The Experience System is relatively simple but critical for game feel. It uses the **Observer Pattern** to notify level-ups, and **ScriptableObjects** to define XP curves and stat growth. The separation is: **ExperienceComponent** (tracks XP and level), **LevelConfigSO** (defines required XP per level and stat curves), and the **Upgrade/Progression** system that consumes level-up events.

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Level Config")]
public class LevelConfigSO : ScriptableObject
{
    public int maxLevel = 100;
    public XPFormulaType formulaType;
    public float baseXP = 100f;
    public float exponent = 1.5f;          // for exponential formula
    public float linearIncrement = 50f;    // for linear formula
    public AnimationCurve customCurve;     // for custom curve
    public StatGrowth[] statGrowthPerLevel;

    public int GetXPForLevel(int level)
    {
        return formulaType switch
        {
            // Exponential: XP = base * level^exponent
            XPFormulaType.Exponential =>
                Mathf.RoundToInt(baseXP * Mathf.Pow(level, exponent)),

            // Linear: XP = base + (level * increment)
            XPFormulaType.Linear =>
                Mathf.RoundToInt(baseXP + (level * linearIncrement)),

            // Quadratic: XP = base * level^2
            XPFormulaType.Quadratic =>
                Mathf.RoundToInt(baseXP * level * level),

            // Custom curve (0-1 normalized to max level)
            XPFormulaType.Custom =>
                Mathf.RoundToInt(customCurve.Evaluate((float)level / maxLevel) * baseXP * maxLevel),

            _ => Mathf.RoundToInt(baseXP * level)
        };
    }

    [System.Serializable]
    public class StatGrowth
    {
        public StatType stat;
        public float baseValue;
        public float growthPerLevel;
        public AnimationCurve growthCurve; // override for non-linear growth
    }
}

public enum XPFormulaType { Linear, Quadratic, Exponential, Custom }

public interface IExperienceSystem
{
    int CurrentLevel { get; }
    int CurrentXP { get; }
    int XPToNextLevel { get; }
    float XPPercent { get; }             // 0-1 progress in current level
    void AddXP(int amount);
    float GetStatAtLevel(StatType stat, int level);
    event Action<int> OnLevelUp;          // new level
    event Action<int, int> OnXPGained;    // amount, total
}
```

### Data-Driven with ScriptableObjects

**LevelConfigSO** centralizes all progression configuration. `AnimationCurve` in the Inspector lets designers visually tune the difficulty curve. For games with multiple characters, each references a different **LevelConfigSO** with adjusted curves.

```
LevelConfigSO "Survivors Standard"
├── maxLevel: 50 (per run)
├── formulaType: Exponential
├── baseXP: 10
├── exponent: 1.3
│   Level 1: 10 XP    Level 10: ~200 XP
│   Level 20: ~800 XP  Level 50: ~5000 XP
└── statGrowth:
    ├── MaxHP: base 100, +8/level
    ├── Damage: base 10, +2/level (curve: slightly exponential)
    └── MoveSpeed: base 5, +0.05/level (caps via curve)

LevelConfigSO "Meta Account Level"
├── maxLevel: 999
├── formulaType: Custom (AnimationCurve with slow start, fast mid, slow endgame)
└── rewards defined at milestone levels
```

### Performance Considerations

XP from multiple simultaneous sources (many gems collected at once) should be batched into a single `AddXP` call per frame to avoid multiple level-up events in one frame. Pre-compute the XP table per level into an array in `Awake()` for O(1) lookup instead of recalculating the formula. The `GetStatAtLevel` calculation should also be cached when used repeatedly (e.g. stat previews in the UI).

---

## 10. Pickup System

### Recommended Architecture

The Pickup System manages collectible items in the world (XP gems, gold, health orbs, power-ups). It combines **Object Pooling** for performance, **Magnet/Attraction Pattern** for auto-collection, and **Observer Pattern** to notify other systems (XP, Inventory). In survivors games, pickups can number in the hundreds to thousands simultaneously on screen, making performance the top priority.

The architecture separates: **PickupComponent** (MonoBehaviour on the collectible), **PickupCollector** (on the player, defines radius and magnet), and **PickupManager** (central, manages pools and spawning).

### Public Interface (API)

```csharp
[CreateAssetMenu(menuName = "Gameplay/Pickup Definition")]
public class PickupDefinitionSO : ScriptableObject
{
    public string pickupId;
    public PickupType pickupType;        // XP, Gold, Health, PowerUp
    public Sprite sprite;
    public float baseValue = 1f;
    public float magnetPriority = 1f;    // magnet priority (1 = normal)
    public float lifetime = 30f;         // time before disappearing
    public bool autoCollectOnMagnet = true;
    public AudioClip collectSound;
    public GameObject collectVFX;
}

public enum PickupType { XPGem, Gold, HealthOrb, Magnet, Bomb, Chest, PowerUp }

// Collector on the player
public interface IPickupCollector
{
    float CollectRadius { get; set; }       // normal collection radius
    float MagnetRadius { get; set; }        // magnet effect radius
    float MagnetForce { get; set; }         // attraction speed
    void ActivateMagnet(float duration);    // temporary magnet (collects everything)
    void CollectAllOnScreen();              // collects everything on screen
    event Action<PickupDefinitionSO, float> OnPickupCollected; // def, value
}

// Component on the pickup
public class PickupComponent : MonoBehaviour
{
    public PickupDefinitionSO Definition { get; private set; }
    public float Value { get; set; }

    private Transform _magnetTarget;
    private float _magnetSpeed;
    private bool _isBeingMagneted;
    private float _lifetimeRemaining;

    public void Initialize(PickupDefinitionSO def, float value, Vector3 position)
    {
        Definition = def;
        Value = value;
        transform.position = position;
        _lifetimeRemaining = def.lifetime;
        _isBeingMagneted = false;
    }

    public void StartMagnet(Transform target, float speed)
    {
        _magnetTarget = target;
        _magnetSpeed = speed;
        _isBeingMagneted = true;
    }

    private void Update()
    {
        if (_isBeingMagneted && _magnetTarget != null)
        {
            // Move toward player with acceleration
            _magnetSpeed += Time.deltaTime * 20f; // acceleration
            transform.position = Vector3.MoveTowards(
                transform.position, _magnetTarget.position,
                _magnetSpeed * Time.deltaTime);
        }

        _lifetimeRemaining -= Time.deltaTime;
        if (_lifetimeRemaining <= 0f) ReturnToPool();
    }
}
```

### Data-Driven with ScriptableObjects

Each pickup type is a **PickupDefinitionSO** asset. The collector configuration (radii, speeds) lives in **PlayerStatsSO** and is modifiable by upgrades. Pickup spawn tables are part of **LootTableSO**.

```
PickupDefinitionSO "Blue XP Gem"
├── pickupType: XPGem
├── baseValue: 1
├── magnetPriority: 1
├── lifetime: 60s
└── autoCollect: true

PickupDefinitionSO "Red XP Gem"
├── pickupType: XPGem
├── baseValue: 10
├── magnetPriority: 2 (more attracted)
├── lifetime: 90s
└── autoCollect: true

PickupDefinitionSO "Magnet Item"
├── pickupType: Magnet
├── effect: ActivateMagnet(duration: 5s)
└── collectsAllOnScreen: true
```

### Performance Considerations

Pickups are the biggest performance bottleneck in survivors games. Essential strategies: use strict **Object Pooling** with generous prewarming (pool of 500+ gems). For collection detection, do NOT use `OnTriggerEnter2D` with a Rigidbody on each pickup — this overloads the physics engine. Instead, use a **spatial hashing system** or `Physics2D.OverlapCircleNonAlloc` called by the `PickupCollector` every N frames. Consider **SpriteRenderer** instead of UI for world-space pickups. For magnets with hundreds of pickups moving simultaneously, batch movement via `TransformAccessArray` + `IJobParallelForTransform` (Jobs). Disable colliders and Rigidbody on pickups that are already in the "magneted" state — they no longer need physics. Use `SpriteRenderer.enabled = false` before returning to pool to avoid an unwanted visual frame.

---

## 11. Camera System

### Recommended Architecture

For cameras in Unity, **Cinemachine** (now Unity Cinemachine 3.x in Unity 6) is the industry-standard solution. Avoid implementing a follow camera from scratch — Cinemachine handles follow, confiner, look-ahead, damping, and blending between virtual cameras out of the box. The pattern is an implicit **State Machine**: multiple Virtual Cameras with different configurations, and the Cinemachine Brain automatically selects the one with the highest priority.

For screen shake effects, the **Cinemachine Impulse** system is the recommended approach — emitters generate impulses that listeners on the camera consume, with distance attenuation and decay control.

### Public Interface (API)

```csharp
// Wrapper service to abstract Cinemachine from the rest of the code
public interface ICameraService
{
    void SetFollowTarget(Transform target);
    void SetLookAtTarget(Transform target);
    void TriggerShake(ShakePresetSO preset, Vector3 position);
    void TriggerShake(float intensity, float duration);
    void SetZoom(float orthoSize, float transitionTime);
    void FocusOnPoint(Vector3 point, float duration, float zoom);
    void ReturnToPlayer();
    void SetConfinerBounds(Collider2D bounds);
    void EnableSlowMotion(float timeScale, float duration);
    float CurrentZoom { get; }
}

[CreateAssetMenu(menuName = "Gameplay/Camera/Shake Preset")]
public class ShakePresetSO : ScriptableObject
{
    public string presetName;
    public float intensity = 1f;
    public float duration = 0.3f;
    public float frequency = 10f;
    public AnimationCurve decayCurve;    // fade out
    public Vector3 direction;            // (0,0,0) = omnidirectional
    public float attenuationDistance = 20f;
}

// Example of Cinemachine configuration via code
public class CameraSetup : MonoBehaviour
{
    [Header("Follow Settings")]
    public float damping = 0.5f;
    public float lookAheadTime = 0.3f;   // camera anticipates movement direction
    public float lookAheadSmoothing = 5f;
    public float screenX = 0.5f;         // horizontal offset (0.5 = center)
    public float screenY = 0.5f;         // vertical offset

    [Header("Confiner")]
    public Collider2D confinerBounds;    // PolygonCollider2D delimiting the map

    [Header("Zoom")]
    public float defaultOrthoSize = 8f;
    public float minZoom = 4f;
    public float maxZoom = 12f;
}
```

### Data-Driven with ScriptableObjects

Shake presets for different events (explosion, hit, boss slam) are **ShakePresetSO** assets. Camera configurations per zone/level live in **CameraConfigSO**. Cinemachine Noise profiles (Perlin noise for hand-held effect) are native Cinemachine assets.

```
ShakePresetSO "Explosion"
├── intensity: 3.0
├── duration: 0.4s
├── frequency: 15
├── decayCurve: fast exponential decay
└── attenuationDistance: 30 units

ShakePresetSO "Player Hit"
├── intensity: 1.0
├── duration: 0.15s
├── frequency: 20
└── direction: toward hit source

ShakePresetSO "Boss Slam"
├── intensity: 5.0
├── duration: 0.6s
├── frequency: 8
└── decayCurve: two-bounce decay

CameraConfigSO "Boss Arena"
├── orthoSize: 10 (zoom out to see the entire arena)
├── confiner: BossArenaCollider
├── lookAheadTime: 0 (focus on center)
└── transitionTime: 1.5s
```

### Performance Considerations

Cinemachine is extremely optimized and adds minimal overhead. Do not create Virtual Cameras dynamically — instantiate all of them at load time and activate/deactivate by priority. For screen shake, the Impulse system is more performant than manipulating `transform.position` directly, as it operates within the Cinemachine pipeline without conflicting with follow. For slow-motion (`Time.timeScale`), adjust `CinemachineBrain.m_IgnoreTimeScale` to keep the camera responsive. In pixel art games, use **Pixel Perfect Camera** integrated with Cinemachine — pay attention to snapping which can cause visual jitter (configure the `CinemachinePixelPerfect` extension).

---

## 12. System Relationship Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    GAME MANAGER / GAME LOOP                      │
└───────┬──────────────────────────────┬──────────────────────────┘
        │                              │
        ▼                              ▼
┌───────────────┐             ┌─────────────────┐
│ SPAWN SYSTEM  │─── spawns ──│  ENEMY ENTITIES  │
│               │  enemies    │ (Health, AI, SO) │
└───────┬───────┘             └────────┬─────────┘
        │                              │
        │ configures waves             │ on death
        │ via SO                       │
        ▼                              ▼
┌───────────────┐             ┌─────────────────┐
│  WAVE CONFIG  │             │   LOOT SYSTEM   │──── drops ────┐
│ (ScriptableObj)│            │  (Drop Tables)  │               │
└───────────────┘             └────────┬────────┘               │
                                       │                        │
                                       ▼                        ▼
                              ┌────────────────┐      ┌─────────────────┐
                              │ PICKUP SYSTEM  │      │ INVENTORY SYSTEM│
                              │ (Pool, Magnet) │      │ (Equip, Items)  │
                              └───────┬────────┘      └────────┬────────┘
                                      │ collected              │ equip
                                      ▼                        ▼
                              ┌────────────────┐      ┌─────────────────┐
                              │   XP / LEVEL   │      │ WEAPON SYSTEM   │
                              │   SYSTEM       │      │ (Attack, Mods)  │
                              └───────┬────────┘      └────────┬────────┘
                                      │ level up               │ attacks
                                      ▼                        ▼
                              ┌────────────────┐      ┌─────────────────┐
                              │   UPGRADE /    │      │  DAMAGE SYSTEM  │
                              │  PROGRESSION   │      │ (Calc, Pipeline)│
                              └───────┬────────┘      └────────┬────────┘
                                      │ modifies               │ applies
                                      │ stats/weapons          │ damage
                                      ▼                        ▼
                              ┌────────────────┐      ┌─────────────────┐
                              │ STATUS EFFECTS │◄────►│  HEALTH SYSTEM  │
                              │ (Buffs/Debuffs)│      │ (HP, Shield, UI)│
                              └────────────────┘      └────────┬────────┘
                                                               │
                                                               │ on death
                                                               ▼
                                                      ┌─────────────────┐
                                                      │  CAMERA SYSTEM  │
                                                      │ (Shake on hit,  │
                                                      │  zoom on boss)  │
                                                      └─────────────────┘
```

### Main Flow (Survivors-like)

```
1. SPAWN SYSTEM reads WaveConfigSO → instantiates enemies via Object Pool
2. WEAPON SYSTEM attacks automatically → generates DamagePayload
3. DAMAGE SYSTEM calculates final damage (crits, resistances, buffs)
4. HEALTH SYSTEM applies damage → CAMERA SYSTEM triggers screen shake
5. Enemy dies → LOOT SYSTEM rolls drop table → spawns pickups via pool
6. PICKUP SYSTEM detects proximity → magnet attracts → player collects
7. XP SYSTEM accumulates → level up → UPGRADE SYSTEM presents choices
8. Player chooses upgrade → modifies WEAPON/STATUS/STATS
9. STATUS EFFECTS apply temporary buffs/debuffs to stats
10. Loop returns to step 1 with scaled difficulty
```

### Inter-System Communication

Communication is primarily via **C# events** (not UnityEvents, for performance) and **ScriptableObject Event Channels** (for designer-friendly cross-scene communication). Systems never reference other systems directly — they use interfaces and events. A **ServiceLocator** or **Dependency Injection** (VContainer, Zenject) provides references.

```
Recommended communication pattern:
- Intra-system: direct C# events (Action<T>)
- Cross-system: SO Event Channels or interfaces via DI
- UI ↔ Gameplay: Observer via interfaces (IHealth → HealthBarUI)
- Save/Load: Serialization of IDs + values, resolve refs on load
```

---

## 13. Sources and References

### Official Unity Documentation

- [Create Modular Game Architecture with ScriptableObjects (Unity 6)](https://unity.com/resources/create-modular-game-architecture-scriptableobjects-unity-6) — Official guide to modular architecture with SOs
- [Architect Game Code with ScriptableObjects](https://unity.com/how-to/architect-game-code-scriptable-objects) — Architecture best practices
- [ScriptableObjects as Event Channels](https://unity.com/how-to/scriptableobjects-event-channels-game-code) — Event pattern via SO
- [PaddleGameSO Demo Project (GitHub)](https://github.com/UnityTechnologies/PaddleGameSO) — Official SO architecture demo project
- [Object Pooling in Unity](https://unity.com/how-to/use-object-pooling-boost-performance-c-scripts-unity) — Official pooling guide
- [Unity ObjectPool API](https://docs.unity3d.com/ScriptReference/Pool.ObjectPool_1.html) — Native pooling API reference
- [Cinemachine 2D Tips and Tricks](https://unity.com/blog/engine-platform/cinemachine-2d-tips-and-tricks) — Cinemachine 2D best practices
- [Cinemachine Impulse (Screen Shake)](https://docs.unity3d.com/Packages/com.unity.cinemachine@2.3/manual/CinemachineImpulse.html) — Camera shake documentation
- [Cinemachine Noise Profiles](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.1/manual/setup-apply-noise.html) — Noise configuration for camera

### Articles and Tutorials

- [Unity Architecture: Scriptable Object Pattern (Medium)](https://medium.com/@simon.nordon/unity-architecture-scriptable-object-pattern-0a6c25b2d741) — Detailed analysis of the SO pattern
- [ScriptableObjects and Data-Driven Design](https://gamineai.com/blog/scriptableobjects-and-data-driven-design-in-unity) — Data-driven design
- [Building Modular Game Systems with SOs](https://peerdh.com/blogs/programming-insights/building-modular-game-systems-with-scriptable-objects-in-unity) — Modular systems
- [Creating a Modular Health/Damage System (Medium)](https://aliemreonur.medium.com/creating-a-modular-health-damage-system-a118375c5ab6) — Modular damage system
- [A Framework for Status Effects](https://straypixels.net/statuseffects-framework/) — Complete status effects framework
- [Flexible Buff System with Scriptable Objects](https://www.jonathanyu.xyz/2016/12/30/buff-system-with-scriptable-objects-for-unity/) — Buff system tutorial with SOs
- [Weighted Random System for Loot Drops](https://outscal.com/blog/unity-weighted-random-system-loot-drops) — Weighted random system
- [Unity Weighted Loot Table (Medium)](https://medium.com/@kshesho/unity-how-to-create-a-weighted-loot-table-3bcbf478eaf9) — Loot table implementation
- [Level Systems and Character Growth in RPGs](https://pavcreations.com/level-systems-and-character-growth-in-rpg-games/) — XP formulas and character growth
- [Grid-Based Inventory System in Unity](https://www.wayline.io/blog/unity-grid-inventory-system) — Grid-based inventory
- [Object Pooling Best Practices (Wayline)](https://www.wayline.io/blog/implementing-object-pooling-in-unity-for-performance) — Pooling for performance
- [Unity Object Pool Tutorial 2026](https://makaka.org/unity-tutorials/object-pool) — Updated pooling tutorial

### Open Source Repositories

- [ScriptableObject Pooling Framework (GitHub)](https://github.com/onewinter/ScriptObjPoolingFramework) — SO-based pooling framework
- [Modular Weapon System (GitHub)](https://github.com/IvanAfanasiev/Modular-Weapon-System) — Modular weapon system with SOs and DOTween
- [ScriptableObject-Based Guns (LlamaCademy)](https://github.com/llamacademy/scriptable-object-based-guns) — SO-based gun system with video tutorial
- [Flexible Buff System (GitHub)](https://github.com/xjjon/unity-flexible-buff-system) — Flexible buff framework
- [ModiBuff (GitHub)](https://github.com/Chillu1/ModiBuff) — Modifier library with 0 GC allocation, fully pooled
- [Diablo 2-style Inventory (GitHub)](https://github.com/FarrokhGames/Inventory) — Grid inventory with multi-size items
- [Wave Spawn System (GitHub)](https://github.com/berkkl/Unity-Wave-Spawn-System) — Wave-based spawn system

### Game Design Reference

- [The Secret Sauce of Vampire Survivors](https://jboger.substack.com/p/the-secret-sauce-of-vampire-survivors) — Design analysis of the progression loop
- [Creating a Vampire Survivors-style Rogue-like in Unity](https://blog.terresquall.com/2024/07/creating-a-rogue-like-vampire-survivors-part-22/) — Complete tutorial series
- [SO-Based Gun System Tutorial (Unity Forums)](https://forum.unity.com/threads/tutorial-make-a-scriptableobject-based-gun-system-from-scratch.1347170/) — Step-by-step tutorial

---

> **Document generated as part of the Unity architecture research series for action/survivors games.**
> Previous document: [06-mobile-optimization.md](./06-mobile-optimization.md)
