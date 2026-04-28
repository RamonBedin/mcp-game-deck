#nullable enable

using System.IO;
using GameDeck.Editor.Settings;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.PackageManager;
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
        /// cancel slot) opens the project's repo URL when clicked. App version is a
        /// stub until task 4.2 wires <c>PinBinaryManager.IsInstalled</c>.
        /// </summary>
        private static void OnAboutClicked()
        {
            var packageVersion = GetPackageVersion();
            var updateLine = PinPolling.UpdateAvailable ? $"Update available: v{PinPolling.UpdateVersion}" : ABOUT_UP_TO_DATE;
            var message = $"Package version: v{packageVersion}\n" + $"App version: {ABOUT_APP_VERSION_STUB}\n" + $"{updateLine}";
            bool okClicked = EditorUtility.DisplayDialog(ABOUT_DIALOG_TITLE, message, ABOUT_DIALOG_OK, ABOUT_DIALOG_GITHUB);

            if (!okClicked)
            {
                Application.OpenURL(GITHUB_URL);
            }
        }

        /// <summary>
        /// Reads the current package version from the assembly's <see cref="PackageInfo"/>.
        /// </summary>
        /// <returns>Package version string (e.g. <c>"0.1.0"</c>) or
        /// <see cref="ABOUT_PACKAGE_VERSION_UNKNOWN"/> when metadata cannot be resolved.</returns>
        private static string GetPackageVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PinDropdownMenu).Assembly);
            return packageInfo?.version ?? ABOUT_PACKAGE_VERSION_UNKNOWN;
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