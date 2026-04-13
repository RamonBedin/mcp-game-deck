#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns the current state of the Unity Editor including play mode, compilation, and scene info.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing a formatted multi-line string with Unity version,
        /// build target, play/pause/compile/update flags, and active scene name, path, dirty state,
        /// and root object count.
        /// </returns>
        [McpTool("editor-get-state", Title = "Editor / Get State", ReadOnlyHint = true)]
        [Description("Returns current Editor state: play mode, pause, compiling, current scene, platform, Unity version.")]
        public ToolResponse GetState()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Unity Editor State:");
                sb.AppendLine($"  Unity Version: {Application.unityVersion}");
                sb.AppendLine($"  Platform: {EditorUserBuildSettings.activeBuildTarget}");
                sb.AppendLine($"  isPlaying: {EditorApplication.isPlaying}");
                sb.AppendLine($"  isPaused: {EditorApplication.isPaused}");
                sb.AppendLine($"  isCompiling: {EditorApplication.isCompiling}");
                sb.AppendLine($"  isUpdating: {EditorApplication.isUpdating}");

                var scene = SceneManager.GetActiveScene();
                sb.AppendLine($"  Active Scene: {scene.name} ({scene.path})");
                sb.AppendLine($"  Scene isDirty: {scene.isDirty}");
                sb.AppendLine($"  Scene rootCount: {scene.rootCount}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}