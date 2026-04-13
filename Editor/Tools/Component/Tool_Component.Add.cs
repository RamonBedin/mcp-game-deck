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
    /// MCP tools for adding, removing, listing, inspecting, and updating Components
    /// on GameObjects via SerializedObject. Includes shared helpers for component type
    /// resolution across UnityEngine assemblies.
    /// </summary>
    [McpToolType]
    public partial class Tool_Component
    {
        #region TOOL METHODS

        /// <summary>
        /// Adds a Unity Component of the specified type to a GameObject, with full Undo support.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <param name="componentType">
        /// Simple or fully-qualified type name of the component to add
        /// (e.g. "Rigidbody", "BoxCollider", "AudioSource", "Light").
        /// UnityEngine namespaces are searched automatically.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the added component's type name and instance ID,
        /// or an error if the GameObject is not found, the type cannot be resolved, or the
        /// type is not a <see cref="Component"/>.
        /// </returns>
        [McpTool("component-add", Title = "Component / Add")]
        [Description("Adds a Unity Component to a GameObject by type name. " + "Searches UnityEngine namespaces automatically for common types such as Rigidbody, " + "BoxCollider, AudioSource, and Light. Registers the operation with Undo.")]
        public ToolResponse AddComponent(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Simple or fully-qualified component type name to add " + "(e.g. 'Rigidbody', 'BoxCollider', 'AudioSource', 'Light').")] string componentType = ""
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

                var added = Undo.AddComponent(go, resolvedType);

                if (added == null)
                {
                    return ToolResponse.Error($"Failed to add component '{resolvedType.FullName}' to '{go.name}'. " + "The component may already exist or be restricted to a single instance.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Added component to '{go.name}':");
                sb.AppendLine($"  Type: {added.GetType().FullName}");
                sb.AppendLine($"  Instance ID: {added.GetInstanceID()}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Attempts to resolve a component type by name, searching common Unity namespaces
        /// before falling back to a full assembly scan.
        /// </summary>
        /// <param name="typeName">Simple or fully-qualified type name to resolve.</param>
        /// <returns>
        /// The matching <see cref="System.Type"/> if it is assignable to <see cref="Component"/>;
        /// otherwise <c>null</c>.
        /// </returns>
        private static System.Type? ResolveComponentType(string typeName)
        {
            string[] prefixes = { "UnityEngine.", "UnityEngine.UI.", "UnityEngine.Rendering.", "" };
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            for (int p = 0; p < prefixes.Length; p++)
            {
                string fullName = prefixes[p] + typeName;

                for (int a = 0; a < assemblies.Length; a++)
                {
                    var type = assemblies[a].GetType(fullName, throwOnError: false, ignoreCase: true);

                    if (type != null && typeof(UnityEngine.Component).IsAssignableFrom(type))
                    {
                        return type;
                    }
                }
            }

            return null;
        }

        #endregion
    }
}