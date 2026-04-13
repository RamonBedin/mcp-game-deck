#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Texture
    {
        #region TOOL METHODS

        /// <summary>
        /// Loads a texture asset and returns a detailed report of its dimensions, format, memory
        /// footprint, and TextureImporter settings.
        /// </summary>
        /// <param name="assetPath">Asset path of the texture to inspect (e.g. "Assets/Textures/player.png").</param>
        /// <returns>Formatted text with runtime properties and import settings of the texture.</returns>
        [McpTool("texture-inspect", Title = "Texture / Inspect")]
        [Description("Inspects a texture asset and returns its import settings, dimensions, format, " + "memory size, and compression information.")]
        public ToolResponse Inspect(
            [Description("Asset path of the texture (e.g. 'Assets/Textures/player.png').")] string assetPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("assetPath must start with 'Assets/' (e.g. 'Assets/Textures/MyTex.png').");
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);

                if (tex == null)
                {
                    return ToolResponse.Error($"Texture not found at '{assetPath}'.");
                }

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                var sb = new StringBuilder();

                sb.AppendLine($"Texture: {assetPath}");
                sb.AppendLine($"  Name: {tex.name}");
                sb.AppendLine($"  Dimensions: {tex.width}x{tex.height}");
                sb.AppendLine($"  Filter Mode: {tex.filterMode}");
                sb.AppendLine($"  Wrap Mode: {tex.wrapMode}");
                sb.AppendLine($"  Aniso Level: {tex.anisoLevel}");

                if (tex is Texture2D tex2d)
                {
                    sb.AppendLine($"  Format: {tex2d.format}");
                    sb.AppendLine($"  Mipmaps: {tex2d.mipmapCount}");
                    sb.AppendLine($"  Readable: {tex2d.isReadable}");
                }

                long memSize = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                sb.AppendLine($"  Memory: {memSize / 1024f:F2} KB ({memSize / (1024f * 1024f):F2} MB)");

                if (importer != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("Import Settings:");
                    sb.AppendLine($"  Texture Type: {importer.textureType}");
                    sb.AppendLine($"  sRGB: {importer.sRGBTexture}");
                    sb.AppendLine($"  Max Size: {importer.maxTextureSize}");
                    sb.AppendLine($"  Compression: {importer.textureCompression}");
                    sb.AppendLine($"  Generate Mipmaps: {importer.mipmapEnabled}");
                    sb.AppendLine($"  Read/Write Enabled: {importer.isReadable}");
                    sb.AppendLine($"  Alpha Source: {importer.alphaSource}");
                    sb.AppendLine($"  Alpha Is Transparency: {importer.alphaIsTransparency}");

                    if (importer.textureType == TextureImporterType.Sprite)
                    {
                        sb.AppendLine($"  Sprite Mode: {importer.spriteImportMode}");
                        sb.AppendLine($"  Pixels Per Unit: {importer.spritePixelsPerUnit}");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}