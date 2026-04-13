#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Selection
    {
        #region TOOL METHODS

        /// <summary>
        /// Sets the Editor selection to the specified GameObjects, identified either by
        /// instance IDs or by hierarchy paths.  Both parameters may be supplied simultaneously;
        /// results are merged and deduplicated.
        /// </summary>
        /// <param name="instanceIds">
        /// Comma-separated list of integer instance IDs to select (e.g. "12345,67890").
        /// Empty or whitespace entries are ignored.
        /// </param>
        /// <param name="objectPaths">
        /// Comma-separated list of hierarchy paths to select (e.g. "Player,World/Terrain").
        /// Each path is passed to <c>GameObject.Find</c>.
        /// Empty or whitespace entries are ignored.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new selection, or an error when
        /// no valid GameObjects could be resolved from the provided inputs.
        /// </returns>
        [McpTool("selection-set", Title = "Selection / Set")]
        [Description("Sets the Editor selection to specific GameObjects. Provide instance IDs " + "(comma-separated integers) and/or hierarchy paths (comma-separated strings). " + "Both lists are combined. At least one valid GameObject must be resolved.")]
        public ToolResponse SetSelection(
            [Description("Comma-separated instance IDs of GameObjects to select (e.g. '12345,67890'). " + "Leave empty to skip.")] string instanceIds = "",
            [Description("Comma-separated hierarchy paths of GameObjects to select " + "(e.g. 'Player,World/Terrain'). Leave empty to skip.")] string objectPaths = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var resolved = new List<Object>();
                var warnings = new StringBuilder();


                if (!string.IsNullOrWhiteSpace(instanceIds))
                {
                    string[] idParts = instanceIds.Split(',');

                    for (int i = 0; i < idParts.Length; i++)
                    {
                        string trimmed = idParts[i].Trim();

                        if (string.IsNullOrWhiteSpace(trimmed))
                        {
                            continue;
                        }

                        if (!int.TryParse(trimmed, out int id))
                        {
                            warnings.AppendLine($"  Warning: '{trimmed}' is not a valid integer instance ID — skipped.");
                            continue;
                        }

                        var obj = EditorUtility.EntityIdToObject(id);

                        if (obj == null)
                        {
                            warnings.AppendLine($"  Warning: No object found for instanceId {id} — skipped.");
                            continue;
                        }

                        bool alreadyAdded = false;

                        for (int j = 0; j < resolved.Count; j++)
                        {
                            if (resolved[j].GetInstanceID() == id) { alreadyAdded = true; break; }
                        }

                        if (!alreadyAdded)
                        {
                            resolved.Add(obj);
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(objectPaths))
                {
                    string[] pathParts = objectPaths.Split(',');

                    for (int i = 0; i < pathParts.Length; i++)
                    {
                        string trimmed = pathParts[i].Trim();

                        if (string.IsNullOrWhiteSpace(trimmed))
                        {
                            continue;
                        }

                        var go = GameObject.Find(trimmed);

                        if (go == null)
                        {
                            warnings.AppendLine($"  Warning: GameObject '{trimmed}' not found — skipped.");
                            continue;
                        }

                        bool alreadyAdded = false;
                        int goId = go.GetInstanceID();

                        for (int j = 0; j < resolved.Count; j++)
                        {
                            if (resolved[j].GetInstanceID() == goId) { alreadyAdded = true; break; }
                        }

                        if (!alreadyAdded)
                        {
                            resolved.Add(go);
                        }
                    }
                }

                if (resolved.Count == 0)
                {
                    return ToolResponse.Error("No valid GameObjects could be resolved from the provided inputs.\n" + warnings.ToString());
                }

                Selection.objects = resolved.ToArray();

                var sb = new StringBuilder();
                sb.AppendLine($"Selection set ({resolved.Count} object(s)):");

                for (int i = 0; i < resolved.Count; i++)
                {
                    sb.AppendLine($"  {resolved[i].name} (instanceId: {resolved[i].GetInstanceID()})");
                }

                if (warnings.Length > 0)
                {
                    sb.AppendLine();
                    sb.Append(warnings);
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}