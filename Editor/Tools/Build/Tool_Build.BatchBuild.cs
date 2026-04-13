#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for building, configuring, and managing Unity project builds.
    /// Covers single and batch platform builds, build settings, scene management,
    /// platform switching, and player settings configuration.
    /// </summary>
    [McpToolType]
    public partial class Tool_Build
    {
        #region TOOL METHODS

        /// <summary>
        /// Builds the project for each platform in the supplied list and returns a consolidated result summary.
        /// </summary>
        /// <param name="targets">
        /// Comma-separated list of platform identifiers to build.
        /// Accepted values per token: windows64, osx, linux64, android, ios, webgl, uwp, tvos.
        /// </param>
        /// <param name="outputDir">
        /// Base output directory. Each platform produces a subfolder named after its build target.
        /// Defaults to Builds/.
        /// </param>
        /// <param name="development">
        /// When true, all platform builds are produced as development builds with debug symbols.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing the per-platform outcome (size, duration, or error count)
        /// and a final succeeded/failed tally. Returns an error response if any individual build failed.
        /// </returns>
        [McpTool("build-batch", Title = "Build / Batch Build")]
        [Description("Triggers builds for multiple target platforms in sequence. Returns summary of " + "all build results. Useful for cross-platform release builds.")]
        public ToolResponse BatchBuild(
            [Description("Comma-separated target platforms: windows64, osx, linux64, android, ios, webgl, uwp, tvos")] string targets,
            [Description("Base output directory for all builds. Each target gets a subfolder. Default: 'Builds/'")] string outputDir = "Builds",
            [Description("Enable development build for all targets. Default false.")] bool development = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(targets))
                {
                    return ToolResponse.Error("At least one target is required.");
                }

                var targetList = new List<string>();

                foreach (var t in targets.Split(','))
                {
                    var trimmed = t.Trim();

                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        targetList.Add(trimmed);
                    }
                }

                var scenePathList = new List<string>();

                foreach (var s in EditorBuildSettings.scenes)
                {
                    if (s.enabled)
                    {
                        scenePathList.Add(s.path);
                    }
                }

                var scenePaths = scenePathList.ToArray();

                if (scenePaths.Length == 0)
                {
                    return ToolResponse.Error("No enabled scenes in Build Settings.");
                }

                var sb = new StringBuilder();
                sb.AppendLine("Batch Build Results:");
                sb.AppendLine();

                int succeeded = 0;
                int failed = 0;

                foreach (var targetName in targetList)
                {
                    var buildTarget = ParseBuildTarget(targetName);

                    if (buildTarget == null)
                    {
                        sb.AppendLine($"  [{targetName}] SKIPPED — unknown platform");
                        failed++;
                        continue;
                    }

                    var ext = buildTarget.Value == BuildTarget.StandaloneWindows64 ? ".exe" : buildTarget.Value == BuildTarget.StandaloneOSX ? ".app" : buildTarget.Value == BuildTarget.Android ? ".apk" : "";
                    var path = $"{outputDir}/{buildTarget.Value}/{PlayerSettings.productName}{ext}";
                    var buildOptions = BuildOptions.None;

                    if (development)
                    {
                        buildOptions |= BuildOptions.Development;
                    }

                    var buildPlayerOptions = new BuildPlayerOptions
                    {
                        scenes = scenePaths,
                        locationPathName = path,
                        target = buildTarget.Value,
                        options = buildOptions
                    };

                    var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                    var summary = report.summary;

                    if (summary.result == BuildResult.Succeeded)
                    {
                        sb.AppendLine($"  [{targetName}] SUCCESS — {summary.totalSize / (1024 * 1024):F2} MB, {summary.totalTime}");
                        succeeded++;
                    }
                    else
                    {
                        sb.AppendLine($"  [{targetName}] FAILED — {summary.totalErrors} errors");
                        failed++;
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"Summary: {succeeded} succeeded, {failed} failed out of {targetList.Count} targets.");

                return failed > 0 ? ToolResponse.Error(sb.ToString()) : ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}