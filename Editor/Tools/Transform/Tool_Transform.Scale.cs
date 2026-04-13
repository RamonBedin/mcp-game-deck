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
        /// Scales a GameObject to an absolute or relative local scale.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="x">X component of the target local scale.</param>
        /// <param name="y">Y component of the target local scale.</param>
        /// <param name="z">Z component of the target local scale.</param>
        /// <param name="relative">If true, multiplies each axis of the current local scale by the given values. If false, sets local scale directly.</param>
        /// <returns>A <see cref="ToolResponse"/> with the resulting local scale, or an error if the object is not found.</returns>
        [McpTool("transform-scale", Title = "Transform / Scale")]
        [Description("Scales a GameObject to an absolute local scale or multiplies its current scale by the given values. " + "Returns the resulting local scale after the operation.")]
        public ToolResponse Scale(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("X component of the target local scale. Default 1.")] float x = 1f,
            [Description("Y component of the target local scale. Default 1.")] float y = 1f,
            [Description("Z component of the target local scale. Default 1.")] float z = 1f,
            [Description("If true, multiplies each axis of the current local scale by the given values. If false, sets local scale directly.")] bool relative = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Undo.RecordObject(go.transform, $"Scale {go.name}");

                var scale = new Vector3(x, y, z);

                if (relative)
                {
                    var current = go.transform.localScale;
                    go.transform.localScale = new Vector3(current.x * scale.x, current.y * scale.y, current.z * scale.z);
                }
                else
                {
                    go.transform.localScale = scale;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Scaled '{go.name}':");
                sb.AppendLine($"  Local Scale: {go.transform.localScale}");
                sb.AppendLine($"  Lossy Scale: {go.transform.lossyScale}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}