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
        #region FIND

        /// <summary>
        /// Searches for assets in the project using Unity's search filter syntax.
        /// </summary>
        /// <param name="searchFilter">Unity search filter string (e.g. 't:Prefab', 't:Texture2D sky', 'l:MyLabel').</param>
        /// <param name="folder">Root folder to search in. Default 'Assets'.</param>
        /// <param name="maxResults">Maximum number of results to return. Default 25.</param>
        /// <returns>A <see cref="ToolResponse"/> listing matching asset paths, or an error if the filter is empty.</returns>
        [McpTool("asset-find", Title = "Asset / Find", ReadOnlyHint = true)]
        [Description("Searches for assets using Unity filter syntax (e.g. 't:Prefab', 't:Material player', 'l:Important').")]
        public ToolResponse Find(
            [Description("Search filter (e.g. 't:Prefab', 't:Texture2D sky', 'l:MyLabel').")] string searchFilter,
            [Description("Folder to search in (e.g. 'Assets/Prefabs'). Default 'Assets'.")] string folder = "Assets",
            [Description("Maximum results to return. Default 25.")] int maxResults = 25
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(searchFilter))
                {
                    return ToolResponse.Error("searchFilter is required.");
                }

                string[] guids = AssetDatabase.FindAssets(searchFilter, new[] { folder });
                var sb = new StringBuilder();
                int count = guids.Length < maxResults ? guids.Length : maxResults;
                sb.AppendLine($"Found {guids.Length} assets (showing {count}):");

                for (int i = 0; i < count; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    sb.AppendLine($"  {path}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}