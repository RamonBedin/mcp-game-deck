#nullable enable
using System.ComponentModel;
using System.IO;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Camera
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a temporary camera, renders six views around the target GameObject
        /// (front, back, left, right, top, bird_eye), stitches them into a 3×2 contact
        /// sheet, saves the result as a PNG, and destroys all temporary objects.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the focus object. 0 to use <paramref name="objectPath"/> or world origin.</param>
        /// <param name="objectPath">Hierarchy path of the focus object. Used when <paramref name="instanceId"/> is 0.</param>
        /// <param name="savePath">Project-relative path for the output PNG. Default 'Assets/Screenshots/Multiview.png'.</param>
        /// <param name="resolution">Pixel resolution of each individual view tile. Default 256. Contact sheet will be 3×resolution wide and 2×resolution tall.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the saved PNG path and contact-sheet dimensions,
        /// or an error on failure.
        /// </returns>
        [McpTool("camera-screenshot-multiview", Title = "Camera / Screenshot Multiview")]
        [Description("Renders 6 orthographic views of a target object (front/back/left/right/top/bird_eye) " + "and saves them as a 3x2 PNG contact sheet. resolution controls individual tile size.")]
        public ToolResponse ScreenshotMultiview(
            [Description("Unity instance ID of the focus object. 0 to use objectPath or world origin.")] int instanceId = 0,
            [Description("Hierarchy path of the focus object. Used when instanceId is 0.")] string objectPath = "",
            [Description("Project-relative save path for the PNG. Default 'Assets/Screenshots/Multiview.png'.")] string savePath = "Assets/Screenshots/Multiview.png",
            [Description("Per-tile resolution in pixels. Contact sheet is 3 tiles wide, 2 tiles tall. Default 256.")] int resolution = 256
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (resolution < 16)
                {
                    resolution = 16;
                }

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    savePath = "Assets/Screenshots/Multiview.png";
                }

                Vector3 focus = Vector3.zero;
                float radius  = 5f;

                var focusGo = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (focusGo != null)
                {
                    focus = focusGo.transform.position;
                    Renderer[] renderers = focusGo.GetComponentsInChildren<Renderer>();

                    if (renderers.Length > 0)
                    {
                        Bounds b = renderers[0].bounds;

                        for (int i = 1; i < renderers.Length; i++)
                        {
                            b.Encapsulate(renderers[i].bounds);
                        }

                        radius = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 2.5f;

                        if (radius < 0.1f)
                        {
                            radius = 5f;
                        }
                    }
                }

                string? folder = Path.GetDirectoryName(savePath);

                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var camGo = new GameObject("__MultiviewTempCam__");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = radius;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 10f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                cam.enabled = false;

                int viewCount = 6;
                string[] viewNames  = { "Front",  "Back",     "Left",      "Right",     "Top",        "Bird Eye"   };
                Vector3[] positions = {
                    focus + new Vector3(0,        0,        -radius),
                    focus + new Vector3(0,        0,         radius),
                    focus + new Vector3(-radius,  0,         0),
                    focus + new Vector3( radius,  0,         0),
                    focus + new Vector3(0,        radius,    0),
                    focus + new Vector3(-radius * 0.7f, radius * 0.7f, -radius * 0.7f),
                };
                Vector3[] rotations = {
                    new(0,   0,   0),
                    new(0,   180, 0),
                    new(0,   90,  0),
                    new(0,  -90,  0),
                    new(90,  0,   0),
                    new(35, 45,   0),
                };

                int cols = 3;
                int rows = 2;
                int sheetW = cols * resolution;
                int sheetH = rows * resolution;

                var sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
                var fillColor = new Color32(31, 31, 31, 255);
                Color32[] bg = new Color32[sheetW * sheetH];

                for (int i = 0; i < bg.Length; i++)
                {
                    bg[i] = fillColor;
                }

                sheet.SetPixels32(bg);

                var renderTex = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };

                RenderTexture? prevActive = RenderTexture.active;

                for (int v = 0; v < viewCount; v++)
                {
                    cam.transform.SetPositionAndRotation(positions[v], Quaternion.Euler(rotations[v]));
                    cam.targetTexture = renderTex;
                    cam.Render();

                    RenderTexture.active = renderTex;
                    var tile = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                    tile.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                    tile.Apply();

                    int col = v % cols;
                    int row = rows - 1 - (v / cols);
                    int startX = col * resolution;
                    int startY = row * resolution;

                    Color32[] tilePixels = tile.GetPixels32();
                    sheet.SetPixels32(startX, startY, resolution, resolution, tilePixels);

                    Object.DestroyImmediate(tile);
                }

                RenderTexture.active = prevActive;
                cam.targetTexture    = null;

                Object.DestroyImmediate(renderTex);
                Object.DestroyImmediate(camGo);

                sheet.Apply();
                byte[] png = sheet.EncodeToPNG();
                Object.DestroyImmediate(sheet);

                if (png == null || png.Length == 0)
                {
                    return ToolResponse.Error("Failed to encode multiview contact sheet to PNG.");
                }

                File.WriteAllBytes(savePath, png);
                AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

                var sb = new StringBuilder();
                sb.AppendLine($"Multiview contact sheet saved to '{savePath}':");
                sb.AppendLine($"  Sheet size: {sheetW}x{sheetH} ({cols}x{rows} tiles @ {resolution}px each)");
                sb.AppendLine($"  Focus: {focus}  radius: {radius:F2}");
                sb.AppendLine($"  Views: {string.Join(", ", viewNames)}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}