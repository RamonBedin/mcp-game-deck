#nullable enable

using System.Text;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Stateless JSON-RPC 2.0 protocol handler used by the MCP WebSocket server.
    /// Parses inbound request frames and builds well-formed response frames without
    /// any external JSON library — all parsing is done with index/substring operations
    /// on the raw JSON string, which is safe for the simple, flat JSON-RPC envelope.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The JSON-RPC 2.0 request envelope has a well-known, non-nested structure:
    /// <code>
    /// { "jsonrpc": "2.0", "id": "...", "method": "tools/call", "params": { ... } }
    /// </code>
    /// <see cref="ParseRequest"/> extracts the three fields the dispatcher cares about
    /// (<c>method</c>, <c>id</c>, and the raw <c>params</c> JSON substring) without
    /// deserializing the full document.
    /// </para>
    /// <para>
    /// All response builders return complete JSON-RPC 2.0 frames ready to be sent over
    /// the wire. The <c>result</c> or <c>error</c> value is embedded verbatim — callers
    /// are responsible for supplying valid JSON for those fields.
    /// </para>
    /// </remarks>
    public static class McpProtocol
    {
        #region CONSTANTS

        public const int PARSE_ERROR = -32700;
        public const int INVALID_REQUEST = -32600;
        public const int METHOD_NOT_FOUND = -32601;
        public const int INVALID_PARAMS = -32602;
        public const int INTERNAL_ERROR = -32603;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Extracts the string value of a named field from a flat JSON object.
        /// Returns an empty string when the field is absent or its value is not a JSON string.
        /// </summary>
        /// <param name="json">The JSON object to search in.</param>
        /// <param name="fieldName">The field name to look for (without quotes).</param>
        /// <returns>The unescaped string value, or an empty string when not found.</returns>
        internal static string ExtractStringField(string json, string fieldName)
        {
            string key = "\"" + fieldName + "\":";
            int keyIndex = json.IndexOf(key);

            if (keyIndex < 0)
            {
                return string.Empty;
            }

            int valueStart = keyIndex + key.Length;

            while (valueStart < json.Length && json[valueStart] == ' ')
            {
                valueStart++;
            }

            if (valueStart >= json.Length || json[valueStart] != '"')
            {
                return string.Empty;
            }

            valueStart++;

            int valueEnd = FindClosingQuote(json, valueStart);

            if (valueEnd < 0)
            {
                return string.Empty;
            }

            return UnescapeJsonString(json, valueStart, valueEnd);
        }

        /// <summary>
        /// Extracts the <c>"id"</c> field from a JSON-RPC envelope, preserving its raw JSON
        /// representation. The id can be a string, number, or null in JSON-RPC 2.0.
        /// Returns <c>"null"</c> when the field is absent.
        /// </summary>
        /// <param name="json">The JSON object to search in.</param>
        /// <returns>The raw JSON token for the id, e.g. <c>"\"abc\""</c>, <c>"1"</c>, or <c>"null"</c>.</returns>
        private static string ExtractIdField(string json)
        {
            string key = "\"id\":";
            int keyIndex = json.IndexOf(key);

            if (keyIndex < 0)
            {
                return McpConstants.JSON_NULL;
            }

            int valueStart = keyIndex + key.Length;

            while (valueStart < json.Length && json[valueStart] == ' ')
            {
                valueStart++;
            }

            if (valueStart >= json.Length)
            {
                return McpConstants.JSON_NULL;
            }

            char first = json[valueStart];

            if (first == 'n')
            {
                return McpConstants.JSON_NULL;
            }

            if (first == '"')
            {
                int closingQuote = FindClosingQuote(json, valueStart + 1);

                if (closingQuote < 0)
                {
                    return McpConstants.JSON_NULL;
                }

                return json.Substring(valueStart, closingQuote - valueStart + 1);
            }

            int numEnd = valueStart;

            while (numEnd < json.Length && json[numEnd] != ',' && json[numEnd] != '}' && json[numEnd] != ' ')
            {
                numEnd++;
            }

            return json[valueStart..numEnd];
        }

        /// <summary>
        /// Extracts the raw JSON value (object or array) of a named field.
        /// Returns <c>"{}"</c> when the field is absent or when the value is not an object or array.
        /// </summary>
        /// <param name="json">The JSON object to search in.</param>
        /// <param name="fieldName">The field name to look for (without quotes).</param>
        /// <returns>The raw JSON object or array substring, or <c>"{}"</c>.</returns>
        internal static string ExtractObjectField(string json, string fieldName)
        {
            string key = "\"" + fieldName + "\":";
            int keyIndex = json.IndexOf(key);

            if (keyIndex < 0)
            {
                return McpConstants.EMPTY_JSON_OBJECT;
            }

            int valueStart = keyIndex + key.Length;

            while (valueStart < json.Length && json[valueStart] == ' ')
            {
                valueStart++;
            }

            if (valueStart >= json.Length)
            {
                return McpConstants.EMPTY_JSON_OBJECT;
            }

            char opener = json[valueStart];

            if (opener != '{' && opener != '[')
            {
                return McpConstants.EMPTY_JSON_OBJECT;
            }

            char closer = opener == '{' ? '}' : ']';
            int depth = 1;
            int cursor = valueStart + 1;

            while (cursor < json.Length && depth > 0)
            {
                char c = json[cursor];

                if (c == '"')
                {
                    cursor = FindClosingQuote(json, cursor + 1);

                    if (cursor < 0)
                    {
                        return McpConstants.EMPTY_JSON_OBJECT;
                    }
                }
                else if (c == opener)
                {
                    depth++;
                }
                else if (c == closer)
                {
                    depth--;
                }

                cursor++;
            }

            if (depth != 0)
            {
                return McpConstants.EMPTY_JSON_OBJECT;
            }

            return json[valueStart..cursor];
        }

        /// <summary>
        /// Finds the index of the closing <c>"</c> for a JSON string that begins at
        /// <paramref name="start"/> (the character immediately after the opening quote).
        /// Correctly handles backslash escape sequences.
        /// </summary>
        /// <param name="json">The full JSON string being scanned.</param>
        /// <param name="start">Index of the first character inside the string (after the opening quote).</param>
        /// <returns>
        /// The index of the closing <c>"</c>, or <c>-1</c> if the string is unterminated.
        /// </returns>
        internal static int FindClosingQuote(string json, int start)
        {
            int i = start;

            while (i < json.Length)
            {
                char c = json[i];

                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                if (c == '"')
                {
                    return i;
                }

                i++;
            }

            return -1;
        }

        /// <summary>
        /// Copies a substring from <paramref name="json"/> in the range
        /// [<paramref name="start"/>, <paramref name="closingQuote"/>)
        /// and converts common JSON escape sequences to their character equivalents.
        /// </summary>
        /// <param name="json">The source JSON string.</param>
        /// <param name="start">Index of the first character inside the quoted string (after the opening quote).</param>
        /// <param name="closingQuote">Index of the closing quote character.</param>
        /// <returns>The unescaped string value.</returns>
        internal static string UnescapeJsonString(string json, int start, int closingQuote)
        {
            bool hasEscape = false;

            for (int i = start; i < closingQuote; i++)
            {
                if (json[i] == '\\')
                {
                    hasEscape = true;
                    break;
                }
            }

            if (!hasEscape)
            {
                return json[start..closingQuote];
            }

            var sb = new StringBuilder(closingQuote - start);
            int pos = start;

            while (pos < closingQuote)
            {
                char c = json[pos];

                if (c == '\\' && pos + 1 < closingQuote)
                {
                    char next = json[pos + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (pos + 5 < closingQuote)
                            {
                                string hex = json.Substring(pos + 2, 4);
                                int code = 0;
                                bool valid = true;

                                for (int h = 0; h < 4; h++)
                                {
                                    char hc = hex[h];

                                    if (hc >= '0' && hc <= '9')
                                    {
                                        code = code * 16 + (hc - '0');
                                    }
                                    else if (hc >= 'a' && hc <= 'f')
                                    {
                                        code = code * 16 + (hc - 'a' + 10);
                                    }
                                    else if (hc >= 'A' && hc <= 'F')
                                    {
                                        code = code * 16 + (hc - 'A' + 10);
                                    }
                                    else
                                    {
                                        valid = false;
                                        break;
                                    }
                                }

                                if (valid)
                                {
                                    sb.Append((char)code);
                                    pos += 6;
                                    continue;
                                }
                            }
                            sb.Append(next);
                            break;
                        default:
                            sb.Append(next);
                            break;
                    }

                    pos += 2;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Appends <paramref name="s"/> to <paramref name="sb"/> with JSON-unsafe characters
        /// replaced by their escape sequences. Used when embedding strings inside error messages.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="s">The string to escape. May be <c>null</c> or empty.</param>
        internal static void AppendEscaped(StringBuilder sb, string? s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return;
            }

            for (int i = 0; i < s!.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < McpConstants.CONTROL_CHAR_BOUNDARY)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
        }

        #endregion

        #region REQUEST PARSING

        /// <summary>
        /// Parses a JSON-RPC 2.0 request string and extracts the method name, request ID,
        /// and raw params JSON substring.
        /// </summary>
        /// <param name="json">
        /// The complete JSON-RPC 2.0 request frame as received from the WebSocket client.
        /// Must not be <c>null</c>.
        /// </param>
        /// <returns>
        /// A value tuple containing:
        /// <list type="bullet">
        ///   <item><description><c>method</c> — the <c>"method"</c> field value, or empty string when absent.</description></item>
        ///   <item><description><c>id</c> — the <c>"id"</c> field value as a string, or <c>"null"</c> when absent.</description></item>
        ///   <item><description><c>paramsJson</c> — the raw JSON object/array value of the <c>"params"</c> field, or <c>"{}"</c> when absent.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The parser makes the following assumptions about the incoming JSON, all of which
        /// are guaranteed by any conforming JSON-RPC 2.0 client:
        /// <list type="bullet">
        ///   <item>The envelope is a single flat JSON object (no arrays at the top level).</item>
        ///   <item>String field values do not contain escaped <c>"</c> before the closing <c>"</c> of the value.</item>
        ///   <item>The <c>"params"</c> field, when present, is a JSON object or array (not a primitive).</item>
        /// </list>
        /// When any field is missing or cannot be extracted, the corresponding tuple member
        /// falls back to its safe default so the dispatcher can produce a well-formed error response.
        /// </remarks>
        public static (string method, string id, string paramsJson) ParseRequest(string json)
        {
            string method = ExtractStringField(json, "method");
            string id = ExtractIdField(json);
            string paramsJson = ExtractObjectField(json, "params");

            return (method, id, paramsJson);
        }

        #endregion

        #region RESPONSE BUILDERS

        /// <summary>
        /// Builds a JSON-RPC 2.0 success response frame.
        /// </summary>
        /// <param name="id">
        /// The request ID to echo back. Must match the <c>id</c> from the originating request.
        /// Pass <c>"null"</c> for notifications (no ID).
        /// </param>
        /// <param name="resultJson">
        /// The JSON value to embed as the <c>"result"</c> field. Must be valid JSON
        /// (object, array, string, number, boolean, or <c>null</c>).
        /// </param>
        /// <returns>A complete JSON-RPC 2.0 response frame as a string.</returns>
        public static string BuildResponse(string id, string resultJson)
        {
            var sb = new StringBuilder(resultJson.Length + 64);
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
            sb.Append(id);
            sb.Append(",\"result\":");
            sb.Append(resultJson);
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Builds a JSON-RPC 2.0 error response frame.
        /// </summary>
        /// <param name="id">
        /// The request ID to echo back. Use <c>"null"</c> when the ID could not be parsed
        /// from the request (e.g. parse errors).
        /// </param>
        /// <param name="code">
        /// The JSON-RPC error code. Use one of the constants defined on this class
        /// (e.g. <see cref="METHOD_NOT_FOUND"/>) or an application-defined code in the
        /// range <c>-32000</c> to <c>-32099</c>.
        /// </param>
        /// <param name="message">
        /// A short, human-readable description of the error. Will be JSON-escaped before
        /// embedding so it is safe to include raw exception messages here.
        /// </param>
        /// <returns>A complete JSON-RPC 2.0 error response frame as a string.</returns>
        public static string BuildErrorResponse(string id, int code, string message)
        {
            var sb = new StringBuilder(message.Length + 96);
            sb.Append("{\"jsonrpc\":\"2.0\",\"id\":");
            sb.Append(id);
            sb.Append(",\"error\":{\"code\":");
            sb.Append(code);
            sb.Append(",\"message\":\"");
            AppendEscaped(sb, message);
            sb.Append("\"}}");
            return sb.ToString();
        }

        #endregion
    }
}
