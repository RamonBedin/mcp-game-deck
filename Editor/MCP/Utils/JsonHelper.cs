#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using GameDeck.MCP.Models;
using GameDeck.MCP.Server;

namespace GameDeck.MCP.Utils
{
    /// <summary>
    /// JSON serialization utilities for the MCP framework.
    /// Zero external dependencies — hand-written serialization for MCP protocol structures
    /// (tool lists, resource lists, input schemas).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hand-written serialization for MCP protocol structures.
    /// </para>
    /// <para>
    /// All methods are allocation-conscious: <see cref="StringBuilder"/> instances are reused
    /// where possible, and no LINQ is used anywhere in the implementation.
    /// JSON string escaping is delegated to <see cref="McpProtocol.AppendEscaped"/>.
    /// </para>
    /// </remarks>
    public static class JsonHelper
    {
        #region PRIVATE METHODS

        /// <summary>
        /// Appends the JSON object representation of a single <see cref="McpToolInfo"/> to
        /// <paramref name="sb"/>, including its full <c>inputSchema</c>.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="tool">The tool metadata to serialize.</param>
        private static void AppendToolInfo(StringBuilder sb, McpToolInfo tool)
        {
            sb.Append("{\"name\":\"");
            AppendEscaped(sb, tool.Id);
            sb.Append("\",\"description\":\"");
            AppendEscaped(sb, tool.Description);
            sb.Append("\",\"inputSchema\":");
            AppendInputSchema(sb, tool);
            sb.Append('}');
        }

        /// <summary>
        /// Appends the <c>inputSchema</c> JSON object for a tool.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="tool">The tool whose parameter schema is rendered.</param>
        private static void AppendInputSchema(StringBuilder sb, McpToolInfo tool)
        {
            sb.Append("{\"type\":\"object\",\"properties\":{");

            List<McpParameterInfo> parameters = tool.Parameters;

            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                McpParameterInfo param = parameters[i];
                sb.Append('"');
                AppendEscaped(sb, param.Name);
                sb.Append("\":{\"type\":\"");
                AppendEscaped(sb, param.JsonSchemaType);
                sb.Append("\",\"description\":\"");
                AppendEscaped(sb, param.Description);
                sb.Append('"');

                if (param.IsOptional && param.DefaultValue is not null)
                {
                    sb.Append(",\"default\":");
                    AppendDefaultValue(sb, param);
                }

                sb.Append('}');
            }

            sb.Append("},\"required\":[");
            AppendRequiredArray(sb, parameters);
            sb.Append("]}");
        }

        /// <summary>
        /// Appends the <c>"required"</c> array containing names of all non-optional parameters.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="parameters">The full parameter list for the tool.</param>
        private static void AppendRequiredArray(StringBuilder sb, List<McpParameterInfo> parameters)
        {
            bool first = true;

            for (int i = 0; i < parameters.Count; i++)
            {
                McpParameterInfo param = parameters[i];

                if (param.IsOptional)
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append('"');
                AppendEscaped(sb, param.Name);
                sb.Append('"');
                first = false;
            }
        }

        /// <summary>
        /// Appends the JSON representation of a parameter's default value.
        /// Numbers and booleans are written as bare tokens; everything else is quoted.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="param">The parameter whose default value is rendered.</param>
        private static void AppendDefaultValue(StringBuilder sb, McpParameterInfo param)
        {
            bool isNumeric = param.JsonSchemaType == McpParameterInfo.JSON_SCHEMA_NUMBER || param.JsonSchemaType == McpParameterInfo.JSON_SCHEMA_INTEGER;
            bool isBoolean = param.JsonSchemaType == McpParameterInfo.JSON_SCHEMA_BOOLEAN;
            string defaultText = param.DefaultValue is System.IFormattable fmt ? fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : param.DefaultValue?.ToString() ?? "null";

            if (isNumeric || isBoolean)
            {
                sb.Append(isBoolean ? defaultText.ToLowerInvariant() : defaultText);
            }
            else
            {
                sb.Append('"');
                AppendEscaped(sb, defaultText);
                sb.Append('"');
            }
        }

        /// <summary>
        /// Appends the JSON object representation of a single <see cref="McpResourceInfo"/> to
        /// <paramref name="sb"/>.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="resource">The resource metadata to serialize.</param>
        /// <remarks>
        /// The MCP protocol uses the key <c>"uri"</c> for the resource address.
        /// <see cref="McpResourceInfo.Route"/> holds the URI route template and is mapped
        /// to that key here.
        /// </remarks>
        private static void AppendResourceInfo(StringBuilder sb, McpResourceInfo resource)
        {
            sb.Append("{\"uri\":\"");
            AppendEscaped(sb, resource.Route);
            sb.Append("\",\"name\":\"");
            AppendEscaped(sb, resource.Name);
            sb.Append("\",\"description\":\"");
            AppendEscaped(sb, resource.Description);
            sb.Append("\",\"mimeType\":\"");
            AppendEscaped(sb, resource.MimeType);
            sb.Append("\"}");
        }

        /// <summary>
        /// Appends <paramref name="s"/> to <paramref name="sb"/> with all JSON-unsafe characters
        /// replaced by their escape sequences. Delegates to <see cref="McpProtocol.AppendEscaped"/>.
        /// </summary>
        /// <param name="sb">The target <see cref="StringBuilder"/>.</param>
        /// <param name="s">The string to escape and append. May be <c>null</c>.</param>
        private static void AppendEscaped(StringBuilder sb, string? s)
        {
            McpProtocol.AppendEscaped(sb, s);
        }

        #endregion

        #region MCP PROTOCOL SERIALIZERS

        /// <summary>
        /// Generates the JSON body for a <c>tools/list</c> MCP response.
        /// </summary>
        /// <param name="tools">
        /// The list of registered tools to include. Must not be <c>null</c>.
        /// An empty list produces a valid response with an empty <c>"tools"</c> array.
        /// </param>
        /// <returns>
        /// A JSON string conforming to the MCP <c>tools/list</c> result shape:
        /// <code>
        /// {
        ///   "tools": [
        ///     {
        ///       "name": "physics-raycast",
        ///       "description": "...",
        ///       "inputSchema": {
        ///         "type": "object",
        ///         "properties": { "originX": { "type": "number", "description": "..." } },
        ///         "required": ["originX"]
        ///       }
        ///     }
        ///   ]
        /// }
        /// </code>
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="tools"/> is <c>null</c>.
        /// </exception>
        public static string SerializeToolsList(List<McpToolInfo> tools)
        {
            if (tools is null)
            {
                throw new ArgumentNullException(nameof(tools));
            }

            var sb = new StringBuilder(512);
            sb.Append("{\"tools\":[");

            for (int i = 0; i < tools.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                AppendToolInfo(sb, tools[i]);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Generates the JSON body for a <c>resources/list</c> MCP response.
        /// </summary>
        /// <param name="resources">
        /// The list of registered resources to include. Must not be <c>null</c>.
        /// An empty list produces a valid response with an empty <c>"resources"</c> array.
        /// </param>
        /// <returns>
        /// A JSON string conforming to the MCP <c>resources/list</c> result shape:
        /// <code>
        /// {
        ///   "resources": [
        ///     { "uri": "mcp-game-deck://scenes-hierarchy", "name": "...", "description": "...", "mimeType": "text/plain" }
        ///   ]
        /// }
        /// </code>
        /// The <c>"uri"</c> field is sourced from <see cref="McpResourceInfo.Route"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="resources"/> is <c>null</c>.
        /// </exception>
        public static string SerializeResourcesList(List<McpResourceInfo> resources)
        {
            if (resources is null)
            {
                throw new ArgumentNullException(nameof(resources));
            }

            var sb = new StringBuilder(256);
            sb.Append("{\"resources\":[");

            for (int i = 0; i < resources.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                AppendResourceInfo(sb, resources[i]);
            }

            sb.Append("]}");
            return sb.ToString();
        }

        #endregion
    }
}
