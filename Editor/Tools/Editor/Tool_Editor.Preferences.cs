#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region PREF CONSTANTS

        private const string PREF_WRITE_PREFIX = "GameDeck_";

        private static readonly string[] _sensitiveKeyPatterns = new[]
        {
            "token",
            "secret",
            "password",
            "license",
            "auth",
            "credential",
            "apikey",
            "api_key",
        };

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Reads a single EditorPrefs entry by key and returns its value as a string.
        /// </summary>
        /// <param name="key">EditorPrefs key to read.</param>
        /// <param name="type">Expected value type: string, int, float, or bool.</param>
        /// <returns>Text in the form "key = value", or an error if the key does not exist.</returns>
        [McpTool("editor-get-pref", Title = "Editor / Get Preference")]
        [Description("Gets an EditorPrefs value by key. Returns the stored string, int, float, or bool value.")]
        public ToolResponse GetPref(
            [Description("EditorPrefs key to read.")] string key,
            [Description("Expected type: string, int, float, bool. Default 'string'.")] string type = "string"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return ToolResponse.Error("key is required.");
                }

                if (IsSensitiveKey(key))
                {
                    return ToolResponse.Error($"Key '{key}' matches a sensitive pattern and cannot be read.");
                }

                if (!EditorPrefs.HasKey(key))
                {
                    return ToolResponse.Text($"Key '{key}' not found in EditorPrefs.");
                }

                string value = type.ToLowerInvariant() switch
                {
                    "int" => EditorPrefs.GetInt(key).ToString(),
                    "float" => EditorPrefs.GetFloat(key).ToString("F4"),
                    "bool" => EditorPrefs.GetBool(key).ToString(),
                    _ => EditorPrefs.GetString(key)
                };

                return ToolResponse.Text($"{key} = {value}");
            });
        }

        /// <summary>
        /// Writes a value to an EditorPrefs entry, converting from string to the specified type.
        /// </summary>
        /// <param name="key">EditorPrefs key to write.</param>
        /// <param name="value">String representation of the value to store.</param>
        /// <param name="type">Target value type: string, int, float, or bool.</param>
        /// <returns>Confirmation text showing the key, value, and type that were stored.</returns>
        [McpTool("editor-set-pref", Title = "Editor / Set Preference")]
        [Description("Sets an EditorPrefs value by key.")]
        public ToolResponse SetPref(
            [Description("EditorPrefs key to set.")] string key,
            [Description("Value to store.")] string value,
            [Description("Value type: string, int, float, bool. Default 'string'.")] string type = "string"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return ToolResponse.Error("key is required.");
                }

                if (!key.StartsWith(PREF_WRITE_PREFIX, StringComparison.Ordinal))
                {
                    return ToolResponse.Error($"Only keys with prefix '{PREF_WRITE_PREFIX}' can be written. Got: '{key}'");
                }

                switch (type.ToLowerInvariant())
                {
                    case "int":
                        if (!int.TryParse(value, out int intVal))
                        {
                            return ToolResponse.Error($"Cannot parse '{value}' as int.");
                        }
                        EditorPrefs.SetInt(key, intVal);
                        break;
                    case "float":
                        if (!float.TryParse(value, out float floatVal))
                        {
                            return ToolResponse.Error($"Cannot parse '{value}' as float.");
                        }
                        EditorPrefs.SetFloat(key, floatVal);
                        break;
                    case "bool":
                        if (!bool.TryParse(value, out bool boolVal))
                        {
                            return ToolResponse.Error($"Cannot parse '{value}' as bool. Use 'true' or 'false'.");
                        }
                        EditorPrefs.SetBool(key, boolVal);
                        break;
                    default:
                        EditorPrefs.SetString(key, value);
                        break;
                }

                return ToolResponse.Text($"Set {key} = {value} ({type})");
            });
        }

        /// <summary>
        /// Queries the Unity Editor for version, platform, file paths, play mode state,
        /// and active scene information.
        /// </summary>
        /// <returns>Formatted multi-line text with Unity Editor diagnostics.</returns>
        [McpTool("editor-info", Title = "Editor / Info")]
        [Description("Gets Unity Editor information including version, platform, data paths, " + "play mode state, and current scene.")]
        public ToolResponse EditorInfo()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Unity Editor Info:");
                sb.AppendLine($"  Version: {UnityEngine.Application.unityVersion}");
                sb.AppendLine($"  Platform: {UnityEngine.Application.platform}");
                sb.AppendLine($"  Data Path: {UnityEngine.Application.dataPath}");
                sb.AppendLine($"  Persistent Data: {UnityEngine.Application.persistentDataPath}");
                sb.AppendLine($"  Streaming Assets: {UnityEngine.Application.streamingAssetsPath}");
                sb.AppendLine($"  Is Playing: {EditorApplication.isPlaying}");
                sb.AppendLine($"  Is Compiling: {EditorApplication.isCompiling}");
                sb.AppendLine($"  Is Paused: {EditorApplication.isPaused}");

                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                sb.AppendLine($"  Active Scene: {scene.name} ({scene.path})");
                sb.AppendLine($"  Scene Count: {UnityEngine.SceneManagement.SceneManager.sceneCount}");
                sb.AppendLine($"  Build Target: {EditorUserBuildSettings.activeBuildTarget}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Returns <c>true</c> if the key matches any sensitive pattern (case-insensitive).
        /// </summary>
        /// <param name="key">The EditorPrefs key to check against sensitive patterns.</param>
        /// <returns><c>true</c> if the key contains a sensitive substring; <c>false</c> otherwise.</returns>
        private static bool IsSensitiveKey(string key)
        {
            for (int i = 0; i < _sensitiveKeyPatterns.Length; i++)
            {
                if (key.Contains(_sensitiveKeyPatterns[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}