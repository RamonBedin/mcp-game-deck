#nullable enable
using System.ComponentModel;
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
        /// Disables physics auto-simulation, steps the physics engine a given number of times with
        /// a configurable step size, then restores the previous auto-simulation state.
        /// Useful for deterministic physics testing in Edit mode.
        /// </summary>
        /// <param name="steps">Number of simulation steps to advance. Defaults to 1.</param>
        /// <param name="stepSize">Duration of each step in seconds. Defaults to 0.02 (50 Hz).</param>
        /// <returns>A <see cref="ToolResponse"/> confirming how many steps were simulated.</returns>
        [McpTool("physics-simulate-step", Title = "Physics / Simulate Step")]
        [Description("Manually advances the physics simulation by the given number of fixed steps. " + "Disables auto-simulation during the operation and restores it afterwards.")]
        public ToolResponse SimulateStep(
            [Description("Number of simulation steps to advance. Defaults to 1.")] int steps = 1,
            [Description("Duration of each simulation step in seconds. Defaults to 0.02 (50 Hz fixed timestep).")] float stepSize = 0.02f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (steps < 1)
                {
                    return ToolResponse.Error("steps must be at least 1.");
                }

                if (stepSize <= 0f)
                {
                    return ToolResponse.Error("stepSize must be greater than 0.");
                }

                stepSize = Mathf.Clamp(stepSize, 0.0001f, 1f);

                var previousMode = UnityEngine.Physics.simulationMode;
                UnityEngine.Physics.simulationMode = SimulationMode.Script;

                for (int i = 0; i < steps; i++)
                {
                    UnityEngine.Physics.Simulate(stepSize);
                }

                UnityEngine.Physics.simulationMode = previousMode;

                return ToolResponse.Text($"Simulated {steps} step{(steps == 1 ? "" : "s")} of {stepSize}s each " + $"(total: {steps * stepSize:F4}s). Auto-simulation restored to {previousMode}.");
            });
        }

        #endregion
    }
}