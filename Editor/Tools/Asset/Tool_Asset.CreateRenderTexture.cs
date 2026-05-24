#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region CREATE RENDER TEXTURE

        /// <summary>
        /// Creates a new RenderTexture asset at the given project path with the specified dimensions and format.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path including the .renderTexture extension (e.g. 'Assets/RenderTextures/Mirror.renderTexture'). Auto-prepends 'Assets/' if omitted.</param>
        /// <param name="width">Width in pixels. Default 256.</param>
        /// <param name="height">Height in pixels. Default 256.</param>
        /// <param name="depth">Depth-buffer bit count: 0 (no depth), 16, 24, or 32. Default 24.</param>
        /// <param name="format">RenderTextureFormat name (e.g. 'ARGB32', 'RGB565', 'RGFloat'). Default 'ARGB32'. Case-insensitive.</param>
        /// <param name="filterMode">FilterMode name: 'Point', 'Bilinear', or 'Trilinear'. Default 'Bilinear'. Case-insensitive.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the created asset path, or an error when the path is invalid, the folder cannot be created, or the format / filter-mode strings are unrecognised.</returns>
        [McpTool("asset-create-render-texture", Title = "Asset / Create Render Texture")]
        [Description("Creates a new RenderTexture asset with explicit dimensions, depth-buffer, format, and filter mode. Strongly typed — no JSON blob. For other asset types use the dedicated creator: Material → material-create; PhysicsMaterial → physics-create-material; ScriptableObject → scriptableobject-create; AnimatorController → animation-configure-controller.")]
        public ToolResponse CreateRenderTexture(
            [Description("Project-relative asset path with .renderTexture extension (e.g. 'Assets/RenderTextures/Mirror.renderTexture'). Missing intermediate folders are created automatically. If a file already exists at the path, a unique suffix is appended.")] string assetPath,
            [Description("Width in pixels. Default 256.")] int width = 256,
            [Description("Height in pixels. Default 256.")] int height = 256,
            [Description("Depth-buffer bit count. Valid values: 0, 16, 24, 32. Default 24.")] int depth = 24,
            [Description("RenderTextureFormat name (e.g. 'ARGB32', 'RGB565', 'RGFloat', 'RFloat'). Case-insensitive. Default 'ARGB32'.")] string format = "ARGB32",
            [Description("FilterMode: 'Point', 'Bilinear', or 'Trilinear'. Case-insensitive. Default 'Bilinear'.")] string filterMode = "Bilinear"
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

                if (width <= 0 || height <= 0)
                {
                    return ToolResponse.Error($"width and height must be positive (got {width}x{height}).");
                }

                if (depth != 0 && depth != 16 && depth != 24 && depth != 32)
                {
                    return ToolResponse.Error($"depth must be 0, 16, 24, or 32 (got {depth}).");
                }

                if (!System.Enum.TryParse<RenderTextureFormat>(format, true, out var rtFormat))
                {
                    return ToolResponse.Error($"Unrecognised RenderTextureFormat '{format}'. Try 'ARGB32', 'RGB565', 'RGFloat', 'RFloat'.");
                }

                if (!System.Enum.TryParse<FilterMode>(filterMode, true, out var fMode))
                {
                    return ToolResponse.Error($"Unrecognised FilterMode '{filterMode}'. Valid values: 'Point', 'Bilinear', 'Trilinear'.");
                }

                string folder = Path.GetDirectoryName(assetPath) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                var rt = new RenderTexture(width, height, depth, rtFormat)
                {
                    filterMode = fMode
                };

                AssetDatabase.CreateAsset(rt, assetPath);
                AssetDatabase.SaveAssets();

                return ToolResponse.Text($"RenderTexture created at '{assetPath}' ({width}x{height}, depth {depth}, format {rtFormat}, filter {fMode}).");
            });
        }

        #endregion
    }
}