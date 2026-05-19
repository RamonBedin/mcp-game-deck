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
    /// MCP tools for creating, updating, querying, duplicating, deleting,
    /// and manipulating GameObjects in the Unity scene hierarchy.
    /// Covers creation (3D primitives and 2D sprites), property updates, parenting,
    /// sibling-index reordering, transform operations, and scene queries.
    /// </summary>
    [McpToolType]
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new GameObject in the scene, optionally as a Unity primitive or an empty object,
        /// and places it at a given position under an optional parent. Optionally sets initial tag,
        /// layer, active state, and static flag in a single call.
        /// </summary>
        /// <param name="name">Name to assign to the new GameObject.</param>
        /// <param name="primitiveType">
        /// The type of object to create. Accepted values (case-insensitive):
        /// "Empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad". Default "Empty".
        /// </param>
        /// <param name="posX">X position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="posY">Y position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="posZ">Z position (world-space when worldPositionStays=true; local-to-parent otherwise). Default 0.</param>
        /// <param name="parentInstanceId">Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.</param>
        /// <param name="parentPath">
        /// Hierarchy path of the parent GameObject (e.g. "World/Props"). Used when parentInstanceId is 0.
        /// Both empty/0 = scene root.
        /// </param>
        /// <param name="tag">Initial tag (must exist in Tag Manager). Empty = use Unity default ('Untagged').</param>
        /// <param name="layer">Initial layer index (0–31). Pass -1 to use Unity default (0 = Default).</param>
        /// <param name="isActive">Initial active state: "true", "false", or empty = use Unity default (active).</param>
        /// <param name="isStatic">Initial static flag: "true", "false", or empty = use Unity default (not static).</param>
        /// <param name="worldPositionStays">When true (default), posX/Y/Z are interpreted as world-space and preserved after parenting. When false, they are interpreted as local-to-parent.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming creation with name, instance ID, and position,
        /// or an error when the primitive type is unrecognised or the parent is not found.
        /// </returns>
        [McpTool("gameobject-create", Title = "GameObject / Create")]
        [Description("Creates a new GameObject in the active scene. Supports empty objects and built-in Unity primitives (Cube, Sphere, Capsule, Cylinder, Plane, Quad). " + "Optionally parents the object, sets its world position, and assigns initial tag, layer, active state, and static flag in a single call. " + "For 2D sprite GameObjects use gameobject-create-sprite. Registers the operation with Undo and selects the new object.")]
        public ToolResponse Create(
            [Description("Name to assign to the new GameObject.")] string name,
            [Description("Type of object to create (case-insensitive): Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
            [Description("X position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posX = 0f,
            [Description("Y position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posY = 0f,
            [Description("Z position (world-space when worldPositionStays=true; local-to-parent when worldPositionStays=false). Default 0.")] float posZ = 0f,
            [Description("Unity instance ID of the parent GameObject. Pass 0 to use parentPath. Both empty/0 = scene root.")] int parentInstanceId = 0,
            [Description("Hierarchy path of the parent GameObject (e.g. 'World/Props'). Used when parentInstanceId is 0. Both empty/0 = scene root.")] string parentPath = "",
            [Description("Initial tag (must exist in Tag Manager). Empty = use Unity default ('Untagged').")] string tag = "",
            [Description("Initial layer index (0–31). Pass -1 to use Unity default (0 = Default).")] int layer = -1,
            [Description("Initial active state: 'true', 'false', or empty = use Unity default (active).")] string isActive = "",
            [Description("Initial static flag: 'true', 'false', or empty = use Unity default (not static).")] string isStatic = "",
            [Description("When true (default), posX/Y/Z are interpreted as world-space and preserved after parenting. When false, they are interpreted as local-to-parent. Default true.")] bool worldPositionStays = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ToolResponse.Error("name is required.");
                }

                if (layer != -1 && (layer < 0 || layer > 31))
                {
                    return ToolResponse.Error($"Layer {layer} out of range; valid range is 0–31.");
                }

                bool applyActive;
                bool activeValue = false;
                bool applyStatic;
                bool staticValue = false;
                try
                {
                    applyActive = TryParseNullableBool(isActive, out activeValue);
                    applyStatic = TryParseNullableBool(isStatic, out staticValue);
                }
                catch (System.ArgumentException ex)
                {
                    return ToolResponse.Error(ex.Message);
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

                GameObject go;
                var typeKey = primitiveType.Trim().ToLowerInvariant();

                switch (typeKey)
                {
                    case "empty":
                    case "":
                        go = new GameObject(name);
                        break;

                    case "cube":
                        go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        go.name = name;
                        break;

                    case "sphere":
                        go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        go.name = name;
                        break;

                    case "capsule":
                        go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                        go.name = name;
                        break;

                    case "cylinder":
                        go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                        go.name = name;
                        break;

                    case "plane":
                        go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                        go.name = name;
                        break;

                    case "quad":
                        go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                        go.name = name;
                        break;

                    default:
                        return ToolResponse.Error($"Unknown primitiveType '{primitiveType}'. " + "Valid values: Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad.");
                }

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

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    try
                    {
                        go.tag = tag;
                    }
                    catch (UnityException)
                    {
                        Object.DestroyImmediate(go);
                        return ToolResponse.Error($"Tag '{tag}' is not defined in Tag Manager.");
                    }
                }

                if (layer >= 0 && layer <= 31)
                {
                    go.layer = layer;
                }

                if (applyActive)
                {
                    go.SetActive(activeValue);
                }

                if (applyStatic)
                {
                    go.isStatic = staticValue;
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create GameObject {name}");
                Selection.activeGameObject = go;

                var sb = new StringBuilder();
                sb.AppendLine($"Created GameObject '{go.name}':");
                sb.AppendLine($"  Instance ID: {go.GetInstanceID()}");
                sb.AppendLine($"  Type: {(typeKey == "empty" || typeKey == "" ? "Empty" : primitiveType)}");
                sb.AppendLine($"  Position: ({posX}, {posY}, {posZ})");

                if (parent != null)
                {
                    sb.AppendLine($"  Parent: {parent.name}");
                }

                if (!string.IsNullOrWhiteSpace(tag))
                {
                    sb.AppendLine($"  Tag: {tag}");
                }

                if (layer >= 0 && layer <= 31)
                {
                    sb.AppendLine($"  Layer: {layer} ({LayerMask.LayerToName(layer)})");
                }

                if (applyActive)
                {
                    sb.AppendLine($"  Active: {activeValue}");
                }

                if (applyStatic)
                {
                    sb.AppendLine($"  Static: {staticValue}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Parses the project-wide string-sentinel convention for nullable booleans.
        /// Returns true with <paramref name="value"/> set when the input is "true"/"false" (case-insensitive).
        /// Returns false when the input is empty/whitespace ("leave unchanged").
        /// Throws ArgumentException on any other input — caller should catch and return ToolResponse.Error.
        /// </summary>
        /// <param name="raw">String input from the MCP transport.</param>
        /// <param name="value">Parsed boolean value when the method returns true.</param>
        /// <returns>True if the caller should apply <paramref name="value"/>; false to leave unchanged.</returns>
        private static bool TryParseNullableBool(string raw, out bool value)
        {
            value = false;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string normalized = raw.Trim().ToLowerInvariant();

            if (normalized == "true")
            {
                value = true;
                return true;
            }

            if (normalized == "false")
            {
                value = false;
                return true;
            }

            throw new System.ArgumentException($"Invalid boolean sentinel '{raw}'. Use 'true', 'false', or empty.");
        }

        #endregion
    }
}