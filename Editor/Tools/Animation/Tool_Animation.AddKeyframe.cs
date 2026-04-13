#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, configuring, and inspecting Unity Animation assets
    /// (AnimationClips and AnimatorControllers).
    /// </summary>
    [McpToolType]
    public partial class Tool_Animation
    {
        #region ADD KEYFRAME

        /// <summary>
        /// Loads an <see cref="AnimationClip"/> and adds (or extends) a curve for the given property
        /// by inserting a single <see cref="Keyframe"/> at the specified time.
        /// </summary>
        /// <param name="clipPath">Project-relative path to the AnimationClip asset (e.g. <c>Assets/Animations/PlayerRun.anim</c>).</param>
        /// <param name="propertyPath">
        /// Animated property path as understood by <see cref="AnimationClip.SetCurve"/>,
        /// e.g. <c>localPosition.x</c>, <c>localScale.y</c>, <c>m_IsActive</c>.
        /// </param>
        /// <param name="time">Time in seconds where the keyframe will be placed.</param>
        /// <param name="value">Value of the animated property at the given time.</param>
        /// <param name="objectType">
        /// The component type that owns the property (e.g. "Transform", "MeshRenderer").
        /// Resolved via reflection against <c>UnityEngine</c> namespace.
        /// Default is "Transform".
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the keyframe was added,
        /// or an error message when the clip or type cannot be resolved.
        /// </returns>
        [McpTool("animation-add-keyframe", Title = "Animation / Add Keyframe")]
        [Description("Adds a single keyframe to a curve on an AnimationClip. " + "propertyPath example: 'localPosition.x'. objectType example: 'Transform'.")]
        public ToolResponse AddKeyframe(
            [Description("Project-relative path to the AnimationClip asset (e.g. 'Assets/Animations/PlayerRun.anim').")] string clipPath,
            [Description("Animated property path (e.g. 'localPosition.x', 'localScale.y', 'm_IsActive').")] string propertyPath,
            [Description("Time in seconds for the keyframe.")] float time,
            [Description("Value of the property at the specified time.")] float value,
            [Description("Component type that owns the property (e.g. 'Transform', 'MeshRenderer'). Default 'Transform'.")] string objectType = "Transform"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(clipPath))
                {
                    return ToolResponse.Error("clipPath is required.");
                }

                if (!clipPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("clipPath must start with 'Assets/'.");
                }

                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    return ToolResponse.Error("propertyPath is required.");
                }

                if (time < 0f)
                {
                    return ToolResponse.Error($"time must be >= 0. Got {time}.");
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                if (clip == null)
                {
                    return ToolResponse.Error($"AnimationClip not found at '{clipPath}'.");
                }

                System.Type? resolvedType = ResolveUnityType(objectType);

                if (resolvedType == null)
                {
                    return ToolResponse.Error($"Cannot resolve component type '{objectType}'. " + "Use a fully-qualified UnityEngine type name (e.g. 'Transform', 'MeshRenderer').");
                }

                var binding = new EditorCurveBinding
                {
                    path         = string.Empty,
                    type         = resolvedType,
                    propertyName = propertyPath
                };

                AnimationCurve? existingCurve = AnimationUtility.GetEditorCurve(clip, binding);
                AnimationCurve curve = existingCurve ?? new AnimationCurve();

                var keyframe = new Keyframe(time, value);
                curve.AddKey(keyframe);

                AnimationUtility.SetEditorCurve(clip, binding, curve);

                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();

                int keyCount = curve.length;
                return ToolResponse.Text($"Keyframe added to '{clipPath}': {objectType}.{propertyPath} at t={time}s, value={value}. " + $"Curve now has {keyCount} key{(keyCount == 1 ? "" : "s")}.");
            });
        }

        #endregion

        #region TYPE RESOLUTION HELPER

        /// <summary>
        /// Resolves a short component type name (e.g. "Transform") to a <see cref="System.Type"/>
        /// by searching the <c>UnityEngine</c> assembly.
        /// </summary>
        /// <param name="typeName">Short or fully-qualified type name.</param>
        /// <returns>The resolved <see cref="System.Type"/>, or <c>null</c> when not found.</returns>
        private static System.Type? ResolveUnityType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            System.Type? t = System.Type.GetType(typeName);

            if (t != null)
            {
                return t;
            }

            t = System.Type.GetType($"UnityEngine.{typeName}, UnityEngine");

            if (t != null)
            {
                return t;
            }

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            for (int ai = 0; ai < assemblies.Length; ai++)
            {
                var asm = assemblies[ai];
                t = asm.GetType(typeName);
                if (t != null) return t;
                t = asm.GetType($"UnityEngine.{typeName}");

                if (t != null)
                {
                    return t;
                }
            }

            return null;
        }

        #endregion
    }
}