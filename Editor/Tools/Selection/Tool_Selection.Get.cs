#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for reading and setting the Unity Editor selection.
    /// Covers querying the active GameObject, all selected GameObjects and asset GUIDs,
    /// and programmatically setting the selection by instance ID or hierarchy path.
    /// </summary>
    [McpToolType]
    public partial class Tool_Selection
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns information about the objects currently selected in the Unity Editor,
        /// including the active GameObject, all selected GameObjects, and all selected asset GUIDs.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the active GameObject name and instance ID,
        /// the full list of selected GameObjects (name + instance ID), and the list of
        /// selected asset paths resolved from their GUIDs.
        /// </returns>
        [McpTool("selection-get", Title = "Selection / Get", ReadOnlyHint = true)]
        [Description("Returns the current Editor selection: active GameObject (name + instanceId), " + "all selected GameObjects (name + instanceId list), and all selected asset paths " + "(resolved from GUIDs). Returns empty sections when nothing is selected.")]
        public ToolResponse GetSelection()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                var active = Selection.activeGameObject;

                if (active == null)
                {
                    sb.AppendLine("Active GameObject: (none)");
                }
                else
                {
                    sb.AppendLine($"Active GameObject: {active.name} (instanceId: {active.GetInstanceID()})");
                }

                var gameObjects = Selection.gameObjects;

                if (gameObjects == null || gameObjects.Length == 0)
                {
                    sb.AppendLine("Selected GameObjects: (none)");
                }
                else
                {
                    sb.AppendLine($"Selected GameObjects ({gameObjects.Length}):");

                    for (int i = 0; i < gameObjects.Length; i++)
                    {
                        var go = gameObjects[i];

                        if (go == null)
                        {
                            continue;
                        }

                        sb.AppendLine($"  {go.name} (instanceId: {go.GetInstanceID()})");
                    }
                }

                var guids = Selection.assetGUIDs;

                if (guids == null || guids.Length == 0)
                {
                    sb.AppendLine("Selected Assets: (none)");
                }
                else
                {
                    sb.AppendLine($"Selected Assets ({guids.Length}):");

                    for (int i = 0; i < guids.Length; i++)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                        sb.AppendLine($"  {(string.IsNullOrEmpty(path) ? guids[i] : path)}");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}