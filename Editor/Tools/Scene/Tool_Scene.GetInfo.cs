#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns information about a specific open scene or the currently active scene.
        /// Reports the scene name, path, dirty state, root GameObject count, build index, and load state.
        /// </summary>
        /// <param name="scenePath">
        /// Asset path of the scene to inspect (e.g. 'Assets/Scenes/Main.unity').
        /// Leave empty to use the active scene.
        /// </param>
        /// <returns>A <see cref="ToolResponse"/> with the scene's metadata, or an error if not found.</returns>
        [McpTool("scene-get-info", Title = "Scene / Get Info", ReadOnlyHint = true)]
        [Description("Returns metadata for an open scene: name, path, dirty flag, root GameObject count, " + "build index, and load state. Defaults to the active scene when scenePath is empty.")]
        public ToolResponse GetInfo(
            [Description("Asset path of the scene to inspect (e.g. 'Assets/Scenes/Main.unity'). " + "Leave empty to use the currently active scene.")] string scenePath = ""
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
                        return ToolResponse.Error($"No open scene found at path '{scenePath}'. " + "The scene must be currently loaded in the Editor.");
                    }
                }
                else
                {
                    scene = SceneManager.GetActiveScene();

                    if (!scene.IsValid())
                    {
                        return ToolResponse.Error("No active scene is currently open.");
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Scene: {scene.name}");
                sb.AppendLine($"  Path: {(string.IsNullOrEmpty(scene.path) ? "(unsaved)" : scene.path)}");
                sb.AppendLine($"  Is Dirty: {scene.isDirty}");
                sb.AppendLine($"  Is Loaded: {scene.isLoaded}");
                sb.AppendLine($"  Build Index: {scene.buildIndex}");
                sb.AppendLine($"  Root Object Count: {scene.rootCount}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}