#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Immutable metadata record describing a discovered and registered MCP tool.
    /// Produced by the tool-discovery system when it scans assemblies for classes
    /// decorated with <c>[McpToolType]</c> and methods decorated with <c>[McpTool]</c>.
    /// </summary>
    /// <remarks>
    /// Both <see cref="DeclaringType"/> and <see cref="Method"/> are stored so that the
    /// MCP server dispatcher can invoke the tool method without a second round of reflection
    /// at call time.
    /// </remarks>
    public sealed class McpToolInfo
    {
        #region CONSTRUCTOR

        public McpToolInfo(string id, string title, string description, bool readOnlyHint, bool idempotentHint, List<McpParameterInfo> parameters, Type declaringType, MethodInfo method)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Tool ID must not be empty or whitespace.", nameof(id));
            }

            Id = id;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            ReadOnlyHint = readOnlyHint;
            IdempotentHint = idempotentHint;
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            Method = method ?? throw new ArgumentNullException(nameof(method));
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the unique tool identifier used in MCP protocol messages, e.g. <c>"physics-raycast"</c>.
        /// This value is matched against the <c>name</c> field in an MCP <c>tools/call</c> request.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Gets the human-readable display name shown in tool lists and documentation,
        /// e.g. <c>"Physics / Raycast"</c>.
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// Gets the description of what this tool does, taken from the <c>[Description]</c>
        /// attribute on the tool method. Used by MCP clients to explain the tool to AI agents.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this tool only reads state and never modifies the
        /// Unity project or scene. When <c>true</c> clients may call the tool without user
        /// confirmation. Corresponds to the MCP <c>readOnlyHint</c> annotation.
        /// </summary>
        public bool ReadOnlyHint { get; private set; }

        /// <summary>
        /// Gets a value indicating whether calling this tool multiple times with the same
        /// arguments produces the same result and has no additional side effects after the
        /// first call. Corresponds to the MCP <c>idempotentHint</c> annotation.
        /// </summary>
        public bool IdempotentHint { get; private set; }

        /// <summary>
        /// Gets the ordered list of parameter metadata for this tool's method.
        /// The order matches the C# method signature. Parameters with <see cref="McpParameterInfo.IsOptional"/>
        /// set to <c>true</c> are excluded from the JSON Schema <c>required</c> array.
        /// </summary>
        public List<McpParameterInfo> Parameters { get; private set; }

        /// <summary>
        /// Gets the <see cref="System.Type"/> of the class that declares the tool method.
        /// Used by the dispatcher to instantiate the declaring class (if needed) before invocation.
        /// </summary>
        public Type DeclaringType { get; private set; }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> of the tool method itself.
        /// Used by the dispatcher to invoke the method directly via reflection.
        /// </summary>
        public MethodInfo Method { get; private set; }

        #endregion
    }
}
