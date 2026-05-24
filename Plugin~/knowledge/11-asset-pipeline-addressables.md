# 11 — Asset Pipeline, Addressables, and Memory Management

> Practical reference guide for Unity projects focused on asset importing, the Addressables system, texture/audio compression, and project organization.

---

## Table of Contents

1. [Asset Pipeline v2](#1-asset-pipeline-v2)
2. [Resources vs AssetBundles vs Addressables](#2-resources-vs-assetbundles-vs-addressables)
3. [Addressables In Depth](#3-addressables-in-depth)
4. [Sprite Atlas](#4-sprite-atlas)
5. [Texture Import Settings](#5-texture-import-settings)
6. [Audio Import Settings](#6-audio-import-settings)
7. [Asset Organization](#7-asset-organization)
8. [Sources and References](#8-sources-and-references)

---

## 1. Asset Pipeline v2

### How it works

Introduced in Unity 2019.3 and the default since Unity 2020.1, the Asset Import Pipeline v2 is the system that **discovers, imports, processes, and serializes** all project assets.

**Import flow:**

```
Original file (PNG, FBX, WAV...)
        │
        ▼
   Asset Importer (converts to internal format)
        │
        ▼
   Serialization → Artifact (stored in Library/)
        │
        ▼
   .meta file generated (GUID + import settings)
```

When you place an `image.png` in the project, the pipeline:

1. Detects the new file
2. Runs the corresponding importer (TextureImporter, ModelImporter, etc.)
3. Converts it to a serialized instance of `UnityEngine.Object` (e.g., `Texture2D` class)
4. Stores the result as an **artifact** in the `Library/` folder
5. Generates a `.meta` file with the **GUID** (globally identifies the asset) and **import settings**

When you access `image.png` via script, you are accessing the **serialized artifact**, not the original file.

### Advantages of Pipeline v2

- **Fast platform switching** — artifact cache per platform
- **Parallel import** — multiple assets imported simultaneously
- **Robust dependency tracking** — only reimports what changed
- **Deterministic** — same inputs produce the same outputs

### The .meta file

Every asset generates a `.meta` containing:

- **GUID** — unique identifier connecting the asset to the artifact
- **Import settings** — importer-specific settings (compression, max size, etc.)

> **Golden rule:** Never delete `.meta` files manually. Losing a `.meta` means losing all references to that asset in the project.

---

## 2. Resources vs AssetBundles vs Addressables

### Comparison Table

| Criterion | Resources | AssetBundles | Addressables |
|---|---|---|---|
| **Setup** | Zero — just create a `Resources/` folder | Medium — requires manual bundle build | Medium — requires package + group configuration |
| **Loading** | `Resources.Load<T>("path")` synchronous | Manual bundle + asset API | `Addressables.LoadAssetAsync<T>("key")` |
| **Build size** | Everything included in the build, always | On demand, granular | On demand, granular |
| **Startup time** | Indexes everything at initialization | No impact | No impact |
| **Remote content** | Not supported | Yes, manual | Yes, integrated |
| **Versioning** | None | Manual and error-prone | Automatic via catalogs |
| **Dependencies** | No control | Manual | Automatically managed |
| **Memory management** | `Resources.UnloadUnusedAssets()` | Manual via `AssetBundle.Unload()` | Automatic reference counting |
| **Content updates** | Full rebuild | Rebuild specific bundles | Integrated delta update workflow |
| **Recommendation** | Prototypes/game jams only | Legacy projects | **Standard for production** |

### Why Resources is discouraged

1. **Everything goes into the build** — any file in `Resources/` is included, even if never used
2. **Slow startup** — Unity indexes all `Resources/` content at initialization
3. **No granularity** — impossible to separate local from remote content
4. **Inefficient unloading** — `Resources.UnloadUnusedAssets()` is slow and imprecise

### Migration Path: Resources → Addressables

```csharp
// BEFORE — Resources
GameObject prefab = Resources.Load<GameObject>("Enemies/Goblin");
Instantiate(prefab);

// AFTER — Addressables
var handle = Addressables.LoadAssetAsync<GameObject>("Enemies/Goblin");
handle.Completed += op => {
    if (op.Status == AsyncOperationStatus.Succeeded)
        Instantiate(op.Result);
};
```

**Migration steps:**

1. Install the `com.unity.addressables` package via Package Manager
2. Mark each asset in `Resources/` as Addressable (Inspector → "Addressable" checkbox)
3. The default address will be the relative path (e.g., `Enemies/Goblin`) — maintains compatibility
4. Replace `Resources.Load<T>()` with `Addressables.LoadAssetAsync<T>()`
5. Add `Addressables.Release(handle)` where appropriate
6. Remove the `Resources/` folders once migration is complete
7. Test with **Play Mode Script → Use Existing Build** to validate

---

## 3. Addressables In Depth

### 3.1 Setup and Configuration

**Installation:**

```
Window → Package Manager → Addressables (com.unity.addressables)
```

**Initial configuration:**

```
Window → Asset Management → Addressables → Groups → Create Addressables Settings
```

This creates the `Assets/AddressableAssetsData/` folder with:

- `AddressableAssetSettings.asset` — global settings
- `Default Local Group` — default group for local assets

**Making an asset Addressable:**

1. Select the asset in the Project window
2. In the Inspector, check the **"Addressable"** checkbox
3. Define the address — a unique string that identifies the asset

### 3.2 Addressable Groups — Grouping Strategies

Groups are the **primary organizational unit**. Each group generates one or more AssetBundles in the build.

**Recommended strategies:**

| Strategy | When to use | Example |
|---|---|---|
| **By scene/level** | Assets used in specific scenes | `Group_Level1`, `Group_Level2` |
| **By type** | When types have different lifecycles | `Group_UI`, `Group_Audio`, `Group_Characters` |
| **By update frequency** | Frequently changing content separated from static | `Group_StaticCore`, `Group_LiveContent` |
| **By distribution** | Separate local from remote | `Group_Local`, `Group_Remote` |
| **By simultaneous use** | Assets that always load together | `Group_MainMenu`, `Group_BattleAssets` |

**Practical rules:**

- Assets that **always load together** → same group
- Assets that **never load together** → different groups (avoids loading the entire bundle)
- **Large assets** (>1MB) → consider individual group or Bundle Mode = Pack Separately
- **Do not create groups with hundreds of small assets** → catalog overhead

**Group configuration (Schemas):**

Each group has schemas that define behavior:

- **Content Packing & Loading** — build/load paths, bundle mode, compression
- **Content Update Restriction** — Can Change Post Release / Cannot Change Post Release

**Bundle Mode:**

| Mode | Description |
|---|---|
| **Pack Together** | All group assets in 1 bundle |
| **Pack Separately** | Each asset in its own bundle |
| **Pack Together By Label** | Assets with the same label grouped together |

### 3.3 Asynchronous Loading

**LoadAssetAsync — Load a single asset:**

```csharp
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddressableLoader : MonoBehaviour
{
    private AsyncOperationHandle<GameObject> _handle;

    async void Start()
    {
        // Option 1: async/await
        _handle = Addressables.LoadAssetAsync<GameObject>("Prefabs/Player");
        GameObject prefab = await _handle.Task;

        if (_handle.Status == AsyncOperationStatus.Succeeded)
        {
            Instantiate(prefab, transform.position, Quaternion.identity);
        }
    }

    void OnDestroy()
    {
        // ALWAYS release the handle
        Addressables.Release(_handle);
    }
}
```

**Callback option:**

```csharp
void LoadWithCallback()
{
    Addressables.LoadAssetAsync<Sprite>("UI/Icons/coin")
        .Completed += handle =>
    {
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            myImage.sprite = handle.Result;
        }
    };
}
```

**LoadAssetsAsync — Load multiple assets by label:**

```csharp
async void LoadAllEnemies()
{
    var handle = Addressables.LoadAssetsAsync<GameObject>(
        "enemies",                    // label
        prefab => {                   // callback per loaded asset
            Debug.Log($"Loaded: {prefab.name}");
        });

    IList<GameObject> allEnemies = await handle.Task;
    Debug.Log($"Total enemies loaded: {allEnemies.Count}");
}
```

**InstantiateAsync — Instantiate directly:**

```csharp
// Recommended when you want to instantiate (better ref counting)
var handle = Addressables.InstantiateAsync("Prefabs/Bullet",
    position, rotation, parentTransform);

handle.Completed += op =>
{
    if (op.Status == AsyncOperationStatus.Succeeded)
    {
        // op.Result is the instantiated GameObject
    }
};

// To release:
// Addressables.ReleaseInstance(handle) — or —
// Addressables.ReleaseInstance(gameObject)
```

**LoadSceneAsync — Load a scene:**

```csharp
using UnityEngine.SceneManagement;

async void LoadLevel()
{
    var handle = Addressables.LoadSceneAsync("Scenes/Level_02",
        LoadSceneMode.Additive);

    var sceneInstance = await handle.Task;

    // To unload:
    // Addressables.UnloadSceneAsync(handle);
}
```

### 3.4 Reference Counting and Memory Management

The Addressables system uses **reference counting** to manage memory:

```
LoadAssetAsync("key")    → ref count +1
LoadAssetAsync("key")    → ref count +1 (same asset, count = 2)
Release(handle1)         → ref count -1 (count = 1, asset remains)
Release(handle2)         → ref count -1 (count = 0, eligible for unload)
```

**Fundamental rule:**

> For every `Load` or `Instantiate` call, there must be a corresponding `Release` call.

**AssetBundle ref counting (lower layer):**

```
Bundle "enemies.bundle" contains: Goblin, Orc, Troll
    LoadAssetAsync("Goblin")  → bundle ref +1
    LoadAssetAsync("Orc")     → bundle ref +1 (count = 2)
    Release(goblinHandle)     → bundle ref -1 (count = 1)
    Release(orcHandle)        → bundle ref -1 (count = 0)
    → Entire bundle unloaded from memory
```

**Critical warning — Load + manual Instantiate:**

```csharp
// ⚠️ DANGEROUS — ref count does NOT increment per instance
var handle = Addressables.LoadAssetAsync<GameObject>("Prefab");
await handle.Task;

var inst1 = Object.Instantiate(handle.Result);
var inst2 = Object.Instantiate(handle.Result);

// If you Release BEFORE destroying the instances:
Addressables.Release(handle); // ref count = 0 → bundle unloaded!
// inst1 and inst2 lose materials, textures, etc. → broken visuals!
```

**Safe pattern with InstantiateAsync:**

```csharp
// ✅ SAFE — each instance increments ref count
var h1 = Addressables.InstantiateAsync("Prefab");
var h2 = Addressables.InstantiateAsync("Prefab");
// ref count = 2

Addressables.ReleaseInstance(h1); // ref count = 1
Addressables.ReleaseInstance(h2); // ref count = 0 → safe unload
```

**Diagnostic tool:**

```
Window → Asset Management → Addressables → Event Viewer
```

Shows in real time: ref counts, loaded bundles, active operations.

### 3.5 Build Profiles (Local vs Remote)

**Profiles** define path variables for build and load:

```
Window → Asset Management → Addressables → Profiles
```

**Typical configuration:**

| Variable | Local | Remote |
|---|---|---|
| **BuildPath** | `[UnityEngine.AddressableAssets.Addressables.BuildPath]/[BuildTarget]` | `ServerData/[BuildTarget]` |
| **LoadPath** | `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` | `https://cdn.mygame.com/assets/[BuildTarget]` |

**Recommended profiles:**

| Profile | Build Path | Load Path | Use |
|---|---|---|---|
| **Development** | Local build path | Local load path | Editor testing |
| **Staging** | Remote server data | Staging server URL | QA and testing |
| **Production** | Remote server data | Production CDN URL | Final build |

**Applying a profile to a group:**

1. Select the group in the Groups window
2. In the **Content Packing & Loading** schema
3. **Build Path** → `Remote.BuildPath`
4. **Load Path** → `Remote.LoadPath`

### 3.6 Content Update Workflow

Allows updating content in a published game **without a full rebuild**.

**Flow:**

```
1. Initial build (Full Build)
   → Generates bundles + catalog + addressables_content_state.bin
   → Publish app + host remote bundles

2. Modify assets in the project

3. Addressables → Tools → Check for Content Update Restrictions
   → Analyzes addressables_content_state.bin
   → Moves changed assets to a new temporary group

4. Addressables → Build → Update a Previous Build
   → Generates ONLY the modified bundles + new catalog
   → Upload new bundles to the server

5. Installed app downloads new catalog → detects changed bundles → incremental download
```

**Critical file:** `addressables_content_state.bin`

- Generated at each full build in `Assets/AddressableAssetsData/[platform]/`
- **Must be versioned and preserved** for each published build
- Without it, content updates are not possible

**Content Update Restriction on the group:**

| Setting | Behavior |
|---|---|
| **Can Change Post Release** | Assets can be updated remotely (typical for remote groups) |
| **Cannot Change Post Release** | Assets are locked to the build version (typical for local groups) |

---

## 4. Sprite Atlas

### When to use

- **UI with many sprites** — reduces draw calls by grouping sprites into a texture
- **2D games** — character spritesheets, tiles, icons
- **Any scenario with many small sprites** — batch rendering

### Configuration

1. `Assets → Create → 2D → Sprite Atlas`
2. Drag sprites/folders into the **Objects for Packing** list
3. Configure:
   - **Allow Rotation** — rotate sprites for better packing (disable for pixel-perfect UI)
   - **Tight Packing** — trims transparency to save space
   - **Padding** — pixels between sprites (2-4 pixels recommended to avoid bleeding)
4. **Include in Build** — enabled by default

### V1 vs V2

| Feature | Sprite Atlas V1 | Sprite Atlas V2 |
|---|---|---|
| **Unity version** | 2017+ | 2020.1+ |
| **Asset type** | `.spriteatlas` | `.spriteatlasv2` |
| **Variants** | Supports variants (e.g., HD/SD) | Variants via Addressables |
| **Binding** | Early binding (at build) | Supports native late binding |
| **Addressables** | Requires workarounds | Improved integration |
| **Activation** | `Edit → Project Settings → Editor → Sprite Packer → Mode: Sprite Atlas V1` | `Mode: Sprite Atlas V2` |

### Late Binding with Addressables

Late binding loads the atlas at **runtime** instead of at build time. Required when the atlas comes from a remote AssetBundle/Addressable.

**Setup:**

1. In the Sprite Atlas, **uncheck** "Include in Build"
2. Mark the Sprite Atlas as **Addressable**
3. Implement the late binding callback:

```csharp
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.AddressableAssets;

public class SpriteAtlasLoader : MonoBehaviour
{
    void OnEnable()
    {
        SpriteAtlasManager.atlasRequested += OnAtlasRequested;
    }

    void OnDisable()
    {
        SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
    }

    void OnAtlasRequested(string tag, System.Action<SpriteAtlas> callback)
    {
        // tag = name of the requested Sprite Atlas
        var handle = Addressables.LoadAssetAsync<SpriteAtlas>(tag);
        handle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
                callback(op.Result);
        };
    }
}
```

**Tip:** Place this script on a GameObject that persists between scenes (DontDestroyOnLoad) or in a bootstrap scene.

---

## 5. Texture Import Settings

### Formats by Platform

| Platform | RGB (no alpha) | RGBA (with alpha) | HDR | Bits/pixel |
|---|---|---|---|---|
| **PC/Console (DX11+)** | DXT1 (BC1) | BC7 or DXT5 (BC3) | BC6H | 4–8 bpp |
| **iOS (A8+)** | ASTC 6x6 or 8x8 | ASTC 4x4 or 6x6 | ASTC HDR | 0.89–8 bpp |
| **Android (ES 3.1+/Vulkan)** | ASTC 6x6 or 8x8 | ASTC 4x4 or 6x6 | ASTC HDR | 0.89–8 bpp |
| **Android (legacy)** | ETC2 RGB | ETC2 RGBA | — | 4–8 bpp |
| **WebGL** | DXT1 / ETC2 | DXT5 / ETC2 RGBA | — | 4–8 bpp |

### ASTC Block Sizes (Mobile)

| Block size | Bits/pixel | Quality | Recommended use |
|---|---|---|---|
| **4x4** | 8.0 bpp | Maximum | Normal maps, UI elements |
| **5x5** | 5.12 bpp | High | Character textures |
| **6x6** | 3.56 bpp | Good | Props, general textures |
| **8x8** | 2.0 bpp | Medium | Terrains, backgrounds |
| **10x10** | 1.28 bpp | Low | Skyboxes, distant objects |
| **12x12** | 0.89 bpp | Minimum | Thumbnails, previews |

### Recommended Settings

| Setting | UI Sprites | 3D Textures | Normal Maps | Lightmaps |
|---|---|---|---|---|
| **Max Size** | 1024–2048 | 512–2048 | 512–2048 | 1024–4096 |
| **Compression** | High Quality | Normal Quality | High Quality | Normal Quality |
| **Mipmaps** | **Disabled** | Enabled | Enabled | Enabled |
| **sRGB** | Enabled | Enabled | **Disabled** | Depends |
| **Read/Write** | **Disabled** | **Disabled** | **Disabled** | **Disabled** |
| **Alpha Source** | Input Texture Alpha | Input Texture Alpha | None | None |

### Read/Write Enabled — Impact

| Read/Write | Memory | When to enable |
|---|---|---|
| **Disabled** (default) | Texture on GPU only | Most cases |
| **Enabled** | Copy on GPU **+ copy in RAM** = **2x memory** | Only if you need to read pixels via `Texture2D.GetPixels()` at runtime |

> **Rule:** Keep Read/Write **always disabled** unless your code needs to read/write texture pixels at runtime.

### Mipmaps

- Enabled: generates smaller versions of the texture (50%, 25%, 12.5%...)
- **Cost:** +33% memory per texture
- **Benefit:** better performance and visual quality for distant objects
- **Disable for:** UI sprites, fixed 2D textures on screen, render textures

---

## 6. Audio Import Settings

### Compression Formats

| Format | Compression | Quality | CPU to decode | Best for |
|---|---|---|---|---|
| **PCM** | None (1:1) | Perfect | Almost zero | Rarely — only when CPU is critical and RAM is abundant |
| **ADPCM** | 3.5:1 | Low (digital noise) | Very low | Noisy SFX (explosions, impacts, ambient noise) |
| **Vorbis** | High (adjustable) | Good to excellent | High | Music, dialogue, clean SFX — **recommended default format** |

### Load Types

| Load Type | RAM Memory | Latency | CPU | Best for |
|---|---|---|---|---|
| **Decompress on Load** | High (PCM in RAM) | Minimal | Minimal during playback | Short, frequent SFX (<1s) |
| **Compressed in Memory** | Low (compressed in RAM) | Medium | Medium during playback | Medium SFX, dialogue |
| **Streaming** | Minimal (chunks) | High (buffer) | Continuous medium | Long music, ambient loops |

### Recommended Settings by Audio Type

| Audio type | Format | Load Type | Quality | Preload | Force Mono |
|---|---|---|---|---|---|
| **Short SFX** (<1s) | ADPCM | Decompress on Load | — | Yes | Evaluate |
| **Medium SFX** (1-5s) | Vorbis | Compressed in Memory | 70-100% | Yes | Evaluate |
| **Dialogue** | Vorbis | Compressed in Memory | 70-85% | No | **Yes** (mono) |
| **Music** | Vorbis | Streaming | 50-70% | No | No |
| **Ambient loops** | Vorbis | Streaming | 50-60% | No | Evaluate |

### Preload Audio Data

| Setting | Behavior |
|---|---|
| **Enabled** | Audio preloaded when the scene loads — no delay on first play |
| **Disabled** | Audio loaded on first `AudioSource.Play()` — may cause a hitch |

**Recommendation:** Enable for frequent SFX. Disable for music and long audio that does not play immediately.

### Force Mono

- Dialogue and SFX without panning → **Force Mono enabled** (half the size)
- Music and ambient with intentional stereo → keep stereo

---

## 7. Asset Organization

### Recommended Folder Structure

```
Assets/
├── _Project/                    # All project content (separated from packages)
│   ├── Art/
│   │   ├── Animations/
│   │   │   ├── Characters/
│   │   │   └── UI/
│   │   ├── Materials/
│   │   │   ├── Characters/
│   │   │   ├── Environment/
│   │   │   └── UI/
│   │   ├── Models/
│   │   │   ├── Characters/
│   │   │   ├── Props/
│   │   │   └── Environment/
│   │   ├── Sprites/
│   │   │   ├── UI/
│   │   │   │   ├── Icons/
│   │   │   │   ├── Buttons/
│   │   │   │   └── Backgrounds/
│   │   │   └── Gameplay/
│   │   ├── Textures/
│   │   │   ├── Characters/
│   │   │   ├── Environment/
│   │   │   └── Shared/
│   │   └── Shaders/
│   ├── Audio/
│   │   ├── Music/
│   │   ├── SFX/
│   │   │   ├── UI/
│   │   │   ├── Combat/
│   │   │   └── Environment/
│   │   └── Dialogue/
│   ├── Prefabs/
│   │   ├── Characters/
│   │   ├── UI/
│   │   ├── Environment/
│   │   └── VFX/
│   ├── Scenes/
│   │   ├── Levels/
│   │   ├── UI/
│   │   └── Test/
│   ├── Scripts/
│   │   ├── Core/
│   │   ├── Gameplay/
│   │   ├── UI/
│   │   └── Utils/
│   ├── ScriptableObjects/
│   └── Settings/               # Addressables, render pipelines, etc.
├── AddressableAssetsData/       # Auto-generated
└── Plugins/                     # Third-party SDKs
```

### Naming Conventions

**General rules:**

| Rule | Good | Bad |
|---|---|---|
| No spaces | `PlayerHealth.cs` | `Player Health.cs` |
| PascalCase for scripts | `GameManager.cs` | `gameManager.cs` |
| Noun first | `tree_oak_large` | `large_oak_tree` |
| Prefix by type | `tex_stone_diffuse` | `stone_diffuse` |
| Suffix by variant | `btn_play_pressed` | `btn_pressed_play` |

**Recommended prefixes:**

| Asset type | Prefix | Example |
|---|---|---|
| Texture / Sprite | `tex_` / `spr_` | `tex_rock_normal`, `spr_coin_gold` |
| Material | `mat_` | `mat_metal_rusty` |
| Audio SFX | `sfx_` | `sfx_explosion_large` |
| Audio Music | `mus_` | `mus_main_theme` |
| Animation Clip | `anim_` | `anim_player_run` |
| Animator Controller | `ac_` | `ac_player` |
| Prefab | `pfb_` | `pfb_enemy_goblin` |
| ScriptableObject | `so_` | `so_weapon_data` |
| Shader | `shd_` | `shd_toon_outline` |
| VFX / Particle | `vfx_` | `vfx_fire_small` |
| Sprite Atlas | `atlas_` | `atlas_ui_icons` |
| Font | `font_` | `font_main_bold` |

### Asset Labels

Labels are tags that can be assigned to any asset for **filtering and searching**.

**Main uses:**

1. **Addressables** — load multiple assets by label:
   ```csharp
   // Load all assets with the "level1" label
   Addressables.LoadAssetsAsync<GameObject>("level1", null);
   ```

2. **AssetDatabase** — Editor-time search:
   ```csharp
   // Search by label in the Editor
   string[] guids = AssetDatabase.FindAssets("l:environment");
   ```

3. **Grouping in Addressables** — Bundle Mode "Pack Together By Label"

**Recommended labels:**

| Context | Labels |
|---|---|
| By level | `level1`, `level2`, `menu` |
| By load priority | `preload`, `lazy`, `streaming` |
| By distribution | `local`, `remote`, `dlc_pack1` |
| By platform | `mobile_only`, `pc_hd` |

---

## 8. Sources and References

**Asset Pipeline:**
- [Unity Blog — The New Asset Import Pipeline](https://blog.unity.com/technology/the-new-asset-import-pipeline-solid-foundation-for-speeding-up-asset-imports)
- [Unity Manual — Asset Pipeline](https://docs.unity3d.com/Manual/AssetWorkflow.html)

**Addressables:**
- [Unity Docs — Addressables 1.20.5](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/index.html)
- [Unity Docs — Memory Management](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/MemoryManagement.html)
- [Unity Docs — Content Update Workflow](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/ContentUpdateWorkflow.html)
- [Unity Docs — Remote Content Distribution](https://docs.unity3d.com/Packages/com.unity.addressables@1.20/manual/RemoteContentDistribution.html)
- [Unity Docs — Groups Window (2.2.2)](https://docs.unity3d.com/Packages/com.unity.addressables@2.2/manual/GroupsWindow.html)
- [Unity Learn — Get Started with Addressables](https://learn.unity.com/course/get-started-with-addressables)
- [Unity Docs — Migration Guide](https://docs.unity3d.com/Packages/com.unity.addressables@1.1/manual/AddressableAssetsMigrationGuide.html)

**Sprite Atlas:**
- [Unity Manual — Sprite Atlas Late Binding](https://docs.unity3d.com/Manual/sprite/atlas/distribution/late-binding.html)
- [Unity Support — Sprite Atlas with Late Binding](https://support.unity.com/hc/en-us/articles/360000665546)

**Textures:**
- [Unity Manual — Texture Compression Formats](https://docs.unity3d.com/2022.2/Documentation/Manual/class-TextureImporterOverride.html)
- [DEV Community — Optimizing Texture Import Settings](https://dev.to/attiliohimeki/optimising-unity-texture-import-settings-37gi)

**Audio:**
- [Unity Manual — Audio Clip Import Settings](https://docs.unity3d.com/Manual/class-AudioClip.html)
- [GameDeveloper — Unity Audio Import Optimization](https://www.gamedeveloper.com/audio/unity-audio-import-optimisation---getting-more-bam-for-your-ram)

**Organization:**
- [Unity — Best Practices for Organizing Your Project](https://unity.com/how-to/organizing-your-project)
- [Anchorpoint — Unity Folder Structure Guide](https://www.anchorpoint.app/blog/unity-folder-structure)
