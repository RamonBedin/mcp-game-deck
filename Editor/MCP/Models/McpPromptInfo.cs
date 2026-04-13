#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;

namespace GameDeck.MCP.Models
{
    /// <summary>
    /// Immutable metadata record describing a discovered and registered MCP prompt.
    /// Produced by the prompt-discovery system when it scans assemblies for methods
    /// decorated with <c>[McpPrompt]</c>. Unlike tools and resources, prompts do not
    /// require a type-level attribute on the declaring class.
    /// </summary>
    /// <remarks>
    /// MCP prompts are reusable, parameterised text templates that guide AI agent behaviour
    /// for a specific workflow (e.g. the GameObject handling strategy or build pipeline prompt).
    /// The discovery system stores both <see cref="DeclaringType"/> and <see cref="Method"/>
    /// so that the dispatcher can invoke the prompt handler without a second round of reflection.
    /// </remarks>
    public sealed class McpPromptInfo
    {
        #region CONSTRUCTOR

        public McpPromptInfo(string name, string description, List<McpParameterInfo> parameters, Type declaringType, MethodInfo method)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Prompt name must not be empty or whitespace.", nameof(name));
            }

            Name = name;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            DeclaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
            Method = method ?? throw new ArgumentNullException(nameof(method));
        }

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the unique prompt name used in MCP protocol messages,
        /// e.g. <c>"gameobject-handling-strategy"</c>.
        /// Matched against the <c>name</c> field in an MCP <c>prompts/get</c> request.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the human-readable description of what this prompt does,
        /// taken from the <c>[Description]</c> attribute on the prompt method.
        /// Shown to AI agents and developers in prompt listings.
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// Gets the ordered list of parameter metadata for this prompt's handler method.
        /// The order matches the C# method signature. Parameters with
        /// <see cref="McpParameterInfo.IsOptional"/> set to <c>true</c> are not required
        /// when the client invokes the prompt.
        /// </summary>
        public List<McpParameterInfo> Parameters { get; private set; }

        /// <summary>
        /// Gets the <see cref="System.Type"/> of the class that declares the prompt handler method.
        /// Used by the dispatcher to instantiate the declaring class before invocation.
        /// </summary>
        public Type DeclaringType { get; private set; }

        /// <summary>
        /// Gets the <see cref="MethodInfo"/> of the prompt handler method.
        /// The method must return <see cref="string"/> (the prompt text) or an async
        /// equivalent. Used by the dispatcher to invoke the method directly via reflection.
        /// </summary>
        public MethodInfo Method { get; private set; }

        #endregion
    }
}
