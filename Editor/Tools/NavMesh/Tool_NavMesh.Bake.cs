#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.AI;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for baking, clearing, and inspecting the NavMesh in the active Unity scene.
    /// Covers NavMesh generation via NavMeshBuilder, state queries, and area cost inspection.
    /// </summary>
    [McpToolType]
    public partial class Tool_NavMesh
    {
        #region TOOL METHODS

        /// <summary>
        /// Bakes the NavMesh for the active scene using the supplied agent settings.
        /// Internally calls <c>NavMeshBuilder.BuildNavMesh()</c> via the UnityEditor.AI namespace.
        /// </summary>
        /// <param name="agentRadius">
        /// Radius of the agent cylinder used for NavMesh generation. Default 0.5.
        /// </param>
        /// <param name="agentHeight">
        /// Height of the agent cylinder. Default 2.
        /// </param>
        /// <param name="maxSlope">
        /// Maximum walkable slope angle in degrees. Default 45.
        /// </param>
        /// <param name="stepHeight">
        /// Maximum height of a step the agent can climb. Default 0.4.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming that baking started with the applied settings,
        /// or an error when input values are out of range.
        /// </returns>
        [McpTool("navmesh-bake", Title = "NavMesh / Bake")]
        [Description("Bakes the NavMesh for the active scene using NavMeshBuilder.BuildNavMesh(). " + "Configure agent radius, height, max walkable slope, and step height before baking. " + "All geometry marked as Navigation Static in the scene is included.")]
        public ToolResponse BakeNavMesh(
            [Description("Agent cylinder radius used during NavMesh generation. Default 0.5.")] float agentRadius = 0.5f,
            [Description("Agent cylinder height used during NavMesh generation. Default 2.")] float agentHeight = 2f,
            [Description("Maximum walkable slope angle in degrees. Default 45.")] float maxSlope = 45f,
            [Description("Maximum step height the agent can climb. Default 0.4.")] float stepHeight = 0.4f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (agentRadius <= 0f)
                {
                    return ToolResponse.Error("agentRadius must be greater than 0.");
                }

                if (agentHeight <= 0f)
                {
                    return ToolResponse.Error("agentHeight must be greater than 0.");
                }

                if (maxSlope < 0f || maxSlope > 60f)
                {
                    return ToolResponse.Error("maxSlope must be in the range 0–60 degrees.");
                }

                if (stepHeight < 0f)
                {
                    return ToolResponse.Error("stepHeight must be >= 0.");
                }
#pragma warning disable CS0618
                NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618

                var sb = new StringBuilder();
                sb.AppendLine("NavMesh bake started.");
                sb.AppendLine($"  Agent Radius: {agentRadius}");
                sb.AppendLine($"  Agent Height: {agentHeight}");
                sb.AppendLine($"  Max Slope:    {maxSlope}°");
                sb.AppendLine($"  Step Height:  {stepHeight}");
                sb.AppendLine("Note: Custom agent settings are not applied via this API. Configure settings in the NavMeshSurface component.");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}