#nullable enable

using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Builds and displays the pin's dropdown menu — the entry point users hit after
    /// clicking the toolbar pin. Hosts the <c>Open Chat</c>, <c>Settings</c>, and
    /// <c>Copy MCP Server URL</c> items and routes each selection to its handler.
    /// </summary>
    internal static class PinDropdownMenu
    {
        #region CONSTANTS

        private const string MENU_ITEM_OPEN_CHAT = "Open Chat";
        private const string MENU_ITEM_SETTINGS = "Settings";
        private const string MENU_ITEM_COPY_URL = "Copy MCP Server URL";
        private const string NOTIFICATION_URL_COPIED = "MCP Server URL copied";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Stub handler for the <c>Open Chat</c> item. Real launch lands in task 4.5
        /// once <c>PinLauncher.LaunchOrFocus</c> exists.
        /// </summary>
        private static void OnOpenChatClicked()
        {
            McpLogger.Info("[Pin] Open Chat clicked");
        }

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
        /// Displays the dropdown menu anchored under the pin's button rect.
        /// </summary>
        /// <param name="anchorRect">Screen-space rect of the dropdown button (received
        /// from <see cref="UnityEditor.Toolbars.MainToolbarDropdown"/>'s click callback).
        /// <see cref="GenericMenu.DropDown(Rect)"/> uses this rect to position the menu
        /// directly under the trigger.</param>
        public static void Show(Rect anchorRect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(MENU_ITEM_OPEN_CHAT), false, OnOpenChatClicked);
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(MENU_ITEM_SETTINGS), false, OnSettingsClicked);
            menu.AddItem(new GUIContent(MENU_ITEM_COPY_URL), false, OnCopyUrlClicked);
            menu.DropDown(anchorRect);
        }

        #endregion
    }
}