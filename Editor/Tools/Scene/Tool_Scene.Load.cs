#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Opens a scene in the Editor in Single or Additive mode.
        /// </summary>
        /// <param name="scenePath">Asset path of the scene to open (e.g. "Assets/Scenes/Main.unity").</param>
        /// <param name="mode">
        /// Load mode: "Single" replaces the current scene, "Additive" adds it alongside existing scenes.
        /// Default "Single".
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the loaded scene name and mode,
        /// a notice if the user cancelled the save prompt, or an error if the path is invalid
        /// or the scene fails to open.
        /// </returns>
        [McpTool("scene-load", Title = "Scene / Load")]
        [Description("Opens a scene in the Editor. Prompts to save the current scene if it has unsaved changes.")]
        public ToolResponse Load(
            [Description("Asset path of the scene (e.g. 'Assets/Scenes/Main.unity').")] string scenePath,
            [Description("Load mode: 'Single' replaces current scene, 'Additive' adds to current scenes. Default 'Single'.")] string mode = "Single"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return ToolResponse.Error("scenePath is required.");
                }

                if (!scenePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("scenePath must start with 'Assets/' (e.g. 'Assets/Scenes/Main.unity').");
                }

                if (AssetDatabase.GetMainAssetTypeAtPath(scenePath) == null)
                {
                    return ToolResponse.Error($"No scene asset found at '{scenePath}'.");
                }

                OpenSceneMode openMode = OpenSceneMode.Single;

                if (string.Equals(mode, "Additive", System.StringComparison.OrdinalIgnoreCase))
                {
                    openMode = OpenSceneMode.Additive;
                }

                bool proceed = EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

                if (!proceed)
                {
                    return ToolResponse.Text("Scene load cancelled by the user.");
                }

                var scene = EditorSceneManager.OpenScene(scenePath, openMode);

                if (!scene.IsValid())
                {
                    return ToolResponse.Error($"Failed to open scene at '{scenePath}'. The file may be corrupted or incompatible.");
                }

                return ToolResponse.Text($"Loaded scene '{scene.name}' ({mode}) from {scenePath}.");
            });
        }

        #endregion
    }
}