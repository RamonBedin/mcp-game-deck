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
        /// Lists all packages currently installed in the project, including their display name,
        /// package name, version, and source.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> containing one line per installed package.</returns>
        [McpTool("package-list", Title = "Package / List", ReadOnlyHint = true)]
        [Description("Lists all packages installed in the project via Unity Package Manager. Returns display name, package name, version, and source for each package.")]
        public ToolResponse List()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var request = Client.List(offlineMode: false, includeIndirectDependencies: false);

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status != StatusCode.Success)
                {
                    return ToolResponse.Error($"Failed to list packages: {request.Error?.message ?? "Unknown error"}");
                }

                var sb = new StringBuilder();
                int count = 0;

                foreach (var pkg in request.Result)
                {
                    count++;
                }

                sb.AppendLine($"Installed Packages ({count}):");
                sb.AppendLine();

                foreach (var pkg in request.Result)
                {
                    sb.AppendLine($"  Display Name : {pkg.displayName}");
                    sb.AppendLine($"  Name         : {pkg.name}");
                    sb.AppendLine($"  Version      : {pkg.version}");
                    sb.AppendLine($"  Source       : {pkg.source}");
                    sb.AppendLine();
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}