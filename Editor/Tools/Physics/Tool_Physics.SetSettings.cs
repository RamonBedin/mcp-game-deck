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
        /// Applies one or more changes to the global Unity physics settings.
        /// Any parameter left null is not modified.
        /// </summary>
        /// <param name="gravity">World-space gravity vector to apply.</param>
        /// <param name="defaultSolverIterations">Number of solver iterations per physics step.</param>
        /// <param name="defaultSolverVelocityIterations">Number of velocity solver iterations per physics step.</param>
        /// <param name="bounceThreshold">Minimum relative velocity required for a bounce to occur.</param>
        /// <param name="sleepThreshold">Energy threshold below which objects are put to sleep.</param>
        /// <param name="defaultContactOffset">Default contact offset applied to newly created colliders.</param>
        /// <returns>A <see cref="ToolResponse"/> listing each setting that was changed.</returns>
        [McpTool("physics-set-settings", Title = "Physics / Set Settings")]
        [Description("Modifies physics settings. Only provided (non-null) values are changed. " + "Omit a parameter to leave its current value unchanged.")]
        public ToolResponse SetSettings(
            [Description("World gravity vector. Example: (0, -9.81, 0).")] Vector3? gravity = null,
            [Description("Default number of solver iterations per physics step.")] int? defaultSolverIterations = null,
            [Description("Default number of velocity solver iterations per physics step.")] int? defaultSolverVelocityIterations = null,
            [Description("Minimum relative velocity required for a collision to trigger a bounce.")] float? bounceThreshold = null,
            [Description("Mass-normalized energy threshold below which objects start going to sleep.")] float? sleepThreshold = null,
            [Description("Default contact offset for newly created colliders.")] float? defaultContactOffset = null
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Physics settings updated:");

                if (gravity.HasValue)
                {
                    Physics.gravity = gravity.Value;
                    sb.AppendLine($"  Gravity: {Physics.gravity}");
                }

                if (defaultSolverIterations.HasValue)
                {
                    Physics.defaultSolverIterations = defaultSolverIterations.Value;
                    sb.AppendLine($"  Default Solver Iterations: {Physics.defaultSolverIterations}");
                }

                if (defaultSolverVelocityIterations.HasValue)
                {
                    Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations.Value;
                    sb.AppendLine($"  Default Solver Velocity Iterations: {Physics.defaultSolverVelocityIterations}");
                }

                if (bounceThreshold.HasValue)
                {
                    Physics.bounceThreshold = bounceThreshold.Value;
                    sb.AppendLine($"  Bounce Threshold: {Physics.bounceThreshold}");
                }

                if (sleepThreshold.HasValue)
                {
                    Physics.sleepThreshold = sleepThreshold.Value;
                    sb.AppendLine($"  Sleep Threshold: {Physics.sleepThreshold}");
                }

                if (defaultContactOffset.HasValue)
                {
                    Physics.defaultContactOffset = defaultContactOffset.Value;
                    sb.AppendLine($"  Default Contact Offset: {Physics.defaultContactOffset}");
                }

                if (sb.ToString().Trim() == "Physics settings updated:")
                {
                    sb.AppendLine("  No values were provided. No changes made.");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}