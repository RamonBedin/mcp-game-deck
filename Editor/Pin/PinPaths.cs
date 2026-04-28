#nullable enable

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Cross-platform install path helpers for the pin's external Tauri app binary.
    /// </summary>
    /// <remarks>
    /// Stub introduced in task 2.2 so the pin's state machine can branch on
    /// "binary present on disk" without depending on the full path resolver. The real
    /// implementation (resolved <c>%APPDATA%</c> / <c>~/Library</c> / <c>~/.local/share</c>
    /// roots, per-version subfolders, .sha256 sidecar paths) lands in task 4.1.
    /// </remarks>
    public static class PinPaths
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Returns the resolved filesystem path of the installed app binary for the
        /// current package version, or <c>null</c> when no binary is present.
        /// </summary>
        /// <returns>Absolute path to the binary, or <c>null</c> if not installed.</returns>
        /// <remarks>
        /// Always returns <c>null</c> until task 4.1 wires real path resolution.
        /// Callers should treat <c>null</c> as "not installed" and surface the
        /// gray <see cref="EPinStatus.NOT_INSTALLED"/> status in that case.
        /// </remarks>
        public static string? GetBinaryPath()
        {
            return null;
        }

        #endregion
    }
}