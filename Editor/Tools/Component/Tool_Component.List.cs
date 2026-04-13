#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Component
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns all Components attached to the specified GameObject, including their type names,
        /// instance IDs, and enabled status for <see cref="Behaviour"/>-derived components.
        /// This is a read-only operation.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Parent/Child"). Used when instanceId is 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each component's index, type name, instance ID, and
        /// enabled state (where applicable), or an error if the GameObject cannot be located.
        /// </returns>
        [McpTool("component-list", Title = "Component / List", ReadOnlyHint = true)]
        [Description("Lists all Components attached to a GameObject with their type name, instance ID, " + "and enabled status for Behaviour-derived components (e.g. MonoBehaviour, Renderer, Collider). " + "This is a read-only operation.")]
        public ToolResponse ListComponents(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Parent/Child'). Used when instanceId is 0.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var components = go.GetComponents<UnityEngine.Component>();
                var sb = new StringBuilder();

                sb.AppendLine($"Components on '{go.name}' (instanceId: {go.GetInstanceID()}) — {components.Length} total:");

                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];

                    if (comp == null)
                    {
                        sb.AppendLine($"  [{i}] [Missing Component]");
                        continue;
                    }

                    string typeName = comp.GetType().FullName ?? comp.GetType().Name;
                    int compId = comp.GetInstanceID();
                    var behaviour = comp as Behaviour;

                    if (behaviour != null)
                    {
                        sb.AppendLine($"  [{i}] {typeName}  (instanceId: {compId},  enabled: {behaviour.enabled})");
                    }
                    else
                    {
                        sb.AppendLine($"  [{i}] {typeName}  (instanceId: {compId})");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}