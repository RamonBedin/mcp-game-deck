#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP health-check tool that confirms the MCP Game Deck package is loaded and
    /// its tools are registered in the MCP tool registry.
    /// </summary>
    [McpToolType]
    public partial class Tool_Ping
    {
        #region TOOL METHODS

        /// <summary>
        /// Returns a pong response containing the package version, Unity version, and runtime platform.
        /// Optionally echoes a caller-supplied message back in the response.
        /// </summary>
        /// <param name="message">Optional message to echo back in the pong response.</param>
        /// <returns>Text confirming the package is loaded, with optional echo of the input message.</returns>
        [McpTool("specialist-ping", Title = "Specialist / Ping")]
        [Description("Health check tool for the MCP Game Deck extension package. " + "Returns a pong response confirming the package is loaded and tools are registered. " + "Use this to verify the extension is working correctly.")]
        public ToolResponse Ping(
            [Description("Optional message to echo back in the response.")] string? message = null
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var response = $"pong — MCP Game Deck v1.0.0 loaded. " + $"Unity {Application.unityVersion}, " + $"Platform: {Application.platform}";

                if (!string.IsNullOrEmpty(message))
                {
                    response += $", Echo: {message}";
                }

                return ToolResponse.Text(response);
            });
        }

        #endregion
    }
}