#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Cross-platform install path helpers for the pin's external Tauri app binary.
    /// </summary>
    /// <remarks>
    /// Resolves the per-OS install root, per-version subfolders, and the SHA-256
    /// sidecar location. Linux paths follow the XDG Base Directory Specification
    /// — <c>$XDG_DATA_HOME</c> when set and non-empty, otherwise
    /// <c>$HOME/.local/share</c>. The package ships Editor-only (asmdef Editor
    /// platform), so only the <c>*Editor</c> <see cref="RuntimePlatform"/>
    /// variants are reachable; any other platform throws
    /// <see cref="PlatformNotSupportedException"/>.
    /// </remarks>
    public static class PinPaths
    {
        #region CONSTANTS

        private const string APP_FOLDER_NAME = "MCPGameDeck";
        private const string BIN_SUBFOLDER = "bin";
        private const string BINARY_BASE_NAME = "mcp-game-deck-app";
        private const string WINDOWS_BINARY_EXTENSION = ".exe";
        private const string SHA256_EXTENSION = ".sha256";
        private const string MACOS_LIBRARY_FOLDER = "Library";
        private const string MACOS_APP_SUPPORT_FOLDER = "Application Support";
        private const string LINUX_LOCAL_FOLDER = ".local";
        private const string LINUX_SHARE_FOLDER = "share";
        private const string XDG_DATA_HOME_VAR = "XDG_DATA_HOME";
        private const string UNSUPPORTED_PLATFORM_MESSAGE_FORMAT =
            "PinPaths: unsupported platform '{0}'. MCP Game Deck ships Editor-only " +
            "(asmdef Editor platform); only WindowsEditor / OSXEditor / LinuxEditor " +
            "are valid runtime targets.";

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Root folder where the pin installs the Tauri app binary, resolved per-OS:
        /// <list type="bullet">
        /// <item><description>Windows: <c>%APPDATA%\MCPGameDeck</c></description></item>
        /// <item><description>macOS: <c>~/Library/Application Support/MCPGameDeck</c></description></item>
        /// <item><description>Linux: <c>$XDG_DATA_HOME/MCPGameDeck</c> when set and
        /// non-empty, otherwise <c>~/.local/share/MCPGameDeck</c> per the
        /// XDG Base Directory Specification.</description></item>
        /// </list>
        /// </summary>
        /// <exception cref="PlatformNotSupportedException">Thrown when
        /// <see cref="Application.platform"/> is not one of <c>WindowsEditor</c>,
        /// <c>OSXEditor</c>, or <c>LinuxEditor</c>.</exception>
        public static string InstallRoot => ResolveInstallRoot();

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Resolves the per-OS install root, throwing on non-Editor platforms.
        /// </summary>
        /// <returns>Absolute path of the install root for the current OS.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when the
        /// current <see cref="Application.platform"/> is not one of
        /// <c>WindowsEditor</c>, <c>OSXEditor</c>, or <c>LinuxEditor</c>.</exception>
        private static string ResolveInstallRoot()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    return Path.Combine(appData, APP_FOLDER_NAME);
                }
                case RuntimePlatform.OSXEditor:
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return Path.Combine(home, MACOS_LIBRARY_FOLDER, MACOS_APP_SUPPORT_FOLDER, APP_FOLDER_NAME);
                }
                case RuntimePlatform.LinuxEditor:
                {
                    var xdgDataHome = Environment.GetEnvironmentVariable(XDG_DATA_HOME_VAR);
                    string dataHome;

                    if (!string.IsNullOrEmpty(xdgDataHome))
                    {
                        dataHome = xdgDataHome;
                    }
                    else
                    {
                        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        dataHome = Path.Combine(home, LINUX_LOCAL_FOLDER, LINUX_SHARE_FOLDER);
                    }

                    return Path.Combine(dataHome, APP_FOLDER_NAME);
                }
                default:
                    throw new PlatformNotSupportedException(
                        string.Format(UNSUPPORTED_PLATFORM_MESSAGE_FORMAT, Application.platform));
            }
        }

        /// <summary>
        /// Returns the platform-specific binary filename — <c>mcp-game-deck-app.exe</c>
        /// on Windows, <c>mcp-game-deck-app</c> on macOS/Linux.
        /// </summary>
        /// <returns>Bare filename of the binary (no directory component).</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when the
        /// current <see cref="Application.platform"/> is not one of
        /// <c>WindowsEditor</c>, <c>OSXEditor</c>, or <c>LinuxEditor</c>.</exception>
        private static string GetBinaryFileName()
        {
            return Application.platform switch
            {
                RuntimePlatform.WindowsEditor => BINARY_BASE_NAME + WINDOWS_BINARY_EXTENSION,
                RuntimePlatform.OSXEditor or RuntimePlatform.LinuxEditor => BINARY_BASE_NAME,
                _ => throw new PlatformNotSupportedException(string.Format(UNSUPPORTED_PLATFORM_MESSAGE_FORMAT, Application.platform)),
            };
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns the per-version subfolder under <see cref="InstallRoot"/> that
        /// holds the binary and its <c>.sha256</c> sidecar.
        /// </summary>
        /// <param name="version">Package version string (e.g. <c>"1.1.0"</c>).</param>
        /// <returns>Absolute path of the form <c>&lt;InstallRoot&gt;/bin/&lt;version&gt;</c>.</returns>
        public static string BinFolder(string version)
        {
            return Path.Combine(InstallRoot, BIN_SUBFOLDER, version);
        }

        /// <summary>
        /// Returns the absolute path the Tauri app binary should be installed at
        /// for the given <paramref name="version"/>.
        /// </summary>
        /// <param name="version">Package version string (e.g. <c>"1.1.0"</c>).</param>
        /// <returns>Absolute path including the <c>.exe</c> extension on Windows
        /// and no extension on macOS/Linux.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when
        /// <see cref="Application.platform"/> is not one of <c>WindowsEditor</c>,
        /// <c>OSXEditor</c>, or <c>LinuxEditor</c>.</exception>
        public static string BinaryPath(string version)
        {
            return Path.Combine(BinFolder(version), GetBinaryFileName());
        }

        /// <summary>
        /// Returns the absolute path of the SHA-256 sidecar file for the binary
        /// at the given <paramref name="version"/>.
        /// </summary>
        /// <param name="version">Package version string (e.g. <c>"1.1.0"</c>).</param>
        /// <returns><see cref="BinaryPath(string)"/> with a <c>.sha256</c> suffix.</returns>
        /// <exception cref="PlatformNotSupportedException">Thrown when
        /// <see cref="Application.platform"/> is not one of <c>WindowsEditor</c>,
        /// <c>OSXEditor</c>, or <c>LinuxEditor</c>.</exception>
        public static string Sha256Path(string version)
        {
            return BinaryPath(version) + SHA256_EXTENSION;
        }

        #endregion

    }
}