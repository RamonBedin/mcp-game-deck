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
        /// Instantiates a prefab or non-prefab GameObject asset into the active scene at a specified
        /// position and rotation. Uses PrefabUtility.InstantiatePrefab when the asset is a prefab
        /// (preserving prefab connection) and Object.Instantiate otherwise.
        /// </summary>
        /// <param name="prefabPath">Asset path of the GameObject asset (e.g. 'Assets/Prefabs/Enemy.prefab' or 'Assets/Models/Tree.fbx').</param>
        /// <param name="name">Override name for the new instance. Keeps the asset's name when empty.</param>
        /// <param name="posX">World-space X position of the new instance. Default 0.</param>
        /// <param name="posY">World-space Y position of the new instance. Default 0.</param>
        /// <param name="posZ">World-space Z position of the new instance. Default 0.</param>
        /// <param name="rotX">World-space X rotation (Euler degrees). Default 0.</param>
        /// <param name="rotY">World-space Y rotation (Euler degrees). Default 0.</param>
        /// <param name="rotZ">World-space Z rotation (Euler degrees). Default 0.</param>
        /// <param name="parentPath">Top-level name or full hierarchy path of the parent GameObject (resolved via GameObject.Find). Empty places the instance at scene root.</param>
        /// <returns>A <see cref="ToolResponse"/> with the new instance's name and ID, or an error.</returns>
        [McpTool("prefab-instantiate", Title = "Prefab / Instantiate")]
        [Description("Instantiates a prefab or other GameObject asset (FBX, model, plain .asset GameObject) into the active scene at a specified position and rotation. " + "When the asset is a prefab, creates a linked instance via PrefabUtility.InstantiatePrefab; otherwise falls back to Object.Instantiate. " + "Supports world position, world rotation (Euler), optional name override, and an optional parent GameObject (top-level name or hierarchy path). " + "To enumerate or search for prefabs by path/filter, use 'asset-find' with t:Prefab.")]
        public ToolResponse Instantiate(
            [Description("Asset path of the GameObject asset to instantiate (e.g. 'Assets/Prefabs/Enemy.prefab' or 'Assets/Models/Tree.fbx').")] string prefabPath,
            [Description("Name for the new instance. Leave empty to keep the asset's original name.")] string name = "",
            [Description("World-space X position. Default 0.")] float posX = 0f,
            [Description("World-space Y position. Default 0.")] float posY = 0f,
            [Description("World-space Z position. Default 0.")] float posZ = 0f,
            [Description("World-space X rotation in degrees (Euler). Default 0.")] float rotX = 0f,
            [Description("World-space Y rotation in degrees (Euler). Default 0.")] float rotY = 0f,
            [Description("World-space Z rotation in degrees (Euler). Default 0.")] float rotZ = 0f,
            [Description("Parent GameObject — top-level name or full hierarchy path (e.g. 'World/Enemies'). Resolves via GameObject.Find. Empty for scene root.")] string parentPath = ""
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

                GameObject? instance;
                PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(prefabAsset);

                if (assetType != PrefabAssetType.NotAPrefab)
                {
                    instance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;

                    if (instance == null)
                    {
                        return ToolResponse.Error("PrefabUtility.InstantiatePrefab returned null.");
                    }
                }
                else
                {
                    instance = Object.Instantiate(prefabAsset, parent);

                    if (instance == null)
                    {
                        return ToolResponse.Error("Object.Instantiate returned null for the non-prefab asset.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    instance.name = name;
                }

                var position = new Vector3(posX, posY, posZ);
                var rotation = Quaternion.Euler(rotX, rotY, rotZ);
                instance.transform.SetPositionAndRotation(position, rotation);

                Undo.RegisterCreatedObjectUndo(instance, $"Instantiate Prefab {instance.name}");
                Selection.activeGameObject = instance;

                return ToolResponse.Text($"Instantiated '{prefabAsset.name}' as '{instance.name}' " + $"(ID: {instance.GetInstanceID()}) at position ({posX}, {posY}, {posZ}) rotation ({rotX}, {rotY}, {rotZ})." + (parent != null ? $" Parent: '{parentPath}'." : ""));
            });
        }

        #endregion
    }
}