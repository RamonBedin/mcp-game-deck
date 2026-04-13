#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for ProBuilder mesh editing. Uses reflection to avoid compile-time
    /// dependency on com.unity.probuilder. Returns friendly errors when not installed.
    /// </summary>
    [McpToolType]
    public partial class Tool_ProBuilder
    {
        #region TOOL METHODS

        /// <summary>Checks if ProBuilder is installed.</summary>
        /// <returns>A <see cref="ToolResponse"/> with version info or an error if ProBuilder is not installed.</returns>
        [McpTool("probuilder-ping", Title = "ProBuilder / Ping", ReadOnlyHint = true)]
        [Description("Checks if ProBuilder is installed and returns version info.")]
        public ToolResponse Ping()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                return ToolResponse.Text("ProBuilder is installed and available.");
            });
        }

        #endregion
    }
}