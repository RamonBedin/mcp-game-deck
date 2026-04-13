#nullable enable
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools.Helpers
{
    /// <summary>
    /// Helper utilities shared across UI Toolkit MCP tools for resolving
    /// GameObjects by instance ID or hierarchy path.
    /// </summary>
    internal static class UIToolkitHelper
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Resolves a GameObject by instanceId (if non-zero) or by hierarchy path.
        /// </summary>
        /// <param name="instanceId">The instance ID to look up. If zero, falls back to <paramref name="objectPath"/>.</param>
        /// <param name="objectPath">The scene hierarchy path (e.g. "Canvas/Panel/Button") used when <paramref name="instanceId"/> is zero.</param>
        /// <param name="go">When this method returns <c>true</c>, contains the resolved GameObject.</param>
        /// <param name="error">When this method returns <c>false</c>, contains a human-readable error message.</param>
        /// <returns><c>true</c> if the GameObject was found; otherwise <c>false</c>.</returns>
        public static bool TryResolveGameObject(int instanceId, string objectPath, out GameObject go, out string? error)
        {
            go = null!;
            error = null;

            if (instanceId != 0)
            {
                var obj = EditorUtility.EntityIdToObject(instanceId) as GameObject;

                if (obj == null)
                {
                    error = $"No GameObject found with instanceId {instanceId}.";
                    return false;
                }

                go = obj;
                return true;
            }

            if (string.IsNullOrWhiteSpace(objectPath))
            {
                error = "Either instanceId or objectPath must be provided.";
                return false;
            }

            var found = GameObject.Find(objectPath);

            if (found == null)
            {
                error = $"No GameObject found at path '{objectPath}'.";
                return false;
            }

            go = found;
            return true;
        }

        #endregion
    }
}