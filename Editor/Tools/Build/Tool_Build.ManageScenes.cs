#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Build
    {
        #region TOOL METHODS

        /// <summary>
        /// Performs a scene management action against the Build Settings scene list.
        /// </summary>
        /// <param name="action">
        /// The operation to perform. Accepted values: list, add, remove, enable, disable, reorder.
        /// </param>
        /// <param name="scenePath">
        /// Asset path of the target scene (e.g. Assets/Scenes/Main.unity).
        /// Required for all actions except list.
        /// </param>
        /// <param name="index">
        /// Zero-based destination index used by the reorder action. Ignored for all other actions.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> describing the result of the action, or an error response
        /// if the action name is unrecognised, a required parameter is missing, or the scene cannot be found.
        /// </returns>
        [McpTool("build-manage-scenes", Title = "Build / Manage Scenes")]
        [Description("Manages scenes in Build Settings. Actions: list (show current scenes), " + "add (add a scene), remove (remove a scene by path), enable/disable (toggle a scene), " + "reorder (move a scene to a new index).")]
        public ToolResponse ManageScenes(
            [Description("Action: list, add, remove, enable, disable, reorder")] string action,
            [Description("Scene asset path (e.g. 'Assets/Scenes/Main.unity'). Required for add, remove, enable, disable, reorder.")] string scenePath = "",
            [Description("New index position for reorder action. Zero-based.")] int index = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var act = action.ToLowerInvariant().Trim();
                var rawScenes = EditorBuildSettings.scenes;
                var scenes = new List<EditorBuildSettingsScene>(rawScenes.Length);

                for (int i = 0; i < rawScenes.Length; i++)
                {
                    scenes.Add(rawScenes[i]);
                }

                switch (act)
                {
                    case "list":
                    {
                        if (scenes.Count == 0)
                        {
                            return ToolResponse.Text("No scenes in Build Settings.");
                        }
                        var sb = new StringBuilder();
                        sb.AppendLine("Build Settings Scenes:");
                        for (int i = 0; i < scenes.Count; i++)
                        {
                            sb.AppendLine($"  [{i}] {scenes[i].path} (enabled: {scenes[i].enabled})");
                        }
                        return ToolResponse.Text(sb.ToString());
                    }

                    case "add":
                    {
                        if (string.IsNullOrWhiteSpace(scenePath))
                        {
                            return ToolResponse.Error("scenePath is required for add action.");
                        }
                        bool alreadyPresent = false;
                        foreach (var s in scenes)
                        {
                            if (s.path == scenePath)
                            {
                                alreadyPresent = true;
                                break;
                            }
                        }
                        if (alreadyPresent)
                        {
                            return ToolResponse.Error($"Scene '{scenePath}' is already in Build Settings.");
                        }
                        var guid = AssetDatabase.AssetPathToGUID(scenePath);
                        if (string.IsNullOrEmpty(guid))
                        {
                            return ToolResponse.Error($"Scene not found at '{scenePath}'.");
                        }
                        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                        EditorBuildSettings.scenes = scenes.ToArray();
                        return ToolResponse.Text($"Added '{scenePath}' to Build Settings at index {scenes.Count - 1}.");
                    }

                    case "remove":
                    {
                        if (string.IsNullOrWhiteSpace(scenePath))
                        {
                            return ToolResponse.Error("scenePath is required for remove action.");
                        }
                        int removed = 0;
                        for (int i = scenes.Count - 1; i >= 0; i--)
                        {
                            if (scenes[i].path == scenePath)
                            {
                                scenes.RemoveAt(i);
                                removed++;
                            }
                        }
                        if (removed == 0)
                        {
                            return ToolResponse.Error($"Scene '{scenePath}' not found in Build Settings.");
                        }
                        EditorBuildSettings.scenes = scenes.ToArray();
                        return ToolResponse.Text($"Removed '{scenePath}' from Build Settings.");
                    }

                    case "enable":
                    case "disable":
                    {
                        if (string.IsNullOrWhiteSpace(scenePath))
                        {
                            return ToolResponse.Error($"scenePath is required for {act} action.");
                        }
                        if (!scenePath.StartsWith("Assets/") && !scenePath.StartsWith("Packages/"))
                        {
                            return ToolResponse.Error("scenePath must start with 'Assets/' or 'Packages/'.");
                        }
                        int idx = -1;
                        for (int i = 0; i < scenes.Count; i++)
                        {
                            if (scenes[i].path == scenePath)
                            {
                                idx = i;
                                break;
                            }
                        }
                        if (idx < 0)
                        {
                            return ToolResponse.Error($"Scene '{scenePath}' not found in Build Settings.");
                        }
                        bool enable = act == "enable";
                        var scene = scenes[idx];
                        scene.enabled = enable;
                        scenes[idx] = scene;
                        EditorBuildSettings.scenes = scenes.ToArray();
                        return ToolResponse.Text($"Scene '{scenePath}' {(enable ? "enabled" : "disabled")}.");
                    }

                    case "reorder":
                    {
                        if (string.IsNullOrWhiteSpace(scenePath))
                        {
                            return ToolResponse.Error("scenePath is required for reorder action.");
                        }
                        if (!scenePath.StartsWith("Assets/") && !scenePath.StartsWith("Packages/"))
                        {
                            return ToolResponse.Error("scenePath must start with 'Assets/' or 'Packages/'.");
                        }
                        if (index < 0 || index >= scenes.Count)
                        {
                            return ToolResponse.Error($"Index {index} is out of range. Valid: 0-{scenes.Count - 1}.");
                        }
                        int oldIdx = -1;
                        for (int i = 0; i < scenes.Count; i++)
                        {
                            if (scenes[i].path == scenePath)
                            {
                                oldIdx = i;
                                break;
                            }
                        }
                        if (oldIdx < 0)
                        {
                            return ToolResponse.Error($"Scene '{scenePath}' not found in Build Settings.");
                        }
                        var scene = scenes[oldIdx];
                        scenes.RemoveAt(oldIdx);
                        scenes.Insert(index, scene);
                        EditorBuildSettings.scenes = scenes.ToArray();
                        return ToolResponse.Text($"Moved '{scenePath}' from index {oldIdx} to {index}.");
                    }

                    default:
                        return ToolResponse.Error($"Unknown action '{action}'. Valid: list, add, remove, enable, disable, reorder.");
                }
            });
        }

        #endregion
    }
}