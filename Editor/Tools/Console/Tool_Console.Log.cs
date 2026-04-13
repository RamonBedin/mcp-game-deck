#nullable enable
using System;
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Console
    {
        #region TOOL METHODS

        /// <summary>
        /// Sends a message to the Unity Console with the specified log level.
        /// </summary>
        /// <param name="message">The text to log.</param>
        /// <param name="type">Log level: "info", "warning", or "error". Default "info".</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the logged message.</returns>
        [McpTool("console-log", Title = "Console / Log")]
        [Description("Sends a message to the Unity Console (Info, Warning, or Error).")]
        public ToolResponse Log(
            [Description("The message to log.")] string message,
            [Description("Log type: 'info', 'warning', or 'error'. Default 'info'.")] string type = "info"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return ToolResponse.Error("message is required.");
                }

                string prefixed = $"[Game Deck] {message}";

                if (string.Equals(type, "warning", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogWarning(prefixed);
                }
                else if (string.Equals(type, "error", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError(prefixed);
                }
                else
                {
                    Debug.Log(prefixed);
                }

                return ToolResponse.Text($"Logged {type}: {message}");
            });
        }

        #endregion
    }
}