#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Asset
    {
        #region SET LABELS

        /// <summary>
        /// Writes labels to an asset. By default appends to existing labels (deduplicated).
        /// Pass clearExisting=true to replace the entire label set; pass labelsJson="[]" with
        /// clearExisting=true to remove all labels.
        /// </summary>
        /// <param name="assetPath">Project-relative asset path (e.g. 'Assets/Prefabs/Boss.prefab').</param>
        /// <param name="labelsJson">JSON array of label strings (e.g. '["Boss","Level3"]'). Empty array clears labels when clearExisting=true.</param>
        /// <param name="clearExisting">When true, replaces all existing labels with the provided set. When false (default), appends the provided labels to the existing set and deduplicates.</param>
        /// <returns>A <see cref="ToolResponse"/> with the final label set, or an error when the asset is missing or labelsJson is malformed.</returns>
        [McpTool("asset-set-labels", Title = "Asset / Set Labels")]
        [Description("Writes labels (tags) on an asset for later search via asset-find l:LabelName. By default APPENDS to existing labels (deduplicated). Set clearExisting=true to REPLACE the full label set; pass labelsJson=\"[]\" with clearExisting=true to clear all labels. Read labels via asset-get-info.")]
        public ToolResponse SetLabels(
            [Description("Project-relative asset path (e.g. 'Assets/Prefabs/Boss.prefab').")] string assetPath,
            [Description("JSON array of label strings (e.g. '[\"Boss\",\"Level3\"]'). Use '[]' with clearExisting=true to clear all labels.")] string labelsJson,
            [Description("When true, replaces all existing labels with the provided set. When false (default), appends to the existing set (deduplicated).")] bool clearExisting = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    return ToolResponse.Error("assetPath is required.");
                }

                if (labelsJson == null)
                {
                    return ToolResponse.Error("labelsJson is required. Pass '[]' to clear (with clearExisting=true).");
                }

                if (!assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = "Assets/" + assetPath;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                if (asset == null)
                {
                    return ToolResponse.Error($"Asset not found at '{assetPath}'.");
                }

                string[]? incoming = ParseStringArrayJson(labelsJson);

                if (incoming == null)
                {
                    return ToolResponse.Error("labelsJson must be a JSON array of strings (e.g. '[\"Boss\",\"Level3\"]' or '[]').");
                }

                string[] finalLabels;

                if (clearExisting)
                {
                    finalLabels = incoming;
                }
                else
                {
                    string[] existing = AssetDatabase.GetLabels(asset);
                    var merged = new List<string>(existing.Length + incoming.Length);
                    var seen = new HashSet<string>(System.StringComparer.Ordinal);

                    for (int i = 0; i < existing.Length; i++)
                    {
                        if (seen.Add(existing[i]))
                        {
                            merged.Add(existing[i]);
                        }
                    }

                    for (int i = 0; i < incoming.Length; i++)
                    {
                        if (seen.Add(incoming[i]))
                        {
                            merged.Add(incoming[i]);
                        }
                    }

                    finalLabels = merged.ToArray();
                }

                AssetDatabase.SetLabels(asset, finalLabels);
                AssetDatabase.SaveAssets();

                if (finalLabels.Length == 0)
                {
                    return ToolResponse.Text($"Cleared all labels on '{assetPath}'.");
                }

                return ToolResponse.Text($"Labels on '{assetPath}' set to: [{string.Join(", ", finalLabels)}].");
            });
        }

        /// <summary>
        /// Minimal JSON string-array parser tolerant of the inputs LLMs typically produce:
        /// double-quoted strings, comma-separated, optional whitespace, empty array allowed.
        /// Returns <c>null</c> on malformed input.
        /// </summary>
        /// <param name="json">The JSON array text.</param>
        /// <returns>The parsed array, or <c>null</c> when the input is not a valid string array.</returns>
        private static string[]? ParseStringArrayJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            string trimmed = json.Trim();

            if (trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']')
            {
                return null;
            }

            string inner = trimmed[1..^1].Trim();

            if (inner.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var result = new List<string>();
            int i = 0;

            while (i < inner.Length)
            {
                while (i < inner.Length && (inner[i] == ',' || char.IsWhiteSpace(inner[i])))
                {
                    i++;
                }

                if (i >= inner.Length)
                {
                    break;
                }

                if (inner[i] != '"')
                {
                    return null;
                }

                i++;
                var sb = new System.Text.StringBuilder();

                while (i < inner.Length && inner[i] != '"')
                {
                    if (inner[i] == '\\' && i + 1 < inner.Length)
                    {
                        sb.Append(inner[i + 1]);
                        i += 2;
                        continue;
                    }

                    sb.Append(inner[i]);
                    i++;
                }

                if (i >= inner.Length)
                {
                    return null;
                }

                result.Add(sb.ToString());
                i++;
            }

            return result.ToArray();
        }

        #endregion
    }
}