#nullable enable

using System;
using System.Reflection;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Immutable metadata record describing a discovered and registered MCP resource.
    /// Produced by the resource-discovery system when it scans assemblies for classes
    /// decorated with <c>[McpResourceType]</c> and methods decorated with <c>[McpResource]</c>.
    /// </summary>
    /// <remarks>
    /// MCP resources are read-only data endpoints identified by URI routes (e.g.
    /// <c>"mcp-game-deck://assets/{filter}"</c>). Route parameters in curly braces
    /// are extracted and forwarded as method arguments by the server dispatcher.
    /// Both <see cref="DeclaringType"/> and <see cref="Method"/> are retained so that the
    /// dispatcher can invoke the handler without a second round of reflection at request time.
    /// </remarks>
    public sealed class McpResourceInfo
    {
        #region CONSTRUCTOR

        public McpResourceInfo(string name, string route, string mimeType, string description, Type declaringType, MethodInfo method)
        {
            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (string.IsNullOrWhiteSpace(route))
            {
                throw new ArgumentException("Resource route must not be empty or whitespace.", nameof(route));
            }

            Name = name ?? throw new ArgumentNullException(nameof(name));
            Route = route;
            MimeType = mimeType ?? throw new ArgumentNullException(nameof(mimeType));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            Method = method ?? throw new ArgumentNullException(nameof(method));
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the human-readable name of the resource shown in resource listings,
        /// e.g. <c>"Project Assets"</c>. Taken from the <c>Name</c> property of the
        /// <c>[McpResource]</c> attribute.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the URI route template for this resource,
        /// e.g. <c>"mcp-game-deck://assets/{filter}"</c>.
        /// Curly-brace tokens are named route parameters that the dispatcher maps to
        /// method arguments by matching parameter names.
        /// </summary>
        public string Route { get; private set; }

        /// <summary>
        /// Gets the MIME type of the content returned by this resource,
        /// e.g. <c>"text/plain"</c> or <c>"application/json"</c>.
        /// Used by MCP clients to interpret the resource body.
        /// </summary>
        public string MimeType { get; private set; }

        /// <summary>
        /// Gets the description of this resource, taken from the <c>Description</c>
        /// property of the <c>[McpResource]</c> attribute.
        /// Shown to AI agents so they know when and how to request this resource.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the <see cref="System.Type"/> of the class that declares the resource handler method.
        /// Used by the dispatcher to instantiate the declaring class before invocation.
        /// </summary>
        public Type DeclaringType { get; private set; }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> of the resource handler method.
        /// Used by the dispatcher to invoke the method directly via reflection.
        /// The method must return <see cref="ResourceResponse"/>[] or a type assignable from it.
        /// </summary>
        public MethodInfo Method { get; private set; }

        #endregion
    }
}
