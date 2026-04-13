#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Writes the supplied text to a file at the given path, creating the file (and any missing
        /// directories) if necessary, then calls <see cref="AssetDatabase.ImportAsset"/> so Unity
        /// picks up the change immediately.
        /// </summary>
        /// <param name="path">Asset path of the file to write (e.g. "Assets/UI/HUD.uxml").</param>
        /// <param name="contents">Full text content to write to the file.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the write, or an error message.</returns>
        [McpTool("uitoolkit-update-file", Title = "UI Toolkit / Update File")]
        [Description("Overwrites the contents of a UXML, USS, or any text file on disk and reimports it " + "into the AssetDatabase so Unity reflects the changes immediately.")]
        public ToolResponse UpdateFile(
            [Description("Asset path of the file to write (e.g. 'Assets/UI/HUD.uxml').")] string path,
            [Description("Full text content to write to the file. Overwrites existing content.")] string? contents
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return ToolResponse.Error("path is required.");
                }

                if (!path.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("path must start with 'Assets/' (e.g. 'Assets/UI/HUD.uxml').");
                }

                if (contents == null)
                {
                    return ToolResponse.Error("contents must not be null.");
                }

                var dir = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, contents);
                AssetDatabase.ImportAsset(path);

                return ToolResponse.Text($"Updated file at '{path}' ({contents.Length} characters).");
            });
        }

        #endregion
    }
}