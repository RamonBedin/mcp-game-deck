#nullable enable
using System.ComponentModel;
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
        /// Unpacks a prefab instance in the current scene, severing its prefab connection
        /// so subsequent edits no longer track the source prefab.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the prefab instance root. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the prefab instance root. Used when instanceId is 0.</param>
        /// <param name="unpackMode">Unpack mode: "outermost" (default) unpacks only the outermost prefab; "completely" unpacks all nested prefabs.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the unpack, or an error.</returns>
        [McpTool("prefab-unpack-instance", Title = "Prefab / Unpack Instance")]
        [Description("Unpacks a prefab instance in the current scene, severing its prefab connection. After unpack, the GameObject is a plain scene object with no link back to the prefab asset. Use 'outermost' (default) to unpack only the outermost prefab and keep nested prefabs intact, or 'completely' to unpack all nested prefabs in the hierarchy.")]
        public ToolResponse UnpackInstance(
            [Description("Instance ID of the prefab instance root. 0 to use objectPath.")] int instanceId = 0,
            [Description("Hierarchy path of the prefab instance root (e.g. 'World/Enemies/Goblin'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Unpack mode. One of 'outermost' (default; unpacks only the outermost prefab) or 'completely' (unpacks all nested prefabs).")] string unpackMode = "outermost"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
                {
                    return ToolResponse.Error($"'{go.name}' is not a prefab instance root. Only prefab instance roots can be unpacked.");
                }

                string norm = unpackMode.Trim().ToLowerInvariant();
                PrefabUnpackMode mode;

                if (norm == "outermost")
                {
                    mode = PrefabUnpackMode.OutermostRoot;
                }
                else if (norm == "completely")
                {
                    mode = PrefabUnpackMode.Completely;
                }
                else if (string.IsNullOrEmpty(norm))
                {
                    return ToolResponse.Error("unpackMode is required. Valid values: 'outermost', 'completely'.");
                }
                else
                {
                    return ToolResponse.Error($"Unknown unpackMode '{unpackMode}'. Valid values: 'outermost', 'completely'.");
                }

                PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);

                return ToolResponse.Text($"Unpacked prefab instance '{go.name}' (mode: {mode}).");
            });
        }

        #endregion
    }
}