# Design Patterns for Game Development in Unity with C#

> **Reference guide** — Each pattern includes: the problem it solves, C# implementation, and when to use/not use it.
> Examples contextualized in real gameplay, not academic abstractions.

---

## Table of Contents

1. [Observer Pattern](#1-observer-pattern)
2. [Command Pattern](#2-command-pattern)
3. [State Machine](#3-state-machine)
4. [Object Pool](#4-object-pool)
5. [Singleton](#5-singleton)
6. [Strategy Pattern](#6-strategy-pattern)
7. [Factory Pattern](#7-factory-pattern)
8. [Decorator Pattern](#8-decorator-pattern)
9. [Component Pattern](#9-component-pattern)
10. [SOLID Principles Applied to Unity](#10-solid-principles-applied-to-unity)
11. [Common Anti-Patterns](#11-common-anti-patterns)
12. [Sources and References](#12-sources-and-references)

---

## 1. Observer Pattern

### Problem it solves

When an event occurs (player dies, enemy takes damage, score changes), multiple systems need to react — UI, audio, achievements, analytics. Without Observer, you end up with direct coupling: `PlayerHealth` needs to know about `UIManager`, `AudioManager`, `AchievementTracker`... and every time you add a new system, you have to modify existing code.

### 1.1 — UnityEvents (Inspector-friendly)

```csharp
using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    [Header("Events")]
    public UnityEvent<int, int> OnHealthChanged;  // current, max
    public UnityEvent OnPlayerDied;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
            OnPlayerDied?.Invoke();
    }
}
```

**Usage:** Drag components in the Inspector to connect listeners. Ideal for designers who do not touch code.

### 1.2 — C# Actions/Events (Code-driven)

```csharp
using System;
using UnityEngine;

public class GameEvents : MonoBehaviour
{
    // Simple singleton for global events
    public static GameEvents Instance { get; private set; }

    // Typed events
    public event Action<int> OnScoreChanged;
    public event Action<Vector3> OnEnemyKilled;
    public event Action OnLevelCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ScoreChanged(int newScore) => OnScoreChanged?.Invoke(newScore);
    public void EnemyKilled(Vector3 position) => OnEnemyKilled?.Invoke(position);
    public void LevelCompleted() => OnLevelCompleted?.Invoke();
}

// --- Example listener: score UI ---
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;

    private void OnEnable()
    {
        GameEvents.Instance.OnScoreChanged += UpdateScore;
    }

    private void OnDisable()
    {
        GameEvents.Instance.OnScoreChanged -= UpdateScore;
    }

    private void UpdateScore(int score)
    {
        scoreText.text = $"Score: {score}";
    }
}
```

### 1.3 — Event Queue (for high volume)

```csharp
using System.Collections.Generic;
using UnityEngine;

public struct GameEvent
{
    public enum Type { Damage, Heal, Spawn, Death, PickupCollected }
    public Type EventType;
    public GameObject Source;
    public GameObject Target;
    public float Value;
}

public class EventQueue : MonoBehaviour
{
    public static EventQueue Instance { get; private set; }

    private Queue<GameEvent> eventQueue = new Queue<GameEvent>();
    private const int MAX_EVENTS_PER_FRAME = 20;

    private Dictionary<GameEvent.Type, List<System.Action<GameEvent>>> listeners
        = new Dictionary<GameEvent.Type, List<System.Action<GameEvent>>>();

    private void Awake()
    {
        Instance = this;
    }

    public void Subscribe(GameEvent.Type type, System.Action<GameEvent> callback)
    {
        if (!listeners.ContainsKey(type))
            listeners[type] = new List<System.Action<GameEvent>>();
        listeners[type].Add(callback);
    }

    public void Unsubscribe(GameEvent.Type type, System.Action<GameEvent> callback)
    {
        if (listeners.ContainsKey(type))
            listeners[type].Remove(callback);
    }

    public void Enqueue(GameEvent evt)
    {
        eventQueue.Enqueue(evt);
    }

    private void Update()
    {
        int processed = 0;
        while (eventQueue.Count > 0 && processed < MAX_EVENTS_PER_FRAME)
        {
            var evt = eventQueue.Dequeue();
            if (listeners.ContainsKey(evt.EventType))
            {
                foreach (var listener in listeners[evt.EventType])
                    listener.Invoke(evt);
            }
            processed++;
        }
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| UI reacting to game state changes | Simple 1:1 communication between two components on the same GameObject |
| Decoupled systems (audio, VFX, analytics) | When execution order is critical (events do not guarantee order) |
| Event queues for bullet hell / many projectiles | For logic that needs a return value (events are fire-and-forget) |

**Classic pitfall:** Forgetting to unsubscribe in `OnDisable`/`OnDestroy` → memory leaks and null references on destroyed objects.

---

## 2. Command Pattern

### Problem it solves

You need to represent actions as objects — in order to queue inputs, implement undo/redo, or record replays. Without Command, input is hardcoded in `Update()` and it is impossible to undo or reproduce.

### 2.1 — Base interface and input queuing

```csharp
public interface ICommand
{
    void Execute();
    void Undo();
}

// --- Movement command on a grid (tactical/puzzle) ---
public class MoveCommand : ICommand
{
    private Transform unit;
    private Vector3 previousPosition;
    private Vector3 targetPosition;

    public MoveCommand(Transform unit, Vector3 target)
    {
        this.unit = unit;
        this.targetPosition = target;
        this.previousPosition = unit.position;
    }

    public void Execute()
    {
        previousPosition = unit.position;
        unit.position = targetPosition;
    }

    public void Undo()
    {
        unit.position = previousPosition;
    }
}

// --- Attack command ---
public class AttackCommand : ICommand
{
    private IDamageable target;
    private int damage;
    private int previousHealth;

    public AttackCommand(IDamageable target, int damage)
    {
        this.target = target;
        this.damage = damage;
    }

    public void Execute()
    {
        previousHealth = target.CurrentHealth;
        target.TakeDamage(damage);
    }

    public void Undo()
    {
        target.Heal(previousHealth - target.CurrentHealth);
    }
}
```

### 2.2 — Command Manager with undo/redo

```csharp
using System.Collections.Generic;
using UnityEngine;

public class CommandManager : MonoBehaviour
{
    private Stack<ICommand> undoStack = new Stack<ICommand>();
    private Stack<ICommand> redoStack = new Stack<ICommand>();

    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        undoStack.Push(command);
        redoStack.Clear(); // New command invalidates redo
    }

    public void Undo()
    {
        if (undoStack.Count == 0) return;
        var command = undoStack.Pop();
        command.Undo();
        redoStack.Push(command);
    }

    public void Redo()
    {
        if (redoStack.Count == 0) return;
        var command = redoStack.Pop();
        command.Execute();
        undoStack.Push(command);
    }
}
```

### 2.3 — Replay system

```csharp
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TimestampedCommand
{
    public float Timestamp;
    public ICommand Command;
}

public class ReplaySystem : MonoBehaviour
{
    private List<TimestampedCommand> recording = new List<TimestampedCommand>();
    private bool isRecording;
    private bool isReplaying;
    private float replayTimer;
    private int replayIndex;

    public void StartRecording()
    {
        recording.Clear();
        isRecording = true;
    }

    public void RecordCommand(ICommand command)
    {
        if (!isRecording) return;
        recording.Add(new TimestampedCommand
        {
            Timestamp = Time.time,
            Command = command
        });
    }

    public void PlayReplay()
    {
        isReplaying = true;
        replayIndex = 0;
        replayTimer = 0f;
    }

    private void Update()
    {
        if (!isReplaying || replayIndex >= recording.Count) return;

        replayTimer += Time.deltaTime;
        float baseTime = recording[0].Timestamp;

        while (replayIndex < recording.Count
            && (recording[replayIndex].Timestamp - baseTime) <= replayTimer)
        {
            recording[replayIndex].Command.Execute();
            replayIndex++;
        }

        if (replayIndex >= recording.Count)
            isReplaying = false;
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Turn-based games (undo moves) | Simple input in action games (unnecessary overhead) |
| Level editors (undo/redo object placement) | When actions are irreversible by design |
| Replays and ghost runs | For a few simple commands — YAGNI |
| Input buffering in fighting games | |

---

## 3. State Machine

### Problem it solves

Characters, enemies, and UI systems have behaviors that change based on state (idle, running, attacking, stunned). Without a state machine, you end up with giant if/else chains in `Update()` that are impossible to maintain and debug.

### 3.1 — Simple FSM (enum-based)

```csharp
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Attack, Dead }

    [SerializeField] private State currentState = State.Patrol;
    [SerializeField] private float chaseRange = 10f;
    [SerializeField] private float attackRange = 2f;

    private Transform player;

    private void Update()
    {
        switch (currentState)
        {
            case State.Patrol:  UpdatePatrol();  break;
            case State.Chase:   UpdateChase();   break;
            case State.Attack:  UpdateAttack();  break;
            case State.Dead:    break; // Nothing
        }
    }

    private void UpdatePatrol()
    {
        // ... patrol logic ...
        if (DistanceToPlayer() < chaseRange)
            TransitionTo(State.Chase);
    }

    private void UpdateChase()
    {
        // ... chase player ...
        if (DistanceToPlayer() < attackRange)
            TransitionTo(State.Attack);
        else if (DistanceToPlayer() > chaseRange * 1.5f)
            TransitionTo(State.Patrol);
    }

    private void UpdateAttack()
    {
        // ... attack ...
        if (DistanceToPlayer() > attackRange * 1.2f)
            TransitionTo(State.Chase);
    }

    private void TransitionTo(State newState)
    {
        // Exit logic
        switch (currentState)
        {
            case State.Chase: /* stop run animation */ break;
            case State.Attack: /* cancel ongoing attack */ break;
        }

        currentState = newState;

        // Enter logic
        switch (newState)
        {
            case State.Patrol: /* reset route */ break;
            case State.Chase: /* trigger alert VFX */ break;
            case State.Attack: /* start animation */ break;
        }
    }

    private float DistanceToPlayer() =>
        Vector3.Distance(transform.position, player.position);
}
```

**Good for:** Simple enemies with few states (3-5). Beyond that, it becomes spaghetti.

### 3.2 — OOP State Pattern (scalable)

```csharp
using UnityEngine;

// --- Base interface ---
public interface IState
{
    void Enter();
    void Execute();  // Called in Update
    void Exit();
}

// --- Generic State Machine ---
public class StateMachine
{
    public IState CurrentState { get; private set; }

    public void ChangeState(IState newState)
    {
        CurrentState?.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Execute();
    }
}

// --- Concrete states for a Player ---
public class PlayerIdleState : IState
{
    private PlayerController player;

    public PlayerIdleState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.Animator.Play("Idle");
        player.Rigidbody.linearVelocity = Vector3.zero;
    }

    public void Execute()
    {
        if (player.InputHandler.MoveInput.magnitude > 0.1f)
            player.StateMachine.ChangeState(new PlayerRunState(player));

        if (player.InputHandler.JumpPressed && player.IsGrounded)
            player.StateMachine.ChangeState(new PlayerJumpState(player));
    }

    public void Exit() { }
}

public class PlayerRunState : IState
{
    private PlayerController player;

    public PlayerRunState(PlayerController player)
    {
        this.player = player;
    }

    public void Enter()
    {
        player.Animator.Play("Run");
    }

    public void Execute()
    {
        Vector3 move = player.InputHandler.MoveInput * player.MoveSpeed;
        player.Rigidbody.linearVelocity = new Vector3(move.x, player.Rigidbody.linearVelocity.y, move.z);

        if (player.InputHandler.MoveInput.magnitude < 0.1f)
            player.StateMachine.ChangeState(new PlayerIdleState(player));

        if (player.InputHandler.JumpPressed && player.IsGrounded)
            player.StateMachine.ChangeState(new PlayerJumpState(player));

        if (player.InputHandler.AttackPressed)
            player.StateMachine.ChangeState(new PlayerAttackState(player));
    }

    public void Exit() { }
}

// --- Controller that uses the state machine ---
public class PlayerController : MonoBehaviour
{
    public StateMachine StateMachine { get; private set; }
    public Animator Animator { get; private set; }
    public Rigidbody Rigidbody { get; private set; }
    public InputHandler InputHandler { get; private set; }
    public float MoveSpeed = 6f;
    public bool IsGrounded => Physics.Raycast(transform.position, Vector3.down, 1.1f);

    private void Awake()
    {
        StateMachine = new StateMachine();
        Animator = GetComponentInChildren<Animator>();
        Rigidbody = GetComponent<Rigidbody>();
        InputHandler = GetComponent<InputHandler>();
    }

    private void Start()
    {
        StateMachine.ChangeState(new PlayerIdleState(this));
    }

    private void Update()
    {
        StateMachine.Update();
    }
}
```

### 3.3 — Hierarchical FSM (HFSM)

```csharp
// Super-states that contain sub-states
// Ex: "Combat" is a super-state that can have sub-states "Melee", "Ranged", "Block"

public class HierarchicalState : IState
{
    protected StateMachine subStateMachine = new StateMachine();

    public virtual void Enter() { }

    public virtual void Execute()
    {
        subStateMachine.Update();
    }

    public virtual void Exit()
    {
        subStateMachine.CurrentState?.Exit();
    }
}

// --- Example: Boss with phases ---
public class BossCombatState : HierarchicalState
{
    private BossController boss;

    public BossCombatState(BossController boss)
    {
        this.boss = boss;
    }

    public override void Enter()
    {
        // Start boss phase 1
        subStateMachine.ChangeState(new BossMeleePhase(boss));
    }

    public override void Execute()
    {
        base.Execute(); // Updates sub-state machine

        // Phase transition based on HP
        if (boss.HealthPercent < 0.5f
            && subStateMachine.CurrentState is BossMeleePhase)
        {
            subStateMachine.ChangeState(new BossRangedPhase(boss));
        }

        if (boss.HealthPercent <= 0f)
        {
            boss.MainStateMachine.ChangeState(new BossDeathState(boss));
        }
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Player controller with multiple states | Objects with a single behavior |
| Enemy AI (patrol → chase → attack) | Purely reactive logic with no state "memory" |
| UI flow (menu → game → pause → game over) | When Animator Controller already handles it (simple animation states) |
| Boss fights with phases (HFSM) | Over-engineering for background NPCs |

---

## 4. Object Pool

### Problem it solves

Instantiating and destroying objects (bullets, particles, enemies) generates garbage collection spikes that cause stuttering. Object Pool pre-allocates objects and recycles them, eliminating runtime allocations.

### 4.1 — Unity IObjectPool (built-in, Unity 2021+)

```csharp
using UnityEngine;
using UnityEngine.Pool;

public class BulletSpawner : MonoBehaviour
{
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private int defaultCapacity = 20;
    [SerializeField] private int maxSize = 100;

    private IObjectPool<Bullet> pool;

    private void Awake()
    {
        pool = new ObjectPool<Bullet>(
            createFunc:         () => Instantiate(bulletPrefab),
            actionOnGet:        (bullet) => bullet.gameObject.SetActive(true),
            actionOnRelease:    (bullet) => bullet.gameObject.SetActive(false),
            actionOnDestroy:    (bullet) => Destroy(bullet.gameObject),
            collectionCheck:    true,
            defaultCapacity:    defaultCapacity,
            maxSize:            maxSize
        );
    }

    public Bullet SpawnBullet(Vector3 position, Vector3 direction)
    {
        var bullet = pool.Get();
        bullet.transform.position = position;
        bullet.transform.forward = direction;
        bullet.Initialize(pool); // Bullet needs to know how to return itself
        return bullet;
    }
}

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 30f;
    [SerializeField] private float lifetime = 3f;

    private IObjectPool<Bullet> pool;
    private float timer;

    public void Initialize(IObjectPool<Bullet> pool)
    {
        this.pool = pool;
        timer = 0f;
    }

    private void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;
        timer += Time.deltaTime;

        if (timer >= lifetime)
            ReturnToPool();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Apply damage, spawn VFX, etc.
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        pool.Release(this);
    }
}
```

### 4.2 — Custom generic pool (no Unity.Pool dependency)

```csharp
using System.Collections.Generic;
using UnityEngine;

public class GenericPool<T> where T : Component
{
    private T prefab;
    private Transform parent;
    private Queue<T> available = new Queue<T>();
    private HashSet<T> inUse = new HashSet<T>();

    public int CountActive => inUse.Count;
    public int CountInactive => available.Count;

    public GenericPool(T prefab, Transform parent, int initialSize)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < initialSize; i++)
        {
            var obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            available.Enqueue(obj);
        }
    }

    public T Get()
    {
        T obj;
        if (available.Count > 0)
        {
            obj = available.Dequeue();
        }
        else
        {
            obj = Object.Instantiate(prefab, parent);
            Debug.LogWarning($"[Pool] Expanding pool for {prefab.name}. " +
                             $"Consider increasing initialSize.");
        }

        obj.gameObject.SetActive(true);
        inUse.Add(obj);
        return obj;
    }

    public void Release(T obj)
    {
        if (!inUse.Contains(obj)) return;

        obj.gameObject.SetActive(false);
        inUse.Remove(obj);
        available.Enqueue(obj);
    }

    public void ReleaseAll()
    {
        foreach (var obj in inUse)
        {
            obj.gameObject.SetActive(false);
            available.Enqueue(obj);
        }
        inUse.Clear();
    }
}

// --- Usage ---
public class VFXManager : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitEffectPrefab;
    private GenericPool<ParticleSystem> hitEffectPool;

    private void Awake()
    {
        hitEffectPool = new GenericPool<ParticleSystem>(
            hitEffectPrefab, transform, initialSize: 15);
    }

    public void PlayHitEffect(Vector3 position)
    {
        var fx = hitEffectPool.Get();
        fx.transform.position = position;
        fx.Play();
        StartCoroutine(ReturnAfterDelay(fx, fx.main.duration));
    }

    private System.Collections.IEnumerator ReturnAfterDelay(
        ParticleSystem fx, float delay)
    {
        yield return new WaitForSeconds(delay);
        hitEffectPool.Release(fx);
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Projectiles (bullets, arrows, magic missiles) | Unique objects created once (boss, player) |
| Frequent particle effects | Objects that are rarely created/destroyed |
| Enemies in wave-based games | When memory is not a bottleneck (early prototypes) |
| Pickups, coins, damage numbers | Objects with complex state that is hard to reset |

**Practical rule:** If you call `Instantiate()` and `Destroy()` on the same type of object more than ~10 times per second, use a pool.

---

## 5. Singleton

### Problem it solves

Some systems need exactly one instance accessible globally — AudioManager, SaveSystem, InputManager. Singleton guarantees a unique instance and a global access point.

### 5.1 — Robust implementation

```csharp
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T instance;
    private static readonly object lockObj = new object();
    private static bool isShuttingDown = false;

    public static T Instance
    {
        get
        {
            if (isShuttingDown)
            {
                Debug.LogWarning($"[Singleton] Trying to access {typeof(T)} " +
                                 "during shutdown. Returning null.");
                return null;
            }

            lock (lockObj)
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<T>();

                    if (instance == null)
                    {
                        var go = new GameObject($"[{typeof(T).Name}]");
                        instance = go.AddComponent<T>();
                    }
                }
                return instance;
            }
        }
    }

    protected virtual void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    private void OnApplicationQuit()
    {
        isShuttingDown = true;
    }

    private void OnDestroy()
    {
        if (instance == this)
            isShuttingDown = true;
    }
}

// --- Usage ---
public class AudioManager : Singleton<AudioManager>
{
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        // ...
    }
}

// Anywhere:
// AudioManager.Instance.PlaySFX(hitSound);
```

### 5.2 — Problems with Singleton

Singleton is the most abused pattern in game dev. Real problems:

1. **Hidden coupling** — any class can access `AudioManager.Instance`, making dependencies invisible.
2. **Initialization order** — if `A.Awake()` accesses `B.Instance` before B exists, behavior is undefined.
3. **Zero testability** — impossible to mock in unit tests.
4. **Scene persistence** — `DontDestroyOnLoad` can cause duplicates if poorly implemented.
5. **God Object tendency** — Singletons tend to accumulate responsibilities ("since it's global, I'll put it here").

### 5.3 — Alternative: Service Locator

```csharp
using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static Dictionary<Type, object> services = new Dictionary<Type, object>();

    public static void Register<T>(T service) where T : class
    {
        var type = typeof(T);
        if (services.ContainsKey(type))
        {
            Debug.LogWarning($"[ServiceLocator] Replacing service {type.Name}");
        }
        services[type] = service;
    }

    public static T Get<T>() where T : class
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var service))
            return service as T;

        throw new InvalidOperationException(
            $"[ServiceLocator] Service {type.Name} not registered.");
    }

    public static bool TryGet<T>(out T service) where T : class
    {
        var type = typeof(T);
        if (services.TryGetValue(type, out var obj))
        {
            service = obj as T;
            return true;
        }
        service = null;
        return false;
    }

    public static void Unregister<T>() where T : class
    {
        services.Remove(typeof(T));
    }

    public static void Clear()
    {
        services.Clear();
    }
}

// --- Interface + implementation ---
public interface IAudioService
{
    void PlaySFX(AudioClip clip, float volume = 1f);
    void PlayMusic(AudioClip clip);
}

public class AudioService : MonoBehaviour, IAudioService
{
    private AudioSource sfxSource;
    private AudioSource musicSource;

    private void Awake()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;

        ServiceLocator.Register<IAudioService>(this);
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IAudioService>();
    }

    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        sfxSource.PlayOneShot(clip, volume);
    }

    public void PlayMusic(AudioClip clip)
    {
        musicSource.clip = clip;
        musicSource.Play();
    }
}

// --- Usage ---
// ServiceLocator.Get<IAudioService>().PlaySFX(hitClip);
```

**Advantage over Singleton:** Allows swapping the implementation (e.g., `NullAudioService` for tests), decouples via interface.

### 5.4 — Alternative: ScriptableObject as "service"

```csharp
[CreateAssetMenu(menuName = "Services/Game Settings")]
public class GameSettings : ScriptableObject
{
    public float MasterVolume = 1f;
    public float MusicVolume = 0.7f;
    public float SFXVolume = 1f;
    public int TargetFrameRate = 60;
}

// Inject via Inspector — no global access, no Singleton
public class AudioController : MonoBehaviour
{
    [SerializeField] private GameSettings settings;

    private void Update()
    {
        // Use settings.MasterVolume etc.
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Quick prototypes and game jams | Large productions — prefer Service Locator or DI |
| Genuinely global systems (1 per game) | Anything that could have multiple instances |
| When architecture overhead is not worth it | When testability matters |

---

## 6. Strategy Pattern

### Problem it solves

You have multiple variations of a behavior (different attack types, different AIs, different pathfinding algorithms) and want to switch between them at runtime without `if/else` or `switch` statements.

### 6.1 — Swappable ability system

```csharp
using UnityEngine;

// --- Strategy interface ---
public interface IAbility
{
    string Name { get; }
    float Cooldown { get; }
    void Execute(Transform caster, Transform target);
}

// --- Concrete implementations ---
public class FireballAbility : IAbility
{
    public string Name => "Fireball";
    public float Cooldown => 2f;

    public void Execute(Transform caster, Transform target)
    {
        Vector3 direction = (target.position - caster.position).normalized;
        // Spawn fireball projectile, apply damage on hit
        Debug.Log($"Casting Fireball from {caster.name} to {target.name}");
    }
}

public class TeleportAbility : IAbility
{
    public string Name => "Teleport";
    public float Cooldown => 5f;

    public void Execute(Transform caster, Transform target)
    {
        caster.position = target.position + Vector3.back * 2f;
        Debug.Log($"{caster.name} teleported behind {target.name}");
    }
}

public class HealAbility : IAbility
{
    public string Name => "Heal";
    public float Cooldown => 8f;

    public void Execute(Transform caster, Transform target)
    {
        var health = caster.GetComponent<PlayerHealth>();
        health?.Heal(30);
        Debug.Log($"{caster.name} healed for 30 HP");
    }
}

// --- Character that uses strategies ---
public class AbilityUser : MonoBehaviour
{
    private IAbility[] equippedAbilities = new IAbility[4];
    private float[] cooldownTimers = new float[4];

    public void EquipAbility(int slot, IAbility ability)
    {
        equippedAbilities[slot] = ability;
        cooldownTimers[slot] = 0f;
    }

    public void UseAbility(int slot, Transform target)
    {
        if (equippedAbilities[slot] == null) return;
        if (cooldownTimers[slot] > 0f) return;

        equippedAbilities[slot].Execute(transform, target);
        cooldownTimers[slot] = equippedAbilities[slot].Cooldown;
    }

    private void Update()
    {
        for (int i = 0; i < cooldownTimers.Length; i++)
        {
            if (cooldownTimers[i] > 0f)
                cooldownTimers[i] -= Time.deltaTime;
        }
    }
}
```

### 6.2 — Swappable AI behaviors

```csharp
using UnityEngine;

public interface IEnemyBehavior
{
    void UpdateBehavior(EnemyController enemy);
}

public class AggressiveBehavior : IEnemyBehavior
{
    public void UpdateBehavior(EnemyController enemy)
    {
        // Always advances toward the player and attacks
        enemy.MoveTowards(enemy.Player.position);
        if (enemy.InAttackRange)
            enemy.Attack();
    }
}

public class CautiousBehavior : IEnemyBehavior
{
    public void UpdateBehavior(EnemyController enemy)
    {
        // Keeps distance, attacks at range
        if (enemy.DistanceToPlayer < 5f)
            enemy.MoveAwayFrom(enemy.Player.position);
        else
            enemy.RangedAttack();
    }
}

public class FleeingBehavior : IEnemyBehavior
{
    public void UpdateBehavior(EnemyController enemy)
    {
        enemy.MoveAwayFrom(enemy.Player.position);
    }
}

public class EnemyController : MonoBehaviour
{
    public Transform Player;
    public bool InAttackRange => DistanceToPlayer < 2f;
    public float DistanceToPlayer =>
        Vector3.Distance(transform.position, Player.position);

    private IEnemyBehavior currentBehavior;

    public void SetBehavior(IEnemyBehavior behavior)
    {
        currentBehavior = behavior;
    }

    private void Update()
    {
        currentBehavior?.UpdateBehavior(this);

        // Strategy swap based on conditions
        if (DistanceToPlayer < 3f && currentBehavior is CautiousBehavior)
            SetBehavior(new FleeingBehavior());
    }

    public void MoveTowards(Vector3 target) { /* ... */ }
    public void MoveAwayFrom(Vector3 target) { /* ... */ }
    public void Attack() { /* ... */ }
    public void RangedAttack() { /* ... */ }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Multiple variations of the same behavior | When only one implementation exists (YAGNI) |
| Abilities/spells with different logic | Trivial logic that fits in 5 lines |
| AI with behaviors that change at runtime | When the variation is only in data, not logic (use SO) |
| Damage calculation algorithms | |

---

## 7. Factory Pattern

### Problem it solves

Creating objects is complex — it involves configuration, dependencies, and variations. Factory centralizes creation logic, preventing spawning code from being scattered across the project. Especially powerful with ScriptableObjects for data-driven factories.

### 7.1 — Simple Factory for spawning

```csharp
using UnityEngine;

public class EnemyFactory : MonoBehaviour
{
    [SerializeField] private GameObject goblinPrefab;
    [SerializeField] private GameObject skeletonPrefab;
    [SerializeField] private GameObject dragonPrefab;

    public enum EnemyType { Goblin, Skeleton, Dragon }

    public GameObject SpawnEnemy(EnemyType type, Vector3 position)
    {
        GameObject prefab = type switch
        {
            EnemyType.Goblin   => goblinPrefab,
            EnemyType.Skeleton => skeletonPrefab,
            EnemyType.Dragon   => dragonPrefab,
            _ => throw new System.ArgumentException($"Unknown enemy type: {type}")
        };

        var enemy = Instantiate(prefab, position, Quaternion.identity);
        ConfigureEnemy(enemy, type);
        return enemy;
    }

    private void ConfigureEnemy(GameObject enemy, EnemyType type)
    {
        var health = enemy.GetComponent<EnemyHealth>();
        var ai = enemy.GetComponent<EnemyAI>();

        // Configuration based on type
        switch (type)
        {
            case EnemyType.Goblin:
                health.SetMaxHealth(50);
                ai.SetAggression(0.8f);
                break;
            case EnemyType.Dragon:
                health.SetMaxHealth(500);
                ai.SetAggression(1.0f);
                break;
        }
    }
}
```

### 7.2 — Data-driven Factory with ScriptableObjects

```csharp
using UnityEngine;

// --- Enemy configuration SO ---
[CreateAssetMenu(menuName = "Enemies/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    public string EnemyName;
    public GameObject Prefab;
    public int BaseHealth;
    public float MoveSpeed;
    public float AttackDamage;
    public float AggroRange;
    public Color TintColor = Color.white;

    [Header("Loot")]
    public LootTable LootTable;

    [Header("Scaling")]
    public float HealthPerLevel = 10f;
    public float DamagePerLevel = 2f;
}

// --- Data-driven factory ---
public class DataDrivenEnemyFactory : MonoBehaviour
{
    [SerializeField] private EnemyConfig[] enemyConfigs;

    // Registry for lookup by name
    private System.Collections.Generic.Dictionary<string, EnemyConfig> configMap;

    private void Awake()
    {
        configMap = new System.Collections.Generic.Dictionary<string, EnemyConfig>();
        foreach (var config in enemyConfigs)
            configMap[config.EnemyName] = config;
    }

    public GameObject Create(string enemyName, Vector3 position, int level = 1)
    {
        if (!configMap.TryGetValue(enemyName, out var config))
        {
            Debug.LogError($"[EnemyFactory] Config not found: {enemyName}");
            return null;
        }

        var enemy = Instantiate(config.Prefab, position, Quaternion.identity);

        // Apply SO configuration
        var health = enemy.GetComponent<EnemyHealth>();
        health.SetMaxHealth(
            Mathf.RoundToInt(config.BaseHealth + config.HealthPerLevel * (level - 1)));

        var movement = enemy.GetComponent<EnemyMovement>();
        movement.SetSpeed(config.MoveSpeed);

        var combat = enemy.GetComponent<EnemyCombat>();
        combat.SetDamage(config.AttackDamage + config.DamagePerLevel * (level - 1));
        combat.SetAggroRange(config.AggroRange);

        var renderer = enemy.GetComponentInChildren<SpriteRenderer>();
        if (renderer != null)
            renderer.color = config.TintColor;

        return enemy;
    }
}
```

### 7.3 — Abstract Factory for families of objects

```csharp
// Creates "families" of related objects — e.g., the visual theme of a biome

public interface IEnvironmentFactory
{
    GameObject CreateGround();
    GameObject CreateObstacle();
    GameObject CreateDecoration();
    ParticleSystem CreateAmbientEffect();
}

public class ForestFactory : MonoBehaviour, IEnvironmentFactory
{
    [SerializeField] private GameObject grassTile;
    [SerializeField] private GameObject treePrefab;
    [SerializeField] private GameObject bushPrefab;
    [SerializeField] private ParticleSystem leavesFX;

    public GameObject CreateGround()      => Instantiate(grassTile);
    public GameObject CreateObstacle()    => Instantiate(treePrefab);
    public GameObject CreateDecoration()  => Instantiate(bushPrefab);
    public ParticleSystem CreateAmbientEffect() => Instantiate(leavesFX);
}

public class DesertFactory : MonoBehaviour, IEnvironmentFactory
{
    [SerializeField] private GameObject sandTile;
    [SerializeField] private GameObject rockPrefab;
    [SerializeField] private GameObject cactusPrefab;
    [SerializeField] private ParticleSystem sandstormFX;

    public GameObject CreateGround()      => Instantiate(sandTile);
    public GameObject CreateObstacle()    => Instantiate(rockPrefab);
    public GameObject CreateDecoration()  => Instantiate(cactusPrefab);
    public ParticleSystem CreateAmbientEffect() => Instantiate(sandstormFX);
}

// --- Level generator uses the abstract factory ---
public class LevelGenerator : MonoBehaviour
{
    private IEnvironmentFactory factory;

    public void SetBiome(IEnvironmentFactory biomeFactory)
    {
        factory = biomeFactory;
    }

    public void GenerateChunk(Vector3 origin)
    {
        for (int x = 0; x < 10; x++)
        for (int z = 0; z < 10; z++)
        {
            var ground = factory.CreateGround();
            ground.transform.position = origin + new Vector3(x, 0, z);

            if (Random.value < 0.1f)
            {
                var obstacle = factory.CreateObstacle();
                obstacle.transform.position = origin + new Vector3(x, 0.5f, z);
            }
        }
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Wave spawner with varied enemy types | When `Instantiate(prefab)` is sufficient |
| Procedural generation with themes/biomes | For a few objects without variation |
| Data-driven games (designers create EnemyConfig SOs) | When creation is trivial and does not change |
| Loot systems with rarity and configuration | |

---

## 8. Decorator Pattern

### Problem it solves

You want to add behaviors to objects at runtime in a composable way — buffs, debuffs, damage modifiers — without creating a subclass for every possible combination. Decorator allows stacking modifiers.

### 8.1 — Damage modifier system

```csharp
// --- Base interface ---
public interface IDamageDealer
{
    float CalculateDamage();
    string GetDescription();
}

// --- Base implementation ---
public class BaseDamage : IDamageDealer
{
    private float baseDamage;

    public BaseDamage(float damage)
    {
        baseDamage = damage;
    }

    public float CalculateDamage() => baseDamage;
    public string GetDescription() => $"Base: {baseDamage}";
}

// --- Decorators ---
public abstract class DamageModifier : IDamageDealer
{
    protected IDamageDealer wrapped;

    protected DamageModifier(IDamageDealer wrapped)
    {
        this.wrapped = wrapped;
    }

    public abstract float CalculateDamage();
    public abstract string GetDescription();
}

public class CriticalHitModifier : DamageModifier
{
    private float critMultiplier;

    public CriticalHitModifier(IDamageDealer wrapped, float multiplier = 2f)
        : base(wrapped)
    {
        critMultiplier = multiplier;
    }

    public override float CalculateDamage()
        => wrapped.CalculateDamage() * critMultiplier;

    public override string GetDescription()
        => $"{wrapped.GetDescription()} → Crit x{critMultiplier}";
}

public class ElementalModifier : DamageModifier
{
    private float bonusDamage;
    private string element;

    public ElementalModifier(IDamageDealer wrapped, string element, float bonus)
        : base(wrapped)
    {
        this.element = element;
        this.bonusDamage = bonus;
    }

    public override float CalculateDamage()
        => wrapped.CalculateDamage() + bonusDamage;

    public override string GetDescription()
        => $"{wrapped.GetDescription()} → +{bonusDamage} {element}";
}

public class ArmorPenetrationModifier : DamageModifier
{
    private float penetrationPercent;

    public ArmorPenetrationModifier(IDamageDealer wrapped, float percent)
        : base(wrapped)
    {
        penetrationPercent = percent;
    }

    public override float CalculateDamage()
        => wrapped.CalculateDamage() * (1f + penetrationPercent);

    public override string GetDescription()
        => $"{wrapped.GetDescription()} → Pen {penetrationPercent * 100}%";
}

// --- Gameplay usage ---
// IDamageDealer damage = new BaseDamage(25f);
// damage = new ElementalModifier(damage, "Fire", 10f);    // 25 + 10 = 35
// damage = new CriticalHitModifier(damage, 2f);           // 35 * 2 = 70
// damage = new ArmorPenetrationModifier(damage, 0.3f);    // 70 * 1.3 = 91
// Debug.Log(damage.GetDescription());
// → "Base: 25 → +10 Fire → Crit x2 → Pen 30%"
```

### 8.2 — Composable Buff/Debuff system

```csharp
using System.Collections.Generic;
using UnityEngine;

// --- Base stats ---
[System.Serializable]
public class CharacterStats
{
    public float MoveSpeed;
    public float AttackSpeed;
    public float DamageMultiplier;
    public float DamageReduction;

    public CharacterStats(float move, float atkSpd, float dmgMul, float dmgRed)
    {
        MoveSpeed = move;
        AttackSpeed = atkSpd;
        DamageMultiplier = dmgMul;
        DamageReduction = dmgRed;
    }

    public CharacterStats Clone() => new CharacterStats(
        MoveSpeed, AttackSpeed, DamageMultiplier, DamageReduction);
}

// --- Abstract buff ---
public abstract class Buff
{
    public string Name;
    public float Duration;
    public float RemainingTime;

    public abstract void Apply(CharacterStats stats);
}

// --- Concrete buffs ---
public class SpeedBoost : Buff
{
    private float multiplier;

    public SpeedBoost(float multiplier, float duration)
    {
        Name = "Speed Boost";
        this.multiplier = multiplier;
        Duration = duration;
        RemainingTime = duration;
    }

    public override void Apply(CharacterStats stats)
    {
        stats.MoveSpeed *= multiplier;
    }
}

public class PoisonDebuff : Buff
{
    private float slowPercent;

    public PoisonDebuff(float slowPercent, float duration)
    {
        Name = "Poison";
        this.slowPercent = slowPercent;
        Duration = duration;
        RemainingTime = duration;
    }

    public override void Apply(CharacterStats stats)
    {
        stats.MoveSpeed *= (1f - slowPercent);
        stats.AttackSpeed *= 0.8f;
    }
}

// --- Buff system ---
public class BuffSystem : MonoBehaviour
{
    [SerializeField] private CharacterStats baseStats =
        new CharacterStats(5f, 1f, 1f, 0f);

    private List<Buff> activeBuffs = new List<Buff>();

    public CharacterStats CurrentStats { get; private set; }

    public void AddBuff(Buff buff)
    {
        activeBuffs.Add(buff);
        RecalculateStats();
    }

    private void Update()
    {
        bool changed = false;
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].RemainingTime -= Time.deltaTime;
            if (activeBuffs[i].RemainingTime <= 0f)
            {
                activeBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed) RecalculateStats();
    }

    private void RecalculateStats()
    {
        CurrentStats = baseStats.Clone();
        foreach (var buff in activeBuffs)
            buff.Apply(CurrentStats);
    }
}
```

### When to use / When not to use

| Use | Do not use |
|------|----------|
| Stackable buffs/debuffs | Fixed modifiers known at compile time |
| Composable weapon mods (scope + silencer + extended mag) | When there are few combinations (use simple inheritance) |
| Damage pipeline with phases (base → elemental → crit → armor) | When the order of modifiers does not matter (use a simple list) |
| Potion/enchantment stacking | |

---

## 9. Component Pattern

### Problem it solves

Unity already implements the Component Pattern natively via `MonoBehaviour` + `GameObject`. The challenge is using it **well** — small, focused components vs. mega-scripts that do everything.

### 9.1 — Correct composition: granular components

```csharp
// ❌ WRONG: One MonoBehaviour that does everything
public class Player : MonoBehaviour
{
    // 500+ lines with movement, combat, inventory,
    // health, stamina, animation, input, audio...
}

// ✅ CORRECT: Separate and focused components
// Each in its own file

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 8f;

    private Rigidbody rb;
    private PlayerInput input; // Reference to another component

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<PlayerInput>();
    }

    private void FixedUpdate()
    {
        Vector3 move = input.MoveDirection * moveSpeed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

    public void Jump()
    {
        if (IsGrounded())
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private bool IsGrounded() =>
        Physics.Raycast(transform.position, Vector3.down, 1.1f);
}

public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 2f;
    [SerializeField] private LayerMask enemyLayer;

    public void Attack()
    {
        var hits = Physics.OverlapSphere(
            transform.position + transform.forward, attackRange, enemyLayer);

        foreach (var hit in hits)
        {
            if (hit.TryGetComponent<IDamageable>(out var target))
                target.TakeDamage(attackDamage);
        }
    }
}

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;

    public int CurrentHealth => currentHealth;
    public event System.Action<int, int> OnHealthChanged;
    public event System.Action OnDied;

    private void Awake() => currentHealth = maxHealth;

    public void TakeDamage(int amount)
    {
        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        if (currentHealth <= 0) OnDied?.Invoke();
    }

    public void Heal(int amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
}

// Shared interface — anything that takes damage
public interface IDamageable
{
    int CurrentHealth { get; }
    void TakeDamage(int amount);
    void Heal(int amount);
}
```

### 9.2 — RequireComponent and communication between components

```csharp
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerMovement : MonoBehaviour
{
    // Unity guarantees Rigidbody and PlayerInput exist
    // when this component is added
}

// --- Communication between components on the same GO ---

// Option A: GetComponent in Awake (cache it!)
public class PlayerAnimator : MonoBehaviour
{
    private PlayerMovement movement;
    private PlayerCombat combat;
    private Animator animator;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        animator = GetComponentInChildren<Animator>();
    }
}

// Option B: [SerializeField] — more explicit
public class PlayerAudio : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerCombat combat;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip attackSound;
}

// Option C: Events — fully decoupled
public class PlayerVFX : MonoBehaviour
{
    [SerializeField] private ParticleSystem hitParticles;

    private PlayerHealth health;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
    }

    private void OnEnable()
    {
        health.OnHealthChanged += OnHealthChanged;
        health.OnDied += OnDeath;
    }

    private void OnDisable()
    {
        health.OnHealthChanged -= OnHealthChanged;
        health.OnDied -= OnDeath;
    }

    private void OnHealthChanged(int current, int max)
    {
        if (current < max) hitParticles.Play();
    }

    private void OnDeath()
    {
        // Spawn death VFX
    }
}
```

### Principles for good use of the Component Pattern

1. **Single Responsibility** — each component does ONE thing.
2. **Interfaces for communication** — `IDamageable`, `IInteractable`, `IPickable` allow components to interact without knowing about each other.
3. **Cache GetComponent** — NEVER call `GetComponent<T>()` in `Update()`. Always cache in `Awake()`.
4. **Prefer composition over inheritance** — instead of `Enemy → FlyingEnemy → FlyingRangedEnemy`, use components: `FlightComponent` + `RangedAttackComponent`.
5. **ScriptableObjects for data** — components contain logic, SOs contain configuration.

---

## 10. SOLID Principles Applied to Unity

### S — Single Responsibility Principle

> A class should have only one reason to change.

```csharp
// ❌ Violates SRP — handles input, movement, and animation
public class BadPlayerController : MonoBehaviour
{
    private void Update()
    {
        // Input
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Movement
        transform.position += new Vector3(h, 0, v) * 5f * Time.deltaTime;

        // Animation
        GetComponent<Animator>().SetFloat("Speed", new Vector2(h, v).magnitude);

        // Audio
        if (new Vector2(h, v).magnitude > 0.1f)
            GetComponent<AudioSource>().Play();
    }
}

// ✅ Respects SRP — each class has one responsibility
public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveDirection { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool AttackPressed { get; private set; }

    private void Update()
    {
        MoveDirection = new Vector2(
            Input.GetAxis("Horizontal"),
            Input.GetAxis("Vertical"));
        JumpPressed = Input.GetButtonDown("Jump");
        AttackPressed = Input.GetButtonDown("Fire1");
    }
}
// + PlayerMovement, PlayerAnimator, PlayerAudio separated
```

### O — Open/Closed Principle

> Open for extension, closed for modification.

```csharp
// ❌ Violates OCP — requires modification for each new damage type
public class DamageCalculator
{
    public float Calculate(string type, float base_dmg)
    {
        switch (type)
        {
            case "fire": return base_dmg * 1.5f;
            case "ice": return base_dmg * 1.2f;
            case "poison": return base_dmg * 0.8f;
            // Each new element = modify this class
            default: return base_dmg;
        }
    }
}

// ✅ Respects OCP — new damage type = new SO, without touching existing code
[CreateAssetMenu(menuName = "Damage/Element")]
public class DamageElement : ScriptableObject
{
    public string ElementName;
    public float DamageMultiplier = 1f;
    public Color EffectColor = Color.white;
    public GameObject HitEffectPrefab;

    public virtual float ModifyDamage(float baseDamage)
    {
        return baseDamage * DamageMultiplier;
    }
}

public class DamageDealer : MonoBehaviour
{
    [SerializeField] private float baseDamage = 10f;
    [SerializeField] private DamageElement element; // Drag in the Inspector

    public float GetFinalDamage() => element.ModifyDamage(baseDamage);
}
```

### L — Liskov Substitution Principle

> Subtypes must be substitutable for their base types.

```csharp
// ❌ Violates LSP — Turret inherits from Enemy but does not move
public class Enemy : MonoBehaviour
{
    public virtual void Move(Vector3 target)
    {
        transform.position = Vector3.MoveTowards(
            transform.position, target, 5f * Time.deltaTime);
    }

    public virtual void Attack() { /* ... */ }
}

public class Turret : Enemy
{
    public override void Move(Vector3 target)
    {
        // Turret does not move — violates the base type's expectation!
        throw new System.NotSupportedException("Turrets don't move!");
    }
}

// ✅ Respects LSP — granular interfaces
public interface IMovable
{
    void Move(Vector3 target);
}

public interface IAttacker
{
    void Attack(IDamageable target);
}

public class MeleeEnemy : MonoBehaviour, IMovable, IAttacker
{
    public void Move(Vector3 target) { /* walks to target */ }
    public void Attack(IDamageable target) { /* melee attack */ }
}

public class Turret : MonoBehaviour, IAttacker
{
    // Does not implement IMovable — no broken contract
    public void Attack(IDamageable target) { /* ranged attack */ }
}
```

### I — Interface Segregation Principle

> Clients should not depend on interfaces they do not use.

```csharp
// ❌ Violates ISP — giant interface that forces empty implementations
public interface IEntity
{
    void Move(Vector3 target);
    void TakeDamage(int amount);
    void Attack();
    void OpenInventory();
    void Talk(string dialogue);
    void Trade(IEntity other);
}

// Chest needs to implement Move, Attack, Talk... makes no sense

// ✅ Respects ISP — focused interfaces
public interface IDamageable
{
    void TakeDamage(int amount);
    int CurrentHealth { get; }
}

public interface IInteractable
{
    string InteractionPrompt { get; }
    void Interact(GameObject interactor);
}

public interface IMovable
{
    void MoveTo(Vector3 target);
    float MoveSpeed { get; }
}

public interface ITalkable
{
    void StartDialogue();
}

// Chest only implements what makes sense
public class TreasureChest : MonoBehaviour, IInteractable, IDamageable
{
    public string InteractionPrompt => "Open chest";
    public int CurrentHealth => health;

    private int health = 20;

    public void Interact(GameObject interactor) { /* open and drop loot */ }
    public void TakeDamage(int amount) { health -= amount; /* break */ }
}
```

### D — Dependency Inversion Principle

> Depend on abstractions, not on concrete implementations.

```csharp
// ❌ Violates DIP — SaveManager depends directly on implementation
public class SaveManager : MonoBehaviour
{
    private JsonFileSaver saver = new JsonFileSaver(); // Tightly coupled!

    public void SaveGame(GameData data)
    {
        saver.SaveToFile(data, "save.json");
    }
}

// ✅ Respects DIP — depends on interface
public interface ISaveSystem
{
    void Save(string key, string data);
    string Load(string key);
    bool HasSave(string key);
}

public class LocalFileSaveSystem : ISaveSystem
{
    public void Save(string key, string data)
        => System.IO.File.WriteAllText(GetPath(key), data);

    public string Load(string key)
        => System.IO.File.ReadAllText(GetPath(key));

    public bool HasSave(string key)
        => System.IO.File.Exists(GetPath(key));

    private string GetPath(string key)
        => $"{Application.persistentDataPath}/{key}.json";
}

public class CloudSaveSystem : ISaveSystem
{
    public void Save(string key, string data)   { /* upload to cloud */ }
    public string Load(string key)              { return /* download */; }
    public bool HasSave(string key)             { return /* check cloud */; }
}

public class PlayerPrefsSaveSystem : ISaveSystem
{
    public void Save(string key, string data) => PlayerPrefs.SetString(key, data);
    public string Load(string key) => PlayerPrefs.GetString(key, "");
    public bool HasSave(string key) => PlayerPrefs.HasKey(key);
}

// SaveManager depends on the abstraction
public class SaveManager : MonoBehaviour
{
    private ISaveSystem saveSystem;

    // Injection via constructor or method
    public void Initialize(ISaveSystem system)
    {
        saveSystem = system;
    }

    public void SaveGame(GameData data)
    {
        string json = JsonUtility.ToJson(data);
        saveSystem.Save("game_save", json);
    }

    public GameData LoadGame()
    {
        if (!saveSystem.HasSave("game_save")) return new GameData();
        string json = saveSystem.Load("game_save");
        return JsonUtility.FromJson<GameData>(json);
    }
}
```

---

## 11. Common Anti-Patterns

### 11.1 — God Object

The script that does everything: input, movement, combat, UI updates, audio, save/load.

```csharp
// ❌ GOD OBJECT — "GameManager" with 2000 lines
public class GameManager : MonoBehaviour
{
    // Manages: score, lives, level, enemy spawning, UI,
    // pause, save/load, audio, achievements, analytics...
    // Any change to any system touches this file.
    // Impossible to test. Impossible to merge in a team.
}
```

**Solution:** Decompose into focused systems — `ScoreSystem`, `SpawnManager`, `UIController`, `SaveSystem`. Each with a clear single responsibility.

### 11.2 — Manager Hell

The "solution" that is equally bad: instead of one God Object, you create 30 Managers (AudioManager, UIManager, EnemyManager, SpawnManager, WaveManager, LootManager, EffectManager, CameraManager...) all Singletons that reference each other.

```csharp
// ❌ MANAGER HELL — chain of circular dependencies
public class WaveManager : Singleton<WaveManager>
{
    private void SpawnWave()
    {
        EnemyManager.Instance.SpawnEnemies(waveData);
        UIManager.Instance.ShowWaveStart(waveNumber);
        AudioManager.Instance.PlayWaveMusic(waveNumber);
        AnalyticsManager.Instance.TrackWaveStarted(waveNumber);
        DifficultyManager.Instance.ScaleForWave(waveNumber);
        // If any of these is null... cascade of errors
    }
}
```

**Solution:** Use events to decouple. `WaveManager` fires `OnWaveStarted`, and whoever needs to react subscribes. No Manager knows about the others.

```csharp
// ✅ Decoupled via events
public class WaveManager : MonoBehaviour
{
    public event System.Action<int> OnWaveStarted;

    private void SpawnWave()
    {
        // Local spawn logic
        OnWaveStarted?.Invoke(waveNumber);
        // UI, Audio, Analytics subscribe on their own
    }
}
```

### 11.3 — Update() Abuse

Placing expensive or unnecessary logic in `Update()` that runs every frame.

```csharp
// ❌ UPDATE ABUSE
public class BadExample : MonoBehaviour
{
    private void Update()
    {
        // FindObjectOfType EVERY FRAME — O(n) across all MonoBehaviours
        var player = FindFirstObjectByType<Player>();

        // GetComponent every frame — should be cached
        var rb = GetComponent<Rigidbody>();

        // String comparison every frame
        if (gameObject.tag == "Enemy") { }

        // Linq every frame — allocates garbage
        var nearbyEnemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None)
            .Where(e => Vector3.Distance(e.transform.position, transform.position) < 10f)
            .OrderBy(e => Vector3.Distance(e.transform.position, transform.position))
            .ToList();

        // Check that does not need to run every frame
        CheckIfPlayerIsInRange(); // Only needs to check every 0.5s
    }
}

// ✅ FIXED
public class GoodExample : MonoBehaviour
{
    private Player player;
    private Rigidbody rb;
    private float checkInterval = 0.5f;
    private float checkTimer;

    private void Awake()
    {
        // Cache in Awake
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Find once
        player = FindFirstObjectByType<Player>();
    }

    private void Update()
    {
        // CompareTag instead of == (no string allocation)
        if (gameObject.CompareTag("Enemy")) { }

        // Check with interval
        checkTimer += Time.deltaTime;
        if (checkTimer >= checkInterval)
        {
            checkTimer = 0f;
            CheckIfPlayerIsInRange();
        }
    }
}
```

**Other alternatives to Update polling:**

```csharp
// Coroutine for periodic checks
private IEnumerator PeriodicCheck()
{
    var wait = new WaitForSeconds(0.5f);
    while (true)
    {
        CheckCondition();
        yield return wait; // Reuse the WaitForSeconds!
    }
}

// InvokeRepeating for simple logic
private void Start()
{
    InvokeRepeating(nameof(SlowUpdate), 0f, 0.5f);
}

private void SlowUpdate()
{
    // Logic that does not need to run at 60fps
}

// Event-driven instead of polling
// Instead of checking "did the player die?" every frame,
// listen to the OnPlayerDied event
```

### Quick anti-pattern summary

| Anti-pattern | Symptom | Solution |
|---|---|---|
| God Object | One class with 1000+ lines and 20+ responsibilities | Decomposition by responsibility (SRP) |
| Manager Hell | 15+ Singletons with circular dependencies | Event-driven architecture, Service Locator |
| Update() Abuse | `FindObjectOfType`, `GetComponent`, Linq in Update | Cache, intervals, events, coroutines |
| Premature Optimization | Object pools for everything, including objects created once | Profile first, optimize later |
| String-Driven Development | `SendMessage("OnDamage")`, `GameObject.Find("Player")` | Direct references, interfaces, events |
| Inheritance Obsession | `Enemy → FlyingEnemy → FlyingRangedEnemy → FlyingRangedPoisonEnemy` | Composition via components |

---

## 12. Sources and References

### Books

- **Game Programming Patterns** — Robert Nystrom (freely available at [gameprogrammingpatterns.com](https://gameprogrammingpatterns.com)). Primary reference for Observer, Command, State, Object Pool, Component. Examples in C++ but concepts are universal.
- **Design Patterns: Elements of Reusable Object-Oriented Software** — Gang of Four. The original classic for Strategy, Factory, Decorator, Observer, Command.
- **Clean Code / Clean Architecture** — Robert C. Martin. Reference for SOLID principles.

### Unity Official

- **Unity E-Book: "Level up your code with game programming patterns"** — Unity Technologies (available at [unity.com/resources](https://unity.com/resources)). Covers Observer, Command, State, Object Pool, Factory, Strategy, Singleton, MVC/MVP applied specifically to Unity.
- **Unity E-Book: "Create a C# style guide"** — Best practices for organization and naming.
- **Unity Documentation** — `UnityEngine.Pool.ObjectPool<T>`, `UnityEvent`, `ScriptableObject`.

### Community and Blogs

- **Habrador** — [habrador.com](https://www.habrador.com). Extensive tutorials on design patterns in Unity with complete implementations and visuals. Covers State machines, Command pattern, Observer, and others with playable examples.
- **Refactoring Guru** — [refactoring.guru](https://refactoring.guru). Visual explanations of all design patterns with UML diagrams and C# examples.
- **Jason Weimann** (Unity3D College) — Videos and articles on architecture in Unity.
- **Infallible Code** — YouTube channel focused on patterns and clean architecture in Unity.

---

*Document compiled as a reference for game development in Unity with C#. The code examples are functional and can be adapted directly for real projects.*
