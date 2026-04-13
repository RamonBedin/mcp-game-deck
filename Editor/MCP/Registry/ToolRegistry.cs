#nullable enable

using System;
using System.Collections.Generic;
using GameDeck.MCP.Models;
using UnityEngine;

namespace GameDeck.MCP.Registry
{
    /// <summary>
    /// In-memory registry that stores all discovered <see cref="McpToolInfo"/> instances
    /// and provides O(1) lookup by tool ID.
    /// </summary>
    /// <remarks>
    /// Populated once at Editor startup by the tool-discovery system. After population the
    /// registry is queried by the MCP server dispatcher on every <c>tools/call</c> request.
    /// Not thread-safe — all access is expected on the Unity main thread.
    /// </remarks>
    public class ToolRegistry
    {
        #region FIELDS

        private readonly Dictionary<string, McpToolInfo> _tools = new();

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Gets the number of tools currently registered.
        /// </summary>
        public int Count => _tools.Count;

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Registers a tool by its <see cref="McpToolInfo.Id"/>.
        /// If a tool with the same ID is already registered, the duplicate is ignored and
        /// a warning is logged to the Unity console.
        /// </summary>
        /// <param name="tool">The tool metadata to register. Must not be <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tool"/> is <c>null</c>.</exception>
        public void Register(McpToolInfo tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            if (_tools.TryGetValue(tool.Id, out McpToolInfo? existing))
            {
                Debug.LogWarning(
                    $"[ToolRegistry] Duplicate tool ID \"{tool.Id}\" — " +
                    $"skipping registration of {tool.DeclaringType.FullName}.{tool.Method.Name}. " +
                    $"Already registered by {existing.DeclaringType.FullName}.");
                return;
            }

            _tools[tool.Id] = tool;
        }

        /// <summary>
        /// Looks up a registered tool by its unique ID.
        /// </summary>
        /// <param name="id">The tool ID to find, e.g. <c>"physics-raycast"</c>.</param>
        /// <returns>
        /// The matching <see cref="McpToolInfo"/>, or <c>null</c> if no tool with that ID
        /// has been registered.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> is <c>null</c>.</exception>
        public McpToolInfo? GetTool(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            _tools.TryGetValue(id, out McpToolInfo? result);
            return result;
        }

        /// <summary>
        /// Returns a snapshot list of all registered tools in an unspecified order.
        /// </summary>
        /// <remarks>
        /// Returns a new <see cref="List{T}"/> each time so callers cannot mutate internal state.
        /// </remarks>
        /// <returns>A new list containing every registered <see cref="McpToolInfo"/>.</returns>
        public List<McpToolInfo> GetAllTools()
        {
            List<McpToolInfo> result = new(_tools.Count);

            foreach (KeyValuePair<string, McpToolInfo> pair in _tools)
            {
                result.Add(pair.Value);
            }

            return result;
        }

        #endregion
    }
}
