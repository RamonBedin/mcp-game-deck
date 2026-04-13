#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Closes and unloads a scene that is currently open in the Editor.
        /// The scene asset on disk is not modified or deleted.
        /// </summary>
        /// <param name="scenePath">Asset path of the scene to unload (e.g. 'Assets/Scenes/Level1.unity').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the scene was closed, or an error if not found or not loaded.</returns>
        [McpTool("scene-unload", Title = "Scene / Unload")]
        [Description("Closes and unloads an open scene from the Editor. " + "The scene asset is not deleted. The scene must currently be open in the Editor.")]
        public ToolResponse Unload(
            [Description("Asset path of the scene to unload (e.g. 'Assets/Scenes/Level1.unity').")] string scenePath
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
                    return ToolResponse.Error("scenePath must start with 'Assets/' (e.g. 'Assets/Scenes/Level.unity').");
                }

                var scene = SceneManager.GetSceneByPath(scenePath);

                if (!scene.IsValid())
                {
                    return ToolResponse.Error($"No open scene found at path '{scenePath}'. " + "Make sure the scene is currently loaded in the Editor.");
                }

                string sceneName = scene.name;

                bool closed = EditorSceneManager.CloseScene(scene, true);

                if (!closed)
                {
                    return ToolResponse.Error($"Failed to close scene '{sceneName}'. " + "It may be the only open scene — at least one scene must remain open.");
                }

                return ToolResponse.Text($"Unloaded scene '{sceneName}' from '{scenePath}'.");
            });
        }

        #endregion
    }
}