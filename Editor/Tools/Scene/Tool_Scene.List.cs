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
        /// Enumerates all scenes currently open in the Editor and reports each scene's
        /// name, asset path, load state, and dirty flag.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with a formatted list of all open scenes, or a message
        /// when no scenes are open.
        /// </returns>
        [McpTool("scene-list", Title = "Scene / List", ReadOnlyHint = true)]
        [Description("Lists all scenes currently open in the Editor. " + "Returns each scene's name, asset path, load state, and dirty flag.")]
        public ToolResponse List()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                int count = SceneManager.sceneCount;

                if (count == 0)
                {
                    return ToolResponse.Text("No scenes are currently open in the Editor.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Open Scenes ({count}):");

                for (int i = 0; i < count; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    sb.AppendLine($"  [{i}] {scene.name}");
                    sb.AppendLine($"       Path: {(string.IsNullOrEmpty(scene.path) ? "(unsaved)" : scene.path)}");
                    sb.AppendLine($"       Is Loaded: {scene.isLoaded}");
                    sb.AppendLine($"       Is Dirty: {scene.isDirty}");
                    sb.AppendLine($"       Build Index: {scene.buildIndex}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}