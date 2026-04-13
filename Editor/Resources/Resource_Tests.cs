#nullable enable
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;

namespace GameDeck.Editor.Resources
{
    /// <summary>
    /// MCP Resource that discovers available test scripts and test assembly definitions
    /// in the project, categorized by EditMode and PlayMode.
    /// </summary>
    [McpResourceType]
    public class Resource_Tests
    {
        #region CONSTANTS

        private const string MIME_TEXT_PLAIN = "text/plain";
        private const string ASSETS_ROOT_FOLDER = "Assets";
        private const string PATH_KEYWORD_TEST = "test";
        private const string PATH_KEYWORD_EDITOR = "editor";
        private const string PATH_KEYWORD_EDITMODE = "editmode";
        private const string PATH_KEYWORD_RUNTIME = "runtime";
        private const string PATH_KEYWORD_PLAYMODE = "playmode";

        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Returns a summary of test assemblies and test scripts found in the project.
        /// </summary>
        /// <param name="uri">The resource URI requested by the MCP client.</param>
        /// <returns>An array of resource content entries containing the test overview as plain text.</returns>
        [McpResource
        (
            Name = "Available Tests",
            Route = "mcp-game-deck://tests",
            MimeType = "text/plain",
            Description = "Lists available test assemblies and test scripts in the project. " + "Shows EditMode and PlayMode test files found under Tests/ or Editor/Tests/ folders."
        )]
        public ResourceResponse[] GetTests(string uri)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                var testGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { ASSETS_ROOT_FOLDER });
                var editModeTests = new StringBuilder();
                var playModeTests = new StringBuilder();
                int editCount = 0, playCount = 0;

                foreach (var guid in testGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var lowerPath = path.ToLowerInvariant();

                    if (!lowerPath.Contains(PATH_KEYWORD_TEST))
                    {
                        continue;
                    }

                    if (lowerPath.Contains(PATH_KEYWORD_EDITOR) || lowerPath.Contains(PATH_KEYWORD_EDITMODE))
                    {
                        editModeTests.AppendLine($"    {path}");
                        editCount++;
                    }
                    else if (lowerPath.Contains(PATH_KEYWORD_RUNTIME) || lowerPath.Contains(PATH_KEYWORD_PLAYMODE))
                    {
                        playModeTests.AppendLine($"    {path}");
                        playCount++;
                    }
                    else
                    {
                        playModeTests.AppendLine($"    {path}");
                        playCount++;
                    }
                }

                var asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
                var testAssemblies = new StringBuilder();
                int asmCount = 0;

                foreach (var guid in asmdefGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);

                    if (path.ToLowerInvariant().Contains(PATH_KEYWORD_TEST))
                    {
                        testAssemblies.AppendLine($"    {path}");
                        asmCount++;
                    }
                }

                sb.AppendLine("Test Overview:");
                sb.AppendLine();

                if (asmCount > 0)
                {
                    sb.AppendLine($"  Test Assemblies ({asmCount}):");
                    sb.Append(testAssemblies);
                    sb.AppendLine();
                }

                if (editCount > 0)
                {
                    sb.AppendLine($"  EditMode Tests ({editCount}):");
                    sb.Append(editModeTests);
                    sb.AppendLine();
                }

                if (playCount > 0)
                {
                    sb.AppendLine($"  PlayMode Tests ({playCount}):");
                    sb.Append(playModeTests);
                    sb.AppendLine();
                }

                if (editCount == 0 && playCount == 0 && asmCount == 0)
                {
                    sb.AppendLine("  No test files found in the project.");
                }

                sb.AppendLine("To run tests, use Window > General > Test Runner in Unity Editor.");

                return ResourceResponse.CreateText(uri: uri, mimeType: MIME_TEXT_PLAIN, text: sb.ToString()).MakeArray();
            });
        }

        #endregion
    }
}