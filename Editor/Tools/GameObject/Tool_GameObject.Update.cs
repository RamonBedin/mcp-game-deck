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
        /// Modifies the name, tag, layer, active state, and/or static flag of an existing GameObject.
        /// Only parameters that differ from their sentinel defaults are applied.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target (e.g. "World/Enemies/Goblin"). Used when instanceId is 0.</param>
        /// <param name="name">New name to assign. Empty string to leave unchanged.</param>
        /// <param name="tag">New tag to assign (must exist in Tag Manager). Empty string to leave unchanged.</param>
        /// <param name="layer">New layer index (0–31). Pass -1 to leave unchanged.</param>
        /// <param name="isActive">
        /// Active state: "true" = activate, "false" = deactivate, "" (empty) = unchanged.
        /// </param>
        /// <param name="isStatic">
        /// Static flag: "true" = mark static, "false" = clear static, "" (empty) = unchanged.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each property that was changed,
        /// or an error when the GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-update", Title = "GameObject / Update")]
        [Description("Updates properties of an existing GameObject: name, tag, layer, active state, and static flag. " + "Locate the object by instanceId or hierarchy path. Each property has a 'leave unchanged' sentinel — only non-sentinel values are applied (see per-param descriptions).")]
        public ToolResponse Update(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'World/Props/Crate'). Used when instanceId is 0.")] string objectPath = "",
            [Description("New name for the GameObject. Empty string to leave unchanged.")] string name = "",
            [Description("New tag (must exist in Tag Manager). Empty string to leave unchanged.")] string tag = "",
            [Description("New layer index (0-31). Pass -1 to leave unchanged.")] int layer = -1,
            [Description("Active state: 'true' = activate, 'false' = deactivate, empty = leave unchanged.")] string isActive = "",
            [Description("Static flag: 'true' = mark static, 'false' = clear static, empty = leave unchanged.")] string isStatic = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                Undo.RecordObject(go, $"Update GameObject {go.name}");

                var sb = new StringBuilder();
                sb.AppendLine($"Updated GameObject '{go.name}':");

                if (!string.IsNullOrEmpty(name))
                {
                    go.name = name;
                    sb.AppendLine($"  Name: {name}");
                }

                if (!string.IsNullOrEmpty(tag))
                {
                    go.tag = tag;
                    sb.AppendLine($"  Tag: {tag}");
                }

                if (layer >= 0 && layer <= 31)
                {
                    go.layer = layer;
                    sb.AppendLine($"  Layer: {layer} ({LayerMask.LayerToName(layer)})");
                }
                try
                {
                    if (TryParseNullableBool(isActive, out bool activeValue))
                    {
                        go.SetActive(activeValue);
                        sb.AppendLine($"  Active: {activeValue}");
                    }

                    if (TryParseNullableBool(isStatic, out bool staticValue))
                    {
                        go.isStatic = staticValue;
                        sb.AppendLine($"  Static: {staticValue}");
                    }
                }
                catch (System.ArgumentException ex)
                {
                    return ToolResponse.Error(ex.Message);
                }

                EditorUtility.SetDirty(go);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}