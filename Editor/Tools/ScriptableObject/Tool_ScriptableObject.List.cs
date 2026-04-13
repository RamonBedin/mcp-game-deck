#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_ScriptableObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Lists ScriptableObject assets in the project, optionally filtered by type name
        /// and/or folder path. Returns asset path, type, and name for each match.
        /// </summary>
        /// <param name="typeName">
        /// Filter by ScriptableObject type name (e.g. <c>WeaponConfig</c>). Partial, case-insensitive
        /// match is applied. Pass an empty string to include all types.
        /// </param>
        /// <param name="folderPath">
        /// Restrict the search to a specific folder (e.g. <c>Assets/Data</c>).
        /// Pass an empty string to search all folders.
        /// </param>
        /// <param name="maxResults">Maximum number of results to return. Default 50.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing matching assets as
        /// <c>path [TypeName] "assetName"</c> lines, with a header showing the total count.
        /// Returns an informational message when no assets match the criteria.
        /// </returns>
        [McpTool("scriptableobject-list", Title = "ScriptableObject / List")]
        [Description("Lists ScriptableObject assets in the project, optionally filtered by type name " + "and/or folder path. Returns asset path, type, and name.")]
        public ToolResponse List(
            [Description("Filter by ScriptableObject type name (e.g. 'WeaponConfig'). Partial match supported. Empty for all.")] string typeName = "",
            [Description("Filter by folder path (e.g. 'Assets/Data'). Empty for all folders.")] string folderPath = "",
            [Description("Maximum number of results to return. Default 50.")] int maxResults = 50
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!string.IsNullOrWhiteSpace(folderPath) && !folderPath.StartsWith("Assets/") && folderPath != "Assets")
                {
                    return ToolResponse.Error("folderPath must start with 'Assets/'.");
                }

                var guids = string.IsNullOrWhiteSpace(folderPath) ? AssetDatabase.FindAssets("t:ScriptableObject") : AssetDatabase.FindAssets("t:ScriptableObject", new[] { folderPath });

                var sb = new StringBuilder();
                int count = 0;

                for (int g = 0; g < guids.Length; g++)
                {
                    if (count >= maxResults)
                    {
                        break;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                    if (asset == null)
                    {
                        continue;
                    }

                    var actualTypeName = asset.GetType().Name;

                    if (!string.IsNullOrWhiteSpace(typeName) && !actualTypeName.Contains(typeName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    sb.AppendLine($"  {path} [{actualTypeName}] \"{asset.name}\"");
                    count++;
                }

                if (count == 0)
                {
                    return ToolResponse.Text("No ScriptableObject assets found matching the criteria.");
                }

                var header = $"ScriptableObject Assets ({count} found" + (count >= maxResults ? $", showing first {maxResults}" : "") + "):";

                return ToolResponse.Text(header + "\n" + sb.ToString());
            });
        }

        #endregion
    }
}