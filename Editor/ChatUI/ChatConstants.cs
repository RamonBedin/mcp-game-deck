#nullable enable

namespace GameDeck.Editor.ChatUI
{
     /// <summary>
    /// Shared constants for the Game Deck Chat UI, covering window layout, EditorPrefs keys,
    /// model/agent dropdown values, session list limits, MCP tool prefixes, WebSocket retry
    /// configuration, server process settings, environment variables, and message renderer timing.
    /// </summary>
    public static class ChatConstants
    {
        #region WINDOW

        public const string MENU_PATH = "Window/MCP Game Deck";
        public const float MIN_WINDOW_WIDTH = 400f;
        public const float MIN_WINDOW_HEIGHT = 300f;
        public const float SCROLL_BOTTOM_THRESHOLD = 50f;

        #endregion

        #region EDITOR PREFS KEYS

        public const string MODEL_PREF_KEY = "GameDeck_Model";

        #endregion

        #region MODEL DROPDOWN

        public const string MODEL_SONNET_LABEL = "Sonnet";
        public const string MODEL_OPUS_LABEL = "Opus";
        public const string MODEL_HAIKU_LABEL = "Haiku";
        public const string MODEL_SONNET_ID = "sonnet";
        public const string MODEL_OPUS_ID = "opus";
        public const string MODEL_HAIKU_ID = "haiku";
        public const string MODEL_DEFAULT_LABEL = MODEL_SONNET_LABEL;

        #endregion

        #region AGENT DROPDOWN
        public const string AGENT_DEFAULT_LABEL = "(default)";

        #endregion

        #region PERMISSION MODE DROPDOWN

        public const string PERM_PREF_KEY = "GameDeck_PermissionMode";
        public const string PERM_ASK_LABEL = "Ask";
        public const string PERM_AUTO_LABEL = "Auto";
        public const string PERM_PLAN_LABEL = "Plan";
        public const string PERM_ASK_ID = "default";
        public const string PERM_AUTO_ID = "acceptEdits";
        public const string PERM_PLAN_ID = "plan";
        public const string PERM_DEFAULT_LABEL = PERM_ASK_LABEL;

        #endregion

        #region SESSION LIST

        public const int SESSION_TITLE_MAX_LENGTH = 40;

        #endregion

        #region MCP TOOL PREFIX

        public const string MCP_TOOL_PREFIX = "mcp__unity-mcp__";
        public const int MCP_TOOL_PREFIX_LENGTH = 16;

        #endregion

        #region WEB SOCKET

        public const string DEFAULT_WS_URL = "ws://localhost:9100";
        public const int DEFAULT_RECONNECT_DELAY_MS = 3000;
        public const int WS_BUFFER_SIZE = 8192;
        public const int WS_MAX_CONNECT_ATTEMPTS = 10;
        public const int WS_INITIAL_DELAY_MS = 500;
        public const int WS_MAX_DELAY_MS = 3000;

        #endregion

        #region SERVER PROCESS

        public const string LOCALHOST_IP = "127.0.0.1";
        public const int PROCESS_WAIT_TIMEOUT_MS = 3000;
        public const int PROCESS_KILL_TIMEOUT_MS = 5000;
        public const string FALLBACK_SERVER_PATH = "Packages/com.mcp-game-deck/Server~";
        public const string FALLBACK_PACKAGE_PATH = "Packages/com.mcp-game-deck";

        #endregion

        #region ENVIRONMENT VARIABLES

        public const string ENV_PORT = "PORT";
        public const string ENV_PROJECT_CWD = "PROJECT_CWD";
        public const string ENV_PACKAGE_DIR = "PACKAGE_DIR";
        public const string ENV_MCP_SERVER_URL = "MCP_SERVER_URL";
        public const string ENV_MODEL = "MODEL";

        #endregion

        #region MESSAGE RENDERER

        public const int SCROLL_DELAY_MS = 50;
        public const int TYPING_FRAME_DIVISOR = 4;
        public const float TYPING_ACTIVE_OPACITY = 1.0f;
        public const float TYPING_INACTIVE_OPACITY = 0.3f;
        public const float TYPING_ACTIVE_SCALE = 1.2f;
        public const float TYPING_INACTIVE_SCALE = 0.8f;
        public const int TYPING_ANIMATION_INTERVAL_MS = 100;

        #endregion
    }
}
