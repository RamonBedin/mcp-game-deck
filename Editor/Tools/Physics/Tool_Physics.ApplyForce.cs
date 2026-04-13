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
    /// <summary>
    /// MCP tools for physics simulation, querying, and configuration in the Unity scene.
    /// Covers force and impulse application, Rigidbody property management, raycasts,
    /// linecasts, shape casts, overlaps, joints, physics materials, collision matrix,
    /// global physics settings, and simulation stepping.
    /// </summary>
    [McpToolType]
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Adds a force vector to the Rigidbody on the specified GameObject using the chosen ForceMode.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <param name="forceX">X component of the force vector.</param>
        /// <param name="forceY">Y component of the force vector.</param>
        /// <param name="forceZ">Z component of the force vector.</param>
        /// <param name="forceMode">Unity ForceMode name: Force, Impulse, Acceleration, or VelocityChange.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the applied force and resulting velocity, or an error on invalid input.</returns>
        [McpTool("physics-apply-force", Title = "Physics / Apply Force")]
        [Description("Applies a force or impulse to a GameObject's Rigidbody. " + "The GameObject must already have a Rigidbody component. " + "Supported force modes: Force, Impulse, Acceleration, VelocityChange.")]
        public ToolResponse ApplyForce(
            [Description("GameObject name or hierarchy path (e.g. 'Player' or 'Environment/Ball').")] string target,
            [Description("X component of the force vector.")] float forceX,
            [Description("Y component of the force vector.")] float forceY,
            [Description("Z component of the force vector.")] float forceZ,
            [Description("Force mode: 'Force', 'Impulse', 'Acceleration', or 'VelocityChange'. Defaults to 'Force'.")] string forceMode = "Force"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found: '{target}'");
                }

                if (!go.TryGetComponent<Rigidbody>(out var rb))
                {
                    return ToolResponse.Error($"No Rigidbody component on '{target}'.");
                }

                if (!Enum.TryParse<ForceMode>(forceMode, true, out var mode))
                {
                    return ToolResponse.Error($"Invalid force mode: '{forceMode}'. " + "Valid values: Force, Impulse, Acceleration, VelocityChange.");
                }

                var force = new Vector3(forceX, forceY, forceZ);
                rb.AddForce(force, mode);

                var sb = new StringBuilder();
                sb.AppendLine($"Applied force to '{target}':");
                sb.AppendLine($"  Force: {force}");
                sb.AppendLine($"  Mode: {mode}");
                sb.AppendLine($"  Resulting Velocity: {rb.linearVelocity}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}