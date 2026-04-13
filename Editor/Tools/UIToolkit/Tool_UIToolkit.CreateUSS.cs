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
        /// Writes a USS stylesheet to the specified asset path. When no content is supplied a template
        /// with common styles using Unity USS variables is generated automatically.
        /// </summary>
        /// <param name="assetPath">Destination asset path ending in .uss (e.g. "Assets/UI/MainMenu.uss").</param>
        /// <param name="content">Full USS text to write. Leave empty to use the built-in template.</param>
        /// <returns>Confirmation text with the path of the created USS file.</returns>
        [McpTool("uitoolkit-create-uss", Title = "UI Toolkit / Create USS")]
        [Description("Creates a UI Toolkit USS stylesheet file at the specified path. " + "If no content is provided, creates a template with common styles.")]
        public ToolResponse CreateUSS(
            [Description("Asset path for the USS file (e.g. 'Assets/UI/MainMenu.uss').")] string assetPath,
            [Description("USS content. If empty, creates a template with common styles.")] string content = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (!assetPath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("assetPath must start with 'Assets/' (e.g. 'Assets/UI/MainMenu.uss').");
                }

                if (!assetPath.EndsWith(".uss"))
                {
                    return ToolResponse.Error("assetPath must end with .uss.");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    content = @".root-container {
    flex-grow: 1;
    padding: 8px;
    background-color: var(--unity-colors-window-background);
}

Label {
    font-size: 14px;
    color: var(--unity-colors-default-text);
    margin-bottom: 4px;
}

Button {
    height: 32px;
    margin: 4px 0;
    border-radius: 4px;
}
";
                }

                var dir = Path.GetDirectoryName(assetPath)!;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(assetPath, content);
                AssetDatabase.ImportAsset(assetPath);

                return ToolResponse.Text($"Created USS at '{assetPath}'.");
            });
        }

        #endregion
    }
}