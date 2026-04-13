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
        /// Selects the specified GameObject in the Editor, making it active in the Inspector
        /// and optionally highlighting (pinging) it in the Hierarchy window.
        /// Resolution priority: instanceId, then objectPath, then objectName.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target. Checked first. Pass 0 to skip.</param>
        /// <param name="objectPath">Hierarchy path of the target (e.g. "World/Player"). Checked second when instanceId is 0.</param>
        /// <param name="objectName">
        /// Short name used with <see cref="GameObject.Find"/> as a last resort when both
        /// instanceId and objectPath are unspecified. Note: finds the first match by name only.
        /// </param>
        /// <param name="ping">When <c>true</c>, pings the object in the Hierarchy window to flash-highlight it. Default <c>true</c>.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the selected object's name and instance ID,
        /// or an error when no matching GameObject is found.
        /// </returns>
        [McpTool("gameobject-select", Title = "GameObject / Select")]
        [Description("Selects a GameObject in the Unity Editor and makes it active in the Inspector. " + "Resolution order: instanceId > objectPath > objectName. " + "Optionally pings (flash-highlights) it in the Hierarchy window.")]
        public ToolResponse Select(
            [Description("Unity instance ID of the target. Pass 0 to use objectPath or objectName instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target (e.g. 'World/Player'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Short name for a last-resort GameObject.Find lookup. Used when both instanceId and objectPath are empty.")] string objectName = "",
            [Description("If true, pings the object in the Hierarchy window. Default true.")] bool ping = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null && !string.IsNullOrWhiteSpace(objectName))
                {
                    go = GameObject.Find(objectName);
                }

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId, objectPath, or objectName.");
                }

                Selection.activeGameObject = go;

                if (ping)
                {
                    EditorGUIUtility.PingObject(go);
                }

                return ToolResponse.Text($"Selected GameObject '{go.name}' (instanceId: {go.GetInstanceID()}).");
            });
        }

        #endregion
    }
}