#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using GameDeck.Editor.Tools.Helpers;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds a GameObject with a <see cref="UIDocument"/> component and recursively dumps its
        /// <see cref="VisualElement"/> tree, including element type, name, USS classes, text content,
        /// and child count.
        /// </summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Takes priority over objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="maxDepth">Maximum recursion depth when walking the tree. Defaults to 10.</param>
        /// <returns>A <see cref="ToolResponse"/> with a formatted visual tree dump, or an error message.</returns>
        [McpTool("uitoolkit-get-visual-tree", Title = "UI Toolkit / Get Visual Tree", ReadOnlyHint = true)]
        [Description("Finds a GameObject with a UIDocument and returns a structured dump of its live visual element tree, " + "including element type, name, USS classes, text, and child count.")]
        public ToolResponse GetVisualTree(
            [Description("Instance ID of the target GameObject. Use 0 to find by objectPath instead.")] int instanceId = 0,
            [Description("Hierarchy path of the target GameObject (e.g. 'Canvas/HUD'). Used when instanceId is 0.")] string objectPath = "",
            [Description("Maximum recursion depth when walking the visual tree. Defaults to 10.")] int maxDepth = 10
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!UIToolkitHelper.TryResolveGameObject(instanceId, objectPath, out var go, out var goError))
                {
                    return ToolResponse.Error(goError!);
                }

                if (!go.TryGetComponent<UIDocument>(out var doc))
                {
                    return ToolResponse.Error($"GameObject '{go.name}' does not have a UIDocument component.");
                }

                var root = doc.rootVisualElement;

                if (root == null)
                {
                    return ToolResponse.Error($"UIDocument on '{go.name}' has no rootVisualElement. Is the game running?");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Visual Tree for '{go.name}':");
                AppendElement(sb, root, depth: 0, maxDepth: maxDepth);

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Recursively appends a <see cref="VisualElement"/> and its children to the string builder.
        /// </summary>
        /// <param name="sb">Accumulator for the tree output.</param>
        /// <param name="element">The element to dump.</param>
        /// <param name="depth">Current indentation depth.</param>
        /// <param name="maxDepth">Maximum depth before truncation.</param>
        private static void AppendElement(StringBuilder sb, VisualElement element, int depth, int maxDepth)
        {
            if (depth >= maxDepth)
            {
                sb.Append(' ', depth * 2);
                sb.AppendLine("[max depth reached]");
                return;
            }

            var classListStr = new StringBuilder();
            var classList = element.GetClasses();
            bool first = true;

            foreach (string cls in classList)
            {
                if (!first)
                {
                    classListStr.Append(' ');
                }

                classListStr.Append('.');
                classListStr.Append(cls);
                first = false;
            }

            string typeName  = element.GetType().Name;
            string nameStr   = string.IsNullOrEmpty(element.name) ? "(no name)" : element.name;
            string classStr  = classListStr.Length > 0 ? $" [{classListStr}]" : "";
            string textStr   = "";

            if (element is TextElement textEl && !string.IsNullOrEmpty(textEl.text))
            {
                textStr = $" text=\"{textEl.text}\"";
            }

            sb.Append(' ', depth * 2);
            sb.AppendLine($"{typeName} name=\"{nameStr}\"{classStr}{textStr} children={element.childCount}");

            for (int i = 0; i < element.childCount; i++)
            {
                AppendElement(sb, element[i], depth + 1, maxDepth);
            }
        }

        #endregion
    }
}