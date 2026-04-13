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
        #region Tool Methods

        /// <summary>
        /// Writes a UXML file to the specified asset path. When no content is supplied a minimal
        /// VisualElement root template is generated automatically.
        /// </summary>
        /// <param name="assetPath">Destination asset path ending in .uxml (e.g. "Assets/UI/MainMenu.uxml").</param>
        /// <param name="content">Full UXML text to write. Leave empty to use the built-in template.</param>
        /// <returns>Confirmation text with the path of the created UXML file.</returns>
        [McpTool("uitoolkit-create-uxml", Title = "UI Toolkit / Create UXML")]
        [Description("Creates a UI Toolkit UXML file at the specified path with the given content. " + "If no content is provided, creates a minimal template with a VisualElement root.")]
        public ToolResponse CreateUXML(
            [Description("Asset path for the UXML file (e.g. 'Assets/UI/MainMenu.uxml').")] string assetPath,
            [Description("UXML content. If empty, creates a minimal template.")] string content = ""
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
                    return ToolResponse.Error("assetPath must start with 'Assets/' (e.g. 'Assets/UI/MainMenu.uxml').");
                }

                if (!assetPath.EndsWith(".uxml"))
                {
                    return ToolResponse.Error("assetPath must end with .uxml.");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    content = @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements""
    xsi=""http://www.w3.org/2001/XMLSchema-instance""
    editor=""UnityEditor.UIElements""
    noNamespaceSchemaLocation=""../../UIElementsSchema/UIElements.xsd"">

    <ui:VisualElement name=""Root"" class=""root-container"">
        <ui:Label text=""Hello UI Toolkit"" name=""Title"" />
    </ui:VisualElement>

</ui:UXML>";
                }

                var dir = Path.GetDirectoryName(assetPath)!;

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(assetPath, content);
                AssetDatabase.ImportAsset(assetPath);

                return ToolResponse.Text($"Created UXML at '{assetPath}'.");
            });
        }

        #endregion
    }
}