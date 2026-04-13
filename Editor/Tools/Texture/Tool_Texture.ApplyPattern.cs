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
        #region TOOOL METHODS

        /// <summary>
        /// Loads a Texture2D PNG asset, applies a procedural pattern pixel-by-pixel,
        /// saves the result back to disk, and reimports it.
        /// </summary>
        /// <param name="path">Project-relative path to the target PNG texture (e.g. 'Assets/Textures/Ground.png').</param>
        /// <param name="pattern">Pattern to apply: "checkerboard" or "stripes-horizontal" or "stripes-vertical". Default "checkerboard".</param>
        /// <param name="patternSize">Size in pixels of one pattern cell. Default 8.</param>
        /// <param name="color1Hex">Hex colour string for the first pattern colour (e.g. '#FFFFFF'). Default white.</param>
        /// <param name="color2Hex">Hex colour string for the second pattern colour (e.g. '#000000'). Default black.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the pattern was applied,
        /// or an error when the texture cannot be loaded or written.
        /// </returns>
        [McpTool("texture-apply-pattern", Title = "Texture / Apply Pattern")]
        [Description("Applies a procedural pattern to a Texture2D PNG asset. " + "pattern values: 'checkerboard', 'stripes-horizontal', 'stripes-vertical'. " + "Colors are hex strings (e.g. '#FF0000'). Overwrites the file on disk.")]
        public ToolResponse ApplyPattern(
            [Description("Project-relative path to the texture PNG (e.g. 'Assets/Textures/Ground.png').")] string path,
            [Description("Pattern to apply: 'checkerboard', 'stripes-horizontal', 'stripes-vertical'. Default 'checkerboard'.")] string pattern = "checkerboard",
            [Description("Size of one pattern cell in pixels. Default 8.")] int patternSize = 8,
            [Description("Hex colour string for the first pattern colour (e.g. '#FFFFFF'). Default white.")] string color1Hex = "#FFFFFF",
            [Description("Hex colour string for the second pattern colour (e.g. '#000000'). Default black.")] string color2Hex = "#000000"
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

                if (patternSize < 1)
                {
                    patternSize = 1;
                }

                if (!ColorUtility.TryParseHtmlString(color1Hex, out Color c1))
                {
                    return ToolResponse.Error($"Cannot parse color1Hex '{color1Hex}'. Use format '#RRGGBB' or '#RRGGBBAA'.");
                }

                if (!ColorUtility.TryParseHtmlString(color2Hex, out Color c2))
                {
                    return ToolResponse.Error($"Cannot parse color2Hex '{color2Hex}'. Use format '#RRGGBB' or '#RRGGBBAA'.");
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                bool wasReadable = false;

                if (importer != null)
                {
                    wasReadable = importer.isReadable;

                    if (!wasReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (tex == null)
                {
                    return ToolResponse.Error($"Texture2D not found at '{path}'.");
                }

                int w = tex.width;
                int h = tex.height;
                var pixels = new Color32[w * h];
                Color32 c132 = c1;
                Color32 c232 = c2;
                string normPattern = pattern.ToLowerInvariant();

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var useFirst = normPattern switch
                        {
                            "stripes-horizontal" => (y / patternSize) % 2 == 0,
                            "stripes-vertical" => (x / patternSize) % 2 == 0,
                            _ => ((x / patternSize) + (y / patternSize)) % 2 == 0,
                        };

                        pixels[y * w + x] = useFirst ? c132 : c232;
                    }
                }

                var writeable = new Texture2D(w, h, TextureFormat.RGBA32, false);
                writeable.SetPixels32(pixels);
                writeable.Apply();

                byte[] png = writeable.EncodeToPNG();
                Object.DestroyImmediate(writeable);

                if (png == null || png.Length == 0)
                {
                    return ToolResponse.Error("Failed to encode texture to PNG.");
                }

                File.WriteAllBytes(path, png);

                if (importer != null && !wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return ToolResponse.Text($"Pattern '{pattern}' applied to '{path}'  ({w}x{h}, cellSize={patternSize}).");
            });
        }

        #endregion
    }
}