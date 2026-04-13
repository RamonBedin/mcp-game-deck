#nullable enable

using System;

namespace GameDeck.MCP.Attributes
{
    /// <summary>
    /// Marks a method as an MCP tool that the AI agent can invoke.
    /// The tool discovery system registers each decorated method under the given <see cref="Id"/>
    /// and exposes it through the MCP protocol.
    /// </summary>
    /// <remarks>
    /// The decorated method must be public and return <c>ToolResponse</c>.
    /// Method parameters become the tool's input schema; decorate them with
    /// <see cref="System.ComponentModel.DescriptionAttribute"/> so the AI understands their purpose.
    /// Replaces <c>[McpPluginTool]</c> from <c>com.IvanMurzak.McpPlugin</c>.
    /// </remarks>
    /// <example>
    /// <code>
    /// [McpTool("physics-raycast", Title = "Physics / Raycast",
    ///           Description = "Casts a ray and returns the first hit.")]
    /// public ToolResponse Raycast([Description("Origin in world space")] Vector3 origin) { ... }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class McpToolAttribute : Attribute
    {
        #region CONSTRUCTOR

        /// <summary>
        /// Initialises a new <see cref="McpToolAttribute"/> with the given tool identifier.
        /// </summary>
        /// <param name="id">
        /// Unique kebab-case identifier for this tool (e.g. "physics-raycast").
        /// Must not be null or empty.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="id"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="id"/> is empty or consists only of whitespace.
        /// </exception>
        public McpToolAttribute(string id)
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id), "Tool id must not be null.");
            }
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Tool id must not be empty or whitespace.", nameof(id));
            }

            _id = id;
        }

        #endregion

        #region FIELDS

        private readonly string _id;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Unique identifier for this tool within the MCP registry.
        /// Must be kebab-case and globally unique across all tool classes (e.g. "physics-raycast").
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// Human-readable display name shown in tool listings and the Chat UI.
        /// Recommended format: "Domain / Action" (e.g. "Physics / Raycast").
        /// Defaults to <see cref="Id"/> when not set.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Plain-text description of what this tool does, passed to the AI as part of the tool schema.
        /// Write in English. Be concise but precise about side-effects and return values.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Hints to the AI client that this tool does not modify any state and is safe to call
        /// multiple times without side-effects.
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool ReadOnlyHint { get; set; } = false;

        /// <summary>
        /// Hints to the AI client that calling this tool multiple times with the same arguments
        /// produces the same result (i.e. the operation is idempotent).
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool IdempotentHint { get; set; } = false;

        /// <summary>
        /// Controls whether this tool is registered and exposed through the MCP protocol.
        /// Set to <c>false</c> to temporarily disable a tool without removing its implementation.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        #endregion
    }
}
