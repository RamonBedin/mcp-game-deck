#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Casts a line segment between two world-space points and reports the first collider hit.
        /// </summary>
        /// <param name="startX">X component of the line start point.</param>
        /// <param name="startY">Y component of the line start point.</param>
        /// <param name="startZ">Z component of the line start point.</param>
        /// <param name="endX">X component of the line end point.</param>
        /// <param name="endY">Y component of the line end point.</param>
        /// <param name="endZ">Z component of the line end point.</param>
        /// <param name="layerMask">Layer mask used to filter hits. -1 hits all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> with hit details, or "No hit." when nothing is struck.</returns>
        [McpTool("physics-linecast", Title = "Physics / Linecast")]
        [Description("Casts a line between two points and returns the first hit, " + "including hit position, normal, distance, collider name, GameObject name, and layer.")]
        public ToolResponse Linecast(
            [Description("X component of the start point.")] float startX,
            [Description("Y component of the start point.")] float startY,
            [Description("Z component of the start point.")] float startZ,
            [Description("X component of the end point.")] float endX,
            [Description("Y component of the end point.")] float endY,
            [Description("Z component of the end point.")] float endZ,
            [Description("Layer mask to filter which layers the linecast can hit. -1 means all layers.")] int layerMask = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var start = new Vector3(startX, startY, startZ);
                var end = new Vector3(endX, endY, endZ);

                if (Physics.Linecast(start, end, out RaycastHit hit, layerMask))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Linecast Hit:");
                    AppendHitInfo(sb, hit);
                    return ToolResponse.Text(sb.ToString());
                }

                return ToolResponse.Text("No hit.");
            });
        }

        #endregion
    }
}