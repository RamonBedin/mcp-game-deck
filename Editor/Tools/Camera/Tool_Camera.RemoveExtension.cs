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
        /// Finds the Cinemachine extension identified by <paramref name="extensionType"/> on the
        /// camera GameObject and destroys it via <see cref="Undo.DestroyObjectImmediate"/> so the
        /// operation can be undone.
        /// </summary>
        /// <param name="cameraName">Name of the camera GameObject. Used when <paramref name="instanceId"/> is 0.</param>
        /// <param name="instanceId">Unity instance ID of the camera GameObject. Takes priority over <paramref name="cameraName"/>.</param>
        /// <param name="extensionType">
        /// Simple or fully-qualified name of the Cinemachine extension to remove
        /// (e.g. 'CinemachineConfiner2D', 'CinemachineCollider').
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming removal, a no-change notice when the component
        /// is absent, or an error when Cinemachine is not installed or the type is unresolvable.
        /// </returns>
        [McpTool("camera-remove-extension", Title = "Camera / Remove Extension")]
        [Description("Removes a Cinemachine extension component from a virtual camera. " + "extensionType is the simple or fully-qualified component name " + "(e.g. 'CinemachineConfiner2D', 'CinemachineCollider'). " + "Requires Cinemachine to be installed.")]
        public ToolResponse RemoveExtension(
            [Description("Name of the camera GameObject. Used when instanceId is 0.")] string cameraName = "",
            [Description("Unity instance ID of the camera GameObject. Takes priority over cameraName.")] int instanceId = 0,
            [Description("Simple or fully-qualified Cinemachine extension type name (e.g. 'CinemachineConfiner2D').")] string extensionType = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(extensionType))
                {
                    return ToolResponse.Error("extensionType is required.");
                }

                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                GameObject? go = FindCameraGameObject(instanceId, cameraName);

                if (go == null)
                {
                    return ToolResponse.Error($"Camera GameObject not found. instanceId={instanceId}, cameraName='{cameraName}'.");
                }

                Type? extType = ResolveCinemachineType(extensionType);

                if (extType == null)
                {
                    return ToolResponse.Error($"Cinemachine extension type '{extensionType}' not found. " + "Verify the type name and that the required Cinemachine package is installed.");
                }

                UnityEngine.Component? comp = go.GetComponent(extType);

                if (comp == null)
                {
                    return ToolResponse.Text($"'{extType.Name}' is not present on '{go.name}' — no change made.");
                }

                string fullName = comp.GetType().FullName ?? extType.Name;
                Undo.DestroyObjectImmediate(comp);
                EditorUtility.SetDirty(go);

                var sb = new StringBuilder();
                sb.AppendLine($"Removed Cinemachine extension from '{go.name}':");
                sb.AppendLine($"  Type: {fullName}");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}