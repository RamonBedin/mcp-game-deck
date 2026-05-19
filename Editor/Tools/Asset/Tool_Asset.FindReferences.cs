#nullable enable
using System.Collections.Generic;
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
        #region FIND REFERENCES

        /// <summary>
        /// Finds every asset in the project that depends on (references) the given asset.
        /// Inverse of <see cref="AssetDatabase.GetDependencies(string, bool)"/>, which Unity
        /// only exposes in the outgoing direction. Implemented by scanning all assets and
        /// checking each one's dependency list — O(n × avg-deps) and can take several seconds
        /// on medium projects.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path to find references TO (e.g. 'Assets/Materials/Player.mat').</param>
        /// <param name="maxResults">Maximum references to return. Early-exits the scan once the cap is hit. Default 100.</param>
        /// <returns>A <see cref="ToolResponse"/> listing referencing asset paths, with a 'truncated: true' marker when the cap was hit.</returns>
        [McpTool("asset-find-references", Title = "Asset / Find References", ReadOnlyHint = true)]
        [Description("Finds every asset that references the given asset (incoming references — the inverse of asset-get-info's outgoing dependencies). PERFORMANCE WARNING: Unity has no native reverse-dependency API; this tool scans every asset in the project and inspects each one's dependency list. Expect several seconds on medium projects. Results are capped at maxResults (default 100) with early-exit.")]
        public ToolResponse FindReferences(
            [Description("Project-relative asset path to find references TO (e.g. 'Assets/Materials/Player.mat').")] string assetPath,
            [Description("Maximum referencing assets to return. Default 100. Scan early-exits once the cap is hit; remaining matches go undiscovered.")] int maxResults = 100
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

                if (maxResults <= 0)
                {
                    return ToolResponse.Error($"maxResults must be positive (got {maxResults}).");
                }

                var target = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (target == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string[] allGuids = AssetDatabase.FindAssets("");
                var references = new List<string>();
                bool truncated = false;

                for (int i = 0; i < allGuids.Length; i++)
                {
                    string candidatePath = AssetDatabase.GUIDToAssetPath(allGuids[i]);

                    if (string.IsNullOrEmpty(candidatePath))
                    {
                        continue;
                    }

                    if (string.Equals(candidatePath, assetPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string[] deps = AssetDatabase.GetDependencies(candidatePath, true);

                    for (int d = 0; d < deps.Length; d++)
                    {
                        if (string.Equals(deps[d], assetPath, System.StringComparison.OrdinalIgnoreCase))
                        {
                            references.Add(candidatePath);
                            break;
                        }
                    }

                    if (references.Count >= maxResults)
                    {
                        truncated = true;
                        break;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"References to '{assetPath}': {references.Count}{(truncated ? $" (truncated at maxResults={maxResults})" : "")}");

                for (int i = 0; i < references.Count; i++)
                {
                    sb.AppendLine($"  {references[i]}");
                }

                if (references.Count == 0)
                {
                    sb.AppendLine("  (none)");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}