#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Editor
    {
        #region TOOL METHODS

        /// <summary>
        /// Enters Play mode in the Unity Editor.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming Play mode was entered,
        /// or a text notice if the Editor is already playing.
        /// </returns>
        [McpTool("editor-play", Title = "Editor / Play")]
        [Description("Enters Play mode in the Unity Editor.")]
        public ToolResponse Play()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (EditorApplication.isPlaying)
                {
                    return ToolResponse.Text("Already in Play mode.");
                }

                EditorApplication.isPlaying = true;
                return ToolResponse.Text("Entering Play mode.");
            });
        }

        /// <summary>
        /// Toggles pause state during Play mode.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> indicating the new pause state ("Paused." or "Resumed."),
        /// or an error if the Editor is not currently in Play mode.
        /// </returns>
        [McpTool("editor-pause", Title = "Editor / Pause")]
        [Description("Toggles pause state during Play mode.")]
        public ToolResponse Pause()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!EditorApplication.isPlaying)
                {
                    return ToolResponse.Error("Not in Play mode.");
                }

                EditorApplication.isPaused = !EditorApplication.isPaused;
                return ToolResponse.Text(EditorApplication.isPaused ? "Paused." : "Resumed.");
            });
        }

        /// <summary>
        /// Stops Play mode and returns to Edit mode.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming Play mode was stopped,
        /// or a text notice if the Editor is already in Edit mode.
        /// </returns>
        [McpTool("editor-stop", Title = "Editor / Stop")]
        [Description("Stops Play mode and returns to Edit mode.")]
        public ToolResponse StopPlay()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!EditorApplication.isPlaying)
                {
                    return ToolResponse.Text("Already in Edit mode.");
                }

                EditorApplication.isPlaying = false;
                return ToolResponse.Text("Stopping Play mode.");
            });
        }

        #endregion
    }
}