#nullable enable
using System.Threading.Tasks;
using GameDeck.Editor.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    public partial class ChatWindow
    {
        #region PRIVATE METHODS

        /// <summary>
        /// Creates a <see cref="ServerProcessManager"/> for the current project, subscribes
        /// to its state changes to trigger WebSocket connection, and registers an
        /// <see cref="EditorApplication.quitting"/> callback to kill the server on editor exit.
        /// </summary>
        private void InitializeServer()
        {
            var projectDir = Application.dataPath.Replace("/Assets", "");

            _serverManager = new ServerProcessManager(projectDir);
            _serverManager.OnStateChanged += state =>
            {
                EditorApplication.delayCall += () =>
                {
                    if (state == ServerProcessManager.EServerState.RUNNING)
                        ConnectWebSocket();
                };
            };
            _serverManager.Start();

            EditorApplication.quitting += HandleOnEditorQuitting;
        }

        /// <summary>
        /// Called when the Unity Editor is quitting. Kills the Node.js server process.
        /// </summary>
        private void HandleOnEditorQuitting()
        {
            EditorApplication.quitting -= HandleOnEditorQuitting;
            _serverManager?.ForceStop();
        }

        /// <summary>
        /// Disposes any existing WebSocket connection, creates a new <see cref="WebSocketClient"/>
        /// targeting the configured Agent port, wires up message/state/error callbacks, and connects.
        /// On successful connection, requests the agent list, session list, and a health ping,
        /// then restores the previous session from <see cref="EditorPrefs"/> if one exists.
        /// </summary>
        private void ConnectWebSocket() => SafeAsync(ConnectWebSocketAsync(), "ConnectWebSocket");

        /// <summary>
        /// Disposes any existing WebSocket connection, creates a new <see cref="WebSocketClient"/>
        /// targeting the configured Agent port, wires up message/state/error callbacks, and connects.
        /// On successful connection, requests the agent list, session list, and a health ping,
        /// then restores the previous session from <see cref="EditorPrefs"/> if one exists.
        /// </summary>
        /// <returns>A Task that completes when the connection and initial requests are done.</returns>
        private async Task ConnectWebSocketAsync()
        {
            if (_wsClient != null)
            {
                if (_wsStateHandler != null)
                {
                    _wsClient.OnStateChanged -= _wsStateHandler;
                }

                if (_wsMessageHandler != null)
                {
                    _wsClient.OnMessageReceived -= _wsMessageHandler;
                }

                if (_wsErrorHandler != null)
                {
                    _wsClient.OnError -= _wsErrorHandler;
                }

                _wsClient.Dispose();
            }

            var wsUrl = $"ws://localhost:{GameDeckSettings.Instance._agentPort}";
            _wsClient = new WebSocketClient(wsUrl);

            _wsStateHandler = state =>
            {
                EditorApplication.delayCall += () => UpdateConnectionUI(state);
            };
            _wsMessageHandler = json =>
            {
                EditorApplication.delayCall += () => HandleServerMessage(json);
            };
            _wsErrorHandler = err =>
            {
                EditorApplication.delayCall += () => Debug.LogWarning($"[Game Deck Chat] {err}");
            };

            _wsClient.OnStateChanged += _wsStateHandler;
            _wsClient.OnMessageReceived += _wsMessageHandler;
            _wsClient.OnError += _wsErrorHandler;

            await _wsClient.ConnectAsync();

            if (_wsClient?.State == WebSocketClient.EConnectionState.CONNECTED)
            {
                var client = _wsClient;

                await client.SendAsync("{\"action\":\"list-agents\"}");
                await client.SendAsync("{\"action\":\"list-commands\"}");
                await client.SendAsync("{\"action\":\"list-sessions\"}");
                await client.SendAsync("{\"action\":\"ping\"}");

                var sessionToRestore = _currentSessionId ?? EditorPrefs.GetString(SESSION_PREF_KEY, "");

                if (!string.IsNullOrEmpty(sessionToRestore))
                {
                    _currentSessionId = sessionToRestore;
                    _hasMessages = true;

                    if (_welcomeScreen != null)
                    {
                        _welcomeScreen.style.display = DisplayStyle.None;
                    }

                    await client.SendAsync($"{{\"action\":\"get-session\",\"sessionId\":{EscapeJson(sessionToRestore)}}}");
                }
            }
        }

        /// <summary>
        /// Updates the status label and indicator dot CSS classes to reflect the current
        /// <see cref="WebSocketClient.EConnectionState"/> (Connected, Connecting/Reconnecting, or Disconnected).
        /// </summary>
        /// <param name="state">The new WebSocket connection state.</param>
        private void UpdateConnectionUI(WebSocketClient.EConnectionState state)
        {
            if (_statusLabel == null || _statusIndicator == null)
            {
                return;
            }

            _statusIndicator.RemoveFromClassList("status-indicator--connected");
            _statusIndicator.RemoveFromClassList("status-indicator--connecting");

            switch (state)
            {
                case WebSocketClient.EConnectionState.CONNECTED:
                    _statusLabel.text = "Connected";
                    _statusIndicator.AddToClassList("status-indicator--connected");
                    break;
                case WebSocketClient.EConnectionState.CONNECTING:
                case WebSocketClient.EConnectionState.RECONNECTING:
                    _statusLabel.text = "Connecting...";
                    _statusIndicator.AddToClassList("status-indicator--connecting");
                    break;
                default:
                    _statusLabel.text = "Disconnected";
                    break;
            }
        }

        #endregion
    }
}