#nullable enable

using System;

namespace GameDeck.MCP.Attributes
{
    /// <summary>
    /// Marks a class as a container of MCP tool methods.
    /// The tool discovery system scans all assemblies for classes decorated with this attribute
    /// and registers their methods decorated with <see cref="McpToolAttribute"/>.
    /// </summary>
    /// <remarks>
    /// Apply this attribute to any class that groups related MCP tool implementations.
    /// The class itself does not need to inherit from any base type.
    /// Replaces <c>[McpPluginToolType]</c> from <c>com.IvanMurzak.McpPlugin</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// [McpToolType]
    /// public partial class Tool_Physics
    /// {
    ///     [McpTool("physics-raycast", Title = "Physics / Raycast")]
    ///     public ToolResponse Raycast(...) { ... }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class McpToolTypeAttribute : Attribute
    {
    }
}
