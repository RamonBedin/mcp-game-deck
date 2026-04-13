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
        /// Loads a Prefab asset and returns its hierarchy, components, and prefab type metadata.
        /// </summary>
        /// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the prefab hierarchy and component list,
        /// or an error when the asset cannot be loaded.
        /// </returns>
        [McpTool("prefab-get-info", Title = "Prefab / Get Info", ReadOnlyHint = true)]
        [Description("Loads a Prefab asset and returns its type, full hierarchy, and all components on each GameObject.")]
        public ToolResponse GetInfo(
            [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    return ToolResponse.Error("prefabPath is required.");
                }

                if (!prefabPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("prefabPath must start with 'Assets/' (e.g. 'Assets/Prefabs/Player.prefab').");
                }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    return ToolResponse.Error($"Prefab not found at '{prefabPath}'.");
                }

                PrefabAssetType prefabType = PrefabUtility.GetPrefabAssetType(prefab);

                var sb = new StringBuilder();
                sb.AppendLine($"Prefab: {prefabPath}");
                sb.AppendLine($"  Name:        {prefab.name}");
                sb.AppendLine($"  PrefabType:  {prefabType}");

                sb.AppendLine("  Hierarchy:");
                AppendHierarchy(prefab.transform, sb, 1);

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region HIERARCHY HELPER

        /// <summary>
        /// Recursively appends the transform hierarchy and component list to <paramref name="sb"/>.
        /// </summary>
        /// <param name="t">Transform to start from.</param>
        /// <param name="sb">Target string builder.</param>
        /// <param name="depth">Current indentation depth.</param>
        private static void AppendHierarchy(Transform t, StringBuilder sb, int depth)
        {
            string indent = new(' ', depth * 2);
            var components = t.GetComponents<UnityEngine.Component>();
            var compNames = new StringBuilder();

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    continue;
                }

                if (i > 0)
                {
                    compNames.Append(", ");
                }

                compNames.Append(components[i].GetType().Name);
            }

            sb.AppendLine($"{indent}[{t.name}] active={t.gameObject.activeSelf}  components=[{compNames}]");

            for (int ci = 0; ci < t.childCount; ci++)
            {
                AppendHierarchy(t.GetChild(ci), sb, depth + 1);
            }
        }

        #endregion
    }
}