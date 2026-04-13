#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Component
    {
        #region TOOL METHODS

        /// <summary>
        /// Removes the first instance of the specified Component type from a GameObject.
        /// The operation is recorded in the Unity Undo stack so it can be reversed with Ctrl+Z.
        /// Built-in components that cannot be removed (e.g. Transform) will produce an error
        /// through Unity's normal component removal rules.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="componentType">
        /// Simple or fully-qualified type name of the component to remove
        /// (e.g. "Rigidbody", "BoxCollider", "AudioSource").
        /// UnityEngine namespaces are searched automatically.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the removed component's type and the owning GameObject's name,
        /// or an error if the GameObject, component type, or component instance cannot be located.
        /// </returns>
        [McpTool("component-remove", Title = "Component / Remove")]
        [Description("Removes the first instance of a Component type from a GameObject. " + "The removal is registered with Undo so it can be reversed with Ctrl+Z. " + "Transform and other required components cannot be removed and will raise an error.")]
        public ToolResponse RemoveComponent(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Simple or fully-qualified component type name to remove " + "(e.g. 'Rigidbody', 'BoxCollider', 'AudioSource').")] string componentType = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(componentType))
                {
                    return ToolResponse.Error("componentType is required.");
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var resolvedType = ResolveComponentType(componentType);

                if (resolvedType == null)
                {
                    return ToolResponse.Error($"Could not resolve component type '{componentType}'. " + "Ensure the type name is correct and the assembly is loaded.");
                }

                var component = go.GetComponent(resolvedType);

                if (component == null)
                {
                    return ToolResponse.Error($"Component '{resolvedType.FullName}' not found on '{go.name}'.");
                }

                string typeName = component.GetType().FullName ?? resolvedType.FullName ?? componentType;
                int removedId = component.GetInstanceID();

                Undo.DestroyObjectImmediate(component);

                return ToolResponse.Text($"Removed component '{typeName}' (instanceId: {removedId}) from '{go.name}'.");
            });
        }

        #endregion
    }
}