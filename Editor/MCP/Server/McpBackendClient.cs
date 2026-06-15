#nullable enable

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using GameDeck.MCP.Utils;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// The Editor side of the sidecar transport. Maintains an <b>outbound</b>
    /// connection to the sidecar's loopback backend port (see
    /// <see cref="SidecarManager"/>), reads JSON-RPC request frames, dispatches
    /// each to <see cref="McpRequestHandler.HandleRequest"/> on a worker thread,
    /// and writes the response frame back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Because this is an outbound client (not a listener), a Unity domain reload
    /// merely drops the connection — there is no listen socket to leak. A
    /// background loop ensures the sidecar is running and (re)connects with a
    /// short backoff, so reconnection after a reload is automatic.
    /// </para>
    /// <para>
    /// Frame format (both directions): <c>[cid:int32 BE][len:int32 BE][payload UTF-8]</c>.
    /// <c>cid</c> is assigned by the sidecar so concurrent clients cannot collide.
    /// Tool execution still serializes on the Unity main thread inside
    /// <see cref="Utils.MainThreadDispatcher"/>, exactly as before.
    /// </para>
    /// </remarks>
    internal static class McpBackendClient
    {
        #region CONSTANTS

        private const int RECONNECT_DELAY_MS = 500;
        private const int FRAME_HEADER_SIZE = 8;
        private const string LOOP_THREAD_NAME = "MCP-BackendClient";
        private const string WORKER_THREAD_NAME = "MCP-BackendWorker";

        #endregion

        #region FIELDS

        private static McpRequestHandler? _handler;
        private static string _host = "localhost";
        private static int _port;
        private static volatile bool _running;
        private static Thread? _loopThread;
        private static TcpClient? _client;
        private static readonly object _writeLock = new();

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Ensures the sidecar is up, connects to its backend port, and serves
        /// frames until the connection drops — then retries. Runs until
        /// <see cref="Stop"/> is called.
        /// </summary>
        private static void ConnectionLoop()
        {
            while (_running)
            {
                try
                {
                    SidecarManager.EnsureRunning(_host, _port);

                    if (!SidecarManager.TryGetBackendPort(_host, _port, out int backendPort))
                    {
                        Thread.Sleep(RECONNECT_DELAY_MS);
                        continue;
                    }

                    var client = new TcpClient();
                    client.Connect(IPAddress.Loopback, backendPort);
                    _client = client;
                    McpLogger.Info($"MCP backend connected to sidecar on :{backendPort}.");

                    Serve(client);
                }
                catch (Exception ex)
                {
                    if (_running)
                    {
                        McpLogger.Error($"MCP backend connection error: {ex.Message}");
                    }
                }
                finally
                {
                    try
                    { 
                        _client?.Close(); 
                    }
                    catch 
                    { 
                        /* best-effort */ 
                    }

                    _client = null;
                }

                if (_running)
                {
                    Thread.Sleep(RECONNECT_DELAY_MS);
                }
            }
        }

        /// <summary>
        /// Reads request frames from the backend stream and dispatches each to a
        /// worker thread. Returns when the stream closes or errors (triggering a
        /// reconnect). A worker per in-flight request mirrors the old
        /// per-connection threading so a slow tool call can't block other requests
        /// (and tool calls still serialize on the main thread internally).
        /// </summary>
        private static void Serve(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            while (_running && client.Connected)
            {
                byte[]? header = ReadExactly(stream, FRAME_HEADER_SIZE);

                if (header == null)
                {
                    break;
                }

                int cid = ReadInt32BE(header, 0);
                int len = ReadInt32BE(header, 4);

                if (len < 0 || len > McpConstants.MAX_REQUEST_BODY_SIZE)
                {
                    McpLogger.Error($"MCP backend: frame length {len} out of range — dropping connection.");
                    break;
                }

                byte[]? payloadBytes = ReadExactly(stream, len);

                if (payloadBytes == null)
                {
                    break;
                }

                string payload = Encoding.UTF8.GetString(payloadBytes);

                var worker = new Thread(() => HandleFrame(stream, cid, payload))
                {
                    Name = WORKER_THREAD_NAME,
                    IsBackground = true,
                };
                worker.Start();
            }
        }

        /// <summary>
        /// Runs the request through the handler (tool calls marshal to the main
        /// thread internally) and writes the response frame back.
        /// </summary>
        private static void HandleFrame(NetworkStream stream, int cid, string payload)
        {
            string response;

            try
            {
                response = _handler?.HandleRequest(payload)
                           ?? McpProtocol.BuildErrorResponse("null", McpProtocol.INTERNAL_ERROR, "Handler not initialized");
            }
            catch (Exception ex)
            {
                McpLogger.Error($"MCP backend handler error: {ex.Message}");
                response = McpProtocol.BuildErrorResponse("null", McpProtocol.INTERNAL_ERROR, ex.Message);
            }

            WriteFrame(stream, cid, response ?? string.Empty);
        }

        /// <summary>Writes a single <c>[cid][len][payload]</c> frame under a lock so concurrent workers don't interleave.</summary>
        private static void WriteFrame(NetworkStream stream, int cid, string payload)
        {
            byte[] body = Encoding.UTF8.GetBytes(payload);
            byte[] header = new byte[FRAME_HEADER_SIZE];
            WriteInt32BE(header, 0, cid);
            WriteInt32BE(header, 4, body.Length);

            lock (_writeLock)
            {
                try
                {
                    stream.Write(header, 0, header.Length);

                    if (body.Length > 0)
                    {
                        stream.Write(body, 0, body.Length);
                    }
                }
                catch (Exception ex)
                {
                    McpLogger.Error($"MCP backend write failed (cid {cid}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes, or <c>null</c> on EOF/error.
        /// </summary>
        private static byte[]? ReadExactly(NetworkStream stream, int count)
        {
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] buffer = new byte[count];
            int read = 0;

            while (read < count)
            {
                int n;

                try
                {
                    n = stream.Read(buffer, read, count - read);
                }
                catch
                {
                    return null;
                }

                if (n <= 0)
                {
                    return null;
                }

                read += n;
            }

            return buffer;
        }

        /// <summary>
        /// Decodes a big-endian 32-bit integer from <paramref name="b"/> starting
        /// at <paramref name="offset"/> (the frame header encoding).
        /// </summary>
        /// <param name="b">Buffer holding at least 4 bytes from <paramref name="offset"/>.</param>
        /// <param name="offset">Index of the most-significant byte.</param>
        /// <returns>The decoded 32-bit value.</returns>
        private static int ReadInt32BE(byte[] b, int offset)
        {
            return (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
        }

        /// <summary>
        /// Encodes <paramref name="value"/> as a big-endian 32-bit integer into
        /// <paramref name="b"/> starting at <paramref name="offset"/> (the frame
        /// header encoding).
        /// </summary>
        /// <param name="b">Destination buffer with room for 4 bytes from <paramref name="offset"/>.</param>
        /// <param name="offset">Index of the most-significant byte.</param>
        /// <param name="value">The 32-bit value to encode.</param>
        private static void WriteInt32BE(byte[] b, int offset, int value)
        {
            b[offset] = (byte)((value >> 24) & 0xFF);
            b[offset + 1] = (byte)((value >> 16) & 0xFF);
            b[offset + 2] = (byte)((value >> 8) & 0xFF);
            b[offset + 3] = (byte)(value & 0xFF);
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Starts the connection loop on a background thread. No-op if already
        /// running. Called once per domain load from <see cref="McpServer"/>.
        /// </summary>
        /// <param name="handler">The request handler that services relayed frames.</param>
        /// <param name="host">Configured public host (read on the main thread by the caller).</param>
        /// <param name="port">Configured public port (read on the main thread by the caller).</param>
        public static void Start(McpRequestHandler handler, string host, int port)
        {
            _handler = handler;
            _host = host;
            _port = port;

            if (_running)
            {
                return;
            }

            _running = true;
            _loopThread = new Thread(ConnectionLoop)
            {
                Name = LOOP_THREAD_NAME,
                IsBackground = true,
            };

            _loopThread.Start();
        }

        /// <summary>
        /// Stops the connection loop and closes the outbound socket so the sidecar
        /// sees a clean disconnect. Safe to call when not running. Invoked before a
        /// domain reload and on Editor quit.
        /// </summary>
        public static void Stop()
        {
            _running = false;
            try
            { 
                _client?.Close();
            }
            catch 
            { 
                /* best-effort */ 
            }

            _client = null;
        }

        #endregion
    }
}