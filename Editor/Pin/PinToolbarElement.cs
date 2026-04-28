#nullable enable

using GameDeck.Editor.Settings;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Registers the MCP Game Deck pin in the Unity Editor's main toolbar via the
    /// official <see cref="MainToolbarElementAttribute"/> API. Surfaces connection
    /// status with an icon plus colored status dot, opens the chat on left-click,
    /// and exposes Settings / Copy URL / Show install folder / About through a
    /// right-click context menu.
    /// </summary>
    public static class PinToolbarElement
    {
        #region CONSTANTS

        public const string ELEMENT_PATH = "MCP Game Deck/Pin";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Stub click handler. Replaced by the real launch / focus flow in a later task.
        /// </summary>
        private static void OnPinClicked()
        {
            UnityEngine.Debug.Log("[MCP] Pin clicked.");
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Registers the pin element with Unity's main toolbar. Unity invokes this once
        /// at editor startup (and again on each <see cref="MainToolbar.Refresh"/> call)
        /// thanks to <see cref="MainToolbarElementAttribute"/>.
        /// </summary>
        /// <returns>A <see cref="MainToolbarButton"/> describing the pin's content and behavior.</returns>
        [MainToolbarElement(ELEMENT_PATH, defaultDockPosition = MainToolbarDockPosition.Left)]
        public static MainToolbarElement CreatePin()
        {
            var status = PinPolling.CurrentStatus;
            var port = GameDeckSettings.Instance._mcpPort;
            var updateAvailable = PinPolling.UpdateAvailable;
            var updateVersion = PinPolling.UpdateVersion;

            var icon = PinIcon.BuildComposite(status, updateAvailable);
            var tooltip = PinTooltip.GetText(status, port, updateAvailable, updateVersion);
            var content = new MainToolbarContent(icon, tooltip);
            return new MainToolbarButton(content, OnPinClicked);
        }

        #endregion
    }
}