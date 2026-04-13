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
        /// Casts a ray from a world-space origin along a direction and reports the first collider hit.
        /// </summary>
        /// <param name="originX">X component of the ray origin.</param>
        /// <param name="originY">Y component of the ray origin.</param>
        /// <param name="originZ">Z component of the ray origin.</param>
        /// <param name="directionX">X component of the ray direction.</param>
        /// <param name="directionY">Y component of the ray direction.</param>
        /// <param name="directionZ">Z component of the ray direction.</param>
        /// <param name="maxDistance">Maximum travel distance of the ray.</param>
        /// <param name="layerMask">Layer mask used to filter hits. -1 hits all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> with hit details, or "No hit." when nothing is struck.</returns>
        [McpTool("physics-raycast", Title = "Physics / Raycast")]
        [Description("Casts a ray from origin in a given direction and returns the first hit object, " + "including hit position, normal, distance, collider name, GameObject name, and layer.")]
        public ToolResponse Raycast(
            [Description("X component of the ray origin position.")] float originX,
            [Description("Y component of the ray origin position.")] float originY,
            [Description("Z component of the ray origin position.")] float originZ,
            [Description("X component of the ray direction vector.")] float directionX,
            [Description("Y component of the ray direction vector.")] float directionY,
            [Description("Z component of the ray direction vector.")] float directionZ,
            [Description("Maximum distance the ray should travel. Default is 1000.")] float maxDistance = 1000f,
            [Description("Layer mask to filter which layers the ray can hit. -1 means all layers.")] int layerMask = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var origin = new Vector3(originX, originY, originZ);
                var direction = new Vector3(directionX, directionY, directionZ);

                if (direction.sqrMagnitude < Mathf.Epsilon)
                {
                    return ToolResponse.Error("Direction vector cannot be zero.");
                }

                if (UnityEngine.Physics.Raycast(origin, direction.normalized, out RaycastHit hit, maxDistance, layerMask))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Raycast Hit:");
                    AppendHitInfo(sb, hit);
                    return ToolResponse.Text(sb.ToString());
                }

                return ToolResponse.Text("No hit.");
            });
        }

        #endregion
    }
}