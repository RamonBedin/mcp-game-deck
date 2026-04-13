#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Rotates a GameObject so that its forward axis points at a world-space position or at
        /// another named GameObject. The operation is recorded on the Undo stack.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the GameObject to rotate. Pass 0 to use <paramref name="objectPath"/>.</param>
        /// <param name="objectPath">Hierarchy path of the GameObject to rotate (e.g. 'Enemy/Head'). Used when instanceId is 0.</param>
        /// <param name="targetX">X component of the world-space look-at position. Ignored when <paramref name="targetName"/> is provided.</param>
        /// <param name="targetY">Y component of the world-space look-at position. Ignored when <paramref name="targetName"/> is provided.</param>
        /// <param name="targetZ">Z component of the world-space look-at position. Ignored when <paramref name="targetName"/> is provided.</param>
        /// <param name="targetName">Name or hierarchy path of another GameObject whose position is used as the look-at target.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new rotation,
        /// or an error when either GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-look-at", Title = "GameObject / Look At")]
        [Description("Rotates a GameObject so its forward axis faces a world-space point or another GameObject. " + "Provide instanceId or objectPath to identify the source. " + "Provide targetName (hierarchy path) to look at another GO, or set targetX/Y/Z for a world position. " + "Registers the rotation with Undo.")]
        public ToolResponse LookAt(
            [Description("Unity instance ID of the GameObject to rotate. Pass 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the GameObject to rotate (e.g. 'Enemy/Head'). Used when instanceId is 0.")] string objectPath = "",
            [Description("World-space X of the look-at target. Ignored when targetName is set.")] float targetX = 0f,
            [Description("World-space Y of the look-at target. Ignored when targetName is set.")] float targetY = 0f,
            [Description("World-space Z of the look-at target. Ignored when targetName is set.")] float targetZ = 0f,
            [Description("Name or hierarchy path of a target GameObject. When non-empty, overrides targetX/Y/Z.")] string targetName = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"Source GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Vector3 lookTarget;

                if (!string.IsNullOrWhiteSpace(targetName))
                {
                    var targetGo = Tool_Transform.FindGameObject(0, targetName);

                    if (targetGo == null)
                    {
                        return ToolResponse.Error($"Target GameObject '{targetName}' not found.");
                    }

                    lookTarget = targetGo.transform.position;
                }
                else
                {
                    lookTarget = new Vector3(targetX, targetY, targetZ);
                }

                Undo.RecordObject(go.transform, $"LookAt {go.name}");
                go.transform.LookAt(lookTarget);

                return ToolResponse.Text($"'{go.name}' now faces {lookTarget}.\n" + $"  Rotation: {go.transform.rotation.eulerAngles}");
            });
        }

        #endregion
    }
}