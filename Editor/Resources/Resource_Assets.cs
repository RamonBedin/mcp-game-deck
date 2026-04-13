#nullable enable
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that lists project assets, optionally filtered by type.
    /// Results are capped at 200 entries to avoid excessively large responses.
    /// </summary>
    [McpResourceType]
    public class Resource_Assets
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const string ASSETS_ROOT_FOLDER = "Assets";
        private const int MAX_ASSET_RESULTS = 200;

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns a list of project assets, optionally filtered by a type search string.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <param name="filter">An AssetDatabase search filter such as 't:Prefab' or 't:ScriptableObject'. Empty returns all assets.</param>
        /// <returns>An array of resource content entries containing the asset list as plain text.</returns>
        [McpResource
        (
            Name = "Project Assets",
            Route = "mcp-game-deck://assets/{filter}",
            MimeType = "text/plain",
            Description = "Lists project assets, optionally filtered by type (e.g. 't:Prefab', 't:Material', " +
                "'t:ScriptableObject', 't:Scene'). Without filter lists all assets in Assets/ folder."
        )]
        public ResourceResponse[] GetAssets(string uri, string filter)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var searchFilter = string.IsNullOrWhiteSpace(filter) ? "" : filter;
                var guids = AssetDatabase.FindAssets(searchFilter, new[] { ASSETS_ROOT_FOLDER });

                var sb = new StringBuilder();
                sb.AppendLine($"Assets ({guids.Length} found, filter: '{searchFilter}'):");

                int limit = guids.Length < MAX_ASSET_RESULTS ? guids.Length : MAX_ASSET_RESULTS;

                for (int i = 0; i < limit; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    sb.AppendLine($"  {path} [{type?.Name ?? "Unknown"}]");
                }

                if (guids.Length > MAX_ASSET_RESULTS)
                {
                    sb.AppendLine($"  ... and {guids.Length - MAX_ASSET_RESULTS} more. Use a more specific filter.");
                }

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()

                ).MakeArray();
            });
        }

        #endregion
    }
}
