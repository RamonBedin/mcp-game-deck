#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_GameObject
    {
        #region TOOL METHODS

        /// <summary>
        /// Destroys the specified GameObject and all its children from the active scene.
        /// The operation is recorded in the Unity Undo stack so it can be reversed with Ctrl+Z.
        /// </summary>
        /// <param name="instanceId">Unity instance ID of the target. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the target (e.g. "World/Enemies/Goblin"). Used when instanceId is 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the name and instance ID of the destroyed object,
        /// or an error when the GameObject cannot be located.
        /// </returns>
        [McpTool("gameobject-delete", Title = "GameObject / Delete")]
        [Description("Destroys a GameObject and all its children from the active scene. " + "The action is recorded in the Undo stack. " + "Locate the object by instanceId or hierarchy path.")]
        public ToolResponse Delete(
            [Description("Unity instance ID of the target GameObject. Pass 0 to use objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'World/Enemies/Goblin'). Used when instanceId is 0.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found. Provide a valid instanceId or objectPath.");
                }

                var deletedName = go.name;
                var deletedId   = go.GetInstanceID();

                Undo.DestroyObjectImmediate(go);

                return ToolResponse.Text($"Deleted GameObject '{deletedName}' (instanceId: {deletedId}).");
            });
        }

        #endregion
    }
}