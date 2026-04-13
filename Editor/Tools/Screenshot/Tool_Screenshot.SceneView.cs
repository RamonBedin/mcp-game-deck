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
    public partial class Tool_Screenshot
    {
        #region TOOL METHODS

        /// <summary>
        /// Captures a screenshot from the active Scene View.
        /// </summary>
        /// <param name="savePath">Project-relative path to save the PNG (e.g. "Assets/Screenshots/SceneView.png"). Default "Assets/Screenshots/SceneView.png".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the save path and captured dimensions,
        /// or an error if the path is invalid, no Scene View is active, or the camera is unavailable.
        /// </returns>
        [McpTool("screenshot-scene-view", Title = "Screenshot / Scene View")]
        [Description("Captures a screenshot from the active Scene View and saves it as PNG.")]
        public ToolResponse SceneViewCapture(
            [Description("Save path for the PNG file. Default 'Assets/Screenshots/SceneView.png'.")] string savePath = "Assets/Screenshots/SceneView.png"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                var sv = SceneView.lastActiveSceneView;

                if (sv == null)
                {
                    return ToolResponse.Error("No active Scene View found.");
                }

                string folder = Path.GetDirectoryName(savePath) ?? "Assets/Screenshots";

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var cam = sv.camera;

                if (cam == null)
                {
                    return ToolResponse.Error("Scene View camera is not available.");
                }

                int w = (int)sv.position.width;
                int h = (int)sv.position.height;

                if (w <= 0 || h <= 0)
                {
                    return ToolResponse.Error("Scene View has invalid dimensions.");
                }

                var rt = new RenderTexture(w, h, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(rt);

                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);

                File.WriteAllBytes(savePath, png);
                AssetDatabase.ImportAsset(savePath);

                return ToolResponse.Text($"Scene View screenshot saved to '{savePath}' ({w}x{h}).");
            });
        }

        #endregion
    }
}