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
        /// Instantiates a copy of the specified GameObject, optionally applying a world-space
        /// offset and assigning a new name. The new object is placed under the same parent as
        /// the original. The operation is recorded in the Unity Undo stack.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the source GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the source (e.g. "World/Props/Barrel"). Used when instanceId is 0.</param>
        /// <param name="newName">Name to assign to the duplicated object. Defaults to the original name when empty.</param>
        /// <param name="offsetX">World-space X offset applied to the duplicate's position. Default 0.</param>
        /// <param name="offsetY">World-space Y offset applied to the duplicate's position. Default 0.</param>
        /// <param name="offsetZ">World-space Z offset applied to the duplicate's position. Default 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new object's name, instance ID, and world position,
        /// or an error when the source GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-duplicate", Title = "GameObject / Duplicate")]
        [Description("Instantiates a copy of an existing GameObject in the active scene. " + "Optionally applies a world-space position offset and assigns a new name. " + "The duplicate is placed under the same parent as the original. " + "Registers the operation with Undo.")]
        public ToolResponse Duplicate(
            [Description("Unity instance ID of the source GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the source GameObject (e.g. 'World/Props/Barrel'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Name to assign to the duplicated GameObject. Defaults to the original name when empty.")] string newName = "",
            [Description("World-space X offset added to the duplicate's position. Default 0.")] float offsetX = 0f,
            [Description("World-space Y offset added to the duplicate's position. Default 0.")] float offsetY = 0f,
            [Description("World-space Z offset added to the duplicate's position. Default 0.")] float offsetZ = 0f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var source = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (source == null)
                {
                    return ToolResponse.Error($"Source GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var duplicate = Object.Instantiate(source, source.transform.parent);

                duplicate.transform.position = source.transform.position + new Vector3(offsetX, offsetY, offsetZ);
                duplicate.name = string.IsNullOrWhiteSpace(newName) ? source.name : newName;

                Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate GameObject {duplicate.name}");
                Selection.activeGameObject = duplicate;

                var sb = new StringBuilder();
                sb.AppendLine($"Duplicated '{source.name}' → '{duplicate.name}':");
                sb.AppendLine($"  Instance ID:     {duplicate.GetInstanceID()}");
                sb.AppendLine($"  World Position:  {duplicate.transform.position}");

                if (duplicate.transform.parent != null)
                {
                    sb.AppendLine($"  Parent:          {duplicate.transform.parent.name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}