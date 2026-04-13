#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Screenshot
    {
        #region TOOL METHODS

        /// <summary>
        /// Captures a screenshot from the Game View camera.
        /// </summary>
        /// <param name="savePath">Project-relative path to save the PNG (e.g. "Assets/Screenshots/GameView.png"). Default "Assets/Screenshots/GameView.png".</param>
        /// <param name="width">Render width in pixels. Default 1920.</param>
        /// <param name="height">Render height in pixels. Default 1080.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the save path and dimensions,
        /// or an error if the path is invalid, dimensions are non-positive, or no Main Camera is found.
        /// </returns>
        [McpTool("screenshot-game-view", Title = "Screenshot / Game View")]
        [Description("Captures a screenshot from the main camera and saves it as PNG.")]
        public ToolResponse GameView(
            [Description("Save path for the PNG file. Default 'Assets/Screenshots/GameView.png'.")] string savePath = "Assets/Screenshots/GameView.png",
            [Description("Image width in pixels. Default 1920.")] int width = 1920,
            [Description("Image height in pixels. Default 1080.")] int height = 1080
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                if (width <= 0 || height <= 0)
                {
                    return ToolResponse.Error("width and height must be greater than 0.");
                }

                var cam = Camera.main;

                if (cam == null)
                {
                    return ToolResponse.Error("No Main Camera found in scene.");
                }

                string folder = Path.GetDirectoryName(savePath) ?? "Assets/Screenshots";

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var rt = new RenderTexture(width, height, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);

                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                File.WriteAllBytes(savePath, png);
                UnityEditor.AssetDatabase.ImportAsset(savePath);

                return ToolResponse.Text($"Screenshot saved to '{savePath}' ({width}x{height}).");
            });
        }

        #endregion
    }
}