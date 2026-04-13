#nullable enable
using System;
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
        /// Sets the AmplitudeGain and FrequencyGain on the Cinemachine noise component
        /// attached to a virtual camera. The noise component produces a handheld-camera effect.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject.</param>
        /// <param name="amplitudeGain">Overall shake amplitude multiplier. 0 disables shaking.</param>
        /// <param name="frequencyGain">Overall shake frequency multiplier. Higher values create faster noise.</param>
        /// <returns>Confirmation text with the applied gain values.</returns>
        [McpTool("camera-set-noise", Title = "Camera / Set Noise")]
        [Description("Sets AmplitudeGain and FrequencyGain on the Cinemachine noise (shake) component " + "of a virtual camera. Set amplitudeGain to 0 to disable camera shake. " + "Requires Cinemachine to be installed with a noise profile assigned.")]
        public ToolResponse SetNoise(
            [Description("Name of the Cinemachine camera GameObject.")] string cameraName,
            [Description("Overall amplitude (shake strength) multiplier. 0 = no shake. Default 1.")] float amplitudeGain = 1f,
            [Description("Overall frequency (shake speed) multiplier. Higher = faster. Default 1.")] float frequencyGain = 1f)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;
                UnityEngine.Component? noiseComp = FindNoiseComponent(cam.gameObject);

                if (noiseComp == null)
                {
                    return ToolResponse.Error($"'{cameraName}' has no Cinemachine noise component. " + "Add a CinemachineBasicMultiChannelPerlin component and assign a noise profile first.");
                }

                Undo.RecordObject(noiseComp, $"Set CM Noise {cameraName}");

                bool setAmp = SetProperty(noiseComp, "AmplitudeGain", amplitudeGain);
                bool setFreq = SetProperty(noiseComp, "FrequencyGain", frequencyGain);

                if (!setAmp)
                {
                    setAmp = SetField(noiseComp, "m_AmplitudeGain", amplitudeGain);
                }

                if (!setFreq)
                {
                    setFreq = SetField(noiseComp, "m_FrequencyGain", frequencyGain);
                }

                EditorUtility.SetDirty(noiseComp);

                var sb = new StringBuilder();
                sb.AppendLine($"Set noise on '{cameraName}' ({noiseComp.GetType().Name}):");
                sb.AppendLine($"  AmplitudeGain: {amplitudeGain} ({(setAmp ? "ok" : "property not found")})");
                sb.AppendLine($"  FrequencyGain: {frequencyGain} ({(setFreq ? "ok" : "property not found")})");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE NOISE HELPER

        /// <summary>
        /// Searches the GameObject and its children for a Cinemachine noise component.
        /// Checks known type names for both v2 and v3.
        /// </summary>
        /// <param name="go">The root GameObject to search.</param>
        /// <returns>The noise Component, or null when not found.</returns>
        private static UnityEngine.Component? FindNoiseComponent(GameObject go)
        {
            string[] candidateTypeNames =
            {
                "Unity.Cinemachine.CinemachineBasicMultiChannelPerlin, Unity.Cinemachine",
                "Cinemachine.CinemachineBasicMultiChannelPerlin, Cinemachine",
                "Unity.Cinemachine.CinemachineNoise, Unity.Cinemachine",
                "Cinemachine.CinemachineNoise, Cinemachine"
            };

            for (int i = 0; i < candidateTypeNames.Length; i++)
            {
                Type? t = Type.GetType(candidateTypeNames[i]);

                if (t == null)
                {
                    continue;
                }

                UnityEngine.Component? comp = go.GetComponentInChildren(t, true);

                if (comp != null)
                {
                    return comp;
                }
            }

            return null;
        }

        #endregion
    }
}