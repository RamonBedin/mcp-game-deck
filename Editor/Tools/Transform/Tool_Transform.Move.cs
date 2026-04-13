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
        /// Moves a GameObject to an absolute or relative position in world or local space.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="x">X component of the target position.</param>
        /// <param name="y">Y component of the target position.</param>
        /// <param name="z">Z component of the target position.</param>
        /// <param name="space">Coordinate space: "world" or "local". Default is "world".</param>
        /// <param name="relative">If true, adds the given values to the current position. If false, sets position directly.</param>
        /// <returns>A <see cref="ToolResponse"/> with the resulting position, or an error if the object is not found.</returns>
        [McpTool("transform-move", Title = "Transform / Move")]
        [Description("Moves a GameObject to an absolute or relative position in world or local space. " + "Returns the resulting world position after the operation.")]
        public ToolResponse Move(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("X component of the target position. Default 0.")] float x = 0f,
            [Description("Y component of the target position. Default 0.")] float y = 0f,
            [Description("Z component of the target position. Default 0.")] float z = 0f,
            [Description("Coordinate space: 'world' or 'local'. Default is 'world'.")] string space = "world",
            [Description("If true, adds the given values to the current position. If false, sets position directly.")] bool relative = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Undo.RecordObject(go.transform, $"Move {go.name}");

                var delta = new Vector3(x, y, z);
                bool useLocal = space == "local";

                if (relative)
                {
                    if (useLocal)
                    {
                        go.transform.localPosition += delta;
                    }
                    else
                    {
                        go.transform.position += delta;
                    }
                }
                else
                {
                    if (useLocal)
                    {
                        go.transform.localPosition = delta;
                    }
                    else
                    {
                        go.transform.position = delta;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Moved '{go.name}':");
                sb.AppendLine($"  World Position: {go.transform.position}");
                sb.AppendLine($"  Local Position: {go.transform.localPosition}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}