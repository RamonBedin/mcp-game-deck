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
        /// Adds a joint component of the specified type to a GameObject and optionally connects it
        /// to a second Rigidbody.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <param name="jointType">Joint type to add: fixed, hinge, spring, character, or configurable.</param>
        /// <param name="connectedBody">Optional name or path of the GameObject to use as the connected Rigidbody.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming which joint was added and its connected body, or an error on failure.</returns>
        [McpTool("physics-add-joint", Title = "Physics / Add Joint")]
        [Description("Adds a joint component to a GameObject. Supports fixed, hinge, spring, character, and configurable joint types.")]
        public ToolResponse AddJoint(
            [Description("Name or hierarchy path of the target GameObject.")] string target,
            [Description("Type of joint to add: fixed, hinge, spring, character, configurable.")] string jointType,
            [Description("Optional name or path of the GameObject to use as connected body.")] string? connectedBody = null
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{target}' not found.");
                }

                var type = jointType.ToLowerInvariant().Trim();
                Joint joint;

                switch (type)
                {
                    case "fixed":
                        joint = go.AddComponent<FixedJoint>();
                        break;

                    case "hinge":
                        joint = go.AddComponent<HingeJoint>();
                        break;

                    case "spring":
                        joint = go.AddComponent<SpringJoint>();
                        break;

                    case "character":
                        joint = go.AddComponent<CharacterJoint>();
                        break;

                    case "configurable":
                        joint = go.AddComponent<ConfigurableJoint>();
                        break;

                    default:
                        return ToolResponse.Error($"Unknown joint type '{jointType}'. Use: fixed, hinge, spring, character, configurable.");
                }

                if (!string.IsNullOrEmpty(connectedBody))
                {
                    var connectedGo = GameObject.Find(connectedBody);

                    if (connectedGo == null)
                    {
                        return ToolResponse.Error($"Connected body GameObject '{connectedBody}' not found.");
                    }

                    if (!connectedGo.TryGetComponent<Rigidbody>(out var rb))
                    {
                        return ToolResponse.Error($"Connected body GameObject '{connectedBody}' does not have a Rigidbody component.");
                    }

                    joint.connectedBody = rb;
                }

                EditorUtility.SetDirty(go);
                return ToolResponse.Text($"Added {joint.GetType().Name} to '{go.name}'." + (joint.connectedBody != null ? $" Connected to '{joint.connectedBody.gameObject.name}'." : ""));
            });
        }

        /// <summary>
        /// Modifies joint properties (break thresholds, collision, preprocessing) on the first
        /// Joint component found on a GameObject.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <param name="breakForce">Force threshold at which the joint breaks. Use Mathf.Infinity for unbreakable.</param>
        /// <param name="breakTorque">Torque threshold at which the joint breaks. Use Mathf.Infinity for unbreakable.</param>
        /// <param name="enableCollision">Whether to enable collision between the connected bodies.</param>
        /// <param name="enablePreprocessing">Whether to enable joint preprocessing. Disabling can improve stability.</param>
        /// <returns>A <see cref="ToolResponse"/> listing each property that was changed, or an error if no joint exists.</returns>
        [McpTool("physics-configure-joint", Title = "Physics / Configure Joint")]
        [Description("Configures joint properties on a GameObject, such as break force, break torque, collision, and preprocessing.")]
        public ToolResponse ConfigureJoint(
            [Description("Name or hierarchy path of the target GameObject.")] string target,
            [Description("Force threshold at which the joint breaks. Use Mathf.Infinity for unbreakable.")] float? breakForce = null,
            [Description("Torque threshold at which the joint breaks. Use Mathf.Infinity for unbreakable.")] float? breakTorque = null,
            [Description("Whether to enable collision between the connected bodies.")] bool? enableCollision = null,
            [Description("Whether to enable joint preprocessing. Disabling can help with stability issues.")] bool? enablePreprocessing = null
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{target}' not found.");
                }


                if (!go.TryGetComponent<Joint>(out var joint))
                {
                    return ToolResponse.Error($"GameObject '{target}' does not have a Joint component.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Joint configured on '{go.name}' ({joint.GetType().Name}):");

                if (breakForce.HasValue)
                {
                    joint.breakForce = breakForce.Value;
                    sb.AppendLine($"  Break Force: {joint.breakForce}");
                }

                if (breakTorque.HasValue)
                {
                    joint.breakTorque = breakTorque.Value;
                    sb.AppendLine($"  Break Torque: {joint.breakTorque}");
                }

                if (enableCollision.HasValue)
                {
                    joint.enableCollision = enableCollision.Value;
                    sb.AppendLine($"  Enable Collision: {joint.enableCollision}");
                }

                if (enablePreprocessing.HasValue)
                {
                    joint.enablePreprocessing = enablePreprocessing.Value;
                    sb.AppendLine($"  Enable Preprocessing: {joint.enablePreprocessing}");
                }

                EditorUtility.SetDirty(go);
                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Removes all Joint components from the specified GameObject.
        /// </summary>
        /// <param name="target">Name or hierarchy path of the target GameObject.</param>
        /// <returns>A <see cref="ToolResponse"/> reporting how many joints were removed, or a message if none were found.</returns>
        [McpTool("physics-remove-joint", Title = "Physics / Remove Joint")]
        [Description("Removes all joint components from a GameObject and returns the count of joints removed.")]
        public ToolResponse RemoveJoint(
            [Description("Name or hierarchy path of the target GameObject.")] string target
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = GameObject.Find(target);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject '{target}' not found.");
                }

                var joints = go.GetComponents<Joint>();

                if (joints.Length == 0)
                {
                    return ToolResponse.Text($"No joints found on '{go.name}'.");
                }

                int count = joints.Length;

                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    Object.DestroyImmediate(joints[i]);
                }

                EditorUtility.SetDirty(go);
                return ToolResponse.Text($"Removed {count} joint(s) from '{go.name}'.");
            });
        }

        #endregion
    }
}