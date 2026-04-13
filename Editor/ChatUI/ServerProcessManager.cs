#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using GameDeck.Editor.Settings;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// Manages the lifecycle of the Agent SDK Server Node.js process.
    /// The Node.js process survives Unity assembly reloads — it is only killed
    /// when the editor quits or the user explicitly restarts it.
    /// On startup, reconnects to an existing process if one is still running.
    /// </summary>
    public class ServerProcessManager : IDisposable
    {
        #region CONSTRUCTOR

        public ServerProcessManager(string projectDir)
        {
            _projectDir = projectDir;
        }

        #endregion

        #region CONSTANTS

        private const string PID_PREF_KEY = "GameDeck_ServerPID";

        #endregion

        #region FIELDS

        private Process? _process;
        private readonly string _projectDir;
        private DataReceivedEventHandler? _outputHandler;
        private DataReceivedEventHandler? _errorHandler;
        private EventHandler? _exitedHandler;

        #endregion

        #region Properties

        /// <summary>Gets the current state of the managed server process.</summary>
        public EServerState State { get; private set; } = EServerState.STOPPED;

        #endregion

        #region EVENTS

        /// <summary>Raised whenever the server process transitions to a new <see cref="EServerState"/>.</summary>
        public event Action<EServerState>? OnStateChanged;

        #endregion

        #region ENUM

        /// <summary>Represents the current operational state of the server process.</summary>
        public enum EServerState
        {
            STOPPED,
            STARTING,
            RUNNING,
            ERROR
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Checks if a process with the given PID is still alive.
        /// </summary>
        /// <param name="pid">The process ID to check.</param>
        /// <returns>True if the process exists and has not exited.</returns>
        private static bool IsProcessAlive(int pid)
        {
            try
            {
                var proc = Process.GetProcessById(pid);
                return !proc.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Kills any existing process listening on the given port.
        /// Uses netstat to find the PID and taskkill to terminate it.
        /// Silently ignores errors (e.g., no process found, insufficient permissions).
        /// </summary>
        /// <param name="port">The TCP port to check.</param>
        private static void KillStaleProcessOnPort(int port)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", $"/c netstat -ano | findstr :{port} | findstr LISTENING")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);

                if (proc == null)
                {
                    return;
                }

                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);

                if (string.IsNullOrWhiteSpace(output))
                {
                    return;
                }

                string[] lines = output.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();

                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length < 5)
                    {
                        continue;
                    }

                    if (int.TryParse(parts[^1], out int pid) && pid > 0)
                    {
                        try
                        {
                            var staleProc = Process.GetProcessById(pid);

                            if (staleProc.ProcessName.IndexOf("node", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                staleProc.Kill();
                                staleProc.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);
                            }
                        }
                        catch(Exception ex)
                        {
                            Debug.LogWarning($"[Game Deck] Could not kill stale process (PID: {pid}): {ex.Message}");
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.LogWarning($"[Game Deck] KillStaleProcessOnPort failed (netstat/cmd may not be available): {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves the absolute path to the Server~ directory inside the package.
        /// Uses PackageInfo API for robust resolution across local and Git installs.
        /// </summary>
        /// <returns>Absolute path to the Server~ directory.</returns>
        private static string ResolveServerDirectory()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ServerProcessManager).Assembly);

            if (packageInfo != null)
            {
                return Path.Combine(packageInfo.resolvedPath, "Server~");
            }

            return Path.GetFullPath(ChatConstants.FALLBACK_SERVER_PATH);
        }

        /// <summary>
        /// Updates <see cref="State"/> and raises <see cref="OnStateChanged"/> if the value changed.
        /// </summary>
        /// <param name="state">The new server process state.</param>
        private void SetState(EServerState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Checks whether Node.js is available on the system PATH.
        /// </summary>
        /// <returns>True if Node.js is installed and executable; otherwise false.</returns>
        public static bool IsNodeInstalled()
        {
            try
            {
                var psi = new ProcessStartInfo("node", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                proc?.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);
                return proc?.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts or reconnects to the Agent SDK Server process.
        /// If a previous process is still running (from before an assembly reload),
        /// reconnects to it instead of spawning a new one.
        /// </summary>
        public void Start()
        {
            if (State == EServerState.RUNNING || State == EServerState.STARTING)
            {
                return;
            }

            int savedPid = EditorPrefs.GetInt(PID_PREF_KEY, -1);

            if (savedPid > 0 && IsProcessAlive(savedPid))
            {
                try
                {
                    var oldProc = Process.GetProcessById(savedPid);

                    if (!oldProc.HasExited)
                    {
                        oldProc.Kill(); oldProc.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);
                    }
                }

                catch(Exception ex)
                {
                    Debug.LogWarning($"[Game Deck] Could not kill previous server (PID: {savedPid}): {ex.Message}");
                }

                EditorPrefs.DeleteKey(PID_PREF_KEY);
            }

            if (!IsNodeInstalled())
            {
                Debug.LogError("[Game Deck] Node.js not found. Install Node.js 18+ to use the chat.");
                SetState(EServerState.ERROR);
                return;
            }

            var settings = GameDeckSettings.Instance;
            int port = settings._agentPort;
            KillStaleProcessOnPort(port);

            var serverDir = ResolveServerDirectory();
            var entryPoint = Path.Combine(serverDir, "dist", "index.js");

            if (!File.Exists(entryPoint))
            {
                Debug.LogError($"[Game Deck] Server entry point not found: {entryPoint}. Run 'npm run build' in Server~/.");
                SetState(EServerState.ERROR);
                return;
            }

            SetState(EServerState.STARTING);

            try
            {
                var psi = new ProcessStartInfo("node", $"\"{entryPoint}\"")
                {
                    WorkingDirectory = _projectDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var packageDir = Path.GetDirectoryName(serverDir) ?? serverDir;
                psi.EnvironmentVariables[ChatConstants.ENV_PORT] = port.ToString();
                psi.EnvironmentVariables[ChatConstants.ENV_PROJECT_CWD] = _projectDir;
                psi.EnvironmentVariables[ChatConstants.ENV_PACKAGE_DIR] = packageDir;
                psi.EnvironmentVariables[ChatConstants.ENV_MCP_SERVER_URL] = $"http://{settings._host}:{settings._mcpPort}";
                psi.EnvironmentVariables[ChatConstants.ENV_MODEL] = settings._defaultModel;

                _process = Process.Start(psi);

                if (_process == null)
                {
                    SetState(EServerState.ERROR);
                    return;
                }

                EditorPrefs.SetInt(PID_PREF_KEY, _process.Id);

                _outputHandler = (_, e) => { }; // Stdout silenced — stderr only.
                _errorHandler = (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        var msg = e.Data;
                        EditorApplication.delayCall += () => Debug.LogWarning($"[Game Deck Server] {msg}");
                    }
                };
                _exitedHandler = (_, _) =>
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.Log("[Game Deck] Server process exited.");
                        EditorPrefs.DeleteKey(PID_PREF_KEY);
                        SetState(EServerState.STOPPED);
                    };
                };

                _process.OutputDataReceived += _outputHandler;
                _process.ErrorDataReceived += _errorHandler;
                _process.EnableRaisingEvents = true;
                _process.Exited += _exitedHandler;

                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                SetState(EServerState.RUNNING);
                Debug.Log($"[Game Deck] Server started on port {port} (PID: {_process.Id})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Game Deck] Failed to start server: {ex.Message}");
                SetState(EServerState.ERROR);
            }
        }

        /// <summary>
        /// Forcefully stops the server process. Used when the editor is quitting
        /// or the user explicitly requests a restart.
        /// </summary>
        public void ForceStop()
        {
            if (_process == null)
            {
                return;
            }
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(ChatConstants.PROCESS_KILL_TIMEOUT_MS);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Game Deck] Error stopping server: {ex.Message}");
            }
            finally
            {
                UnsubscribeProcessEvents();
                _process?.Dispose();
                _process = null;
                EditorPrefs.DeleteKey(PID_PREF_KEY);
                SetState(EServerState.STOPPED);
            }
        }

        /// <summary>
        /// Detaches from the server process without killing it.
        /// Called during assembly reload so the Node.js process survives.
        /// </summary>
        public void Dispose()
        {
            UnsubscribeProcessEvents();
            _process?.Dispose();
            _process = null;
        }

        /// <summary>
        /// Removes all event handlers from the process before disposing.
        /// Prevents handlers from firing on a disposed process.
        /// </summary>
        private void UnsubscribeProcessEvents()
        {
            if (_process == null)
            {
                return;
            }

            if (_outputHandler != null)
            {
                _process.OutputDataReceived -= _outputHandler;
                _outputHandler = null;
            }

            if (_errorHandler != null)
            {
                _process.ErrorDataReceived -= _errorHandler;
                _errorHandler = null;
            }

            if (_exitedHandler != null)
            {
                _process.Exited -= _exitedHandler;
                _exitedHandler = null;
            }
        }

        #endregion
    }
}
