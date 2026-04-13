#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Camera
    {
        #region TOOL METHODS

        /// <summary>
        /// Lists all cameras in the current scene with their full property set.
        /// </summary>
        /// <returns>Formatted text with each camera's position, rotation, FOV, clear flags, culling mask, and depth.</returns>
        [McpTool("camera-list", Title = "Camera / List")]
        [Description("Lists all cameras in the current scene with their properties including " + "position, rotation, field of view, clear flags, culling mask, and depth.")]
        public ToolResponse ListCameras()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var cameras = Camera.allCameras;

                if (cameras.Length == 0)
                {
                    return ToolResponse.Text("No cameras found in scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Cameras ({cameras.Length}):");

                foreach (var cam in cameras)
                {
                    sb.AppendLine($"  {cam.gameObject.name}:");
                    sb.AppendLine($"    Position: {cam.transform.position}");
                    sb.AppendLine($"    Rotation: {cam.transform.rotation.eulerAngles}");
                    sb.AppendLine($"    FOV: {cam.fieldOfView}");
                    sb.AppendLine($"    Near/Far: {cam.nearClipPlane}/{cam.farClipPlane}");
                    sb.AppendLine($"    Orthographic: {cam.orthographic}");

                    if (cam.orthographic)
                    {
                        sb.AppendLine($"    Ortho Size: {cam.orthographicSize}");
                    }

                    sb.AppendLine($"    Depth: {cam.depth}");
                    sb.AppendLine($"    Clear Flags: {cam.clearFlags}");
                    sb.AppendLine($"    Culling Mask: {cam.cullingMask}");
                    sb.AppendLine($"    Target Display: {cam.targetDisplay}");
                    sb.AppendLine($"    Is Main: {cam == Camera.main}");
                    sb.AppendLine($"    Enabled: {cam.enabled}");
                    sb.AppendLine();
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}