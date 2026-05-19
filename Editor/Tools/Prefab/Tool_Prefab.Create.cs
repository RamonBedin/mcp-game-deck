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
        /// Creates a Prefab asset from an existing GameObject in the scene.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the source GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the source GameObject. Used when instanceId is 0.</param>
        /// <param name="savePath">Asset path to save the prefab (e.g. "Assets/Prefabs/Player.prefab"). Default empty: resolves to "Assets/{go.name}.prefab" at the project root.</param>
        /// <param name="keepConnection">Keep prefab connection on the scene object so future edits to the prefab asset propagate. Default true. Set false only when you want a one-shot snapshot with no link back.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the prefab name and saved path,
        /// or an error if the GameObject is not found, the path is invalid, or creation fails.
        /// </returns>
        [McpTool("prefab-create", Title = "Prefab / Create")]
        [Description("Creates a Prefab asset from a scene GameObject and saves it to the project. " + "One of 'instanceId' or 'objectPath' is required. Prefer 'instanceId' if known from a recent tool call; otherwise use 'objectPath'.")]
        public ToolResponse Create(
            [Description("Instance ID of the source GameObject. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the source GameObject.")] string objectPath = "",
            [Description("Asset path to save the prefab (e.g. 'Assets/Prefabs/Player.prefab'). Default empty: resolves to 'Assets/{go.name}.prefab' at the project root.")] string savePath = "",
            [Description("Keep prefab connection on the scene object so future edits to the prefab asset propagate. Default true. Set false only when you want a one-shot snapshot with no link back.")] bool keepConnection = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                if (string.IsNullOrWhiteSpace(savePath))
                {
                    savePath = $"Assets/{go.name}.prefab";
                }

                if (!savePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("savePath must start with 'Assets/' (e.g. 'Assets/Prefabs/Player.prefab').");
                }

                string folder = System.IO.Path.GetDirectoryName(savePath) ?? "Assets";

                if (!AssetDatabase.IsValidFolder(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                savePath = AssetDatabase.GenerateUniqueAssetPath(savePath);
                GameObject prefab;

                if (keepConnection)
                {
                    prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, savePath, InteractionMode.UserAction);
                }
                else
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
                }

                if (prefab == null)
                {
                    return ToolResponse.Error($"Failed to create prefab at '{savePath}'.");
                }

                return ToolResponse.Text($"Created prefab '{prefab.name}' at {savePath}.");
            });
        }

        #endregion
    }
}