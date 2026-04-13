#nullable enable
using GameDeck.Editor.Settings;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Static configuration for the MCP WebSocket server.
    /// Reads values from <see cref="GameDeckSettings"/> so they can be changed
    /// via <b>Project Settings &gt; MCP Game Deck</b>.
    /// </summary>
    /// <remarks>
    /// Modifying these values after the server has started has no effect on the running
    /// listener — a restart via <see cref="McpServer.StopServer"/> followed by
    /// <see cref="McpServer.StartServer"/> is required for changes to take effect.
    /// </remarks>
    public static class McpServerConfig
    {
        #region PROPERTIES

        /// <summary>
        /// Gets the TCP port the WebSocket server listens on.
        /// Backed by <see cref="GameDeckSettings._mcpPort"/> (default <c>8090</c>).
        /// </summary>
        public static int Port => GameDeckSettings.Instance._mcpPort;

        /// <summary>
        /// Gets the hostname the server binds to.
        /// Backed by <see cref="GameDeckSettings._host"/> (default <c>"localhost"</c>).
        /// Use <c>"*"</c> to accept connections on all interfaces.
        /// </summary>
        public static string Host => GameDeckSettings.Instance._host;

        #endregion
    }
}
