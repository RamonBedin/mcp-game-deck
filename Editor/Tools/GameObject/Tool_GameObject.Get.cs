#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns a structured report for the specified GameObject, including transform data,
        /// attached component types, and optionally its immediate children.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target (e.g. "World/Player"). Used when instanceId is 0.</param>
        /// <param name="includeComponents">
        /// When <c>true</c>, the response lists every component type attached to the GameObject. Default <c>true</c>.
        /// </param>
        /// <param name="includeChildren">
        /// When <c>true</c>, the response lists the names and instance IDs of all direct children. Default <c>false</c>.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with name, instance ID, tag, layer, active/static flags,
        /// world-space transform, component types, and (if requested) children list.
        /// Returns an error when the GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-get", Title = "GameObject / Get Info", ReadOnlyHint = true)]
        [Description("Returns detailed information about a GameObject: name, instance ID, tag, layer, " + "active/static state, world transform, and component list. " + "Locate by instanceId or hierarchy path. Optionally includes children.")]
        public ToolResponse GetInfo(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'World/Player'). Used when instanceId is 0.")] string objectPath = "",
            [Description("If true, list all component types attached to the GameObject. Default true.")] bool includeComponents = true,
            [Description("If true, list the name and instance ID of each direct child. Default false.")] bool includeChildren = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                var t = go.transform;
                var sb = new StringBuilder();

                sb.AppendLine($"GameObject: {go.name}");
                sb.AppendLine($"  Instance ID:  {go.GetInstanceID()}");
                sb.AppendLine($"  Tag:          {go.tag}");
                sb.AppendLine($"  Layer:        {go.layer} ({LayerMask.LayerToName(go.layer)})");
                sb.AppendLine($"  Active (self):{go.activeSelf}");
                sb.AppendLine($"  Active (hier):{go.activeInHierarchy}");
                sb.AppendLine($"  Static:       {go.isStatic}");
                sb.AppendLine($"  Scene:        {go.scene.name}");
                sb.AppendLine();
                sb.AppendLine("Transform:");
                sb.AppendLine($"  Position: {t.position}");
                sb.AppendLine($"  Rotation: {t.rotation.eulerAngles}");
                sb.AppendLine($"  Scale:    {t.localScale}");

                if (includeComponents)
                {
                    var components = go.GetComponents<UnityEngine.Component>();
                    sb.AppendLine();
                    sb.AppendLine($"Components ({components.Length}):");

                    for (int i = 0; i < components.Length; i++)
                    {
                        var comp = components[i];

                        if (comp == null)
                        {
                            sb.AppendLine("  [Missing Component]");
                        }
                        else
                        {
                            sb.AppendLine($"  {comp.GetType().Name}");
                        }
                    }
                }

                if (includeChildren)
                {
                    sb.AppendLine();
                    sb.AppendLine($"Children ({t.childCount}):");

                    for (int i = 0; i < t.childCount; i++)
                    {
                        var child = t.GetChild(i);
                        sb.AppendLine($"  [{child.gameObject.GetInstanceID()}] {child.name}");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}