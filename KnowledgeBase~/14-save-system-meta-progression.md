# 14 — Save System & Meta-Progression in Unity

> Complete guide to data persistence, serialization, cloud save, and progression systems across runs/sessions.

---

## Table of Contents

1. [Save System Architecture](#1-save-system-architecture)
2. [Serialization](#2-serialization)
3. [Cloud Save](#3-cloud-save)
4. [Meta-Progression](#4-meta-progression)
5. [Design Patterns](#5-design-patterns)
6. [Solution Comparison](#6-solution-comparison)
7. [Complete Save System Checklist](#7-complete-save-system-checklist)
8. [Sources](#8-sources)

---

## 1. Save System Architecture

### 1.1 Layers of a Professional Save System

A well-architected save system separates responsibilities into layers:

```
┌─────────────────────────────────┐
│   Presentation Layer            │  ← Save slot UI, confirmations
├─────────────────────────────────┤
│   Application Layer             │  ← SaveManager, orchestration
├─────────────────────────────────┤
│   Domain Layer                  │  ← ISaveable, SaveData structs
├─────────────────────────────────┤
│   Infrastructure Layer          │  ← File I/O, serialization, encryption
└─────────────────────────────────┘
```

### 1.2 ISaveable Interface + SaveManager

```csharp
// === Contract for any object that needs to be saved ===
public interface ISaveable
{
    string SaveId { get; }        // Unique identifier
    object CaptureState();         // Captures current state
    void RestoreState(object state); // Restores saved state
}

// === Main data container ===
[System.Serializable]
public class GameSaveData
{
    public int version = 1;
    public string timestamp;
    public Dictionary<string, object> stateDict = new();

    // Meta-progression (persists between runs)
    public MetaProgressionData metaProgression = new();

    // Runtime snapshot (state of the current run)
    public RuntimeStateData runtimeState = new();
}

[System.Serializable]
public class MetaProgressionData
{
    public int totalRuns;
    public int softCurrency;
    public int hardCurrency;
    public List<string> unlockedCharacters = new();
    public List<string> unlockedUpgrades = new();
    public List<string> completedAchievements = new();
    public Dictionary<string, float> statistics = new();
}

[System.Serializable]
public class RuntimeStateData
{
    public int currentLevel;
    public float playerHealth;
    public Vector3Serializable playerPosition;
    public List<InventoryItemData> inventory = new();
}
```

```csharp
// === SaveManager — central orchestrator ===
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [SerializeField] private SaveSettings settings;

    private ISaveSerializer _serializer;
    private ISaveStorage _storage;
    private ISaveEncryptor _encryptor;

    private GameSaveData _currentData;
    private readonly List<ISaveable> _saveables = new();

    // Events
    public event System.Action<GameSaveData> OnBeforeSave;
    public event System.Action<GameSaveData> OnAfterLoad;
    public event System.Action<string> OnSaveError;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Dependency injection via settings
        _serializer = settings.useJson
            ? new JsonSaveSerializer()
            : new BinarySaveSerializer() as ISaveSerializer;
        _storage = new FileSystemStorage(GetSavePath());
        _encryptor = settings.encryptSaves
            ? new AesSaveEncryptor(settings.encryptionKey)
            : null;
    }

    public void Register(ISaveable saveable) => _saveables.Add(saveable);
    public void Unregister(ISaveable saveable) => _saveables.Remove(saveable);

    // ---------- SAVE ----------
    public async Task<bool> SaveGameAsync(int slot = 0)
    {
        try
        {
            _currentData ??= new GameSaveData();
            _currentData.timestamp = System.DateTime.UtcNow.ToString("o");

            OnBeforeSave?.Invoke(_currentData);

            // Capture state from all registered ISaveables
            foreach (var saveable in _saveables)
            {
                _currentData.stateDict[saveable.SaveId] = saveable.CaptureState();
            }

            // Serialize
            byte[] data = _serializer.Serialize(_currentData);

            // Encrypt (optional)
            if (_encryptor != null)
                data = _encryptor.Encrypt(data);

            // Persist
            string fileName = $"save_slot_{slot}.dat";
            await _storage.WriteAsync(fileName, data);

            Debug.Log($"[SaveManager] Game saved in slot {slot}");
            return true;
        }
        catch (System.Exception ex)
        {
            OnSaveError?.Invoke(ex.Message);
            Debug.LogError($"[SaveManager] Save error: {ex.Message}");
            return false;
        }
    }

    // ---------- LOAD ----------
    public async Task<bool> LoadGameAsync(int slot = 0)
    {
        try
        {
            string fileName = $"save_slot_{slot}.dat";
            byte[] data = await _storage.ReadAsync(fileName);

            if (data == null || data.Length == 0) return false;

            // Decrypt
            if (_encryptor != null)
                data = _encryptor.Decrypt(data);

            // Deserialize
            _currentData = _serializer.Deserialize<GameSaveData>(data);

            // Version migration
            _currentData = SaveMigrator.Migrate(_currentData);

            // Restore state for all ISaveables
            foreach (var saveable in _saveables)
            {
                if (_currentData.stateDict.TryGetValue(saveable.SaveId, out var state))
                {
                    saveable.RestoreState(state);
                }
            }

            OnAfterLoad?.Invoke(_currentData);
            Debug.Log($"[SaveManager] Game loaded from slot {slot}");
            return true;
        }
        catch (System.Exception ex)
        {
            OnSaveError?.Invoke(ex.Message);
            Debug.LogError($"[SaveManager] Load error: {ex.Message}");
            return false;
        }
    }

    // ---------- DELETE ----------
    public async Task DeleteSaveAsync(int slot)
    {
        await _storage.DeleteAsync($"save_slot_{slot}.dat");
    }

    // ---------- SAVE PATH BY PLATFORM ----------
    public static string GetSavePath()
    {
#if UNITY_EDITOR
        return Path.Combine(Application.dataPath, "..", "Saves");
#elif UNITY_STANDALONE_WIN
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
            Application.companyName, Application.productName, "Saves");
#elif UNITY_STANDALONE_OSX
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
            "Library", "Application Support", Application.companyName,
            Application.productName, "Saves");
#elif UNITY_STANDALONE_LINUX
        return Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
            $".config/{Application.companyName}/{Application.productName}/Saves");
#elif UNITY_ANDROID || UNITY_IOS
        return Path.Combine(Application.persistentDataPath, "Saves");
#elif UNITY_SWITCH || UNITY_PS4 || UNITY_PS5 || UNITY_GAMECORE
        // Consoles use native APIs — each SDK has its own system
        return Application.persistentDataPath;
#else
        return Path.Combine(Application.persistentDataPath, "Saves");
#endif
    }
}
```

### 1.3 PlayerPrefs: When OK vs When to Avoid

```csharp
// ✅ OK for PlayerPrefs — lightweight settings and preferences
public static class PlayerSettings
{
    public static float MasterVolume
    {
        get => PlayerPrefs.GetFloat("settings_master_volume", 1f);
        set { PlayerPrefs.SetFloat("settings_master_volume", value); PlayerPrefs.Save(); }
    }

    public static int QualityLevel
    {
        get => PlayerPrefs.GetInt("settings_quality", 2);
        set { PlayerPrefs.SetInt("settings_quality", value); PlayerPrefs.Save(); }
    }

    public static string Language
    {
        get => PlayerPrefs.GetString("settings_language", "en");
        set { PlayerPrefs.SetString("settings_language", value); PlayerPrefs.Save(); }
    }
}

// ❌ AVOID PlayerPrefs for game data — no control, no structure
// - ~1MB limit on WebGL
// - Stored in the registry (Windows) — easy to edit
// - No support for complex types
// - No versioning
// - No native encryption
// - No backup / rollback
```

**Practical rule:** PlayerPrefs for *player settings* (volume, language, resolution). Custom system for *game data* (progress, inventory, save states).

### 1.4 Auto-Save Strategies

```csharp
public class AutoSaveController : MonoBehaviour
{
    [SerializeField] private float intervalSeconds = 120f; // every 2 min
    [SerializeField] private bool saveOnCheckpoint = true;
    [SerializeField] private bool saveOnSceneChange = true;
    [SerializeField] private bool saveOnApplicationPause = true;

    private float _timer;
    private bool _isDirty;

    private void OnEnable()
    {
        // Event-based triggers
        if (saveOnCheckpoint)
            CheckpointManager.OnCheckpointReached += HandleCheckpoint;
        if (saveOnSceneChange)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        CheckpointManager.OnCheckpointReached -= HandleCheckpoint;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Update()
    {
        // Interval-based auto-save
        _timer += Time.unscaledDeltaTime;
        if (_timer >= intervalSeconds)
        {
            _timer = 0f;
            TriggerAutoSave("interval");
        }
    }

    private void OnApplicationPause(bool paused)
    {
        // Mobile: save when the app goes to the background
        if (paused && saveOnApplicationPause)
            TriggerAutoSave("app_pause");
    }

    private void OnApplicationQuit()
    {
        // Always save when closing the game
        // Note: use sync save here — async may not complete
        SaveManager.Instance.SaveGameAsync(slot: 0).GetAwaiter().GetResult();
    }

    private void HandleCheckpoint(Vector3 position)
    {
        TriggerAutoSave("checkpoint");
    }

    private void HandleSceneLoaded(
        UnityEngine.SceneManagement.Scene scene,
        UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        if (mode == UnityEngine.SceneManagement.LoadSceneMode.Single)
            TriggerAutoSave("scene_change");
    }

    private async void TriggerAutoSave(string reason)
    {
        Debug.Log($"[AutoSave] Trigger: {reason}");
        await SaveManager.Instance.SaveGameAsync(slot: 0);
    }
}
```

### 1.5 Save File Location by Platform

| Platform | Typical path | Notes |
|---|---|---|
| **Windows** | `%LOCALAPPDATA%/Company/Game/Saves/` | Avoid `AppData/Roaming` unless roaming is needed |
| **macOS** | `~/Library/Application Support/Company/Game/Saves/` | `Application.persistentDataPath` already resolves this |
| **Linux** | `~/.config/Company/Game/Saves/` | Follow the XDG Base Directory spec |
| **Android** | `Application.persistentDataPath` | Internal storage, sandboxed per app |
| **iOS** | `Application.persistentDataPath` | Included in iCloud backup automatically |
| **WebGL** | IndexedDB via `Application.persistentDataPath` | ~1MB default limit, player can clear it |
| **Steam** | Use Steam Cloud API | Path configured in Steamworks |
| **Consoles** | SDK-specific APIs | Switch/PS/Xbox each have their own system |

---

## 2. Serialization

### 2.1 JSON: JsonUtility vs Newtonsoft JSON vs Custom

```csharp
// === Serialization interfaces ===
public interface ISaveSerializer
{
    byte[] Serialize<T>(T data);
    T Deserialize<T>(byte[] data);
}

// === 1. JsonUtility (built-in Unity) ===
public class UnityJsonSerializer : ISaveSerializer
{
    public byte[] Serialize<T>(T data)
    {
        string json = JsonUtility.ToJson(data, prettyPrint: true);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data)
    {
        string json = System.Text.Encoding.UTF8.GetString(data);
        return JsonUtility.FromJson<T>(json);
    }
}
// Limitations: no Dictionary, no polymorphism, no null fields,
// no properties (only public fields or [SerializeField])

// === 2. Newtonsoft JSON (via com.unity.nuget.newtonsoft-json) ===
using Newtonsoft.Json;

public class NewtonsoftJsonSerializer : ISaveSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,  // supports polymorphism
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        Converters = new List<JsonConverter>
        {
            new Vector3Converter(),
            new QuaternionConverter(),
            new ColorConverter()
        }
    };

    public byte[] Serialize<T>(T data)
    {
        string json = JsonConvert.SerializeObject(data, Settings);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    public T Deserialize<T>(byte[] data)
    {
        string json = System.Text.Encoding.UTF8.GetString(data);
        return JsonConvert.DeserializeObject<T>(json, Settings);
    }
}

// Custom converter for Vector3 (not natively serialized)
public class Vector3Converter : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("x"); writer.WriteValue(value.x);
        writer.WritePropertyName("y"); writer.WriteValue(value.y);
        writer.WritePropertyName("z"); writer.WriteValue(value.z);
        writer.WriteEndObject();
    }

    public override Vector3 ReadJson(JsonReader reader, System.Type objectType,
        Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = Newtonsoft.Json.Linq.JObject.Load(reader);
        return new Vector3(
            obj["x"]?.Value<float>() ?? 0f,
            obj["y"]?.Value<float>() ?? 0f,
            obj["z"]?.Value<float>() ?? 0f);
    }
}

// === 3. Binary Serialization (MessagePack / MemoryPack) ===
// Note: do NOT use BinaryFormatter — deprecated and will be removed from .NET
using MessagePack;

public class MessagePackSerializer : ISaveSerializer
{
    public byte[] Serialize<T>(T data)
    {
        return MessagePack.MessagePackSerializer.Serialize(data);
    }

    public T Deserialize<T>(byte[] data)
    {
        return MessagePack.MessagePackSerializer.Deserialize<T>(data);
    }
}
// Requires [MessagePackObject] and [Key(n)] attributes on classes
```

### 2.2 Handling Complex Types

```csharp
// === Dictionary serialization (JsonUtility does not support it) ===
[System.Serializable]
public class SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver
{
    [SerializeField] private List<TKey> keys = new();
    [SerializeField] private List<TValue> values = new();

    private Dictionary<TKey, TValue> _dict = new();

    public Dictionary<TKey, TValue> Dict => _dict;

    public void OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();
        foreach (var kvp in _dict)
        {
            keys.Add(kvp.Key);
            values.Add(kvp.Value);
        }
    }

    public void OnAfterDeserialize()
    {
        _dict = new Dictionary<TKey, TValue>();
        for (int i = 0; i < Mathf.Min(keys.Count, values.Count); i++)
        {
            _dict[keys[i]] = values[i];
        }
    }
}

// === Polymorphism with Newtonsoft ===
[JsonObject]
public abstract class ItemData
{
    public string id;
    public string displayName;
    public int stackCount;
}

[JsonObject]
public class WeaponData : ItemData
{
    public float damage;
    public float attackSpeed;
}

[JsonObject]
public class ConsumableData : ItemData
{
    public float healAmount;
    public float duration;
}

// With TypeNameHandling.Auto, Newtonsoft saves the concrete type:
// { "$type": "WeaponData, Assembly-CSharp", "damage": 15.0, ... }

// === Serializable Vector3 (for JsonUtility) ===
[System.Serializable]
public struct Vector3Serializable
{
    public float x, y, z;

    public Vector3Serializable(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new(x, y, z);

    public static implicit operator Vector3Serializable(Vector3 v) => new(v);
    public static implicit operator Vector3(Vector3Serializable v) => v.ToVector3();
}
```

### 2.3 ScriptableObjects and Serialization

```csharp
// ScriptableObjects are great as DATA DEFINITIONS (read-only)
// but should not be modified at runtime for save — use separate data classes

[CreateAssetMenu(menuName = "Game/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    public string characterId;
    public string displayName;
    public Sprite portrait;
    public int baseHealth;
    public int baseDamage;
    // ... static design data
}

// The save data references by ID, not by ScriptableObject
[System.Serializable]
public class CharacterSaveData
{
    public string characterId;  // reference to the SO
    public int level;
    public int experience;
    public List<string> equippedItemIds;

    // When loading, resolve the SO via registry:
    public CharacterDefinition GetDefinition()
    {
        return CharacterRegistry.Instance.GetById(characterId);
    }
}

// === Registry to resolve IDs → ScriptableObjects ===
public class CharacterRegistry : MonoBehaviour
{
    public static CharacterRegistry Instance { get; private set; }

    [SerializeField] private List<CharacterDefinition> allCharacters;
    private Dictionary<string, CharacterDefinition> _lookup;

    private void Awake()
    {
        Instance = this;
        _lookup = allCharacters.ToDictionary(c => c.characterId);
    }

    public CharacterDefinition GetById(string id) =>
        _lookup.TryGetValue(id, out var def) ? def : null;
}
```

### 2.4 Save File Versioning (Migrations)

```csharp
public static class SaveMigrator
{
    // Current save schema version
    public const int CURRENT_VERSION = 4;

    public static GameSaveData Migrate(GameSaveData data)
    {
        if (data.version == CURRENT_VERSION) return data;

        Debug.Log($"[SaveMigrator] Migrating v{data.version} → v{CURRENT_VERSION}");

        // Apply incremental migrations
        while (data.version < CURRENT_VERSION)
        {
            data = data.version switch
            {
                1 => MigrateV1ToV2(data),
                2 => MigrateV2ToV3(data),
                3 => MigrateV3ToV4(data),
                _ => throw new System.Exception(
                    $"Unknown migration for version {data.version}")
            };
        }

        return data;
    }

    // v1→v2: Added currency system
    private static GameSaveData MigrateV1ToV2(GameSaveData data)
    {
        data.metaProgression.softCurrency = 0;
        data.metaProgression.hardCurrency = 0;
        data.version = 2;
        return data;
    }

    // v2→v3: Renamed "coins" to "softCurrency" in statistics
    private static GameSaveData MigrateV2ToV3(GameSaveData data)
    {
        if (data.metaProgression.statistics.TryGetValue("coins", out float val))
        {
            data.metaProgression.statistics["totalSoftCurrencyEarned"] = val;
            data.metaProgression.statistics.Remove("coins");
        }
        data.version = 3;
        return data;
    }

    // v3→v4: Added achievement system
    private static GameSaveData MigrateV3ToV4(GameSaveData data)
    {
        data.metaProgression.completedAchievements ??= new List<string>();
        data.version = 4;
        return data;
    }
}
```

---

## 3. Cloud Save

### 3.1 Unity Cloud Save (UGS)

```csharp
using Unity.Services.CloudSave;
using Unity.Services.Core;
using Unity.Services.Authentication;

public class UnityCloudSaveProvider : ICloudSaveProvider
{
    public async Task InitializeAsync()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    public async Task SaveAsync(string key, GameSaveData data)
    {
        var serialized = JsonConvert.SerializeObject(data);
        var saveDict = new Dictionary<string, object> { { key, serialized } };
        await CloudSaveService.Instance.Data.Player.SaveAsync(saveDict);
    }

    public async Task<GameSaveData> LoadAsync(string key)
    {
        var query = new HashSet<string> { key };
        var results = await CloudSaveService.Instance.Data.Player.LoadAsync(query);

        if (results.TryGetValue(key, out var item))
        {
            string json = item.Value.GetAs<string>();
            return JsonConvert.DeserializeObject<GameSaveData>(json);
        }
        return null;
    }

    public async Task DeleteAsync(string key)
    {
        await CloudSaveService.Instance.Data.Player.DeleteAsync(key);
    }
}
```

### 3.2 Firebase Realtime Database / Firestore

```csharp
using Firebase.Firestore;
using Firebase.Extensions;

public class FirestoreCloudSaveProvider : ICloudSaveProvider
{
    private FirebaseFirestore _db;
    private string _userId;

    public async Task InitializeAsync()
    {
        var app = Firebase.FirebaseApp.DefaultInstance;
        _db = FirebaseFirestore.DefaultInstance;
        _userId = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser?.UserId
            ?? "anonymous";
    }

    public async Task SaveAsync(string key, GameSaveData data)
    {
        var docRef = _db.Collection("saves").Document($"{_userId}_{key}");
        var saveDoc = new Dictionary<string, object>
        {
            { "data", JsonConvert.SerializeObject(data) },
            { "timestamp", FieldValue.ServerTimestamp },
            { "version", data.version },
            { "platform", Application.platform.ToString() }
        };
        await docRef.SetAsync(saveDoc);
    }

    public async Task<GameSaveData> LoadAsync(string key)
    {
        var docRef = _db.Collection("saves").Document($"{_userId}_{key}");
        var snapshot = await docRef.GetSnapshotAsync();

        if (snapshot.Exists)
        {
            string json = snapshot.GetValue<string>("data");
            return JsonConvert.DeserializeObject<GameSaveData>(json);
        }
        return null;
    }

    public async Task DeleteAsync(string key)
    {
        var docRef = _db.Collection("saves").Document($"{_userId}_{key}");
        await docRef.DeleteAsync();
    }
}
```

### 3.3 Steam Cloud

```csharp
using Steamworks;

public class SteamCloudSaveProvider : ICloudSaveProvider
{
    public Task InitializeAsync()
    {
        if (!SteamManager.Initialized)
            throw new System.Exception("Steam not initialized");
        return Task.CompletedTask;
    }

    public Task SaveAsync(string key, GameSaveData data)
    {
        string json = JsonConvert.SerializeObject(data);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);

        bool success = SteamRemoteStorage.FileWrite(key, bytes, bytes.Length);
        if (!success)
            Debug.LogError($"[SteamCloud] Failed to save: {key}");

        return Task.CompletedTask;
    }

    public Task<GameSaveData> LoadAsync(string key)
    {
        if (!SteamRemoteStorage.FileExists(key))
            return Task.FromResult<GameSaveData>(null);

        int size = SteamRemoteStorage.GetFileSize(key);
        byte[] bytes = new byte[size];
        int bytesRead = SteamRemoteStorage.FileRead(key, bytes, size);

        string json = System.Text.Encoding.UTF8.GetString(bytes, 0, bytesRead);
        var data = JsonConvert.DeserializeObject<GameSaveData>(json);
        return Task.FromResult(data);
    }

    public Task DeleteAsync(string key)
    {
        SteamRemoteStorage.FileDelete(key);
        return Task.CompletedTask;
    }
}
```

### 3.4 Conflict Resolution Strategies

```csharp
public enum ConflictStrategy
{
    LatestTimestamp,     // Most recent wins
    LongestPlaytime,    // Highest playtime wins
    HighestProgress,    // Highest progress wins
    ManualChoice,       // Player chooses
    MergeFields         // Field-by-field merge
}

public class CloudSyncManager
{
    private readonly ICloudSaveProvider _cloudProvider;
    private readonly ISaveStorage _localStorage;

    public async Task<GameSaveData> SyncAndResolve(
        string key, ConflictStrategy strategy)
    {
        var localData = await LoadLocal(key);
        var cloudData = await _cloudProvider.LoadAsync(key);

        // No conflict
        if (localData == null) return cloudData;
        if (cloudData == null) return localData;

        // No difference
        if (localData.timestamp == cloudData.timestamp) return localData;

        // Conflict resolution
        return strategy switch
        {
            ConflictStrategy.LatestTimestamp =>
                CompareTimestamp(localData, cloudData) > 0 ? localData : cloudData,

            ConflictStrategy.LongestPlaytime =>
                GetPlaytime(localData) >= GetPlaytime(cloudData) ? localData : cloudData,

            ConflictStrategy.HighestProgress =>
                GetProgressScore(localData) >= GetProgressScore(cloudData)
                    ? localData : cloudData,

            ConflictStrategy.MergeFields =>
                MergeData(localData, cloudData),

            ConflictStrategy.ManualChoice =>
                await PromptUserChoice(localData, cloudData),

            _ => localData
        };
    }

    // Smart merge: picks the "best" from each field
    private GameSaveData MergeData(GameSaveData local, GameSaveData cloud)
    {
        var merged = CompareTimestamp(local, cloud) > 0 ? local : cloud;

        // Meta-progression: additive merge (take the max of each)
        merged.metaProgression.softCurrency = Mathf.Max(
            local.metaProgression.softCurrency,
            cloud.metaProgression.softCurrency);

        // Unlocks: union of both
        merged.metaProgression.unlockedCharacters = local.metaProgression
            .unlockedCharacters
            .Union(cloud.metaProgression.unlockedCharacters)
            .ToList();

        // Achievements: union
        merged.metaProgression.completedAchievements = local.metaProgression
            .completedAchievements
            .Union(cloud.metaProgression.completedAchievements)
            .ToList();

        // Statistics: take the highest value
        foreach (var key in cloud.metaProgression.statistics.Keys)
        {
            if (!merged.metaProgression.statistics.ContainsKey(key) ||
                cloud.metaProgression.statistics[key] > merged.metaProgression.statistics[key])
            {
                merged.metaProgression.statistics[key] = cloud.metaProgression.statistics[key];
            }
        }

        return merged;
    }

    private int CompareTimestamp(GameSaveData a, GameSaveData b)
    {
        var ta = System.DateTime.Parse(a.timestamp);
        var tb = System.DateTime.Parse(b.timestamp);
        return ta.CompareTo(tb);
    }

    private float GetPlaytime(GameSaveData d) =>
        d.metaProgression.statistics.GetValueOrDefault("totalPlaytime", 0f);

    private float GetProgressScore(GameSaveData d) =>
        d.metaProgression.totalRuns +
        d.metaProgression.unlockedCharacters.Count * 10 +
        d.metaProgression.completedAchievements.Count * 5;
}
```

---

## 4. Meta-Progression

### 4.1 Persistent Unlocks

```csharp
public class UnlockManager
{
    private MetaProgressionData _meta;

    public event System.Action<string> OnItemUnlocked;
    public event System.Action<string, int> OnCurrencyChanged;

    public UnlockManager(MetaProgressionData meta)
    {
        _meta = meta;
    }

    // === Characters ===
    public bool IsCharacterUnlocked(string id) =>
        _meta.unlockedCharacters.Contains(id);

    public bool TryUnlockCharacter(string id, int cost)
    {
        if (IsCharacterUnlocked(id)) return false;
        if (_meta.softCurrency < cost) return false;

        _meta.softCurrency -= cost;
        _meta.unlockedCharacters.Add(id);

        OnCurrencyChanged?.Invoke("soft", _meta.softCurrency);
        OnItemUnlocked?.Invoke(id);
        return true;
    }

    // === Upgrades (with levels) ===
    private Dictionary<string, int> _upgradeRegistry = new();

    public int GetUpgradeLevel(string upgradeId) =>
        _upgradeRegistry.GetValueOrDefault(upgradeId, 0);

    public bool TryPurchaseUpgrade(string upgradeId, int maxLevel, int cost)
    {
        int current = GetUpgradeLevel(upgradeId);
        if (current >= maxLevel) return false;
        if (_meta.softCurrency < cost) return false;

        _meta.softCurrency -= cost;
        _upgradeRegistry[upgradeId] = current + 1;

        if (!_meta.unlockedUpgrades.Contains(upgradeId))
            _meta.unlockedUpgrades.Add(upgradeId);

        OnCurrencyChanged?.Invoke("soft", _meta.softCurrency);
        return true;
    }
}
```

### 4.2 Currency System (Soft / Hard)

```csharp
public class CurrencyManager
{
    private MetaProgressionData _meta;

    public event System.Action<CurrencyType, int, int> OnCurrencyChanged; // type, old, new

    public enum CurrencyType { Soft, Hard }

    public CurrencyManager(MetaProgressionData meta)
    {
        _meta = meta;
    }

    public int GetBalance(CurrencyType type) => type switch
    {
        CurrencyType.Soft => _meta.softCurrency,
        CurrencyType.Hard => _meta.hardCurrency,
        _ => 0
    };

    public bool CanAfford(CurrencyType type, int amount) =>
        GetBalance(type) >= amount;

    public bool TrySpend(CurrencyType type, int amount)
    {
        if (!CanAfford(type, amount)) return false;

        int oldBalance = GetBalance(type);

        switch (type)
        {
            case CurrencyType.Soft:
                _meta.softCurrency -= amount;
                break;
            case CurrencyType.Hard:
                _meta.hardCurrency -= amount;
                break;
        }

        OnCurrencyChanged?.Invoke(type, oldBalance, GetBalance(type));

        // Track for analytics
        TrackStatistic($"total_{type}_spent", amount);
        return true;
    }

    public void Add(CurrencyType type, int amount)
    {
        int oldBalance = GetBalance(type);

        switch (type)
        {
            case CurrencyType.Soft:
                _meta.softCurrency += amount;
                break;
            case CurrencyType.Hard:
                _meta.hardCurrency += amount;
                break;
        }

        OnCurrencyChanged?.Invoke(type, oldBalance, GetBalance(type));
        TrackStatistic($"total_{type}_earned", amount);
    }

    private void TrackStatistic(string key, float addValue)
    {
        _meta.statistics.TryGetValue(key, out float current);
        _meta.statistics[key] = current + addValue;
    }
}
```

### 4.3 Achievement System

```csharp
// === Achievement Definition (ScriptableObject) ===
[CreateAssetMenu(menuName = "Game/Achievement")]
public class AchievementDefinition : ScriptableObject
{
    public string achievementId;
    public string title;
    public string description;
    public Sprite icon;
    public AchievementType type;
    public float targetValue;       // e.g. "Kill 100 enemies" → 100
    public string trackingStatKey;  // key in the statistics dict
    public int rewardCurrency;
    public CurrencyManager.CurrencyType rewardType;

    public enum AchievementType
    {
        Cumulative,     // Progressive sum (total kills)
        SingleRun,      // In a single run (speed run)
        Binary          // Did it or not (defeated boss X)
    }
}

// === Achievement Manager ===
public class AchievementManager
{
    private readonly List<AchievementDefinition> _definitions;
    private MetaProgressionData _meta;
    private CurrencyManager _currencyManager;

    public event System.Action<AchievementDefinition> OnAchievementUnlocked;

    public AchievementManager(
        List<AchievementDefinition> definitions,
        MetaProgressionData meta,
        CurrencyManager currencyManager)
    {
        _definitions = definitions;
        _meta = meta;
        _currencyManager = currencyManager;
    }

    // Call this whenever a stat changes
    public void EvaluateAchievements()
    {
        foreach (var def in _definitions)
        {
            if (_meta.completedAchievements.Contains(def.achievementId))
                continue;

            if (IsConditionMet(def))
            {
                _meta.completedAchievements.Add(def.achievementId);
                _currencyManager.Add(def.rewardType, def.rewardCurrency);
                OnAchievementUnlocked?.Invoke(def);
                Debug.Log($"[Achievement] Unlocked: {def.title}");
            }
        }
    }

    private bool IsConditionMet(AchievementDefinition def)
    {
        if (!_meta.statistics.TryGetValue(def.trackingStatKey, out float current))
            return false;
        return current >= def.targetValue;
    }

    public float GetProgress(AchievementDefinition def)
    {
        if (_meta.completedAchievements.Contains(def.achievementId))
            return 1f;
        _meta.statistics.TryGetValue(def.trackingStatKey, out float current);
        return Mathf.Clamp01(current / def.targetValue);
    }

    public bool IsCompleted(string achievementId) =>
        _meta.completedAchievements.Contains(achievementId);
}
```

### 4.4 Statistics Tracking

```csharp
public class StatisticsTracker
{
    private MetaProgressionData _meta;
    private AchievementManager _achievementManager;

    // Current run stats (not persisted if the run is abandoned)
    private Dictionary<string, float> _runStats = new();

    public StatisticsTracker(MetaProgressionData meta, AchievementManager achievements)
    {
        _meta = meta;
        _achievementManager = achievements;
    }

    // === Tracking during gameplay ===
    public void IncrementStat(string key, float amount = 1f)
    {
        // Run stats
        _runStats.TryGetValue(key, out float runCurrent);
        _runStats[key] = runCurrent + amount;

        // Global stats (persistent)
        _meta.statistics.TryGetValue(key, out float globalCurrent);
        _meta.statistics[key] = globalCurrent + amount;

        // Re-evaluate achievements
        _achievementManager.EvaluateAchievements();
    }

    public void SetStat(string key, float value)
    {
        _runStats[key] = value;

        // For "max" stats (best time, highest combo)
        _meta.statistics.TryGetValue(key, out float current);
        if (value > current)
        {
            _meta.statistics[key] = value;
            _achievementManager.EvaluateAchievements();
        }
    }

    // Called at the end of each run
    public void CommitRunStats()
    {
        _meta.totalRuns++;
        _meta.statistics["totalRuns"] = _meta.totalRuns;
        _runStats.Clear();
    }

    // Common stats to track
    public void TrackKill(string enemyType)
    {
        IncrementStat("totalKills");
        IncrementStat($"kills_{enemyType}");
    }

    public void TrackPlaytime(float deltaTime)
    {
        IncrementStat("totalPlaytime", deltaTime);
        IncrementStat("runPlaytime", deltaTime);
    }

    public void TrackLevelCompleted(int level)
    {
        IncrementStat("totalLevelsCompleted");
        SetStat("highestLevelReached", level);
    }

    // Read
    public float GetGlobalStat(string key) =>
        _meta.statistics.GetValueOrDefault(key, 0f);

    public float GetRunStat(string key) =>
        _runStats.GetValueOrDefault(key, 0f);
}
```

### 4.5 Prestige / Reset Mechanics

```csharp
public class PrestigeSystem
{
    private MetaProgressionData _meta;
    private CurrencyManager _currency;

    public event System.Action<int, int> OnPrestige; // newLevel, bonusReward

    [System.Serializable]
    public class PrestigeConfig
    {
        public int[] requiredRunsPerLevel = { 5, 10, 20, 35, 50, 75, 100 };
        public float[] bonusMultiplierPerLevel = { 1.1f, 1.25f, 1.5f, 1.75f, 2.0f, 2.5f, 3.0f };
        public int baseCurrencyReward = 500;
    }

    private PrestigeConfig _config;

    public PrestigeSystem(
        MetaProgressionData meta,
        CurrencyManager currency,
        PrestigeConfig config)
    {
        _meta = meta;
        _currency = currency;
        _config = config;
    }

    public int CurrentPrestigeLevel =>
        (int)_meta.statistics.GetValueOrDefault("prestigeLevel", 0);

    public float CurrentBonusMultiplier
    {
        get
        {
            int level = CurrentPrestigeLevel;
            if (level <= 0) return 1f;
            int idx = Mathf.Min(level - 1, _config.bonusMultiplierPerLevel.Length - 1);
            return _config.bonusMultiplierPerLevel[idx];
        }
    }

    public bool CanPrestige()
    {
        int level = CurrentPrestigeLevel;
        if (level >= _config.requiredRunsPerLevel.Length) return false;
        return _meta.totalRuns >= _config.requiredRunsPerLevel[level];
    }

    public void DoPrestige()
    {
        if (!CanPrestige()) return;

        int newLevel = CurrentPrestigeLevel + 1;
        int reward = Mathf.RoundToInt(
            _config.baseCurrencyReward * CurrentBonusMultiplier);

        // Selective reset — keeps achievements and permanent unlocks
        _meta.softCurrency = 0;
        _meta.totalRuns = 0;
        _meta.statistics["prestigeLevel"] = newLevel;

        // Prestige reward
        _currency.Add(CurrencyManager.CurrencyType.Hard, reward);

        // NOT reset:
        // - _meta.unlockedCharacters (permanent)
        // - _meta.completedAchievements (permanent)
        // - _meta.hardCurrency (premium)
        // - _meta.statistics with "lifetime_" prefix

        OnPrestige?.Invoke(newLevel, reward);
    }
}
```

---

## 5. Design Patterns

### 5.1 Repository Pattern for Save Data

```csharp
// Generic repository interface
public interface ISaveRepository<T> where T : class
{
    Task<T> GetAsync(string id);
    Task SaveAsync(string id, T entity);
    Task DeleteAsync(string id);
    Task<IEnumerable<string>> ListKeysAsync();
}

// File system implementation
public class FileSaveRepository<T> : ISaveRepository<T> where T : class
{
    private readonly string _basePath;
    private readonly ISaveSerializer _serializer;

    public FileSaveRepository(string basePath, ISaveSerializer serializer)
    {
        _basePath = basePath;
        _serializer = serializer;
        Directory.CreateDirectory(basePath);
    }

    public async Task<T> GetAsync(string id)
    {
        string path = GetPath(id);
        if (!File.Exists(path)) return null;

        byte[] data = await File.ReadAllBytesAsync(path);
        return _serializer.Deserialize<T>(data);
    }

    public async Task SaveAsync(string id, T entity)
    {
        byte[] data = _serializer.Serialize(entity);

        // Atomic write: save to temp, then move
        string path = GetPath(id);
        string tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, data);
        File.Move(tempPath, path, overwrite: true);
    }

    public Task DeleteAsync(string id)
    {
        string path = GetPath(id);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListKeysAsync()
    {
        var keys = Directory.GetFiles(_basePath, "*.sav")
            .Select(Path.GetFileNameWithoutExtension);
        return Task.FromResult(keys);
    }

    private string GetPath(string id) => Path.Combine(_basePath, $"{id}.sav");
}

// Usage:
// var repo = new FileSaveRepository<GameSaveData>(savePath, serializer);
// await repo.SaveAsync("slot_0", currentSave);
// var loaded = await repo.GetAsync("slot_0");
```

### 5.2 Memento Pattern for State Snapshots

```csharp
// Ideal for undo/redo, rewind mechanics, or debugging
public class GameStateMemento
{
    public readonly string Id;
    public readonly System.DateTime Timestamp;
    public readonly byte[] SerializedState;

    public GameStateMemento(string id, byte[] state)
    {
        Id = id;
        Timestamp = System.DateTime.UtcNow;
        SerializedState = state;
    }
}

public class StateSnapshotManager
{
    private readonly ISaveSerializer _serializer;
    private readonly LinkedList<GameStateMemento> _snapshots = new();
    private readonly int _maxSnapshots;

    public StateSnapshotManager(ISaveSerializer serializer, int maxSnapshots = 30)
    {
        _serializer = serializer;
        _maxSnapshots = maxSnapshots;
    }

    // Capture a snapshot of the current state
    public void CaptureSnapshot(string label, GameSaveData state)
    {
        byte[] data = _serializer.Serialize(state);
        var memento = new GameStateMemento(label, data);

        _snapshots.AddLast(memento);

        // Ring buffer: remove oldest
        while (_snapshots.Count > _maxSnapshots)
            _snapshots.RemoveFirst();
    }

    // Restore the most recent snapshot (undo)
    public GameSaveData RestoreLatest()
    {
        if (_snapshots.Count == 0) return null;

        var memento = _snapshots.Last.Value;
        _snapshots.RemoveLast();
        return _serializer.Deserialize<GameSaveData>(memento.SerializedState);
    }

    // Restore snapshot by label
    public GameSaveData RestoreByLabel(string label)
    {
        var node = _snapshots.Last;
        while (node != null)
        {
            if (node.Value.Id == label)
                return _serializer.Deserialize<GameSaveData>(node.Value.SerializedState);
            node = node.Previous;
        }
        return null;
    }

    public int SnapshotCount => _snapshots.Count;
}
```

### 5.3 Separation Between Runtime State and Persistent State

```csharp
// === Runtime State: exists only during gameplay, discarded on game over ===
public class RuntimeGameState
{
    // Player state in the current run
    public float currentHealth;
    public float maxHealth;
    public Vector3 position;
    public int currentLevel;
    public float runTimer;

    // Temporary run inventory
    public List<RuntimeItem> inventory = new();

    // Active power-ups (do not persist)
    public List<ActiveBuff> activeBuffs = new();

    // Cache of calculated data
    public float cachedDamageMultiplier;

    // NOT saved — recalculated on run start
    [System.NonSerialized] public int currentCombo;
    [System.NonSerialized] public float comboTimer;
}

// === Persistent State: survives between sessions ===
// (Already defined above as MetaProgressionData)

// === Mediator that connects the two ===
public class GameStateMediator
{
    public RuntimeGameState Runtime { get; private set; }
    public MetaProgressionData Persistent { get; private set; }

    public GameStateMediator(MetaProgressionData persistent)
    {
        Persistent = persistent;
    }

    // Start a new run — creates runtime state based on persistent
    public void StartNewRun(string characterId)
    {
        var charDef = CharacterRegistry.Instance.GetById(characterId);
        var prestige = new PrestigeSystem(Persistent, null, null);
        float multiplier = prestige.CurrentBonusMultiplier;

        Runtime = new RuntimeGameState
        {
            maxHealth = charDef.baseHealth * multiplier,
            currentHealth = charDef.baseHealth * multiplier,
            position = Vector3.zero,
            currentLevel = 0,
            runTimer = 0f,
            cachedDamageMultiplier = multiplier
        };
    }

    // End of run — commits results to persistent
    public void EndRun(bool victory)
    {
        Persistent.totalRuns++;
        Persistent.statistics["totalPlaytime"] =
            Persistent.statistics.GetValueOrDefault("totalPlaytime") + Runtime.runTimer;

        if (victory)
        {
            Persistent.statistics["totalVictories"] =
                Persistent.statistics.GetValueOrDefault("totalVictories") + 1;
        }

        // Currency earned in the run goes to persistent
        // (assuming runtime has a field for this)

        Runtime = null; // Clear runtime
    }
}
```

---

## 6. Solution Comparison

### Serialization

| Aspect | JsonUtility | Newtonsoft JSON | MessagePack | BinaryFormatter |
|---|---|---|---|---|
| **Performance** | Fast | Moderate | Very fast | Slow |
| **File size** | Medium | Medium-Large | Small | Medium |
| **Readability** | Yes (JSON) | Yes (JSON) | No (binary) | No (binary) |
| **Dictionary** | ❌ | ✅ | ✅ | ✅ |
| **Polymorphism** | ❌ | ✅ | ✅ | ✅ |
| **Null handling** | ❌ | ✅ | ✅ | ✅ |
| **Dependency** | Built-in | NuGet package | NuGet package | ⚠️ Deprecated |
| **Debug-friendly** | ✅ | ✅ | ❌ | ❌ |
| **Recommendation** | Rapid prototyping | **Production (best cost-benefit)** | High performance | **DO NOT use** |

### Cloud Save

| Aspect | Unity Cloud Save | Firebase Firestore | Steam Cloud |
|---|---|---|---|
| **Setup** | Easy (UGS) | Moderate | Moderate |
| **Cost** | Generous free tier | Free tier + pay-as-you-go | Free (with Steam) |
| **Offline support** | ❌ (requires internet) | ✅ (local cache) | ✅ (automatic sync) |
| **Conflict resolution** | Server-side (last write wins) | Transactions + merge | Automatic + callback |
| **Multi-platform** | ✅ All | ✅ All | ❌ Steam only |
| **Max storage** | 50MB/player | 1GB free | 1GB/game default |
| **Recommendation** | Mobile/cross-platform | Custom backend | PC via Steam |

### Auto-Save Strategy

| Strategy | Pros | Cons | Best for |
|---|---|---|---|
| **Interval** (timer) | Predictable, simple | May save at a bad moment | RPGs, sandbox |
| **Event-based** | Saves at logical moments | May lose progress between events | Action, puzzles |
| **Checkpoint** | Full designer control | Player can lose progress | Metroidvania, platformers |
| **Hybrid** | Combines advantages | More complex | **Recommended for production** |

---

## 7. Complete Save System Checklist

### Architecture
- [ ] ISaveable interface implemented
- [ ] SaveManager singleton with DontDestroyOnLoad
- [ ] Clear separation between runtime state and persistent state
- [ ] Repository pattern for data access
- [ ] Dependency injection for serializer/storage/encryptor

### Serialization
- [ ] Serializer chosen (Newtonsoft recommended for production)
- [ ] Custom converters for Unity types (Vector3, Quaternion, Color)
- [ ] SerializableDictionary implemented (if using JsonUtility)
- [ ] Round-trip tests (serialize → deserialize → compare)

### Versioning
- [ ] `version` field in save data
- [ ] Incremental migration system
- [ ] Migration tests from each previous version
- [ ] Graceful fallback for corrupted saves

### Security
- [ ] AES encryption for save files (if required)
- [ ] Hash/checksum to detect tampering
- [ ] Encryption key not hardcoded in source
- [ ] Save data validated after deserialization

### Auto-Save
- [ ] Auto-save by configurable interval
- [ ] Save at checkpoints/important events
- [ ] Save on scene change
- [ ] Save on app pause (mobile) / game close
- [ ] Visual "saving..." indicator (prevent quit during save)

### Persistence
- [ ] Correct save path per platform
- [ ] Atomic writes (temp file + move)
- [ ] Backup of previous save before overwriting
- [ ] Multiple save slots

### Cloud Save
- [ ] Provider implemented (Unity/Firebase/Steam)
- [ ] Conflict resolution strategy defined
- [ ] Fallback to local save when offline
- [ ] Sync on login and at regular intervals

### Meta-Progression
- [ ] Currency system (soft + hard)
- [ ] Unlock system (characters, upgrades)
- [ ] Achievement system with tracking
- [ ] Statistics tracker (playtime, kills, etc.)
- [ ] Prestige/reset (if applicable)
- [ ] Separation between permanent and resettable data

### Basic Encryption

```csharp
using System.Security.Cryptography;

public class AesSaveEncryptor : ISaveEncryptor
{
    private readonly byte[] _key;
    private readonly byte[] _iv;

    public AesSaveEncryptor(string password)
    {
        // Derive key from password using PBKDF2
        using var deriveBytes = new Rfc2898DeriveBytes(
            password,
            salt: new byte[] { 0x53, 0x61, 0x76, 0x65, 0x47, 0x61, 0x6D, 0x65 },
            iterations: 10000,
            HashAlgorithmName.SHA256);
        _key = deriveBytes.GetBytes(32); // AES-256
        _iv = deriveBytes.GetBytes(16);
    }

    public byte[] Encrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    public byte[] Decrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = _iv;

        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(data, 0, data.Length);
    }
}

public interface ISaveEncryptor
{
    byte[] Encrypt(byte[] data);
    byte[] Decrypt(byte[] data);
}

public interface ISaveStorage
{
    Task WriteAsync(string fileName, byte[] data);
    Task<byte[]> ReadAsync(string fileName);
    Task DeleteAsync(string fileName);
}

public class FileSystemStorage : ISaveStorage
{
    private readonly string _basePath;

    public FileSystemStorage(string basePath)
    {
        _basePath = basePath;
        Directory.CreateDirectory(basePath);
    }

    public async Task WriteAsync(string fileName, byte[] data)
    {
        string path = Path.Combine(_basePath, fileName);
        string tempPath = path + ".tmp";
        string backupPath = path + ".bak";

        // Atomic write with backup
        await File.WriteAllBytesAsync(tempPath, data);

        if (File.Exists(path))
            File.Copy(path, backupPath, overwrite: true);

        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<byte[]> ReadAsync(string fileName)
    {
        string path = Path.Combine(_basePath, fileName);
        if (!File.Exists(path)) return null;
        return await File.ReadAllBytesAsync(path);
    }

    public Task DeleteAsync(string fileName)
    {
        string path = Path.Combine(_basePath, fileName);
        if (File.Exists(path)) File.Delete(path);

        string backup = path + ".bak";
        if (File.Exists(backup)) File.Delete(backup);

        return Task.CompletedTask;
    }
}
```

---

## 8. Sources

- [Game Save Systems: Complete Data Persistence Guide 2025](https://generalistprogrammer.com/tutorials/game-save-systems-complete-data-persistence-guide-2025)
- [Unity Binary Serialization Save System](https://outscal.com/blog/unity-binary-serialization-save-system)
- [Unity Save & Load: JSON Serialization Guide](https://uhiyama-lab.com/en/notes/unity/unity-save-load-json-serialization-guide/)
- [Unity Serialization Package Docs](https://docs.unity3d.com/Packages/com.unity.serialization@2.0/manual/index.html)
- [Unity Data Saving Step-by-Step Guide](https://www.somethingsblog.com/2024/10/21/unity-data-saving-a-step-by-step-guide/)
- [JSON vs Binary Serialization — Unity Discussions](https://discussions.unity.com/t/solved-future-proofing-the-save-function-json-or-binary-serialization/668333)
- [Unity Cloud Save Docs](https://docs.unity.com/ugs/en-us/manual/cloud-save/manual)
- [Firebase Cloud Storage for Unity](https://firebase.google.com/docs/storage/unity/start)
- [Steam Cloud Saves Setup Guide](https://hedgefield.blog/how-to-add-steam-cloud-saves-to-your-game/)
- [Google Play Saved Games in Unity](https://developer.android.com/games/pgs/unity/saved-games)
- [Unity Cloud Save Offline Mode Discussion](https://discussions.unity.com/t/does-cloud-save-also-support-offline-mode/1564168)
- [Achievement System with ScriptableObjects & PlayerPrefs](https://www.wayline.io/blog/unity-achievement-system-scriptableobjects-playerprefs)
- [How to Create Achievement System in Unity](https://medium.com/@joshua.wiscaver/how-to-create-a-basic-achievement-system-in-unity-a9aabe4ccd70)
- [Steamworks API: Stats and Achievements](https://medium.com/finite-guild/the-simple-guide-to-steamworks-api-in-unity-stats-and-achievements-70f155d19852)
- [How to Secure Save Data — Unity Discussions](https://discussions.unity.com/t/how-to-secure-save-data/883690)
- [Encryption in Save Files (AES)](https://giannisakritidis.com/blog/Using-Encryption-In-Save-Files/)
- [Easy Save 3: Encryption & Compression](https://docs.moodkie.com/easy-save-3/es3-guides/es3-encryption-compression/)
- [USaveSerializer — GitHub](https://github.com/TinyPlay/USaveSerializer)
- [Unity Cloud Save Interface (Multi-platform)](https://github.com/gilzoide/unity-cloud-save)

---

> **Last updated:** April 2026
