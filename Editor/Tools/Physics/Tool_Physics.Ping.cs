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
        /// Returns the most frequently needed physics configuration values: gravity, fixed delta time,
        /// default solver iterations, default solver velocity iterations, and bounce threshold.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> with a formatted summary of core physics settings.</returns>
        [McpTool("physics-ping", Title = "Physics / Ping", ReadOnlyHint = true)]
        [Description("Returns a quick summary of core physics settings: gravity, fixedDeltaTime, " + "defaultSolverIterations, defaultSolverVelocityIterations, and bounceThreshold.")]
        public ToolResponse Ping()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Physics Ping:");
                sb.AppendLine($"  Gravity: {UnityEngine.Physics.gravity}");
                sb.AppendLine($"  Fixed Delta Time: {Time.fixedDeltaTime}");
                sb.AppendLine($"  Default Solver Iterations: {UnityEngine.Physics.defaultSolverIterations}");
                sb.AppendLine($"  Default Solver Velocity Iterations: {UnityEngine.Physics.defaultSolverVelocityIterations}");
                sb.AppendLine($"  Bounce Threshold: {UnityEngine.Physics.bounceThreshold}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}