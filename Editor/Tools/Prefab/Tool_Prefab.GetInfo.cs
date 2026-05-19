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
        /// Loads a Prefab asset and returns its type, hierarchy (with nested-prefab annotations),
        /// components, and an isVariant flag.
        /// </summary>
        /// <param name="prefabPath">Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').</param>
        /// <param name="maxDepth">Maximum hierarchy depth to traverse. -1 = unlimited; 0 = root only.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the prefab hierarchy, component list, isVariant flag,
        /// and nested-prefab annotations, or an error when the asset cannot be loaded.
        /// </returns>
        [McpTool("prefab-get-info", Title = "Prefab / Get Info", ReadOnlyHint = true)]
        [Description("Loads a Prefab asset and returns its type, full hierarchy (with nested-prefab annotations), and all components on each GameObject. " + "Output is plain text with one header block followed by an indented hierarchy. Each hierarchy line has the form '[name] active=... components=[Comp1, Comp2, ...]' and is prefixed '[nested-prefab]' when the GameObject is a nested prefab instance root. " + "To enumerate or search for prefabs by path/filter, use 'asset-find' with t:Prefab.")]
        public ToolResponse GetInfo(
            [Description("Project-relative path to the Prefab asset (e.g. 'Assets/Prefabs/Player.prefab').")] string prefabPath,
            [Description("Maximum hierarchy depth to traverse. -1 means unlimited (default; preserves existing behavior). 0 prints the root only.")] int maxDepth = -1
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
                bool isVariant = prefabType == PrefabAssetType.Variant;

                var sb = new StringBuilder();
                sb.AppendLine($"Prefab: {prefabPath}");
                sb.AppendLine($"  Name:        {prefab.name}");
                sb.AppendLine($"  PrefabType:  {prefabType}");
                sb.AppendLine($"  isVariant:   {(isVariant ? "true" : "false")}");

                sb.AppendLine("  Hierarchy:");
                AppendHierarchy(prefab.transform, sb, 1, maxDepth);

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region HIERARCHY HELPER

        /// <summary>
        /// Recursively appends the transform hierarchy and component list to <paramref name="sb"/>,
        /// prefixing nested-prefab-root rows with "[nested-prefab]" and stopping traversal at
        /// <paramref name="maxDepth"/> when that value is non-negative.
        /// </summary>
        /// <param name="t">Transform to start from.</param>
        /// <param name="sb">Target string builder.</param>
        /// <param name="depth">Current indentation depth (1 = direct child of the printed root).</param>
        /// <param name="maxDepth">Maximum depth to traverse. -1 = unlimited.</param>
        private static void AppendHierarchy(Transform t, StringBuilder sb, int depth, int maxDepth)
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

            bool isNestedPrefabRoot = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject);
            string nestedTag = isNestedPrefabRoot ? "[nested-prefab] " : string.Empty;

            sb.AppendLine($"{indent}{nestedTag}[{t.name}] active={t.gameObject.activeSelf}  components=[{compNames}]");

            if (maxDepth >= 0 && depth >= maxDepth)
            {
                if (t.childCount > 0)
                {
                    sb.AppendLine($"{indent}  ... ({t.childCount} child{(t.childCount == 1 ? string.Empty : "ren")} omitted at maxDepth={maxDepth})");
                }

                return;
            }

            for (int ci = 0; ci < t.childCount; ci++)
            {
                AppendHierarchy(t.GetChild(ci), sb, depth + 1, maxDepth);
            }
        }

        #endregion
    }
}