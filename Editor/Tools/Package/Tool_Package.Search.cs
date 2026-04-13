#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.PackageManager;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Package
    {
        #region TOOL METHODS

        /// <summary>
        /// Searches the Unity Package Manager registry for packages matching the given query string.
        /// Returns display name, package name, version, and a truncated description (100 chars) for each match.
        /// </summary>
        /// <param name="query">The search term to use when querying the registry.</param>
        /// <returns>A <see cref="ToolResponse"/> listing all matching packages.</returns>
        [McpTool("package-search", Title = "Package / Search", ReadOnlyHint = true)]
        [Description("Searches the Unity Package Manager registry for packages matching a query. Returns display name, name, version, and description (truncated to 100 chars) for each result.")]
        public ToolResponse Search(
            [Description("Search query string (e.g. 'cinemachine', 'com.unity.2d', 'input system'). Matches against package names and descriptions.")] string query
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return ToolResponse.Error("query is required.");
                }

                var request = Client.SearchAll(offlineMode: false);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status != StatusCode.Success)
                {
                    return ToolResponse.Error($"Registry search failed: {request.Error?.message ?? "Unknown error"}");
                }

                string trimmedQuery = query.Trim();
                var sb = new StringBuilder();
                int matchCount = 0;

                foreach (var pkg in request.Result)
                {
                    bool nameMatch = pkg.name.IndexOf(trimmedQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool displayMatch = !string.IsNullOrEmpty(pkg.displayName) && pkg.displayName.IndexOf(trimmedQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool descMatch = !string.IsNullOrEmpty(pkg.description) && pkg.description.IndexOf(trimmedQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!nameMatch && !displayMatch && !descMatch)
                    {
                        continue;
                    }

                    string truncatedDesc = string.Empty;

                    if (!string.IsNullOrWhiteSpace(pkg.description))
                    {
                        truncatedDesc = pkg.description.Length > 100 ? pkg.description[..100] + "..." : pkg.description;
                    }

                    sb.AppendLine($"  Display Name : {pkg.displayName}");
                    sb.AppendLine($"  Name         : {pkg.name}");
                    sb.AppendLine($"  Version      : {pkg.version}");

                    if (!string.IsNullOrEmpty(truncatedDesc))
                    {
                        sb.AppendLine($"  Description  : {truncatedDesc}");
                    }

                    sb.AppendLine();
                    matchCount++;
                }

                if (matchCount == 0)
                {
                    return ToolResponse.Text($"No packages found matching '{query}'.");
                }

                var header = new StringBuilder();
                header.AppendLine($"Search Results for '{query}' ({matchCount} packages):");
                header.AppendLine();
                header.Append(sb);

                return ToolResponse.Text(header.ToString());
            });
        }

        #endregion
    }
}