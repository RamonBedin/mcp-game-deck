#nullable enable

using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Scene-view toolbar overlay that surfaces MCP Game Deck connection status inside Unity.
    /// </summary>
    /// <remarks>
    /// Attaches to the Scene view by default; users can drag-reposition it.
    /// Renders an icon with a colored status dot (connected / busy / disconnected / not
    /// installed) and a blue update-available badge. Left-click launches or focuses the
    /// external Tauri app; right-click opens a context menu with Settings, Copy MCP Server
    /// URL, Show install folder, and About.
    /// </remarks>
    [Overlay(typeof(SceneView), OVERLAY_ID, OVERLAY_DISPLAY_NAME)]
    public class PinOverlay : Overlay
    {
        #region CONSTANTS

        private const string OVERLAY_ID = "mcp-game-deck-pin";
        private const string OVERLAY_DISPLAY_NAME = "MCP Game Deck Pin";

        #endregion

        #region PUBLIC METHODS

        public override void OnCreated()
        {
            base.OnCreated();
            McpLogger.Info("Pin overlay attached.");
        }

        public override VisualElement CreatePanelContent()
        {
            var root = new VisualElement
            {
                name = "pin-overlay-root",
                style =
                {
                    width = 24,
                    height = 24,
                },
            };
            return root;
        }

        #endregion
    }
}