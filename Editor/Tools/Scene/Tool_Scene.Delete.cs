#nullable enable
using System.ComponentModel;
using System.Collections.Generic;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region TOOL METHODS

        /// <summary>
        /// Permanently deletes a Scene asset from disk and optionally removes its entry
        /// from the Editor Build Settings scene list.
        /// </summary>
        /// <param name="scenePath">Asset path of the scene to delete (e.g. 'Assets/Scenes/Old.unity').</param>
        /// <param name="removeFromBuildSettings">
        /// When true, the scene is also removed from EditorBuildSettings.scenes. Default true.
        /// </param>
        /// <returns>A <see cref="ToolResponse"/> confirming deletion, or an error on failure.</returns>
        [McpTool("scene-delete", Title = "Scene / Delete")]
        [Description("Permanently deletes a Scene asset from the project. " + "Optionally removes its entry from the Build Settings scene list.")]
        public ToolResponse Delete(
            [Description("Asset path of the scene to delete (e.g. 'Assets/Scenes/Old.unity').")] string scenePath,
            [Description("Remove the scene from Build Settings after deletion. Default true.")] bool removeFromBuildSettings = true
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    return ToolResponse.Error("scenePath is required.");
                }

                if (!scenePath.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("scenePath must start with 'Assets/' (e.g. 'Assets/Scenes/Level.unity').");
                }

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(scenePath);

                if (assetType == null)
                {
                    return ToolResponse.Error($"No asset found at '{scenePath}'.");
                }

                string buildSettingsNote = "";

                if (removeFromBuildSettings)
                {
                    var existing = EditorBuildSettings.scenes;
                    var filtered = new List<EditorBuildSettingsScene>(existing.Length);
                    bool found = false;

                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (existing[i].path == scenePath)
                        {
                            found = true;
                            continue;
                        }

                        filtered.Add(existing[i]);
                    }

                    if (found)
                    {
                        EditorBuildSettings.scenes = filtered.ToArray();
                        buildSettingsNote = " Removed from Build Settings.";
                    }
                }

                bool deleted = AssetDatabase.DeleteAsset(scenePath);

                if (!deleted)
                {
                    return ToolResponse.Error($"AssetDatabase failed to delete '{scenePath}'. " + "The file may be locked or outside the Assets folder.");
                }

                return ToolResponse.Text($"Deleted scene at '{scenePath}'.{buildSettingsNote}");
            });
        }

        #endregion
    }
}