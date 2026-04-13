#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.Editor.Tools.Helpers;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Camera
    {
        #region CONSTANTS

        private const int FORCED_PRIORITY = 9999;

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Forces a Cinemachine virtual camera to become the active live camera by setting its
        /// priority to 9999, overriding all other virtual cameras.
        /// Use camera-release-override to restore normal priority behaviour.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject to force live.</param>
        /// <returns>Confirmation text with the camera name and forced priority value.</returns>
        [McpTool("camera-force-camera", Title = "Camera / Force Camera")]
        [Description("Forces a specific Cinemachine virtual camera to become the live camera by setting " + "its priority to 9999. Use camera-release-override to restore normal priorities. " + "Requires Cinemachine to be installed.")]
        public ToolResponse ForceCamera(
            [Description("Name of the Cinemachine virtual camera GameObject to force active.")] string cameraName)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;
                Undo.RecordObject(cam, $"Force Cinemachine Camera {cameraName}");
                bool set = SetProperty(cam, "Priority", FORCED_PRIORITY);

                if (!set)
                {
                    set = SetField(cam, "Priority", FORCED_PRIORITY);
                }

                if (!set)
                {
                    set = SetField(cam, "m_Priority", FORCED_PRIORITY);
                }

                if (!set)
                {
                    return ToolResponse.Error("Could not set Priority — property not found.");
                }

                EditorUtility.SetDirty(cam);

                var sb = new StringBuilder();
                sb.AppendLine($"Forced camera '{cameraName}' to live:");
                sb.AppendLine($"  Priority set to: {FORCED_PRIORITY}");
                sb.AppendLine("  Use camera-release-override to restore normal priorities.");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}