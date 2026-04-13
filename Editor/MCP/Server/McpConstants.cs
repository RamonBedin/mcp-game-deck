#nullable enable

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Shared constants for the MCP Server covering protocol identity, JSON-RPC method names,
    /// HTTP server configuration, JSON parsing tokens, HTTP headers/methods/status codes,
    /// and MIME/URI template utilities.
    /// </summary>
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

        #region HTTP SERVER

        public const int MAX_REQUEST_BODY_SIZE = 16 * 1024 * 1024;
        public const long MAX_SCRIPT_FILE_SIZE = 10 * 1024 * 1024;
        public const string THREAD_NAME_ACCEPT_LOOP = "MCP-AcceptLoop";
        public const int RECEIVE_TIMEOUT_MS = 30000;
        public const int SEND_TIMEOUT_MS = 10000;
        public const string STATUS_OK_JSON = "{\"status\":\"ok\"}";
        public const string CONTENT_TYPE_JSON = "application/json";
        public const int KEEP_ALIVE_TIMEOUT_SECONDS = 30;

        #endregion

        #region JSON PROTOCOL

        public const string JSON_NULL = "null";
        public const string EMPTY_JSON_OBJECT = "{}";
        public const char CONTROL_CHAR_BOUNDARY = '\u0020';
        public const string JSON_TRUE = "true";
        public const string JSON_TRUE_PASCAL = "True";
        public const string JSON_FALSE = "false";
        public const string JSON_FALSE_PASCAL = "False";

        #endregion

        #region HTTP HEADERS AND METHODS

        public const string HOST_LOCALHOST = "localhost";
        public const string HOST_WILDCARD = "*";
        public const string HOST_ANY_ADDRESS = "0.0.0.0";
        public const string HEADER_CONTENT_LENGTH = "Content-Length:";
        public const int HEADER_CONTENT_LENGTH_SIZE = 15;
        public const string HEADER_CONNECTION = "Connection:";
        public const int HEADER_CONNECTION_SIZE = 11;
        public const string CONNECTION_CLOSE = "close";
        public const string HTTP_METHOD_POST = "POST";
        public const string HTTP_METHOD_GET = "GET";
        public const string HTTP_METHOD_OPTIONS = "OPTIONS";

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
        public const string HEADER_AUTHORIZATION = "Authorization:";
        public const int HEADER_AUTHORIZATION_SIZE = 14;
        public const string AUTH_BEARER_PREFIX = "Bearer ";
        public const int AUTH_TOKEN_BYTE_LENGTH = 16;
        public const int HTTP_UNAUTHORIZED = 401;
        public const int HTTP_TOO_MANY_REQUESTS = 429;
        public const int RATE_LIMIT_MAX_REQUESTS = 120;
        public const long RATE_LIMIT_WINDOW_TICKS = 600000000L;

        #endregion

        #region HTTP STATUS CODES

        public const int HTTP_OK = 200;
        public const int HTTP_NO_CONTENT = 204;
        public const int HTTP_BAD_REQUEST = 400;
        public const int HTTP_METHOD_NOT_ALLOWED = 405;
        public const int HTTP_CONTENT_TOO_LARGE = 413;
        public const int HTTP_INTERNAL_ERROR = 500;

        #endregion
    }
}
