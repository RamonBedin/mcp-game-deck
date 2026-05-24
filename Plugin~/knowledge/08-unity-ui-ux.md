# 08 — UI/UX in Unity: Best Practices (2025-2026)

> Comprehensive guide covering UI Toolkit, uGUI (Canvas), architecture patterns, performance for mass entities, responsive mobile design, accessibility, and TextMeshPro.

---

## 1. UI Toolkit vs uGUI (Canvas)

### 1.1 Architectural Overview

| Aspect | uGUI (Canvas) | UI Toolkit |
|---|---|---|
| **Released** | 2014 | 2019 (runtime mature in Unity 6) |
| **Model** | GameObject + Components | Visual Tree + UXML/USS (inspired by HTML/CSS) |
| **Rendering** | Canvas batching per GameObject | Retained-mode mesh generation |
| **Animation** | Animator, DOTween, Timeline | USS Transitions, C# manipulation |
| **Data Binding** | Manual or via frameworks | Native (Unity 6+) with runtime binding system |
| **Editor UI** | Not recommended | Standard since Unity 2019 |
| **Runtime Game UI** | Fully supported and mature | Supported (Unity 6+), but with gaps |

### 1.2 When to Use Each (2025-2026 State)

**Use uGUI when:**

- The project already uses uGUI extensively (partial migration is risky)
- You need complex animations via Animator/Timeline (blend trees, state machines)
- You use world-space UI extensively (3D health bars, in-world tooltips)
- You depend on Asset Store assets that only support uGUI
- The team has consolidated experience in uGUI

**Use UI Toolkit when:**

- Data-driven interfaces: inventories, skill trees, quest logs, leaderboards, chat
- Complex menus with many elements (settings, shop, character customization)
- You want MVVM pattern with native data binding
- New projects that want to align with Unity's future direction
- UI that benefits from the flexbox-like model for responsive layout

**Hybrid approach (recommended for many projects):**

```
Gameplay HUD    →  uGUI         (animations, world-space, maturity)
Menus/Settings  →  UI Toolkit   (data binding, flexible layout)
Editor tools    →  UI Toolkit   (official standard)
```

### 1.3 Performance Comparison

| Metric | uGUI | UI Toolkit |
|---|---|---|
| **Cost per element** | High (1 GameObject + Transform + RectTransform per element) | Low (lightweight VisualElement, no GameObject overhead) |
| **Batching** | Canvas-level, breaks with material/texture mixing | Retained-mode, fewer draw calls for static UIs |
| **Rebuild cost** | Rebuilds the entire Canvas when any child changes (mitigable with sub-canvases) | Dirty-rect tracking, only changed elements are recalculated |
| **Layout** | LayoutGroup is expensive and recalculates every frame when dirty | Yogacore (flexbox engine), more efficient for complex layouts |
| **Memory** | Larger (GameObjects + Components) | Smaller footprint per element |

> **Note:** UI Toolkit does not yet support all uGUI scenarios, such as custom meshes in world-space and custom shaders easily applicable per element. Check the [official comparison table](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html) for the most current state.

---

## 2. uGUI Performance: Canvas Optimization

### 2.1 Canvas Splitting (Static vs Dynamic)

The Canvas performs a **rebatch** (recombines geometry into batches and generates render commands) every time **any** drawable child changes. This is the biggest performance bottleneck in uGUI.

**Golden rule:** Separate static and dynamic elements into different Canvases.

```
UIRoot (Empty GameObject)
├── Canvas_Static          ← Backgrounds, fixed labels, frames
│   ├── Background
│   ├── Logo
│   └── StaticLabels
├── Canvas_Dynamic         ← HP bar, score, timers
│   ├── HealthBar
│   ├── ScoreText
│   └── Timer
└── Canvas_Overlay         ← Popups, tooltips (renders on top)
    ├── TooltipPanel
    └── ModalDialog
```

**Sub-Canvas for isolation:**

```csharp
// Sub-Canvas isolates rebuilds: a dirty child does NOT force the parent to rebuild
// Add a Canvas component to a child of the main Canvas
// The sub-canvas inherits sorting from the parent, but rebuilds independently

[RequireComponent(typeof(Canvas))]
public class SubCanvasSetup : MonoBehaviour
{
    void Awake()
    {
        // Sub-canvas does not need a GraphicRaycaster
        // unless it has interactive elements
        var canvas = GetComponent<Canvas>();
        canvas.overrideSorting = true; // if you need to control draw order
    }
}
```

**Important trade-off:** Each Canvas generates at least 1 draw call. More Canvases = more draw calls, but fewer rebuilds. Find the balance using the **Unity Profiler** (UI module) and **Frame Debugger**.

### 2.2 Rebuild vs Rebatch — Understanding the Cycle

```
Dirty Element marked
       │
       ▼
┌─────────────────┐
│  Layout Rebuild  │ ← Recalculates RectTransform (position, size)
│  (CanvasUpdate-  │   Expensive with nested LayoutGroups
│   Handler)       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Graphic Rebuild  │ ← Regenerates vertex data (mesh, colors, UVs)
│ (Graphic.       │   Triggered by: SetVerticesDirty(), color change,
│  UpdateGeometry) │   sprite swap, text change
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Canvas Rebatch │ ← Re-sorts and re-batches all geometries
│  (Canvas.Build-  │   in the Canvas. Runs on native thread.
│   Batch)         │   Cost proportional to number of elements.
└─────────────────┘
```

**Rebuild optimization tips:**

```csharp
// ❌ BAD: Setting text every frame even without a change
void Update()
{
    scoreText.text = $"Score: {score}"; // Marks dirty EVERY frame!
}

// ✅ GOOD: Only set when the value changes
private int _lastScore = -1;
void Update()
{
    if (score != _lastScore)
    {
        _lastScore = score;
        scoreText.text = $"Score: {score}";
    }
}
```

```csharp
// ❌ BAD: Enabling/disabling the entire GameObject (causes rebuild + re-layout)
panel.SetActive(false);

// ✅ BETTER: Use CanvasGroup.alpha = 0 + CanvasGroup.blocksRaycasts = false
// Does not remove from Canvas batch, but avoids a full rebuild
canvasGroup.alpha = 0f;
canvasGroup.blocksRaycasts = false;
canvasGroup.interactable = false;
```

### 2.3 Layout Group Overhead and Alternatives

**Problem:** `HorizontalLayoutGroup`, `VerticalLayoutGroup`, and `GridLayoutGroup` force a layout recalculation across the entire hierarchy when any child changes. In nested hierarchies, the cost is **O(n²)** in the worst case.

**Alternatives:**

```csharp
// 1. MANUAL ANCHORS — For static layouts, use RectTransform anchors
//    Configure in the Editor and avoid LayoutGroups entirely

// 2. LAYOUT ONLY ON INITIALIZATION
// If the layout does not change at runtime, calculate once and disable
public class OneTimeLayout : MonoBehaviour
{
    void Start()
    {
        // Force an immediate layout rebuild
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

        // Disable the LayoutGroup after calculation
        var layoutGroup = GetComponent<LayoutGroup>();
        if (layoutGroup != null)
            layoutGroup.enabled = false;
    }
}

// 3. CONTENT SIZE FITTER — If you only need auto-sizing,
//    use ContentSizeFitter without a LayoutGroup

// 4. CUSTOM LAYOUT — For complex cases, implement ILayoutGroup
//    with manual caching and dirty-flag
```

```csharp
// 5. For large lists: VIRTUALIZED SCROLL
// Recycle visible elements instead of creating hundreds of children
public class VirtualizedScrollView : MonoBehaviour
{
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private RectTransform _itemPrefab;
    [SerializeField] private int _visibleCount = 10;

    private readonly List<RectTransform> _pool = new();
    private int _totalItems;
    private float _itemHeight;

    public void SetItemCount(int count)
    {
        _totalItems = count;
        _itemHeight = _itemPrefab.rect.height;

        // Configure total content size (fake scroll)
        var content = _scrollRect.content;
        content.sizeDelta = new Vector2(content.sizeDelta.x, _itemHeight * count);

        // Create pool of visible elements + buffer
        int poolSize = _visibleCount + 2;
        for (int i = _pool.Count; i < poolSize; i++)
        {
            var item = Instantiate(_itemPrefab, content);
            _pool.Add(item);
        }
    }

    // OnScroll: reposition and update only the visible pool items
}
```

### 2.4 Raycast Target Optimization

**Every `Graphic` (Image, Text, RawImage) has `raycastTarget = true` by default.** This means the `GraphicRaycaster` iterates over ALL these elements on every input event.

```csharp
// ❌ Default: hundreds of elements with raycast enabled unnecessarily

// ✅ Disable raycastTarget on everything that is not interactive:
// - Decorative backgrounds
// - Informational labels/text
// - Non-clickable icons
// - Separators, borders

// In the Inspector: uncheck "Raycast Target" on each Image/Text
// Or via code:
GetComponent<Image>().raycastTarget = false;
GetComponent<TMP_Text>().raycastTarget = false;
```

```csharp
// For bulk cleanup in the Editor:
#if UNITY_EDITOR
[MenuItem("Tools/Disable Non-Interactive Raycasts")]
static void DisableUnnecessaryRaycasts()
{
    var allGraphics = FindObjectsOfType<Graphic>();
    int count = 0;
    foreach (var g in allGraphics)
    {
        // Keep raycast only on Selectables (Button, Toggle, etc.)
        if (g.GetComponent<Selectable>() == null &&
            g.GetComponent<IEventSystemHandler>() == null)
        {
            g.raycastTarget = false;
            EditorUtility.SetDirty(g);
            count++;
        }
    }
    Debug.Log($"Disabled raycast on {count} non-interactive graphics");
}
#endif
```

**Additional tip:** Use `Raycast Padding` (Unity 2020.1+) to expand or shrink the hit area without changing the visual, useful for small buttons on mobile.

---

## 3. UI Patterns for Games

### 3.1 Screen Manager / UI Stack (Push/Pop Screens)

A UI Stack manages screens like a stack: push opens a new screen on top, pop closes the top screen. This handles menu navigation, modals, and the back button.

```csharp
public class UIScreen : MonoBehaviour
{
    public virtual void OnScreenPushed() { gameObject.SetActive(true); }
    public virtual void OnScreenPopped() { gameObject.SetActive(false); }
    public virtual void OnScreenFocused() { } // Becomes the top screen again
    public virtual void OnScreenUnfocused() { } // Another screen was pushed on top
}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private UIScreen _initialScreen;
    private readonly Stack<UIScreen> _screenStack = new();
    private readonly Dictionary<Type, UIScreen> _screenRegistry = new();

    void Awake()
    {
        Instance = this;
        // Register all child screens
        foreach (var screen in GetComponentsInChildren<UIScreen>(true))
        {
            _screenRegistry[screen.GetType()] = screen;
            screen.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (_initialScreen != null)
            Push(_initialScreen);
    }

    public void Push<T>() where T : UIScreen
    {
        if (_screenRegistry.TryGetValue(typeof(T), out var screen))
            Push(screen);
    }

    public void Push(UIScreen screen)
    {
        // Unfocus the current screen
        if (_screenStack.TryPeek(out var current))
            current.OnScreenUnfocused();

        _screenStack.Push(screen);
        screen.OnScreenPushed();
        screen.OnScreenFocused();
    }

    public void Pop()
    {
        if (_screenStack.Count <= 1) return; // Do not pop the root screen

        var popped = _screenStack.Pop();
        popped.OnScreenPopped();

        if (_screenStack.TryPeek(out var next))
            next.OnScreenFocused();
    }

    public void PopToRoot()
    {
        while (_screenStack.Count > 1)
        {
            var popped = _screenStack.Pop();
            popped.OnScreenPopped();
        }
        if (_screenStack.TryPeek(out var root))
            root.OnScreenFocused();
    }

    // Android back button support
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            Pop();
    }
}
```

### 3.2 MVP/MVVM Applied to Game UI

#### MVP (Model-View-Presenter) — Works with uGUI and UI Toolkit

```
┌─────────┐     ┌───────────┐     ┌──────────┐
│  Model   │◄────│ Presenter │────►│   View   │
│(SO/Data) │     │ (C# class)│     │(UI Elems)│
└─────────┘     └───────────┘     └──────────┘
  Pure data       UI logic          Display only
  Unaware of      Knows both        Unaware of
  the View        Model and View    the Model
```

```csharp
// --- MODEL ---
[CreateAssetMenu]
public class PlayerStats : ScriptableObject
{
    public int Health;
    public int MaxHealth;
    public int Gold;
    public event System.Action OnChanged;

    public void TakeDamage(int amount)
    {
        Health = Mathf.Max(0, Health - amount);
        OnChanged?.Invoke();
    }
}

// --- VIEW (uGUI) ---
public class PlayerHUDView : MonoBehaviour
{
    [SerializeField] private Slider _healthBar;
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private TMP_Text _goldText;

    public void SetHealth(float normalized, string text)
    {
        _healthBar.value = normalized;
        _healthText.text = text;
    }

    public void SetGold(string text) => _goldText.text = text;
}

// --- PRESENTER ---
public class PlayerHUDPresenter : MonoBehaviour
{
    [SerializeField] private PlayerStats _model;
    [SerializeField] private PlayerHUDView _view;

    void OnEnable() => _model.OnChanged += Refresh;
    void OnDisable() => _model.OnChanged -= Refresh;

    void Start() => Refresh();

    void Refresh()
    {
        float normalized = (float)_model.Health / _model.MaxHealth;
        _view.SetHealth(normalized, $"{_model.Health}/{_model.MaxHealth}");
        _view.SetGold($"{_model.Gold:N0}");
    }
}
```

#### MVVM with UI Toolkit (Unity 6+ Native Data Binding)

```csharp
// --- VIEW MODEL ---
// Unity 6 runtime binding uses [CreateProperty] or INotifyBindablePropertyChanged
[Serializable]
public class PlayerViewModel : INotifyBindablePropertyChanged
{
    public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

    private float _healthNormalized;
    [CreateProperty]
    public float HealthNormalized
    {
        get => _healthNormalized;
        set
        {
            if (Math.Abs(_healthNormalized - value) < 0.001f) return;
            _healthNormalized = value;
            Notify(nameof(HealthNormalized));
        }
    }

    private string _healthText;
    [CreateProperty]
    public string HealthText
    {
        get => _healthText;
        set
        {
            if (_healthText == value) return;
            _healthText = value;
            Notify(nameof(HealthText));
        }
    }

    void Notify(string property)
    {
        propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
    }
}

// --- BINDING SETUP (C#) ---
public class PlayerHUDBinding : MonoBehaviour
{
    [SerializeField] private UIDocument _document;
    [SerializeField] private PlayerStats _model;

    private PlayerViewModel _viewModel = new();

    void OnEnable()
    {
        var root = _document.rootVisualElement;

        // Bind ProgressBar.value ← ViewModel.HealthNormalized
        var healthBar = root.Q<ProgressBar>("health-bar");
        healthBar.SetBinding("value", new DataBinding
        {
            dataSource = _viewModel,
            dataSourcePath = new PropertyPath(nameof(PlayerViewModel.HealthNormalized))
        });

        _model.OnChanged += SyncViewModel;
        SyncViewModel();
    }

    void SyncViewModel()
    {
        _viewModel.HealthNormalized = (float)_model.Health / _model.MaxHealth;
        _viewModel.HealthText = $"{_model.Health}/{_model.MaxHealth}";
    }
}
```

### 3.3 Data Binding: Manual vs Frameworks

| Approach | Pros | Cons |
|---|---|---|
| **Manual (events + refresh)** | No dependencies, full control, easy to debug | Boilerplate, easy to forget to update |
| **Unity 6 Runtime Binding** | Native, UXML integration, good performance | UI Toolkit only, API still evolving |
| **UnityMvvmToolkit** | Works with uGUI and UI Toolkit, converter syntax | External dependency |
| **UniRx/R3 + ReactiveProperty** | Powerful, composable, automatic cancellation | Learning curve, overhead if misused |

**Recommendation:** For new projects with UI Toolkit, use native data binding. For uGUI, manual MVP with events is the most pragmatic approach. UniRx/R3 is excellent if the team has already adopted reactive programming.

### 3.4 UI Events via ScriptableObject Channels

ScriptableObject-based event channels decouple emitters from listeners without singletons or direct references.

```csharp
// --- Generic channel ---
public abstract class GameEventChannel<T> : ScriptableObject
{
    private readonly List<System.Action<T>> _listeners = new();

    public void Raise(T value)
    {
        // Iterate in reverse to allow unsubscribe during callback
        for (int i = _listeners.Count - 1; i >= 0; i--)
            _listeners[i]?.Invoke(value);
    }

    public void Subscribe(System.Action<T> listener) => _listeners.Add(listener);
    public void Unsubscribe(System.Action<T> listener) => _listeners.Remove(listener);
}

// --- Concrete channels ---
[CreateAssetMenu(menuName = "Events/Int Channel")]
public class IntEventChannel : GameEventChannel<int> { }

[CreateAssetMenu(menuName = "Events/Void Channel")]
public class VoidEventChannel : ScriptableObject
{
    private readonly List<System.Action> _listeners = new();
    public void Raise() { for (int i = _listeners.Count - 1; i >= 0; i--) _listeners[i]?.Invoke(); }
    public void Subscribe(System.Action l) => _listeners.Add(l);
    public void Unsubscribe(System.Action l) => _listeners.Remove(l);
}

// --- Usage: OnPlayerDamaged channel ---
// In the Inspector, drag the same SO asset to both the emitter and the listener

// Emitter (gameplay):
[SerializeField] private IntEventChannel onPlayerDamaged;
public void TakeDamage(int dmg) => onPlayerDamaged.Raise(dmg);

// Listener (UI):
[SerializeField] private IntEventChannel onPlayerDamaged;
void OnEnable() => onPlayerDamaged.Subscribe(OnDamage);
void OnDisable() => onPlayerDamaged.Unsubscribe(OnDamage);
void OnDamage(int dmg) => PlayDamageFlash();
```

---

## 4. Genre-Specific UI (Mass Entities)

### 4.1 Health Bars at Scale

For games with dozens or hundreds of entities with health bars (RTS, ARPG, tower defense), the approach matters dramatically.

#### Approach Comparison

| Approach | Draw Calls | CPU Cost | GPU Cost | Flexibility |
|---|---|---|---|---|
| **World-Space Canvas (1 per entity)** | 1 per bar | Very high (rebuild per Canvas) | Low | High (full uGUI) |
| **Screen-Space + manual positioning** | Batched (few) | Medium (camera.WorldToScreenPoint) | Low | High |
| **Shader-Based (GPU Instancing)** | **1 total** | **Minimal** | Low | Limited |
| **SpriteRenderer with MaterialPropertyBlock** | Batchable | Low | Low | Medium |

#### Shader-Based Health Bars (Recommended for 50+ entities)

```hlsl
// HealthBarShader.shader — Renders health bar via shader
// Uses _Fill passed via MaterialPropertyBlock
Shader "Game/HealthBar"
{
    Properties
    {
        _Fill ("Fill Amount", Range(0,1)) = 1.0
        _BarColor ("Bar Color", Color) = (0,1,0,1)
        _BackgroundColor ("Background Color", Color) = (0.2,0.2,0.2,1)
        _BorderColor ("Border Color", Color) = (0,0,0,1)
        _BorderWidth ("Border Width", Range(0, 0.1)) = 0.02
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BarColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            float4 _BackgroundColor, _BorderColor;
            float _BorderWidth;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);
                float4 barCol = UNITY_ACCESS_INSTANCED_PROP(Props, _BarColor);

                // Border
                if (i.uv.x < _BorderWidth || i.uv.x > 1-_BorderWidth ||
                    i.uv.y < _BorderWidth || i.uv.y > 1-_BorderWidth)
                    return _BorderColor;

                // Fill vs background
                return i.uv.x < fill ? barCol : _BackgroundColor;
            }
            ENDCG
        }
    }
}
```

```csharp
// HealthBarRenderer.cs — Per-entity component, uses MaterialPropertyBlock
public class HealthBarRenderer : MonoBehaviour
{
    private static readonly int FillID = Shader.PropertyToID("_Fill");
    private static readonly int ColorID = Shader.PropertyToID("_BarColor");

    [SerializeField] private MeshRenderer _barRenderer;
    private MaterialPropertyBlock _mpb;

    void Awake() => _mpb = new MaterialPropertyBlock();

    public void UpdateHealth(float normalized)
    {
        _mpb.SetFloat(FillID, normalized);
        _mpb.SetColor(ColorID, Color.Lerp(Color.red, Color.green, normalized));
        _barRenderer.SetPropertyBlock(_mpb);
    }
}
```

**Performance:** With GPU Instancing enabled, hundreds of health bars render in **1-2 draw calls** instead of hundreds.

### 4.2 Floating Damage Numbers (Object Pooled)

```csharp
public class DamageNumberPool : MonoBehaviour
{
    [SerializeField] private DamageNumber _prefab;
    [SerializeField] private int _poolSize = 50;
    [SerializeField] private Canvas _canvas; // Dedicated screen-space canvas

    private readonly Queue<DamageNumber> _pool = new();

    void Awake()
    {
        for (int i = 0; i < _poolSize; i++)
        {
            var instance = Instantiate(_prefab, _canvas.transform);
            instance.gameObject.SetActive(false);
            instance.Pool = this;
            _pool.Enqueue(instance);
        }
    }

    public DamageNumber Spawn(Vector3 worldPos, int damage, DamageType type)
    {
        if (_pool.Count == 0) return null; // or grow pool

        var number = _pool.Dequeue();
        number.Show(worldPos, damage, type);
        return number;
    }

    public void Return(DamageNumber number) => _pool.Enqueue(number);
}

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _duration = 1f;
    [SerializeField] private float _floatSpeed = 100f;
    [SerializeField] private AnimationCurve _scaleCurve;
    [SerializeField] private AnimationCurve _alphaCurve;

    [HideInInspector] public DamageNumberPool Pool;

    private Camera _cam;
    private Vector3 _worldPos;
    private float _elapsed;
    private RectTransform _rect;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cam = Camera.main;
    }

    public void Show(Vector3 worldPos, int damage, DamageType type)
    {
        _worldPos = worldPos;
        _elapsed = 0f;

        _text.text = damage.ToString();
        _text.color = type switch
        {
            DamageType.Critical => Color.yellow,
            DamageType.Heal => Color.green,
            _ => Color.white
        };

        gameObject.SetActive(true);
    }

    void Update()
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _duration)
        {
            gameObject.SetActive(false);
            Pool.Return(this);
            return;
        }

        float t = _elapsed / _duration;

        // Float upward in world space
        _worldPos += Vector3.up * (_floatSpeed * Time.deltaTime);

        // Project to screen space
        Vector3 screenPos = _cam.WorldToScreenPoint(_worldPos);
        if (screenPos.z < 0) { gameObject.SetActive(false); Pool.Return(this); return; }
        _rect.position = screenPos;

        // Animate scale and alpha
        float scale = _scaleCurve.Evaluate(t);
        _rect.localScale = new Vector3(scale, scale, 1f);

        var c = _text.color;
        c.a = _alphaCurve.Evaluate(t);
        _text.color = c;
    }
}

public enum DamageType { Normal, Critical, Heal }
```

**Performance tips:**
- Use a **separate Canvas** only for damage numbers (rebuild isolation)
- **Pool size** should cover the peak number of simultaneous numbers
- Avoid `string.Format` — use `int.ToString()` or cached strings for common values
- Consider `StringBuilder` or `TMP_Text.SetText("Damage: {0}", value)` (TMP optimizes internally)

### 4.3 Minimap Systems

#### Approach with RenderTexture (most common)

```csharp
public class MinimapSystem : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Camera _minimapCamera;      // Orthographic, top-down
    [SerializeField] private RawImage _minimapDisplay;    // On the HUD Canvas
    [SerializeField] private RenderTexture _renderTexture;

    [Header("Config")]
    [SerializeField] private Transform _player;
    [SerializeField] private float _height = 50f;
    [SerializeField] private float _orthoSize = 30f;
    [SerializeField] private bool _rotateWithPlayer = true;

    [Header("Performance")]
    [SerializeField] private int _renderInterval = 2; // Render every N frames
    private int _frameCount;

    void Start()
    {
        // Create the RenderTexture at an appropriate resolution (does not need to be high)
        if (_renderTexture == null)
        {
            _renderTexture = new RenderTexture(256, 256, 16);
            _renderTexture.antiAliasing = 1; // No AA on the minimap
        }

        _minimapCamera.targetTexture = _renderTexture;
        _minimapCamera.orthographic = true;
        _minimapCamera.orthographicSize = _orthoSize;
        _minimapDisplay.texture = _renderTexture;

        // IMPORTANT: Use culling mask to render only minimap layers
        // Exclude shadows, particles, etc. that are not relevant
        _minimapCamera.cullingMask = LayerMask.GetMask("MinimapTerrain", "MinimapIcons");
    }

    void LateUpdate()
    {
        // Position camera above the player
        var pos = _player.position;
        _minimapCamera.transform.position = new Vector3(pos.x, _height, pos.z);

        if (_rotateWithPlayer)
        {
            float yRotation = _player.eulerAngles.y;
            _minimapCamera.transform.rotation = Quaternion.Euler(90f, yRotation, 0f);
        }

        // Render at intervals to save GPU
        _frameCount++;
        _minimapCamera.enabled = (_frameCount % _renderInterval == 0);
    }
}
```

**Minimap optimizations:**

- **Low resolution:** 128x128 or 256x256 is sufficient for most minimaps
- **Skip frames:** Render every 2-3 frames (players rarely notice)
- **Dedicated culling mask:** Only render what is needed (simplified terrain + icons)
- **No shadows:** Disable shadows on the minimap camera
- **Simple icons:** Use sprites/quads on the minimap layer instead of the actual 3D models

### 4.4 Dynamic HUD with Many Elements

```csharp
// For HUDs with dozens of bars, buff icons, indicators:
// 1. Group by update frequency
// 2. Use dirty flags aggressively
// 3. Throttle non-critical updates

public class ThrottledHUDUpdater : MonoBehaviour
{
    [SerializeField] private float _criticalInterval = 0f;    // HP, ammo: every frame
    [SerializeField] private float _normalInterval = 0.1f;    // Buffs, minimap: 10 fps
    [SerializeField] private float _lowInterval = 0.5f;       // XP bar, quest tracker: 2 fps

    private float _normalTimer, _lowTimer;

    void Update()
    {
        // Always update critical elements
        UpdateCriticalElements();

        _normalTimer += Time.deltaTime;
        if (_normalTimer >= _normalInterval)
        {
            _normalTimer = 0f;
            UpdateNormalElements();
        }

        _lowTimer += Time.deltaTime;
        if (_lowTimer >= _lowInterval)
        {
            _lowTimer = 0f;
            UpdateLowPriorityElements();
        }
    }

    void UpdateCriticalElements() { /* HP bar, ammo counter */ }
    void UpdateNormalElements() { /* Buff icons, cooldowns */ }
    void UpdateLowPriorityElements() { /* XP bar, quest tracker, objectives */ }
}
```

---

## 5. Responsive UI for Multiple Mobile Resolutions

### 5.1 Canvas Scaler Configuration

```csharp
// Recommended CanvasScaler settings for mobile:
// Mode: Scale With Screen Size
// Reference Resolution: 1080 x 1920 (portrait) or 1920 x 1080 (landscape)
// Screen Match Mode: Match Width Or Height
// Match: 0.5 (balance between width and height)
//
// For predominantly portrait games: Match = 0 (prioritizes width)
// For predominantly landscape games: Match = 1 (prioritizes height)
```

### 5.2 Safe Area Handling

```csharp
/// <summary>
/// Adjusts a RectTransform to respect the device Safe Area
/// (notch, rounded corners, gesture bar).
/// Attach to a panel that contains all interactive UI.
/// </summary>
public class SafeAreaHandler : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    void Awake() => _rect = GetComponent<RectTransform>();

    void Update()
    {
        // Recalculate if safe area or resolution changed (screen rotation)
        if (Screen.safeArea != _lastSafeArea ||
            Screen.width != _lastScreenSize.x ||
            Screen.height != _lastScreenSize.y)
        {
            ApplySafeArea();
        }
    }

    void ApplySafeArea()
    {
        var safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // Convert safe area from pixels to anchors (0-1)
        var anchorMin = safeArea.position;
        var anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
    }
}
```

### 5.3 Responsive Layout Strategies

```
┌──────────────────────────────────────────────┐
│  Principles for Responsive Mobile UI:        │
│                                              │
│  1. ANCHOR EVERYTHING relatively             │
│     - Never use absolute pixel positions     │
│     - Anchors must reflect intent            │
│       (corner, center, stretch)              │
│                                              │
│  2. TEST AT EXTREME ASPECT RATIOS            │
│     - 16:9 (standard)                        │
│     - 18:9 / 19.5:9 (modern phones)         │
│     - 4:3 (tablets, iPad)                    │
│     - 20:9+ (ultra-wide phones)             │
│                                              │
│  3. DYNAMIC FONT SCALING                     │
│     - Use Auto Size in TextMeshPro           │
│     - Set min/max font size                  │
│     - Test readability at the smallest size  │
│                                              │
│  4. MINIMUM TOUCH TARGET: 44x44 dp          │
│     (~88px at 1080p reference)               │
│                                              │
│  5. USE DEVICE SIMULATOR                     │
│     Window > Analysis > Device Simulator     │
└──────────────────────────────────────────────┘
```

```csharp
// Tip: Adaptive layout component based on aspect ratio
public class AdaptiveLayout : MonoBehaviour
{
    [SerializeField] private RectTransform _target;
    [SerializeField] private Vector2 _wideOffset;    // 16:9+
    [SerializeField] private Vector2 _tallOffset;    // 4:3
    [SerializeField] private float _aspectThreshold = 1.5f; // width/height

    void Start() => Adapt();
    void OnRectTransformDimensionsChange() => Adapt();

    void Adapt()
    {
        float aspect = (float)Screen.width / Screen.height;
        _target.anchoredPosition = aspect >= _aspectThreshold ? _wideOffset : _tallOffset;
    }
}
```

---

## 6. Accessibility in Game UI

### 6.1 Core Principles

Accessibility is not a "nice to have" — it expands your audience and frequently improves UX for all players. Unity offers the `UnityEngine.Accessibility` module since Unity 2023.1+.

### 6.2 Implementation Checklist

**Visual:**

- **Minimum contrast:** Text on background must have ratio >= 4.5:1 (WCAG AA). Use tools like WebAIM Contrast Checker
- **Text scaling:** Respect the device's `AccessibilitySettings.fontScale`; offer an in-game slider from 80% to 200%
- **Color blindness mode:** Offer filters (Protanopia, Deuteranopia, Tritanopia) or use iconography beyond color to convey information

```csharp
// Respect system accessibility settings
using UnityEngine.Accessibility;

public class AccessibleTextScaler : MonoBehaviour
{
    [SerializeField] private TMP_Text[] _texts;
    [SerializeField] private float _baseFontSize = 24f;

    void Start()
    {
        // AccessibilitySettings.fontScale returns the system multiplier
        float systemScale = AccessibilitySettings.fontScale;
        foreach (var text in _texts)
            text.fontSize = _baseFontSize * systemScale;
    }
}
```

**Auditory:**

- Subtitles for all dialogue and important sound effects
- Visual indicators for directional audio (where the sound is coming from)
- Independent volume per category (music, SFX, voice, UI)

**Motor:**

- Full control remapping
- Hold input instead of quick-tap (or option to toggle)
- Configurable auto-aim / aim-assist

**Cognitive:**

- Revisitable tutorial and glossary of terms
- Difficulty option that does not penalize progression
- Simplified HUD as an option

### 6.3 Screen Reader Support (Unity 2023.1+)

```csharp
// Use AccessibilityHierarchy to create a tree navigable by screen readers
using UnityEngine.Accessibility;

public class AccessibleMenuSetup : MonoBehaviour
{
    void Start()
    {
        var hierarchy = new AccessibilityHierarchy();

        var playButton = new AccessibilityNode
        {
            label = "Play Game",
            role = AccessibilityRole.Button,
            // Callback when the screen reader activates this node
        };

        hierarchy.Add(playButton);

        // Register the hierarchy with the system
        AssistiveSupport.activeHierarchy = hierarchy;
    }
}
```

---

## 7. TextMeshPro: Best Practices

### 7.1 SDF Fonts — How It Works

TextMeshPro uses **Signed Distance Field** rendering: the font atlas contains distance-from-outline information for each glyph, not pixels. This enables:

- **Perfect scalability:** Sharp text at any size, without pixelation
- **Effects at no extra cost:** Outline, shadow, glow, underlay via shader
- **Lower memory usage:** A 512x512 atlas can cover many sizes

### 7.2 Font Asset Creation

```
Font Asset Creator — Recommended Settings:
─────────────────────────────────────────────
Sampling Point Size:    Auto Size (or 64-90 for high quality)
Padding:                5-9 (more padding = smoother, more atlas space)
Packing Method:         Optimum
Atlas Resolution:       512x512 (sufficient for most cases)
                        1024x1024 (if including CJK/many characters)
Character Set:          ASCII (for Western)
                        Extended ASCII (if accented characters are needed)
                        Custom Characters (for specific language support)
Render Mode:            SDFAA (default, best cost-benefit ratio)
                        SDFAA_HINTED (better for small sizes)
```

### 7.3 Material Presets

```
GOLDEN RULE: Never modify the default material of the Font Asset.
Always create Material Presets for variations.

Material Preset = same atlas + different material = no extra texture

Workflow:
1. Select the Font Asset in the Project
2. Click the default material
3. Ctrl+D (Duplicate) → Rename: "FontName - Outline", "FontName - Shadow"
4. Customize the preset (outline width, shadow offset, etc.)
5. In the TMP component, select the preset from the Material Preset dropdown
```

```csharp
// Tip: Swap material presets at runtime for states (normal, hover, disabled)
public class TMPMaterialSwapper : MonoBehaviour
{
    [SerializeField] private TMP_Text _text;
    [SerializeField] private Material _normalPreset;
    [SerializeField] private Material _highlightPreset;
    [SerializeField] private Material _disabledPreset;

    public void SetState(ButtonState state)
    {
        _text.fontSharedMaterial = state switch
        {
            ButtonState.Normal => _normalPreset,
            ButtonState.Highlighted => _highlightPreset,
            ButtonState.Disabled => _disabledPreset,
            _ => _normalPreset
        };
    }
}
```

### 7.4 Performance Best Practices

```csharp
// ✅ Use fontSharedMaterial (shared) instead of fontMaterial (creates an instance)
text.fontSharedMaterial = presetMaterial; // ← no allocation
// ❌ text.fontMaterial = presetMaterial;  // ← creates a new instance!

// ✅ Use SetText with numeric formatting (avoids string allocation)
text.SetText("Score: {0}", scoreValue);     // No GC alloc
// ❌ text.text = $"Score: {scoreValue}";   // Allocates a string

// ✅ For text that changes frequently, disable raycastTarget
text.raycastTarget = false;

// ✅ Use Auto Size with caution — it recalculates layout
// Set Min/Max Size to limit the search range
// In large lists, avoid Auto Size

// ✅ Rich text tags are processed every frame if the text is dirty
// Prefer Material Presets for global styling
// Use rich text only for inline variations (<color>, <b>, etc.)
```

### 7.5 Shader Selection

| Shader | Use Case | Performance |
|---|---|---|
| **Distance Field** | Desktop, Console | Full features, heavier |
| **Mobile/Distance Field** | Mobile (default) | Optimized, fewer features |
| **Distance Field Overlay** | Always-visible UI | Ignores depth, useful for HUD |
| **Bitmap** | Pixel art style | No SDF, lighter, does not scale |
| **Mobile/Bitmap** | Pixel art mobile | Lightest option |

### 7.6 Fallback Fonts and Multi-Language

```
To support multiple languages:

1. Create a primary Font Asset (e.g.: Roboto - ASCII)
2. Create fallback Font Assets:
   - CJK font (e.g.: Noto Sans CJK — Chinese/Japanese/Korean characters)
   - Arabic font (e.g.: Noto Sans Arabic)
3. In the primary Font Asset, add fallbacks under:
   Font Asset > Fallback Font Assets List
4. TMP automatically uses the fallback when a character is not in the primary atlas

⚠️ Use Dynamic SDF with caution:
   - Allows generating glyphs at runtime (no need to have all of them in the atlas)
   - Useful for CJK (thousands of characters)
   - But causes hitches the first time a new glyph is rendered
   - Pre-warm the most common characters via TMP_FontAsset.TryAddCharacters()
```

---

## 8. Performance Summary — Quick Reference

| Technique | Impact | Difficulty |
|---|---|---|
| Disable `raycastTarget` on non-interactive elements | High | Easy |
| Canvas splitting (static/dynamic) | High | Easy |
| Dirty-check before setting `.text` | High | Easy |
| Object pooling for damage numbers | High | Medium |
| Shader-based health bars | Very High | Medium |
| CanvasGroup.alpha vs SetActive | Medium | Easy |
| Disable LayoutGroup after init | Medium | Easy |
| Virtualized scroll for large lists | Very High | Medium |
| TMP: `SetText()` instead of `string.Format` | Medium | Easy |
| TMP: `fontSharedMaterial` instead of `fontMaterial` | Medium | Easy |
| Minimap: skip frames + low-res RT | High | Easy |
| Throttle HUD updates by priority | High | Medium |

---

## Sources and References

- [Unity Manual — Comparison of UI systems](https://docs.unity3d.com/6000.0/Documentation/Manual/UI-system-compare.html)
- [Unity Manual — Migrate from uGUI to UI Toolkit](https://docs.unity3d.com/6000.2/Documentation/Manual/UIE-Transitioning-From-UGUI.html)
- [Unity UI Toolkit vs UGUI: 2025 Developer Guide — Angry Shark Studio](https://www.angry-shark-studio.com/blog/unity-ui-toolkit-vs-ugui-2025-guide/)
- [Optimizing Unity UI — Unity Learn](https://learn.unity.com/tutorial/optimizing-unity-ui)
- [Unity UI Optimization Tips — Unity](https://unity.com/how-to/unity-ui-optimization-tips)
- [Unity UI Profiling: Canvas Rebuilds — TheGamedev.Guru](https://thegamedev.guru/unity-ui/profiling-canvas-rebuilds/)
- [Unity UI Best Practices for Performance — Wayline](https://www.wayline.io/blog/unity-ui-best-practices-for-performance)
- [Split Canvas for Dynamic Objects — Unity Support](https://support.unity.com/hc/en-us/articles/115000355466-Split-canvas-for-dynamic-objects)
- [MVVM Pattern — Unity Learn](https://learn.unity.com/tutorial/model-view-viewmodel-pattern)
- [MVC and MVP Patterns — Unity Learn](https://learn.unity.com/course/design-patterns-unity-6/tutorial/build-a-modular-codebase-with-mvc-and-mvp-programming-patterns)
- [ScriptableObjects as Event Channels — Unity](https://unity.com/how-to/scriptableobjects-event-channels-game-code)
- [UnityMvvmToolkit — GitHub](https://github.com/LibraStack/UnityMvvmToolkit)
- [UI Manager (Screen Stack) — GitHub](https://github.com/Blitzy/unity-ui-manager)
- [Unity Manual — Data Binding](https://docs.unity3d.com/6000.3/Documentation/Manual/best-practice-guides/ui-toolkit-for-advanced-unity-developers/data-binding.html)
- [Enemy Health Bars in 1 Draw Call — Steve Streeting](https://www.stevestreeting.com/2019/02/22/enemy-health-bars-in-1-draw-call-in-unity/)
- [Object Pooling in Unity — Wayline](https://www.wayline.io/blog/implementing-object-pooling-in-unity-for-performance)
- [Designing UI for Multiple Resolutions — Unity Manual](https://docs.unity3d.com/Packages/com.unity.ugui@1.0/manual/HOWTO-UIMultiResolution.html)
- [Canvas Scaler — Unity Manual](https://docs.unity3d.com/Packages/com.unity.ugui@2.0/manual/script-CanvasScaler.html)
- [Unity Safe Area for UI Canvas — RustyCruise Labs](https://rustycruiselabs.com/devlogs/generic/2025-01-11-unity-safe-area/)
- [Accessibility for Unity Applications — Unity Manual](https://docs.unity3d.com/6000.3/Documentation/Manual/accessibility.html)
- [Accessibility Fundamentals — Unity Foundations](https://www.foundations.unity.com/fundamentals/accessibility)
- [Accessibility Standards — Unity Learn](https://learn.unity.com/course/practical-game-accessibility/unit/inclusive-design-and-accessibility/tutorial/accessibility-standards-and-guidelines)
- [SDF Fonts — TextMeshPro Manual](https://docs.unity3d.com/Packages/com.unity.textmeshpro@4.0/manual/FontAssetsSDF.html)
- [Material Presets — Unity Learn](https://learn.unity.com/tutorial/textmesh-pro-working-with-material-presets)
- [TMP Shaders and Materials — Unity Learn](https://learn.unity.com/tutorial/textmesh-pro-shaders-and-material-properties)
- [Font Asset Creator — TextMeshPro Manual](https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.2/manual/FontAssetsCreator.html)
- [Game UI Design: Complete Interface Guide 2025 — GeneralistProgrammer](https://generalistprogrammer.com/tutorials/game-ui-design-complete-interface-guide-2025)
- [Ultimate Unity Optimization Guide for Mobile 2025 — TECHsWILL](https://www.techswill.com/2025/05/26/the-ultimate-unity-optimization-guide-for-mobile-games-2025-edition/)
