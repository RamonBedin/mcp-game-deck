#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region CREATE FOLDER

        /// <summary>
        /// Creates a folder hierarchy in the project. Creates intermediate folders as needed.
        /// </summary>
        /// <param name="folderPath">Full folder path to create (e.g. 'Assets/Prefabs/Enemies'). Auto-prepends 'Assets/' if omitted.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the folder was created, or that it already exists.</returns>
        [McpTool("asset-create-folder", Title = "Asset / Create Folder")]
        [Description("Creates a folder in the project, including any missing intermediate folders.")]
        public ToolResponse CreateFolder(
            [Description("Full folder path (e.g. 'Assets/Prefabs/Enemies').")] string folderPath
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    return ToolResponse.Error("folderPath is required.");
                }

                if (!folderPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase) && !string.Equals(folderPath, "Assets", System.StringComparison.OrdinalIgnoreCase))
                {
                    folderPath = "Assets/" + folderPath;
                }

                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    return ToolResponse.Text($"Folder already exists: {folderPath}");
                }

                string[] parts = folderPath.Replace('\\', '/').Split('/');
                string current = parts[0];

                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];

                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }

                    current = next;
                }

                return ToolResponse.Text($"Created folder: {folderPath}");
            });
        }

        #endregion
    }
}