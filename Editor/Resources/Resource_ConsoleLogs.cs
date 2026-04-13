#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that captures and retrieves Unity console log entries.
    /// Registers a log callback on first use and keeps a rolling buffer of the last 500 entries.
    /// </summary>
    [McpResourceType]
    public class Resource_ConsoleLogs
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const int MAX_RETURNED_ENTRIES = 50;
        private const int MAX_STACK_TRACE_LINES = 5;
        private const int MAX_BUFFER_SIZE = 500;
        private const string FILTER_ERROR = "error";
        private const string FILTER_WARNING = "warning";
        private const string FILTER_LOG = "log";
        private const string FILTER_INFO = "info";
        private const string FILTER_EXCEPTION = "exception";
        private const string FILTER_ASSERT = "assert";

        #endregion

        #region FIELDS

        private static readonly List<LogEntry> _logEntries = new();
        private static bool _registered;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Subscribes to <see cref="Application.logMessageReceived"/> if not already registered.
        /// Called lazily before the first log query to avoid hooking the callback unnecessarily.
        /// </summary>
        private static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            Application.logMessageReceived += HandleOnLogMessage;
        }

        /// <summary>
        /// Callback for <see cref="Application.logMessageReceived"/>. Appends the log entry
        /// to the buffer and trims it to <see cref="MAX_BUFFER_SIZE"/> when exceeded.
        /// </summary>
        /// <param name="condition">The log message text.</param>
        /// <param name="stackTrace">The associated stack trace, if any.</param>
        /// <param name="type">The Unity log type (Log, Warning, Error, etc.).</param>
        private static void HandleOnLogMessage(string condition, string stackTrace, LogType type)
        {
            _logEntries.Add(new LogEntry
            {
                _message = condition,
                _stackTrace = stackTrace,
                _type = type,
                _timestamp = DateTime.Now
            });

            if (_logEntries.Count > MAX_BUFFER_SIZE)
            {
                _logEntries.RemoveRange(0, _logEntries.Count - MAX_BUFFER_SIZE);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns recent console log entries, optionally filtered by log type, newest first.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <param name="logType">Filter by log level: 'error', 'warning', 'log', 'exception', 'assert'. Empty returns all types.</param>
        /// <returns>An array of resource content entries containing the log output as plain text.</returns>
        [McpResource
        (
            Name = "Console Logs",
            Route = "mcp-game-deck://console-logs/{logType}",
            MimeType = "text/plain",
            Description = "Retrieves Unity console log entries, optionally filtered by type " +
                "(error, warning, log). Returns newest first with timestamp and stack trace."
        )]
        public ResourceResponse[] GetConsoleLogs(string uri, string logType)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                EnsureRegistered();
                LogType? targetType = null;

                if (!string.IsNullOrWhiteSpace(logType))
                {
                    targetType = logType.ToLowerInvariant() switch
                    {
                        FILTER_ERROR => LogType.Error,
                        FILTER_WARNING => LogType.Warning,
                        FILTER_LOG or FILTER_INFO => LogType.Log,
                        FILTER_EXCEPTION => LogType.Exception,
                        FILTER_ASSERT => LogType.Assert,
                        _ => (LogType?)null
                    };
                }

                var result = new List<LogEntry>();

                for (int i = _logEntries.Count - 1; i >= 0 && result.Count < MAX_RETURNED_ENTRIES; i--)
                {
                    var entry = _logEntries[i];

                    if (targetType == null || entry._type == targetType.Value)
                    {
                        result.Add(entry);
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Console Logs ({result.Count} entries, filter: '{logType}'):");
                sb.AppendLine();

                foreach (var entry in result)
                {
                    sb.AppendLine($"[{entry._timestamp:HH:mm:ss}] [{entry._type}] {entry._message}");

                    if (entry._type == LogType.Error || entry._type == LogType.Exception)
                    {
                        if (!string.IsNullOrWhiteSpace(entry._stackTrace))
                        {
                            var lines = entry._stackTrace.Split('\n');
                            int lineLimit = lines.Length < MAX_STACK_TRACE_LINES ? lines.Length : MAX_STACK_TRACE_LINES;

                            for (int i = 0; i < lineLimit; i++)
                            {
                                sb.AppendLine($"  {lines[i].Trim()}");
                            }
                        }
                    }

                    sb.AppendLine();
                }

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()).MakeArray();
            });
        }

        #endregion

        #region NESTED TYPES

        /// <summary>
        /// Represents a single captured console log entry with message, stack trace, type, and timestamp.
        /// </summary>
        private struct LogEntry
        {
            public string _message;
            public string _stackTrace;
            public LogType _type;
            public DateTime _timestamp;
        }

        #endregion
    }
}
