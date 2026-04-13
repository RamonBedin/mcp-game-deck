#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region TOOL METHODS

        /// <summary>
        /// Performs a single undo step in the Unity Editor, reverting the most recent recorded change.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> confirming the undo was performed.</returns>
        [McpTool("editor-undo", Title = "Editor / Undo")]
        [Description("Performs one undo step in the Unity Editor, reverting the most recent recorded change.")]
        public ToolResponse PerformUndo()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Undo.PerformUndo();
                return ToolResponse.Text("Undo performed.");
            });
        }

        /// <summary>
        /// Performs a single redo step in the Unity Editor, reapplying the most recently undone change.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> confirming the redo was performed.</returns>
        [McpTool("editor-redo", Title = "Editor / Redo")]
        [Description("Performs one redo step in the Unity Editor, reapplying the most recently undone change.")]
        public ToolResponse PerformRedo()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Undo.PerformRedo();
                return ToolResponse.Text("Redo performed.");
            });
        }

        #endregion
    }
}