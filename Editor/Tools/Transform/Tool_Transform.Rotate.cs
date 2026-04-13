#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Transform
    {
        #region TOOL METHODS

        /// <summary>
        /// Rotates a GameObject to an absolute or relative orientation in world or local space using Euler angles.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="x">X rotation in degrees (pitch).</param>
        /// <param name="y">Y rotation in degrees (yaw).</param>
        /// <param name="z">Z rotation in degrees (roll).</param>
        /// <param name="space">Coordinate space: "world" or "local". Default is "world".</param>
        /// <param name="relative">If true, adds the given angles to the current rotation. If false, sets rotation directly.</param>
        /// <returns>A <see cref="ToolResponse"/> with the resulting Euler angles, or an error if the object is not found.</returns>
        [McpTool("transform-rotate", Title = "Transform / Rotate")]
        [Description("Rotates a GameObject to an absolute or relative orientation in world or local space " + "using Euler angles in degrees. Returns the resulting rotation after the operation.")]
        public ToolResponse Rotate(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("X rotation in degrees (pitch). Default 0.")] float x = 0f,
            [Description("Y rotation in degrees (yaw). Default 0.")] float y = 0f,
            [Description("Z rotation in degrees (roll). Default 0.")] float z = 0f,
            [Description("Coordinate space: 'world' or 'local'. Default is 'world'.")] string space = "world",
            [Description("If true, adds the given angles to the current rotation. If false, sets rotation directly.")] bool relative = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Undo.RecordObject(go.transform, $"Rotate {go.name}");

                var euler = new Vector3(x, y, z);
                bool useLocal = space == "local";

                if (relative)
                {
                    if (useLocal)
                    {
                        go.transform.localRotation *= Quaternion.Euler(euler);
                    }
                    else
                    {
                        go.transform.rotation *= Quaternion.Euler(euler);
                    }
                }
                else
                {
                    if (useLocal)
                    {
                        go.transform.localRotation = Quaternion.Euler(euler);
                    }
                    else
                    {
                        go.transform.rotation = Quaternion.Euler(euler);
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Rotated '{go.name}':");
                sb.AppendLine($"  World Euler: {go.transform.rotation.eulerAngles}");
                sb.AppendLine($"  Local Euler: {go.transform.localRotation.eulerAngles}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}