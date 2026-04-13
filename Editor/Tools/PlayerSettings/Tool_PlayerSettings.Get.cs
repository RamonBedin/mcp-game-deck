#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Build;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for reading and modifying Unity Player Settings.
    /// </summary>
    [McpToolType]
    public partial class Tool_PlayerSettings
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns the current Player Settings values.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with company name, product name, version, color space,
        /// run-in-background flag, default screen dimensions, and fullscreen mode.
        /// </returns>
        [McpTool("player-settings-get", Title = "PlayerSettings / Get", ReadOnlyHint = true)]
        [Description("Returns current Player Settings: company name, product name, version, scripting backend, API level, etc.")]
        public ToolResponse Get()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Player Settings:");
                sb.AppendLine($"  Company Name: {PlayerSettings.companyName}");
                sb.AppendLine($"  Product Name: {PlayerSettings.productName}");
                sb.AppendLine($"  Version: {PlayerSettings.bundleVersion}");
                var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Application);
                sb.AppendLine($"  Default Icon: {(icons.Length > 0 && icons[0] != null ? "Set" : "Not set")}");
                sb.AppendLine($"  Color Space: {PlayerSettings.colorSpace}");
                sb.AppendLine($"  Scripting Backend: (use editor-get-pref)");
                sb.AppendLine($"  API Compatibility: (use editor-get-pref)");
                sb.AppendLine($"  Run In Background: {PlayerSettings.runInBackground}");
                sb.AppendLine($"  Default Screen Width: {PlayerSettings.defaultScreenWidth}");
                sb.AppendLine($"  Default Screen Height: {PlayerSettings.defaultScreenHeight}");
                sb.AppendLine($"  Fullscreen Mode: {PlayerSettings.fullScreenMode}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}