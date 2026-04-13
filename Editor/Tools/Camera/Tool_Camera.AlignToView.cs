#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.Editor.Tools.Helpers;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, configuring, listing, and controlling cameras in the scene,
    /// including Cinemachine virtual camera support (priority, lens, body, aim, noise,
    /// blending, extensions, brain status, and multi-view screenshots).
    /// </summary>
    [McpToolType]
    public partial class Tool_Camera
    {
        #region TOOL METHODS

        /// <summary>
        /// Copies the position and rotation of the active Scene View camera onto a scene camera.
        /// If no camera name is supplied the Main Camera is used.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject to align. Empty to use Main Camera.</param>
        /// <returns>Confirmation text with the resulting position and rotation of the aligned camera.</returns>
        [McpTool("camera-align-to-view", Title = "Camera / Align to Scene View")]
        [Description("Aligns a camera to the current Scene View camera position and rotation. " + "Useful for setting up a camera to match what you see in the Scene window.")]
        public ToolResponse AlignToView(
            [Description("Name of the camera GameObject to align. If empty, uses Main Camera.")] string cameraName = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sceneView = SceneView.lastActiveSceneView;

                if (sceneView == null)
                {
                    return ToolResponse.Error("No active Scene View found.");
                }

                if (!CameraHelper.TryGet(cameraName, out var cam, out var lookupError))
                {
                    return lookupError!;
                }

                Undo.RecordObject(cam!.transform, "Align Camera to View");

                var sceneCamera = sceneView.camera;
                cam.transform.SetPositionAndRotation(sceneCamera.transform.position, sceneCamera.transform.rotation);
                EditorUtility.SetDirty(cam);

                var sb = new StringBuilder();
                sb.AppendLine($"Aligned '{cam.gameObject.name}' to Scene View:");
                sb.AppendLine($"  Position: {cam.transform.position}");
                sb.AppendLine($"  Rotation: {cam.transform.rotation.eulerAngles}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}