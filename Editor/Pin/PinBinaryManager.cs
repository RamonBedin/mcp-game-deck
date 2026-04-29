#nullable enable

using System.IO;
using UnityEditor.PackageManager;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Discovers whether the Tauri app binary is installed on disk for the current
    /// package version, and resolves that version from the package metadata.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="PinPolling"/> to decide between
    /// <see cref="EPinStatus.NOT_RUNNING"/> (binary present, app not running) and
    /// <see cref="EPinStatus.NOT_INSTALLED"/> (binary missing) when the TCP probe
    /// fails. Also feeds the pin's About dialog with the real app version.
    /// </remarks>
    public static class PinBinaryManager
    {
        #region PUBLIC METHODS

        /// <summary>
        /// Checks whether the per-version Tauri app binary is present on disk.
        /// </summary>
        /// <param name="version">Package version string (e.g. <c>"1.1.0"</c>).
        /// Empty / whitespace versions return <c>false</c>.</param>
        /// <returns><c>true</c> if a file exists at
        /// <see cref="PinPaths.BinaryPath(string)"/> for <paramref name="version"/>;
        /// <c>false</c> otherwise.</returns>
        public static bool IsInstalled(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            return File.Exists(PinPaths.BinaryPath(version));
        }

        /// <summary>
        /// Resolves the currently-loaded package version from the assembly's
        /// <see cref="PackageInfo"/> metadata.
        /// </summary>
        /// <returns>Version string (e.g. <c>"1.1.0"</c>) or <c>null</c> when the
        /// package metadata cannot be resolved (for example when the source loads
        /// from a non-package folder during local development).</returns>
        public static string? GetCurrentVersion()
        {
            var packageInfo = PackageInfo.FindForAssembly(typeof(PinToolbarElement).Assembly);
            return packageInfo?.version;
        }

        #endregion
    }
}