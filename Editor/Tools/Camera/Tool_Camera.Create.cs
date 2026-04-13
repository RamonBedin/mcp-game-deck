#nullable enable
using System.ComponentModel;
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
        /// Creates a new Camera GameObject with the specified transform and projection settings.
        /// </summary>
        /// <param name="name">Name for the new camera GameObject.</param>
        /// <param name="posX">World-space X position.</param>
        /// <param name="posY">World-space Y position.</param>
        /// <param name="posZ">World-space Z position.</param>
        /// <param name="rotX">X rotation in degrees.</param>
        /// <param name="rotY">Y rotation in degrees.</param>
        /// <param name="fieldOfView">Vertical field of view in degrees (perspective mode).</param>
        /// <param name="orthographic">When true, creates an orthographic camera.</param>
        /// <param name="orthoSize">Half-height of the orthographic viewport.</param>
        /// <param name="nearClip">Near clipping plane distance.</param>
        /// <param name="farClip">Far clipping plane distance.</param>
        /// <param name="depth">Camera depth controlling render order.</param>
        /// <returns>Confirmation text with the created camera's name, position, projection mode, FOV, and depth.</returns>
        [McpTool("camera-create", Title = "Camera / Create")]
        [Description("Creates a new Camera GameObject in the scene with the specified settings. " + "Supports perspective and orthographic cameras with configurable FOV, clipping planes, and depth.")]
        public ToolResponse CreateCamera(
            [Description("Name for the camera GameObject. Default 'New Camera'.")] string name = "New Camera",
            [Description("X position. Default 0.")] float posX = 0f,
            [Description("Y position. Default 1.")] float posY = 1f,
            [Description("Z position. Default -10.")] float posZ = -10f,
            [Description("X rotation in degrees. Default 0.")] float rotX = 0f,
            [Description("Y rotation in degrees. Default 0.")] float rotY = 0f,
            [Description("Field of view in degrees (perspective mode). Default 60.")] float fieldOfView = 60f,
            [Description("If true, creates an orthographic camera. Default false.")] bool orthographic = false,
            [Description("Orthographic size (only used if orthographic is true). Default 5.")] float orthoSize = 5f,
            [Description("Near clipping plane distance. Default 0.3.")] float nearClip = 0.3f,
            [Description("Far clipping plane distance. Default 1000.")] float farClip = 1000f,
            [Description("Camera depth (render order). Higher values render on top. Default 0.")] float depth = 0f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = new GameObject(name);
                var cam = go.AddComponent<Camera>();

                go.transform.SetPositionAndRotation(new Vector3(posX, posY, posZ), Quaternion.Euler(rotX, rotY, 0));
                cam.fieldOfView = fieldOfView;
                cam.orthographic = orthographic;
                cam.orthographicSize = orthoSize;
                cam.nearClipPlane = nearClip;
                cam.farClipPlane = farClip;
                cam.depth = depth;

                if (Camera.allCameras.Length <= 1)
                {
                    go.AddComponent<AudioListener>();
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create Camera {name}");
                Selection.activeGameObject = go;

                var sb = new StringBuilder();
                sb.AppendLine($"Created camera '{name}':");
                sb.AppendLine($"  Position: {go.transform.position}");
                sb.AppendLine($"  Mode: {(orthographic ? "Orthographic" : "Perspective")}");
                sb.AppendLine($"  FOV: {fieldOfView}");
                sb.AppendLine($"  Depth: {depth}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}