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
        #region RENAME

        /// <summary>
        /// Renames an asset file inside the project using <see cref="AssetDatabase.RenameAsset"/>.
        /// The asset stays in the same folder — only the file name (without extension) changes.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the asset to rename (e.g. 'Assets/Materials/OldName.mat').</param>
        /// <param name="newName">New file name without extension (e.g. 'NewName').</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the rename, or an error string returned
        /// by <see cref="AssetDatabase.RenameAsset"/> on failure.
        /// </returns>
        [McpTool("asset-rename", Title = "Asset / Rename")]
        [Description("Renames a project asset file. Provide the current asset path and the new file name (no extension). " + "The asset stays in the same folder.")]
        public ToolResponse Rename(
            [Description("Project-relative path of the asset to rename (e.g. 'Assets/Materials/OldName.mat').")] string assetPath,
            [Description("New file name without extension (e.g. 'NewName').")] string newName
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (string.IsNullOrWhiteSpace(newName))
                {
                    return ToolResponse.Error("newName is required.");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string error = AssetDatabase.RenameAsset(assetPath, newName);

                if (!string.IsNullOrEmpty(error))
                {
                    return ToolResponse.Error($"Rename failed: {error}");
                }

                AssetDatabase.SaveAssets();
                return ToolResponse.Text($"Asset renamed to '{newName}' (was '{assetPath}').");
            });
        }

        #endregion
    }
}