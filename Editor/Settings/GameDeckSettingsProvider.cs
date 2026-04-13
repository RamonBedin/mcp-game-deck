#nullable enable
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Settings
{
    /// <summary>
    /// Registers a <see cref="SettingsProvider"/> so MCP Game Deck configuration
    /// appears under <b>Project Settings &gt; MCP Game Deck</b>.
    /// </summary>
    public static class GameDeckSettingsProvider
    {
        #region CONSTANTS

        private const string SETTINGS_PROVIDER_PATH = "Project/Game Deck";
        private const int PORT_MIN = 1024;
        private const int PORT_MAX = 65535;
        private const string DEFAULT_HOST = "localhost";
        private const int TIMEOUT_MIN_SECONDS = 5;
        private const int TIMEOUT_MAX_SECONDS = 300;
        private const float SPACE_SECTION_HEADER = 4f;
        private const float SPACE_MCP_CONFIG = 12f;
        private const float CONFIG_TEXT_AREA_HEIGHT = 180f;
        private const string PACKAGE_PATH_PLACEHOLDER = "<package-path>";

        #endregion

        #region PROVIDER REGISTRATION

        /// <summary>
        /// Creates and returns the <see cref="SettingsProvider"/> for Project Settings.
        /// </summary>
        /// <returns>A configured <see cref="SettingsProvider"/> instance.</returns>
        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SETTINGS_PROVIDER_PATH, SettingsScope.Project)
            {
                guiHandler = DrawSettingsGUI,
                keywords = new[] { "AI", "MCP", "Port", "Claude", "Agent", "Host", "Timeout", "Model" }
            };
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Resolves the absolute filesystem path to this package.
        /// </summary>
        /// <returns>Absolute package path, or a placeholder if not found.</returns>
        private static string ResolvePackagePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GameDeckSettingsProvider).Assembly);

            if (packageInfo != null)
            {
                return packageInfo.resolvedPath;
            }

            return PACKAGE_PATH_PLACEHOLDER;
        }

        /// <summary>
        /// Normalizes a filesystem path for JSON embedding by converting backslashes
        /// to forward slashes. Required for Windows paths inside JSON strings.
        /// </summary>
        /// <param name="path">The file path to normalize.</param>
        /// <returns>Path with all backslashes replaced by forward slashes.</returns>
        private static string EscapeJsonPath(string path)
        {
            return path.Replace("\\", "/");
        }

        #endregion

        #region GUI

        /// <summary>
        /// Draws the settings GUI inside the Project Settings window.
        /// </summary>
        /// <param name="searchContext">The current search string from the settings window.</param>
        private static void DrawSettingsGUI(string searchContext)
        {
            var s = GameDeckSettings.Instance;

            var prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 180f;

            EditorGUILayout.Space(SPACE_SECTION_HEADER);
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int newMcpPort = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("MCP Server Port", "Port for the C# HTTP server that exposes Unity tools (default: 8090)"), s._mcpPort), PORT_MIN, PORT_MAX);

            int newAgentPort = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Agent Server Port", "Port for the Node.js WebSocket server the Chat UI connects to (default: 9100)"), s._agentPort), PORT_MIN, PORT_MAX);

            string newHost = EditorGUILayout.TextField(new GUIContent("Host", "Hostname the MCP server binds to (default: localhost)"), s._host);

            if (string.IsNullOrWhiteSpace(newHost))
            {
                newHost = DEFAULT_HOST;
            }

            int newTimeout = Mathf.Clamp(EditorGUILayout.IntField(new GUIContent("Request Timeout (seconds)", "Max time to wait for a tool invocation on the Unity main thread (default: 30)"), s._requestTimeoutSeconds), TIMEOUT_MIN_SECONDS, TIMEOUT_MAX_SECONDS);

            bool newAutoStart = EditorGUILayout.Toggle(new GUIContent("Auto Start Servers", "Automatically start servers when the Chat window opens"), s._autoStart);

            string newModel = EditorGUILayout.TextField(new GUIContent("Default Model", "Claude model ID for new conversations (default: claude-sonnet-4-6)"), s._defaultModel);

            EditorGUI.indentLevel--;

            bool portChanged = newMcpPort != s._mcpPort || newAgentPort != s._agentPort;
            bool changed = portChanged || newHost != s._host || newTimeout != s._requestTimeoutSeconds || newAutoStart != s._autoStart || newModel != s._defaultModel;

            if (changed)
            {
                s._mcpPort = newMcpPort;
                s._agentPort = newAgentPort;
                s._host = newHost;
                s._requestTimeoutSeconds = newTimeout;
                s._autoStart = newAutoStart;
                s._defaultModel = newModel;
                s.Save();

                if (portChanged)
                {
                    EditorUtility.DisplayDialog("Restart Required", "Port changes require restarting the MCP Game Deck servers.\n" + "Close and re-open the Chat window to apply.", "OK");
                }
            }

            EditorGUILayout.Space(SPACE_MCP_CONFIG);
            EditorGUILayout.LabelField("MCP Configuration (for Claude Desktop / Claude Code)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Copy the JSON below into your Claude Desktop or Claude Code MCP configuration file.", MessageType.Info);

            var packageDir = ResolvePackagePath();
            string config = "{\n" +
                "  \"mcpServers\": {\n" +
                "    \"mcp-game-deck\": {\n" +
                "      \"command\": \"node\",\n" +
                $"      \"args\": [\"{EscapeJsonPath(packageDir)}/Server~/dist/mcp-proxy.js\"],\n" +
                "      \"env\": {\n" +
                $"        \"UNITY_MCP_PORT\": \"{s._mcpPort}\",\n" +
                $"        \"UNITY_MCP_HOST\": \"{s._host}\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}";

            EditorGUILayout.TextArea(config, GUILayout.Height(CONFIG_TEXT_AREA_HEIGHT));

            if (GUILayout.Button("Copy MCP Config to Clipboard"))
            {
                GUIUtility.systemCopyBuffer = config;
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        #endregion
    }
}
