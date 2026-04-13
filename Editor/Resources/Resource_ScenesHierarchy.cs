#nullable enable
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that retrieves the complete hierarchy of all loaded Unity scenes,
    /// including GameObjects, their active state, components, and nesting depth.
    /// </summary>
    [McpResourceType]
    public class Resource_ScenesHierarchy
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const int MAX_DEPTH = 50;
        private const int INDENT_SIZE = 2;
        private const int COMPONENT_INDENT_OFFSET = 4;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Recursively appends a <see cref="GameObject"/> and its children to the hierarchy output.
        /// Each entry is indented by <paramref name="depth"/>, shows active state, lists non-Transform
        /// components, and recurses into children up to <see cref="MAX_DEPTH"/>.
        /// </summary>
        /// <param name="sb">The StringBuilder accumulating the hierarchy text.</param>
        /// <param name="go">The GameObject to append.</param>
        /// <param name="depth">Current recursion depth (controls indentation).</param>
        private static void AppendGameObject(StringBuilder sb, GameObject go, int depth)
        {
            if (depth > MAX_DEPTH)
            {
                sb.Append(' ', depth * INDENT_SIZE);
                sb.AppendLine("... (max depth reached)");
                return;
            }

            int indentChars = depth * INDENT_SIZE;
            sb.Append(' ', indentChars);
            sb.Append("- ");
            sb.Append(go.name);

            if (!go.activeSelf)
            {
                sb.Append(" [INACTIVE]");
            }

            sb.AppendLine();
            var components = go.GetComponents<Component>();

            foreach (var comp in components)
            {
                if (comp == null)
                {
                    continue;
                }

                if (comp is Transform)
                {
                    continue;
                }

                sb.Append(' ', indentChars + COMPONENT_INDENT_OFFSET);
                sb.Append('[');
                sb.Append(comp.GetType().Name);
                sb.AppendLine("]");
            }

            for (int i = 0; i < go.transform.childCount; i++)
            {
                AppendGameObject(sb, go.transform.GetChild(i).gameObject, depth + 1);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns a text representation of all loaded scenes and their GameObject hierarchies.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <returns>An array of resource content entries containing the scenes hierarchy as plain text.</returns>
        [McpResource
        (
            Name = "Scenes Hierarchy",
            Route = "mcp-game-deck://scenes-hierarchy",
            MimeType = "text/plain",
            Description = "Retrieves the complete hierarchy of all loaded scenes with GameObjects, " + "their active state, components, and nesting depth."
        )]
        public ResourceResponse[] GetScenesHierarchy(string uri)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                int sceneCount = SceneManager.sceneCount;

                for (int i = 0; i < sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);

                    if (!scene.isLoaded)
                    {
                        continue;
                    }

                    sb.AppendLine($"Scene: {scene.name} (path: {scene.path})");
                    sb.AppendLine($"  Root GameObjects: {scene.rootCount}");
                    sb.AppendLine();

                    foreach (var root in scene.GetRootGameObjects())
                    {
                        AppendGameObject(sb, root, 0);
                    }

                    sb.AppendLine();
                }

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()).MakeArray();
            });
        }

        #endregion
    }
}
