#nullable enable
using System.ComponentModel;
using System.IO;
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
        /// Writes the provided content to a script file, overwriting its current contents.
        /// </summary>
        /// <param name="path">Project-relative path of the script to write (e.g. "Assets/Scripts/Player.cs").</param>
        /// <param name="content">Full text content to write to the file.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the path and character count written,
        /// or an error if the path is missing or does not start with "Assets/" or "Packages/".
        /// </returns>

        [McpTool("script-update", Title = "Script / Update")]
        [Description("Writes new content to a script file, replacing its current contents entirely.")]
        public ToolResponse Update(
            [Description("File path (e.g. 'Assets/Scripts/Player.cs').")] string path,
            [Description("Full file content to write.")] string content
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string? pathError = ValidateScriptPath(path);

                if (pathError != null)
                {
                    return ToolResponse.Error(pathError);
                }

                string folder = Path.GetDirectoryName(path) ?? "Assets";

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                if (content.Length > GameDeck.MCP.Server.McpConstants.MAX_SCRIPT_FILE_SIZE)
                {
                    return ToolResponse.Error($"Content is too large ({content.Length / (1024 * 1024)}MB). Maximum is 10MB.");
                }

                File.WriteAllText(path, content);
                AssetDatabase.ImportAsset(path);

                return ToolResponse.Text($"Updated script at '{path}' ({content.Length} chars).");
            });
        }

        #endregion
    }
}