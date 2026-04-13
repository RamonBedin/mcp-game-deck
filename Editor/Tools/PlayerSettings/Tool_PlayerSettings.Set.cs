#nullable enable
using System;
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_PlayerSettings
    {
        #region TOOL METHODS

        /// <summary>
        /// Modifies Player Settings values. Only provided (non-empty) values are changed.
        /// </summary>
        /// <param name="companyName">Company name to set. Empty to leave unchanged.</param>
        /// <param name="productName">Product name to set. Empty to leave unchanged.</param>
        /// <param name="version">Bundle version string (e.g. "1.0.0"). Empty to leave unchanged.</param>
        /// <param name="colorSpace">Color space: "Linear" or "Gamma". Empty to leave unchanged.</param>
        /// <param name="runInBackground">Run in background: 1 = true, 0 = false, -1 = unchanged.</param>
        /// <param name="screenWidth">Default screen width in pixels. 0 to leave unchanged.</param>
        /// <param name="screenHeight">Default screen height in pixels. 0 to leave unchanged.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each changed setting,
        /// a notice when no changes were applied, or an error if colorSpace is invalid.
        /// </returns>
        [McpTool("player-settings-set", Title = "PlayerSettings / Set")]
        [Description("Modifies Player Settings. Only non-empty values are applied.")]
        public ToolResponse Set(
            [Description("Company name. Empty = unchanged.")] string companyName = "",
            [Description("Product name. Empty = unchanged.")] string productName = "",
            [Description("Bundle version (e.g. '1.0.0'). Empty = unchanged.")] string version = "",
            [Description("Color space: 'Linear' or 'Gamma'. Empty = unchanged.")] string colorSpace = "",
            [Description("Run in background. -1 = unchanged, 0 = false, 1 = true.")] int runInBackground = -1,
            [Description("Default screen width. 0 = unchanged.")] int screenWidth = 0,
            [Description("Default screen height. 0 = unchanged.")] int screenHeight = 0
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var changes = new System.Text.StringBuilder();

                if (!string.IsNullOrWhiteSpace(companyName))
                {
                    PlayerSettings.companyName = companyName;
                    changes.AppendLine($"  Company Name = {companyName}");
                }

                if (!string.IsNullOrWhiteSpace(productName))
                {
                    PlayerSettings.productName = productName;
                    changes.AppendLine($"  Product Name = {productName}");
                }

                if (!string.IsNullOrWhiteSpace(version))
                {
                    PlayerSettings.bundleVersion = version;
                    changes.AppendLine($"  Version = {version}");
                }

                if (!string.IsNullOrWhiteSpace(colorSpace))
                {
                    if (string.Equals(colorSpace, "Linear", StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerSettings.colorSpace = ColorSpace.Linear;
                        changes.AppendLine($"  Color Space = {colorSpace}");
                    }
                    else if (string.Equals(colorSpace, "Gamma", StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerSettings.colorSpace = ColorSpace.Gamma;
                        changes.AppendLine($"  Color Space = {colorSpace}");
                    }
                    else
                    {
                        return ToolResponse.Error($"Invalid colorSpace '{colorSpace}'. Use 'Linear' or 'Gamma'.");
                    }
                }

                if (runInBackground >= 0)
                {
                    PlayerSettings.runInBackground = runInBackground == 1;
                    changes.AppendLine($"  Run In Background = {runInBackground == 1}");
                }

                if (screenWidth > 0)
                {
                    PlayerSettings.defaultScreenWidth = screenWidth;
                    changes.AppendLine($"  Screen Width = {screenWidth}");
                }

                if (screenHeight > 0)
                {
                    PlayerSettings.defaultScreenHeight = screenHeight;
                    changes.AppendLine($"  Screen Height = {screenHeight}");
                }

                if (changes.Length == 0)
                {
                    return ToolResponse.Text("No changes applied (all parameters empty/default).");
                }

                return ToolResponse.Text($"Updated Player Settings:\n{changes}");
            });
        }

        #endregion
    }
}