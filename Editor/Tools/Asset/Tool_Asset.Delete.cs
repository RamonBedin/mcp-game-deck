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
        #region DELETE

        /// <summary>
        /// Deletes an asset, optionally moving it to the OS trash instead of permanent deletion.
        /// </summary>
        /// <param name="assetPath">Project-relative path of the asset to delete (e.g. 'Assets/Materials/Old.mat').</param>
        /// <param name="moveToTrash">When <c>true</c>, moves to OS trash instead of permanent deletion. Default <c>true</c>.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming deletion, or an error if it failed.</returns>
        [McpTool("asset-delete", Title = "Asset / Delete")]
        [Description("Deletes an asset from the project. Can move to trash instead of permanent deletion.")]
        public ToolResponse Delete(
            [Description("Asset path to delete.")] string assetPath,
            [Description("Move to OS trash instead of permanent delete. Default true.")] bool moveToTrash = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                bool success;

                if (moveToTrash)
                {
                    success = AssetDatabase.MoveAssetToTrash(assetPath);
                }
                else
                {
                    success = AssetDatabase.DeleteAsset(assetPath);
                }

                return success ? ToolResponse.Text($"Deleted '{assetPath}'" + (moveToTrash ? " (moved to trash)." : ".")) : ToolResponse.Error($"Failed to delete '{assetPath}'.");
            });
        }

        #endregion
    }
}