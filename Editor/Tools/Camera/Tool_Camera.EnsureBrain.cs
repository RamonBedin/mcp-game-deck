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
        #region TOOL METHODS

        /// <summary>
        /// Finds the specified camera and ensures it has a CinemachineBrain component.
        /// When the brain is missing it is added via reflection and configured with the
        /// supplied blend style and duration. When already present, blend settings are updated.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject to add the brain to.</param>
        /// <param name="blendStyle">
        /// Default blend style keyword: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut.
        /// </param>
        /// <param name="blendDuration">Default blend duration in seconds.</param>
        /// <returns>Confirmation text indicating whether the brain was added or already existed.</returns>
        [McpTool("camera-ensure-brain", Title = "Camera / Ensure Brain")]
        [Description("Ensures a CinemachineBrain component exists on the specified camera. " + "Adds it when missing and configures the default blend. " + "Blend styles: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut. " + "Requires Cinemachine to be installed.")]
        public ToolResponse EnsureBrain(
            [Description("Name of the camera GameObject. Default 'Main Camera'.")] string cameraName = "Main Camera",
            [Description("Default blend style: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut. " + "Default EaseInOut.")] string blendStyle = "EaseInOut",
            [Description("Default blend duration in seconds. Default 2.")] float blendDuration = 2f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                Type? brainType = GetCinemachineBrainType();

                if (brainType == null)
                {
                    return ToolResponse.Error("Could not resolve CinemachineBrain type.");
                }

                var go = GameObject.Find(cameraName);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{cameraName}' not found.");
                }

                if (!go.TryGetComponent<Camera>(out var cam))
                {
                    return ToolResponse.Error($"'{cameraName}' has no Camera component. " + "CinemachineBrain requires a Camera on the same GameObject.");
                }

                var sb = new StringBuilder();
                UnityEngine.Component? brain = go.GetComponent(brainType);

                if (brain == null)
                {
                    brain = go.AddComponent(brainType);
                    Undo.RegisterCreatedObjectUndo(brain, $"Add CinemachineBrain to {cameraName}");
                    sb.AppendLine($"Added CinemachineBrain to '{cameraName}'.");
                }
                else
                {
                    Undo.RecordObject(brain, $"Configure CinemachineBrain on {cameraName}");
                    sb.AppendLine($"CinemachineBrain already present on '{cameraName}'.");
                }

                var blendSb = new StringBuilder();
                bool applied = ApplyBlendDefinition(brain, "DefaultBlend", blendStyle, blendDuration, blendSb);

                if (!applied)
                {
                    applied = ApplyBlendDefinition(brain, "m_DefaultBlend", blendStyle, blendDuration, blendSb);
                }

                if (applied)
                {
                    sb.Append(blendSb);
                }
                else
                {
                    sb.AppendLine("  Warning: could not apply blend settings (property not found).");
                }

                EditorUtility.SetDirty(brain);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}