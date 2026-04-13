#nullable enable
using System;
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
        #region Tool Methods

        /// <summary>
        /// Queries the CinemachineBrain and returns a status report including the currently live
        /// virtual camera, whether a blend is in progress, the outgoing camera (if blending),
        /// and a list of all virtual cameras found in the scene.
        /// </summary>
        /// <returns>Formatted text with the brain's current state.</returns>
        [McpTool("camera-get-brain-status", Title = "Camera / Get Brain Status", ReadOnlyHint = true)]
        [Description("Returns a status report from the CinemachineBrain: active live camera, " + "current blend state, and all virtual cameras in the scene. " + "Read-only — no state is modified. Requires Cinemachine to be installed.")]
        public ToolResponse GetBrainStatus()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                UnityEngine.Component? brain = FindCinemachineBrain();

                if (brain == null)
                {
                    return ToolResponse.Text("No CinemachineBrain found in the scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("CinemachineBrain Status:");
                sb.AppendLine($"  Brain on: {brain.gameObject.name}");
                var brainBehaviour = brain as Behaviour;
                sb.AppendLine($"  Brain enabled: {(brainBehaviour != null ? brainBehaviour.enabled.ToString() : "unknown")}");

                object? activeCam = GetProperty(brain, "ActiveVirtualCamera");
                activeCam ??= GetProperty(brain, "ActiveCamera");

                if (activeCam != null)
                {
                    var activeCamComp = activeCam as UnityEngine.Component;
                    string activeName = activeCamComp != null ? activeCamComp.gameObject.name : activeCam.ToString();
                    sb.AppendLine($"  Active virtual camera: {activeName}");
                }
                else
                {
                    sb.AppendLine("  Active virtual camera: none");
                }

                object? isBlending = GetProperty(brain, "IsBlending");
                sb.AppendLine($"  Is blending: {isBlending ?? "unknown"}");

                if (isBlending is true)
                {
                    object? blend = GetProperty(brain, "ActiveBlend");

                    if (blend != null)
                    {
                        object? camA     = GetProperty(blend, "CamA");
                        object? camB     = GetProperty(blend, "CamB");
                        object? blendPct = GetProperty(blend, "BlendWeight");
                        var camAComp = camA as UnityEngine.Component;
                        var camBComp = camB as UnityEngine.Component;
                        string camAName = camAComp != null ? camAComp.gameObject.name : (camA != null ? camA.ToString() : "unknown");
                        string camBName = camBComp != null ? camBComp.gameObject.name : (camB != null ? camB.ToString() : "unknown");

                        sb.AppendLine($"  Blending from: {camAName}");
                        sb.AppendLine($"  Blending to:   {camBName}");
                        sb.AppendLine($"  Blend weight:  {(blendPct != null ? blendPct.ToString() : "unknown")}");
                    }
                }

                Type? cmType = GetCinemachineCameraType();

                if (cmType != null)
                {
                    UnityEngine.Object[] allCams = UnityEngine.Object.FindObjectsByType(cmType, FindObjectsSortMode.None);

                    sb.AppendLine($"\nVirtual Cameras in scene ({allCams.Length}):");

                    for (int i = 0; i < allCams.Length; i++)
                    {
                        UnityEngine.Component? vc = allCams[i] as UnityEngine.Component;

                        if (vc == null)
                        {
                            continue;
                        }

                        object? pri = GetProperty(vc, "Priority");
                        pri ??= GetField(vc, "Priority");
                        pri ??= GetField(vc, "m_Priority");

                        var vcBehaviour = vc as Behaviour;
                        bool isEnabled = vcBehaviour != null && vcBehaviour.enabled;
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}