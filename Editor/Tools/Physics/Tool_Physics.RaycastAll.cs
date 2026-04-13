#nullable enable
using System;
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
        /// Casts a ray and returns every collider it passes through, sorted by distance from the origin.
        /// </summary>
        /// <param name="originX">X component of the ray origin.</param>
        /// <param name="originY">Y component of the ray origin.</param>
        /// <param name="originZ">Z component of the ray origin.</param>
        /// <param name="directionX">X component of the ray direction.</param>
        /// <param name="directionY">Y component of the ray direction.</param>
        /// <param name="directionZ">Z component of the ray direction.</param>
        /// <param name="maxDistance">Maximum travel distance of the ray.</param>
        /// <param name="layerMask">Layer mask used to filter hits. -1 hits all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> listing all hits in distance order, or "No hits." when the ray misses.</returns>
        [McpTool("physics-raycast-all", Title = "Physics / Raycast All")]
        [Description("Casts a ray from origin in a given direction and returns ALL hit objects along the path, " + "sorted by distance. Each hit includes position, normal, distance, collider name, GameObject name, and layer.")]
        public ToolResponse RaycastAll(
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

                var buffer = new RaycastHit[256];
                int count = UnityEngine.Physics.RaycastNonAlloc(origin, direction.normalized, buffer, maxDistance, layerMask);

                if (count == 0)
                {
                    return ToolResponse.Text("No hits.");
                }

                Array.Sort(buffer, 0, count, new RaycastHitDistanceComparer());

                var sb = new StringBuilder();
                sb.AppendLine($"Raycast All — {count} hit(s):");

                for (int i = 0; i < count; i++)
                {
                    AppendHitInfo(sb, buffer[i], i);
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Compares two <see cref="RaycastHit"/> instances by their distance from the ray origin,
        /// for use with <see cref="Array.Sort{T}(T[], int, int, System.Collections.Generic.IComparer{T})"/>.
        /// </summary>
        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            #region PUBLIC METHODS

            /// <summary>
            /// Compares <paramref name="a"/> and <paramref name="b"/> by distance.
            /// </summary>
            /// <param name="a">First hit.</param>
            /// <param name="b">Second hit.</param>
            /// <returns>Negative if <paramref name="a"/> is closer, zero if equal, positive if farther.</returns>
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);

            #endregion
        }

        #endregion
    }
}