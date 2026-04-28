#nullable enable

using GameDeck.Editor.Settings;
using UnityEditor.Toolbars;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Registers the MCP Game Deck pin in the Unity Editor's main toolbar via the
    /// official <see cref="MainToolbarElementAttribute"/> API. Surfaces connection
    /// status with an icon plus colored status dot, and opens a dropdown menu on
    /// click that exposes Open Chat / Settings / Copy MCP Server URL / Show install
    /// folder / About.
    /// </summary>
    public static class PinToolbarElement
    {
        #region CONSTANTS

        public const string ELEMENT_PATH = "MCP Game Deck/Pin";

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Registers the pin element with Unity's main toolbar. Unity invokes this once
        /// at editor startup (and again on each <see cref="MainToolbar.Refresh"/> call)
        /// thanks to <see cref="MainToolbarElementAttribute"/>.
        /// </summary>
        /// <returns>A <see cref="MainToolbarDropdown"/> describing the pin's content and behavior.</returns>
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
            return new MainToolbarDropdown(content, OnDropdownClicked);
        }

        #endregion

        #region EVENT HANDLERS

        /// <summary>
        /// Click handler for the dropdown trigger. Receives the screen-space rect of
        /// the rendered button so the dropdown menu can anchor under it.
        /// </summary>
        /// <param name="anchorRect">Screen-space rect of the dropdown button. Forwarded
        /// to <see cref="PinDropdownMenu.Show(Rect)"/>.</param>
        private static void OnDropdownClicked(Rect anchorRect)
        {
            PinDropdownMenu.Show(anchorRect);
        }

        #endregion
    }
}
