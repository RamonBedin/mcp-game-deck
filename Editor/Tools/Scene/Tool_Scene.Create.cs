#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for Unity scene management.
    /// Covers scene creation, loading, saving, unloading, deletion, hierarchy queries,
    /// scene info retrieval, build settings management, and Scene View framing.
    /// </summary>
    [McpToolType]
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new empty scene and optionally adds it to the Build Settings.
        /// </summary>
        /// <param name="sceneName">Name for the new scene without the .unity extension.</param>
        /// <param name="folderPath">Project folder path to save the scene (e.g. "Assets/Scenes"). Default "Assets/Scenes".</param>
        /// <param name="addToBuildSettings">When true, appends the new scene to the Build Settings list. Default true.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the scene path and whether it was added to Build Settings,
        /// or an error if the name is missing, the path is invalid, or directory creation fails.
        /// </returns>
        [McpTool("scene-create", Title = "Scene / Create")]
        [Description("Creates a new empty scene asset and optionally adds it to Build Settings.")]
        public ToolResponse Create(
            [Description("Name for the new scene (without .unity extension).")] string sceneName,
            [Description("Folder path to save (e.g. 'Assets/Scenes'). Default 'Assets/Scenes'.")] string folderPath = "Assets/Scenes",
            [Description("Whether to add the scene to Build Settings. Default true.")] bool addToBuildSettings = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    return ToolResponse.Error("sceneName is required.");
                }

                if (!folderPath.StartsWith("Assets/") && folderPath != "Assets")
                {
                    return ToolResponse.Error("folderPath must start with 'Assets/' (e.g. 'Assets/Scenes').");
                }

                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(folderPath);
                    }
                    catch (System.Exception ex)
                    {
                        return ToolResponse.Error($"Failed to create directory '{folderPath}': {ex.Message}");
                    }

                    AssetDatabase.Refresh();
                }

                string scenePath = $"{folderPath}/{sceneName}.unity";
                scenePath = AssetDatabase.GenerateUniqueAssetPath(scenePath);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorSceneManager.CloseScene(scene, true);

                if (addToBuildSettings)
                {
                    var scenes = EditorBuildSettings.scenes;
                    var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];

                    for (int i = 0; i < scenes.Length; i++)
                    {
                        newScenes[i] = scenes[i];
                    }

                    newScenes[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
                    EditorBuildSettings.scenes = newScenes;
                }

                return ToolResponse.Text($"Created scene '{sceneName}' at {scenePath}." + (addToBuildSettings ? " Added to Build Settings." : ""));
            });
        }

        #endregion
    }
}