#nullable enable
using System.ComponentModel;
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
        /// Changes the parent of a GameObject in the scene hierarchy.
        /// Pass empty parentInstanceId (0) and empty parentPath to unparent the object (move to scene root).
        /// The operation is recorded in the Unity Undo stack so it can be reversed with Ctrl+Z.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the child GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the child (e.g. "OldParent/Child"). Used when instanceId is 0.</param>
        /// <param name="parentInstanceId">Unity instance ID of the new parent. Pass 0 to use parentPath or to unparent.</param>
        /// <param name="parentPath">Hierarchy path of the new parent (e.g. "World/Props"). Leave empty to unparent to scene root.</param>
        /// <param name="worldPositionStays">When true, the child keeps its world-space position. Default true.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new parent relationship,
        /// or an error when either GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-set-parent", Title = "GameObject / Set Parent")]
        [Description("Changes the parent of a GameObject in the scene hierarchy. " + "Leave parentInstanceId=0 and parentPath empty to unparent the object to the scene root. " + "Registers the operation with Undo. " + "worldPositionStays controls whether the world position is preserved after reparenting.")]
        public ToolResponse SetParent(
            [Description("Unity instance ID of the child GameObject to reparent. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the child GameObject (e.g. 'OldParent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Unity instance ID of the new parent GameObject. Pass 0 to use parentPath or to unparent.")] int parentInstanceId = 0,
            [Description("Hierarchy path of the new parent (e.g. 'World/Props'). Leave empty to move to scene root.")] string parentPath = "",
            [Description("When true, the child retains its world-space position after reparenting. Default true.")] bool worldPositionStays = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var child = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (child == null)
                {
                    return ToolResponse.Error($"Child GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                Transform? newParent = null;
                bool hasParentRequest = parentInstanceId != 0 || !string.IsNullOrWhiteSpace(parentPath);

                if (hasParentRequest)
                {
                    var parentGo = Tool_Transform.FindGameObject(parentInstanceId, parentPath);

                    if (parentGo == null)
                    {
                        return ToolResponse.Error($"Parent GameObject not found. parentInstanceId={parentInstanceId}, parentPath='{parentPath}'.");
                    }

                    if (parentGo == child)
                    {
                        return ToolResponse.Error("Cannot parent a GameObject to itself.");
                    }

                    newParent = parentGo.transform;
                }

                Undo.SetTransformParent(child.transform, newParent, worldPositionStays, $"Set Parent {child.name}");

                string parentDesc = newParent != null ? $"'{newParent.name}'" : "scene root";
                return ToolResponse.Text($"Reparented '{child.name}' (instanceId: {child.GetInstanceID()}) → {parentDesc}.\n" + $"  World Position: {child.transform.position}\n" + $"  WorldPositionStays: {worldPositionStays}");
            });
        }

        #endregion
    }
}