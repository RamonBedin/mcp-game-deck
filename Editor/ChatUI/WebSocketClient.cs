#nullable enable
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// WebSocket client for communicating with the Agent SDK Server.
    /// Handles connection, reconnection, sending and receiving JSON messages.
    /// </summary>
    public class WebSocketClient : IDisposable
    {
        #region CONSTRUCTOR

        public WebSocketClient(string url = ChatConstants.DEFAULT_WS_URL, int reconnectDelayMs = ChatConstants.DEFAULT_RECONNECT_DELAY_MS)
        {
            _url = url;
            _reconnectDelayMs = reconnectDelayMs;
        }

        #endregion

        #region FIELDS

        private ClientWebSocket? _ws;
        private CancellationTokenSource? _cts;
        private readonly string _url;
        private readonly int _reconnectDelayMs;
        private bool _disposed;

        #endregion

        #region PROPERTIES

        /// <summary>Gets the current connection state of this client.</summary>
        public EConnectionState State { get; private set; } = EConnectionState.DISCONNECTED;

        #endregion

        #region EVENTS

        /// <summary>Raised when the connection state changes.</summary>
        public event Action<EConnectionState>? OnStateChanged;

        /// <summary>Raised when a complete JSON message is received from the server.</summary>
        public event Action<string>? OnMessageReceived;

        /// <summary>Raised when a connection or send error occurs.</summary>
        public event Action<string>? OnError;

        #endregion

        #region ENUM

        /// <summary>Represents the current connection state of the WebSocket client.</summary>
        public enum EConnectionState
        {
            DISCONNECTED,
            CONNECTING,
            CONNECTED,
            RECONNECTING
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Reads messages from the WebSocket in a loop until the connection closes or
        /// <paramref name="ct"/> is cancelled. Accumulates partial frames in a
        /// <see cref="StringBuilder"/> and invokes <see cref="OnMessageReceived"/>
        /// when a complete message arrives. On unexpected disconnect, triggers
        /// <see cref="TryReconnect"/>.
        /// </summary>
        /// <param name="ct">Cancellation token to stop the loop on dispose/disconnect.</param>
        /// <returns>Completes when the connection is closed or cancelled.</returns>
        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[ChatConstants.WS_BUFFER_SIZE];
            var messageBuilder = new StringBuilder();
            try
            {
                while (_ws?.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        SetState(EConnectionState.DISCONNECTED);
                        break;
                    }

                    messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                    if (result.EndOfMessage)
                    {
                        var message = messageBuilder.ToString();
                        messageBuilder.Clear();
                        OnMessageReceived?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                Debug.Log($"[WebSocket] Receive loop cancelled (expected on disconnect): {ex.Message}");
            }
            catch (WebSocketException ex)
            {
                OnError?.Invoke($"WebSocket error: {ex.Message}");
            }
            finally
            {
                if (!_disposed && State != EConnectionState.DISCONNECTED)
                {
                    SetState(EConnectionState.DISCONNECTED);
                    _ = TryReconnect();
                }
            }
        }

        /// <summary>
        /// Waits <see cref="_reconnectDelayMs"/> then attempts to reconnect via
        /// <see cref="ConnectAsync"/>. No-ops if the client has been disposed.
        /// </summary>
        /// <returns>Completes when the reconnection attempt finishes.</returns>
        private async Task TryReconnect()
        {
            if (_disposed)
            {
                return;
            }

            SetState(EConnectionState.RECONNECTING);

            await Task.Delay(_reconnectDelayMs);

            if (!_disposed)
            {
                await ConnectAsync();
            }
        }

        /// <summary>
        /// Updates <see cref="State"/> and raises <see cref="OnStateChanged"/> if the value changed.
        /// </summary>
        /// <param name="state">The new connection state.</param>
        private void SetState(EConnectionState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            OnStateChanged?.Invoke(state);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Connects to the WebSocket server asynchronously. No-op if already connected or connecting.
        /// Retries up to 10 times with increasing delay to handle server restart after Unity reload.
        /// </summary>
        /// <returns>Completes when the connection is established or all retry attempts are exhausted.</returns>
        public async Task ConnectAsync()
        {
            if (State == EConnectionState.CONNECTED || State == EConnectionState.CONNECTING)
            {
                return;
            }

            SetState(EConnectionState.CONNECTING);
            _cts = new CancellationTokenSource();

            for (int attempt = 1; attempt <= ChatConstants.WS_MAX_CONNECT_ATTEMPTS; attempt++)
            {
                if (_disposed)
                {
                    return;
                }
                try
                {
                    _ws?.Dispose();
                    _ws = new ClientWebSocket();
                    await _ws.ConnectAsync(new Uri(_url), _cts.Token);
                    SetState(EConnectionState.CONNECTED);
                    _ = ReceiveLoop(_cts.Token);
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt < ChatConstants.WS_MAX_CONNECT_ATTEMPTS)
                    {
                        int delayMs = Math.Min(ChatConstants.WS_INITIAL_DELAY_MS * attempt, ChatConstants.WS_MAX_DELAY_MS);
                        Debug.Log($"[Game Deck Chat] Connection attempt {attempt}/{ChatConstants.WS_MAX_CONNECT_ATTEMPTS} failed, retrying in {delayMs}ms...");
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        OnError?.Invoke($"Connection failed after {ChatConstants.WS_MAX_CONNECT_ATTEMPTS} attempts: {ex.Message}");
                        SetState(EConnectionState.DISCONNECTED);
                    }
                }
            }
        }

        /// <summary>
        /// Sends a JSON string to the server. Logs an error if not connected.
        /// </summary>
        /// <param name="json">The JSON payload to send.</param>
        /// <returns>Completes when the message has been sent or an error is raised.</returns>
        public async Task SendAsync(string json)
        {
            if (_ws?.State != WebSocketState.Open)
            {
                OnError?.Invoke("Cannot send: not connected.");
                return;
            }

            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Send failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cancels pending operations and releases WebSocket resources.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _cts?.Cancel();
            _ws?.Dispose();
            _ws = null;
            _cts?.Dispose();
            _cts = null;
        }

        #endregion
    }
}
