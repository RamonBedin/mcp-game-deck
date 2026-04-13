# Unity Project Architecture — Best Practices (2024–2026)

> Reference guide for structuring, organizing, and scaling Unity projects professionally.

---

## 1. Folder Structure: By Feature vs. By Type

There are two dominant approaches to organizing assets in a Unity project. In practice, most professional projects adopt a **hybrid approach**.

### 1.1 Type-Based Organization

Groups files by their technical nature. It is simple and works well for small projects, but becomes hard to navigate as the project grows.

```
Assets/
├── Scripts/
│   ├── PlayerController.cs
│   ├── EnemyAI.cs
│   └── UIManager.cs
├── Prefabs/
│   ├── Player.prefab
│   └── Enemy.prefab
├── Materials/
├── Textures/
└── Animations/
```

**Pros:** Easy to understand for beginners; quick to locate assets by type.
**Cons:** A single feature is spread across dozens of folders; makes removing or refactoring entire features difficult.

### 1.2 Feature-Based Organization

Groups all assets related to a specific feature. This is the recommended approach for medium and large projects.

```
Assets/
├── _Project/
│   ├── Features/
│   │   ├── Player/
│   │   │   ├── Scripts/
│   │   │   ├── Prefabs/
│   │   │   ├── Animations/
│   │   │   └── Materials/
│   │   ├── Enemies/
│   │   │   ├── Scripts/
│   │   │   ├── Prefabs/
│   │   │   └── AI/
│   │   └── UI/
│   │       ├── HUD/
│   │       ├── Menus/
│   │       └── Shared/
│   ├── Core/
│   │   ├── Scripts/
│   │   ├── ScriptableObjects/
│   │   └── Events/
│   └── Infrastructure/
│       ├── SceneManagement/
│       ├── SaveSystem/
│       └── Networking/
├── _ThirdParty/
│   ├── DOTween/
│   └── TextMeshPro/
├── Art/
│   ├── Characters/
│   ├── Environment/
│   └── VFX/
├── Audio/
│   ├── Music/
│   └── SFX/
├── Scenes/
│   ├── Boot.unity
│   ├── MainMenu.unity
│   └── Levels/
└── Resources/
```

**Pros:** High cohesion; easy to delete or move an entire feature; each developer works in a folder without conflicts.
**Cons:** Shared assets (shaders, fonts) need a `Shared` or `Core` folder.

### 1.3 Practical Recommendation

Use the `_` (underscore) prefix on the main project folder (`_Project`) so it appears at the top of the Project Browser. Keep third-party assets in `_ThirdParty` without altering their original structure — copy and modify only inside `_Project`.

---

## 2. Namespaces and Naming Conventions

### 2.1 Namespaces

Namespaces prevent name collisions and create logical boundaries in the code. Use the `CompanyName.ProjectName.Module` structure:

```csharp
namespace MyCompany.MyGame.Core
{
    public class GameManager : MonoBehaviour { }
}

namespace MyCompany.MyGame.Gameplay.Player
{
    public class PlayerController : MonoBehaviour { }
}

namespace MyCompany.MyGame.UI
{
    public class HUDController : MonoBehaviour { }
}
```

Each feature folder should have its own corresponding namespace. This makes finding files and understanding dependencies easier.

### 2.2 Naming Conventions — C# Code

| Element              | Convention    | Example                        |
|----------------------|---------------|--------------------------------|
| Classes              | PascalCase    | `PlayerController`             |
| Interfaces           | IPascalCase   | `IDamageable`                  |
| Methods              | PascalCase    | `TakeDamage()`, `GetHealth()`  |
| Properties           | PascalCase    | `MaxHealth`                    |
| Private fields       | _camelCase    | `_currentHealth`               |
| Local variables      | camelCase     | `targetPosition`               |
| Constants            | UPPER_SNAKE   | `MAX_PLAYER_COUNT`             |
| Enums                | PascalCase    | `GameState.Playing`            |
| Parameters           | camelCase     | `float damageAmount`           |
| Booleans             | Question form | `IsAlive`, `HasKey`, `CanJump` |

**General rules:**

- Methods use **verbs**: `GetDirection()`, `FindTarget()`, `ApplyDamage()`.
- Booleans ask **questions**: `IsGameOver`, `HasStartedTurn`.
- Avoid obscure abbreviations: `HealthBar` instead of `HB`.
- Never use spaces, hyphens, or special characters in file and folder names.

### 2.3 Naming Conventions — Assets

| Asset Type       | Convention             | Example                          |
|------------------|------------------------|----------------------------------|
| Prefabs          | PascalCase             | `EnemyGoblin.prefab`            |
| Materials        | PascalCase + suffix    | `DarkVampire_Diffuse.mat`       |
| Textures         | Name_Type              | `DarkVampire_Normalmap.png`     |
| Animations       | CharacterName@Action   | `Player@Idle.anim`              |
| ScriptableObjects| PascalCase + context   | `SwordConfig.asset`             |
| Scenes           | PascalCase             | `Level01_Forest.unity`          |
| Sequential items | Name + number (0-based)| `PathNode0`, `PathNode1`        |

Use underscore `_` to separate the base name from descriptive aspects: `EnterButton_Active`, `EnterButton_Inactive`.

---

## 3. Assembly Definitions: When and Why to Use Them

### 3.1 The Problem

By default, Unity compiles **all** of your C# scripts into a single monolithic assembly (`Assembly-CSharp.dll`). Any change to any script forces the recompilation of **everything**. On large projects, this can mean waiting 30 seconds to several minutes per change.

### 3.2 The Solution: .asmdef

Assembly Definition Files (`.asmdef`) allow you to split code into smaller, independent assemblies. When you change a script, Unity recompiles **only** the affected assembly and those that depend on it.

```
_Project/
├── Core/
│   └── MyCompany.MyGame.Core.asmdef
├── Features/
│   ├── Player/
│   │   └── MyCompany.MyGame.Player.asmdef
│   ├── Enemies/
│   │   └── MyCompany.MyGame.Enemies.asmdef
│   └── UI/
│       └── MyCompany.MyGame.UI.asmdef
├── Infrastructure/
│   └── MyCompany.MyGame.Infrastructure.asmdef
└── Tests/
    ├── MyCompany.MyGame.Tests.EditMode.asmdef
    └── MyCompany.MyGame.Tests.PlayMode.asmdef
```

### 3.3 Benefits

- **Drastically reduced compile times** — from minutes to seconds on large projects.
- **Forced encapsulation** — one assembly can only access another if there is an explicit reference, preventing accidental dependencies.
- **Modularity** — makes it easier to reuse modules across projects.
- **Isolated tests** — test assemblies reference only what they need to test.

### 3.4 When to Use

| Situation                           | Recommendation        |
|-------------------------------------|-----------------------|
| Small prototype (< 50 scripts)      | Optional              |
| Medium project (50–200 scripts)     | Recommended           |
| Large project (200+ scripts)        | **Required**          |
| Library/plugin code                 | **Required**          |
| Unit tests (Edit/Play Mode)         | **Required**          |

### 3.5 Example Dependency Graph

```
Infrastructure  ←──  Core  ←──  Gameplay  ←──  UI
                       ↑            ↑
                     Tests      Tests
```

`Core` does not reference any other project assembly. `Gameplay` references `Core`. `UI` references `Core` and `Gameplay`. This creates a unidirectional dependency flow.

**Tip:** Mark assemblies that contain only pure logic (no `UnityEngine`) with "No Engine References" for near-instant compilation.

---

## 4. Separation of Concerns: Core, Gameplay, UI, Infrastructure

A well-layered architecture makes maintenance, testing, and scalability easier.

### 4.1 The Four Layers

```
┌──────────────────────────────────────┐
│               UI Layer               │  ← Presentation (HUD, menus, visual feedback)
├──────────────────────────────────────┤
│           Gameplay Layer             │  ← Game rules, mechanics, AI, input
├──────────────────────────────────────┤
│            Core Layer                │  ← Shared systems, events, data
├──────────────────────────────────────┤
│        Infrastructure Layer          │  ← Persistence, networking, analytics, platform
└──────────────────────────────────────┘
```

### 4.2 Responsibilities of Each Layer

**Core** — The heart of the project, with no dependencies on other game layers:

```csharp
namespace MyCompany.MyGame.Core
{
    // Decoupled event system via ScriptableObject
    [CreateAssetMenu(menuName = "Events/Game Event")]
    public class GameEvent : ScriptableObject
    {
        private readonly List<IGameEventListener> _listeners = new();

        public void Raise()
        {
            for (int i = _listeners.Count - 1; i >= 0; i--)
                _listeners[i].OnEventRaised();
        }

        public void Register(IGameEventListener listener) => _listeners.Add(listener);
        public void Unregister(IGameEventListener listener) => _listeners.Remove(listener);
    }

    public interface IGameEventListener
    {
        void OnEventRaised();
    }
}
```

**Gameplay** — Mechanics that depend on Core but have no knowledge of the UI:

```csharp
namespace MyCompany.MyGame.Gameplay.Player
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private GameEvent _onPlayerDamaged;
        [SerializeField] private GameEvent _onPlayerDied;

        private int _currentHealth;

        private void Awake() => _currentHealth = _maxHealth;

        public void TakeDamage(int amount)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - amount);
            _onPlayerDamaged?.Raise();

            if (_currentHealth <= 0)
                _onPlayerDied?.Raise();
        }

        // Property for UI to observe (no direct UI reference)
        public float HealthPercent => (float)_currentHealth / _maxHealth;
    }
}
```

**UI** — Observes Gameplay data via events, never modifies logic directly:

```csharp
namespace MyCompany.MyGame.UI
{
    public class HealthBarUI : MonoBehaviour, IGameEventListener
    {
        [SerializeField] private PlayerHealth _playerHealth;
        [SerializeField] private GameEvent _onPlayerDamaged;
        [SerializeField] private Image _fillImage;

        private void OnEnable() => _onPlayerDamaged.Register(this);
        private void OnDisable() => _onPlayerDamaged.Unregister(this);

        public void OnEventRaised()
        {
            _fillImage.fillAmount = _playerHealth.HealthPercent;
        }
    }
}
```

**Infrastructure** — Low-level services accessed by Core via interfaces:

```csharp
namespace MyCompany.MyGame.Infrastructure
{
    public interface ISaveService
    {
        void Save<T>(string key, T data);
        T Load<T>(string key);
    }

    public class JsonSaveService : ISaveService
    {
        public void Save<T>(string key, T data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
        }

        public T Load<T>(string key)
        {
            string json = PlayerPrefs.GetString(key, "{}");
            return JsonUtility.FromJson<T>(json);
        }
    }
}
```

---

## 5. Scene Management

### 5.1 Bootstrapper Pattern

The Bootstrapper Pattern ensures the game always starts from a consistent state, regardless of which scene the developer has open in the Editor.

```csharp
namespace MyCompany.MyGame.Infrastructure
{
    /// <summary>
    /// The "Boot" scene is Scene 0 in Build Settings.
    /// Initializes all systems and loads the first real scene.
    /// </summary>
    public class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private string _firstSceneName = "MainMenu";

        private async void Start()
        {
            // 1. Initialize essential services
            ServiceLocator.Register<ISaveService>(new JsonSaveService());
            ServiceLocator.Register<IAudioService>(new AudioService());

            // 2. Load the persistent scene (managers that survive between scenes)
            await SceneManager.LoadSceneAsync("PersistentManagers", LoadSceneMode.Additive);

            // 3. Load the first game scene
            await SceneManager.LoadSceneAsync(_firstSceneName, LoadSceneMode.Additive);

            // 4. Unload the boot scene
            await SceneManager.UnloadSceneAsync("Boot");
        }
    }
}
```

### 5.2 Additive Scenes

Instead of loading a monolithic scene, divide the game into composite scenes loaded simultaneously:

```
Scenes active at the same time:
┌─────────────────────┐
│ PersistentManagers   │  ← Always loaded (GameManager, AudioManager)
├─────────────────────┤
│ UI_HUD               │  ← Player interface
├─────────────────────┤
│ Level_Forest_01      │  ← Current level content
├─────────────────────┤
│ Level_Forest_Lighting│  ← Lightmaps and lighting configuration
└─────────────────────┘
```

**Level loading example:**

```csharp
namespace MyCompany.MyGame.Infrastructure
{
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField] private GameEvent _onSceneLoadStarted;
        [SerializeField] private GameEvent _onSceneLoadCompleted;

        private string _currentLevelScene;

        public async void LoadLevel(string sceneName)
        {
            _onSceneLoadStarted?.Raise();

            // Unload the previous level (if any)
            if (!string.IsNullOrEmpty(_currentLevelScene))
                await SceneManager.UnloadSceneAsync(_currentLevelScene);

            // Load the new level additively
            await SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            _currentLevelScene = sceneName;

            // Set the active scene (for lightmaps and instantiated objects)
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(sceneName));

            _onSceneLoadCompleted?.Raise();
        }
    }
}
```

### 5.3 Scene Management Best Practices

- **Always load by name (string), not by index** — it is more readable and resistant to reordering in Build Settings.
- **Use a "Boot" scene** as Scene 0 — ensures consistent initialization.
- **PersistentManagers scene** — loaded additively on boot and never unloaded; contains essential singletons.
- **Avoid excessive DontDestroyOnLoad** — prefer keeping persistent objects in a dedicated additive scene.
- **Editor testing** — create an `[InitializeOnLoad]` script that automatically loads the Boot scene when clicking Play, regardless of which scene is open.

---

## 6. Architectural Patterns: MVC, MVP, and MVVM

### 6.1 MVC (Model-View-Controller)

The Model stores data and business logic, the View displays information on screen, and the Controller receives input and coordinates the Model and View.

```
┌────────┐  input   ┌────────────┐  updates  ┌───────┐
│  View  │ ───────→ │ Controller │ ─────────→ │ Model │
│        │ ←─────── │            │ ←───────── │       │
└────────┘ updates  └────────────┘  notifies  └───────┘
```

```csharp
// Model — pure data, no reference to Unity UI
public class PlayerModel
{
    public int Health { get; private set; }
    public event Action OnHealthChanged;

    public void TakeDamage(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
        OnHealthChanged?.Invoke();
    }
}

// View — only displays, contains no logic
public class PlayerView : MonoBehaviour
{
    [SerializeField] private Slider _healthSlider;
    public void UpdateHealth(float percent) => _healthSlider.value = percent;
}

// Controller — coordinates Model and View
public class PlayerController : MonoBehaviour
{
    private PlayerModel _model;
    [SerializeField] private PlayerView _view;

    private void Start()
    {
        _model = new PlayerModel();
        _model.OnHealthChanged += () => _view.UpdateHealth(_model.Health / 100f);
    }
}
```

| Pros | Cons |
|------|---------|
| Simple to understand | View and Model can become coupled in Unity projects |
| Good initial separation | Controller tends to become a "God Class" |
| Works well for small projects | Less testable than MVP/MVVM |

### 6.2 MVP (Model-View-Presenter)

An evolution of MVC where **all** communication between Model and View passes through the Presenter. The View is passive — it only implements an interface.

```
┌────────┐  events   ┌───────────┐  updates  ┌───────┐
│  View  │ ────────→ │ Presenter │ ─────────→ │ Model │
│ (IView)│ ←──────── │           │ ←───────── │       │
└────────┘ commands  └───────────┘   data     └───────┘
```

```csharp
// View interface — enables mocking in tests
public interface IPlayerView
{
    void UpdateHealthBar(float percent);
    void ShowDeathScreen();
    event Action OnRestartClicked;
}

// Presenter — contains all presentation logic
public class PlayerPresenter
{
    private readonly PlayerModel _model;
    private readonly IPlayerView _view;

    public PlayerPresenter(PlayerModel model, IPlayerView view)
    {
        _model = model;
        _view = view;

        _model.OnHealthChanged += HandleHealthChanged;
        _view.OnRestartClicked += HandleRestart;
    }

    private void HandleHealthChanged()
    {
        float percent = (float)_model.Health / _model.MaxHealth;
        _view.UpdateHealthBar(percent);

        if (_model.Health <= 0)
            _view.ShowDeathScreen();
    }

    private void HandleRestart() => _model.ResetHealth();
}
```

| Pros | Cons |
|------|---------|
| View is fully passive and testable | More boilerplate than MVC |
| Presenter is testable without Unity (mocks) | One interface per View can be verbose |
| Excellent separation of concerns | Steeper learning curve |
| Preferred for professional Unity projects | — |

### 6.3 MVVM (Model-View-ViewModel)

Similar to MVP, but uses **data binding** — the View observes reactive properties of the ViewModel and updates automatically.

```
┌────────┐  binding  ┌────────────┐  updates  ┌───────┐
│  View  │ ←───────→ │  ViewModel │ ─────────→ │ Model │
│        │           │ (ReactiveP)│ ←───────── │       │
└────────┘           └────────────┘             └───────┘
```

```csharp
// Reactive property utility class
public class ReactiveProperty<T>
{
    private T _value;
    public event Action<T> OnChanged;

    public T Value
    {
        get => _value;
        set
        {
            if (!EqualityComparer<T>.Default.Equals(_value, value))
            {
                _value = value;
                OnChanged?.Invoke(_value);
            }
        }
    }
}

// ViewModel — exposes reactive data, has no knowledge of the View
public class PlayerViewModel
{
    public ReactiveProperty<float> HealthPercent { get; } = new();
    public ReactiveProperty<bool> IsDead { get; } = new();

    private readonly PlayerModel _model;

    public PlayerViewModel(PlayerModel model)
    {
        _model = model;
        _model.OnHealthChanged += UpdateProperties;
    }

    private void UpdateProperties()
    {
        HealthPercent.Value = (float)_model.Health / _model.MaxHealth;
        IsDead.Value = _model.Health <= 0;
    }
}

// View — binds to reactive properties
public class PlayerViewMVVM : MonoBehaviour
{
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private GameObject _deathScreen;

    public void Bind(PlayerViewModel vm)
    {
        vm.HealthPercent.OnChanged += v => _healthSlider.value = v;
        vm.IsDead.OnChanged += v => _deathScreen.SetActive(v);
    }
}
```

| Pros | Cons |
|------|---------|
| Binding reduces update boilerplate | Unity has no native data binding (must be implemented) |
| View is completely decoupled | More complex for junior teams |
| Excellent for UI-heavy games | Overhead of reactive properties |
| ViewModel reuse across platforms | Debugging bindings can be difficult |

### 6.4 Which to Choose?

| Scenario | Recommended Pattern |
|---------|-------------------|
| Prototype / Game Jam | None (KISS) or lightweight MVC |
| Small project (1–3 devs) | MVC |
| Medium project with moderate UI | MVP |
| Large project, complex UI | MVVM |
| Multiplayer / reactive data | MVVM with UniRx |

---

## 7. From Prototype to Production: How to Scale

### 7.1 Phase 1 — Prototype (1–2 weeks)

- **Goal:** Validate the idea quickly.
- Everything in `Assets/Prototype/` — throwaway code.
- MonoBehaviours doing everything: input, logic, visuals.
- No assembly definitions, no complex namespaces.
- **Rule:** Nothing from here goes to production. It is disposable.

### 7.2 Phase 2 — Vertical Slice (1–3 months)

- Create the definitive folder structure (`_Project/`, layers).
- Separate data from behavior using **ScriptableObjects**:

```csharp
[CreateAssetMenu(menuName = "Config/Enemy Config")]
public class EnemyConfig : ScriptableObject
{
    public string EnemyName;
    public int MaxHealth;
    public float MoveSpeed;
    public float AttackRange;
}

public class Enemy : MonoBehaviour
{
    [SerializeField] private EnemyConfig _config;

    // Designers edit the ScriptableObject, not the code
    private void Start() => GetComponent<Health>().SetMax(_config.MaxHealth);
}
```

- Introduce assembly definitions for `Core`, `Gameplay`, `UI`.
- Define code conventions and document them in a `CODING_STANDARDS.md`.
- Implement the Bootstrapper Pattern.

### 7.3 Phase 3 — Production

- Complete assembly definitions with a clear dependency graph.
- Unit and integration tests (Edit Mode + Play Mode tests).
- Object Pooling for everything instantiated/destroyed frequently.
- Addressables for asset management (instead of `Resources/`).
- CI/CD with Unity Build Automation or a custom pipeline.
- Regular profiling (CPU, GPU, memory) as part of the workflow.

### 7.4 Scalability Checklist

```
□ Feature-based folder structure
□ Namespaces aligned with folder structure
□ Assembly definitions with unidirectional dependencies
□ ScriptableObjects for configuration data
□ Decoupled events (ScriptableObject Events or similar)
□ Interfaces for services (save, audio, analytics)
□ Bootstrapper scene as entry point
□ Additive scenes for level composition
□ Object Pooling implemented
□ Automated tests (minimum: core + gameplay)
```

---

## 8. Common Architecture Mistakes

### 8.1 God MonoBehaviours

The most frequent mistake: a `GameManager.cs` with 2000+ lines that controls everything — input, UI, enemy spawning, saving, audio. **Solution:** Split into classes with single responsibility. A "manager" should coordinate, not implement.

### 8.2 Singleton Abuse

`Singleton<T>` for everything (AudioManager, UIManager, GameManager...) creates hidden dependencies and makes testing difficult. **Solution:** Use a Service Locator or Dependency Injection (such as VContainer or Zenject) to manage services.

### 8.3 Excessive Use of Update()

Putting heavy logic in the `Update()` of dozens of objects kills performance. **Solution:** Use events, coroutines, or a centralized tick system. Reserve `Update()` for what truly needs to run every frame.

### 8.4 Direct References Between Systems

`PlayerHealth` directly calling `UIManager.Instance.UpdateHealthBar()` creates tight coupling. **Solution:** Use events (C# events, ScriptableObject events, or a message bus).

### 8.5 Ignoring Object Pooling

Frequent `Instantiate()` and `Destroy()` calls cause Garbage Collection spikes and stuttering. **Solution:** Implement pooling for projectiles, effects, enemies — anything created and destroyed repeatedly.

### 8.6 Hardcoded Data

Magic values scattered throughout the code (`if (health < 30)`, `moveSpeed = 5f`). **Solution:** Extract to ScriptableObjects or configuration files editable by designers.

### 8.7 Monolithic Single Scene

Everything in one scene: UI, level, managers, camera. Constant merge conflicts in a team. **Solution:** Additive scenes — one scene per responsibility.

### 8.8 Classes as Behaviors, Not Concepts

Naming classes like `MoveToPlayer` or `ShootBullet` frames the class as a method. In production, classes should represent **concepts**: `MovementSystem`, `WeaponSystem`, `ProjectileFactory`. This makes scaling and adding variations easier.

---

## 9. Complete Folder Tree — Reference Project

```
Assets/
├── _Project/
│   ├── Core/
│   │   ├── Events/
│   │   │   ├── GameEvent.cs
│   │   │   ├── GameEventListener.cs
│   │   │   └── Events/                    ← ScriptableObject assets (.asset)
│   │   ├── ServiceLocator/
│   │   │   └── ServiceLocator.cs
│   │   ├── Utilities/
│   │   │   ├── Singleton.cs
│   │   │   ├── ObjectPool.cs
│   │   │   └── Extensions/
│   │   ├── Data/
│   │   │   └── SharedDataStructures.cs
│   │   └── MyCompany.MyGame.Core.asmdef
│   │
│   ├── Gameplay/
│   │   ├── Player/
│   │   │   ├── Scripts/
│   │   │   │   ├── PlayerController.cs
│   │   │   │   ├── PlayerHealth.cs
│   │   │   │   └── PlayerInput.cs
│   │   │   ├── Prefabs/
│   │   │   ├── Animations/
│   │   │   └── Config/                    ← Config ScriptableObjects
│   │   ├── Enemies/
│   │   │   ├── Scripts/
│   │   │   ├── Prefabs/
│   │   │   ├── AI/
│   │   │   └── Config/
│   │   ├── Combat/
│   │   │   ├── Scripts/
│   │   │   └── Config/
│   │   └── MyCompany.MyGame.Gameplay.asmdef
│   │
│   ├── UI/
│   │   ├── HUD/
│   │   │   ├── Scripts/
│   │   │   └── Prefabs/
│   │   ├── Menus/
│   │   │   ├── MainMenu/
│   │   │   ├── PauseMenu/
│   │   │   └── Settings/
│   │   ├── Shared/
│   │   │   ├── Widgets/
│   │   │   └── Styles/
│   │   └── MyCompany.MyGame.UI.asmdef
│   │
│   ├── Infrastructure/
│   │   ├── SceneManagement/
│   │   │   ├── Bootstrapper.cs
│   │   │   └── SceneLoader.cs
│   │   ├── SaveSystem/
│   │   ├── Audio/
│   │   ├── Analytics/
│   │   ├── Networking/
│   │   └── MyCompany.MyGame.Infrastructure.asmdef
│   │
│   └── Tests/
│       ├── EditMode/
│       │   └── MyCompany.MyGame.Tests.EditMode.asmdef
│       └── PlayMode/
│           └── MyCompany.MyGame.Tests.PlayMode.asmdef
│
├── Art/
│   ├── Characters/
│   │   ├── Player/
│   │   └── Enemies/
│   ├── Environment/
│   │   ├── Terrain/
│   │   ├── Props/
│   │   └── Structures/
│   ├── VFX/
│   ├── Shaders/
│   └── Fonts/
│
├── Audio/
│   ├── Music/
│   ├── SFX/
│   └── Mixers/
│
├── Scenes/
│   ├── Boot.unity
│   ├── PersistentManagers.unity
│   ├── MainMenu.unity
│   └── Levels/
│       ├── Level01_Forest.unity
│       └── Level02_Cave.unity
│
├── _ThirdParty/
│   ├── DOTween/
│   ├── TextMeshPro/
│   └── VContainer/
│
├── Plugins/                               ← Native plugins
├── StreamingAssets/
├── Editor/
│   └── Tools/
└── Resources/                             ← Use sparingly; prefer Addressables
```

---

## Sources and References

- [Best practices for organizing your Unity project — Unity](https://unity.com/how-to/organizing-your-project)
- [How to architect code as your project scales — Unity](https://unity.com/how-to/how-architect-code-your-project-scales)
- [Build a modular codebase with MVC and MVP — Unity Learn](https://learn.unity.com/tutorial/build-a-modular-codebase-with-mvc-and-mvp-programming-patterns)
- [Model-View-ViewModel pattern — Unity Learn](https://learn.unity.com/tutorial/model-view-viewmodel-pattern)
- [Naming and code style tips for C# scripting — Unity](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity)
- [Unity Project Style Guide — GitHub (Tim D. Hoffmann)](https://github.com/timdhoffmann/unity-project-style-guide)
- [Unity Style Guide — GitHub (Justin Wasilenko)](https://github.com/justinwasilenko/Unity-Style-Guide)
- [A guide to folder structures for Unity 6 projects — Anchorpoint](https://www.anchorpoint.app/blog/unity-folder-structure)
- [Structuring Your Unity Code For Production — Codementor](https://www.codementor.io/@mody/structuring-your-unity-code-for-production-important-best-practices-25bmix6f3q)
- [Organizing architecture for games on Unity — DEV Community](https://dev.to/devsdaddy/organizing-architecture-for-games-on-unity-laying-out-the-important-things-that-matter-4d4p)
- [A better architecture for Unity projects — Game Developer](https://www.gamedeveloper.com/business/a-better-architecture-for-unity-projects)
- [Scene Bootstrapper Architecture — Unity Discussions](https://discussions.unity.com/t/scene-bootstrapper-architecture/831630)
- [Getting started with assembly definitions — Embrace.io](https://embrace.io/blog/getting-started-with-assembly-definitions-in-unity/)
- [Slash Your Unity Compile Times: Assembly Definition Guide — Answerpoint](https://answerpoint.blog/unity-compile-times-assembly-definition-guide)
- [Mastering Unity Game Development: From Prototype to Production — Nerd Level Tech](https://nerdleveltech.com/mastering-unity-game-development-from-prototype-to-production)
