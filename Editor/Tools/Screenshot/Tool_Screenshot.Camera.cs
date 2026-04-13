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
    /// MCP tools for capturing screenshots in the Unity Editor.
    /// Covers camera rendering to PNG, Game View capture, and Scene View capture,
    /// with options to save to disk or return as base64.
    /// </summary>
    [McpToolType]
    public partial class Tool_Screenshot
    {
        #region TOOL METHODS

        /// <summary>
        /// Renders a specific camera to a PNG image, either saved to disk or returned as base64.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject to render from. Takes priority over instanceId.</param>
        /// <param name="instanceId">Instance ID of the camera object. Used when cameraName is empty.</param>
        /// <param name="width">Render texture width in pixels.</param>
        /// <param name="height">Render texture height in pixels.</param>
        /// <param name="savePath">Project-relative path where the PNG will be saved (when returnBase64 is false).</param>
        /// <param name="returnBase64">When true, returns the PNG bytes as a base64 string instead of saving to disk.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the save path confirmation,
        /// or an image response with the base64-encoded PNG bytes when <paramref name="returnBase64"/> is true.
        /// </returns>
        [McpTool("screenshot-camera", Title = "Screenshot / Camera")]
        [Description("Renders a Unity camera to a PNG. Find by cameraName or instanceId. " + "Returns the image as base64 (returnBase64=true) or saves it to savePath.")]
        public ToolResponse CameraScreenshot(
            [Description("Name of the camera GameObject to render. Leave empty to use instanceId.")] string cameraName = "",
            [Description("Instance ID of the Camera object. Used only when cameraName is empty.")] int instanceId = 0,
            [Description("Render width in pixels. Default 1920.")] int width = 1920,
            [Description("Render height in pixels. Default 1080.")] int height = 1080,
            [Description("Save path for the PNG file (e.g. 'Assets/Screenshots/Camera.png'). Ignored when returnBase64 is true.")] string savePath = "Assets/Screenshots/Camera.png",
            [Description("When true, returns the PNG as a base64 string instead of writing to disk.")] bool returnBase64 = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (width <= 0 || height <= 0)
                {
                    return ToolResponse.Error($"width and height must be positive. Got {width}x{height}.");
                }

                Camera? target = null;

                if (!string.IsNullOrWhiteSpace(cameraName))
                {
                    Camera[] all = Camera.allCameras;

                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].gameObject.name == cameraName)
                        {
                            target = all[i];
                            break;
                        }
                    }

                    if (target == null)
                    {
                        return ToolResponse.Error($"No camera found with name '{cameraName}'.");
                    }
                }
                else if (instanceId != 0)
                {
                    var obj = EditorUtility.EntityIdToObject(instanceId);

                    if (obj == null)
                    {
                        return ToolResponse.Error($"No object found with instanceId {instanceId}.");
                    }

                    if (obj is Camera cam)
                    {
                        target = cam;
                    }
                    else if (obj is GameObject go)
                    {
                        target = go.GetComponent<Camera>();

                        if (target == null)
                        {
                            return ToolResponse.Error($"GameObject with instanceId {instanceId} has no Camera component.");
                        }
                    }
                    else
                    {
                        return ToolResponse.Error($"Object with instanceId {instanceId} is not a Camera or GameObject.");
                    }
                }
                else
                {
                    target = Camera.main;

                    if (target == null)
                    {
                        return ToolResponse.Error("No cameraName or instanceId provided, and Camera.main is null.");
                    }
                }

                var renderTex = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1
                };

                RenderTexture? previousTarget = target.targetTexture;
                RenderTexture? previousActive = RenderTexture.active;

                target.targetTexture = renderTex;
                target.Render();

                RenderTexture.active = renderTex;
                var capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply();

                RenderTexture.active = previousActive;
                target.targetTexture = previousTarget;
                UnityEngine.Object.DestroyImmediate(renderTex);

                byte[] pngBytes = capture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(capture);

                if (pngBytes == null || pngBytes.Length == 0)
                {
                    return ToolResponse.Error("Failed to encode captured frame to PNG.");
                }

                if (returnBase64)
                {
                    int b64Length = ((pngBytes.Length + 2) / 3) * 4;
                    return ToolResponse.Image(pngBytes, "image/png", $"Camera '{target.gameObject.name}' rendered at {width}x{height}. Base64 length: {b64Length}");
                }

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    savePath = "Assets/Screenshots/Camera.png";
                }

                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/'.");
                }

                string? folder = Path.GetDirectoryName(savePath);

                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                File.WriteAllBytes(savePath, pngBytes);
                AssetDatabase.ImportAsset(savePath);

                return ToolResponse.Text($"Screenshot saved to '{savePath}' ({width}x{height}, camera: '{target.gameObject.name}', {pngBytes.Length} bytes).");
            });
        }

        #endregion
    }
}