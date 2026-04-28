#nullable enable

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Editor-only polling loop that periodically recomputes the toolbar pin's
    /// status. Registers a throttled callback on <see cref="EditorApplication.update"/>
    /// via <see cref="InitializeOnLoadMethodAttribute"/> and exposes the latest
    /// result through <see cref="CurrentStatus"/> for consumption by
    /// <see cref="PinToolbarElement"/>.
    /// </summary>
    /// <remarks>
    /// Two cadences run inside the same handler:
    /// <list type="bullet">
    ///   <item><description>~500 ms general tick (bumps <see cref="TickCount"/> and
    ///   re-evaluates the visible status so live signals like
    ///   <see cref="EditorApplication.isCompiling"/> and
    ///   <see cref="EditorApplication.isPlayingOrWillChangePlaymode"/> are reflected
    ///   without waiting for the next probe).</description></item>
    ///   <item><description>~1 s TCP probe of the configured MCP host/port. Probe runs
    ///   asynchronously with a 200 ms timeout; the result is applied back on the
    ///   editor main thread via <see cref="EditorApplication.delayCall"/>.</description></item>
    /// </list>
    /// State priority (highest wins): <see cref="EPinStatus.BUSY"/> when Unity is
    /// compiling / entering play mode / importing AND a successful probe occurred
    /// within <c>CONNECTED_RECENCY_WINDOW_SECONDS</c>; otherwise
    /// <see cref="EPinStatus.CONNECTED"/> when the last probe succeeded; otherwise
    /// <see cref="EPinStatus.BIND_FAILURE"/> when the C# MCP server failed to bind
    /// the configured port (detected via a <see cref="Application.logMessageReceivedThreaded"/>
    /// listener that watches for <c>EADDRINUSE</c> / <c>address already in use</c>
    /// messages mentioning the configured port); otherwise
    /// <see cref="EPinStatus.NOT_RUNNING"/> if the binary is on disk; otherwise
    /// <see cref="EPinStatus.NOT_INSTALLED"/>. Transitions trigger
    /// <see cref="MainToolbar.Refresh"/> on the pin element so the icon recolors —
    /// <c>[MainToolbarElement]</c> factories only run at registration / refresh time.
    /// </remarks>
    public static class PinPolling
    {
        #region CONSTANTS

        private const double TICK_INTERVAL_SECONDS = 0.5;
        private const double TCP_PROBE_INTERVAL_SECONDS = 1.0;
        private const int TCP_TIMEOUT_MS = 200;
        private const double CONNECTED_RECENCY_WINDOW_SECONDS = 10.0;
        private const double BIND_FAILURE_RECENCY_WINDOW_SECONDS = 30.0;
        private const string BIND_FAILURE_TOKEN_EADDRINUSE = "EADDRINUSE";
        private const string BIND_FAILURE_TOKEN_ADDRESS_IN_USE = "address already in use";
        public const string UPDATE_AVAILABLE_PREF = "MCPGameDeck.UpdateAvailable";
        public const string LATEST_VERSION_PREF = "MCPGameDeck.LatestVersion";

        #endregion

        #region FIELDS

        private static double _nextTickAt;
        private static double _nextProbeAt;
        private static bool _probeInFlight;
        private static bool _lastProbeConnected;
        private static double _lastSuccessfulProbeAt = double.MinValue;
        private static double _lastBindFailureAt = double.MinValue;
        private static volatile bool _bindFailureFlag;
        private static volatile int _cachedPort;
        private static EPinStatus _lastRefreshedStatus = EPinStatus.NOT_INSTALLED;
        private static bool _lastRefreshedUpdateAvailable;
        private static string _lastRefreshedUpdateVersion = string.Empty;

        #endregion

        #region PROPERTIES

        /// <summary>
        /// Most-recently computed pin status. Read by <see cref="PinToolbarElement"/> when
        /// constructing the toolbar icon.
        /// </summary>
        public static EPinStatus CurrentStatus { get; private set; } = EPinStatus.NOT_INSTALLED;

        /// <summary>
        /// Number of throttled ticks that have fired since the last assembly reload.
        /// Useful for confirming the polling loop is alive during validation.
        /// </summary>
        public static long TickCount { get; private set; }

        /// <summary>
        /// <c>true</c> when a bind-failure log line mentioning the configured port has
        /// been observed within the last <see cref="BIND_FAILURE_RECENCY_WINDOW_SECONDS"/>.
        /// Cleared either by 30 s of silence or by the next successful TCP probe.
        /// </summary>
        public static bool BindFailureDetected => (EditorApplication.timeSinceStartup - _lastBindFailureAt) < BIND_FAILURE_RECENCY_WINDOW_SECONDS;

        /// <summary>
        /// Cached value of <see cref="UPDATE_AVAILABLE_PREF"/>, refreshed once per tick.
        /// Drives both the blue update badge on the icon and the update line in the tooltip.
        /// </summary>
        public static bool UpdateAvailable { get; private set; }

        /// <summary>
        /// Cached value of <see cref="LATEST_VERSION_PREF"/>, refreshed once per tick.
        /// Used by <see cref="PinTooltip"/> when an update is available.
        /// </summary>
        public static string UpdateVersion { get; private set; } = string.Empty;

        #endregion

        #region INITIALIZATION METHODS

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= HandleTick;
            EditorApplication.update += HandleTick;
            Application.logMessageReceivedThreaded -= HandleLogMessage;
            Application.logMessageReceivedThreaded += HandleLogMessage;
            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;
            _nextProbeAt = EditorApplication.timeSinceStartup;
            _cachedPort = GameDeckSettings.Instance._mcpPort;
            UpdateAvailable = EditorPrefs.GetBool(UPDATE_AVAILABLE_PREF, false);
            UpdateVersion = EditorPrefs.GetString(LATEST_VERSION_PREF, string.Empty);
            _lastRefreshedUpdateAvailable = UpdateAvailable;
            _lastRefreshedUpdateVersion = UpdateVersion;
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Throttled editor-update callback that bumps <see cref="TickCount"/>, kicks off
        /// a TCP probe on its own ~1 s cadence, and re-evaluates the visible status so
        /// live signals (Unity busy, recently-connected window) are reflected within one
        /// tick instead of having to wait for the next probe.
        /// </summary>
        private static void HandleTick()
        {
            if (EditorApplication.timeSinceStartup < _nextTickAt)
            {
                return;
            }

            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;
            TickCount++;

            _cachedPort = GameDeckSettings.Instance._mcpPort;

            if (_bindFailureFlag)
            {
                _bindFailureFlag = false;
                _lastBindFailureAt = EditorApplication.timeSinceStartup;
            }

            if (!_probeInFlight && EditorApplication.timeSinceStartup >= _nextProbeAt)
            {
                _nextProbeAt = EditorApplication.timeSinceStartup + TCP_PROBE_INTERVAL_SECONDS;
                StartTcpProbe();
            }

            EvaluateAndApplyState();
        }

        /// <summary>
        /// Kicks off a background TCP probe against the host and port configured in
        /// <see cref="GameDeckSettings"/>, marking <c>_probeInFlight</c> so the polling
        /// loop does not enqueue overlapping probes. The returned task is intentionally
        /// fire-and-forget — completion is handled inside <see cref="ProbeAsync"/>.
        /// </summary>
        private static void StartTcpProbe()
        {
            var settings = GameDeckSettings.Instance;
            var host = settings._host;
            var port = settings._mcpPort;

            _probeInFlight = true;
            _ = ProbeAsync(host, port);
        }

        /// <summary>
        /// Runs the async TCP probe and dispatches the result back to the main thread for
        /// state-machine evaluation. Never throws — all failure paths produce a
        /// <c>connected = false</c> result.
        /// </summary>
        /// <param name="host">Target host name or IP address to probe.</param>
        /// <param name="port">Target TCP port to probe.</param>
        /// <returns>
        /// A task that completes once the probe has finished and the result has been
        /// queued onto the main thread via <see cref="EditorApplication.delayCall"/>.
        /// </returns>
        private static async Task ProbeAsync(string host, int port)
        {
            bool connected;
            try
            {
                connected = await TryConnectAsync(host, port);
            }
            catch (Exception e)
            {
                McpLogger.Error("Pin TCP probe pipeline failed", e);
                connected = false;
            }

            EditorApplication.delayCall += () =>
            {
                _lastProbeConnected = connected;

                if (connected)
                {
                    _lastSuccessfulProbeAt = EditorApplication.timeSinceStartup;
                    _lastBindFailureAt = double.MinValue;
                }

                EvaluateAndApplyState();
                _probeInFlight = false;
            };
        }

        /// <summary>
        /// Attempts a TCP connection to <paramref name="host"/>:<paramref name="port"/>
        /// with a <see cref="TCP_TIMEOUT_MS"/> ceiling enforced via
        /// <see cref="Task.WhenAny"/>. Returns <c>false</c> on timeout, refused
        /// connections, or any other socket-level failure; the underlying
        /// <see cref="TcpClient"/> is always disposed.
        /// </summary>
        /// <param name="host">Target host name or IP address.</param>
        /// <param name="port">Target TCP port.</param>
        /// <returns>
        /// <c>true</c> when the connect handshake completes within the timeout window;
        /// <c>false</c> otherwise.
        /// </returns>
        private static async Task<bool> TryConnectAsync(string host, int port)
        {
            var client = new TcpClient();
            try
            {
                var connectTask = client.ConnectAsync(host, port);
                var winner = await Task.WhenAny(connectTask, Task.Delay(TCP_TIMEOUT_MS));

                if (winner != connectTask)
                {
                    _ = connectTask.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);
                    return false;
                }

                await connectTask;
                return client.Connected;
            }
            catch (SocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Exception e)
            {
                McpLogger.Error("Pin TCP probe encountered an unexpected error", e);
                return false;
            }
            finally
            {
                client.Dispose();
            }
        }

        /// <summary>
        /// Computes the visible <see cref="EPinStatus"/> from the cached probe outcome,
        /// the time of the last successful probe, and Unity's current busy flags, refreshes
        /// <see cref="UpdateAvailable"/> / <see cref="UpdateVersion"/> from EditorPrefs, and
        /// triggers a toolbar refresh only when one of these user-visible aspects has
        /// actually changed since the last refresh.
        /// </summary>
        /// <remarks>
        /// Yellow / <see cref="EPinStatus.BUSY"/> requires both a busy flag AND a
        /// successful probe within <see cref="CONNECTED_RECENCY_WINDOW_SECONDS"/>. This
        /// keeps offline projects (gray) from flashing yellow when the user clicks Play.
        /// </remarks>
        private static void EvaluateAndApplyState()
        {
            var busy = EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating;

            var sinceLastSuccess = EditorApplication.timeSinceStartup - _lastSuccessfulProbeAt;
            var recentlyConnected = sinceLastSuccess < CONNECTED_RECENCY_WINDOW_SECONDS;

            EPinStatus newStatus;

            if (busy && recentlyConnected)
            {
                newStatus = EPinStatus.BUSY;
            }
            else if (_lastProbeConnected)
            {
                newStatus = EPinStatus.CONNECTED;
            }
            else if (BindFailureDetected)
            {
                newStatus = EPinStatus.BIND_FAILURE;
            }
            else
            {
                newStatus = PinPaths.GetBinaryPath() != null ? EPinStatus.NOT_RUNNING : EPinStatus.NOT_INSTALLED;
            }

            CurrentStatus = newStatus;
            UpdateAvailable = EditorPrefs.GetBool(UPDATE_AVAILABLE_PREF, false);
            UpdateVersion = EditorPrefs.GetString(LATEST_VERSION_PREF, string.Empty);

            if (newStatus == _lastRefreshedStatus && UpdateAvailable == _lastRefreshedUpdateAvailable && UpdateVersion == _lastRefreshedUpdateVersion)
            {
                return;
            }

            _lastRefreshedStatus = newStatus;
            _lastRefreshedUpdateAvailable = UpdateAvailable;
            _lastRefreshedUpdateVersion = UpdateVersion;
            TryRefreshToolbar();
        }

        /// <summary>
        /// Threaded log listener that watches for the C# MCP server's port-bind failures.
        /// Sets <see cref="_bindFailureFlag"/> when an error log mentions either
        /// <c>EADDRINUSE</c> or <c>address already in use</c> AND the cached configured
        /// port. The main thread observes the flag inside <see cref="HandleTick"/> and
        /// stamps a timestamp into <see cref="_lastBindFailureAt"/>.
        /// </summary>
        /// <remarks>
        /// Called on whatever thread emitted the log — must NOT touch Unity APIs that
        /// assume the main thread (e.g. <see cref="EditorApplication.timeSinceStartup"/>).
        /// </remarks>
        /// <param name="condition">The log message body to scan for bind-failure tokens.</param>
        /// <param name="stackTrace">Stack trace associated with the log entry; unused.</param>
        /// <param name="type">Severity of the log entry; only errors and exceptions are inspected.</param>
        private static void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(condition))
            {
                return;
            }

            if (condition.IndexOf(BIND_FAILURE_TOKEN_EADDRINUSE, StringComparison.OrdinalIgnoreCase) < 0 && condition.IndexOf(BIND_FAILURE_TOKEN_ADDRESS_IN_USE, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            var port = _cachedPort;
            if (port == 0)
            {
                return;
            }

            if (condition.IndexOf(port.ToString(), StringComparison.Ordinal) < 0)
            {
                return;
            }

            _bindFailureFlag = true;
        }

        /// <summary>
        /// Forces the Unity main toolbar to repaint so the pin reflects the latest
        /// <see cref="CurrentStatus"/>. Swallows and logs any exception so a transient
        /// repaint failure cannot break the polling loop.
        /// </summary>
        private static void TryRefreshToolbar()
        {
            try
            {
                MainToolbar.Refresh(PinToolbarElement.ELEMENT_PATH);
            }
            catch (Exception e)
            {
                McpLogger.Error("Failed to refresh main toolbar after pin status change", e);
            }
        }

        #endregion
    }
}