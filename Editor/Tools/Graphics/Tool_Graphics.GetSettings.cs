#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Graphics
    {
        #region TOOL METHODS

        /// <summary>
        /// Queries GraphicsSettings and QualitySettings and returns a formatted summary of the
        /// active render pipeline, quality level, VSync, shadows, anti-aliasing, and more.
        /// </summary>
        /// <returns>Multi-section text report of current graphics and quality settings.</returns>
        [McpTool("graphics-get-settings", Title = "Graphics / Get Settings")]
        [Description("Gets current graphics and quality settings including render pipeline, " + "quality level, resolution, VSync, shadow settings, and anti-aliasing.")]
        public ToolResponse GetSettings()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();

                sb.AppendLine("Graphics Settings:");
                var srp = GraphicsSettings.currentRenderPipeline;
                sb.AppendLine($"  Render Pipeline: {(srp != null ? srp.name : "Built-in")}");
                sb.AppendLine($"  Color Space: {QualitySettings.activeColorSpace}");

                sb.AppendLine();
                sb.AppendLine("Quality Settings:");
                sb.AppendLine($"  Quality Level: {QualitySettings.names[QualitySettings.GetQualityLevel()]} ({QualitySettings.GetQualityLevel()})");
                sb.AppendLine($"  VSync: {QualitySettings.vSyncCount}");
                sb.AppendLine($"  Anti-Aliasing: {QualitySettings.antiAliasing}x");
                sb.AppendLine($"  Anisotropic Filtering: {QualitySettings.anisotropicFiltering}");
                sb.AppendLine($"  Shadow Quality: {QualitySettings.shadows}");
                sb.AppendLine($"  Shadow Resolution: {QualitySettings.shadowResolution}");
                sb.AppendLine($"  Shadow Distance: {QualitySettings.shadowDistance}");
                sb.AppendLine($"  Texture Quality: {QualitySettings.globalTextureMipmapLimit}");
                sb.AppendLine($"  LOD Bias: {QualitySettings.lodBias}");
                sb.AppendLine($"  Max LOD Level: {QualitySettings.maximumLODLevel}");
                sb.AppendLine($"  Pixel Light Count: {QualitySettings.pixelLightCount}");

                sb.AppendLine();
                sb.AppendLine("Available Quality Levels:");

                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    sb.AppendLine($"  [{i}] {QualitySettings.names[i]}{(i == QualitySettings.GetQualityLevel() ? " (active)" : "")}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}