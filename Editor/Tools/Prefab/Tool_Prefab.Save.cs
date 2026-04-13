#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Saves the Prefab that is currently open in Prefab Edit Mode.
        /// Uses PrefabStageUtility to obtain the current prefab stage's root, then calls
        /// PrefabUtility.SavePrefabAsset to write the changes back to the asset on disk.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the save, or an error when no prefab stage
        /// is currently active or the save operation fails.
        /// </returns>
        [McpTool("prefab-save", Title = "Prefab / Save")]
        [Description("Saves the Prefab currently open in Prefab Edit Mode back to disk. " + "No-ops and returns an error when no Prefab Edit Mode stage is active.")]
        public ToolResponse Save()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();

                if (stage == null)
                {
                    return ToolResponse.Error("No Prefab Edit Mode stage is currently active. " + "Open a prefab first using prefab-open.");
                }

                GameObject root = stage.prefabContentsRoot;

                if (root == null)
                {
                    return ToolResponse.Error("The current prefab stage has no root GameObject.");
                }

                bool saved = PrefabUtility.SavePrefabAsset(root);

                if (!saved)
                {
                    return ToolResponse.Error($"PrefabUtility.SavePrefabAsset failed for '{root.name}'.");
                }

                return ToolResponse.Text($"Saved prefab '{root.name}' at '{stage.assetPath}'.");
            });
        }

        #endregion
    }
}