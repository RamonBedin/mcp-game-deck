#nullable enable

using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Editor-only dialog helpers used by the pin's download + launch flow,
    /// and the home for any future dialogs the pin needs to surface.
    /// </summary>
    /// <remarks>
    /// Surfaces here are produced by <see cref="PinBinaryManager.DownloadAsync"/>
    /// errors and by <see cref="PinLauncher"/> launch failures (task 4.5). Each
    /// error dialog is recoverable: the network and hash variants return whether
    /// the caller should retry, and the launch variant offers a path to the issue
    /// tracker. Progress is rendered in <see cref="PinDownloadProgressWindow"/>
    /// — opened via <see cref="ShowProgress"/> and closed by the caller when the
    /// download finishes.
    /// </remarks>
    public static class PinDialogs
    {
        #region CONSTANTS

        private const string DIALOG_TITLE_NETWORK = "Download failed";
        private const string DIALOG_TITLE_HASH = "Integrity check failed";
        private const string DIALOG_TITLE_LAUNCH = "App launch failed";

        private const string BUTTON_RETRY = "Retry";
        private const string BUTTON_CANCEL = "Cancel";
        private const string BUTTON_OPEN_IN_BROWSER = "Open in browser";
        private const string BUTTON_OK = "OK";
        private const string BUTTON_REPORT_ISSUE = "Report issue";

        private const string GITHUB_ISSUES_URL = "https://github.com/RamonBedin/mcp-game-deck/issues";

        private const string MESSAGE_NETWORK_FORMAT = "MCP Game Deck couldn't download the app binary.\n\n" + "URL: {0}\n\n" + "Check your network connection and retry, or open the URL in your browser to download manually.";
        private const string MESSAGE_HASH = "The downloaded file failed its SHA-256 integrity check.\n\n" + "The corrupt download has been deleted. Retry to download a fresh copy.";
        private const string MESSAGE_LAUNCH_FORMAT = "The MCP Game Deck app exited unexpectedly with code {0}.\n\n" + "If this persists, please file an issue.";
        private const string MESSAGE_LAUNCH_FAILED_TO_START = "The MCP Game Deck app could not start.\n\n" + "If this persists, please file an issue.";

        public const int LAUNCH_FAILED_TO_START = int.MinValue;

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Opens a small utility window with a progress bar that the caller drives
        /// either by setting <see cref="PinDownloadProgressWindow.Progress"/>
        /// directly or by passing <see cref="PinDownloadProgressWindow.AsProgress"/>
        /// to <see cref="PinBinaryManager.DownloadAsync"/>.
        /// </summary>
        /// <returns>The newly-opened progress window. Caller is responsible for
        /// calling <see cref="EditorWindow.Close"/> when the download finishes
        /// (success or failure).</returns>
        public static PinDownloadProgressWindow ShowProgress()
        {
            return PinDownloadProgressWindow.Open();
        }

        /// <summary>
        /// Shows a modal dialog explaining a network failure and asks whether the
        /// caller should retry. The "Open in browser" button opens
        /// <paramref name="url"/> in the user's default browser and returns
        /// <c>false</c> (so the caller does not auto-retry — the user is taking
        /// over the download manually).
        /// </summary>
        /// <param name="url">Release URL the failed request targeted; shown in the
        /// message body and used by the "Open in browser" button.</param>
        /// <returns><c>true</c> when the user clicks Retry; <c>false</c> on Cancel
        /// or after Open in browser.</returns>
        public static bool ShowNetworkError(string url)
        {
            var message = string.Format(MESSAGE_NETWORK_FORMAT, url);
            var choice = EditorUtility.DisplayDialogComplex(
                DIALOG_TITLE_NETWORK,
                message,
                BUTTON_RETRY,
                BUTTON_CANCEL,
                BUTTON_OPEN_IN_BROWSER);

            if (choice == 2)
            {
                Application.OpenURL(url);
                return false;
            }

            return choice == 0;
        }

        /// <summary>
        /// Shows a modal dialog explaining a SHA-256 integrity failure and asks
        /// whether the caller should retry. The corrupt temp file is already
        /// deleted by <see cref="PinBinaryManager.DownloadAsync"/> before this
        /// dialog is shown.
        /// </summary>
        /// <returns><c>true</c> when the user clicks Retry; <c>false</c> on Cancel.</returns>
        public static bool ShowHashMismatch()
        {
            return EditorUtility.DisplayDialog(
                DIALOG_TITLE_HASH,
                MESSAGE_HASH,
                BUTTON_RETRY,
                BUTTON_CANCEL);
        }

        /// <summary>
        /// Shows a modal dialog reporting that the app process exited unexpectedly,
        /// or — when <paramref name="exitCode"/> equals
        /// <see cref="LAUNCH_FAILED_TO_START"/> — that it could not be launched at
        /// all. The "Report issue" button opens the GitHub issue tracker; the dialog
        /// otherwise dismisses without further action.
        /// </summary>
        /// <param name="exitCode">Exit code reported by
        /// <see cref="System.Diagnostics.Process"/>, or
        /// <see cref="LAUNCH_FAILED_TO_START"/> when the process never started.</param>
        public static void ShowLaunchFailed(int exitCode)
        {
            var message = exitCode == LAUNCH_FAILED_TO_START ? MESSAGE_LAUNCH_FAILED_TO_START : string.Format(MESSAGE_LAUNCH_FORMAT, exitCode);
            var reportIssue = !EditorUtility.DisplayDialog(DIALOG_TITLE_LAUNCH, message, BUTTON_OK, BUTTON_REPORT_ISSUE);

            if (reportIssue)
            {
                Application.OpenURL(GITHUB_ISSUES_URL);
            }
        }

        #endregion
    }
}