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
        /// Gets current global physics settings from the Unity Physics manager.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> containing a formatted list of physics settings.</returns>
        [McpTool("physics-get-settings", Title = "Physics / Get Settings")]
        [Description("Gets current physics settings including gravity, default solver iterations, " + "bounce threshold, sleep threshold, contact offset, and max angular speed.")]
        public ToolResponse GetSettings()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Physics Settings:");
                sb.AppendLine($"  Gravity: {Physics.gravity}");
                sb.AppendLine($"  Default Solver Iterations: {Physics.defaultSolverIterations}");
                sb.AppendLine($"  Default Solver Velocity Iterations: {Physics.defaultSolverVelocityIterations}");
                sb.AppendLine($"  Bounce Threshold: {Physics.bounceThreshold}");
                sb.AppendLine($"  Sleep Threshold: {Physics.sleepThreshold}");
                sb.AppendLine($"  Default Contact Offset: {Physics.defaultContactOffset}");
                sb.AppendLine($"  Default Max Angular Speed: {Physics.defaultMaxAngularSpeed}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}