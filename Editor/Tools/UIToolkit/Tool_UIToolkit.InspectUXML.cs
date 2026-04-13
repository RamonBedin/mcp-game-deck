#nullable enable
using System.ComponentModel;
using System.IO;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UIToolkit
    {
        #region TOOL METHODS

        /// <summary>
        /// Loads a UXML asset from the AssetDatabase and returns its full file content as text.
        /// </summary>
        /// <param name="assetPath">Asset path of the UXML file to inspect (e.g. "Assets/UI/MainMenu.uxml").</param>
        /// <returns>The raw UXML text prefixed with the asset path, or an error if the asset is not found.</returns>
        [McpTool("uitoolkit-inspect-uxml", Title = "UI Toolkit / Inspect UXML")]
        [Description("Reads and returns the content of a UXML file. Useful for inspecting " + "existing UI layouts before modifying them.")]
        public ToolResponse InspectUXML(
            [Description("Asset path of the UXML file (e.g. 'Assets/UI/MainMenu.uxml').")] string assetPath
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

                var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"UXML file not found at '{assetPath}'.");
                }

                var content = File.ReadAllText(assetPath);
                return ToolResponse.Text($"UXML content of '{assetPath}':\n\n{content}");
            });
        }

        #endregion
    }
}