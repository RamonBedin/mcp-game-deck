#nullable enable

using System.IO;
using GameDeck.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Pin
{
    /// <summary>
    /// Builds and displays the pin's dropdown menu — the entry point users hit after
    /// clicking the toolbar pin. Hosts the <c>Open Chat</c>, <c>Settings</c>,
    /// <c>Copy MCP Server URL</c>, <c>Show install folder</c>, and <c>About</c> items
    /// and routes each selection to its handler.
    /// </summary>
    internal static class PinDropdownMenu
    {
        #region CONSTANTS

        private const string MENU_ITEM_OPEN_CHAT = "Open Chat";
        private const string MENU_ITEM_SETTINGS = "Settings";
        private const string MENU_ITEM_COPY_URL = "Copy MCP Server URL";
        private const string MENU_ITEM_SHOW_FOLDER = "Show install folder";
        private const string MENU_ITEM_ABOUT = "About";
        private const string NOTIFICATION_URL_COPIED = "MCP Server URL copied";
        private const string ABOUT_DIALOG_TITLE = "About MCP Game Deck";
        private const string ABOUT_DIALOG_OK = "OK";
        private const string ABOUT_DIALOG_GITHUB = "View on GitHub";
        private const string ABOUT_APP_VERSION_STUB = "not installed";
        private const string ABOUT_UP_TO_DATE = "Up to date";
        private const string ABOUT_PACKAGE_VERSION_UNKNOWN = "unknown";
        private const string GITHUB_URL = "https://github.com/RamonBedin/mcp-game-deck";

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Launches (or focuses, post task 5.1) the Tauri app on the default
        /// <c>/chat</c> route via <see cref="PinLauncher.LaunchOrFocus"/>.
        /// </summary>
        private static void OnOpenChatClicked()
        {
            PinLauncher.LaunchOrFocus();
        }

        /// <summary>
        /// Launches (or focuses, post task 5.1) the Tauri app pointed at the in-app
        /// settings route via <see cref="PinLauncher.LaunchOrFocus"/>.
        /// </summary>
        private static void OnSettingsClicked()
        {
            PinLauncher.LaunchOrFocus("/settings");
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

        /// <summary>
        /// Ensures the install folder exists (creating it empty if absent) and reveals
        /// it in the OS file explorer.
        /// </summary>
        private static void OnShowFolderClicked()
        {
            var path = PinPaths.InstallRoot;

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// Shows a modal dialog with package/app version + update status. The dialog's
        /// "View on GitHub" button (mapped to <see cref="EditorUtility.DisplayDialog(string, string, string, string)"/>'s
        /// cancel slot) opens the project's repo URL when clicked. App version reflects
        /// whether <see cref="PinBinaryManager.IsInstalled(string)"/> sees a binary on
        /// disk for the current package version.
        /// </summary>
        private static void OnAboutClicked()
        {
            var currentVersion = PinBinaryManager.GetCurrentVersion();
            var packageVersionDisplay = currentVersion ?? ABOUT_PACKAGE_VERSION_UNKNOWN;
            var appVersionDisplay = currentVersion != null && PinBinaryManager.IsInstalled(currentVersion) ? $"v{currentVersion}" : ABOUT_APP_VERSION_STUB;
            var updateLine = PinPolling.UpdateAvailable ? $"Update available: v{PinPolling.UpdateVersion}" : ABOUT_UP_TO_DATE;
            var message = $"Package version: v{packageVersionDisplay}\n" + $"App version: {appVersionDisplay}\n" + $"{updateLine}";
            bool okClicked = EditorUtility.DisplayDialog(ABOUT_DIALOG_TITLE, message, ABOUT_DIALOG_OK, ABOUT_DIALOG_GITHUB);

            if (!okClicked)
            {
                Application.OpenURL(GITHUB_URL);
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
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent(MENU_ITEM_SHOW_FOLDER), false, OnShowFolderClicked);
            menu.AddItem(new GUIContent(MENU_ITEM_ABOUT), false, OnAboutClicked);
            menu.DropDown(anchorRect);
        }

        #endregion
    }
}