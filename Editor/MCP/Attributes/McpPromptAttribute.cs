#nullable enable

using System;

namespace GameDeck.MCP.Attributes
{
    /// <summary>
    /// Marks a method as an MCP prompt template that the AI agent can invoke to receive
    /// structured workflow guidance.
    /// </summary>
    /// <remarks>
    /// Prompts are reusable, parameterised text templates that guide the AI through
    /// multi-step Unity workflows (e.g. "create a new scene", "set up URP post-processing").
    /// The decorated method must be public and return <c>string</c> (the rendered prompt text).
    /// Method parameters, if any, become the prompt's fill-in variables.
    /// The prompt discovery system scans assemblies for methods decorated with this attribute
    /// inside classes decorated with <see cref="McpResourceTypeAttribute"/> or any class —
    /// decoration on the method alone is sufficient for discovery.
    /// </remarks>
    /// <example>
    /// <code>
    /// [McpPrompt(Name        = "setup-urp-post-processing",
    ///            Description = "Step-by-step guide for configuring URP post-processing volumes.")]
    /// public string SetupUrpPostProcessing(
    ///     [Description("Target quality level (Low/Medium/High)")] string quality = "Medium")
    /// {
    ///     return $"1. Open Project Settings → Graphics ...\n2. Quality: {quality}";
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class McpPromptAttribute : Attribute
    {
        #region PROPERTIES

        /// <summary>
        /// Unique name for this prompt template within the MCP registry.
        /// Should be kebab-case and descriptive of the workflow it guides
        /// (e.g. "setup-urp-post-processing").
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Plain-text description of what workflow this prompt guides the AI through.
        /// Write in English. This text is exposed to the AI client as part of the prompt schema.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Controls whether this prompt is registered and exposed through the MCP protocol.
        /// Set to <c>false</c> to temporarily disable a prompt without removing its implementation.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;

        #endregion
    }
}
