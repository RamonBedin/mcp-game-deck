#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace GameDeck.Editor.Settings
{
    /// <summary>
    /// Persistent project-level settings for the MCP Game Deck package.
    /// Stored as JSON in <c>ProjectSettings/GameDeckSettings.json</c> so each
    /// Unity project can have independent configuration.
    /// </summary>
    /// <remarks>
    /// This file contains no secrets (auth tokens are stored in <c>Library/GameDeck/</c>
    /// which Unity gitignores by default). If your project requires hiding server
    /// configuration (host, port, model), add <c>ProjectSettings/GameDeckSettings.json</c>
    /// to your <c>.gitignore</c>.
    /// </remarks>
    [Serializable]
    public class GameDeckSettings
    {
        #region CONSTRUCTOR

        private GameDeckSettings()
        {
            Load();
        }

        #endregion

        #region CONSTANTS

        private const string SETTINGS_PATH = "ProjectSettings/GameDeckSettings.json";

        #endregion

        #region FIELDS

        [Tooltip("Port for the MCP Server (C# HTTP server that Unity tools listen on)")]
        public int _mcpPort = 8090;

        [Tooltip("Port for the Agent SDK WebSocket server (Chat UI connects here)")]
        public int _agentPort = 9100;

        [Tooltip("Hostname the MCP server binds to")]
        public string _host = "localhost";

        [Tooltip("Request timeout in seconds")]
        public int _requestTimeoutSeconds = 30;

        [Tooltip("Auto-start servers when the Chat window opens")]
        public bool _autoStart = true;

        [Tooltip("Claude model to use")]
        public string _defaultModel = "claude-sonnet-4-6";

        #endregion

        #region SINGLETON

        private static GameDeckSettings? _instance;

        /// <summary>
        /// Gets the singleton settings instance. Loads from disk on first access.
        /// </summary>
        public static GameDeckSettings Instance
        {
            get
            {
                _instance ??= new GameDeckSettings();
                return _instance;
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Loads settings from <see cref="SETTINGS_PATH"/>. Creates the file with
        /// defaults if it does not exist.
        /// </summary>
        public void Load()
        {
            if (File.Exists(SETTINGS_PATH))
            {
                try
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(SETTINGS_PATH), this);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Game Deck] Failed to load settings: {ex.Message}. Using defaults.");
                }
            }
            else
            {
                Save();
            }
        }

        /// <summary>
        /// Persists the current settings to <see cref="SETTINGS_PATH"/> as formatted JSON.
        /// </summary>
        public void Save()
        {
            try
            {
                File.WriteAllText(SETTINGS_PATH, JsonUtility.ToJson(this, true));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Game Deck] Failed to save settings: {ex.Message}");
            }
        }

        #endregion
    }
}