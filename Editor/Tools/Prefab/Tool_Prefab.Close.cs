#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.SceneManagement;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for creating, opening, editing, saving, instantiating, and closing Unity Prefabs.
    /// Covers prefab asset creation, prefab stage management, content modification, and scene instantiation.
    /// </summary>
    [McpToolType]
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Closes the current Prefab editing stage and returns to the main scene.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the stage was closed,
        /// or a text notice if no Prefab stage is currently open.
        /// </returns>
        [McpTool("prefab-close", Title = "Prefab / Close")]
        [Description("Closes the current Prefab editing stage and returns to the main scene.")]
        public ToolResponse Close()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();

                if (stage == null)
                {
                    return ToolResponse.Text("No Prefab stage is currently open.");
                }

                StageUtility.GoToMainStage();
                return ToolResponse.Text("Closed Prefab stage, returned to main scene.");
            });
        }

        #endregion
    }
}