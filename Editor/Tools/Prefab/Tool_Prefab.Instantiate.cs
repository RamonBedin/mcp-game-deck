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
        /// Loads a Prefab asset and instantiates it into the active scene as a linked prefab instance.
        /// Supports setting an initial world position, custom name, and optional parent object.
        /// </summary>
        /// <param name="prefabPath">Asset path of the prefab to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab').</param>
        /// <param name="name">Override name for the new instance. Keeps the prefab's name when empty.</param>
        /// <param name="posX">World-space X position of the new instance. Default 0.</param>
        /// <param name="posY">World-space Y position of the new instance. Default 0.</param>
        /// <param name="posZ">World-space Z position of the new instance. Default 0.</param>
        /// <param name="parentPath">Hierarchy path of the parent GameObject. Empty places instance at scene root.</param>
        /// <returns>A <see cref="ToolResponse"/> with the new instance's name and ID, or an error.</returns>
        [McpTool("prefab-instantiate", Title = "Prefab / Instantiate")]
        [Description("Loads a Prefab asset and instantiates it into the active scene as a linked prefab instance. " + "Supports world position, optional name override, and an optional parent GameObject.")]
        public ToolResponse Instantiate(
            [Description("Asset path of the prefab to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab').")] string prefabPath,
            [Description("Name for the new instance. Leave empty to keep the prefab's original name.")] string name = "",
            [Description("World-space X position. Default 0.")] float posX = 0f,
            [Description("World-space Y position. Default 0.")] float posY = 0f,
            [Description("World-space Z position. Default 0.")] float posZ = 0f,
            [Description("Hierarchy path of the parent GameObject (e.g. 'World/Enemies'). Empty for scene root.")] string parentPath = ""
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

                Transform? parent = null;

                if (!string.IsNullOrWhiteSpace(parentPath))
                {
                    var parentGo = GameObject.Find(parentPath);

                    if (parentGo == null)
                    {
                        return ToolResponse.Error($"Parent GameObject not found at path '{parentPath}'.");
                    }

                    parent = parentGo.transform;
                }

                var instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;

                if (instance == null)
                {
                    return ToolResponse.Error("PrefabUtility.InstantiatePrefab returned null.");
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    instance.name = name;
                }

                instance.transform.position = new Vector3(posX, posY, posZ);

                Undo.RegisterCreatedObjectUndo(instance, $"Instantiate Prefab {instance.name}");
                Selection.activeGameObject = instance;

                return ToolResponse.Text($"Instantiated prefab '{prefabAsset.name}' as '{instance.name}' " + $"(ID: {instance.GetInstanceID()}) at ({posX}, {posY}, {posZ})." + (parent != null ? $" Parent: '{parentPath}'." : ""));
            });
        }

        #endregion
    }
}