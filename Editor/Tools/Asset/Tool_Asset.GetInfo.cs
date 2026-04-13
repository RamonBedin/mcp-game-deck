#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region GET INFO

        /// <summary>
        /// Returns detailed information about an asset including type, GUID, size, and dependencies.
        /// </summary>
        /// <param name="assetPath">Project-relative path to the asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> with asset metadata (type, GUID, size, labels, dependencies), or an error if not found.</returns>
        [McpTool("asset-get-info", Title = "Asset / Get Info", ReadOnlyHint = true)]
        [Description("Returns detailed information about an asset: type, GUID, file size, labels, and dependencies.")]
        public ToolResponse GetInfo(
            [Description("Asset path (e.g. 'Assets/Prefabs/Player.prefab').")] string assetPath
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

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                var sb = new StringBuilder();
                sb.AppendLine($"Asset: {assetPath}");
                sb.AppendLine($"  Type: {asset.GetType().FullName}");
                sb.AppendLine($"  GUID: {guid}");
                sb.AppendLine($"  Name: {asset.name}");

                var fileInfo = new System.IO.FileInfo(assetPath);

                if (fileInfo.Exists)
                {
                    sb.AppendLine($"  Size: {fileInfo.Length} bytes");
                }

                string[] labels = AssetDatabase.GetLabels(asset);

                if (labels.Length > 0)
                {
                    sb.Append("  Labels: ");

                    for (int i = 0; i < labels.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append(labels[i]);
                    }

                    sb.AppendLine();
                }

                string[] deps = AssetDatabase.GetDependencies(assetPath, false);

                if (deps.Length > 0)
                {
                    sb.AppendLine($"  Dependencies ({deps.Length}):");
                    int max = deps.Length < 10 ? deps.Length : 10;

                    for (int i = 0; i < max; i++)
                    {
                        sb.AppendLine($"    {deps[i]}");
                    }

                    if (deps.Length > 10)
                    {
                        sb.AppendLine($"    ... and {deps.Length - 10} more");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}