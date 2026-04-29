#nullable enable

using System;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Temporary editor menu item used to validate
    /// <see cref="PinBinaryManager.DownloadAsync"/> during task 4.3 development.
    /// </summary>
    /// <remarks>
    /// Delete this file (and its <c>.meta</c>) after task 4.5 wires the real launcher —
    /// the click-to-launch flow on the pin replaces this manual trigger and the menu
    /// item should not ship in v2.0.
    /// </remarks>
    internal static class PinDownloadTestMenu
    {
        #region CONSTANTS

        private const string MENU_PATH = "MCP Game Deck/Internal/Test Download";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Editor menu handler that triggers a one-shot download of the binary for the
        /// currently-loaded package version and logs each step (start, progress, result)
        /// via <see cref="McpLogger"/> so the validation steps in task 4.3 can be run
        /// without extra instrumentation.
        /// </summary>
        [MenuItem(MENU_PATH)]
        private static async void RunTestDownload()
        {
            try
            {
                var version = PinBinaryManager.GetCurrentVersion();

                if (string.IsNullOrEmpty(version))
                {
                    McpLogger.Error("[Pin] Test Download: package version unavailable");
                    return;
                }

                McpLogger.Info($"[Pin] Test Download starting for v{version}");

                var progress = new Progress<float>(p => McpLogger.Info($"[Pin] Test Download progress: {p:P0}"));
                var result = await PinBinaryManager.DownloadAsync(version, progress);
                McpLogger.Info($"[Pin] Test Download result: {result}");
            }
            catch (Exception e)
            {
                McpLogger.Error("[Pin] Test Download exception", e);
            }
        }

        #endregion
    }
}