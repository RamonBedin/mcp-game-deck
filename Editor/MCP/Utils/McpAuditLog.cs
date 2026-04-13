#nullable enable
using System;
using System.IO;

namespace GameDeck.MCP.Utils
{
    /// <summary>
    /// Writes structured audit log entries to <c>Library/GameDeck/audit.log</c>.
    /// Each entry is timestamped in UTC ISO 8601 format. Thread-safe via file locking.
    /// Logs to a file instead of the Unity Console to avoid spamming during rapid
    /// sequential tool calls.
    /// </summary>
    public static class McpAuditLog
    {
        #region CONSTANTS

        private const string LOG_DIR = "Library/GameDeck";
        private const string LOG_FILE = "Library/GameDeck/audit.log";
        private const long MAX_LOG_SIZE = 5 * 1024 * 1024;

        #endregion

        #region FIELDS

        private static readonly object _lock = new();

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Renames the current log file to <c>audit.log.old</c> when it exceeds
        /// <see cref="MAX_LOG_SIZE"/>. The previous <c>.old</c> file is overwritten.
        /// </summary>
        private static void RotateIfNeeded()
        {
            if (!File.Exists(LOG_FILE))
            {
                return;
            }

            if (new FileInfo(LOG_FILE).Length < MAX_LOG_SIZE)
            {
                return;
            }

            string oldPath = LOG_FILE + ".old";

            if (File.Exists(oldPath))
            {
                File.Delete(oldPath);
            }

            File.Move(LOG_FILE, oldPath);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Appends a timestamped audit entry to the log file.
        /// Rotates the file when it exceeds <see cref="MAX_LOG_SIZE"/>.
        /// </summary>
        /// <param name="message">The audit message to log.</param>
        public static void Write(string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(LOG_DIR);
                    RotateIfNeeded();

                    string entry = $"{DateTime.UtcNow:o} {message}\n";
                    File.AppendAllText(LOG_FILE, entry);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[MCP] Audit log write failed: {ex.Message}");
            }
        }

        #endregion
    }
}
