#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tool that instantiates a prefab or model asset into the current scene at a specified
    /// world position and rotation, with optional parenting and custom naming.
    /// </summary>
    [McpToolType]
    public partial class Tool_AddAssetToScene
    {
        #region TOOL METHODS

        /// <summary>
        /// Instantiates a prefab or model asset into the current scene at the specified position and rotation.
        /// </summary>
        /// <param name="assetPath">Asset path of the prefab or model (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <param name="posX">X position in world space. Default 0.</param>
        /// <param name="posY">Y position in world space. Default 0.</param>
        /// <param name="posZ">Z position in world space. Default 0.</param>
        /// <param name="rotY">Y rotation in degrees (Euler). Default 0.</param>
        /// <param name="name">Optional name for the instantiated GameObject. If empty, uses the prefab name.</param>
        /// <param name="parentName">Optional parent GameObject name to parent the new object under.</param>
        /// <returns>A <see cref="ToolResponse"/> with details of the created GameObject, or an error.</returns>
        [McpTool("add-asset-to-scene", Title = "Scene / Add Asset to Scene")]
        [Description("Instantiates a prefab or model asset into the current scene at the specified position " + "and rotation. Returns the created GameObject name and instance ID.")]
        public ToolResponse AddAsset(
            [Description("Asset path of the prefab or model (e.g. 'Assets/Prefabs/Player.prefab').")] string assetPath,
            [Description("X position in world space. Default 0.")] float posX = 0f,
            [Description("Y position in world space. Default 0.")] float posY = 0f,
            [Description("Z position in world space. Default 0.")] float posZ = 0f,
            [Description("Y rotation in degrees (Euler). Default 0.")] float rotY = 0f,
            [Description("Optional name for the instantiated GameObject. If empty, uses the prefab name.")] string name = "",
            [Description("Optional parent GameObject name to parent the new object under.")] string parentName = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"No GameObject asset found at '{assetPath}'.");
                }

                var position = new Vector3(posX, posY, posZ);
                var rotation = Quaternion.Euler(0, rotY, 0);

                GameObject instance;

                if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab)
                {
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    instance.transform.SetPositionAndRotation(position, rotation);
                }
                else
                {
                    instance = Object.Instantiate(asset, position, rotation);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    instance.name = name;
                }

                if (!string.IsNullOrWhiteSpace(parentName))
                {
                    var parent = GameObject.Find(parentName);

                    if (parent != null)
                    {
                        instance.transform.SetParent(parent.transform, true);
                    }
                }

                Undo.RegisterCreatedObjectUndo(instance, $"Add {instance.name} to scene");
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);

                var sb = new StringBuilder();
                sb.AppendLine($"Added '{instance.name}' to scene:");
                sb.AppendLine($"  Asset: {assetPath}");
                sb.AppendLine($"  Instance ID: {instance.GetInstanceID()}");
                sb.AppendLine($"  Position: {instance.transform.position}");
                sb.AppendLine($"  Rotation: {instance.transform.rotation.eulerAngles}");

                if (instance.transform.parent != null)
                {
                    sb.AppendLine($"  Parent: {instance.transform.parent.name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}