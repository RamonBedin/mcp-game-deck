#nullable enable

using UnityEditor;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Builds the per-state tooltip text shown on the toolbar pin.
    /// </summary>
    /// <remarks>
    /// Pure function — output depends only on the arguments plus the live
    /// <see cref="EditorApplication"/> busy flags read inside <see cref="GetBusyReason"/>.
    /// Texts follow the table from feature 07's design doc, decision #5.
    /// </remarks>
    public static class PinTooltip
    {
        #region CONSTANTS

        private const string UPDATE_LINE_PREFIX = "Update available";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Resolves a short human-readable label describing why the editor is currently
        /// considered busy, prioritising compilation, play-mode transitions, and asset
        /// import in that order. Falls back to a generic <c>"busy"</c> when no specific
        /// state matches.
        /// </summary>
        /// <returns>A lowercase reason string suitable for inclusion in status messages.</returns>
        private static string GetBusyReason()
        {
            if (EditorApplication.isCompiling)
            {
                return "compiling";
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return "play mode";
            }

            if (EditorApplication.isUpdating)
            {
                return "importing assets";
            }

            return "busy";
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns the tooltip text for the given pin state.
        /// </summary>
        /// <param name="status">Current pin status (drives the base sentence).</param>
        /// <param name="port">Configured MCP server port; interpolated into the
        /// <see cref="EPinStatus.BIND_FAILURE"/> message.</param>
        /// <param name="updateAvailable">When <c>true</c>, an update line is appended
        /// after the base sentence.</param>
        /// <param name="updateVersion">Version string for the update line. Pass empty
        /// to omit the version suffix.</param>
        /// <returns>The fully-formatted tooltip text.</returns>
        public static string GetText(EPinStatus status, int port, bool updateAvailable, string updateVersion)
        {
            var baseText = status switch
            {
                EPinStatus.CONNECTED => "MCP Game Deck connected. Click to open chat.",
                EPinStatus.BUSY => $"Unity is busy ({GetBusyReason()}). App still connected.",
                EPinStatus.NOT_RUNNING => "MCP Game Deck app is not running. Click to launch.",
                EPinStatus.BIND_FAILURE => $"MCP Game Deck can't bind port {port}. Another Unity instance may already be using it.",
                EPinStatus.NOT_INSTALLED => "First time? Click to install MCP Game Deck app (~9 MB download).",
                _ => "MCP Game Deck",
            };

            if (!updateAvailable)
            {
                return baseText;
            }
            //
            var versionSuffix = string.IsNullOrEmpty(updateVersion) ? "." : $": v{updateVersion}.";
            return $"{baseText}\n{UPDATE_LINE_PREFIX}{versionSuffix}";
        }

        #endregion

    }
}