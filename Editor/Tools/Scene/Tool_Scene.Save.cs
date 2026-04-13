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
        /// Saves the current active scene, or a specific scene by path.
        /// </summary>
        /// <param name="scenePath">
        /// Asset path of the scene to save (e.g. "Assets/Scenes/Level.unity").
        /// Leave empty to save the currently active scene.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the scene name and saved path,
        /// or an error if the specified scene is not loaded or the save operation fails.
        /// </returns>
        [McpTool("scene-save", Title = "Scene / Save")]
        [Description("Saves the current active scene or a specific scene by path.")]
        public ToolResponse Save([Description("Optional scene path to save. If empty, saves the active scene.")] string scenePath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Scene scene;

                if (!string.IsNullOrWhiteSpace(scenePath))
                {
                    if (!scenePath.StartsWith("Assets/"))
                    {
                        return ToolResponse.Error("scenePath must start with 'Assets/' (e.g. 'Assets/Scenes/Level.unity').");
                    }

                    scene = SceneManager.GetSceneByPath(scenePath);

                    if (!scene.IsValid())
                    {
                        return ToolResponse.Error($"Scene not found at '{scenePath}'.");
                    }
                }
                else
                {
                    scene = SceneManager.GetActiveScene();
                }

                bool saved = EditorSceneManager.SaveScene(scene);
                return saved ? ToolResponse.Text($"Saved scene '{scene.name}' at {scene.path}.") : ToolResponse.Error($"Failed to save scene '{scene.name}'.");
            });
        }

        #endregion
    }
}