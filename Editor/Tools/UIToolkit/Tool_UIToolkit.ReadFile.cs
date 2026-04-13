#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Reads the full text content of a file at the given path and returns it as a plain-text response.
        /// Useful for inspecting UXML and USS files without opening the Unity editor.
        /// </summary>
        /// <param name="path">Absolute or project-relative path to the file to read.</param>
        /// <returns>A <see cref="ToolResponse"/> with the full file content, or an error if the file does not exist.</returns>
        [McpTool("uitoolkit-read-file", Title = "UI Toolkit / Read File", ReadOnlyHint = true)]
        [Description("Reads the raw text content of a UXML, USS, or any text file and returns it as a string. " + "Useful for inspecting UI Toolkit source files.")]
        public ToolResponse ReadFile(
            [Description("Absolute or project-relative path to the file to read (e.g. 'Assets/UI/HUD.uxml').")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!File.Exists(path))
                {
                    return ToolResponse.Error($"File not found: '{path}'.");
                }

                string content = File.ReadAllText(path);
                return ToolResponse.Text(content);
            });
        }

        #endregion
    }
}