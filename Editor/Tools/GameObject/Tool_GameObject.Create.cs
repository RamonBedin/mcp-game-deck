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
    /// MCP tools for creating, updating, querying, duplicating, deleting, selecting,
    /// and manipulating GameObjects in the Unity scene hierarchy.
    /// Covers creation, property updates, parenting, transform operations, and scene queries.
    /// </summary>
    [McpToolType]
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new GameObject in the scene, optionally as a Unity primitive or an empty object,
        /// and places it at a given world position under an optional parent.
        /// </summary>
        /// <param name="name">Name to assign to the new GameObject.</param>
        /// <param name="primitiveType">
        /// The type of object to create. Accepted values (case-insensitive):
        /// "Empty", "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad". Default "Empty".
        /// </param>
        /// <param name="posX">World-space X position. Default 0.</param>
        /// <param name="posY">World-space Y position. Default 0.</param>
        /// <param name="posZ">World-space Z position. Default 0.</param>
        /// <param name="parentPath">
        /// Hierarchy path of the parent GameObject (e.g. "World/Props").
        /// Leave empty to create at the scene root.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming creation with name, instance ID, and position,
        /// or an error when the primitive type is unrecognised or the parent is not found.
        /// </returns>
        [McpTool("gameobject-create", Title = "GameObject / Create")]
        [Description("Creates a new GameObject in the active scene. Supports empty objects and " + "built-in Unity primitives (Cube, Sphere, Capsule, Cylinder, Plane, Quad). " + "Optionally parents the object and sets its world position.")]
        public ToolResponse Create(
            [Description("Name to assign to the new GameObject.")] string name,
            [Description("Type of object to create: Empty, Cube, Sphere, Capsule, Cylinder, Plane, Quad. Default 'Empty'.")] string primitiveType = "Empty",
            [Description("World-space X position. Default 0.")] float posX = 0f,
            [Description("World-space Y position. Default 0.")] float posY = 0f,
            [Description("World-space Z position. Default 0.")] float posZ = 0f,
            [Description("Hierarchy path of the parent GameObject (e.g. 'World/Props'). Empty for scene root.")] string parentPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return ToolResponse.Error("name is required.");
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

                go.transform.position = new Vector3(posX, posY, posZ);

                if (parent != null)
                {
                    go.transform.SetParent(parent, worldPositionStays: true);
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
                    sb.AppendLine($"  Parent: {parentPath}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}