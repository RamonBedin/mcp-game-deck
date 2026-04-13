#nullable enable

using System;

namespace GameDeck.MCP.Attributes
{
    /// <summary>
    /// Marks a class as a container of MCP resource methods.
    /// The resource discovery system scans all assemblies for classes decorated with this attribute
    /// and registers their methods decorated with <see cref="McpResourceAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Resources are read-only data endpoints — they expose Unity project state to the AI agent
    /// without modifying it. Apply this attribute to any class that groups related resource
    /// implementations.
    /// Replaces <c>[McpPluginResourceType]</c> from <c>com.IvanMurzak.McpPlugin</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// [McpResourceType]
    /// public partial class Resource_Scene
    /// {
    ///     [McpResource(Name = "scene-hierarchy", Route = "unity://scene/hierarchy",
    ///                  MimeType = "application/json",
    ///                  Description = "Returns the full scene hierarchy as JSON.")]
    ///     public ResourceResponse Hierarchy() { ... }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class McpResourceTypeAttribute : Attribute
    {
    }
}
