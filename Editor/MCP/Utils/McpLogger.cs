#nullable enable

using System;
using UnityEngine;

namespace GameDeck.MCP.Utils
{
    /// <summary>
    /// Centralised logging utility for the MCP Game Deck framework.
    /// All output is prefixed with <c>[MCP]</c> to make log messages instantly identifiable
    /// in the Unity Console among messages from other packages.
    /// </summary>
    /// <remarks>
    /// Thin wrapper around <see cref="Debug"/> — zero allocation overhead on top of what Unity
    /// itself does. Use this instead of calling <see cref="Debug"/> directly in MCP code so that
    /// the prefix is applied consistently and can be changed in one place.
    /// </remarks>
    public static class McpLogger
    {
        #region CONSTANTS

        private const string PREFIX = "[MCP]";

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Logs an informational message to the Unity Console.
        /// </summary>
        /// <param name="message">The message text. Must not be <c>null</c>.</param>
        /// <remarks>
        /// Maps to <see cref="Debug.Log(object)"/>. Visible in the Unity Console when the
        /// "Log" filter is active (white icon).
        /// </remarks>
        public static void Info(string message)
        {
            UnityEngine.Debug.Log($"{PREFIX} {message}");
        }

        /// <summary>
        /// Logs an error message to the Unity Console.
        /// </summary>
        /// <param name="message">The message text. Must not be <c>null</c>.</param>
        /// <remarks>
        /// Maps to <see cref="UnityEngine.Debug.LogError(object)"/>. Visible when the "Error" filter is
        /// active (red circle icon). Increments the Editor's error count.
        /// </remarks>
        public static void Error(string message)
        {
            UnityEngine.Debug.LogError($"{PREFIX} {message}");
        }

        /// <summary>
        /// Logs an error message together with exception details to the Unity Console.
        /// </summary>
        /// <param name="message">
        /// Contextual description of what operation failed. Must not be <c>null</c>.
        /// </param>
        /// <param name="ex">
        /// The exception that caused the failure. Must not be <c>null</c>.
        /// Only <see cref="Exception.Message"/> is logged; the full stack trace is available
        /// via <see cref="UnityEngine.Debug.LogException(Exception)"/> if needed.
        /// </param>
        /// <remarks>
        /// The logged string format is: <c>[MCP] {message}: {ex.Message}</c>.
        /// Maps to <see cref="UnityEngine.Debug.LogError(object)"/> rather than
        /// <see cref="UnityEngine.Debug.LogException(Exception)"/> so the message and exception detail
        /// appear as a single Console entry.
        /// </remarks>
        public static void Error(string message, Exception ex)
        {
            UnityEngine.Debug.LogError($"{PREFIX} {message}: {ex.Message}");
        }

        /// <summary>
        /// Logs a verbose/debug message to the Unity Console.
        /// Intended for development-time tracing that should be stripped or silenced in release.
        /// </summary>
        /// <param name="message">The message text. Must not be <c>null</c>.</param>
        /// <remarks>
        /// Maps to <see cref="Debug.Log(object)"/> and is always visible when the "Log" filter
        /// is active. Consider guarding call sites with <c>#if UNITY_EDITOR</c> or a compile
        /// constant to suppress verbose output in production builds.
        /// </remarks>
        public static void Debug(string message)
        {
            UnityEngine.Debug.Log($"{PREFIX} [DEBUG] {message}");
        }

        #endregion
    }
}