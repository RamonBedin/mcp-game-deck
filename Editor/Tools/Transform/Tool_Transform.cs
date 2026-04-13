#nullable enable
using GameDeck.MCP.Attributes;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tool for manipulating GameObject transforms, including position,
    /// rotation, and scale operations.
    /// </summary>
    [McpToolType]
    public partial class Tool_Transform
    {
        #region Helpers

        /// <summary>
        /// Finds a GameObject by instance ID or hierarchy path.
        /// Tries instanceId first (if non-zero), then objectPath via GameObject.Find.
        /// </summary>
        /// <param name="instanceId">The Unity instance ID of the target GameObject. Pass 0 to skip.</param>
        /// <param name="objectPath">The hierarchy path (e.g. "Parent/Child") to find if instanceId is 0.</param>
        /// <returns>The matching <see cref="GameObject"/>, or null if neither lookup succeeds.</returns>
        public static GameObject? FindGameObject(int instanceId, string objectPath)
        {
            if (instanceId != 0)
            {
#pragma warning disable CS0618
                var obj = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618

                if (obj is GameObject go)
                {
                    return go;
                }

                if (obj is UnityEngine.Component comp)
                {
                    return comp.gameObject;
                }

                return null;
            }

            if (!string.IsNullOrWhiteSpace(objectPath))
            {
                var obj = GameObject.Find(objectPath);

                if (obj != null)
                {
                    return obj;
                }
            }

            return null;
        }

        #endregion
    }
}