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
        /// Reorders a GameObject within its current parent by setting its sibling index.
        /// index = 0 makes it the first child; index = -1 (or any value &gt;= the parent's childCount) makes it the last child.
        /// Useful for UI z-order (Canvas children) and any hierarchy where deterministic order matters.
        /// The operation is recorded in the Unity Undo stack.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject (e.g. "Canvas/Panel/Button"). Used when instanceId is 0.</param>
        /// <param name="index">New sibling index within the parent. 0 = first child; -1 = last child; values are clamped to [0, parent.childCount-1].</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the previous and new sibling indices,
        /// or an error when the GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-set-sibling-index", Title = "GameObject / Set Sibling Index")]
        [Description("Reorders a GameObject within its current parent by setting its sibling index. " + "index = 0 makes it the first child; index = -1 (or any value >= the parent's childCount) makes it the last child. " + "Useful for UI z-order (Canvas children) and any hierarchy where deterministic order matters. " + "Registers the operation with Undo.")]
        public ToolResponse SetSiblingIndex(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Canvas/Panel/Button'). Used when instanceId is 0.")] string objectPath = "",
            [Description("New sibling index within the parent. 0 = first child; -1 = last child; values clamped to [0, parent.childCount-1].")] int index = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error($"GameObject not found. instanceId={instanceId}, objectPath='{objectPath}'.");
                }

                var t = go.transform;
                var parent = t.parent;
                int siblingCount;

                if (parent != null)
                {
                    siblingCount = parent.childCount;
                }
                else
                {
                    siblingCount = go.scene.rootCount;
                }

                int previousIndex = t.GetSiblingIndex();
                int targetIndex;

                if (index < 0 || index >= siblingCount)
                {
                    targetIndex = siblingCount - 1;
                }
                else
                {
                    targetIndex = index;
                }

                Undo.RegisterFullObjectHierarchyUndo(parent != null ? (UnityEngine.Object)parent.gameObject : (UnityEngine.Object)go, $"Set Sibling Index {go.name}");
                t.SetSiblingIndex(targetIndex);

                string parentDesc = parent != null ? $"'{parent.name}'" : "(scene root)";
                return ToolResponse.Text($"Reordered '{go.name}' under {parentDesc}: sibling index {previousIndex} → {targetIndex} (of {siblingCount} children).");
            });
        }

        #endregion
    }
}