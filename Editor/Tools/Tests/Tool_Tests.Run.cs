#nullable enable
using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Tests
    {
        #region TOOL METHODS

        /// <summary>
        /// Runs Unity tests using the Test Runner API and returns results.
        /// </summary>
        /// <param name="testMode">Test mode to run: "EditMode", "PlayMode", or "All". Default "All".</param>
        /// <param name="filter">Optional substring to filter test names. Leave empty to run all tests.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with total, passed, failed, skipped counts, duration,
        /// and a list of failed test names, or an error if the run times out.
        /// </returns>
        [McpTool("tests-run", Title = "Tests / Run")]
        [Description("Runs Unity tests (EditMode, PlayMode, or All) and returns pass/fail results.")]
        public ToolResponse Run(
            [Description("Test mode: 'EditMode', 'PlayMode', or 'All'. Default 'All'.")] string testMode = "All",
            [Description("Optional filter to match test names (substring match).")] string filter = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var resultCollector = new TestResultCollector();

                api.RegisterCallbacks(resultCollector);

                var executionFilter = new Filter();

                if (string.Equals(testMode, "EditMode", StringComparison.OrdinalIgnoreCase))
                {
                    executionFilter.testMode = TestMode.EditMode;
                }
                else if (string.Equals(testMode, "PlayMode", StringComparison.OrdinalIgnoreCase))
                {
                    executionFilter.testMode = TestMode.PlayMode;
                }
                else
                {
                    executionFilter.testMode = TestMode.EditMode | TestMode.PlayMode;
                }

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    executionFilter.testNames = new[] { filter };
                }

                api.Execute(new ExecutionSettings(executionFilter));
                int waited = 0;

                while (!resultCollector.IsComplete && waited < 30000)
                {
                    Thread.Sleep(100);
                    waited += 100;
                }

                api.UnregisterCallbacks(resultCollector);

                if (!resultCollector.IsComplete)
                {
                    return ToolResponse.Error("Test run timed out after 30 seconds. Check the Test Runner window for detailed results.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Test Results ({testMode}):");
                sb.AppendLine($"  Total: {resultCollector.Total}");
                sb.AppendLine($"  Passed: {resultCollector.Passed}");
                sb.AppendLine($"  Failed: {resultCollector.Failed}");
                sb.AppendLine($"  Skipped: {resultCollector.Skipped}");
                sb.AppendLine($"  Duration: {resultCollector.Duration:F2}s");

                if (resultCollector.FailedTests.Length > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Failed tests:");

                    for (int i = 0; i < resultCollector.FailedTests.Length; i++)
                    {
                        sb.AppendLine($"  - {resultCollector.FailedTests[i]}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("Note: Test execution monitoring is limited. Check the Test Runner window for detailed results.");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region RESULT COLLECTOR

        /// <summary>
        /// Collects test results from the TestRunnerApi callbacks.
        /// </summary>
        private class TestResultCollector : ICallbacks
        {
            #region FIELDS

            private int _total;
            private int _passed;
            private int _failed;
            private int _skipped;
            private float _duration;
            private readonly System.Collections.Generic.List<string> _failedTests = new();
            private volatile bool _isComplete;

            #endregion

            #region PROPERTIES

            /// <summary>Whether the test run has completed.</summary>
            public bool IsComplete => _isComplete;

            /// <summary>Total test count.</summary>
            public int Total => _total;

            /// <summary>Passed test count.</summary>
            public int Passed => _passed;

            /// <summary>Failed test count.</summary>
            public int Failed => _failed;

            /// <summary>Skipped test count.</summary>
            public int Skipped => _skipped;

            /// <summary>Total duration in seconds.</summary>
            public float Duration => _duration;

            /// <summary>Names of failed tests.</summary>
            public string[] FailedTests => _failedTests.ToArray();

            #endregion

            #region ICALLBACKS

            /// <summary>Called when a test run starts.</summary>
            public void RunStarted(ITestAdaptor testsToRun) { }

            /// <summary>Called when a test run finishes.</summary>
            public void RunFinished(ITestResultAdaptor result)
            {
                _duration = (float)result.Duration;
                _isComplete = true;
            }

            /// <summary>Called when an individual test starts.</summary>
            public void TestStarted(ITestAdaptor test) { }

            /// <summary>Called when an individual test finishes.</summary>
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.Test.IsSuite)
                {
                    _total++;

                    string status = result.TestStatus.ToString();

                    if (string.Equals(status, "Passed", StringComparison.OrdinalIgnoreCase))
                    {
                        _passed++;
                    }

                    else if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase))
                    {
                        _failed++;
                        _failedTests.Add(result.Test.FullName);
                    }
                    else
                    {
                        _skipped++;
                    }
                }
            }

            #endregion
        }

        #endregion
    }
}