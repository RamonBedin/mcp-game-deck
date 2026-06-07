#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using GameDeck.MCP.Discovery;
using GameDeck.MCP.Models;
using GameDeck.MCP.Registry;
using GameDeck.MCP.Utils;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Editor-side entry point for the MCP server. Runs tool/resource/prompt
    /// discovery, ensures the auth token file exists, and drives the
    /// <see cref="McpBackendClient"/> that connects OUT to the long-lived
    /// <see cref="SidecarManager">sidecar</see> process.
    /// </summary>
    /// <remarks>
    /// The TCP listening socket lives in the sidecar, NOT here — the Editor only
    /// makes an outbound connection. This is deliberate: a listener bound inside
    /// the Editor assembly was recreated on every domain reload, and under
    /// connection churn Mono intermittently failed to release the old listen
    /// socket on AppDomain teardown, orphaning a LISTENING socket on the port and
    /// wedging all connections until Unity restarted. With the listener in a
    /// process that never reloads, a domain reload just drops the outbound
    /// connection (no listener to leak) and the next domain reconnects.
    /// </remarks>
    [InitializeOnLoad]
    public static class McpServer
    {
        #region CONSTRUCTOR

        static McpServer()
        {
            SubscribeEditorEvents();
            DiscoverAndRegister();
            EnsureAuthToken();

            if (_handler != null)
            {
                SidecarManager.Initialize();
                McpBackendClient.Start(_handler, McpServerConfig.Host, McpServerConfig.Port);
            }
        }

        #endregion

        #region FIELDS

        private static McpRequestHandler? _handler;
        private static bool _discovered;
        private static bool _eventsSubscribed;

        #endregion

        #region AUTH TOKEN

        /// <summary>
        /// Ensures <see cref="McpConstants.AUTH_TOKEN_FILE"/> contains a valid bearer
        /// token, generating one if missing/malformed. The token is read by the
        /// sidecar (which validates incoming requests), the proxy, and the Rust
        /// client, so keeping it stable across reloads keeps all of them authorized.
        /// </summary>
        private static void EnsureAuthToken()
        {
            try
            {
                if (File.Exists(McpConstants.AUTH_TOKEN_FILE))
                {
                    string existing = File.ReadAllText(McpConstants.AUTH_TOKEN_FILE).Trim();

                    if (existing.Length == McpConstants.AUTH_TOKEN_BYTE_LENGTH * 2)
                    {
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                McpLogger.Error($"Failed to read existing auth token: {ex.Message}");
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
                McpLogger.Error($"Failed to write auth token: {ex.Message}");
            }
        }

        #endregion

        #region DISCOVERY

        /// <summary>
        /// Runs tool, resource, and prompt discovery and builds the request
        /// handler. Only runs once per domain load. Also initializes the
        /// <see cref="MainThreadDispatcher"/> so tool calls can marshal to the
        /// Unity main thread.
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

                var toolRegistry = new ToolRegistry();
                List<McpToolInfo> tools = ToolDiscovery.DiscoverTools();

                for (int i = 0; i < tools.Count; i++)
                {
                    toolRegistry.Register(tools[i]);
                }

                var resourceRegistry = new ResourceRegistry();
                List<McpResourceInfo> resources = ResourceDiscovery.DiscoverResources();

                for (int i = 0; i < resources.Count; i++)
                {
                    resourceRegistry.Register(resources[i]);
                }

                var promptRegistry = new PromptRegistry();
                List<McpPromptInfo> prompts = PromptDiscovery.DiscoverPrompts();

                for (int i = 0; i < prompts.Count; i++)
                {
                    promptRegistry.Register(prompts[i]);
                }

                _handler = new McpRequestHandler(toolRegistry, resourceRegistry, promptRegistry);
                _discovered = true;

                McpLogger.Info($"MCP discovery complete — {tools.Count} tools, {resources.Count} resources, {prompts.Count} prompts.");
            }
            catch (Exception ex)
            {
                McpLogger.Error($"MCP discovery failed: {ex}");
            }
        }

        #endregion

        #region EVENT SUBSCRIPTION

        /// <summary>
        /// Subscribes to Editor lifecycle events exactly once per domain. Guarded by
        /// <see cref="_eventsSubscribed"/> (which resets on reload, so each new
        /// domain re-subscribes).
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
        }

        #endregion

        #region LIFECYCLE CALLBACKS

        /// <summary>
        /// Called before Unity recompiles scripts. Closes the outbound backend
        /// connection cleanly. There is no listener to release — the sidecar keeps
        /// the port — and the new domain's static constructor reconnects.
        /// </summary>
        private static void HandleBeforeAssemblyReload()
        {
            McpBackendClient.Stop();
        }

        /// <summary>
        /// Called when the Unity Editor is quitting. Stops the backend connection,
        /// terminates the sidecar (it is owned by this Editor session), and
        /// unsubscribes.
        /// </summary>
        private static void HandleEditorQuitting()
        {
            McpBackendClient.Stop();
            SidecarManager.KillExisting();

            EditorApplication.quitting -= HandleEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            _eventsSubscribed = false;
        }

        #endregion
    }
}