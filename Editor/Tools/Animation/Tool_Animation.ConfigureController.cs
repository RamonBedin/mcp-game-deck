#nullable enable
using System.ComponentModel;
using System.IO;
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
        #region CONFIGURE CONTROLLER

        /// <summary>
        /// Creates or modifies an <see cref="AnimatorController"/> asset using the specified action.
        /// </summary>
        /// <param name="controllerPath">
        /// Project-relative path to an existing AnimatorController.
        /// Required for all actions except "create".
        /// </param>
        /// <param name="action">
        /// Operation to perform: "create", "add-state", "add-transition", or "set-default".
        /// </param>
        /// <param name="stateName">
        /// Name of the state to add or act upon. Required for "add-state", "add-transition", and "set-default".
        /// </param>
        /// <param name="clipPath">
        /// Project-relative path to an AnimationClip to associate with a new state. Used by "add-state".
        /// </param>
        /// <param name="fromState">Source state name for "add-transition".</param>
        /// <param name="toState">Destination state name for "add-transition".</param>
        /// <param name="savePath">
        /// Folder or full .controller path where the new controller is saved. Used by "create".
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> describing the result of the action,
        /// or an error when the controller or referenced states cannot be located.
        /// </returns>
        [McpTool("animation-configure-controller", Title = "Animation / Configure Controller")]
        [Description("Creates or modifies an AnimatorController. " + "action values: 'create' (new controller at savePath/stateName.controller), " + "'add-state' (add state with optional clip), " + "'add-transition' (add transition from fromState to toState), " + "'set-default' (set default state by stateName).")]
        public ToolResponse ConfigureController(
            [Description("Path to an existing AnimatorController asset. Required for add-state, add-transition, set-default.")] string controllerPath = "",
            [Description("Action to perform: 'create', 'add-state', 'add-transition', 'set-default'.")] string action = "create",
            [Description("State name. Required for add-state, add-transition (fromState/toState), and set-default.")] string stateName = "",
            [Description("Path to an AnimationClip to attach to the new state (used by add-state).")] string clipPath = "",
            [Description("Source state name for add-transition.")] string fromState = "",
            [Description("Destination state name for add-transition.")] string toState = "",
            [Description("Folder or full .controller path for the new file (used by create). Default 'Assets/Animations'.")] string savePath = "Assets/Animations"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                return action.ToLowerInvariant() switch
                {
                    "create" => ExecuteCreate(stateName, savePath),
                    "add-state" => ExecuteAddState(controllerPath, stateName, clipPath),
                    "add-transition" => ExecuteAddTransition(controllerPath, fromState, toState),
                    "set-default" => ExecuteSetDefault(controllerPath, stateName),
                    _ => ToolResponse.Error($"Unknown action '{action}'. Valid values: 'create', 'add-state', 'add-transition', 'set-default'."),
                };
            });
        }

        #endregion

        #region ACTION IMPLEMENTATIONS

        /// <summary>
        /// Creates a new AnimatorController asset.
        /// </summary>
        /// <param name="controllerName">Name for the controller (derived from stateName param).</param>
        /// <param name="savePath">Destination folder or full .controller path.</param>
        /// <returns>A <see cref="ToolResponse"/> with the asset path of the created controller.</returns>
        private static ToolResponse ExecuteCreate(string controllerName, string savePath)
        {
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                controllerName = "NewController";
            }

            if (!savePath.StartsWith("Assets/"))
            {
                return ToolResponse.Error("savePath must start with 'Assets/'.");
            }

            string assetPath;

            if (savePath.EndsWith(".controller", System.StringComparison.OrdinalIgnoreCase))
            {
                assetPath = savePath;
            }
            else
            {
                assetPath = savePath.TrimEnd('/') + "/" + controllerName + ".controller";
            }

            string? folder = Path.GetDirectoryName(assetPath);

            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(assetPath);

            if (controller == null)
            {
                return ToolResponse.Error($"Failed to create AnimatorController at '{assetPath}'.");
            }

            AssetDatabase.SaveAssets();
            return ToolResponse.Text($"AnimatorController created at '{assetPath}'.");
        }

        /// <summary>
        /// Adds a new state to an existing AnimatorController's base layer.
        /// </summary>
        /// <param name="controllerPath">Path to the controller asset.</param>
        /// <param name="stateName">Name of the state to add.</param>
        /// <param name="clipPath">Optional path to an AnimationClip to associate.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the added state.</returns>
        private static ToolResponse ExecuteAddState(string controllerPath, string stateName, string clipPath)
        {
            if (string.IsNullOrWhiteSpace(controllerPath))
            {
                return ToolResponse.Error("controllerPath is required for action 'add-state'.");
            }

            if (!controllerPath.StartsWith("Assets/"))
            {
                return ToolResponse.Error("controllerPath must start with 'Assets/'.");
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                return ToolResponse.Error("stateName is required for action 'add-state'.");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                return ToolResponse.Error($"AnimatorController not found at '{controllerPath}'.");
            }

            var stateMachine = controller.layers[0].stateMachine;
            var existingStates = stateMachine.states;

            for (int i = 0; i < existingStates.Length; i++)
            {
                if (existingStates[i].state.name == stateName)
                {
                    return ToolResponse.Error($"State '{stateName}' already exists in the base layer.");
                }
            }

            var newState = stateMachine.AddState(stateName);

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                if (!clipPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("clipPath must start with 'Assets/'.");
                }

                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

                if (clip == null)
                {
                    return ToolResponse.Error($"AnimationClip not found at '{clipPath}'.");
                }

                newState.motion = clip;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.Append($"State '{stateName}' added to '{controllerPath}'.");

            if (!string.IsNullOrWhiteSpace(clipPath))
            {
                sb.Append($" Motion: '{clipPath}'.");
            }

            return ToolResponse.Text(sb.ToString());
        }

        /// <summary>
        /// Adds a transition between two existing states in the base layer.
        /// </summary>
        /// <param name="controllerPath">Path to the controller asset.</param>
        /// <param name="fromStateName">Source state name.</param>
        /// <param name="toStateName">Destination state name.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the transition.</returns>
        private static ToolResponse ExecuteAddTransition(string controllerPath, string fromStateName, string toStateName)
        {
            if (string.IsNullOrWhiteSpace(controllerPath))
            {
                return ToolResponse.Error("controllerPath is required for action 'add-transition'.");
            }

            if (!controllerPath.StartsWith("Assets/"))
            {
                return ToolResponse.Error("controllerPath must start with 'Assets/'.");
            }

            if (string.IsNullOrWhiteSpace(fromStateName))
            {
                return ToolResponse.Error("fromState is required for action 'add-transition'.");
            }

            if (string.IsNullOrWhiteSpace(toStateName))
            {
                return ToolResponse.Error("toState is required for action 'add-transition'.");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                return ToolResponse.Error($"AnimatorController not found at '{controllerPath}'.");
            }

            var stateMachine = controller.layers[0].stateMachine;

            AnimatorState? from = FindState(stateMachine, fromStateName);

            if (from == null)
            {
                return ToolResponse.Error($"State '{fromStateName}' not found in the base layer.");
            }

            AnimatorState? to = FindState(stateMachine, toStateName);

            if (to == null)
            {
                return ToolResponse.Error($"State '{toStateName}' not found in the base layer.");
            }

            from.AddTransition(to);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResponse.Text($"Transition added: '{fromStateName}' → '{toStateName}' in '{controllerPath}'.");
        }

        /// <summary>
        /// Sets a state as the default state of the base layer.
        /// </summary>
        /// <param name="controllerPath">Path to the controller asset.</param>
        /// <param name="stateName">Name of the state to set as default.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the new default state.</returns>
        private static ToolResponse ExecuteSetDefault(string controllerPath, string stateName)
        {
            if (string.IsNullOrWhiteSpace(controllerPath))
            {
                return ToolResponse.Error("controllerPath is required for action 'set-default'.");
            }

            if (!controllerPath.StartsWith("Assets/"))
            {
                return ToolResponse.Error("controllerPath must start with 'Assets/'.");
            }

            if (string.IsNullOrWhiteSpace(stateName))
            {
                return ToolResponse.Error("stateName is required for action 'set-default'.");
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (controller == null)
            {
                return ToolResponse.Error($"AnimatorController not found at '{controllerPath}'.");
            }

            var stateMachine = controller.layers[0].stateMachine;
            AnimatorState? target = FindState(stateMachine, stateName);

            if (target == null)
            {
                return ToolResponse.Error($"State '{stateName}' not found in the base layer.");
            }

            stateMachine.defaultState = target;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            return ToolResponse.Text($"Default state set to '{stateName}' in '{controllerPath}'.");
        }

        #endregion

        #region CONTROLLER HELPERS

        /// <summary>
        /// Searches an <see cref="AnimatorStateMachine"/> for a state with the given name.
        /// </summary>
        /// <param name="stateMachine">The state machine to search.</param>
        /// <param name="name">The state name to find (case-sensitive).</param>
        /// <returns>The matching <see cref="AnimatorState"/>, or <c>null</c> when not found.</returns>
        private static AnimatorState? FindState(AnimatorStateMachine stateMachine, string name)
        {
            var states = stateMachine.states;

            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == name)
                {
                    return states[i].state;
                }
            }

            return null;
        }

        #endregion
    }
}