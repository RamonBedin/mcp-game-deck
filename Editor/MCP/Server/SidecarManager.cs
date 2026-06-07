#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using GameDeck.MCP.Utils;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Locates, spawns, and tears down the long-lived MCP <b>sidecar</b> process
    /// (<c>Server~/dist/sidecar.cjs</c>, run with <c>node</c>). The sidecar owns
    /// the public MCP port for the whole Editor session so the listening socket
    /// never lives inside the reloading Editor AppDomain (which leaked listen
    /// sockets across domain reloads). The Editor connects OUT to the sidecar's
    /// loopback backend port via <see cref="McpBackendClient"/>.
    /// </summary>
    /// <remarks>
    /// The sidecar survives Unity domain reloads (it is a separate OS process),
    /// so its identity is rediscovered each domain load via the
    /// <c>Library/GameDeck/sidecar.json</c> file it writes. Managed state (a
    /// <see cref="Process"/> handle) cannot survive a reload, so all lifecycle
    /// decisions go through that file plus a PID liveness check.
    /// <para>
    /// <see cref="Initialize"/> MUST be called once on the Unity main thread
    /// before <see cref="EnsureRunning"/> (which runs on a background thread): it
    /// resolves the package script path and project path, which depend on Unity
    /// APIs (<see cref="PackageInfo"/>, <see cref="Application.dataPath"/>) that
    /// throw off the main thread.
    /// </para>
    /// </remarks>
    internal static class SidecarManager
    {
        #region CONSTANTS

        private const string INFO_RELATIVE_PATH = "Library/GameDeck/sidecar.json";
        private const string NODE_EXECUTABLE = "node";

        #endregion

        #region FIELDS

        private static string _sidecarScript = "";
        private static string _projectPath = "";

        #endregion

        #region TYPES

        /// <summary>Deserialized shape of <c>sidecar.json</c> (written by the sidecar).</summary>
        [Serializable]
        private class SidecarInfo
        {
            public int pid;
            public string publicHost = "";
            public int publicPort;
            public int backendPort;
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Resolves and caches the Unity-API-dependent paths (sidecar script,
        /// project root). Must be called once on the main thread before the
        /// background connection loop starts.
        /// </summary>
        public static void Initialize()
        {
            _projectPath = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;

            PackageInfo? package = PackageInfo.FindForAssembly(typeof(SidecarManager).Assembly);

            if (package == null)
            {
                McpLogger.Error("SidecarManager: cannot resolve package path (PackageInfo null) — sidecar script path unknown.");
                _sidecarScript = "";
                return;
            }

            _sidecarScript = Path.Combine(package.resolvedPath, "Server~", "dist", "sidecar.cjs");
        }

        /// <summary>
        /// Ensures a sidecar matching the configured <paramref name="host"/>/<paramref name="port"/>
        /// is running, spawning one if necessary. Non-blocking: when a spawn is
        /// needed it starts the process and returns; the sidecar writes its info
        /// file shortly after and <see cref="TryGetBackendPort"/> picks it up.
        /// </summary>
        /// <param name="host">Configured public host (from <see cref="McpServerConfig.Host"/>).</param>
        /// <param name="port">Configured public port (from <see cref="McpServerConfig.Port"/>).</param>
        public static void EnsureRunning(string host, int port)
        {
            if (TryGetBackendPort(host, port, out _))
            {
                return;
            }

            KillExisting();
            Spawn(host, port);
        }

        /// <summary>
        /// Reads <c>sidecar.json</c> and, if it describes a LIVE sidecar bound to
        /// the configured <paramref name="host"/>/<paramref name="port"/>, returns
        /// its backend port.
        /// </summary>
        /// <param name="host">Configured public host to match.</param>
        /// <param name="port">Configured public port to match.</param>
        /// <param name="backendPort">The loopback backend port to connect to.</param>
        /// <returns><c>true</c> when a matching live sidecar exists.</returns>
        public static bool TryGetBackendPort(string host, int port, out int backendPort)
        {
            backendPort = 0;
            SidecarInfo? info = ReadInfo();

            if (info == null || info.backendPort <= 0 || info.pid <= 0)
            {
                return false;
            }

            if (info.publicPort != port || !string.Equals(info.publicHost, host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsPidAlive(info.pid))
            {
                return false;
            }

            backendPort = info.backendPort;
            return true;
        }

        /// <summary>
        /// Kills the sidecar described by <c>sidecar.json</c> (if alive) and removes
        /// the info file. Called on Editor quit and before respawning a stale one.
        /// </summary>
        public static void KillExisting()
        {
            SidecarInfo? info = ReadInfo();

            if (info != null && info.pid > 0 && IsPidAlive(info.pid))
            {
                try
                {
                    using Process proc = Process.GetProcessById(info.pid);
                    proc.Kill();
                    McpLogger.Info($"Sidecar (pid {info.pid}) terminated.");
                }
                catch (Exception ex)
                {
                    McpLogger.Error($"SidecarManager: failed to kill pid {info.pid}: {ex.Message}");
                }
            }

            try
            {
                string path = InfoFilePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                McpLogger.Error($"SidecarManager: failed to delete info file: {ex.Message}");
            }
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Spawns <c>node sidecar.cjs</c> with the public host/port, project path,
        /// info-file path, and this Editor's PID (so the sidecar self-exits if the
        /// Editor crashes). The <see cref="Process"/> handle is intentionally not
        /// retained — it cannot survive a domain reload; the info file + PID drive
        /// all later lifecycle decisions.
        /// </summary>
        private static void Spawn(string host, int port)
        {
            if (string.IsNullOrEmpty(_sidecarScript) || !File.Exists(_sidecarScript))
            {
                McpLogger.Error($"SidecarManager: sidecar script not found at '{_sidecarScript}'. MCP server will not start.");
                return;
            }

            string infoFile = InfoFilePath();
            int parentPid = Process.GetCurrentProcess().Id;

            var psi = new ProcessStartInfo
            {
                FileName = NODE_EXECUTABLE,
                Arguments = $"\"{_sidecarScript}\" --public-host {host} --public-port {port.ToString(CultureInfo.InvariantCulture)} " + $"--project \"{_projectPath}\" --info-file \"{infoFile}\" " + $"--parent-pid {parentPid.ToString(CultureInfo.InvariantCulture)}",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            try
            {
                Process? proc = Process.Start(psi);

                if (proc == null)
                {
                    McpLogger.Error("SidecarManager: Process.Start returned null spawning the sidecar.");
                    return;
                }

                McpLogger.Info($"Sidecar spawned (pid {proc.Id}) on {host}:{port}.");
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                McpLogger.Error($"SidecarManager: failed to launch '{NODE_EXECUTABLE}' ({ex.Message}). " + "Node.js must be installed and on PATH for the MCP server to run.");
            }
            catch (Exception ex)
            {
                McpLogger.Error($"SidecarManager: failed to spawn sidecar: {ex.Message}");
            }
        }

        /// <summary>Reads and deserializes <c>sidecar.json</c>, or <c>null</c> if absent/invalid.</summary>
        private static SidecarInfo? ReadInfo()
        {
            try
            {
                string path = InfoFilePath();

                if (!File.Exists(path))
                {
                    return null;
                }

                string json = File.ReadAllText(path);
                return string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SidecarInfo>(json);
            }
            catch (Exception ex)
            {
                McpLogger.Error($"SidecarManager: failed to read info file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="pid"/> is a live <c>node</c>
        /// process. The process-name check guards against PID reuse pointing the
        /// kill/connect logic at an unrelated process.
        /// </summary>
        private static bool IsPidAlive(int pid)
        {
            try
            {
                using Process proc = Process.GetProcessById(pid);

                if (proc.HasExited)
                {
                    return false;
                }

                return proc.ProcessName.IndexOf(NODE_EXECUTABLE, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Absolute path to <c>Library/GameDeck/sidecar.json</c> (uses the cached project path).</summary>
        private static string InfoFilePath()
        {
            return Path.Combine(_projectPath, INFO_RELATIVE_PATH);
        }

        #endregion
    }
}