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
        /// Finds all colliders whose bounds overlap a sphere defined by a center and radius.
        /// </summary>
        /// <param name="positionX">X component of the sphere center.</param>
        /// <param name="positionY">Y component of the sphere center.</param>
        /// <param name="positionZ">Z component of the sphere center.</param>
        /// <param name="radius">Radius of the sphere. Must be greater than zero.</param>
        /// <param name="layerMask">Layer mask used to filter results. -1 includes all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> listing all overlapping colliders with name, layer, and position.</returns>
        [McpTool("physics-overlap-sphere", Title = "Physics / Overlap Sphere")]
        [Description("Finds all colliders within a sphere defined by a center point and radius. " + "Returns the name, layer, and position of each collider found.")]
        public ToolResponse OverlapSphere(
            [Description("X component of the sphere center position.")] float positionX,
            [Description("Y component of the sphere center position.")] float positionY,
            [Description("Z component of the sphere center position.")] float positionZ,
            [Description("Radius of the sphere.")] float radius,
            [Description("Layer mask to filter which layers to include. -1 means all layers.")] int layerMask = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var center = new Vector3(positionX, positionY, positionZ);

                if (radius <= 0f)
                {
                    return ToolResponse.Error("Radius must be greater than zero.");
                }

                var buffer = new Collider[256];
                int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, layerMask);
                var colliders = buffer;

                if (count == 0)
                {
                    return ToolResponse.Text("No colliders found within the sphere.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Overlap Sphere Results ({count} collider(s) found):");
                sb.AppendLine($"  Center: {center}, Radius: {radius}");
                sb.AppendLine();

                for (int i = 0; i < count; i++)
                {
                    var col = colliders[i];
                    sb.AppendLine($"  [{i}] {col.gameObject.name}");
                    sb.AppendLine($"      Layer: {LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer})");
                    sb.AppendLine($"      Position: {col.transform.position}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Finds all colliders whose bounds overlap an axis-aligned box defined by a center and half extents.
        /// </summary>
        /// <param name="positionX">X component of the box center.</param>
        /// <param name="positionY">Y component of the box center.</param>
        /// <param name="positionZ">Z component of the box center.</param>
        /// <param name="halfExtentX">X half extent of the box. Must be greater than zero.</param>
        /// <param name="halfExtentY">Y half extent of the box. Must be greater than zero.</param>
        /// <param name="halfExtentZ">Z half extent of the box. Must be greater than zero.</param>
        /// <param name="layerMask">Layer mask used to filter results. -1 includes all layers.</param>
        /// <returns>A <see cref="ToolResponse"/> listing all overlapping colliders with name, layer, and position.</returns>
        [McpTool("physics-overlap-box", Title = "Physics / Overlap Box")]
        [Description("Finds all colliders within an axis-aligned box defined by a center point and half extents. " + "Returns the name, layer, and position of each collider found.")]
        public ToolResponse OverlapBox(
            [Description("X component of the box center position.")] float positionX,
            [Description("Y component of the box center position.")] float positionY,
            [Description("Z component of the box center position.")] float positionZ,
            [Description("X half extent of the box.")] float halfExtentX,
            [Description("Y half extent of the box.")] float halfExtentY,
            [Description("Z half extent of the box.")] float halfExtentZ,
            [Description("Layer mask to filter which layers to include. -1 means all layers.")] int layerMask = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var center = new Vector3(positionX, positionY, positionZ);
                var halfExtents = new Vector3(halfExtentX, halfExtentY, halfExtentZ);

                if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                {
                    return ToolResponse.Error("All half extents must be greater than zero.");
                }

                var buffer = new Collider[256];
                int count = Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, Quaternion.identity, layerMask);
                var colliders = buffer;

                if (count == 0)
                {
                    return ToolResponse.Text("No colliders found within the box.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Overlap Box Results ({count} collider(s) found):");
                sb.AppendLine($"  Center: {center}, Half Extents: {halfExtents}");
                sb.AppendLine();

                for (int i = 0; i < count; i++)
                {
                    var col = colliders[i];
                    sb.AppendLine($"  [{i}] {col.gameObject.name}");
                    sb.AppendLine($"      Layer: {LayerMask.LayerToName(col.gameObject.layer)} ({col.gameObject.layer})");
                    sb.AppendLine($"      Position: {col.transform.position}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}