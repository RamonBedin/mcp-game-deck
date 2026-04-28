#nullable enable

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
        private const string TOOLTIP = "MCP Game Deck";

        #endregion

        #region FIELDS

        private static readonly EPinStatus _testStatus = EPinStatus.CONNECTED;
        private static readonly bool _testUpdateAvailable = false;

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
            var icon = PinIcon.BuildComposite(_testStatus, _testUpdateAvailable);
            var content = new MainToolbarContent(icon, TOOLTIP);
            return new MainToolbarButton(content, OnPinClicked);
        }

        #endregion
    }
}