#nullable enable
using System;
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
        #region CONSTANTS

        private const int DEFAULT_CINEMACHINE_PRIORITY = 10;

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Resets the priority on every Cinemachine virtual camera in the scene to the default
        /// value (10), releasing any forced overrides applied by camera-force-camera.
        /// </summary>
        /// <returns>Confirmation text listing all cameras whose priority was reset.</returns>
        [McpTool("camera-release-override", Title = "Camera / Release Override")]
        [Description("Resets all Cinemachine virtual camera priorities to the default value (10), " + "releasing any priority override applied by camera-force-camera. " + "Requires Cinemachine to be installed.")]
        public ToolResponse ReleaseOverride()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                Type? cmType = GetCinemachineCameraType();

                if (cmType == null)
                {
                    return ToolResponse.Error("Could not resolve Cinemachine camera type.");
                }

                UnityEngine.Object[] allCmCams = UnityEngine.Object.FindObjectsByType(cmType, FindObjectsSortMode.None);

                if (allCmCams.Length == 0)
                {
                    return ToolResponse.Text("No Cinemachine virtual cameras found in scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Released priority override on {allCmCams.Length} Cinemachine camera(s):");

                for (int i = 0; i < allCmCams.Length; i++)
                {
                    UnityEngine.Component? cmCam = allCmCams[i] as UnityEngine.Component;

                    if (cmCam == null)
                    {
                        continue;
                    }

                    Undo.RecordObject(cmCam, "Release CM Priority Override");
                    bool set = SetProperty(cmCam, "Priority", DEFAULT_CINEMACHINE_PRIORITY);

                    if (!set)
                    {
                        set = SetField(cmCam, "Priority", DEFAULT_CINEMACHINE_PRIORITY);
                    }

                    if (!set)
                    {
                        set = SetField(cmCam, "m_Priority", DEFAULT_CINEMACHINE_PRIORITY);
                    }

                    EditorUtility.SetDirty(cmCam);

                    sb.AppendLine($"  {cmCam.gameObject.name}: priority -> {DEFAULT_CINEMACHINE_PRIORITY}" + (set ? "" : " (property not found, skipped)"));
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}