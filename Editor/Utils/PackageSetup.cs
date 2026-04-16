#nullable enable
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Utils
{
    /// <summary>
    /// Automatically copies package resources (.claude/, CLAUDE.md) to the Unity
    /// project root on first install. Uses [InitializeOnLoadMethod] to run once
    /// when the Editor loads after package import.
    /// </summary>
    public static class PackageSetup
    {
        #region CONSTANTS

        private const string SETUP_DONE_KEY = "GameDeck_SetupDone_v1";
        private const string PARENT_DIR = "..";
        private const string DIR_CLAUDE = ".claude";
        private const string FILE_CLAUDE_MD = "CLAUDE.md";
        private const string FILE_SETTINGS_LOCAL = "settings.local.json";

        #endregion

        #region INITIALIZATION METHODS

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            if (SessionState.GetBool(SETUP_DONE_KEY, false))
            {
                return;
            }

            SessionState.SetBool(SETUP_DONE_KEY, true);

            var packagePath = ResolvePackagePath();

            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogWarning("[Game Deck] Could not resolve package path. Skipping setup.");
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, PARENT_DIR));

            CopyDirectoryIfNotExists(Path.Combine(packagePath, DIR_CLAUDE), Path.Combine(projectRoot, DIR_CLAUDE));
            CopyFileIfNotExists(Path.Combine(packagePath, FILE_CLAUDE_MD), Path.Combine(projectRoot, FILE_CLAUDE_MD));
        }

        #endregion

        #region PRIVATE METHODS

        /// <summary>
        /// Resolves the absolute path to the package root using the PackageInfo API.
        /// </summary>
        /// <returns>Absolute path to the package root, or null if not found.</returns>
        private static string? ResolvePackagePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(PackageSetup).Assembly);
            return packageInfo?.resolvedPath;
        }

        /// <summary>
        /// Copies a directory tree from source to destination if the destination does not exist.
        /// Skips files that should remain local (e.g., settings.local.json).
        /// </summary>
        /// <param name="sourceDir">Source directory path inside the package.</param>
        /// <param name="destDir">Destination directory path in the project root.</param>
        private static void CopyDirectoryIfNotExists(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                return;
            }

            if (Directory.Exists(destDir))
            {
                Debug.Log($"[Game Deck] Directory already exists, skipping: {destDir}");
                return;
            }

            CopyDirectoryRecursive(sourceDir, destDir);
            Debug.Log($"[Game Deck] Copied {Path.GetFileName(sourceDir)}/ to project root.");
        }

        /// <summary>
        /// Copies a single file from source to destination if the destination does not exist.
        /// </summary>
        /// <param name="sourceFile">Source file path inside the package.</param>
        /// <param name="destFile">Destination file path in the project root.</param>
        private static void CopyFileIfNotExists(string sourceFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                return;
            }

            if (File.Exists(destFile))
            {
                Debug.Log($"[Game Deck] File already exists, skipping: {destFile}");
                return;
            }

            File.Copy(sourceFile, destFile);
            Debug.Log($"[Game Deck] Copied {Path.GetFileName(sourceFile)} to project root.");
        }

        /// <summary>
        /// Recursively copies all files and subdirectories from source to destination.
        /// Skips settings.local.json to avoid overwriting user-specific settings.
        /// </summary>
        /// <param name="sourceDir">Source directory to copy from.</param>
        /// <param name="destDir">Destination directory to copy to.</param>
        private static void CopyDirectoryRecursive(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            string[] files = Directory.GetFiles(sourceDir);

            for (int i = 0; i < files.Length; i++)
            {
                string fileName = Path.GetFileName(files[i]);

                if (fileName == FILE_SETTINGS_LOCAL)
                {
                    continue;
                }

                string destFile = Path.Combine(destDir, fileName);
                File.Copy(files[i], destFile);
            }

            string[] dirs = Directory.GetDirectories(sourceDir);

            for (int i = 0; i < dirs.Length; i++)
            {
                string dirName = Path.GetFileName(dirs[i]);
                CopyDirectoryRecursive(dirs[i], Path.Combine(destDir, dirName));
            }
        }

        #endregion
    }
}