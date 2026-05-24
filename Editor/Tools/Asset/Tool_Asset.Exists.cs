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
        #region EXISTS

        /// <summary>
        /// Cheap predicate for checking whether a project path resolves to an asset, a folder, or nothing.
        /// Use before asset-create-* / asset-copy / asset-rename to avoid collisions, or before asset-get-info to skip the heavier load.
        /// </summary>
        /// <param name="path">Project-relative path (e.g. 'Assets/Prefabs/Player.prefab' or 'Assets/Prefabs'). Auto-prepends 'Assets/' if omitted.</param>
        /// <returns>A <see cref="ToolResponse"/> reporting exists / kind ('asset' | 'folder' | 'none').</returns>
        [McpTool("asset-exists", Title = "Asset / Exists", ReadOnlyHint = true)]
        [Description("Lightweight check for whether a project path resolves to an asset, a folder, or nothing. Returns {exists, kind} where kind ∈ 'asset' | 'folder' | 'none'. Cheaper than asset-get-info — use this for guard checks before create/copy/rename.")]
        public ToolResponse Exists(
            [Description("Project-relative path to check (e.g. 'Assets/Prefabs/Player.prefab' or 'Assets/Prefabs'). Auto-prepends 'Assets/' if omitted.")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) && !string.Equals(path, "Assets", System.StringComparison.OrdinalIgnoreCase))
                {
                    path = "Assets/" + path;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return ToolResponse.Text($"path: '{path}' | exists: true | kind: folder");
                }

                string guid = AssetDatabase.AssetPathToGUID(path);

                if (!string.IsNullOrEmpty(guid))
                {
                    return ToolResponse.Text($"path: '{path}' | exists: true | kind: asset | guid: {guid}");
                }

                return ToolResponse.Text($"path: '{path}' | exists: false | kind: none");
            });
        }

        #endregion
    }
}