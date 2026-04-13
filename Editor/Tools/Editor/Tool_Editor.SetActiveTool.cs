#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region TOOL METHODS

        /// <summary>
        /// Sets <see cref="Tools.current"/> to the specified transform tool,
        /// equivalent to pressing the corresponding shortcut key (W/E/R/T/Y/Q) in the Editor.
        /// </summary>
        /// <param name="toolName">
        /// The tool to activate. Accepted values (case-insensitive):
        /// Move, Rotate, Scale, Rect, Transform, View.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the active tool,
        /// or an error when <paramref name="toolName"/> is not recognised.
        /// </returns>
        [McpTool("editor-set-active-tool", Title = "Editor / Set Active Tool")]
        [Description("Sets the active transform tool in the Unity Editor scene view. " + "Accepted values: Move, Rotate, Scale, Rect, Transform, View.")]
        public ToolResponse SetActiveTool(
            [Description("Tool to activate: Move, Rotate, Scale, Rect, Transform, View. Default 'Move'.")] string toolName = "Move"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(toolName))
                {
                    return ToolResponse.Error("toolName is required.");
                }

                UnityEditor.Tool selected;
                string norm = toolName.Trim().ToLowerInvariant();

                switch (norm)
                {
                    case "move":
                        selected = UnityEditor.Tool.Move;
                        break;

                    case "rotate":
                        selected = UnityEditor.Tool.Rotate;
                        break;

                    case "scale":
                        selected = UnityEditor.Tool.Scale;
                        break;

                    case "rect":
                        selected = UnityEditor.Tool.Rect;
                        break;

                    case "transform":
                        selected = UnityEditor.Tool.Transform;
                        break;

                    case "view":
                        selected = UnityEditor.Tool.View;
                        break;

                    default:
                        return ToolResponse.Error($"Unknown tool '{toolName}'. Valid values: Move, Rotate, Scale, Rect, Transform, View.");
                }

                UnityEditor.Tools.current = selected;
                return ToolResponse.Text($"Active tool set to '{selected}'.");
            });
        }

        #endregion
    }
}