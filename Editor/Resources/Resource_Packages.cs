#nullable enable
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.PackageManager;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that lists all installed Unity packages with their name, version,
    /// source, and a truncated description.
    /// </summary>
    [McpResourceType]
    public class Resource_Packages
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const int POLL_INTERVAL_MS = 10;
        private const int MAX_DESCRIPTION_LENGTH = 120;

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns a list of all installed Unity packages retrieved via the Package Manager API.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <returns>An array of resource content entries containing the package list as plain text.</returns>
        [McpResource
        (
            Name = "Installed Packages",
            Route = "mcp-game-deck://packages",
            MimeType = "text/plain",
            Description = "Lists all installed Unity packages with their name, version, source, and description."
        )]
        public ResourceResponse[] GetPackages(string uri)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var request = Client.List(true, true);
                int elapsed = 0;
                const int MAX_WAIT_MS = 15000;

                while (!request.IsCompleted && elapsed < MAX_WAIT_MS)
                {
                    System.Threading.Thread.Sleep(POLL_INTERVAL_MS);
                    elapsed += POLL_INTERVAL_MS;
                }

                if (!request.IsCompleted)
                {
                    return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: "Package list request timed out after 15 seconds.").MakeArray();
                }

                var sb = new StringBuilder();

                if (request.Status == StatusCode.Success)
                {
                    var packages = request.Result;
                    sb.AppendLine($"Installed Packages ({packages.Length()}):");
                    sb.AppendLine();

                    foreach (var pkg in packages)
                    {
                        sb.AppendLine($"  {pkg.name} @ {pkg.version}");
                        sb.AppendLine($"    Display Name: {pkg.displayName}");
                        sb.AppendLine($"    Source: {pkg.source}");

                        if (!string.IsNullOrWhiteSpace(pkg.description))
                        {
                            var desc = pkg.description.Length > MAX_DESCRIPTION_LENGTH ? pkg.description[..MAX_DESCRIPTION_LENGTH] + "..." : pkg.description;
                            sb.AppendLine($"    Description: {desc}");
                        }

                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine($"Failed to list packages: {request.Error?.message}");
                }

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()).MakeArray();
            });
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for <see cref="PackageCollection"/> to supplement missing count access.
    /// </summary>
    internal static class PackageCollectionExtensions
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Counts the number of packages in a <see cref="PackageCollection"/> by iterating it.
        /// </summary>
        /// <param name="collection">The package collection to count.</param>
        /// <returns>The total number of packages in the collection.</returns>
        public static int Length(this PackageCollection collection)
        {
            int count = 0;

            foreach (var _ in collection)
            {
                count++;
            }

            return count;
        }

        #endregion
    }
}
