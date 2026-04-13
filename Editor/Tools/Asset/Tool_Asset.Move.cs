#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region MOVE

        /// <summary>
        /// Moves an asset from one path to another.
        /// </summary>
        /// <param name="sourcePath">Current project-relative path of the asset (e.g. 'Assets/Old/Player.prefab').</param>
        /// <param name="destinationPath">New project-relative path for the asset (e.g. 'Assets/New/Player.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the move, or an error message from <see cref="AssetDatabase.MoveAsset"/>.</returns>
        [McpTool("asset-move", Title = "Asset / Move")]
        [Description("Moves an asset from source path to destination path.")]
        public ToolResponse Move(
            [Description("Current asset path (e.g. 'Assets/Old/Player.prefab').")] string sourcePath,
            [Description("New asset path (e.g. 'Assets/New/Player.prefab').")] string destinationPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                {
                    return ToolResponse.Error("sourcePath is required.");
                }

                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    return ToolResponse.Error("destinationPath is required.");
                }

                if (!sourcePath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    sourcePath = "Assets/" + sourcePath;
                }

                if (!destinationPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    destinationPath = "Assets/" + destinationPath;
                }

                string result = AssetDatabase.MoveAsset(sourcePath, destinationPath);

                if (string.IsNullOrEmpty(result))
                {
                    return ToolResponse.Text($"Moved '{sourcePath}' to '{destinationPath}'.");
                }

                return ToolResponse.Error($"Failed to move: {result}");
            });
        }

        #endregion
    }
}