#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_ProBuilder
    {
        #region TOOL METHODS

        /// <summary>Gets information about a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> with vertex, face, and edge counts, or an error if ProBuilder is not installed or the mesh is not found.</returns>
        [McpTool("probuilder-get-mesh-info", Title = "ProBuilder / Get Mesh Info", ReadOnlyHint = true)]
        [Description("Returns ProBuilder mesh information: vertex count, face count, edge count.")]
        public ToolResponse GetMeshInfo(
            [Description("Instance ID of the ProBuilder GameObject.")] int instanceId = 0,
            [Description("Path of the ProBuilder GameObject.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component found.");
                }

                var pbType = pb.GetType();
                var vertCount = pbType.GetProperty("vertexCount")?.GetValue(pb);
                var faceCount = pbType.GetProperty("faceCount")?.GetValue(pb);
                var edgeCount = pbType.GetProperty("edgeCount")?.GetValue(pb);

                var sb = new StringBuilder();
                sb.AppendLine($"ProBuilder Mesh: {go.name}");
                sb.AppendLine($"  Vertices: {vertCount}");
                sb.AppendLine($"  Faces: {faceCount}");
                sb.AppendLine($"  Edges: {edgeCount}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}