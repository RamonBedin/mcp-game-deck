#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.Editor.Tools.Helpers;
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
        /// Sets the Follow and LookAt transforms on a Cinemachine virtual camera by name.
        /// Both targets are resolved by searching for a GameObject with the given path/name.
        /// Pass an empty string to clear a target.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject.</param>
        /// <param name="followTarget">Name or path of the GameObject to follow. Empty to clear.</param>
        /// <param name="lookAtTarget">Name or path of the GameObject to look at. Empty to clear.</param>
        /// <returns>Confirmation text listing the assigned targets.</returns>
        [McpTool("camera-set-target", Title = "Camera / Set Target")]
        [Description("Assigns Follow and LookAt targets on a Cinemachine virtual camera. " + "Provide GameObject names to assign targets, or empty strings to clear them. " + "Requires Cinemachine to be installed.")]
        public ToolResponse SetTarget(
            [Description("Name of the Cinemachine camera GameObject.")] string cameraName,
            [Description("Name or path of the Follow target GameObject. Empty string to clear.")] string followTarget = "",
            [Description("Name or path of the LookAt target GameObject. Empty string to clear.")] string lookAtTarget = "")
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;
                Undo.RecordObject(cam, $"Set Cinemachine Target on {cameraName}");

                var sb = new StringBuilder();
                sb.AppendLine($"Set targets on Cinemachine camera '{cameraName}':");

                if (!string.IsNullOrWhiteSpace(followTarget))
                {
                    var followGo = GameObject.Find(followTarget);

                    if (followGo == null)
                    {
                        return ToolResponse.Error($"Follow target GameObject '{followTarget}' not found.");
                    }

                    bool set = SetProperty(cam, "Follow", followGo.transform);

                    if (!set)
                    {
                        SetField(cam, "m_Follow", followGo.transform);
                    }

                    sb.AppendLine($"  Follow: {followGo.name}");
                }
                else if (followTarget == "")
                {
                    SetProperty(cam, "Follow", null!);
                    sb.AppendLine("  Follow: cleared");
                }

                if (!string.IsNullOrWhiteSpace(lookAtTarget))
                {
                    var lookAtGo = GameObject.Find(lookAtTarget);

                    if (lookAtGo == null)
                    {
                        return ToolResponse.Error($"LookAt target GameObject '{lookAtTarget}' not found.");
                    }

                    bool set = SetProperty(cam, "LookAt", lookAtGo.transform);

                    if (!set)
                    {
                        SetField(cam, "m_LookAt", lookAtGo.transform);
                    }

                    sb.AppendLine($"  LookAt: {lookAtGo.name}");
                }
                else if (lookAtTarget == "")
                {
                    SetProperty(cam, "LookAt", null!);
                    sb.AppendLine("  LookAt: cleared");
                }

                EditorUtility.SetDirty(cam);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}