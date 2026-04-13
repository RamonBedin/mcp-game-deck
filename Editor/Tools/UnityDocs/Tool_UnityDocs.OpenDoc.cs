#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_UnityDocs
    {
        #region TOOL METHODS

        /// <summary>
        /// Opens the Unity documentation page for a given class or member in the default browser.
        /// </summary>
        /// <param name="className">Unity class name (e.g. 'Physics', 'Transform', 'Rigidbody').</param>
        /// <param name="memberName">Optional member name (method or property, e.g. 'Raycast', 'position').</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the URL that was opened, or an error.</returns>
        [McpTool("unity-docs-open", Title = "Unity Docs / Open")]
        [Description("Opens the Unity documentation page for a given class or member in the default browser. " + "Uses Unity's built-in Help.BrowseURL with the ScriptReference URL.")]
        public ToolResponse OpenDoc(
            [Description("Unity class name (e.g. 'Physics', 'Transform', 'Rigidbody').")] string className,
            [Description("Optional member name (method or property, e.g. 'Raycast', 'position').")] string memberName = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(className))
                {
                    return ToolResponse.Error("className is required.");
                }

                string page = string.IsNullOrWhiteSpace(memberName) ? $"{className}.html" : $"{className}.{memberName}.html";
                string url = $"https://docs.unity3d.com/ScriptReference/{page}";
                Help.BrowseURL(url);

                return ToolResponse.Text($"Opened documentation: {url}");
            });
        }

        #endregion
    }
}