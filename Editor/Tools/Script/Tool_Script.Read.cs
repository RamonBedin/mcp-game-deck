#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Script
    {
        #region TOOL METHODS

        /// <summary>
        /// Reads and returns the full contents of a script file.
        /// </summary>
        /// <param name="path">Project-relative path of the script to read (e.g. "Assets/Scripts/Player.cs").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the raw file text,
        /// or an error if the path is missing, invalid, or the file does not exist.
        /// </returns>
        [McpTool("script-read", Title = "Script / Read", ReadOnlyHint = true)]
        [Description("Reads and returns the full text content of a script file.")]
        public ToolResponse Read(
            [Description("File path (e.g. 'Assets/Scripts/Player.cs').")] string path
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string? pathError = ValidateScriptPath(path);

                if (pathError != null)
                {
                    return ToolResponse.Error(pathError);
                }

                if (!File.Exists(path))
                {
                    return ToolResponse.Error($"File not found: '{path}'.");
                }

                string? sizeError = ValidateFileSize(path);

                if (sizeError != null)
                {
                    return ToolResponse.Error(sizeError);
                }

                string content = File.ReadAllText(path);
                return ToolResponse.Text(content);
            });
        }

        #endregion
    }
}