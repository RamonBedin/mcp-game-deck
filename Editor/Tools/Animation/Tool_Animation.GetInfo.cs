#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Animation
    {
        #region GET INFO

        /// <summary>
        /// Returns detailed information about an AnimationClip or AnimatorController asset.
        /// Supply <paramref name="clipPath"/> to inspect a clip, or <paramref name="controllerPath"/>
        /// to inspect a controller. At least one path must be provided.
        /// </summary>
        /// <param name="clipPath">Project-relative path to an AnimationClip asset (e.g. 'Assets/Animations/Run.anim').</param>
        /// <param name="controllerPath">Project-relative path to an AnimatorController asset (e.g. 'Assets/Animations/Player.controller').</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with clip or controller metadata,
        /// or an error when neither asset can be loaded.
        /// </returns>
        [McpTool("animation-get-info", Title = "Animation / Get Info", ReadOnlyHint = true)]
        [Description("Returns metadata for an AnimationClip (length, frameRate, wrapMode, event count, curve count) " + "or an AnimatorController (layers, parameters, states per layer). Supply clipPath or controllerPath.")]
        public ToolResponse GetInfo(
            [Description("Project-relative path to an AnimationClip asset (e.g. 'Assets/Animations/Run.anim'). Leave empty to use controllerPath.")] string clipPath = "",
            [Description("Project-relative path to an AnimatorController asset (e.g. 'Assets/Animations/Player.controller'). Leave empty to use clipPath.")] string controllerPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(clipPath) && string.IsNullOrWhiteSpace(controllerPath))
                {
                    return ToolResponse.Error("Provide clipPath or controllerPath.");
                }

                if (!string.IsNullOrWhiteSpace(clipPath))
                {
                    return GetClipInfo(clipPath);
                }

                return GetControllerInfo(controllerPath);
            });
        }

        #endregion

        #region CLIP INFO HELPER

        /// <summary>
        /// Builds a summary string for an <see cref="AnimationClip"/> asset.
        /// </summary>
        /// <param name="path">Asset path of the clip.</param>
        /// <returns>A <see cref="ToolResponse"/> with the clip's metadata.</returns>
        private static ToolResponse GetClipInfo(string path)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
            {
                return ToolResponse.Error($"AnimationClip not found at '{path}'.");
            }

            var bindings = AnimationUtility.GetCurveBindings(clip);
            var events   = AnimationUtility.GetAnimationEvents(clip);

            var sb = new StringBuilder();
            sb.AppendLine($"AnimationClip: {path}");
            sb.AppendLine($"  Name:        {clip.name}");
            sb.AppendLine($"  Length:      {clip.length:F4} s");
            sb.AppendLine($"  Frame Rate:  {clip.frameRate} fps");
            sb.AppendLine($"  Wrap Mode:   {clip.wrapMode}");
            sb.AppendLine($"  Loop:        {clip.isLooping}");
            sb.AppendLine($"  Events:      {events.Length}");
            sb.AppendLine($"  Curves:      {bindings.Length}");

            if (bindings.Length > 0)
            {
                sb.AppendLine("  Curve Bindings (first 10):");
                int max = bindings.Length < 10 ? bindings.Length : 10;

                for (int i = 0; i < max; i++)
                {
                    sb.AppendLine($"    [{i}] {bindings[i].type?.Name ?? "?"}.{bindings[i].propertyName} (path: '{bindings[i].path}')");
                }

                if (bindings.Length > 10)
                {
                    sb.AppendLine($"    ... and {bindings.Length - 10} more");
                }
            }

            return ToolResponse.Text(sb.ToString());
        }

        #endregion

        #region Controller Info Helper

        /// <summary>
        /// Builds a summary string for an <see cref="AnimatorController"/> asset.
        /// </summary>
        /// <param name="path">Asset path of the controller.</param>
        /// <returns>A <see cref="ToolResponse"/> with the controller's metadata.</returns>
        private static ToolResponse GetControllerInfo(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);

            if (controller == null)
            {
                return ToolResponse.Error($"AnimatorController not found at '{path}'.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"AnimatorController: {path}");
            sb.AppendLine($"  Name:   {controller.name}");
            sb.AppendLine($"  Layers: {controller.layers.Length}");

            var parameters = controller.parameters;

            if (parameters.Length > 0)
            {
                sb.AppendLine($"  Parameters ({parameters.Length}):");

                for (int i = 0; i < parameters.Length; i++)
                {
                    sb.AppendLine($"    {parameters[i].name} ({parameters[i].type})");
                }
            }

            for (int li = 0; li < controller.layers.Length; li++)
            {
                var layer = controller.layers[li];
                var states = layer.stateMachine.states;
                sb.AppendLine($"  Layer[{li}] '{layer.name}' — {states.Length} state(s):");

                for (int si = 0; si < states.Length; si++)
                {
                    var state = states[si].state;
                    string motion = state.motion != null ? state.motion.name : "(no motion)";
                    sb.AppendLine($"    [{si}] '{state.name}'  motion: {motion}");
                }
            }

            return ToolResponse.Text(sb.ToString());
        }

        #endregion
    }
}