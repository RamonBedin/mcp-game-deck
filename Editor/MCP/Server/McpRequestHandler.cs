#nullable enable

using System;
using System.Text;
using GameDeck.MCP.Registry;
using GameDeck.MCP.Utils;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Routes inbound JSON-RPC 2.0 MCP requests to the correct handler and returns a
    /// JSON-RPC 2.0 response string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The handler understands the six standard MCP methods:
    /// <list type="bullet">
    ///   <item><description><c>initialize</c> — returns server capabilities and version info.</description></item>
    ///   <item><description><c>tools/list</c> — returns all registered tool schemas.</description></item>
    ///   <item><description><c>tools/call</c> — invokes a tool by name with the provided arguments.</description></item>
    ///   <item><description><c>resources/list</c> — returns all registered resource descriptors.</description></item>
    ///   <item><description><c>resources/read</c> — invokes a resource handler by URI.</description></item>
    ///   <item><description><c>prompts/list</c> — returns all registered prompt descriptors.</description></item>
    ///   <item><description><c>prompts/get</c> — invokes a prompt handler by name.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Tool invocation always routes through <see cref="MainThreadDispatcher.Execute{T}"/> so
    /// that the tool method body can safely call any Unity Editor API. The calling (network) thread
    /// blocks until the main thread completes execution and the result is available.
    /// </para>
    /// <para>
    /// All parameter conversion from JSON string values to the correct CLR types is handled by
    /// <see cref="ConvertArgument"/>. When a required parameter is absent from the request the
    /// method's declared default value is used; when there is no default, an error is returned.
    /// </para>
    /// </remarks>
    public partial class McpRequestHandler
    {
        #region CONSTRUCTOR

        public McpRequestHandler(ToolRegistry tools, ResourceRegistry resources, PromptRegistry prompts)
        {
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        }

        #endregion

        #region FIELDS

        private readonly ToolRegistry _tools;
        private readonly ResourceRegistry _resources;
        private readonly PromptRegistry _prompts;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Appends <paramref name="s"/> to <paramref name="sb"/> with JSON-unsafe characters
        /// replaced by their escape sequences. Delegates to <see cref="McpProtocol.AppendEscaped"/>.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="s">The string to escape and append. May be <c>null</c>.</param>
        private static void AppendEscaped(StringBuilder sb, string? s)
        {
            McpProtocol.AppendEscaped(sb, s);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Parses <paramref name="jsonRpc"/>, dispatches to the appropriate internal handler,
        /// and returns a complete JSON-RPC 2.0 response string.
        /// </summary>
        /// <param name="jsonRpc">
        /// The raw JSON-RPC 2.0 request frame received from the WebSocket client.
        /// Must not be <c>null</c> or empty.
        /// </param>
        /// <returns>
        /// A JSON-RPC 2.0 response frame. Never <c>null</c> or empty — on parse errors the method
        /// returns a JSON-RPC error response with code <see cref="McpProtocol.PARSE_ERROR"/>.
        /// </returns>
        public string HandleRequest(string jsonRpc)
        {
            if (string.IsNullOrEmpty(jsonRpc))
            {
                return McpProtocol.BuildErrorResponse("null", McpProtocol.PARSE_ERROR, "Empty request.");
            }

            string method;
            string id;
            string paramsJson;
            try
            {
                (method, id, paramsJson) = McpProtocol.ParseRequest(jsonRpc);
            }
            catch (Exception ex)
            {
                McpLogger.Error("McpRequestHandler: Failed to parse JSON-RPC request.", ex);
                return McpProtocol.BuildErrorResponse("null", McpProtocol.PARSE_ERROR, ex.Message);
            }

            if (string.IsNullOrEmpty(method))
            {
                return McpProtocol.BuildErrorResponse(id, McpProtocol.INVALID_REQUEST, "Missing 'method' field.");
            }

            if (id == "null" && method.StartsWith(McpConstants.NOTIFICATION_PREFIX))
            {
                return string.Empty;
            }

            try
            {
                string resultJson = DispatchMethod(method, paramsJson);
                return McpProtocol.BuildResponse(id, resultJson);
            }
            catch (McpMethodException mex)
            {
                return McpProtocol.BuildErrorResponse(id, mex.Code, mex.Message);
            }
            catch (System.Reflection.TargetInvocationException tex)
            {
                Exception inner = tex.InnerException ?? tex;
                McpLogger.Error($"McpRequestHandler: Invocation error for '{method}': {inner.Message}\n{inner.StackTrace}");
                return McpProtocol.BuildErrorResponse(id, McpProtocol.INTERNAL_ERROR, inner.Message);
            }
            catch (Exception ex)
            {
                McpLogger.Error($"McpRequestHandler: Unhandled exception for method '{method}'.", ex);
                return McpProtocol.BuildErrorResponse(id, McpProtocol.INTERNAL_ERROR, ex.Message);
            }
        }

        #endregion

        #region METHOD DISPATCH

        /// <summary>
        /// Routes the parsed method name to the correct internal handler.
        /// </summary>
        /// <param name="method">The JSON-RPC method name.</param>
        /// <param name="paramsJson">The raw JSON params object.</param>
        /// <returns>A valid JSON string to embed as the <c>"result"</c> field.</returns>
        /// <exception cref="McpMethodException">
        /// Thrown when the method is unknown or a method-specific error occurs.
        /// </exception>
        private string DispatchMethod(string method, string paramsJson)
        {
            return method switch
            {
                McpConstants.METHOD_INITIALIZE => HandleInitialize(),
                McpConstants.METHOD_TOOLS_LIST => HandleToolsList(),
                McpConstants.METHOD_TOOLS_CALL => HandleToolsCall(paramsJson),
                McpConstants.METHOD_RESOURCES_LIST => HandleResourcesList(),
                McpConstants.METHOD_RESOURCES_READ => HandleResourcesRead(paramsJson),
                McpConstants.METHOD_PROMPTS_LIST => HandlePromptsList(),
                McpConstants.METHOD_PROMPTS_GET => HandlePromptsGet(paramsJson),
                _ => throw new McpMethodException(
                                        McpProtocol.METHOD_NOT_FOUND,
                                        $"Method not found: '{method}'."),
            };
        }

        #endregion
    }

    #region MCP METHOD EXCEPETION

    /// <summary>
    /// Internal exception used to propagate JSON-RPC error codes through the dispatcher
    /// without polluting the public API surface.
    /// </summary>
    internal sealed class McpMethodException : Exception
    {
        #region CONSTRUCTOR

        public McpMethodException(int code, string message) : base(message)
        {
            Code = code;
        }

        #endregion

        #region PROPERTIES

        /// <summary>Gets the JSON-RPC error code to embed in the error response.</summary>
        public int Code { get; }

        #endregion
    }

    #endregion
}