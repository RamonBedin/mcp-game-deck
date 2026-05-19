#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEditor;
using GameDeck.MCP.Discovery;
using GameDeck.MCP.Models;
using GameDeck.MCP.Registry;
using GameDeck.MCP.Utils;
using System.IO;
using System.Security.Cryptography;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Singleton TCP server that implements a minimal HTTP transport for MCP JSON-RPC.
    /// Uses <see cref="TcpListener"/> with <see cref="SocketOptionName.ReuseAddress"/>
    /// so the port is released instantly on stop — no EADDRINUSE after assembly reloads.
    /// Lifecycle: stop before assembly reload, restart after. Server stays up across
    /// play mode transitions so MCP tools remain available while inspecting / debugging
    /// a running game (matches behavior of other Unity MCP servers — see IvanMurzak/Unity-MCP,
    /// mitchchristow/unity-mcp).
    /// </summary>
    [InitializeOnLoad]
    public static class McpServer
    {
        #region CONSTRUCTOR

        static McpServer()
        {
            SubscribeEditorEvents();
            DiscoverAndRegister();
            StartServer();
        }

        #endregion

        #region FIELDS

        private static TcpListener? _tcpListener;
        private static Thread? _acceptThread;
        private static volatile bool _running;

        private static ToolRegistry? _toolRegistry;
        private static ResourceRegistry? _resourceRegistry;
        private static PromptRegistry? _promptRegistry;
        private static McpRequestHandler? _handler;
        private static bool _discovered;
        private static string? _authToken;
        private static int _rateLimitCount;
        private static long _rateLimitWindowStart;
        private static bool _eventsSubscribed;

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Resolves a host string (e.g. <c>"localhost"</c>, <c>"*"</c>, <c>"127.0.0.1"</c>)
        /// to the corresponding <see cref="IPAddress"/> for binding the TCP listener.
        /// Falls back to <see cref="IPAddress.Loopback"/> when the host cannot be resolved.
        /// </summary>
        /// <param name="host">The host string from configuration.</param>
        /// <returns>An <see cref="IPAddress"/> suitable for <see cref="TcpListener"/>.</returns>
        private static IPAddress ResolveBindAddress(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || host == McpConstants.HOST_LOCALHOST)
            {
                return IPAddress.Loopback;
            }

            if (host == McpConstants.HOST_WILDCARD || host == McpConstants.HOST_ANY_ADDRESS)
            {
                McpLogger.Error(
                    $"Host '{host}' would expose the MCP server to the network without authentication. " +
                    "Binding to localhost instead. Remove wildcard host in Project Settings > MCP Game Deck.");
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(host, out IPAddress? parsed))
            {
                if (!IPAddress.IsLoopback(parsed))
                {
                    McpLogger.Error($"Host '{host}' is not a loopback address. Binding to localhost instead for security.");
                    return IPAddress.Loopback;
                }

                return parsed;
            }

            McpLogger.Info($"Unknown host '{host}' — falling back to localhost.");
            return IPAddress.Loopback;
        }

        /// <summary>
        /// Loads the auth token from disk if it exists, otherwise generates a new one.
        /// This ensures the token remains stable across assembly reloads so the
        /// proxy (which reads it once at startup) stays authorized.
        /// </summary>
        /// <returns>A 32-character lowercase hex token string.</returns>
        private static string GenerateAndWriteAuthToken()
        {
            try
            {
                if (File.Exists(McpConstants.AUTH_TOKEN_FILE))
                {
                    string existing = File.ReadAllText(McpConstants.AUTH_TOKEN_FILE).Trim();

                    if (existing.Length == McpConstants.AUTH_TOKEN_BYTE_LENGTH * 2)
                    {
                        return existing;
                    }
                }
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Failed to read existing auth token: {ex}");
            }

            byte[] bytes = new byte[McpConstants.AUTH_TOKEN_BYTE_LENGTH];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            string token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            try
            {
                Directory.CreateDirectory(McpConstants.AUTH_TOKEN_DIR);
                File.WriteAllText(McpConstants.AUTH_TOKEN_FILE, token);
                McpLogger.Info($"Auth token written to {McpConstants.AUTH_TOKEN_FILE}");
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Failed to write auth token: {ex}");
            }

            return token;
        }

        /// <summary>
        /// Validates the Authorization header value against the current auth token.
        /// </summary>
        /// <param name="authHeader">The raw Authorization header value (e.g. "Bearer abc123...").</param>
        /// <returns><c>true</c> if the token matches or no token is configured; <c>false</c> otherwise.</returns>
        private static bool IsAuthorized(string? authHeader)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                return true;
            }

            if (string.IsNullOrEmpty(authHeader))
            {
                return false;
            }

            string trimmed = authHeader!.Trim();

            if (!trimmed.StartsWith(McpConstants.AUTH_BEARER_PREFIX, StringComparison.Ordinal))
            {
                return false;
            }

            return trimmed[McpConstants.AUTH_BEARER_PREFIX.Length..] == _authToken;
        }

        /// <summary>
        /// Checks if the global request rate has been exceeded.
        /// Uses a sliding window of <see cref="McpConstants.RATE_LIMIT_WINDOW_TICKS"/>
        /// allowing up to <see cref="McpConstants.RATE_LIMIT_MAX_REQUESTS"/> per window.
        /// Thread-safe via <see cref="Interlocked"/>.
        /// </summary>
        /// <returns><c>true</c> if the rate limit has been exceeded; <c>false</c> otherwise.</returns>
        private static bool IsRateLimited()
        {
            long now = DateTime.UtcNow.Ticks;
            long windowStart = Interlocked.Read(ref _rateLimitWindowStart);

            if (now - windowStart > McpConstants.RATE_LIMIT_WINDOW_TICKS)
            {
                Interlocked.Exchange(ref _rateLimitWindowStart, now);
                Interlocked.Exchange(ref _rateLimitCount, 1);
                return false;
            }

            int count = Interlocked.Increment(ref _rateLimitCount);
            return count > McpConstants.RATE_LIMIT_MAX_REQUESTS;
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Starts the TCP listener on the configured port.
        /// No-op if already running. Uses <see cref="SocketOptionName.ReuseAddress"/>
        /// so the port can be rebound instantly after a stop.
        /// </summary>
        public static void StartServer()
        {
            if (_running)
            {
                return;
            }

            if (!_discovered)
            {
                DiscoverAndRegister();
            }
            try
            {
                IPAddress bindAddress = ResolveBindAddress(McpServerConfig.Host);
                _tcpListener = new TcpListener(bindAddress, McpServerConfig.Port);
                _tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Start();

                _running = true;
                _authToken = GenerateAndWriteAuthToken();

                _acceptThread = new Thread(AcceptLoop)
                {
                    Name = McpConstants.THREAD_NAME_ACCEPT_LOOP,
                    IsBackground = true,
                };
                _acceptThread.Start();

                McpLogger.Info(
                    $"MCP Server started on :{McpServerConfig.Port} with " +
                    $"{_toolRegistry?.Count ?? 0} tools, " +
                    $"{_resourceRegistry?.Count ?? 0} resources, " +
                    $"{_promptRegistry?.Count ?? 0} prompts.");
            }
            catch (Exception ex)
            {
                McpLogger.Error($"MCP Server failed to start: {ex}");
                try
                {
                    _tcpListener?.Stop();
                }
                catch(Exception stopEx)
                {
                    Debug.LogWarning($"[MCP Server] Cleanup failed while stopping listener: {stopEx}");
                }

                _tcpListener = null;
            }
        }

        /// <summary>
        /// Stops the TCP listener and releases the port.
        /// Safe to call multiple times or when not running.
        /// </summary>
        public static void StopServer()
        {
            _running = false;
            TcpListener? listener = _tcpListener;
            _tcpListener = null;

            if (listener != null)
            {
                try
                {
                    listener.Stop();
                }
                catch (Exception ex)
                {
                    McpLogger.Error($"McpServer.StopServer: {ex}");
                }
            }

            _acceptThread = null;
        }

        #endregion

        #region DISCOVERY

        /// <summary>
        /// Runs tool, resource, and prompt discovery and builds registries and handler.
        /// Only runs once per domain load.
        /// </summary>
        private static void DiscoverAndRegister()
        {
            if (_discovered)
            {
                return;
            }
            try
            {
                MainThreadDispatcher.Initialize();

                _toolRegistry = new ToolRegistry();
                List<McpToolInfo> tools = ToolDiscovery.DiscoverTools();

                for (int i = 0; i < tools.Count; i++)
                {
                    _toolRegistry.Register(tools[i]);
                }

                _resourceRegistry = new ResourceRegistry();
                List<McpResourceInfo> resources = ResourceDiscovery.DiscoverResources();

                for (int i = 0; i < resources.Count; i++)
                {
                    _resourceRegistry.Register(resources[i]);
                }

                _promptRegistry = new PromptRegistry();
                List<McpPromptInfo> prompts = PromptDiscovery.DiscoverPrompts();

                for (int i = 0; i < prompts.Count; i++)
                {
                    _promptRegistry.Register(prompts[i]);
                }

                _handler = new McpRequestHandler(_toolRegistry, _resourceRegistry, _promptRegistry);
                _discovered = true;
            }
            catch (Exception ex)
            {
                McpLogger.Error($"MCP discovery failed: {ex}");
            }
        }

        #endregion

        #region EVENT SUBSCRIPTION

        /// <summary>
        /// Subscribes to Editor lifecycle events exactly once. Guarded by
        /// <see cref="_eventsSubscribed"/> to prevent duplicate subscriptions
        /// across domain reloads.
        /// </summary>
        private static void SubscribeEditorEvents()
        {
            if (_eventsSubscribed)
            {
                return;
            }

            _eventsSubscribed = true;
            EditorApplication.quitting += HandleEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += HandleAfterAssemblyReload;
        }

        #endregion

        #region LIFECYCLE CALLBACKS

        /// <summary>
        /// Called before Unity recompiles scripts. Stops the server to release the port.
        /// </summary>
        private static void HandleBeforeAssemblyReload()
        {
            if (_running)
            {
                StopServer();
            }
        }

        /// <summary>
        /// Called after Unity finishes recompiling. The static constructor already
        /// restarts the server on domain reload, so this is a safety net.
        /// </summary>
        private static void HandleAfterAssemblyReload()
        {
            if (!_running)
            {
                StartServer();
            }
        }

        /// <summary>
        /// Called when the Unity Editor is quitting. Final cleanup.
        /// </summary>
        private static void HandleEditorQuitting()
        {
            StopServer();

            EditorApplication.quitting -= HandleEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload -= HandleAfterAssemblyReload;
            _eventsSubscribed = false;
        }

        #endregion

        #region ACCEPT LOOP

        /// <summary>
        /// Runs on a background thread, accepting TCP connections and dispatching them.
        /// </summary>
        private static void AcceptLoop()
        {
            while (_running)
            {
                TcpClient? client = null;
                try
                {
                    TcpListener? listener = _tcpListener;

                    if (listener == null)
                    {
                        break;
                    }

                    client = listener.AcceptTcpClient();
                    TcpClient captured = client;
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(captured));
                    client = null;
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_running)
                    {
                        break;
                    }

                    McpLogger.Error($"AcceptLoop error: {ex}");
                }
                finally
                {
                    if (client != null)
                    {
                        try
                        {
                            client.Dispose();
                        }
                        catch (Exception ex)
                        {
                            McpLogger.Error($"AcceptLoop failed to dispose TcpClient: {ex}");
                        }
                    }
                }
            }
        }

        #endregion

        #region HTTP CLIENT HANDLER

        /// <summary>
        /// Handles a TCP client connection with HTTP keep-alive support.
        /// Processes multiple requests on the same connection to avoid
        /// TCP TIME_WAIT buildup during rapid sequential tool calls.
        /// </summary>
        /// <param name="client">The connected TCP client.</param>
        private static void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = McpConstants.RECEIVE_TIMEOUT_MS;
                    client.SendTimeout = McpConstants.SEND_TIMEOUT_MS;

                    using NetworkStream stream = client.GetStream();

                    while (_running)
                    {
                        string? requestLine = ReadLine(stream);

                        if (requestLine is null || requestLine.Length == 0)
                        {
                            break;
                        }

                        string[] requestParts = requestLine.Split(' ');

                        if (requestParts.Length < 2)
                        {
                            break;
                        }

                        string httpMethod = requestParts[0];
                        int contentLength = 0;
                        bool keepAlive = true;
                        string? authHeader = null;
                        string? headerLine;

                        while ((headerLine = ReadLine(stream)) != null && headerLine.Length > 0)
                        {
                            if (headerLine.StartsWith(McpConstants.HEADER_CONTENT_LENGTH, StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(headerLine[McpConstants.HEADER_CONTENT_LENGTH_SIZE..].Trim(), out contentLength);
                            }
                            else if (headerLine.StartsWith(McpConstants.HEADER_CONNECTION, StringComparison.OrdinalIgnoreCase))
                            {
                                keepAlive = !headerLine[McpConstants.HEADER_CONNECTION_SIZE..].Trim().Equals(McpConstants.CONNECTION_CLOSE, StringComparison.OrdinalIgnoreCase);
                            }
                            else if (headerLine.StartsWith(McpConstants.HEADER_AUTHORIZATION, StringComparison.OrdinalIgnoreCase))
                            {
                                authHeader = headerLine[McpConstants.HEADER_AUTHORIZATION_SIZE..];
                            }
                        }

                        string responseBody;
                        int statusCode;

                        if (string.Equals(httpMethod, McpConstants.HTTP_METHOD_POST, StringComparison.OrdinalIgnoreCase) && !IsAuthorized(authHeader))
                        {
                            WriteHttpResponse(stream, McpConstants.HTTP_UNAUTHORIZED, "{\"error\":\"Unauthorized\"}", keepAlive: false);
                            break;
                        }

                        if (string.Equals(httpMethod, McpConstants.HTTP_METHOD_POST, StringComparison.OrdinalIgnoreCase) && IsRateLimited())
                        {
                            WriteHttpResponse(stream, McpConstants.HTTP_TOO_MANY_REQUESTS, "{\"error\":\"Rate limit exceeded\"}", keepAlive: false);
                            break;
                        }

                        if (string.Equals(httpMethod, McpConstants.HTTP_METHOD_POST, StringComparison.OrdinalIgnoreCase) && contentLength > 0)
                        {
                            if (contentLength > McpConstants.MAX_REQUEST_BODY_SIZE)
                            {
                                statusCode = McpConstants.HTTP_CONTENT_TOO_LARGE;
                                responseBody = "";
                                WriteHttpResponse(stream, statusCode, responseBody, keepAlive: false);
                                break;
                            }

                            byte[] bodyBytes = new byte[contentLength];
                            int totalRead = 0;

                            while (totalRead < contentLength)
                            {
                                int read = stream.Read(bodyBytes, totalRead, contentLength - totalRead);

                                if (read <= 0)
                                {
                                    break;
                                }

                                totalRead += read;
                            }

                            string body = Encoding.UTF8.GetString(bodyBytes, 0, totalRead);

                            if (string.IsNullOrWhiteSpace(body))
                            {
                                statusCode = McpConstants.HTTP_BAD_REQUEST;
                                responseBody = "";
                            }
                            else
                            {
                                try
                                {
                                    if (_handler == null)
                                    {
                                        responseBody = McpProtocol.BuildErrorResponse("null", McpProtocol.INTERNAL_ERROR, "Handler not initialized");
                                    }
                                    else
                                    {
                                        responseBody = _handler.HandleRequest(body);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    McpLogger.Error($"HandleClient handler error: {ex}");
                                    responseBody = McpProtocol.BuildErrorResponse("null", McpProtocol.INTERNAL_ERROR, ex.Message);
                                }

                                if (string.IsNullOrEmpty(responseBody))
                                {
                                    statusCode = McpConstants.HTTP_NO_CONTENT;
                                    responseBody = "";
                                }
                                else
                                {
                                    statusCode = McpConstants.HTTP_OK;
                                }
                            }
                        }
                        else if (string.Equals(httpMethod, McpConstants.HTTP_METHOD_GET, StringComparison.OrdinalIgnoreCase))
                        {
                            statusCode = McpConstants.HTTP_OK;
                            responseBody = McpConstants.STATUS_OK_JSON;
                        }
                        else if (string.Equals(httpMethod, McpConstants.HTTP_METHOD_OPTIONS, StringComparison.OrdinalIgnoreCase))
                        {
                            statusCode = McpConstants.HTTP_NO_CONTENT;
                            responseBody = "";
                        }
                        else
                        {
                            statusCode = McpConstants.HTTP_METHOD_NOT_ALLOWED;
                            responseBody = "";
                        }

                        WriteHttpResponse(stream, statusCode, responseBody, keepAlive);

                        if (!keepAlive)
                        {
                            break;
                        }
                    }
                }
                catch (IOException)
                {
                    return;
                }
                catch (SocketException)
                {
                    return;
                }
                catch(Exception ex)
                {
                    if (_running)
                    {
                        McpLogger.Error($"HandleClient error: {ex}");
                    }
                }
            }
        }

        /// <summary>
        /// Writes a complete HTTP/1.1 response with CORS headers to the stream.
        /// Supports keep-alive to allow multiple requests per connection.
        /// </summary>
        /// <param name="stream">The network stream to write to.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="body">The response body (may be empty).</param>
        /// <param name="keepAlive">Whether to keep the connection open for more requests.</param>
        private static void WriteHttpResponse(NetworkStream stream, int statusCode, string body, bool keepAlive = true)
        {
            string statusText = statusCode switch
            {
                McpConstants.HTTP_OK => "OK",
                McpConstants.HTTP_NO_CONTENT => "No Content",
                McpConstants.HTTP_BAD_REQUEST => "Bad Request",
                McpConstants.HTTP_UNAUTHORIZED => "Unauthorized",
                McpConstants.HTTP_TOO_MANY_REQUESTS => "Too Many Requests",
                McpConstants.HTTP_METHOD_NOT_ALLOWED => "Method Not Allowed",
                McpConstants.HTTP_CONTENT_TOO_LARGE => "Content Too Large",
                McpConstants.HTTP_INTERNAL_ERROR => "Internal Server Error",
                _ => "OK"
            };

            byte[] bodyBytes = string.IsNullOrEmpty(body) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(body);

            StringBuilder sb = new(256);
            sb.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(statusText).Append("\r\n");
            sb.Append("Content-Type: ").Append(McpConstants.CONTENT_TYPE_JSON).Append("\r\n");
            sb.Append("Content-Length: ").Append(bodyBytes.Length).Append("\r\n");
            sb.Append("Access-Control-Allow-Origin: http://localhost\r\n");
            sb.Append("Access-Control-Allow-Methods: POST, GET, OPTIONS\r\n");
            sb.Append("Access-Control-Allow-Headers: Content-Type, Authorization\r\n");

            if (keepAlive)
            {
                sb.Append("Connection: keep-alive\r\n");
                sb.Append("Keep-Alive: timeout=").Append(McpConstants.KEEP_ALIVE_TIMEOUT_SECONDS).Append("\r\n");
            }
            else
            {
                sb.Append("Connection: close\r\n");
            }

            sb.Append("\r\n");

            byte[] headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);

            if (bodyBytes.Length > 0)
            {
                stream.Write(bodyBytes, 0, bodyBytes.Length);
            }
        }

        /// <summary>
        /// Reads a single line (terminated by \r\n or \n) from a network stream.
        /// Returns null on end-of-stream.
        /// </summary>
        /// <param name="stream">The network stream to read from.</param>
        /// <returns>The line contents without the line terminator, or null on EOF.</returns>
        private static string? ReadLine(NetworkStream stream)
        {
            StringBuilder sb = new(128);
            int b;

            while ((b = stream.ReadByte()) != -1)
            {
                if (b == '\r')
                {
                    int next = stream.ReadByte();

                    if (next != '\n' && next != -1)
                    {
                        sb.Append((char)next);
                    }

                    break;
                }

                if (b == '\n')
                {
                    break;
                }

                sb.Append((char)b);
            }

            return b == -1 && sb.Length == 0 ? null : sb.ToString();
        }

        #endregion
    }
}