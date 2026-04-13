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
    /// <summary>
    /// MCP tool for creating and modifying <see cref="Texture2D"/> PNG assets
    /// in the Unity project, including operations such as creation, patterns,
    /// gradients, inspection, and import configuration.
    /// </summary>
    [McpToolType]
    public partial class Tool_Texture
    {
        #region TOOL METHODS

        /// <summary>
        /// Applies a two-colour gradient to a PNG texture asset. When the file does not
        /// exist it is created at the given path. The gradient supports two modes:
        /// <list type="bullet">
        ///   <item><c>linear</c> — interpolates along a directional axis defined by <paramref name="angle"/> (0° = left→right).</item>
        ///   <item><c>radial</c> — interpolates from the texture centre (colour 1) outward to the edges (colour 2).</item>
        /// </list>
        /// </summary>
        /// <param name="path">Project-relative path to the target PNG (e.g. 'Assets/Textures/Sky.png'). Created if absent.</param>
        /// <param name="gradientType">Gradient mode: "linear" or "radial". Default "linear".</param>
        /// <param name="angle">Gradient angle in degrees for linear mode. 0=left→right, 90=bottom→top. Default 0.</param>
        /// <param name="color1Hex">Hex colour for the start/centre of the gradient (e.g. '#000000'). Default black.</param>
        /// <param name="color2Hex">Hex colour for the end/edge of the gradient (e.g. '#FFFFFF'). Default white.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the asset path and dimensions, or an error on failure.</returns>
        [McpTool("texture-apply-gradient", Title = "Texture / Apply Gradient")]
        [Description("Applies a two-colour gradient to a Texture2D PNG. gradientType: 'linear' (angle in degrees) " + "or 'radial' (centre to edge). Colors are hex strings (e.g. '#FF0000'). " + "Creates the texture if it does not exist.")]
        public ToolResponse ApplyGradient(
            [Description("Project-relative path to the PNG (e.g. 'Assets/Textures/Sky.png'). Created if absent.")] string path,
            [Description("Gradient type: 'linear' or 'radial'. Default 'linear'.")] string gradientType = "linear",
            [Description("Gradient angle in degrees for linear mode (0=left→right, 90=bottom→top). Default 0.")] float angle = 0f,
            [Description("Hex colour for gradient start/centre (e.g. '#000000'). Default black.")] string color1Hex = "#000000",
            [Description("Hex colour for gradient end/edge (e.g. '#FFFFFF'). Default white.")] string color2Hex = "#FFFFFF"
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

                if (!ColorUtility.TryParseHtmlString(color1Hex, out Color c1))
                {
                    return ToolResponse.Error($"Cannot parse color1Hex '{color1Hex}'. Use format '#RRGGBB' or '#RRGGBBAA'.");
                }

                if (!ColorUtility.TryParseHtmlString(color2Hex, out Color c2))
                {
                    return ToolResponse.Error($"Cannot parse color2Hex '{color2Hex}'. Use format '#RRGGBB' or '#RRGGBBAA'.");
                }

                int w = 256;
                int h = 256;
                var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                if (existing != null)
                {
                    w = existing.width;
                    h = existing.height;
                }

                string folder = Path.GetDirectoryName(path) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                string typeNorm = gradientType.Trim().ToLowerInvariant();
                float rad = angle * Mathf.Deg2Rad;
                float dirX =  Mathf.Cos(rad);
                float dirY =  Mathf.Sin(rad);
                float halfW = 0.5f;
                float halfH = 0.5f;
                float maxProj = Mathf.Abs(dirX) * halfW + Mathf.Abs(dirY) * halfH;

                if (maxProj < 0.0001f)
                {
                    maxProj = 0.0001f;
                }

                var pixels = new Color32[w * h];

                float invW = w > 1 ? 1f / (w - 1) : 0f;
                float invH = h > 1 ? 1f / (h - 1) : 0f;

                for (int y = 0; y < h; y++)
                {
                    float vy = y * invH;

                    for (int x = 0; x < w; x++)
                    {
                        float vx = x * invW;

                        float t;

                        if (typeNorm == "radial")
                        {
                            float dx = vx - 0.5f;
                            float dy = vy - 0.5f;
                            t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / (0.5f * Mathf.Sqrt(2f)));
                        }
                        else
                        {
                            float cx = vx - 0.5f;
                            float cy = vy - 0.5f;
                            float proj = cx * dirX + cy * dirY;
                            t = Mathf.Clamp01((proj + maxProj) / (2f * maxProj));
                        }

                        pixels[y * w + x] = (Color32)Color.Lerp(c1, c2, t);
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(pixels);
                tex.Apply();

                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                if (png == null || png.Length == 0)
                {
                    return ToolResponse.Error("Failed to encode gradient texture to PNG.");
                }

                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                return ToolResponse.Text($"Gradient '{gradientType}' applied to '{path}' ({w}x{h}). " + $"angle={angle}, c1={color1Hex}, c2={color2Hex}.");
            });
        }

        #endregion
    }
}