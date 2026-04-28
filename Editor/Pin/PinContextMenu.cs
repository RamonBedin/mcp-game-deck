#nullable enable

using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Right-click context menu for the toolbar pin. Mirrors a subset of
    /// <see cref="PinDropdownMenu"/>'s items — <c>Settings</c> and
    /// <c>Copy MCP Server URL</c> — and anchors the menu at the cursor's local
    /// position rather than under a button rect.
    /// </summary>
    internal static class PinContextMenu
    {
        #region CONSTANTS

        private const string MENU_ITEM_SETTINGS = "Settings";
        private const string MENU_ITEM_COPY_URL = "Copy MCP Server URL";
        private const string NOTIFICATION_URL_COPIED = "MCP Server URL copied";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Stub handler for the <c>Settings</c> item. Real launch lands in task 4.5
        /// once <c>PinLauncher.LaunchOrFocus</c> exists.
        /// </summary>
        private static void OnSettingsClicked()
        {
            McpLogger.Info("[Pin] Settings clicked");
        }

        /// <summary>
        /// Copies <c>http://{host}:{port}</c> to the system clipboard (using current
        /// <see cref="GameDeckSettings"/>) and surfaces a brief notification on the
        /// focused editor window.
        /// </summary>
        private static void OnCopyUrlClicked()
        {
            var settings = GameDeckSettings.Instance;
            var url = $"http://{settings._host}:{settings._mcpPort}";
            EditorGUIUtility.systemCopyBuffer = url;

            var focused = EditorWindow.focusedWindow;

            if (focused != null)
            {
                focused.ShowNotification(new GUIContent(NOTIFICATION_URL_COPIED));
            }
        }

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Displays the right-click context menu at the given position.
        /// </summary>
        /// <param name="mousePosition">Mouse position in the panel's local coordinate
        /// space (typically <c>MouseDownEvent.mousePosition</c>).</param>
        public static void Show(Vector2 mousePosition)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(MENU_ITEM_SETTINGS), false, OnSettingsClicked);
            menu.AddItem(new GUIContent(MENU_ITEM_COPY_URL), false, OnCopyUrlClicked);
            menu.DropDown(new Rect(mousePosition.x, mousePosition.y, 0f, 0f));
        }

        #endregion
    }
}