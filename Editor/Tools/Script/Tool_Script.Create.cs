#nullable enable
using System.ComponentModel;
using System.IO;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Script
    {
        #region TOOL METHODS

        /// <summary>
        /// Creates a new C# script file from a template.
        /// </summary>
        /// <param name="path">Project-relative file path for the new script (e.g. "Assets/Scripts/Player.cs").</param>
        /// <param name="template">Template to use: "MonoBehaviour", "ScriptableObject", "EditorWindow", or "Empty". Default "MonoBehaviour".</param>
        /// <param name="namespaceName">Optional namespace to wrap the class in. Leave empty for no namespace.</param>
        /// <param name="className">Class name to use. Derived from the filename when empty.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the class name, path, and template used,
        /// or an error if the path is missing or does not start with "Assets/" or "Packages/".
        /// </returns>
        [McpTool("script-create", Title = "Script / Create")]
        [Description("Creates a new C# script from a template (MonoBehaviour, ScriptableObject, EditorWindow, or Empty).")]
        public ToolResponse Create(
            [Description("File path (e.g. 'Assets/Scripts/Player.cs').")] string path,
            [Description("Template: 'MonoBehaviour', 'ScriptableObject', 'EditorWindow', 'Empty'. Default 'MonoBehaviour'.")] string template = "MonoBehaviour",
            [Description("Optional namespace for the class.")] string namespaceName = "",
            [Description("Optional class name. If empty, derived from filename.")] string className = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string? pathError = ValidateScriptPath(path);

                if (pathError != null)
                {
                    return ToolResponse.Error(pathError);
                }

                if (string.IsNullOrWhiteSpace(className))
                {
                    className = Path.GetFileNameWithoutExtension(path);
                }

                string folder = Path.GetDirectoryName(path) ?? "Assets";

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var sb = new StringBuilder();
                sb.AppendLine("using UnityEngine;");

                if (template == "EditorWindow")
                {
                    sb.AppendLine("using UnityEditor;");
                }

                sb.AppendLine();
                bool hasNs = !string.IsNullOrWhiteSpace(namespaceName);

                if (hasNs)
                {
                    sb.AppendLine($"namespace {namespaceName}");
                    sb.AppendLine("{");
                }

                string indent = hasNs ? "    " : "";

                switch (template)
                {
                    case "ScriptableObject":
                        sb.AppendLine($"{indent}[CreateAssetMenu(fileName = \"{className}\", menuName = \"Custom/{className}\")]");
                        sb.AppendLine($"{indent}public class {className} : ScriptableObject");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}}}");
                        break;

                    case "EditorWindow":
                        sb.AppendLine($"{indent}public class {className} : EditorWindow");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    [MenuItem(\"Window/{className}\")]");
                        sb.AppendLine($"{indent}    public static void ShowWindow()");
                        sb.AppendLine($"{indent}    {{");
                        sb.AppendLine($"{indent}        GetWindow<{className}>(\"{className}\");");
                        sb.AppendLine($"{indent}    }}");
                        sb.AppendLine($"{indent}}}");
                        break;

                    case "Empty":
                        sb.AppendLine($"{indent}public class {className}");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}}}");
                        break;

                    default:
                        sb.AppendLine($"{indent}public class {className} : MonoBehaviour");
                        sb.AppendLine($"{indent}{{");
                        sb.AppendLine($"{indent}    private void Start()");
                        sb.AppendLine($"{indent}    {{");
                        sb.AppendLine($"{indent}    }}");
                        sb.AppendLine();
                        sb.AppendLine($"{indent}    private void Update()");
                        sb.AppendLine($"{indent}    {{");
                        sb.AppendLine($"{indent}    }}");
                        sb.AppendLine($"{indent}}}");
                        break;
                }

                if (hasNs)
                {
                    sb.AppendLine("}");
                }

                File.WriteAllText(path, sb.ToString());
                AssetDatabase.Refresh();

                return ToolResponse.Text($"Created script '{className}' at {path} (template: {template}).");
            });
        }

        #endregion
    }
}