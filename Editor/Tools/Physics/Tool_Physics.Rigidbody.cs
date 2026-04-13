#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region TOOL METHODS

        /// <summary>
        /// Reads all Rigidbody properties from the specified GameObject.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the GameObject to inspect.</param>
        /// <returns>A <see cref="ToolResponse"/> with a formatted property listing, or an error if the object or component is missing.</returns>
        [McpTool("physics-get-rigidbody", Title = "Physics / Get Rigidbody")]
        [Description("Gets rigidbody properties of a GameObject by name or hierarchy path. " + "Returns mass, drag, angularDrag, useGravity, isKinematic, interpolation, " + "collisionDetectionMode, velocity, angularVelocity, and constraints.")]
        public ToolResponse GetRigidbody([Description("GameObject name or hierarchy path (e.g. 'Player' or 'Environment/Floor').")] string target
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

                var sb = new StringBuilder();
                sb.AppendLine($"Rigidbody on '{target}':");
                sb.AppendLine($"  Mass: {rb.mass}");
                sb.AppendLine($"  Drag: {rb.linearDamping}");
                sb.AppendLine($"  Angular Drag: {rb.angularDamping}");
                sb.AppendLine($"  Use Gravity: {rb.useGravity}");
                sb.AppendLine($"  Is Kinematic: {rb.isKinematic}");
                sb.AppendLine($"  Interpolation: {rb.interpolation}");
                sb.AppendLine($"  Collision Detection: {rb.collisionDetectionMode}");
                sb.AppendLine($"  Velocity: {rb.linearVelocity}");
                sb.AppendLine($"  Angular Velocity: {rb.angularVelocity}");
                sb.AppendLine($"  Constraints: {rb.constraints}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Applies one or more property changes to the Rigidbody on the specified GameObject.
        /// Adds a Rigidbody component if one does not already exist.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <param name="mass">Mass in kilograms to assign.</param>
        /// <param name="drag">Linear drag coefficient to assign.</param>
        /// <param name="angularDrag">Angular drag coefficient to assign.</param>
        /// <param name="useGravity">Whether gravity should affect this Rigidbody.</param>
        /// <param name="isKinematic">Whether this Rigidbody should be kinematic.</param>
        /// <returns>A <see cref="ToolResponse"/> describing what was changed, or an error if the object is not found.</returns>
        [McpTool("physics-configure-rigidbody", Title = "Physics / Configure Rigidbody")]
        [Description("Configures rigidbody properties on a GameObject. " + "Adds a Rigidbody component if one is not already present. " + "Only non-null parameters are applied.")]
        public ToolResponse ConfigureRigidbody(
            [Description("GameObject name or hierarchy path (e.g. 'Player' or 'Environment/Floor').")] string target,
            [Description("Mass of the rigidbody in kilograms.")] float? mass = null,
            [Description("Linear drag coefficient.")] float? drag = null,
            [Description("Angular drag coefficient.")] float? angularDrag = null,
            [Description("Whether gravity affects this rigidbody.")] bool? useGravity = null,
            [Description("Whether this rigidbody is kinematic (not driven by physics).")] bool? isKinematic = null
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found: '{target}'");
                }

                var rb = go.GetComponent<Rigidbody>();
                bool added = false;

                if (rb == null)
                {
                    rb = go.AddComponent<Rigidbody>();
                    added = true;
                }

                var sb = new StringBuilder();

                if (added)
                {
                    sb.AppendLine($"Added Rigidbody to '{target}'.");
                }

                if (mass.HasValue)
                {
                    rb.mass = mass.Value;
                    sb.AppendLine($"  Mass → {mass.Value}");
                }

                if (drag.HasValue)
                {
                    rb.linearDamping = drag.Value;
                    sb.AppendLine($"  Drag → {drag.Value}");
                }

                if (angularDrag.HasValue)
                {
                    rb.angularDamping = angularDrag.Value;
                    sb.AppendLine($"  Angular Drag → {angularDrag.Value}");
                }

                if (useGravity.HasValue)
                {
                    rb.useGravity = useGravity.Value;
                    sb.AppendLine($"  Use Gravity → {useGravity.Value}");
                }

                if (isKinematic.HasValue)
                {
                    rb.isKinematic = isKinematic.Value;
                    sb.AppendLine($"  Is Kinematic → {isKinematic.Value}");
                }

                EditorUtility.SetDirty(rb);

                if (!added && sb.Length == 0)
                {
                    sb.AppendLine("No properties changed.");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}