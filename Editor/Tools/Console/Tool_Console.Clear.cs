#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Console
    {
        #region TOOL METHODS

        /// <summary>
        /// Clears all entries from the Unity Console by invoking <c>UnityEditor.LogEntries.Clear()</c>
        /// via reflection. This is equivalent to clicking the "Clear" button in the Console window.
        /// </summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the console was cleared, or an error if the
        /// internal API is unavailable in the current Unity version.
        /// </returns>
        [McpTool("console-clear", Title = "Console / Clear")]
        [Description("Clears all entries from the Unity Console. Equivalent to clicking the Clear button in the Console window.")]
        public ToolResponse Clear()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var logEntriesType = Type.GetType(LOG_ENTRIES_TYPE_NAME);

                if (logEntriesType == null)
                {
                    return ToolResponse.Error("Could not resolve 'UnityEditor.LogEntries'. This may indicate a Unity version " + "incompatibility. The internal API is not available in your Unity installation.");
                }

                var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);

                if (clearMethod == null)
                {
                    return ToolResponse.Error("Could not find 'UnityEditor.LogEntries.Clear()'. This Unity version may not " + "expose this method via the internal API.");
                }

                try
                {
                    clearMethod.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Failed to clear console: {ex.Message}");
                }

                return ToolResponse.Text("Unity Console cleared successfully.");
            });
        }

        #endregion
    }
}