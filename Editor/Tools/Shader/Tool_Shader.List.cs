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
    public partial class Tool_Shader
    {
        #region TOOL METHODS

        /// <summary>
        /// Searches the AssetDatabase for Shader assets and returns their names and paths.
        /// Built-in and package shaders are excluded by default.
        /// </summary>
        /// <param name="nameFilter">Case-insensitive partial name filter. Empty returns all shaders.</param>
        /// <param name="includeBuiltin">When true, shaders outside the Assets folder are included.</param>
        /// <param name="maxResults">Maximum number of results to return.</param>
        /// <returns>Formatted text listing matching shader names and asset paths.</returns>
        [McpTool("shader-list", Title = "Shader / List")]
        [Description("Lists shaders in the project, optionally filtered by name pattern. " + "Returns shader name, asset path, and whether it's a built-in shader.")]
        public ToolResponse List(
            [Description("Filter by shader name pattern (case-insensitive partial match). Empty for all.")] string nameFilter = "",
            [Description("If true, include built-in/package shaders. Default false (project only).")] bool includeBuiltin = false,
            [Description("Maximum results. Default 50.")] int maxResults = 50
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var guids = AssetDatabase.FindAssets("t:Shader");
                var sb = new StringBuilder();
                int count = 0;

                foreach (var guid in guids)
                {
                    if (count >= maxResults)
                    {
                        break;
                    }

                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!includeBuiltin && !path.StartsWith("Assets/"))
                    {
                        continue;
                    }

                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

                    if (shader == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(nameFilter) && !shader.name.Contains(nameFilter, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    sb.AppendLine($"  {shader.name} — {path}");
                    count++;
                }

                return count == 0 ? ToolResponse.Text("No shaders found matching criteria.") : ToolResponse.Text($"Shaders ({count}):\n{sb}");
            });
        }

        #endregion
    }
}