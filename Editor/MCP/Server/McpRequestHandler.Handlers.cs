#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.MCP.Server
{
    public partial class McpRequestHandler
    {
        #region HANDLER: INITIALIZE

        /// <summary>
        /// Handles the <c>initialize</c> method. Returns the server's capabilities,
        /// name, version, and the MCP protocol version it implements.
        /// </summary>
        /// <returns>JSON string conforming to the MCP initialize result shape.</returns>
        private static string HandleInitialize()
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"protocolVersion\":\"");
            sb.Append(McpConstants.PROTOCOL_VERSION);
            sb.Append("\",\"capabilities\":{\"tools\":{\"listChanged\":false},");
            sb.Append("\"resources\":{\"listChanged\":false},");
            sb.Append("\"prompts\":{\"listChanged\":false}},");
            sb.Append("\"serverInfo\":{\"name\":\"");
            sb.Append(McpConstants.SERVER_NAME);
            sb.Append("\",\"version\":\"");
            sb.Append(McpConstants.SERVER_VERSION);
            sb.Append("\"}}");

            return sb.ToString();
        }

        #endregion

        #region HANDLER: TOOLS/LIST

        /// <summary>
        /// Handles the <c>tools/list</c> method. Returns the JSON schema for every registered tool.
        /// </summary>
        /// <returns>JSON string conforming to the MCP <c>tools/list</c> result shape.</returns>
        private string HandleToolsList()
        {
            List<McpToolInfo> tools = _tools.GetAllTools();
            return JsonHelper.SerializeToolsList(tools);
        }

        #endregion

        #region HANDLER: TOOLS/CALL

        /// <summary>
        /// Handles the <c>tools/call</c> method.
        /// </summary>
        /// <param name="paramsJson">The raw JSON params object from the request.</param>
        /// <returns>JSON string conforming to the MCP <c>tools/call</c> result shape.</returns>
        /// <exception cref="McpMethodException">
        /// Thrown when the tool name is missing, the tool is not found, argument conversion
        /// fails, or the tool invocation itself throws.
        /// </exception>
        private string HandleToolsCall(string paramsJson)
        {
            string toolName = ExtractStringFromJson(paramsJson, "name");

            if (string.IsNullOrEmpty(toolName))
            {
                throw new McpMethodException(McpProtocol.INVALID_PARAMS, "tools/call requires a 'name' parameter.");
            }

            McpToolInfo? tool = _tools.GetTool(toolName) ?? throw new McpMethodException(McpProtocol.METHOD_NOT_FOUND, $"Tool not found: '{toolName}'.");
            string argumentsJson = ExtractObjectFromJson(paramsJson, "arguments");

            string argsPreview = argumentsJson.Length > 200 ? argumentsJson[..200] + "..." : argumentsJson;
            McpAuditLog.Write($"tools/call '{toolName}' args={argsPreview}");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            object?[] args = BuildArgumentArray(tool.Parameters, argumentsJson);

            ToolResponse response = MainThreadDispatcher.Execute(() =>
            {
                object? instance = tool.Method.IsStatic ? null : Activator.CreateInstance(tool.DeclaringType);

                object? raw = tool.Method.Invoke(instance, args);

                if (raw is ToolResponse tr)
                {
                    return tr;
                }

                return ToolResponse.Text(raw?.ToString() ?? string.Empty);
            });

            sw.Stop();
            string status = response.IsError ? "ERROR" : "OK";
            McpAuditLog.Write($"tools/call '{toolName}' {status} ({sw.ElapsedMilliseconds}ms)");

            return SerializeToolCallResult(response);
        }

        /// <summary>
        /// Serializes a <see cref="ToolResponse"/> into the JSON shape expected by MCP
        /// <c>tools/call</c>:
        /// <code>
        /// { "content": [{ "type": "text", "text": "..." }], "isError": false }
        /// </code>
        /// Image responses use <c>"type": "image"</c> with a <c>"data"</c> + <c>"mimeType"</c> pair.
        /// </summary>
        /// <param name="response">The tool response to serialize.</param>
        /// <returns>A valid JSON string.</returns>
        private static string SerializeToolCallResult(ToolResponse response)
        {
            var sb = new StringBuilder(response.Content.Length + 128);
            sb.Append("{\"content\":[");

            if (response.MimeType != null && response.MimeType.StartsWith(McpConstants.MIME_PREFIX_IMAGE))
            {
                if (!string.IsNullOrEmpty(response.AltText))
                {
                    sb.Append("{\"type\":\"text\",\"text\":\"");
                    AppendEscaped(sb, response.AltText);
                    sb.Append("\"},");
                }

                sb.Append("{\"type\":\"image\",\"data\":\"");
                AppendEscaped(sb, response.Content);
                sb.Append("\",\"mimeType\":\"");
                AppendEscaped(sb, response.MimeType);
                sb.Append("\"}");
            }
            else
            {
                sb.Append("{\"type\":\"text\",\"text\":\"");
                AppendEscaped(sb, response.Content);
                sb.Append("\"}");
            }

            sb.Append("],\"isError\":");
            sb.Append(response.IsError ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        #endregion

        #region HANDLER: RESOURCES/LIST

        /// <summary>
        /// Handles the <c>resources/list</c> method. Returns all registered resource descriptors.
        /// </summary>
        /// <returns>JSON string conforming to the MCP <c>resources/list</c> result shape.</returns>
        private string HandleResourcesList()
        {
            List<McpResourceInfo> resources = _resources.GetAllResources();
            return JsonHelper.SerializeResourcesList(resources);
        }

        #endregion

        #region HANDLER: RESOURCES/READ

        /// <summary>
        /// Handles the <c>resources/read</c> method.
        /// </summary>
        /// <param name="paramsJson">The raw JSON params object from the request.</param>
        /// <returns>JSON string conforming to the MCP <c>resources/read</c> result shape.</returns>
        /// <exception cref="McpMethodException">
        /// Thrown when the URI is missing, no matching resource is registered, or the
        /// resource handler throws.
        /// </exception>
        private string HandleResourcesRead(string paramsJson)
        {
            string uri = ExtractStringFromJson(paramsJson, "uri");

            if (string.IsNullOrEmpty(uri))
            {
                throw new McpMethodException(McpProtocol.INVALID_PARAMS, "resources/read requires a 'uri' parameter.");
            }

            McpResourceInfo? resource = _resources.FindByUri(uri) ?? throw new McpMethodException(McpProtocol.METHOD_NOT_FOUND, $"No resource registered for URI: '{uri}'.");
            object?[] args = BuildResourceArgumentArray(resource, uri);
            ResourceResponse[] contents = MainThreadDispatcher.Execute(() =>
            {
                object? instance = resource.Method.IsStatic ? null : Activator.CreateInstance(resource.DeclaringType);
                object? raw = resource.Method.Invoke(instance, args);

                if (raw is ResourceResponse[] arr)
                {
                    return arr;
                }

                return Array.Empty<ResourceResponse>();
            });

            return SerializeResourceReadResult(contents);
        }

        /// <summary>
        /// Builds the route-parameter argument array for a resource invocation by matching
        /// route template segments against the concrete URI.
        /// </summary>
        /// <param name="resource">The resource metadata containing the route template and method signature.</param>
        /// <param name="uri">The concrete URI from the request.</param>
        /// <returns>An object array aligned with the method's parameter list.</returns>
        private static object?[] BuildResourceArgumentArray(McpResourceInfo resource, string uri)
        {
            ParameterInfo[] methodParams = resource.Method.GetParameters();

            if (methodParams.Length == 0)
            {
                return Array.Empty<object?>();
            }

            string[] templateSegments = resource.Route.Split(McpConstants.URI_SEGMENT_SEPARATOR);
            string[] uriSegments = uri.Split(McpConstants.URI_SEGMENT_SEPARATOR);
            var routeValues = new Dictionary<string, string>(StringComparer.Ordinal);
            int segCount = templateSegments.Length < uriSegments.Length ? templateSegments.Length : uriSegments.Length;

            for (int i = 0; i < segCount; i++)
            {
                string seg = templateSegments[i];

                if (seg.Length >= McpConstants.MIN_PARAMETER_SEGMENT_LENGTH && seg[0] == McpConstants.TEMPLATE_PARAM_OPEN && seg[^1] == McpConstants.TEMPLATE_PARAM_CLOSE)
                {
                    string paramName = seg[1..^1];
                    routeValues[paramName] = uriSegments[i];
                }
            }

            object?[] args = new object?[methodParams.Length];

            for (int i = 0; i < methodParams.Length; i++)
            {
                ParameterInfo p = methodParams[i];
                string paramName = p.Name ?? string.Empty;

                if (routeValues.TryGetValue(paramName, out string? rawValue))
                {
                    args[i] = ConvertArgument(rawValue, p.ParameterType);
                }
                else if (string.Equals(paramName, McpConstants.PARAM_NAME_URI, StringComparison.Ordinal) && p.ParameterType == typeof(string))
                {
                    args[i] = uri;
                }
                else if (p.HasDefaultValue)
                {
                    args[i] = p.DefaultValue;
                }
                else
                {
                    args[i] = null;
                }
            }

            return args;
        }

        /// <summary>
        /// Serializes an array of <see cref="ResourceResponse"/> into the MCP
        /// <c>resources/read</c> result shape:
        /// <code>
        /// { "contents": [{ "uri": "...", "mimeType": "...", "text": "..." }] }
        /// </code>
        /// </summary>
        /// <param name="contents">The response array returned by the resource handler.</param>
        /// <returns>A valid JSON string.</returns>
        private static string SerializeResourceReadResult(ResourceResponse[] contents)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\"contents\":[");

            for (int i = 0; i < contents.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                ResourceResponse item = contents[i];
                sb.Append("{\"uri\":\"");
                AppendEscaped(sb, item.Uri);
                sb.Append("\",\"mimeType\":\"");
                AppendEscaped(sb, item.MimeType);
                sb.Append("\",\"text\":\"");
                AppendEscaped(sb, item.Text);
                sb.Append("\"}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        #endregion

        #region HANDLER: PROMPTS/LIST

        /// <summary>
        /// Handles the <c>prompts/list</c> method. Returns all registered prompt descriptors.
        /// </summary>
        /// <returns>JSON string conforming to the MCP <c>prompts/list</c> result shape.</returns>
        private string HandlePromptsList()
        {
            List<McpPromptInfo> prompts = _prompts.GetAllPrompts();

            var sb = new StringBuilder(256);
            sb.Append("{\"prompts\":[");

            for (int i = 0; i < prompts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                McpPromptInfo prompt = prompts[i];
                sb.Append("{\"name\":\"");
                AppendEscaped(sb, prompt.Name);
                sb.Append("\",\"description\":\"");
                AppendEscaped(sb, prompt.Description);
                sb.Append("\",\"arguments\":[");

                List<McpParameterInfo> parameters = prompt.Parameters;

                for (int p = 0; p < parameters.Count; p++)
                {
                    if (p > 0)
                    {
                        sb.Append(',');
                    }

                    McpParameterInfo param = parameters[p];
                    sb.Append("{\"name\":\"");
                    AppendEscaped(sb, param.Name);
                    sb.Append("\",\"description\":\"");
                    AppendEscaped(sb, param.Description);
                    sb.Append("\",\"required\":");
                    sb.Append(param.IsOptional ? "false" : "true");
                    sb.Append('}');
                }

                sb.Append("]}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        #endregion

        #region HANDLER: PROMPTS/GET

        /// <summary>
        /// Handles the <c>prompts/get</c> method.
        /// </summary>
        /// <param name="paramsJson">The raw JSON params object from the request.</param>
        /// <returns>JSON string conforming to the MCP <c>prompts/get</c> result shape.</returns>
        /// <exception cref="McpMethodException">
        /// Thrown when the prompt name is missing, the prompt is not found, or the handler throws.
        /// </exception>
        private string HandlePromptsGet(string paramsJson)
        {
            string promptName = ExtractStringFromJson(paramsJson, "name");

            if (string.IsNullOrEmpty(promptName))
            {
                throw new McpMethodException(McpProtocol.INVALID_PARAMS, "prompts/get requires a 'name' parameter.");
            }

            McpPromptInfo? prompt = _prompts.GetPrompt(promptName) ?? throw new McpMethodException(McpProtocol.METHOD_NOT_FOUND, $"Prompt not found: '{promptName}'.");
            string argumentsJson = ExtractObjectFromJson(paramsJson, "arguments");
            object?[] args = BuildArgumentArray(prompt.Parameters, argumentsJson);
            string promptText = MainThreadDispatcher.Execute(() =>
            {
                object? instance = prompt.Method.IsStatic ? null : Activator.CreateInstance(prompt.DeclaringType);
                object? raw = prompt.Method.Invoke(instance, args);
                return raw?.ToString() ?? string.Empty;
            });

            return SerializePromptsGetResult(promptName, promptText);
        }

        /// <summary>
        /// Serializes a prompt invocation result into the MCP <c>prompts/get</c> result shape:
        /// <code>
        /// { "description": "...", "messages": [{ "role": "user", "content": { "type": "text", "text": "..." } }] }
        /// </code>
        /// </summary>
        /// <param name="promptName">The name of the prompt, embedded as the description.</param>
        /// <param name="promptText">The rendered prompt text.</param>
        /// <returns>A valid JSON string.</returns>
        private static string SerializePromptsGetResult(string promptName, string promptText)
        {
            var sb = new StringBuilder(promptText.Length + 128);
            sb.Append("{\"description\":\"");
            AppendEscaped(sb, promptName);
            sb.Append("\",\"messages\":[{\"role\":\"user\",\"content\":{\"type\":\"text\",\"text\":\"");
            AppendEscaped(sb, promptText);
            sb.Append("\"}}]}");
            return sb.ToString();
        }

        #endregion
    }
}