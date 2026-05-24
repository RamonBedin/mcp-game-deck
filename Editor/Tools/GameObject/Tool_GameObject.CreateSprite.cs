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
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new 2D Sprite GameObject in the active scene with a SpriteRenderer pre-configured.
        /// Optionally loads the Sprite asset at spritePath and assigns it to the SpriteRenderer.
        /// Parenting and positioning mirror gameobject-create; worldPositionStays controls whether posX/Y/Z are world- or local-space after parenting.
        /// The operation is recorded in the Unity Undo stack and the new GameObject is selected.
        /// </summary>
        /// <param name="name">Name to assign to the new Sprite GameObject.</param>
        /// <param name="posX">X position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="posY">Y position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="posZ">Z position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="parentInstanceId">Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.</param>
        /// <param name="parentPath">Hierarchy path of the parent (e.g. "World/Enemies"). Used when parentInstanceId is 0. Both empty/0 = scene root.</param>
        /// <param name="spritePath">Asset path of the Sprite to assign (e.g. "Assets/Art/Player.png"). Empty = create with no sprite assigned.</param>
        /// <param name="sortingLayer">Sorting layer name on the SpriteRenderer. Must exist under Project Settings > Tags and Layers > Sorting Layers. Default "Default".</param>
        /// <param name="orderInLayer">Order in layer (z-order within the sorting layer). Default 0.</param>
        /// <param name="worldPositionStays">When true (default), posX/Y/Z are world-space and preserved after parenting. When false, they are local-to-parent.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> describing the new GameObject (instance ID, position, sprite, sorting layer, order, parent),
        /// or an error when name is empty, the parent cannot be located, the sprite asset is missing, or the sorting layer is undefined.
        /// </returns>
        [McpTool("gameobject-create-sprite", Title = "GameObject / Create Sprite")]
        [Description("Creates a new 2D Sprite GameObject in the active scene with a SpriteRenderer pre-configured. " + "Loads the Sprite asset from spritePath (when non-empty) and assigns it to the SpriteRenderer. " + "Mirrors gameobject-create's parenting and positioning behavior. " + "For 3D primitives or empty GameObjects use gameobject-create. " + "Registers the operation with Undo and selects the new object.")]
        public ToolResponse CreateSprite(
            [Description("Name to assign to the new Sprite GameObject.")] string name,
            [Description("X position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posX = 0f,
            [Description("Y position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posY = 0f,
            [Description("Z position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.")] float posZ = 0f,
            [Description("Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.")] int parentInstanceId = 0,
            [Description("Hierarchy path of the parent GameObject (e.g. 'World/Enemies'). Used when parentInstanceId is 0. Both empty/0 = scene root.")] string parentPath = "",
            [Description("Asset path of the Sprite to assign (e.g. 'Assets/Art/Player.png'). Empty = create with no sprite assigned.")] string spritePath = "",
            [Description("Sorting layer name on the SpriteRenderer (must exist in Tags & Layers / Sorting Layers). Default 'Default'.")] string sortingLayer = "Default",
            [Description("Order in layer (z-order within the sorting layer). Default 0.")] int orderInLayer = 0,
            [Description("When true (default), posX/Y/Z are world-space and preserved after parenting. When false, they are local-to-parent. Default true.")] bool worldPositionStays = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ToolResponse.Error("name is required.");
                }

                Transform? parent = null;

                if (parentInstanceId != 0 || !string.IsNullOrWhiteSpace(parentPath))
                {
                    var parentGo = Tool_Transform.FindGameObject(parentInstanceId, parentPath);

                    if (parentGo == null)
                    {
                        return ToolResponse.Error($"Parent GameObject not found. parentInstanceId={parentInstanceId}, parentPath='{parentPath}'.");
                    }

                    parent = parentGo.transform;
                }

                Sprite? sprite = null;

                if (!string.IsNullOrWhiteSpace(spritePath))
                {
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

                    if (sprite == null)
                    {
                        return ToolResponse.Error($"Sprite asset not found at '{spritePath}'. Ensure the path is correct and the asset is imported as a Sprite.");
                    }
                }

                var layers = SortingLayer.layers;
                bool layerFound = false;

                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].name == sortingLayer)
                    {
                        layerFound = true;
                        break;
                    }
                }

                if (!layerFound)
                {
                    return ToolResponse.Error($"Sorting layer '{sortingLayer}' is not defined. Add it under Project Settings > Tags and Layers > Sorting Layers.");
                }

                var go = new GameObject(name);
                var renderer = go.AddComponent<SpriteRenderer>();

                if (sprite != null)
                {
                    renderer.sprite = sprite;
                }

                renderer.sortingLayerName = sortingLayer;
                renderer.sortingOrder = orderInLayer;

                if (worldPositionStays)
                {
                    go.transform.position = new Vector3(posX, posY, posZ);
                }

                if (parent != null)
                {
                    go.transform.SetParent(parent, worldPositionStays);
                }

                if (!worldPositionStays)
                {
                    go.transform.localPosition = new Vector3(posX, posY, posZ);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create Sprite {name}");
                Selection.activeGameObject = go;

                var sb = new StringBuilder();
                sb.AppendLine($"Created Sprite GameObject '{go.name}':");
                sb.AppendLine($"  Instance ID:    {go.GetInstanceID()}");
                sb.AppendLine($"  Position:       ({posX}, {posY}, {posZ})");
                sb.AppendLine($"  Sprite:         {(sprite != null ? spritePath : "(none)")}");
                sb.AppendLine($"  Sorting Layer:  {sortingLayer}");
                sb.AppendLine($"  Order in Layer: {orderInLayer}");

                if (parent != null)
                {
                    sb.AppendLine($"  Parent:         {parent.name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}