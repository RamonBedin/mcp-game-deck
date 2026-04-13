#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_NavMesh
    {
        #region Get Info

        /// <summary>
        /// Returns information about the baked NavMesh in the current scene.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with vertex count, triangle count, and per-area cost data,
        /// or an informational message when no NavMesh has been baked yet.
        /// </returns>
        [McpTool("navmesh-get-info", Title = "NavMesh / Get Info", ReadOnlyHint = true)]
        [Description("Returns information about the baked NavMesh: triangle count, vertex count, and area info.")]
        public ToolResponse GetInfo()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
                var sb = new StringBuilder();

                if (triangulation.vertices.Length == 0)
                {
                    sb.AppendLine("NavMesh: Not baked (no data).");
                    sb.AppendLine("Use navmesh-bake to generate NavMesh data.");
                    return ToolResponse.Text(sb.ToString());
                }

                sb.AppendLine("NavMesh Info:");
                sb.AppendLine($"  Vertices: {triangulation.vertices.Length}");
                sb.AppendLine($"  Triangles: {triangulation.indices.Length / 3}");

                var areas = new System.Collections.Generic.HashSet<int>();

                for (int i = 0; i < triangulation.areas.Length; i++)
                {
                    areas.Add(triangulation.areas[i]);
                }

                sb.AppendLine($"  Areas used: {areas.Count}");

                foreach (int area in areas)
                {
                    float cost = UnityEngine.AI.NavMesh.GetAreaCost(area);
                    sb.AppendLine($"    Area {area}: cost={cost}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}