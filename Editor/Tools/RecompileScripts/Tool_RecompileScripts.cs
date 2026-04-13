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
    /// <summary>
    /// MCP tool that triggers script recompilation in the Unity Editor and reports compilation status.
    /// </summary>
    [McpToolType]
    public partial class Tool_RecompileScripts
    {
        #region TOOL METHODS

        /// <summary>
        /// Triggers a script recompilation in the Unity Editor and optionally forces a full AssetDatabase reimport.
        /// </summary>
        /// <param name="forceReimport">If true, also forces a full AssetDatabase reimport. Default false.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the recompilation request or reporting already-compiling status.</returns>
        [McpTool("recompile-scripts", Title = "Editor / Recompile Scripts")]
        [Description("Triggers a script recompilation in Unity Editor. Also refreshes the AssetDatabase. " + "Returns current compilation status and any pending assembly info.")]
        public ToolResponse Recompile(
            [Description("If true, also forces a full AssetDatabase reimport. Default false.")] bool forceReimport = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();

                if (EditorApplication.isCompiling)
                {
                    sb.AppendLine("Scripts are already compiling. Waiting for current compilation to finish.");
                    return ToolResponse.Text(sb.ToString());
                }

                if (forceReimport)
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    sb.AppendLine("Forced AssetDatabase reimport triggered.");
                }
                else
                {
                    AssetDatabase.Refresh();
                }

                CompilationPipeline.RequestScriptCompilation();
                sb.AppendLine("Script recompilation requested.");
                sb.AppendLine($"  Is Compiling: {EditorApplication.isCompiling}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>
        /// Checks current script compilation status and lists all player and editor assemblies.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> with compilation flags and assembly lists.</returns>
        [McpTool("recompile-status", Title = "Editor / Compilation Status")]
        [Description("Checks current script compilation status — whether compilation is in progress " + "and lists player/editor assemblies.")]
        public ToolResponse CompilationStatus()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Compilation Status:");
                sb.AppendLine($"  Is Compiling: {EditorApplication.isCompiling}");
                sb.AppendLine($"  Is Playing: {EditorApplication.isPlaying}");
                sb.AppendLine($"  Is Paused: {EditorApplication.isPaused}");

                sb.AppendLine();
                sb.AppendLine("Player Assemblies:");
                var playerAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);

                foreach (var asm in playerAssemblies)
                {
                    sb.AppendLine($"  {asm.name}");
                }

                sb.AppendLine();
                sb.AppendLine("Editor Assemblies:");
                var editorAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);

                foreach (var asm in editorAssemblies)
                {
                    sb.AppendLine($"  {asm.name}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}