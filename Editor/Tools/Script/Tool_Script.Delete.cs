#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Script
    {
        #region TOOL METHODS

        /// <summary>
        /// Deletes a script file from the project via the Asset Database.
        /// </summary>
        /// <param name="path">Project-relative path of the script to delete (e.g. "Assets/Scripts/Old.cs").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the file was moved to trash,
        /// or an error if the path is missing, invalid, or the file does not exist.
        /// </returns>
        [McpTool("script-delete", Title = "Script / Delete")]
        [Description("Deletes a script file from the project.")]
        public ToolResponse Delete(
            [Description("File path to delete (e.g. 'Assets/Scripts/Old.cs').")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string? pathError = ValidateScriptPath(path);

                if (pathError != null)
                {
                    return ToolResponse.Error(pathError);
                }

                bool success = AssetDatabase.MoveAssetToTrash(path);
                return success ? ToolResponse.Text($"Deleted script '{path}' (moved to trash).") : ToolResponse.Error($"Failed to delete '{path}'. File may not exist.");
            });
        }

        #endregion
    }
}