#nullable enable
using System;
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for interacting with the Unity Editor programmatically via MCP.
    /// Covers menu execution, play mode control, editor state, preferences,
    /// transform tools, undo/redo, and tag/layer management.
    /// </summary>
    [McpToolType]
    public partial class Tool_Editor
    {
        #region CONSTANTS

        private static readonly string[] _blockedMenuPrefixes = new[]
        {
            "File/Build",
            "File/Exit",
            "File/New Project",
            "File/Open Project",
            "File/Open Recent",
        };

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Executes a Unity Editor menu item identified by its full menu path.
        /// Equivalent to clicking the menu item manually.
        /// </summary>
        /// <param name="menuPath">Full menu path of the item to execute (e.g. 'File/Save Project', 'Edit/Undo').</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the menu item was executed,
        /// or an error when the item is not found or cannot be invoked.
        /// </returns>
        [McpTool("editor-execute-menu", Title = "Editor / Execute Menu")]
        [Description("Executes a Unity Editor menu item by its full path (e.g. 'File/Save Project', 'Assets/Refresh'). " + "Equivalent to clicking the menu item in the Editor.")]
        public ToolResponse ExecuteMenu(
            [Description("Full menu path to execute (e.g. 'File/Save Project', 'Edit/Undo', 'Assets/Refresh').")] string menuPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(menuPath))
                {
                    return ToolResponse.Error("menuPath is required.");
                }

                for (int i = 0; i < _blockedMenuPrefixes.Length; i++)
                {
                    if (menuPath.StartsWith(_blockedMenuPrefixes[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return ToolResponse.Error($"Menu path '{menuPath}' is blocked for security reasons.");
                    }
                }

                bool success = EditorApplication.ExecuteMenuItem(menuPath);

                if (!success)
                {
                    return ToolResponse.Error($"Menu item '{menuPath}' was not found or could not be executed. " + "Verify the exact menu path including slashes.");
                }

                return ToolResponse.Text($"Menu item '{menuPath}' executed successfully.");
            });
        }

        #endregion
    }
}