#nullable enable

using System;

namespace GameDeck.MCP.Attributes
{
    /// <summary>
    /// Marks a method as an MCP resource endpoint that the AI agent can read.
    /// Resources are read-only — they expose data about the Unity project state
    /// without performing any mutations.
    /// </summary>
    /// <remarks>
    /// The decorated method must be public and return <c>ResourceResponse</c>.
    /// It must not accept parameters (MCP resource GET semantics).
    /// The <see cref="Route"/> must follow the <c>unity://</c> URI scheme used by this framework
    /// (e.g. <c>unity://scene/hierarchy</c>).
    /// Replaces <c>[McpPluginResource]</c> from <c>com.IvanMurzak.McpPlugin</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// [McpResource(Name        = "scene-hierarchy",
    ///              Route       = "unity://scene/hierarchy",
    ///              MimeType    = "application/json",
    ///              Description = "Returns the full scene hierarchy as JSON.")]
    /// public ResourceResponse Hierarchy() { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class McpResourceAttribute : Attribute
    {
        #region PROPERTIES

        /// <summary>
        /// Logical name for this resource, used as the key in the MCP resource registry.
        /// Should be kebab-case and unique across all resource classes (e.g. "scene-hierarchy").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// URI that the AI client uses to request this resource.
        /// Must follow the <c>unity://</c> scheme (e.g. <c>unity://scene/hierarchy</c>).
        /// The route is matched by the request handler to locate the correct method.
        /// </summary>
        public string Route { get; set; } = string.Empty;

        /// <summary>
        /// MIME type of the data returned by this resource endpoint.
        /// Common values: <c>"application/json"</c>, <c>"text/plain"</c>, <c>"image/png"</c>.
        /// Defaults to an empty string; set explicitly to allow correct client-side handling.
        /// </summary>
        public string MimeType { get; set; } = string.Empty;

        /// <summary>
        /// Plain-text description of what data this resource exposes, passed to the AI
        /// as part of the resource schema. Write in English.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Controls whether this resource is registered and exposed through the MCP protocol.
        /// Set to <c>false</c> to temporarily disable a resource without removing its implementation.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        #endregion
    }
}
