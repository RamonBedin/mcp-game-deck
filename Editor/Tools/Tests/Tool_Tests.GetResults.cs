#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for running Unity Test Runner tests and querying the available test tree.
    /// Covers EditMode and PlayMode test execution with pass/fail reporting,
    /// and test list retrieval with recursive suite traversal.
    /// </summary>
    [McpToolType]
    public partial class Tool_Tests
    {
        #region TOOL METHODS

        /// <summary>
        /// Retrieves the test tree for the specified test mode, listing all available tests.
        /// </summary>
        /// <param name="testMode">Test mode to query: "EditMode", "PlayMode", or "All". Default "All".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with an indented list of all test and suite names,
        /// or a notice if no tests are found.
        /// </returns>
        [McpTool("tests-get-results", Title = "Tests / Get Test List", ReadOnlyHint = true)]
        [Description("Lists all available tests in the Test Runner for the specified mode.")]
        public ToolResponse GetResults(
            [Description("Test mode: 'EditMode', 'PlayMode', or 'All'. Default 'All'.")] string testMode = "All"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                TestMode mode;

                if (string.Equals(testMode, "EditMode", System.StringComparison.OrdinalIgnoreCase))
                {
                    mode = TestMode.EditMode;
                }
                else if (string.Equals(testMode, "PlayMode", System.StringComparison.OrdinalIgnoreCase))
                {
                    mode = TestMode.PlayMode;
                }
                else
                {
                    mode = TestMode.EditMode | TestMode.PlayMode;
                }

                ITestAdaptor? result = null;
                void OnTestListReceived(ITestAdaptor root) { result = root; }
                api.RetrieveTestList(mode, OnTestListReceived);

                Object.DestroyImmediate(api);

                if (result == null)
                {
                    return ToolResponse.Text("No tests found.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Tests ({testMode}):");
                CollectTests(result, sb, 0);

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE HELPERS

        /// <summary>
        /// Recursively collects test names from the test adaptor tree.
        /// </summary>
        /// <param name="test">The current test or suite adaptor node.</param>
        /// <param name="sb">The output builder to append to.</param>
        /// <param name="depth">Current recursion depth, used to compute indentation.</param>
        private static void CollectTests(ITestAdaptor test, StringBuilder sb, int depth)
        {
            string indent = new(' ', depth * 2);

            if (!test.IsSuite)
            {
                sb.AppendLine($"{indent}- {test.FullName}");
                return;
            }

            if (depth > 0)
            {
                sb.AppendLine($"{indent}[{test.Name}]");
            }

            if (test.Children != null)
            {
                foreach (var child in test.Children)
                {
                    CollectTests(child, sb, depth + 1);
                }
            }
        }

        #endregion
    }
}