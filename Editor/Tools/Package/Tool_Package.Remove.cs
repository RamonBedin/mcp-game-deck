#nullable enable
using System.ComponentModel;
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
        /// Removes an installed package from the project via Unity Package Manager.
        /// </summary>
        /// <param name="packageId">The package identifier to remove (e.g. 'com.unity.textmeshpro').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming removal or describing the failure.</returns>
        [McpTool("package-remove", Title = "Package / Remove")]
        [Description("Removes an installed package from the project via Unity Package Manager. Pass the full reverse-domain package identifier.")]
        public ToolResponse Remove(
            [Description("Package identifier to remove (e.g. 'com.unity.textmeshpro'). Must be the exact package name, not a display name.")] string packageId
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(packageId))
                {
                    return ToolResponse.Error("packageId is required.");
                }

                var request = Client.Remove(packageId.Trim());

                while (!request.IsCompleted)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (request.Status == StatusCode.Success)
                {
                    return ToolResponse.Text($"Package '{packageId}' was removed successfully.");
                }

                return ToolResponse.Error($"Failed to remove package '{packageId}': {request.Error?.message ?? "Unknown error"}");
            });
        }

        #endregion
    }
}