# ScriptableObjects as a Foundation for Data-Driven Design in Unity

## Table of Contents

1. [What are ScriptableObjects](#1-what-are-scriptableobjects)
2. [ScriptableObjects vs MonoBehaviours](#2-scriptableobjects-vs-monobehaviours)
3. [Patterns with ScriptableObjects](#3-patterns-with-scriptableobjects)
   - 3.1 Data Containers
   - 3.2 Event Channels
   - 3.3 Runtime Sets
   - 3.4 Delegate Objects (Strategy Pattern)
   - 3.5 Enum Replacement
   - 3.6 Flyweight Pattern
4. [ScriptableObjects + Addressables](#4-scriptableobjects--addressables)
5. [Pitfalls and Cautions](#5-pitfalls-and-cautions)
6. [Designer-Friendly Workflows](#6-designer-friendly-workflows)
7. [Ryan Hipple — Game Architecture with ScriptableObjects](#7-ryan-hipple--game-architecture-with-scriptableobjects)
8. [PaddleGameSO — Unity Demo Project](#8-paddlegameso--unity-demo-project)
9. [Pattern Connection Diagram](#9-pattern-connection-diagram)
10. [Sources and References](#10-sources-and-references)

---

## 1. What are ScriptableObjects

ScriptableObjects are data containers that exist as **assets in the project** (`.asset` files), independent of any GameObject or scene. They inherit from `UnityEngine.ScriptableObject` instead of `MonoBehaviour`, which means they do not need to be attached to a GameObject to exist.

Their fundamental purpose is to **store shared data** and **define reusable behaviors** outside the lifecycle of scenes and GameObjects. In practical terms, they are serialized files that live in the `Assets/` folder and can be referenced by any component in any scene.

```csharp
using UnityEngine;

[CreateAssetMenu(fileName = "NewWeaponData", menuName = "Game Data/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int damage;
    public float attackSpeed;
    public float range;
    public Sprite icon;
    public AudioClip attackSound;
}
```

By marking the class with `[CreateAssetMenu]`, designers can create instances directly from the **Assets > Create > Game Data > Weapon Data** menu in the Unity Editor, without writing a single line of code.

---

## 2. ScriptableObjects vs MonoBehaviours

| Characteristic | MonoBehaviour | ScriptableObject |
|---|---|---|
| Requires GameObject | Yes | No |
| Lives in scene | Yes | No (lives as an asset) |
| Scene callbacks (Update, Start) | Yes | No |
| Serialization | Per scene/prefab | As an independent asset |
| Instances in memory | One per GameObject | One per asset (shared) |
| Persistence in Editor | Reset on exiting Play Mode | **Persists** in Editor (pitfall!) |
| Ideal for | Scene logic, behavior | Data, configuration, communication |

The core difference is one of **lifecycle**: MonoBehaviours exist within scenes and are destroyed with them. ScriptableObjects exist in the project, are independent of scenes, and can be referenced cross-cutting by any system.

```csharp
// MonoBehaviour — data duplicated in each instance
public class Enemy : MonoBehaviour
{
    public int maxHealth = 100;      // Copy in EVERY enemy
    public float moveSpeed = 5f;     // Copy in EVERY enemy
    public string enemyName;         // Copy in EVERY enemy
}

// ScriptableObject — data shared via reference
public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyData data;  // Shared reference
    private int currentHealth;

    void Start()
    {
        currentHealth = data.maxHealth;  // Reads from the shared asset
    }
}
```

---

## 3. Patterns with ScriptableObjects

### 3.1 Data Containers (Stats, Configs, Wave Definitions)

The most basic and most widely used pattern. ScriptableObjects store **static data** that does not change at runtime (or changes rarely), serving as configuration "sheets".

```csharp
[CreateAssetMenu(menuName = "Game Data/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    [Header("Identity")]
    public string displayName;
    public Sprite portrait;

    [Header("Base Stats")]
    public int maxHealth = 100;
    public float moveSpeed = 3f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;

    [Header("Loot")]
    public int xpReward = 50;
    public LootTable lootTable;
}
```

**Wave Definitions** — wave definition for a tower defense:

```csharp
[CreateAssetMenu(menuName = "Game Data/Wave Definition")]
public class WaveDefinition : ScriptableObject
{
    [System.Serializable]
    public struct SpawnGroup
    {
        public EnemyConfig enemyType;
        public int count;
        public float spawnInterval;
        public float delayBeforeGroup;
    }

    public string waveName;
    public SpawnGroup[] spawnGroups;
    public float timeBetweenWaves = 10f;
}
```

**Usage in MonoBehaviour:**

```csharp
public class WaveSpawner : MonoBehaviour
{
    [SerializeField] private WaveDefinition[] waves;
    private int currentWaveIndex;

    public IEnumerator SpawnWave()
    {
        var wave = waves[currentWaveIndex];

        foreach (var group in wave.spawnGroups)
        {
            yield return new WaitForSeconds(group.delayBeforeGroup);

            for (int i = 0; i < group.count; i++)
            {
                SpawnEnemy(group.enemyType);
                yield return new WaitForSeconds(group.spawnInterval);
            }
        }

        currentWaveIndex++;
    }

    private void SpawnEnemy(EnemyConfig config)
    {
        // Instantiate prefab and configure with data from ScriptableObject
        var enemy = Instantiate(config.prefab, GetSpawnPoint(), Quaternion.identity);
        enemy.GetComponent<EnemyController>().Initialize(config);
    }
}
```

The designer can create dozens of `WaveDefinition` assets, configure different enemy compositions, and test variations without touching any code.

---

### 3.2 Event Channels (Decoupled Communication)

Event Channels are ScriptableObjects that function as **event buses**: one system fires the event, other systems listen — without any of them needing to know about each other.

**Base EventChannelSO (no parameter):**

```csharp
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/Void Event Channel")]
public class VoidEventChannelSO : ScriptableObject
{
    // Delegate that subscribers use to listen
    public event Action OnEventRaised;

    // Public method that any system can call to fire the event
    public void RaiseEvent()
    {
        if (OnEventRaised != null)
        {
            OnEventRaised.Invoke();
        }
        else
        {
            Debug.LogWarning($"Event Channel '{name}' fired but has no listeners.");
        }
    }
}
```

**Generic Event Channel with parameter:**

```csharp
public abstract class EventChannelSO<T> : ScriptableObject
{
    public event Action<T> OnEventRaised;

    public void RaiseEvent(T value)
    {
        OnEventRaised?.Invoke(value);
    }
}

[CreateAssetMenu(menuName = "Events/Int Event Channel")]
public class IntEventChannelSO : EventChannelSO<int> { }

[CreateAssetMenu(menuName = "Events/Float Event Channel")]
public class FloatEventChannelSO : EventChannelSO<float> { }

[CreateAssetMenu(menuName = "Events/String Event Channel")]
public class StringEventChannelSO : EventChannelSO<string> { }
```

**Generic listener for connecting via Inspector:**

```csharp
using UnityEngine;
using UnityEngine.Events;

public class VoidEventListener : MonoBehaviour
{
    [Header("Listens to this channel")]
    [SerializeField] private VoidEventChannelSO eventChannel;

    [Header("Responds with")]
    [SerializeField] private UnityEvent onEventRaised;

    private void OnEnable()
    {
        if (eventChannel != null)
            eventChannel.OnEventRaised += Respond;
    }

    private void OnDisable()
    {
        if (eventChannel != null)
            eventChannel.OnEventRaised -= Respond;
    }

    private void Respond()
    {
        onEventRaised?.Invoke();
    }
}
```

**Practical example — Player dies and multiple systems react:**

```
Asset: OnPlayerDeath (VoidEventChannelSO)

PlayerHealth.cs       →  eventChannel.RaiseEvent()   [emitter]
UIGameOver.cs         →  VoidEventListener → ShowGameOverScreen()
MusicManager.cs       →  VoidEventListener → PlayDeathMusic()
EnemyAIManager.cs     →  VoidEventListener → CelebrateAllEnemies()
AnalyticsTracker.cs   →  VoidEventListener → TrackDeathEvent()
```

None of these systems directly references any other. They all point to the same **asset** `OnPlayerDeath`. This is radically more decoupled than singletons or direct references.

---

### 3.3 Runtime Sets (Tracking without Singletons)

Runtime Sets solve the classic problem of "how do I find all objects of a type in the scene?" without using `FindObjectsOfType` (expensive) or singletons (coupled).

Each object **registers** itself when enabled and **unregisters** when disabled, maintaining a live list in the ScriptableObject.

```csharp
using System.Collections.Generic;
using System;
using UnityEngine;

public abstract class RuntimeSetSO<T> : ScriptableObject
{
    private readonly List<T> items = new List<T>();

    public IReadOnlyList<T> Items => items;
    public int Count => items.Count;

    // Notifies when the list changes
    public event Action OnChanged;

    public void Add(T item)
    {
        if (!items.Contains(item))
        {
            items.Add(item);
            OnChanged?.Invoke();
        }
    }

    public void Remove(T item)
    {
        if (items.Remove(item))
        {
            OnChanged?.Invoke();
        }
    }

    // Clears on exiting Play Mode (prevents stale data)
    private void OnDisable()
    {
        items.Clear();
    }
}
```

**Concrete implementation and automatic registration:**

```csharp
[CreateAssetMenu(menuName = "Runtime Sets/Enemy Runtime Set")]
public class EnemyRuntimeSetSO : RuntimeSetSO<EnemyController> { }

// Component that auto-registers
public class EnemyController : MonoBehaviour
{
    [SerializeField] private EnemyRuntimeSetSO runtimeSet;

    private void OnEnable() => runtimeSet.Add(this);
    private void OnDisable() => runtimeSet.Remove(this);
}
```

**Usage — radar system that shows all enemies:**

```csharp
public class RadarUI : MonoBehaviour
{
    [SerializeField] private EnemyRuntimeSetSO activeEnemies;

    void Update()
    {
        // Iterates over active enemies without a singleton or Find
        foreach (var enemy in activeEnemies.Items)
        {
            DrawBlipOnRadar(enemy.transform.position);
        }
    }

    private void DrawBlipOnRadar(Vector3 worldPos) { /* ... */ }
}
```

Benefits over `FindObjectsOfType`: O(1) cost per registration/unregistration vs. O(n) for a full search every frame. Benefits over singletons: no strong dependency is created, multiple sets can coexist, and they are independently testable.

---

### 3.4 Delegate Objects (Swappable Logic / Strategy Pattern)

Delegate Objects encapsulate **logic** inside ScriptableObjects, allowing behaviors to be swapped at runtime. This is the application of the **Strategy Pattern** using Unity assets.

```csharp
// Abstract base class — defines the "contract" for a movement behavior
public abstract class MovementStrategySO : ScriptableObject
{
    public abstract Vector3 CalculateMovement(Transform entity, Transform target, float speed);
}
```

**Concrete implementations:**

```csharp
[CreateAssetMenu(menuName = "AI/Movement/Chase Direct")]
public class ChaseDirectSO : MovementStrategySO
{
    public override Vector3 CalculateMovement(Transform entity, Transform target, float speed)
    {
        Vector3 direction = (target.position - entity.position).normalized;
        return direction * speed * Time.deltaTime;
    }
}

[CreateAssetMenu(menuName = "AI/Movement/Patrol Waypoints")]
public class PatrolWaypointsSO : MovementStrategySO
{
    public Transform[] waypoints;
    private int currentIndex;

    public override Vector3 CalculateMovement(Transform entity, Transform target, float speed)
    {
        if (waypoints == null || waypoints.Length == 0) return Vector3.zero;

        Vector3 destination = waypoints[currentIndex].position;
        Vector3 direction = (destination - entity.position).normalized;

        if (Vector3.Distance(entity.position, destination) < 0.5f)
            currentIndex = (currentIndex + 1) % waypoints.Length;

        return direction * speed * Time.deltaTime;
    }
}

[CreateAssetMenu(menuName = "AI/Movement/Flee")]
public class FleeSO : MovementStrategySO
{
    public override Vector3 CalculateMovement(Transform entity, Transform target, float speed)
    {
        Vector3 direction = (entity.position - target.position).normalized;
        return direction * speed * Time.deltaTime;
    }
}
```

**Consumer — swapping strategy at runtime:**

```csharp
public class EnemyMovement : MonoBehaviour
{
    [SerializeField] private MovementStrategySO defaultStrategy;
    [SerializeField] private MovementStrategySO fleeStrategy;
    [SerializeField] private float healthFleeThreshold = 0.25f;

    private MovementStrategySO currentStrategy;
    private Transform target;

    void Start()
    {
        currentStrategy = defaultStrategy;
        target = GameObject.FindWithTag("Player").transform;
    }

    void Update()
    {
        Vector3 movement = currentStrategy.CalculateMovement(transform, target, 5f);
        transform.position += movement;
    }

    // Called when the enemy takes damage
    public void OnHealthChanged(float healthPercent)
    {
        currentStrategy = healthPercent <= healthFleeThreshold
            ? fleeStrategy
            : defaultStrategy;
    }
}
```

The designer can create as many strategies as desired, drag different combinations onto different enemy types, and even swap them at runtime via events — all without recompiling.

---

### 3.5 Enum Replacement (ScriptableObject as Typed Enum)

Traditional C# enums are brittle: adding values can break serialization, reordering changes the underlying integers, and they cannot carry extra data. ScriptableObjects as enums solve all of these problems.

```csharp
// "Enum" base — each instance is an asset in the project
[CreateAssetMenu(menuName = "Game Data/Team")]
public class TeamSO : ScriptableObject
{
    [Header("Visual Identity")]
    public Color teamColor = Color.white;
    public Sprite teamBadge;

    [Header("Gameplay")]
    public int startingScore = 0;

    // Comparison: works because ScriptableObjects are references
    // team1 == team2 compares the reference to the asset, not values
}
```

**Usage — replacing enum Team { Red, Blue, Green }:**

```
Assets/
  Data/
    Teams/
      Team_Red.asset      (teamColor = red)
      Team_Blue.asset     (teamColor = blue)
      Team_Green.asset    (teamColor = green)
```

```csharp
public class Player : MonoBehaviour
{
    [SerializeField] private TeamSO team;  // Drag the asset in the Inspector

    public bool IsSameTeam(Player other)
    {
        // Reference comparison — fast and safe
        return team == other.team;
    }

    public Color GetTeamColor() => team.teamColor;
}
```

**Advantages over traditional enums:**

- **Extensible without breaking**: adding a new team means creating an asset, not changing code.
- **Carries data**: color, icon, settings — all together in the asset.
- **Stable serialization**: does not depend on numeric order like `enum { Red = 0, Blue = 1 }`.
- **Reference comparison**: `==` checks whether it is the same asset, works like an enum.
- **Designer-friendly**: no code required to create new "values".

The Unity PaddleGameSO demonstrates this pattern in the game's team system.

---

### 3.6 Flyweight Pattern (Shared Data Across Instances)

The Flyweight Pattern uses ScriptableObjects to **share immutable data** across thousands of instances, drastically reducing memory consumption.

```csharp
// Shared data (intrinsic state) — one asset per type
[CreateAssetMenu(menuName = "Game Data/Unit Type")]
public class UnitTypeSO : ScriptableObject
{
    [Header("Shared Data — same for all instances")]
    public string unitName;
    public Sprite icon;
    public GameObject prefab;
    public int maxHealth;
    public float moveSpeed;
    public float attackDamage;
    public float attackRange;
    public AudioClip[] attackSounds;
    public Material unitMaterial;
}
```

```csharp
// Individual instance (extrinsic state) — unique per GameObject
public class UnitInstance : MonoBehaviour
{
    [Header("Type (shared data via Flyweight)")]
    [SerializeField] private UnitTypeSO unitType;

    // Individual state — only this data is unique per instance
    private int currentHealth;
    private Vector3 currentPosition;
    private UnitState currentState;

    public UnitTypeSO Type => unitType;

    void Start()
    {
        currentHealth = unitType.maxHealth;
        GetComponent<Renderer>().material = unitType.unitMaterial;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0) Die();
    }
}
```

**Memory impact** (example with 10,000 units of 5 types):

| Approach | Data memory |
|---|---|
| Without Flyweight (duplicated data) | 10,000 × sizeof(UnitData) |
| With Flyweight (ScriptableObject) | 5 × sizeof(UnitTypeSO) + 10,000 × sizeof(individual state) |

With strings, sprites, and AudioClips, the savings can be **orders of magnitude**.

---

## 4. ScriptableObjects + Addressables

The Addressables system allows loading assets on demand, including ScriptableObjects. This is essential for games with a lot of content (hundreds of item types, enemies, etc.) where loading everything at startup would be prohibitive.

**Asynchronous reference to a ScriptableObject:**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[System.Serializable]
public class EnemyConfigReference : AssetReferenceT<EnemyConfig>
{
    public EnemyConfigReference(string guid) : base(guid) { }
}

public class AsyncEnemySpawner : MonoBehaviour
{
    [SerializeField] private AssetReferenceT<EnemyConfig> enemyConfigRef;

    private AsyncOperationHandle<EnemyConfig> handle;

    public async void SpawnEnemyAsync()
    {
        // Loads the ScriptableObject on demand
        handle = Addressables.LoadAssetAsync<EnemyConfig>(enemyConfigRef);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            EnemyConfig config = handle.Result;
            Debug.Log($"Loaded enemy: {config.displayName}");

            // Use config data to spawn
            var enemy = Instantiate(config.prefab);
            enemy.GetComponent<EnemyController>().Initialize(config);
        }
    }

    private void OnDestroy()
    {
        // CRITICAL: always release the handle
        if (handle.IsValid())
            Addressables.Release(handle);
    }
}
```

**Registry Pattern — catalogue of all assets of a type:**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[CreateAssetMenu(menuName = "Registries/Item Registry")]
public class ItemRegistrySO : ScriptableObject
{
    [SerializeField] private AssetLabelReference itemLabel;

    private Dictionary<string, ItemDataSO> loadedItems = new();
    private AsyncOperationHandle<IList<ItemDataSO>> loadHandle;

    public async Awaitable LoadAllItemsAsync()
    {
        loadHandle = Addressables.LoadAssetsAsync<ItemDataSO>(
            itemLabel,
            item => loadedItems[item.itemId] = item
        );
        await loadHandle.Task;
        Debug.Log($"Loaded {loadedItems.Count} items from Addressables.");
    }

    public ItemDataSO GetItem(string itemId)
    {
        loadedItems.TryGetValue(itemId, out var item);
        return item;
    }

    public void ReleaseAll()
    {
        if (loadHandle.IsValid())
            Addressables.Release(loadHandle);
        loadedItems.Clear();
    }
}
```

**Important considerations with Addressables + ScriptableObjects:**

- **Editor vs Build**: in the Editor, ScriptableObjects referenced via Addressables use the same asset instance. In builds, Addressables loads a **new copy** from the AssetBundle. This can cause bugs where `==` comparisons work in the Editor but fail in builds.
- **Initial scene**: the best practice is to have exactly one "built-in" scene (not Addressable) that serves only as a bootstrap to load everything else via Addressables.
- **Handle management**: always call `Addressables.Release()` when the ScriptableObject is no longer needed, to avoid memory leaks.
- **Loading by GUID**: it is possible to load by GUID instead of address, which makes the system resilient to folder reorganizations.

---

## 5. Pitfalls and Cautions

### 5.1 Play Mode Persistence (Editor Only)

This is the most common pitfall. In the Unity Editor, changes made to ScriptableObjects during Play Mode **persist** after exiting Play Mode. In builds, these changes **are discarded** when the application closes.

```csharp
// DANGER: this code permanently modifies the asset in the Editor
[CreateAssetMenu(menuName = "Game Data/Player Stats")]
public class PlayerStatsSO : ScriptableObject
{
    public int currentHealth = 100;  // If modified in Play Mode, the .asset changes on disk!

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;  // Modifies the asset in the Editor!
    }
}
```

**Solution — use a runtime instance:**

```csharp
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO baseStats;  // Original asset (read-only)

    private int runtimeHealth;  // Local runtime copy

    void Start()
    {
        runtimeHealth = baseStats.currentHealth;  // Copy from the asset
    }

    public void TakeDamage(int damage)
    {
        runtimeHealth -= damage;  // Modifies only the local copy
    }
}
```

**Alternative solution — clone via Instantiate:**

```csharp
public class RuntimeDataManager : MonoBehaviour
{
    [SerializeField] private PlayerStatsSO baseStats;
    private PlayerStatsSO runtimeStats;

    void Awake()
    {
        // Creates an in-memory copy — does not affect the original asset
        runtimeStats = Instantiate(baseStats);
    }

    void OnDestroy()
    {
        // Clean up the copy
        if (runtimeStats != null)
            Destroy(runtimeStats);
    }
}
```

### 5.2 References to Scene Objects

ScriptableObjects **cannot serialize references to objects that live in scenes**. An asset exists in the project; a GameObject exists in a scene. Unity cannot maintain that cross-reference in a stable way.

```csharp
// DO NOT DO THIS
[CreateAssetMenu]
public class BadExample : ScriptableObject
{
    public Transform playerTransform;  // Scene reference — will be null at runtime!
    public Camera mainCamera;          // Same problem
}
```

**Solution — use Runtime Sets or Events to connect scene objects to assets:**

```csharp
// The scene object registers itself in the ScriptableObject at runtime
public class PlayerRegistration : MonoBehaviour
{
    [SerializeField] private TransformRuntimeSetSO playerSet;

    void OnEnable() => playerSet.Add(transform);
    void OnDisable() => playerSet.Remove(transform);
}
```

### 5.3 Version Control and Merge Conflicts

ScriptableObjects are serialized as YAML on disk. When two team members edit the same `.asset` file, Git's automatic merge frequently fails.

**Best practices:**

- **Granularity**: prefer many small assets over few large ones. One `EnemyConfig` per enemy type, not a monolithic `AllEnemiesConfig`.
- **Ownership**: establish conventions about who edits which assets to minimize conflicts.
- **Smart Merge**: use `UnityYAMLMerge` (bundled with Unity) as the merge tool in Git to resolve conflicts more intelligently.
- **GUID prefixes**: do not rename assets without a reason — Unity tracks by GUID in the `.meta` file, but renaming causes unnecessary diffs.
- **ScriptableObject IDs**: consider adding a unique `string id` field per asset to make it easier to reference from external data (JSON, databases).

### 5.4 OnEnable/OnDisable — Execution Order

ScriptableObjects receive `OnEnable()` before any `MonoBehaviour.Awake()`. This can cause surprises if the ScriptableObject tries to access systems that have not yet been initialized.

```csharp
[CreateAssetMenu]
public class GameSettingsSO : ScriptableObject
{
    private void OnEnable()
    {
        // CAUTION: this runs BEFORE any MonoBehaviour Awake()
        // Do not access singletons or managers here
        Debug.Log("GameSettings OnEnable — runs very early!");
    }
}
```

### 5.5 Null in Build but Works in Editor

In the Editor, ScriptableObjects instantiated via `CreateInstance<T>()` without being saved as an asset may appear to work, but in builds they will be collected by the GC. Always save as an asset or maintain a strong reference.

---

## 6. Designer-Friendly Workflows

ScriptableObjects are Unity's most powerful mechanism for creating workflows where **designers configure gameplay without coding**.

**Why they are designer-friendly:**

1. **Native Inspector**: all serialized fields appear automatically in the Inspector with readable names, sliders, dropdowns, etc.
2. **CreateAssetMenu**: creating a new item/enemy/skill is a right-click in the folder.
3. **No recompilation**: changing values in a ScriptableObject does not recompile the project.
4. **Drag-and-drop**: connecting systems is done by dragging an asset to a field in the Inspector.
5. **Presets and variants**: duplicating an asset and adjusting values creates an instant variant.

**Custom Editors to improve things further:**

```csharp
#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(EnemyConfig))]
public class EnemyConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var config = (EnemyConfig)target;

        // Visual preview of the enemy
        if (config.portrait != null)
        {
            GUILayout.Label(config.portrait.texture, GUILayout.Height(128), GUILayout.Width(128));
        }

        // Stats with visual bars
        EditorGUILayout.LabelField("Stats Preview", EditorStyles.boldLabel);
        DrawStatBar("Health", config.maxHealth, 500);
        DrawStatBar("Damage", config.attackDamage, 100);
        DrawStatBar("Speed", config.moveSpeed, 20);

        EditorGUILayout.Space();
        DrawDefaultInspector();
    }

    private void DrawStatBar(string label, float value, float max)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(60));
        var rect = GUILayoutUtility.GetRect(200, 18);
        EditorGUI.ProgressBar(rect, value / max, $"{value}/{max}");
        EditorGUILayout.EndHorizontal();
    }
}
#endif
```

**Typical designer + ScriptableObjects workflow:**

```
Designer wants to create a new enemy:
1. Right-click in Assets/Data/Enemies/
2. Create > Game Data > Enemy Config
3. Fill in name, stats, sprite in the Inspector
4. Drag the asset into the desired WaveDefinition
5. Test — no waiting for compilation, no opening an IDE
```

---

## 7. Ryan Hipple — Game Architecture with ScriptableObjects

**Ryan Hipple**, principal engineer at **Schell Games**, delivered the seminal talk "Game Architecture with ScriptableObjects" at **Unite Austin 2017**. This talk transformed how the Unity community thinks about game architecture.

### Core Concepts from the Talk

**Problem identified**: most Unity projects suffer from **excessive coupling** caused by singletons, direct references between MonoBehaviours, and monolithic logic. This makes the code brittle, hard to test, and hostile to designers.

**Proposed solution**: use ScriptableObjects as **modular "glue"** between systems, creating an architecture where:

- Systems do not directly know each other
- Data flows through shared assets
- Designers can reconfigure the game without coding
- Each piece is independently testable

### Patterns Introduced by Ryan Hipple

1. **ScriptableObject Variables (Shared Variables)**: variables like `FloatVariable` that live as assets and are referenced by multiple systems. A `PlayerHealth` FloatVariable can be read by the UI, the sound system, and the AI without any of them knowing about each other.

```csharp
// Ryan Hipple's original concept
[CreateAssetMenu(menuName = "Variables/Float Variable")]
public class FloatVariable : ScriptableObject
{
    public float value;

    public void SetValue(float newValue) => value = newValue;
    public void ApplyChange(float delta) => value += delta;
}
```

2. **Game Events**: ScriptableObjects as event channels — a direct precursor to the Event Channels described in section 3.2.

```csharp
// Ryan Hipple's original concept
[CreateAssetMenu(menuName = "Events/Game Event")]
public class GameEvent : ScriptableObject
{
    private List<GameEventListener> listeners = new List<GameEventListener>();

    public void Raise()
    {
        // Iterates backwards for safety
        for (int i = listeners.Count - 1; i >= 0; i--)
            listeners[i].OnEventRaised();
    }

    public void RegisterListener(GameEventListener listener) => listeners.Add(listener);
    public void UnregisterListener(GameEventListener listener) => listeners.Remove(listener);
}
```

3. **Runtime Sets**: dynamic lists of active objects, maintained by ScriptableObjects, eliminating the need for singletons for global tracking.

4. **Editability principle**: every "knob" in the game should be accessible from the Inspector. If the designer cannot change it, the code is poorly architected.

### Community Impact

The talk generated the open-source repository **Unite2017** (1.3k+ stars on GitHub), inspired the **Unity Atoms** library, and directly influenced the official Unity documentation on ScriptableObject Architecture. The patterns proposed by Hipple are now considered **industry best practices** for medium and large Unity projects.

---

## 8. PaddleGameSO — Unity Demo Project

**PaddleGameSO** is the official Unity Technologies demo project that accompanies the e-book *"Create modular game architecture in Unity with ScriptableObjects"*. It demonstrates all the core patterns in a functional context of paddle and ball mini-games.

### Project Structure

The project contains two modes of exploration:

1. **Patterns Demo**: isolated examples of each pattern, ideal for study
2. **Mini Games**: three variants of paddle games that integrate the patterns:
   - **Classic** — basic implementation with ScriptableObjects
   - **Hockey** — wall positions via serialized text
   - **Foosball** — walls defined via Prefab definitions

The entry point is the **Bootloader_Scene** scene.

### Demonstrated Patterns

| Pattern | Usage in PaddleGameSO |
|---|---|
| **Data Containers** | Paddle stats, ball speed, level configurations |
| **Event Channels** | Communication between paddle, ball, scoring, UI — all via SO events |
| **Delegate Objects** | Objective system (win/lose conditions) uses Strategy Pattern with SOs |
| **ScriptableObject Enums** | Team system (Team A vs Team B as assets, not enums) |
| **Runtime Sets** | Isolated demo of spawning/destroying blocks with tracking via RuntimeSet |

The project demonstrates how **all these patterns integrate**: events fire when the ball scores a point, scoring reads from shared variables, the UI reacts via listeners, and win conditions are swappable delegate objects.

---

## 9. Pattern Connection Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                    SCRIPTABLEOBJECT ARCHITECTURE                    │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────┐     ┌──────────────────┐                      │
│  │  DATA CONTAINER  │     │  FLYWEIGHT        │                     │
│  │  (EnemyConfig)   │────▶│  (UnitTypeSO)     │                     │
│  │                  │     │  Shared by 1000s   │                     │
│  │  Stats, configs  │     │  of instances      │                     │
│  └────────┬─────────┘     └──────────────────┘                      │
│           │                                                         │
│           │ references                                               │
│           ▼                                                         │
│  ┌─────────────────┐         ┌──────────────────┐                   │
│  │  ENUM REPLACE    │         │  DELEGATE OBJECT  │                  │
│  │  (TeamSO)        │         │  (MovementSO)     │                  │
│  │                  │         │                   │                  │
│  │  Reference-based │         │  Strategy pattern │                  │
│  │  comparison      │◀───────▶│  hot-swappable    │                  │
│  └─────────────────┘         └────────┬──────────┘                   │
│                                       │                              │
│           ┌───────────────────────────┘                              │
│           │ strategy swap                                            │
│           │ triggered by                                             │
│           ▼                                                         │
│  ┌─────────────────┐                                                │
│  │  EVENT CHANNEL   │◀─────── Any system can call Raise()          │
│  │  (VoidEventSO)   │                                               │
│  │                  │────────▶ Any listener reacts                   │
│  │  Full            │                                               │
│  │  decoupling      │         ┌──────────────────┐                  │
│  └────────┬─────────┘         │  RUNTIME SET      │                 │
│           │                   │  (EnemySetSO)     │                  │
│           │ notifies          │                   │                  │
│           │ changes via       │  OnEnable → Add   │                  │
│           └──────────────────▶│  OnDisable → Rem  │                  │
│                               │  Global tracking  │                  │
│                               │  without singleton│                  │
│                               └──────────────────┘                   │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                      ADDRESSABLES                            │   │
│  │  Any SO above can be loaded async via Addressables           │   │
│  │  AssetReference<T> → LoadAssetAsync → use → Release          │   │
│  └─────────────────────────────────────────────────────────────┘    │
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐    │
│  │                  DESIGNER WORKFLOW                            │   │
│  │  Inspector → CreateAssetMenu → Drag & Drop → Play & Test    │   │
│  │  All patterns above are configurable without code            │   │
│  └─────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘

TYPICAL DATA FLOW:
═══════════════════════

  Designer creates     Enemy spawns          Enemy registers        UI reads from
  EnemyConfig.asset → and reads config (Flyweight) → into RuntimeSet ──▶ RuntimeSet
       │                                             │
       │                                             │ dies
       ▼                                             ▼
  WaveDefinition     Fires EventChannel ──▶ Score updates
  references configs  "OnEnemyDeath"          (FloatVariable)
                                                     │
                                              UI observes via ◀── Event Channel
                                              listener           "OnScoreChanged"
```

---

## 10. Sources and References

### Talks and Presentations

- [Game Architecture with Scriptable Objects — Ryan Hipple, Schell Games (Blog Post)](https://schellgames.com/blog/game-architecture-with-scriptable-objects)
- [Game Architecture with Scriptable Objects — Slides (SlideShare)](https://www.slideshare.net/slideshow/game-architecture-with-scriptable-objects/80585032)
- [Ryan Hipple — Unite 2017 Talk (Personal blog)](http://www.roboryantron.com/2017/10/unite-2017-game-architecture-with.html)

### Repositories

- [Unite2017 — Sample Project by Ryan Hipple (GitHub)](https://github.com/roboryantron/Unite2017)
- [PaddleGameSO — Unity Technologies Demo Project (GitHub)](https://github.com/UnityTechnologies/PaddleGameSO)
- [Unity Atoms — Library inspired by Hipple's patterns](https://unity-atoms.github.io/unity-atoms/introduction/philosophy)
- [Addressables + ScriptableObjects Test (GitHub)](https://github.com/njelly/addressables-scriptableobjects-test)

### Official Unity Documentation

- [Architect your code with ScriptableObjects](https://unity.com/how-to/architect-game-code-scriptable-objects)
- [Use ScriptableObjects as Event Channels](https://unity.com/how-to/scriptableobjects-event-channels-game-code)
- [Using ScriptableObject-based Runtime Sets](https://unity.com/how-to/scriptableobject-based-runtime-set)
- [Use ScriptableObjects as Delegate Objects](https://unity.com/how-to/scriptableobjects-delegate-objects)
- [Use ScriptableObject-based Enums](https://unity.com/how-to/scriptableobject-based-enums)
- [Separate Game Data and Logic with ScriptableObjects](https://unity.com/how-to/separate-game-data-logic-scriptable-objects)
- [Get Started with the ScriptableObjects Demo](https://unity.com/how-to/get-started-with-scriptableobjects-demo)
- [6 Ways ScriptableObjects Can Benefit Your Team (Unity Blog)](https://blog.unity.com/engine-platform/6-ways-scriptableobjects-can-benefit-your-team-and-your-code)

### E-book

- *Create modular game architecture in Unity with ScriptableObjects* — Unity Technologies (free PDF, accompanies PaddleGameSO)

### Supplementary Articles

- [The Scriptable Object Asset Registry Pattern — Bronson Zgeb](https://bronsonzgeb.com/index.php/2021/09/11/the-scriptable-object-asset-registry-pattern/)
- [Flyweight Pattern in Unity — Marcus Ansley (Medium)](https://m-ansley.medium.com/flyweight-in-unity-design-pattern-9b1dd35dfad5)
- [Game Programming Patterns — Flyweight (Habrador)](https://www.habrador.com/tutorials/programming-patterns/2-flyweight-pattern/)
- [Where ScriptableObjects Live — Eyas's Blog](https://blog.eyas.sh/2020/09/where-scriptableobjects-live/)
- [Unity Serialization Part 3: ScriptableObjects](https://blog.lslabs.dev/posts/unity_serialization_3)
