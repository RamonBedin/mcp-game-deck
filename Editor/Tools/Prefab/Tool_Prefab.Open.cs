#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Opens a Prefab asset in Unity's Prefab Edit Mode via AssetDatabase.OpenAsset,
        /// allowing the user to inspect and modify its hierarchy in isolation.
        /// </summary>
        /// <param name="prefabPath">Asset path of the prefab to open (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the prefab was opened, or an error.</returns>
        [McpTool("prefab-open", Title = "Prefab / Open")]
        [Description("Opens a Prefab asset in Prefab Edit Mode so its hierarchy can be inspected and modified in isolation.")]
        public ToolResponse Open(
            [Description("Asset path of the prefab to open in edit mode (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    return ToolResponse.Error("prefabPath is required.");
                }

                if (!prefabPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("prefabPath must start with 'Assets/' (e.g. 'Assets/Prefabs/Player.prefab').");
                }

                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefabAsset == null)
                {
                    return ToolResponse.Error($"Prefab not found at '{prefabPath}'.");
                }

                bool opened = AssetDatabase.OpenAsset(prefabAsset);

                if (!opened)
                {
                    return ToolResponse.Error($"AssetDatabase.OpenAsset failed for '{prefabPath}'. " + "Ensure the asset is a valid Prefab.");
                }

                return ToolResponse.Text($"Opened prefab '{prefabAsset.name}' in Prefab Edit Mode.");
            });
        }

        #endregion
    }
}