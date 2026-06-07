#nullable enable

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Shared constants for the MCP server: protocol identity, JSON-RPC method
    /// names, request limits, JSON parsing tokens, MIME/URI template utilities,
    /// and the auth-token contract.
    /// </summary>
    /// <remarks>
    /// HTTP transport constants (methods, headers, status codes, socket timeouts,
    /// rate-limit window, CORS) now live in the sidecar process
    /// (<c>Server~/src/sidecar.ts</c>) — the Editor no longer owns the listening
    /// socket. See <see cref="SidecarManager"/> / <see cref="McpBackendClient"/>.
    /// </remarks>
    public static class McpConstants
    {
        #region SERVER IDENTITY

        public const string SERVER_NAME = "mcp-game-deck";
        public const string SERVER_VERSION = "1.0.0";
        public const string PROTOCOL_VERSION = "2024-11-05";

        #endregion

        #region MCP METHODS

        public const string METHOD_INITIALIZE = "initialize";
        public const string METHOD_TOOLS_LIST = "tools/list";
        public const string METHOD_TOOLS_CALL = "tools/call";
        public const string METHOD_RESOURCES_LIST = "resources/list";
        public const string METHOD_RESOURCES_READ = "resources/read";
        public const string METHOD_PROMPTS_LIST = "prompts/list";
        public const string METHOD_PROMPTS_GET = "prompts/get";
        public const string NOTIFICATION_PREFIX = "notifications/";

        #endregion

        #region REQUEST LIMITS

        public const int MAX_REQUEST_BODY_SIZE = 16 * 1024 * 1024;
        public const long MAX_SCRIPT_FILE_SIZE = 10 * 1024 * 1024;

        #endregion

        #region JSON PROTOCOL

        public const string JSON_NULL = "null";
        public const string EMPTY_JSON_OBJECT = "{}";
        public const char CONTROL_CHAR_BOUNDARY = ' ';
        public const string JSON_TRUE = "true";
        public const string JSON_TRUE_PASCAL = "True";
        public const string JSON_FALSE = "false";
        public const string JSON_FALSE_PASCAL = "False";

        #endregion

        #region MIME AND URI

        public const string MIME_PREFIX_IMAGE = "image/";
        public const char URI_SEGMENT_SEPARATOR = '/';
        public const char TEMPLATE_PARAM_OPEN = '{';
        public const char TEMPLATE_PARAM_CLOSE = '}';
        public const int MIN_PARAMETER_SEGMENT_LENGTH = 2;
        public const string PARAM_NAME_URI = "uri";

        #endregion

        #region AUTHENTICATION

        public const string AUTH_TOKEN_DIR = "Library/GameDeck";
        public const string AUTH_TOKEN_FILE = "Library/GameDeck/auth-token";
        public const int AUTH_TOKEN_BYTE_LENGTH = 16;

        #endregion
    }
}