#nullable enable
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.ChatUI
{
    public partial class ChatWindow
    {
        #region HELPER METHODS

        /// <summary>
        /// Extracts a string value from a JSON object by key without external dependencies.
        /// Handles standard JSON escape sequences (\n, \r, \t, \", \\, \/).
        /// </summary>
        /// <param name="json">Raw JSON string to search.</param>
        /// <param name="key">The JSON key whose string value to extract.</param>
        /// <returns>The unescaped string value, or null if the key is not found.</returns>
        private static string? ExtractJsonString(string json, string key)
        {
            var search = $"\"{key}\":\"";
            var idx = json.IndexOf(search, StringComparison.Ordinal);

            if (idx < 0)
            {
                return null;
            }

            var start = idx + search.Length;
            var sb = new System.Text.StringBuilder();

            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        default: sb.Append(next); break;
                    }

                    i++;
                }
                else if (json[i] == '"')
                {
                    break;
                }
                else
                {
                    sb.Append(json[i]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extracts a raw (non-string) JSON value by key — numbers, booleans, etc.
        /// Reads characters until a comma, closing brace, or space is found.
        /// </summary>
        /// <param name="json">Raw JSON string to search.</param>
        /// <param name="key">The JSON key whose value to extract.</param>
        /// <returns>The raw value as a string, or null if the key is not found.</returns>
        private static string? ExtractJsonValue(string json, string key)
        {
            var search = $"\"{key}\":";
            var idx = json.IndexOf(search, StringComparison.Ordinal);

            if (idx < 0)
            {
                return null;
            }

            var start = idx + search.Length;
            var sb = new System.Text.StringBuilder();

            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == ',' || json[i] == '}' || json[i] == ' ')
                {
                    break;
                }

                sb.Append(json[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extracts a string field from an object at a specific index within a JSON array.
        /// Uses brace-depth tracking to locate the Nth object, then delegates to
        /// <see cref="ExtractJsonString"/> for field extraction.
        /// </summary>
        /// <param name="json">Raw JSON string containing the array.</param>
        /// <param name="arrayKey">The key of the JSON array.</param>
        /// <param name="index">Zero-based index of the target object in the array.</param>
        /// <param name="fieldKey">The string field key to extract from the object.</param>
        /// <returns>The unescaped string value, or null if not found.</returns>
        private static string? ExtractJsonStringFromArray(string json, string arrayKey, int index, string fieldKey)
        {
            var arraySearch = $"\"{arrayKey}\":[";
            var arrayIdx = json.IndexOf(arraySearch, StringComparison.Ordinal);

            if (arrayIdx < 0)
            {
                return null;
            }

            var arrayStart = arrayIdx + arraySearch.Length;
            int currentIdx = 0;
            int depth = 0;
            int objStart = -1;

            for (int i = arrayStart; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    if (depth == 0) objStart = i;
                    depth++;
                }
                else if (json[i] == '}')
                {
                    depth--;

                    if (depth == 0)
                    {
                        if (currentIdx == index)
                        {
                            var objJson = json.Substring(objStart, i - objStart + 1);
                            return ExtractJsonString(objJson, fieldKey);
                        }

                        currentIdx++;
                    }
                }
                else if (json[i] == ']' && depth == 0)
                {
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Wraps a string in double quotes and escapes backslashes, quotes, newlines,
        /// and carriage returns for safe embedding in a hand-built JSON payload.
        /// </summary>
        /// <param name="s">The raw string to escape.</param>
        /// <returns>A JSON-safe quoted string (e.g. <c>"hello \"world\""</c>).</returns>
        private static string EscapeJson(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";
        }

        /// <summary>
        /// Resolves the Unity asset path prefix for this package.
        /// Returns the virtual package path used by AssetDatabase.
        /// </summary>
        /// <returns>Package asset path (e.g., "Packages/com.mcp-game-deck").</returns>
        private static string ResolvePackageAssetPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChatWindow).Assembly);

            if (packageInfo != null)
            {
                return $"Packages/{packageInfo.name}";
            }

            return ChatConstants.FALLBACK_PACKAGE_PATH;
        }

        /// <summary>
        /// Safely awaits an async Task, catching and logging any exceptions.
        /// Use as a fire-and-forget wrapper to avoid <c>async void</c> in methods
        /// that must return void (UI callbacks, <see cref="EditorApplication.delayCall"/>).
        /// </summary>
        /// <param name="task">The async Task to await.</param>
        /// <param name="context">Short label for error messages (e.g. "SendPrompt").</param>
        private static async void SafeAsync(Task task, string context)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Game Deck Chat] {context} failed: {ex}");
            }
        }

        #endregion
    }
}