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
        /// Retrieves or configures the Aim component on a Cinemachine virtual camera.
        /// When propertiesJson is empty the tool reports the current aim type.
        /// When propertiesJson is provided, individual named properties are applied to the aim
        /// component via reflection using a simple "key=value" semicolon-separated format.
        /// </summary>
        /// <param name="cameraName">Name of the Cinemachine camera GameObject.</param>
        /// <param name="aimType">
        /// Short name of the desired aim component type (e.g. "Composer", "HardLookAt",
        /// "POV", "SameAsFollowTarget", "GroupComposer"). Empty to skip type change.
        /// </param>
        /// <param name="propertiesJson">
        /// Semicolon-separated key=value pairs to set on the aim component
        /// (e.g. "SoftZoneWidth=0.8;SoftZoneHeight=0.8"). Empty to skip.
        /// </param>
        /// <returns>Confirmation text with the aim type and any properties changed.</returns>
        [McpTool("camera-set-aim", Title = "Camera / Set Aim")]
        [Description("Configures the Aim (rotation algorithm) component of a Cinemachine virtual camera. " + "Pass aimType to identify the algorithm, and propertiesJson as 'key=value;key=value' pairs " + "to set individual properties on it. Requires Cinemachine to be installed.")]
        public ToolResponse SetAim(
            [Description("Name of the Cinemachine camera GameObject.")] string cameraName,
            [Description("Aim component type short name: Composer, HardLookAt, POV, SameAsFollowTarget, " + "GroupComposer. Empty to leave unchanged.")] string aimType = "",
            [Description("Semicolon-separated key=value property overrides for the aim component. " + "Example: 'SoftZoneWidth=0.8;SoftZoneHeight=0.8'. Empty to skip.")] string propertiesJson = "")
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!CinemachineHelper.TryGetCinemachineCamera(cameraName, out var cmCam, out var error))
                {
                    return error!;
                }

                var cam = cmCam!;
                var sb = new StringBuilder();
                sb.AppendLine($"Aim configuration for '{cameraName}':");

                UnityEngine.Component? aimComponent = GetCinemachineSubComponent(cam, "Aim");

                if (aimComponent == null)
                {
                    sb.AppendLine("  Aim component: none");
                }
                else
                {
                    sb.AppendLine($"  Aim component: {aimComponent.GetType().Name}");
                }

                if (!string.IsNullOrWhiteSpace(propertiesJson) && aimComponent != null)
                {
                    Undo.RecordObject(aimComponent, $"Set CM Aim Props {cameraName}");
                    ApplyKeyValueProperties(aimComponent, propertiesJson, sb);
                    EditorUtility.SetDirty(aimComponent);
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}