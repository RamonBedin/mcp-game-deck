#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.PackageManager;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for managing Unity packages via the Package Manager API.
    /// Covers adding, removing, listing, searching, embedding, and resolving packages,
    /// as well as scoped registry management in manifest.json.
    /// </summary>
    [McpToolType]
    public partial class Tool_Package
    {
        #region TOOL METHODS

        /// <summary>
        /// Adds a package to the project via Unity Package Manager.
        /// </summary>
        /// <param name="packageId">Package identifier, Git URL (e.g. "https://..."), or local path (e.g. "file:../mypackage").</param>
        /// <param name="version">Optional version constraint (e.g. "3.0.6"). Ignored for Git URLs and local paths.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the display name, package name, and version that was added,
        /// or an error if the package could not be resolved or installed.
        /// </returns>
        [McpTool("package-add", Title = "Package / Add")]
        [Description("Adds a package to the project via Unity Package Manager. Supports registry packages, Git URLs, and local paths.")]
        public ToolResponse Add(
            [Description("Package identifier (e.g. 'com.unity.textmeshpro'), Git URL, or local path.")] string packageId,
            [Description("Optional version (e.g. '3.0.6'). If empty, uses latest.")] string version = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return ToolResponse.Error("packageId is required.");
                }

                string identifier = packageId;

                if (!string.IsNullOrWhiteSpace(version) && !packageId.Contains("://") && !packageId.Contains("file:"))
                {
                    identifier = $"{packageId}@{version}";
                }

                var request = Client.Add(identifier);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == StatusCode.Success)
                {
                    var info = request.Result;
                    return ToolResponse.Text($"Added package '{info.displayName}' ({info.name}@{info.version}).");
                }

                return ToolResponse.Error($"Failed to add package '{identifier}': {request.Error?.message ?? "Unknown error"}");
            });
        }

        #endregion
    }
}