#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Graphics
    {
        #region TOOL METHODS

        /// <summary>
        /// Sets the active quality level by numeric index or by name (case-insensitive).
        /// </summary>
        /// <param name="level">Quality level index (e.g. "2") or name (e.g. "Ultra").</param>
        /// <returns>Confirmation text with the index and name of the newly active quality level, or an error.</returns>
        [McpTool("graphics-set-quality", Title = "Graphics / Set Quality Level")]
        [Description("Sets the active quality level by index or name.")]
        public ToolResponse SetQuality(
            [Description("Quality level index (0-based) or name (e.g. 'Ultra', 'Medium').")]
            string level
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (int.TryParse(level, out int idx))
                {
                    if (idx < 0 || idx >= QualitySettings.names.Length)
                    {
                        return ToolResponse.Error($"Quality level {idx} out of range (0-{QualitySettings.names.Length - 1}).");
                    }

                    QualitySettings.SetQualityLevel(idx);
                    return ToolResponse.Text($"Quality level set to [{idx}] {QualitySettings.names[idx]}.");
                }

                for (int i = 0; i < QualitySettings.names.Length; i++)
                {
                    if (QualitySettings.names[i].Equals(level, System.StringComparison.OrdinalIgnoreCase))
                    {
                        QualitySettings.SetQualityLevel(i);
                        return ToolResponse.Text($"Quality level set to [{i}] {QualitySettings.names[i]}.");
                    }
                }

                var names = QualitySettings.names;
                var sb2 = new StringBuilder();

                for (int j = 0; j < names.Length; j++)
                {
                    if (j > 0)
                    {
                        sb2.Append(", ");
                    }

                    sb2.Append(names[j]);
                }
                string levelList = sb2.ToString();
                return ToolResponse.Error($"Quality level '{level}' not found. Available: {levelList}");
            });
        }

        #endregion
    }
}