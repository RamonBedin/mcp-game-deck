#nullable enable

using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Orchestrates the pin's "launch the app" flow: ensures the binary is on disk
    /// (downloading on first use), spawns the Tauri process with all required
    /// environment variables, and surfaces failure dialogs via <see cref="PinDialogs"/>.
    /// </summary>
    /// <remarks>
    /// Entry point is <see cref="LaunchOrFocus"/>, called from the dropdown's
    /// <c>Open Chat</c> and <c>Settings</c> items. Concurrent clicks are guarded by
    /// a static flag so a second click during an in-flight download is a no-op
    /// (the progress window is already visible).
    ///
    /// "Focus" semantics — re-clicking the pin while the app is already running
    /// will spawn a second instance until task 5.1 adds the Tauri single-instance
    /// plugin. From the pin's perspective the API is unchanged: each call is
    /// "launch a process pointed at this version".
    /// </remarks>
    public static class PinLauncher
    {
        #region CONSTANTS

        private const string DEFAULT_ROUTE = "/chat";
        private const string ROUTE_ARG_FORMAT = "--route={0}";
        private const int LAUNCH_VERIFY_DELAY_MS = 1500;

        private const string ENV_UNITY_PROJECT_PATH = "UNITY_PROJECT_PATH";
        private const string ENV_UNITY_MCP_HOST = "UNITY_MCP_HOST";
        private const string ENV_UNITY_MCP_PORT = "UNITY_MCP_PORT";
        private const string ENV_UPDATE_AVAILABLE = "MCP_GAME_DECK_UPDATE_AVAILABLE";
        private const string ENV_LATEST_VERSION = "MCP_GAME_DECK_LATEST_VERSION";
        private const string ENV_RELEASE_URL = "MCP_GAME_DECK_RELEASE_URL";
        private const string ENV_TRUE = "true";
        private const string ENV_FALSE = "false";

        #endregion

        #region FIELDS

        private static bool _operationInProgress;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Implementation of <see cref="LaunchOrFocus"/>: resolves the package
        /// version, ensures the binary is installed (downloading via
        /// <see cref="PinBinaryManager.DownloadAsync"/> with retry dialogs on
        /// failure), then spawns the process. The
        /// <see cref="_operationInProgress"/> flag prevents re-entry during an
        /// in-flight download or launch verification.
        /// </summary>
        /// <param name="route">Initial route to pass to the Tauri app.</param>
        /// <returns>A task that completes once the launch attempt finishes (success,
        /// user-cancelled download, or unrecoverable error).</returns>
        private static async Task LaunchOrFocusAsync(string route)
        {
            if (_operationInProgress)
            {
                return;
            }

            _operationInProgress = true;
            try
            {
                var version = PinBinaryManager.GetCurrentVersion();

                if (string.IsNullOrWhiteSpace(version))
                {
                    McpLogger.Error("[Pin] Cannot launch: package version unavailable");
                    return;
                }

                if (!PinBinaryManager.IsInstalled(version!))
                {
                    var installed = await EnsureBinaryInstalledAsync(version!);

                    if (!installed)
                    {
                        return;
                    }
                }

                await StartProcessAsync(version!, route);
            }
            catch (Exception e)
            {
                McpLogger.Error("[Pin] LaunchOrFocus encountered an unexpected error", e);
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        /// <summary>
        /// Drives the download retry loop: opens a progress window, calls
        /// <see cref="PinBinaryManager.DownloadAsync"/>, and on each error variant
        /// asks the user via <see cref="PinDialogs"/> whether to retry. The progress
        /// window is closed in <c>finally</c> so it never leaks.
        /// </summary>
        /// <param name="version">Package version to download.</param>
        /// <returns><c>true</c> when the binary is on disk and verified;
        /// <c>false</c> when the user cancelled or chose to download manually.</returns>
        private static async Task<bool> EnsureBinaryInstalledAsync(string version)
        {
            var window = PinDialogs.ShowProgress();
            try
            {
                var url = PinBinaryManager.GetDownloadUrl(version);
                EDownloadResult result;

                do
                {
                    result = await PinBinaryManager.DownloadAsync(version, window.AsProgress());

                    if (result == EDownloadResult.NETWORK_ERROR)
                    {
                        if (!PinDialogs.ShowNetworkError(url))
                        {
                            return false;
                        }
                    }
                    else if (result == EDownloadResult.HASH_MISMATCH)
                    {
                        if (!PinDialogs.ShowHashMismatch())
                        {
                            return false;
                        }
                    }
                }
                while (result != EDownloadResult.SUCCESS);

                return true;
            }
            finally
            {
                if (window != null)
                {
                    window.Close();
                }
            }
        }

        /// <summary>
        /// Spawns the Tauri process with all required env vars and the
        /// <c>--route=</c> CLI argument, then waits briefly to detect immediate
        /// crashes. Surfaces <see cref="PinDialogs.ShowLaunchFailed"/> when
        /// <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/>
        /// throws or returns null (sentinel
        /// <see cref="PinDialogs.LAUNCH_FAILED_TO_START"/>) or when the process
        /// exits non-zero within <see cref="LAUNCH_VERIFY_DELAY_MS"/>.
        /// </summary>
        /// <param name="version">Package version (used to resolve the binary path).</param>
        /// <param name="route">Initial app route.</param>
        /// <returns>A task that completes once the spawn-and-verify cycle finishes.</returns>
        private static async Task StartProcessAsync(string version, string route)
        {
            var binaryPath = PinPaths.BinaryPath(version);
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = string.Format(ROUTE_ARG_FORMAT, route),
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            PopulateEnvironmentVariables(psi.EnvironmentVariables);
            try
            {
                using var process = System.Diagnostics.Process.Start(psi);

                if (process == null)
                {
                    McpLogger.Error($"[Pin] Process.Start returned null for {binaryPath}");
                    PinDialogs.ShowLaunchFailed(PinDialogs.LAUNCH_FAILED_TO_START);
                    return;
                }

                await Task.Delay(LAUNCH_VERIFY_DELAY_MS);

                if (process.HasExited && process.ExitCode != 0)
                {
                    McpLogger.Error($"[Pin] App exited with code {process.ExitCode} within {LAUNCH_VERIFY_DELAY_MS}ms");
                    PinDialogs.ShowLaunchFailed(process.ExitCode);
                }
            }
            catch (Win32Exception e)
            {
                McpLogger.Error($"[Pin] Process.Start failed for {binaryPath}", e);
                PinDialogs.ShowLaunchFailed(PinDialogs.LAUNCH_FAILED_TO_START);
            }
            catch (Exception e)
            {
                McpLogger.Error($"[Pin] Process.Start failed for {binaryPath}", e);
                PinDialogs.ShowLaunchFailed(PinDialogs.LAUNCH_FAILED_TO_START);
            }
        }

        /// <summary>
        /// Populates the spec-defined env-var contract on the Tauri process: project
        /// path, MCP host/port, update-available flag, and (when the flag is true)
        /// the latest version + release URL.
        /// </summary>
        /// <param name="envVars">Mutable env-var dictionary owned by the
        /// <see cref="System.Diagnostics.ProcessStartInfo"/>.</param>
        private static void PopulateEnvironmentVariables(StringDictionary envVars)
        {
            var settings = GameDeckSettings.Instance;
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            envVars[ENV_UNITY_PROJECT_PATH] = projectPath;
            envVars[ENV_UNITY_MCP_HOST] = settings._host;
            envVars[ENV_UNITY_MCP_PORT] = settings._mcpPort.ToString(CultureInfo.InvariantCulture);

            var updateAvailable = EditorPrefs.GetBool(PinPolling.UPDATE_AVAILABLE_PREF, false);
            envVars[ENV_UPDATE_AVAILABLE] = updateAvailable ? ENV_TRUE : ENV_FALSE;

            if (updateAvailable)
            {
                envVars[ENV_LATEST_VERSION] = EditorPrefs.GetString(PinPolling.LATEST_VERSION_PREF, string.Empty);
                envVars[ENV_RELEASE_URL] = EditorPrefs.GetString(PinPolling.RELEASE_URL_PREF, string.Empty);
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Launches the Tauri app for the current package version (downloading the
        /// binary first if missing) with the requested initial route. Fire-and-forget;
        /// errors are logged via <see cref="McpLogger"/> and surfaced through
        /// <see cref="PinDialogs"/>.
        /// </summary>
        /// <param name="route">Initial in-app route, passed via <c>--route=</c> CLI
        /// argument and consumed by the Tauri side in task 5.2. Defaults to
        /// <c>/chat</c>; the dropdown's <c>Settings</c> item passes <c>/settings</c>.</param>
        public static void LaunchOrFocus(string route = DEFAULT_ROUTE)
        {
            _ = LaunchOrFocusAsync(route);
        }

        #endregion

    }
}