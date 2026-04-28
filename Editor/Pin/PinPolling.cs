#nullable enable

using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Toolbars;

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
    ///   <item><description>~500 ms general tick (bumps <see cref="TickCount"/>).</description></item>
    ///   <item><description>~1 s TCP probe of the configured MCP host/port. Probe runs
    ///   asynchronously with a 200 ms timeout; the result is applied back on the
    ///   editor main thread via <see cref="EditorApplication.delayCall"/>.</description></item>
    /// </list>
    /// When the probe causes <see cref="CurrentStatus"/> to transition,
    /// <see cref="MainToolbar.Refresh"/> is invoked so the pin's icon recolors —
    /// <c>[MainToolbarElement]</c> factories only run at registration / refresh time.
    /// </remarks>
    public static class PinPolling
    {
        #region CONSTANTS

        private const double TICK_INTERVAL_SECONDS = 0.5;
        private const double TCP_PROBE_INTERVAL_SECONDS = 1.0;
        private const int TCP_TIMEOUT_MS = 200;

        #endregion

        #region FIELDS

        private static double _nextTickAt;
        private static double _nextProbeAt;
        private static bool _probeInFlight;

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

        #endregion

        #region INITIALIZATION METHODS

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= HandleTick;
            EditorApplication.update += HandleTick;
            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;
            _nextProbeAt = EditorApplication.timeSinceStartup;
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Throttled editor-update callback that bumps <see cref="TickCount"/> and, on a
        /// separate ~1 s cadence, kicks off a TCP probe against the configured MCP server.
        /// </summary>
        private static void HandleTick()
        {
            if (EditorApplication.timeSinceStartup < _nextTickAt)
            {
                return;
            }

            _nextTickAt = EditorApplication.timeSinceStartup + TICK_INTERVAL_SECONDS;
            TickCount++;

            if (_probeInFlight)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextProbeAt)
            {
                return;
            }

            _nextProbeAt = EditorApplication.timeSinceStartup + TCP_PROBE_INTERVAL_SECONDS;

            StartTcpProbe();
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
                ApplyProbeResult(connected);
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
        /// Translates a probe result into the corresponding <see cref="EPinStatus"/>
        /// value, falling back to <see cref="EPinStatus.NOT_INSTALLED"/> versus
        /// <see cref="EPinStatus.NOT_RUNNING"/> based on whether the binary is
        /// resolvable. Refreshes the toolbar only when the status actually changes
        /// to avoid redundant repaints.
        /// </summary>
        /// <param name="connected">Outcome of the most recent TCP probe.</param>
        private static void ApplyProbeResult(bool connected)
        {
            EPinStatus newStatus;
            if (connected)
            {
                newStatus = EPinStatus.CONNECTED;
            }
            else
            {
                newStatus = PinPaths.GetBinaryPath() != null ? EPinStatus.NOT_RUNNING : EPinStatus.NOT_INSTALLED;
            }

            if (CurrentStatus == newStatus)
            {
                return;
            }

            CurrentStatus = newStatus;
            TryRefreshToolbar();
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