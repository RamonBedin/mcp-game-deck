#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Searches the AssetDatabase for UXML and/or USS files, optionally scoped to a folder.
        /// </summary>
        /// <param name="folderPath">Folder to restrict the search to (e.g. "Assets/UI"). Empty searches the entire project.</param>
        /// <param name="type">Asset type filter: "uxml", "uss", or "all".</param>
        /// <returns>Formatted text listing the matching asset paths grouped by type.</returns>
        [McpTool("uitoolkit-list", Title = "UI Toolkit / List Assets")]
        [Description("Lists all UI Toolkit assets (UXML and USS files) in the project, " + "optionally filtered by folder path.")]
        public ToolResponse ListUI(
            [Description("Filter by folder path (e.g. 'Assets/UI'). Empty for all.")] string folderPath = "",
            [Description("Filter by type: 'uxml', 'uss', or 'all'. Default 'all'.")] string type = "all"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();

                string typeLower = type.ToLowerInvariant();

                if (typeLower != "all" && typeLower != "uxml" && typeLower != "uss")
                {
                    return ToolResponse.Error("type must be 'uxml', 'uss', or 'all'.");
                }

                if (!string.IsNullOrWhiteSpace(folderPath) && !folderPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("folderPath must start with 'Assets/' (e.g. 'Assets/UI').");
                }

                string[]? folders = string.IsNullOrWhiteSpace(folderPath) ? null : new[] { folderPath };

                if (typeLower == "all" || typeLower == "uxml")
                {
                    var uxmlGuids = folders != null ? AssetDatabase.FindAssets("t:VisualTreeAsset", folders) : AssetDatabase.FindAssets("t:VisualTreeAsset");

                    sb.AppendLine($"UXML files ({uxmlGuids.Length}):");

                    foreach (var guid in uxmlGuids)
                    {
                        sb.AppendLine($"  {AssetDatabase.GUIDToAssetPath(guid)}");
                    }

                    sb.AppendLine();
                }

                if (typeLower == "all" || typeLower == "uss")
                {
                    var ussGuids = folders != null ? AssetDatabase.FindAssets("t:StyleSheet", folders) : AssetDatabase.FindAssets("t:StyleSheet");

                    sb.AppendLine($"USS files ({ussGuids.Length}):");

                    foreach (var guid in ussGuids)
                    {
                        sb.AppendLine($"  {AssetDatabase.GUIDToAssetPath(guid)}");
                    }
                }

                var result = sb.ToString().Trim();
                return string.IsNullOrWhiteSpace(result) ? ToolResponse.Text("No UI Toolkit assets found.") : ToolResponse.Text(result);
            });
        }

        #endregion
    }
}