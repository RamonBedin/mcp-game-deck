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
        /// Retrieves or configures the Body component on a Cinemachine virtual camera.
        /// When propertiesJson is empty the tool reports the current body type.
        /// When propertiesJson is provided, individual named properties are applied to the body
        /// component via reflection using a simple "key=value" format.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject.</param>
        /// <param name="bodyType">
        /// Short name of the desired body component type (e.g. "Transposer", "FramingTransposer",
        /// "HardLockToTarget", "OrbitalTransposer", "TrackedDolly"). Empty to skip type change.
        /// </param>
        /// <param name="propertiesJson">
        /// Semicolon-separated key=value pairs to set on the body component
        /// (e.g. "FollowOffset.y=2;XDamping=0.5"). Empty to skip property changes.
        /// </param>
        /// <returns>Confirmation text with the body type and any properties changed.</returns>
        [McpTool("camera-set-body", Title = "Camera / Set Body")]
        [Description("Configures the Body (position algorithm) component of a Cinemachine virtual camera. " + "Pass bodyType to switch algorithm, and propertiesJson as 'key=value;key=value' pairs " + "to set individual properties. Requires Cinemachine to be installed.")]
        public ToolResponse SetBody(
            [Description("Name of the Cinemachine camera GameObject.")] string cameraName,
            [Description("Body component type short name: Transposer, FramingTransposer, HardLockToTarget, " + "OrbitalTransposer, TrackedDolly. Empty to leave unchanged.")] string bodyType = "",
            [Description("Semicolon-separated key=value property overrides for the body component. " + "Example: 'FollowOffset.y=2;XDamping=0.5'. Empty to skip.")] string propertiesJson = "")
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;
                var sb = new StringBuilder();
                sb.AppendLine($"Body configuration for '{cameraName}':");

                UnityEngine.Component? bodyComponent = GetCinemachineSubComponent(cam, "Body");

                if (bodyComponent == null)
                {
                    sb.AppendLine("  Body component: none");
                }
                else
                {
                    sb.AppendLine($"  Body component: {bodyComponent.GetType().Name}");
                }

                if (!string.IsNullOrWhiteSpace(propertiesJson) && bodyComponent != null)
                {
                    Undo.RecordObject(bodyComponent, $"Set CM Body Props {cameraName}");
                    ApplyKeyValueProperties(bodyComponent, propertiesJson, sb);
                    EditorUtility.SetDirty(bodyComponent);
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE CINEMACHINE SUB-COMPONENT HELPERS

        /// <summary>
        /// Retrieves a Cinemachine sub-component (Body or Aim) from a virtual camera using
        /// the GetCinemachineComponent generic method via reflection.
        /// </summary>
        /// <param name="cmCam">The Cinemachine virtual camera component.</param>
        /// <param name="stage">The pipeline stage name: "Body" or "Aim".</param>
        /// <returns>The sub-component, or null when not present.</returns>
        private static UnityEngine.Component? GetCinemachineSubComponent(UnityEngine.Component cmCam, string stage)
        {
            string interfaceName = stage == "Body" ? "Unity.Cinemachine.ICinemachinePositionComposer" : "Unity.Cinemachine.ICinemachineRotationComposer";

            Type? interfaceType = Type.GetType($"{interfaceName}, Unity.Cinemachine");

            if (interfaceType != null)
            {
                UnityEngine.Component[] comps = cmCam.GetComponentsInChildren<UnityEngine.Component>(true);

                for (int i = 0; i < comps.Length; i++)
                {
                    if (interfaceType.IsAssignableFrom(comps[i].GetType()))
                    {
                        return comps[i];
                    }
                }
            }

            Type? stageEnumType = Type.GetType("Cinemachine.CinemachineCore+Stage, Cinemachine");

            if (stageEnumType != null)
            {
                try
                {
                    object stageValue = Enum.Parse(stageEnumType, stage);
                    var method = cmCam.GetType().GetMethod("GetCinemachineComponent", new Type[] { stageEnumType });

                    if (method != null)
                    {
                        object? result = method.Invoke(cmCam, new object[] { stageValue });
                        return result as UnityEngine.Component;
                    }
                }
                catch(Exception ex)
                {
                    Debug.LogWarning($"[Camera] GetCinemachineSubComponent reflection failed for stage '{stage}': {ex.Message}");
                }
            }

            return null;
        }

        /// <summary>
        /// Applies semicolon-separated key=value property pairs to a target object via reflection.
        /// Supports simple numeric float and int properties. Reports each applied change.
        /// </summary>
        /// <param name="target">The object whose properties are set.</param>
        /// <param name="kvPairs">Semicolon-separated "key=value" string.</param>
        /// <param name="sb">StringBuilder to append change log lines to.</param>
        private static void ApplyKeyValueProperties(object target, string kvPairs, StringBuilder sb)
        {
            string[] pairs = kvPairs.Split(';');

            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i].Trim();

                if (string.IsNullOrEmpty(pair))
                {
                    continue;
                }

                int eqIdx = pair.IndexOf('=');

                if (eqIdx < 0)
                {
                    continue;
                }

                string key = pair[..eqIdx].Trim();
                string value = pair[(eqIdx + 1)..].Trim();

                var prop = target.GetType().GetProperty(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        object? converted = Convert.ChangeType(value, prop.PropertyType);
                        prop.SetValue(target, converted);
                        sb.AppendLine($"  {key} = {value}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {key}: failed ({ex.Message})");
                    }

                    continue;
                }

                var field = target.GetType().GetField(key, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    try
                    {
                        object? converted = Convert.ChangeType(value, field.FieldType);
                        field.SetValue(target, converted);
                        sb.AppendLine($"  {key} = {value}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  {key}: failed ({ex.Message})");
                    }
                }
                else
                {
                    sb.AppendLine($"  {key}: property/field not found");
                }
            }
        }

        #endregion
    }
}