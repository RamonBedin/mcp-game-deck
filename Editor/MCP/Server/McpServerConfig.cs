#nullable enable
using GameDeck.Editor.Settings;

namespace GameDeck.MCP.Server
{
    /// <summary>
    /// Static configuration for the MCP server's public endpoint.
    /// Reads values from <see cref="GameDeckSettings"/> so they can be changed
    /// via <b>Project Settings &gt; MCP Game Deck</b>.
    /// </summary>
    /// <remarks>
    /// These values tell <see cref="SidecarManager"/> what host/port the sidecar
    /// should bind. Changing the port in Project Settings respawns the sidecar on
    /// the new port at the next domain load; the Tauri app must be relaunched to
    /// pick up the new port (it reads it from the launch environment).
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