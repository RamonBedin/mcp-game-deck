#nullable enable

using System;
using System.Collections.Generic;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.MCP.Registry
{
    /// <summary>
    /// In-memory registry that stores all discovered <see cref="McpPromptInfo"/> instances
    /// and provides O(1) lookup by prompt name.
    /// </summary>
    /// <remarks>
    /// Populated once at Editor startup by the prompt-discovery system. After population the
    /// registry is queried by the MCP server dispatcher on every <c>prompts/get</c> request.
    /// Not thread-safe — all access is expected on the Unity main thread.
    /// </remarks>
    public class PromptRegistry
    {
        #region FIELDS

        private readonly Dictionary<string, McpPromptInfo> _prompts = new();

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the number of prompts currently registered.
        /// </summary>
        public int Count => _prompts.Count;

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Registers a prompt by its <see cref="McpPromptInfo.Name"/>.
        /// If a prompt with the same name is already registered, the duplicate is ignored and
        /// a warning is logged to the Unity console.
        /// </summary>
        /// <param name="prompt">The prompt metadata to register. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompt"/> is <c>null</c>.</exception>
        public void Register(McpPromptInfo prompt)
        {
            if (prompt == null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            if (_prompts.TryGetValue(prompt.Name, out McpPromptInfo? existing))
            {
                Debug.LogWarning(
                    $"[PromptRegistry] Duplicate prompt name \"{prompt.Name}\" — " +
                    $"skipping registration of {prompt.DeclaringType.FullName}.{prompt.Method.Name}. " +
                    $"Already registered by {existing.DeclaringType.FullName}.");
                return;
            }

            _prompts[prompt.Name] = prompt;
        }

        /// <summary>
        /// Looks up a registered prompt by its unique name.
        /// </summary>
        /// <param name="name">
        /// The prompt name to find, e.g. <c>"gameobject-handling-strategy"</c>.
        /// </param>
        /// <returns>
        /// The matching <see cref="McpPromptInfo"/>, or <c>null</c> if no prompt with that name
        /// has been registered.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c>.</exception>
        public McpPromptInfo? GetPrompt(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            _prompts.TryGetValue(name, out McpPromptInfo? result);
            return result;
        }

        /// <summary>
        /// Returns a snapshot list of all registered prompts in an unspecified order.
        /// </summary>
        /// <remarks>
        /// Returns a new <see cref="List{T}"/> each time so callers cannot mutate internal state.
        /// </remarks>
        /// <returns>A new list containing every registered <see cref="McpPromptInfo"/>.</returns>
        public List<McpPromptInfo> GetAllPrompts()
        {
            List<McpPromptInfo> result = new(_prompts.Count);

            foreach (KeyValuePair<string, McpPromptInfo> pair in _prompts)
            {
                result.Add(pair.Value);
            }

            return result;
        }

        #endregion
    }
}
