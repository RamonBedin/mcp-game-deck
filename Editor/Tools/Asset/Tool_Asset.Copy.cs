#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for managing Unity project assets — find, create, copy, move, rename,
    /// delete, refresh, inspect metadata, and read/write importer settings.
    /// </summary>
    [McpToolType]
    public partial class Tool_Asset
    {
        #region COPY

        /// <summary>
        /// Copies an asset to a new path.
        /// </summary>
        /// <param name="sourcePath">Project-relative path of the asset to copy (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <param name="destinationPath">Project-relative destination path including filename (e.g. 'Assets/Prefabs/PlayerCopy.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the copy, or an error if it failed.</returns>
        [McpTool("asset-copy", Title = "Asset / Copy")]
        [Description("Copies an asset to a new path in the project.")]
        public ToolResponse Copy(
            [Description("Source asset path.")] string sourcePath,
            [Description("Destination asset path.")] string destinationPath
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

                bool success = AssetDatabase.CopyAsset(sourcePath, destinationPath);
                return success ? ToolResponse.Text($"Copied '{sourcePath}' to '{destinationPath}'.") : ToolResponse.Error($"Failed to copy '{sourcePath}' to '{destinationPath}'.");
            });
        }

        #endregion
    }
}