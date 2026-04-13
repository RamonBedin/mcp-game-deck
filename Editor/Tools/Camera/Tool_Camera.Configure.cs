#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.Editor.Tools.Helpers;
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
        /// Modifies the properties of an existing camera identified by its GameObject name.
        /// Only parameters with non-sentinel values are applied; all others are left unchanged.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject to configure.</param>
        /// <param name="fieldOfView">New field of view in degrees. Pass -1 to skip.</param>
        /// <param name="orthographic">"true" or "false" to set projection mode. Empty string to skip.</param>
        /// <param name="orthoSize">Orthographic half-height. Pass -1 to skip.</param>
        /// <param name="nearClip">Near clipping plane distance. Pass -1 to skip.</param>
        /// <param name="farClip">Far clipping plane distance. Pass -1 to skip.</param>
        /// <param name="depth">Camera depth. Pass -9999 to skip.</param>
        /// <param name="clearFlags">Clear flags keyword: skybox, solid_color, depth_only, nothing. Empty to skip.</param>
        /// <param name="backgroundColor">Background color as "r,g,b,a" in 0–1 range. Empty to skip.</param>
        /// <returns>Confirmation text listing each property that was changed.</returns>
        [McpTool("camera-configure", Title = "Camera / Configure")]
        [Description("Configures properties of an existing camera by name. Supports field of view, " + "orthographic mode, clipping planes, depth, clear flags, and background color.")]
        public ToolResponse Configure(
            [Description("Name of the camera GameObject to configure.")] string cameraName,
            [Description("Field of view in degrees. -1 to skip.")] float fieldOfView = -1f,
            [Description("Set to true for orthographic, false for perspective. Empty string to skip.")] string orthographic = "",
            [Description("Orthographic size. -1 to skip.")] float orthoSize = -1f,
            [Description("Near clip plane. -1 to skip.")] float nearClip = -1f,
            [Description("Far clip plane. -1 to skip.")] float farClip = -1f,
            [Description("Camera depth. Use -9999 to skip.")] float depth = -9999f,
            [Description("Clear flags: skybox, solid_color, depth_only, nothing. Empty to skip.")] string clearFlags = "",
            [Description("Background color as 'r,g,b,a' (0-1 range). Empty to skip.")] string backgroundColor = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(cameraName))
                {
                    return ToolResponse.Error("cameraName is required.");
                }

                if (!CameraHelper.TryGet(cameraName, out var cam, out var lookupError))
                {
                    return lookupError!;
                }

                Camera resolvedCam = cam!;
                Undo.RecordObject(resolvedCam, $"Configure Camera {cameraName}");

                var sb = new StringBuilder();
                sb.AppendLine($"Configured camera '{cameraName}':");

                if (fieldOfView > 0)
                {
                    resolvedCam.fieldOfView = fieldOfView;
                    sb.AppendLine($"  FOV: {fieldOfView}");
                }

                if (!string.IsNullOrWhiteSpace(orthographic))
                {
                    resolvedCam.orthographic = orthographic.ToLowerInvariant() == "true";
                    sb.AppendLine($"  Orthographic: {resolvedCam.orthographic}");
                }
                if (orthoSize > 0)
                {
                    resolvedCam.orthographicSize = orthoSize;
                    sb.AppendLine($"  Ortho Size: {orthoSize}");
                }

                if (nearClip > 0)
                {
                    resolvedCam.nearClipPlane = nearClip;
                    sb.AppendLine($"  Near Clip: {nearClip}");
                }

                if (farClip > 0)
                {
                    resolvedCam.farClipPlane = farClip;
                    sb.AppendLine($"  Far Clip: {farClip}");
                }

                if (depth > -9998f)
                {
                    resolvedCam.depth = depth;
                    sb.AppendLine($"  Depth: {depth}");
                }

                if (!string.IsNullOrWhiteSpace(clearFlags))
                {
                    resolvedCam.clearFlags = clearFlags.ToLowerInvariant() switch
                    {
                        "skybox" => CameraClearFlags.Skybox,
                        "solid_color" or "solidcolor" => CameraClearFlags.SolidColor,
                        "depth_only" or "depthonly" or "depth" => CameraClearFlags.Depth,
                        "nothing" or "none" => CameraClearFlags.Nothing,
                        _ => resolvedCam.clearFlags
                    };

                    sb.AppendLine($"  Clear Flags: {resolvedCam.clearFlags}");
                }

                if (!string.IsNullOrWhiteSpace(backgroundColor))
                {
                    var parts = backgroundColor.Split(',');

                    if (parts.Length >= 3 && float.TryParse(parts[0].Trim(), out float r) && float.TryParse(parts[1].Trim(), out float g) && float.TryParse(parts[2].Trim(), out float b))
                    {
                        float a = parts.Length >= 4 && float.TryParse(parts[3].Trim(), out float aa) ? aa : 1f;
                        resolvedCam.backgroundColor = new Color(r, g, b, a);
                        sb.AppendLine($"  Background Color: {resolvedCam.backgroundColor}");
                    }
                }

                EditorUtility.SetDirty(resolvedCam);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}