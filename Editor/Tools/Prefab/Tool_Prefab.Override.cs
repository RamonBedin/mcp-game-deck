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
    public partial class Tool_Prefab
    {
        #region TOOL METHODS

        /// <summary>
        /// Manages overrides on a prefab instance in the scene. Supports listing the current
        /// overrides, applying them back to the prefab asset, or reverting them.
        /// Per-property actions (apply-property, revert-property) and added/removed
        /// component/gameobject deltas are deferred to a later cycle.
        /// </summary>
        /// <param name="action">One of "list", "apply-instance", "revert-instance".</param>
        /// <param name="instanceId">Unity instance ID of the prefab instance root. Pass 0 to use objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the prefab instance root. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> describing the result, or an error.</returns>
        [McpTool("prefab-override", Title = "Prefab / Override")]
        [Description("Manages overrides on a scene prefab instance. Actions: 'list' enumerates the instance's current property overrides (cheap pre-check via HasPrefabInstanceAnyOverrides); 'apply-instance' applies all overrides back to the source prefab asset; 'revert-instance' discards all overrides on the instance. Per-property apply/revert and added/removed component/gameobject deltas are deferred to a later cycle.")]
        public ToolResponse Override(
            [Description("Action to perform. Required. One of 'list', 'apply-instance', 'revert-instance'. Empty returns an error listing the valid values.")] string action = "",
            [Description("Instance ID of the prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the prefab instance root (e.g. 'World/Enemies/Goblin'). Used when instanceId is 0.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(action))
                {
                    return ToolResponse.Error("'action' is required. Valid values: 'list', 'apply-instance', 'revert-instance'.");
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Only prefab instance roots can be targets of override actions.");
                }

                string actionNorm = action.Trim().ToLowerInvariant();

                switch (actionNorm)
                {
                    case "list":
                    {
                        bool hasAny = PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

                        if (!hasAny)
                        {
                            return ToolResponse.Text($"Prefab instance '{go.name}' has no overrides.");
                        }

                        var overrides = PrefabUtility.GetObjectOverrides(go, false);

                        var sb = new StringBuilder();
                        sb.AppendLine($"Overrides on prefab instance '{go.name}':");
                        sb.AppendLine($"  Total: {overrides.Count}");

                        for (int i = 0; i < overrides.Count; i++)
                        {
                            var o = overrides[i];
                            string targetName = o.instanceObject != null ? o.instanceObject.name : "<null>";
                            string targetType = o.instanceObject != null ? o.instanceObject.GetType().Name : "<null>";
                            sb.AppendLine($"  [{i}] {targetType} on '{targetName}'");
                        }

                        return ToolResponse.Text(sb.ToString());
                    }

                    case "apply-instance":
                    {
                        try
                        {
                            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                            return ToolResponse.Text($"Applied all overrides on '{go.name}' back to the source prefab.");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[prefab-override apply-instance] {ex.Message}");
                            return ToolResponse.Error($"ApplyPrefabInstance failed on '{go.name}': {ex.Message}");
                        }
                    }

                    case "revert-instance":
                    {
                        try
                        {
                            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);
                            return ToolResponse.Text($"Reverted all overrides on '{go.name}'.");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[prefab-override revert-instance] {ex.Message}");
                            return ToolResponse.Error($"RevertPrefabInstance failed on '{go.name}': {ex.Message}");
                        }
                    }

                    default:
                    {
                        return ToolResponse.Error($"Unknown action '{action}'. Valid values: 'list', 'apply-instance', 'revert-instance'.");
                    }
                }
            });
        }

        #endregion
    }
}