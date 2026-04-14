#nullable enable
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GameDeck.Editor.Utils
{
    /// <summary>
    /// Checks GitHub for newer releases of MCP Game Deck and logs a console
    /// message when an update is available. Runs once per editor session
    /// (or once per day if the editor stays open).
    /// </summary>
    [InitializeOnLoad]
    public static class UpdateChecker
    {
        #region CONSTRUCTOR

        static UpdateChecker()
        {
            EditorApplication.delayCall += CheckIfDue;
        }

        #endregion

        #region CONSTANTS

        private const string GITHUB_API_URL = "https://api.github.com/repos/RamonBedin/mcp-game-deck/releases/latest";
        private const string LAST_CHECK_PREF = "GameDeck_LastUpdateCheck";
        private const string LATEST_VERSION_PREF = "GameDeck_LatestVersion";
        private const string RELEASE_URL_PREF = "GameDeck_ReleaseUrl";
        private const double CHECK_INTERVAL_HOURS = 24;

        #endregion

        #region PROPERTIES

        /// <summary>The latest version found on GitHub, or empty if not checked yet.</summary>
        public static string LatestVersion => EditorPrefs.GetString(LATEST_VERSION_PREF, "");

        /// <summary>The URL to the latest release page on GitHub.</summary>
        public static string ReleaseUrl => EditorPrefs.GetString(RELEASE_URL_PREF, "");

        /// <summary>The current package version from package.json.</summary>
        public static string CurrentVersion
        {
            get
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UpdateChecker).Assembly);
                return packageInfo?.version ?? "0.0.0";
            }
        }

        /// <summary>Whether a newer version is available on GitHub.</summary>
        public static bool IsUpdateAvailable
        {
            get
            {
                string latest = LatestVersion;
                if (string.IsNullOrEmpty(latest)) return false;
                return CompareVersions(latest, CurrentVersion) > 0;
            }
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Checks if enough time has passed since the last update check.
        /// If so, fires an async request to the GitHub API.
        /// </summary>
        private static void CheckIfDue()
        {
            string lastCheckStr = EditorPrefs.GetString(LAST_CHECK_PREF, "");

            if (!string.IsNullOrEmpty(lastCheckStr) && DateTime.TryParse(lastCheckStr, out DateTime lastCheck) && (DateTime.UtcNow - lastCheck).TotalHours < CHECK_INTERVAL_HOURS)
            {
                if (IsUpdateAvailable)
                {
                    LogUpdateAvailable();
                }
                return;
            }

            CheckForUpdate();
        }

        /// <summary>
        /// Sends a request to the GitHub releases API and parses the latest tag.
        /// </summary>
        private static void CheckForUpdate()
        {
            var request = UnityWebRequest.Get(GITHUB_API_URL);
            request.SetRequestHeader("User-Agent", "MCP-Game-Deck-Unity");
            request.timeout = 10;

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        return;
                    }

                    string json = request.downloadHandler.text;
                    string tagName = ExtractJsonString(json, "tag_name") ?? "";
                    string htmlUrl = ExtractJsonString(json, "html_url") ?? "";

                    if (string.IsNullOrEmpty(tagName))
                    {
                        return;
                    }

                    string version = tagName.TrimStart('v', 'V');

                    EditorPrefs.SetString(LATEST_VERSION_PREF, version);
                    EditorPrefs.SetString(RELEASE_URL_PREF, htmlUrl);
                    EditorPrefs.SetString(LAST_CHECK_PREF, DateTime.UtcNow.ToString("O"));

                    if (CompareVersions(version, CurrentVersion) > 0)
                    {
                        LogUpdateAvailable();
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        /// <summary>
        /// Logs an update notification to the Unity console.
        /// </summary>
        private static void LogUpdateAvailable()
        {
            Debug.Log($"[Game Deck] Update available: v{LatestVersion} (current: v{CurrentVersion}). " +
                      $"Update in Packages/manifest.json or visit: {ReleaseUrl}");
        }

        /// <summary>
        /// Compares two semantic version strings (major.minor.patch).
        /// </summary>
        /// <param name="a">First version string.</param>
        /// <param name="b">Second version string.</param>
        /// <returns>Positive if a &gt; b, negative if a &lt; b, zero if equal.</returns>
        private static int CompareVersions(string a, string b)
        {
            var partsA = a.Split('.');
            var partsB = b.Split('.');
            int count = Math.Max(partsA.Length, partsB.Length);

            for (int i = 0; i < count; i++)
            {
                int numA = i < partsA.Length && int.TryParse(partsA[i], out int pA) ? pA : 0;
                int numB = i < partsB.Length && int.TryParse(partsB[i], out int pB) ? pB : 0;

                if (numA != numB)
                {
                    return numA - numB;
                }
            }

            return 0;
        }

        /// <summary>
        /// Extracts a string value from a JSON object by key.
        /// Minimal parser — no external dependencies.
        /// </summary>
        /// <param name="json">The raw JSON string to search.</param>
        /// <param name="key">The key to find.</param>
        /// <returns>The string value, or null if not found.</returns>
        private static string? ExtractJsonString(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);

            if (idx < 0)
            {
                return null;
            }

            int colonIdx = json.IndexOf(':', idx + search.Length);

            if (colonIdx < 0)
            {
                return null;
            }

            int quoteStart = json.IndexOf('"', colonIdx + 1);

            if (quoteStart < 0)
            {
                return null;
            }

            int quoteEnd = json.IndexOf('"', quoteStart + 1);

            if (quoteEnd < 0)
            {
                return null;
            }

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        #endregion
    }
}