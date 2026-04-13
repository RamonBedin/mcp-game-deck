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
        /// Performs a sphere cast or box cast from an origin along a direction and returns the first hit.
        /// </summary>
        /// <param name="shape">Shape to cast: "sphere" or "box". Defaults to "sphere".</param>
        /// <param name="originX">X component of the cast origin.</param>
        /// <param name="originY">Y component of the cast origin.</param>
        /// <param name="originZ">Z component of the cast origin.</param>
        /// <param name="dirX">X component of the cast direction. Defaults to 0.</param>
        /// <param name="dirY">Y component of the cast direction. Defaults to 0.</param>
        /// <param name="dirZ">Z component of the cast direction. Defaults to 1.</param>
        /// <param name="size">Radius for sphere cast; half-extent for box cast. Defaults to 0.5.</param>
        /// <param name="maxDistance">Maximum cast distance. Defaults to 100.</param>
        /// <param name="layerMask">Layer mask filter. -1 hits all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> with hit details or "No hit."</returns>
        [McpTool("physics-shapecast", Title = "Physics / Shape Cast")]
        [Description("Performs a sphere or box cast from an origin along a direction and returns the first collider hit, " + "including hit position, normal, distance, collider name, and layer.")]
        public ToolResponse ShapeCast(
            [Description("Shape to cast: 'sphere' or 'box'. Defaults to 'sphere'.")] string shape = "sphere",
            [Description("X component of the cast origin.")] float originX = 0f,
            [Description("Y component of the cast origin.")] float originY = 0f,
            [Description("Z component of the cast origin.")] float originZ = 0f,
            [Description("X component of the cast direction. Defaults to 0.")] float dirX = 0f,
            [Description("Y component of the cast direction. Defaults to 0.")] float dirY = 0f,
            [Description("Z component of the cast direction. Defaults to 1.")] float dirZ = 1f,
            [Description("Radius for sphere cast or half-extent for box cast. Defaults to 0.5.")] float size = 0.5f,
            [Description("Maximum cast distance. Defaults to 100.")] float maxDistance = 100f,
            [Description("Layer mask to filter hits. -1 means all layers.")] int layerMask = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var origin    = new Vector3(originX, originY, originZ);
                var direction = new Vector3(dirX, dirY, dirZ);

                if (direction.sqrMagnitude < Mathf.Epsilon)
                {
                    return ToolResponse.Error("Direction vector cannot be zero.");
                }

                direction = direction.normalized;

                bool hit = false;
                RaycastHit hitInfo = default;

                string shapeNorm = shape.ToLowerInvariant().Trim();

                if (shapeNorm == "sphere")
                {
                    hit = UnityEngine.Physics.SphereCast(origin, size, direction, out hitInfo, maxDistance, layerMask);
                }
                else if (shapeNorm == "box")
                {
                    var halfExtents = new Vector3(size, size, size);
                    hit = UnityEngine.Physics.BoxCast(origin, halfExtents, direction, out hitInfo, Quaternion.identity, maxDistance, layerMask);
                }
                else
                {
                    return ToolResponse.Error($"Unknown shape '{shape}'. Supported values: 'sphere', 'box'.");
                }

                if (hit)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"ShapeCast ({shapeNorm}) Hit:");
                    AppendHitInfo(sb, hitInfo);
                    return ToolResponse.Text(sb.ToString());
                }

                return ToolResponse.Text("No hit.");
            });
        }

        #endregion
    }
}