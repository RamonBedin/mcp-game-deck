#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for reading, writing, and clearing the Unity Console via the
    /// internal <c>UnityEditor.LogEntries</c> reflection API
    /// </summary>
    [McpToolType]
    public partial class Tool_Console
    {
        #region Constants

        private const string LOG_ENTRIES_TYPE_NAME = "UnityEditor.LogEntries,UnityEditor";
        private const string LOG_ENTRY_TYPE_NAME = "UnityEditor.LogEntry,UnityEditor";

        #endregion

        #region TOOL METHODS

        /// <summary>
        /// Retrieves entries from the Unity Console using reflection against the internal
        /// <c>UnityEditor.LogEntries</c> API. Supports filtering by log type and optional text search.
        /// </summary>
        /// <param name="type">
        /// Log type filter: "all", "log", "warning", or "error". Defaults to "all".
        /// </param>
        /// <param name="count">Maximum number of entries to return. Defaults to 20.</param>
        /// <param name="filterText">
        /// Optional substring filter applied to the log message. Case-insensitive. Empty means no filter.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the matching console entries, or an error if the
        /// internal API is not available in the current Unity version.
        /// </returns>
        [McpTool("console-get-logs", Title = "Console / Get Logs", ReadOnlyHint = true)]
        [Description("Retrieves entries from the Unity Console. Supports filtering by type (all/log/warning/error) and an optional text search substring. Returns up to 'count' entries.")]
        public ToolResponse GetLogs(
            [Description("Log type filter: 'all', 'log', 'warning', or 'error'. Default is 'all'.")] string type = "all",
            [Description("Maximum number of log entries to return. Default is 20.")] int count = 20,
            [Description("Optional case-insensitive substring to filter log messages. Leave empty to return all.")] string filterText = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (count <= 0)
                {
                    return ToolResponse.Error("count must be greater than zero.");
                }

                var logEntriesType = Type.GetType(LOG_ENTRIES_TYPE_NAME);

                if (logEntriesType == null)
                {
                    return ToolResponse.Error("Could not resolve 'UnityEditor.LogEntries'. This may indicate a Unity version " + "incompatibility. The internal API is not available in your Unity installation.");
                }

                var logEntryType = Type.GetType(LOG_ENTRY_TYPE_NAME);

                if (logEntryType == null)
                {
                    return ToolResponse.Error("Could not resolve 'UnityEditor.LogEntry'. This may indicate a Unity version " + "incompatibility. The internal API is not available in your Unity installation.");
                }

                var startGettingEntriesMethod = logEntriesType.GetMethod("StartGettingEntries", BindingFlags.Static | BindingFlags.Public);
                var endGettingEntriesMethod = logEntriesType.GetMethod("EndGettingEntries", BindingFlags.Static | BindingFlags.Public);
                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);

                if (startGettingEntriesMethod == null || endGettingEntriesMethod == null || getCountMethod == null || getEntryInternalMethod == null)
                {
                    return ToolResponse.Error("One or more required methods on 'UnityEditor.LogEntries' could not be found " + "(StartGettingEntries / EndGettingEntries / GetCount / GetEntryInternal). " + "This Unity version may not support this API.");
                }

                var messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public);
                var modeField = logEntryType.GetField("mode", BindingFlags.Instance | BindingFlags.Public);
                var fileField = logEntryType.GetField("file", BindingFlags.Instance | BindingFlags.Public);
                var lineField = logEntryType.GetField("line", BindingFlags.Instance | BindingFlags.Public);

                if (messageField == null || modeField == null)
                {
                    return ToolResponse.Error("Could not find expected fields on 'UnityEditor.LogEntry' (message, mode). " + "This Unity version may not support this API.");
                }

                string normalizedType = type.Trim().ToLowerInvariant();
                string normalizedFilter = filterText.Trim().ToLowerInvariant();

                int totalCount;
                try
                {
                    totalCount = (int)getCountMethod.Invoke(null, null)!;
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Failed to get console log count: {ex.Message}");
                }

                if (totalCount == 0)
                {
                    return ToolResponse.Text("The Unity Console is empty.");
                }

                var entryInstance = Activator.CreateInstance(logEntryType);

                if (entryInstance == null)
                {
                    return ToolResponse.Error("Failed to create LogEntry instance via reflection.");
                }

                var sb  = new StringBuilder();
                int collected = 0;
                try
                {
                    startGettingEntriesMethod.Invoke(null, null);

                    for (int i = totalCount - 1; i >= 0 && collected < count; i--)
                    {
                        bool fetched = (bool)getEntryInternalMethod.Invoke(null, new object[] { i, entryInstance })!;

                        if (!fetched)
                        {
                            continue;
                        }

                        string message = messageField.GetValue(entryInstance) as string ?? string.Empty;
                        int mode = modeField.GetValue(entryInstance) is int m ? m : 0;
                        string category = CategoriseByMode(mode);

                        if (normalizedType != "all" && !string.Equals(category, normalizedType, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(normalizedFilter) && !message.ToLowerInvariant().Contains(normalizedFilter))
                        {
                            continue;
                        }

                        string file = fileField != null ? (fileField.GetValue(entryInstance) as string ?? "") : "";
                        int line = lineField != null && lineField.GetValue(entryInstance) is int l ? l : 0;

                        sb.AppendLine($"[{category.ToUpperInvariant()}] {message}");

                        if (!string.IsNullOrEmpty(file))
                        {
                            sb.AppendLine($"  at {file}:{line}");
                        }

                        sb.AppendLine();
                        collected++;
                    }
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Failed to read console entries: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        endGettingEntriesMethod.Invoke(null, null);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Console] EndGettingEntries cleanup failed: {ex.Message}");
                    }
                }

                if (collected == 0)
                {
                    return ToolResponse.Text($"No console entries matched type='{type}'" + (string.IsNullOrEmpty(filterText) ? "." : $" filter='{filterText}'."));
                }

                var header = new StringBuilder();
                header.AppendLine($"Console Logs ({collected} entries, type='{type}'" + (string.IsNullOrEmpty(filterText) ? "):" : $", filter='{filterText}'):"));
                header.AppendLine();
                header.Append(sb);

                return ToolResponse.Text(header.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPER

        /// <summary>
        /// Maps a LogEntry mode bitmask to a simple category string (log / warning / error).
        /// Based on UnityEditor internal ConsoleWindow.Mode values observed across Unity 2021–6.
        /// </summary>
        /// <param name="mode">The raw mode integer from the LogEntry.</param>
        /// <returns>One of: "error", "warning", "log".</returns>
        private static string CategoriseByMode(int mode)
        {
            const int ERROR_MASK = 0x001 | 0x002 | 0x010 | 0x020 | 0x200 | 0x400 | 0x800;
            const int WARNING_MASK = 0x100;

            if ((mode & ERROR_MASK) != 0)
            {
                return "error";
            }

            if ((mode & WARNING_MASK) != 0)
            {
                return "warning";
            }

            return "log";
        }

        #endregion
    }
}