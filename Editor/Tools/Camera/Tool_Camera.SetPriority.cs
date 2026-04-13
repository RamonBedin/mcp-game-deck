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
        #region TOOL METHODS

        /// <summary>
        /// Sets the Priority field on a Cinemachine virtual camera.
        /// Higher priority cameras are preferred by the CinemachineBrain.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject.</param>
        /// <param name="priority">New priority value. Higher values take precedence.</param>
        /// <returns>Confirmation text with the camera name and the applied priority.</returns>
        [McpTool("camera-set-priority", Title = "Camera / Set Priority")]
        [Description("Sets the Priority on a Cinemachine virtual camera. The CinemachineBrain selects " + "the live camera by choosing the enabled virtual camera with the highest priority. " + "Requires Cinemachine to be installed.")]
        public ToolResponse SetPriority(
            [Description("Name of the Cinemachine camera GameObject.")] string cameraName,
            [Description("New priority value. Higher numbers take precedence. Common range: 0–100.")] int priority = 10)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;

                Undo.RecordObject(cam, $"Set Cinemachine Priority on {cameraName}");
                bool set = SetProperty(cam, "Priority", priority);

                if (!set)
                {
                    set = SetField(cam, "Priority", priority);
                }

                if (!set)
                {
                    set = SetField(cam, "m_Priority", priority);
                }

                if (!set)
                {
                    return ToolResponse.Error("Could not set Priority — property not found on this Cinemachine version.");
                }

                EditorUtility.SetDirty(cam);

                var sb = new StringBuilder();
                sb.AppendLine($"Set priority on '{cameraName}':");
                sb.AppendLine($"  Priority: {priority}");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}