#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Scene
    {
        #region Get Hierarchy

        /// <summary>
        /// Returns a paginated list of GameObjects from the active scene hierarchy.
        /// Starts at the scene root or at the children of <paramref name="parentPath"/>.
        /// </summary>
        /// <param name="pageSize">Maximum number of GameObjects to return per page.</param>
        /// <param name="cursor">Zero-based index of the first item to return (for pagination).</param>
        /// <param name="parentPath">Hierarchy path of a parent GameObject (e.g. 'World/Environment').
        /// Leave empty to list root GameObjects.</param>
        /// <param name="includeTransform">When true, include world-space position and rotation in the output.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the paginated hierarchy listing,
        /// or an error when <paramref name="parentPath"/> cannot be resolved.
        /// </returns>
        [McpTool("scene-get-hierarchy", Title = "Scene / Get Hierarchy", ReadOnlyHint = true)]
        [Description("Returns a paginated listing of GameObjects in the active scene. " + "Leave parentPath empty to list root objects, or provide a hierarchy path to list its children. " + "Use cursor + pageSize for pagination. " + "Set includeTransform=true to include world-space position and rotation.")]
        public ToolResponse GetHierarchy(
            [Description("Maximum number of GameObjects to return per call. Default 50.")] int pageSize = 50,
            [Description("Zero-based index of the first item for pagination. Default 0.")] int cursor = 0,
            [Description("Hierarchy path of the parent whose children to list (e.g. 'World/Props'). Leave empty for scene roots.")] string parentPath = "",
            [Description("Include world-space position and rotation for each GameObject. Default false.")] bool includeTransform = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (pageSize < 1)
                {
                    pageSize = 50;
                }

                if (pageSize > 500)
                {
                    pageSize = 500;
                }

                if (cursor < 0)
                {
                    cursor = 0;
                }

                Scene scene = SceneManager.GetActiveScene();

                if (!scene.IsValid())
                {
                    return ToolResponse.Error("No active scene is currently loaded.");
                }

                Transform[] objects;

                if (!string.IsNullOrWhiteSpace(parentPath))
                {
                    var parentGo = Tool_Transform.FindGameObject(0, parentPath);

                    if (parentGo == null)
                    {
                        return ToolResponse.Error($"Parent GameObject '{parentPath}' not found.");
                    }

                    int childCount = parentGo.transform.childCount;
                    objects = new Transform[childCount];

                    for (int i = 0; i < childCount; i++)
                    {
                        objects[i] = parentGo.transform.GetChild(i);
                    }
                }
                else
                {
                    var roots = scene.GetRootGameObjects();
                    objects = new Transform[roots.Length];

                    for (int i = 0; i < roots.Length; i++)
                    {
                        objects[i] = roots[i].transform;
                    }
                }

                int total = objects.Length;

                if (cursor >= total)
                {
                    return ToolResponse.Text($"cursor ({cursor}) is past the end of {total} item(s). Use a cursor value less than {total}.");
                }

                int end = cursor + pageSize;

                if (end > total)
                {
                    end = total;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Scene Hierarchy — '{scene.name}'");
                sb.AppendLine($"  Source: {(string.IsNullOrWhiteSpace(parentPath) ? "roots" : $"children of '{parentPath}'")}");
                sb.AppendLine($"  Total: {total}  Page: {cursor}–{end - 1}  PageSize: {pageSize}");
                sb.AppendLine();

                for (int i = cursor; i < end; i++)
                {
                    var t = objects[i];

                    if (t == null)
                    {
                        continue;
                    }

                    sb.AppendLine($"[{i}] '{t.name}'  id={t.gameObject.GetInstanceID()}  " + $"children={t.childCount}  active={t.gameObject.activeSelf}");

                    if (includeTransform)
                    {
                        sb.AppendLine($"     pos={t.position}  rot={t.rotation.eulerAngles}");
                    }
                }

                if (end < total)
                {
                    sb.AppendLine($"  ... {total - end} more. Use cursor={end} to fetch next page.");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}