#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// Setup screen logic for the Game Deck Chat window.
    /// Checks prerequisites (Node.js, server build, Claude Code) and provides
    /// one-click build and install actions. Hides automatically when all checks pass.
    /// </summary>
    public partial class ChatWindow
    {
        #region CONSTANTS

        private const string SETUP_ICON_PASS = "✓";
        private const string SETUP_ICON_FAIL = "✗";
        private const string SETUP_ICON_WARN = "!";

        private const string SETUP_USS_PASS = "setup-icon--pass";
        private const string SETUP_USS_FAIL = "setup-icon--fail";
        private const string SETUP_USS_WARN = "setup-icon--warn";

        private const string NODE_URL = "https://nodejs.org/";
        private const string CLAUDE_CODE_URL = "https://docs.anthropic.com/en/docs/claude-code/overview";

        #endregion

        #region FIELDS

        private VisualElement? _setupScreen;
        private VisualElement? _rootRow;
        private ScrollView? _setupConsole;

        private Label? _nodeIcon;
        private Label? _nodeDetail;
        private Button? _nodeAction;

        private Label? _buildIcon;
        private Label? _buildDetail;
        private Button? _buildAction;

        private Label? _claudeIcon;
        private Label? _claudeDetail;
        private Button? _claudeAction;

        private bool _isBuilding;

        #endregion

        #region SETUP INITIALIZATION

        /// <summary>
        /// Loads the setup screen UXML, queries all UI elements, wires button callbacks,
        /// and runs the initial prerequisite checks.
        /// </summary>
        private void InitializeSetupScreen()
        {
            var packagePath = ResolvePackageAssetPath();
            var setupUxmlPath = $"{packagePath}/Editor/ChatUI/SetupScreen.uxml";
            var setupUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(setupUxmlPath);

            if (setupUxml == null)
            {
                InitializeServer();
                return;
            }

            setupUxml.CloneTree(rootVisualElement);

            _setupScreen = rootVisualElement.Q<VisualElement>("SetupScreen");
            _rootRow = rootVisualElement.Q<VisualElement>("RootRow");
            _setupConsole = rootVisualElement.Q<ScrollView>("SetupConsole");

            _nodeIcon = rootVisualElement.Q<Label>("SetupIcon_Node");
            _nodeDetail = rootVisualElement.Q<Label>("SetupDetail_Node");
            _nodeAction = rootVisualElement.Q<Button>("SetupAction_Node");

            _buildIcon = rootVisualElement.Q<Label>("SetupIcon_Build");
            _buildDetail = rootVisualElement.Q<Label>("SetupDetail_Build");
            _buildAction = rootVisualElement.Q<Button>("SetupAction_Build");

            _claudeIcon = rootVisualElement.Q<Label>("SetupIcon_Claude");
            _claudeDetail = rootVisualElement.Q<Label>("SetupDetail_Claude");
            _claudeAction = rootVisualElement.Q<Button>("SetupAction_Claude");

            _nodeAction?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(NODE_URL));
            _buildAction?.RegisterCallback<ClickEvent>(_ => HandleBuildServer());
            _claudeAction?.RegisterCallback<ClickEvent>(_ => Application.OpenURL(CLAUDE_CODE_URL));

            var refreshBtn = rootVisualElement.Q<Button>("SetupRefreshBtn");
            refreshBtn?.RegisterCallback<ClickEvent>(_ => RunSetupChecks());

            if (_setupConsole != null)
            {
                _setupConsole.style.display = DisplayStyle.None;
            }

            RunSetupChecks();
        }

        #endregion

        #region SETUP CHECKS

        /// <summary>
        /// Runs all three prerequisite checks (Node.js, server build, Claude Code)
        /// and updates the UI accordingly. If all required checks pass, transitions
        /// to the chat view.
        /// </summary>
        private void RunSetupChecks()
        {
            bool nodeOk = CheckNodeJs();
            bool buildOk = CheckServerBuild();
            _ = CheckClaudeCode();

            if (nodeOk && buildOk)
            {
                ShowChatView();
            }
            else
            {
                ShowSetupView();
            }
        }

        /// <summary>
        /// Checks if Node.js is installed and returns version info.
        /// </summary>
        /// <returns><c>true</c> if Node.js is available on the system PATH.</returns>
        private bool CheckNodeJs()
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

                if (proc == null)
                {
                    SetCheckState(_nodeIcon, _nodeDetail, _nodeAction, SETUP_ICON_FAIL, "Not found", true);
                    return false;
                }

                string version = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);

                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(version))
                {
                    SetCheckState(_nodeIcon, _nodeDetail, _nodeAction, SETUP_ICON_PASS, version, false);
                    return true;
                }

                SetCheckState(_nodeIcon, _nodeDetail, _nodeAction, SETUP_ICON_FAIL, "Not found", true);
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetCheckState(_nodeIcon, _nodeDetail, _nodeAction, SETUP_ICON_FAIL, "Not installed", true);
                return false;
            }
            catch (Exception ex)
            {
                SetCheckState(_nodeIcon, _nodeDetail, _nodeAction, SETUP_ICON_FAIL, $"Error: {ex.Message}", true);
                return false;
            }
        }

        /// <summary>
        /// Checks if the TypeScript server has been built (dist/index.js exists).
        /// </summary>
        /// <returns><c>true</c> if the compiled server entry point exists.</returns>
        private bool CheckServerBuild()
        {
            string entryPoint = ResolveServerEntryPoint();

            if (File.Exists(entryPoint))
            {
                SetCheckState(_buildIcon, _buildDetail, _buildAction, SETUP_ICON_PASS, "Ready", false);
                return true;
            }

            SetCheckState(_buildIcon, _buildDetail, _buildAction, SETUP_ICON_FAIL, "Not built", true);
            return false;
        }

        /// <summary>
        /// Checks if Claude Code CLI is available on the system PATH.
        /// This is a warning, not a blocker — the chat UI works without it.
        /// </summary>
        /// <returns><c>true</c> if Claude Code is available.</returns>
        private bool CheckClaudeCode()
        {
            try
            {
                var psi = new ProcessStartInfo("claude", "--version")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);

                if (proc == null)
                {
                    SetCheckState(_claudeIcon, _claudeDetail, _claudeAction, SETUP_ICON_WARN, "Not found (optional)", true);
                    return false;
                }

                string version = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit(ChatConstants.PROCESS_WAIT_TIMEOUT_MS);

                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(version))
                {
                    SetCheckState(_claudeIcon, _claudeDetail, _claudeAction, SETUP_ICON_PASS, version, false);
                    return true;
                }

                SetCheckState(_claudeIcon, _claudeDetail, _claudeAction, SETUP_ICON_WARN, "Not found (optional)", true);
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetCheckState(_claudeIcon, _claudeDetail, _claudeAction, SETUP_ICON_WARN, "Not installed (optional)", true);
                return false;
            }
            catch (Exception ex)
            {
                SetCheckState(_claudeIcon, _claudeDetail, _claudeAction, SETUP_ICON_WARN, $"Error: {ex.Message}", true);
                return false;
            }
        }

        #endregion

        #region SETUP BUILD

        /// <summary>
        /// Runs <c>npm install &amp;&amp; npm run build</c> in the Server~ directory.
        /// Shows output in the setup console and re-checks prerequisites when done.
        /// </summary>
        private void HandleBuildServer() => SafeAsync(HandleBuildServerAsync(), "BuildServer");

        /// <summary>
        /// Async implementation of the server build process.
        /// </summary>
        /// <returns>A Task that completes when the build finishes.</returns>
        private async Task HandleBuildServerAsync()
        {
            if (_isBuilding)
            {
                return;
            }

            _isBuilding = true;

            if (_buildAction != null)
            {
                _buildAction.SetEnabled(false);
                _buildAction.text = "Building...";
            }

            if (_setupConsole != null)
            {
                _setupConsole.style.display = DisplayStyle.Flex;
                _setupConsole.Clear();
                AppendConsole("Starting build...\n");
            }

            string serverDir = ResolveServerDirectory();
            try
            {
                bool installOk = await RunProcessAsync("npm", "install", serverDir);

                if (!installOk)
                {
                    AppendConsole("\n✗ npm install failed.\n");
                    return;
                }

                AppendConsole("\n✓ npm install complete.\n\n");

                bool buildOk = await RunProcessAsync("npm", "run build", serverDir);

                if (!buildOk)
                {
                    AppendConsole("\n✗ npm run build failed.\n");
                    return;
                }

                AppendConsole("\n✓ Build complete!\n");
            }
            finally
            {
                _isBuilding = false;

                if (_buildAction != null)
                {
                    _buildAction.SetEnabled(true);
                    _buildAction.text = "Build";
                }

                RunSetupChecks();
            }
        }

        /// <summary>
        /// Runs an external process and streams its output to the setup console.
        /// On Windows, wraps the command in <c>cmd.exe /c</c> to resolve .cmd/.bat scripts like npm.
        /// </summary>
        /// <param name="command">The command to run (e.g. "npm").</param>
        /// <param name="arguments">Command arguments (e.g. "install").</param>
        /// <param name="workingDir">Working directory for the process.</param>
        /// <returns><c>true</c> if the process exited with code 0.</returns>
        private async Task<bool> RunProcessAsync(string command, string arguments, string workingDir)
        {
            var tcs = new TaskCompletionSource<int>();

            string fileName = command;
            string args = arguments;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                fileName = "cmd.exe";
                args = $"/c {command} {arguments}";
            }

            var psi = new ProcessStartInfo(fileName, args)
            {
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var proc = Process.Start(psi);

            if (proc == null)
            {
                AppendConsole($"Failed to start: {command} {arguments}\n");
                return false;
            }

            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var line = e.Data;
                    EditorApplication.delayCall += () => AppendConsole(line + "\n");
                }
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    var line = e.Data;
                    EditorApplication.delayCall += () => AppendConsole(line + "\n");
                }
            };

            proc.EnableRaisingEvents = true;
            proc.Exited += (_, _) =>
            {
                var code = proc.ExitCode;
                EditorApplication.delayCall += () => tcs.TrySetResult(code);
            };

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            int exitCode = await tcs.Task;
            proc.Dispose();

            return exitCode == 0;
        }

        #endregion

        #region SETUP UI HELPERS

        /// <summary>
        /// Updates a single check row's icon, detail text, and action button visibility.
        /// </summary>
        /// <param name="icon">The icon label element.</param>
        /// <param name="detail">The detail text label.</param>
        /// <param name="action">The action button.</param>
        /// <param name="iconText">Icon character to display.</param>
        /// <param name="detailText">Detail/status text.</param>
        /// <param name="showAction">Whether to show the action button.</param>
        private static void SetCheckState(Label? icon, Label? detail, Button? action, string iconText, string detailText, bool showAction)
        {
            if (icon != null)
            {
                icon.text = iconText;
                icon.RemoveFromClassList(SETUP_USS_PASS);
                icon.RemoveFromClassList(SETUP_USS_FAIL);
                icon.RemoveFromClassList(SETUP_USS_WARN);

                string ussClass = iconText switch
                {
                    SETUP_ICON_PASS => SETUP_USS_PASS,
                    SETUP_ICON_FAIL => SETUP_USS_FAIL,
                    SETUP_ICON_WARN => SETUP_USS_WARN,
                    _ => ""
                };

                if (!string.IsNullOrEmpty(ussClass))
                {
                    icon.AddToClassList(ussClass);
                }
            }

            if (detail != null)
            {
                detail.text = detailText;
            }

            if (action != null)
            {
                action.style.display = showAction ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Appends a line of text to the setup console and scrolls to the bottom.
        /// </summary>
        /// <param name="text">Text to append.</param>
        private void AppendConsole(string text)
        {
            if (_setupConsole == null)
            {
                return;
            }

            var label = new Label(text);
            label.AddToClassList("setup-console-line");
            _setupConsole.Add(label);
            _setupConsole.scrollOffset = new Vector2(0, float.MaxValue);
        }

        /// <summary>
        /// Shows the setup screen and hides the chat view.
        /// </summary>
        private void ShowSetupView()
        {
            if (_setupScreen != null)
            {
                _setupScreen.style.display = DisplayStyle.Flex;
            }

            if (_rootRow != null)
            {
                _rootRow.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// Hides the setup screen, shows the chat view, and starts the server.
        /// Only initializes the server on the first transition to chat.
        /// </summary>
        private void ShowChatView()
        {
            if (_setupScreen != null)
            {
                _setupScreen.style.display = DisplayStyle.None;
            }

            if (_rootRow != null)
            {
                _rootRow.style.display = DisplayStyle.Flex;
            }

            if (_serverManager == null && _welcomeScreen != null)
            {
                _hasMessages = false;
                _currentSessionId = null;
                _welcomeScreen.style.display = DisplayStyle.Flex;
            }

            if (_serverManager == null)
            {
                InitializeServer();
            }
        }

        /// <summary>
        /// Resolves the absolute path to the compiled server entry point.
        /// </summary>
        /// <returns>Absolute path to <c>Server~/dist/index.js</c>.</returns>
        private static string ResolveServerEntryPoint()
        {
            return Path.Combine(ResolveServerDirectory(), "dist", "index.js");
        }

        /// <summary>
        /// Resolves the absolute path to the Server~ directory inside the package.
        /// </summary>
        /// <returns>Absolute path to the Server~ directory.</returns>
        private static string ResolveServerDirectory()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ChatWindow).Assembly);

            if (packageInfo != null)
            {
                return Path.Combine(packageInfo.resolvedPath, "Server~");
            }

            return Path.GetFullPath(ChatConstants.FALLBACK_SERVER_PATH);
        }

        #endregion
    }
}