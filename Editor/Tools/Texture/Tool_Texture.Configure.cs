#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Texture
    {
        #region TOOL METHODS

        /// <summary>
        /// Applies import setting changes to a texture asset. Only parameters with non-sentinel
        /// values are applied; all others are left at their current values. Calls SaveAndReimport
        /// automatically after making changes.
        /// </summary>
        /// <param name="assetPath">Asset path of the texture to configure (e.g. "Assets/Textures/player.png").</param>
        /// <param name="maxSize">Maximum texture size. Pass -1 to skip.</param>
        /// <param name="compression">Compression quality keyword: none, low_quality, normal_quality, high_quality. Empty to skip.</param>
        /// <param name="textureType">Texture type keyword: default, normal_map, sprite, etc. Empty to skip.</param>
        /// <param name="srgb">"true" or "false" to control sRGB setting. Empty to skip.</param>
        /// <param name="mipmaps">"true" or "false" to enable/disable mipmap generation. Empty to skip.</param>
        /// <param name="readable">"true" or "false" to set CPU read/write access. Empty to skip.</param>
        /// <returns>Confirmation text listing each import setting that was changed.</returns>
        [McpTool("texture-configure", Title = "Texture / Configure Import Settings")]
        [Description("Configures texture import settings including max size, compression, " + "texture type, sRGB, mipmaps, and read/write enabled.")]
        public ToolResponse Configure(
            [Description("Asset path of the texture (e.g. 'Assets/Textures/player.png').")] string assetPath,
            [Description("Max texture size: 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192. -1 to skip.")] int maxSize = -1,
            [Description("Compression: none, low_quality, normal_quality, high_quality. Empty to skip.")] string compression = "",
            [Description("Texture type: default, normal_map, editor_gui, sprite, cursor, cookie, lightmap, " + "directional_lightmap, shadow_mask, single_channel. Empty to skip.")] string textureType = "",
            [Description("sRGB color texture. Empty to skip, 'true'/'false'.")] string srgb = "",
            [Description("Generate mipmaps. Empty to skip, 'true'/'false'.")] string mipmaps = "",
            [Description("Read/Write enabled. Empty to skip, 'true'/'false'.")] string readable = ""
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

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (importer == null)
                {
                    return ToolResponse.Error($"No texture importer found at '{assetPath}'.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Configured texture '{assetPath}':");

                if (maxSize > 0)
                {
                    importer.maxTextureSize = maxSize;
                    sb.AppendLine($"  Max Size: {maxSize}");
                }

                if (!string.IsNullOrWhiteSpace(compression))
                {
                    importer.textureCompression = compression.ToLowerInvariant() switch
                    {
                        "none" => TextureImporterCompression.Uncompressed,
                        "low_quality" or "low" => TextureImporterCompression.CompressedLQ,
                        "normal_quality" or "normal" => TextureImporterCompression.Compressed,
                        "high_quality" or "high" => TextureImporterCompression.CompressedHQ,
                        _ => importer.textureCompression
                    };

                    sb.AppendLine($"  Compression: {importer.textureCompression}");
                }

                if (!string.IsNullOrWhiteSpace(textureType))
                {
                    importer.textureType = textureType.ToLowerInvariant() switch
                    {
                        "default" => TextureImporterType.Default,
                        "normal_map" or "normalmap" => TextureImporterType.NormalMap,
                        "editor_gui" or "gui" => TextureImporterType.GUI,
                        "sprite" => TextureImporterType.Sprite,
                        "cursor" => TextureImporterType.Cursor,
                        "cookie" => TextureImporterType.Cookie,
                        "lightmap" => TextureImporterType.Lightmap,
                        "directional_lightmap" => TextureImporterType.DirectionalLightmap,
                        "shadow_mask" or "shadowmask" => TextureImporterType.Shadowmask,
                        "single_channel" => TextureImporterType.SingleChannel,
                        _ => importer.textureType
                    };

                    sb.AppendLine($"  Texture Type: {importer.textureType}");
                }

                if (!string.IsNullOrWhiteSpace(srgb))
                {
                    importer.sRGBTexture = srgb.ToLowerInvariant() == "true";
                    sb.AppendLine($"  sRGB: {importer.sRGBTexture}");
                }

                if (!string.IsNullOrWhiteSpace(mipmaps))
                {
                    importer.mipmapEnabled = mipmaps.ToLowerInvariant() == "true";
                    sb.AppendLine($"  Mipmaps: {importer.mipmapEnabled}");
                }

                if (!string.IsNullOrWhiteSpace(readable))
                {
                    importer.isReadable = readable.ToLowerInvariant() == "true";
                    sb.AppendLine($"  Readable: {importer.isReadable}");
                }

                importer.SaveAndReimport();
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}