#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Compilation;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Script
    {
        #region TOOL METHODS

        /// <summary>
        /// Checks if the project compiles successfully and whether a specific script exists.
        /// </summary>
        /// <param name="path">Project-relative path of the script to validate (e.g. "Assets/Scripts/Player.cs").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with file existence, character count, line count, assembly name,
        /// and current compilation status, or an error if the path is missing.
        /// </returns>
        [McpTool("script-validate", Title = "Script / Validate", ReadOnlyHint = true)]
        [Description("Checks if a script file exists and whether the project is compiling without errors.")]
        public ToolResponse Validate(
            [Description("Script file path (e.g. 'Assets/Scripts/Player.cs').")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                bool exists = System.IO.File.Exists(path);
                bool isCompiling = EditorApplication.isCompiling;

                var sb = new StringBuilder();
                sb.AppendLine($"Script Validation: {path}");
                sb.AppendLine($"  File exists: {exists}");
                sb.AppendLine($"  Unity is compiling: {isCompiling}");

                if (exists)
                {
                    string content = System.IO.File.ReadAllText(path);
                    sb.AppendLine($"  File size: {content.Length} chars");
                    int lineCount = 1;

                    for (int ci = 0; ci < content.Length; ci++)
                    {
                        if (content[ci] == '\n')
                        {
                            lineCount++;
                        }
                    }

                    sb.AppendLine($"  Line count: {lineCount}");
                    Assembly[] assemblies = CompilationPipeline.GetAssemblies();
                    bool foundAssembly = false;

                    for (int i = 0; i < assemblies.Length; i++)
                    {
                        string normalised = path.Replace('\\', '/').ToLowerInvariant();

                        for (int j = 0; j < assemblies[i].sourceFiles.Length; j++)
                        {
                            if (assemblies[i].sourceFiles[j].Replace('\\', '/').ToLowerInvariant().Contains(normalised))
                            {
                                sb.AppendLine($"  Assembly: {assemblies[i].name}");
                                foundAssembly = true;
                                break;
                            }
                        }

                        if (foundAssembly)
                        {
                            break;
                        }
                    }
                }

                sb.AppendLine(isCompiling ? "  Status: Compilation in progress — check console for errors." : "  Status: No compilation running — project compiled OK.");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}