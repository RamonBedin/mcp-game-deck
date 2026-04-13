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
    public partial class Tool_Texture
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new <see cref="Texture2D"/> asset filled with a uniform RGBA colour,
        /// encodes it to PNG, writes it to disk, and reimports it through the Asset Database.
        /// </summary>
        /// <param name="path">Project-relative path including the '.png' extension (e.g. 'Assets/Textures/White.png').</param>
        /// <param name="width">Width of the texture in pixels. Default 64.</param>
        /// <param name="height">Height of the texture in pixels. Default 64.</param>
        /// <param name="fillR">Red channel of the fill colour in the 0–1 range. Default 1.</param>
        /// <param name="fillG">Green channel of the fill colour in the 0–1 range. Default 1.</param>
        /// <param name="fillB">Blue channel of the fill colour in the 0–1 range. Default 1.</param>
        /// <param name="fillA">Alpha channel of the fill colour in the 0–1 range. Default 1.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the asset path of the created texture,
        /// or an error when the PNG cannot be written.
        /// </returns>
        [McpTool("texture-create", Title = "Texture / Create")]
        [Description("Creates a new solid-colour Texture2D PNG asset at the given path. " + "Fill colour is specified as RGBA in the 0–1 range.")]
        public ToolResponse CreateTexture(
            [Description("Project-relative path including '.png' extension (e.g. 'Assets/Textures/White.png').")] string path,
            [Description("Texture width in pixels. Default 64.")] int width = 64,
            [Description("Texture height in pixels. Default 64.")] int height = 64,
            [Description("Red channel of fill colour, 0–1. Default 1.")] float fillR = 1f,
            [Description("Green channel of fill colour, 0–1. Default 1.")] float fillG = 1f,
            [Description("Blue channel of fill colour, 0–1. Default 1.")] float fillB = 1f,
            [Description("Alpha channel of fill colour, 0–1. Default 1.")] float fillA = 1f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("path must start with 'Assets/' (e.g. 'Assets/Textures/MyTex.png').");
                }

                if (width < 1 || height < 1)
                {
                    return ToolResponse.Error($"width and height must be >= 1. Got {width}x{height}.");
                }

                string folder = Path.GetDirectoryName(path) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                var colour = new Color(fillR, fillG, fillB, fillA);
                Color32 colour32 = colour;

                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color32[width * height];

                System.Array.Fill(pixels, colour32);

                tex.SetPixels32(pixels);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                if (png == null || png.Length == 0)
                {
                    return ToolResponse.Error("Failed to encode texture to PNG.");
                }

                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return ToolResponse.Text($"Texture created at '{path}'  ({width}x{height}, fill={colour}).");
            });
        }

        #endregion
    }
}