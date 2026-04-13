#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Build
    {
        #region TOOL METHODS

        /// <summary>
        /// Executes a Unity player build for the requested platform and returns a detailed build report.
        /// </summary>
        /// <param name="target">
        /// Target platform identifier. Accepted values: windows64, osx, linux64, android, ios, webgl, uwp, tvos.
        /// Leave empty to use the currently active build target.
        /// </param>
        /// <param name="outputPath">
        /// Filesystem path for the build output. Leave empty to use a default path under Builds/&lt;target&gt;/.
        /// </param>
        /// <param name="scenes">
        /// Comma-separated list of scene asset paths to include. Leave empty to use the scenes
        /// currently enabled in Build Settings.
        /// </param>
        /// <param name="development">
        /// When true, produces a development build with debug symbols enabled.
        /// </param>
        /// <param name="options">
        /// Comma-separated build option flags. Accepted values: clean_build, auto_run,
        /// deep_profiling, compress_lz4, strict_mode, detailed_report.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> containing the build result, output size, duration,
        /// error count, and any error messages from build steps.
        /// </returns>
        [McpTool("build-player", Title = "Build / Build Player")]
        [Description("Triggers a player build for the specified target platform. Returns build result " + "with summary including size, errors, warnings, and duration.")]
        public ToolResponse BuildPlayer(
            [Description("Target platform: windows64, osx, linux64, android, ios, webgl, uwp, tvos. " + "If empty uses current active target.")] string target = "",
            [Description("Output path for the build. If empty uses a default like 'Builds/<target>/'.")] string outputPath = "",
            [Description("Comma-separated scene paths to include. If empty uses scenes from Build Settings.")] string scenes = "",
            [Description("Enable development build with debug symbols. Default false.")] bool development = false,
            [Description("Comma-separated build options: clean_build, auto_run, deep_profiling, " + "compress_lz4, strict_mode, detailed_report")] string options = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                BuildTarget buildTarget;

                if (string.IsNullOrWhiteSpace(target))
                {
                    buildTarget = EditorUserBuildSettings.activeBuildTarget;
                }
                else
                {
                    var parsed = ParseBuildTarget(target);

                    if (parsed == null)
                    {
                        return ToolResponse.Error($"Unknown build target '{target}'. Valid: windows64, osx, linux64, android, ios, webgl, uwp, tvos.");
                    }

                    buildTarget = parsed.Value;
                }

                string[] scenePaths;

                if (string.IsNullOrWhiteSpace(scenes))
                {
                    var enabledScenes = new List<string>();

                    foreach (var s in EditorBuildSettings.scenes)
                    {
                        if (s.enabled)
                        {
                            enabledScenes.Add(s.path);
                        }
                    }

                    scenePaths = enabledScenes.ToArray();
                }
                else
                {
                    var splitScenes = new List<string>();

                    foreach (var s in scenes.Split(','))
                    {
                        var trimmed = s.Trim();

                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            splitScenes.Add(trimmed);
                        }
                    }

                    scenePaths = splitScenes.ToArray();
                }

                if (scenePaths.Length == 0)
                {
                    return ToolResponse.Error("No scenes to build. Add scenes to Build Settings or specify them.");
                }

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    var ext = buildTarget == BuildTarget.StandaloneWindows64 ? ".exe" : buildTarget == BuildTarget.StandaloneOSX ? ".app" : buildTarget == BuildTarget.Android ? ".apk" : "";
                    outputPath = $"Builds/{buildTarget}/{PlayerSettings.productName}{ext}";
                }

                var buildOptions = BuildOptions.None;

                if (development)
                {
                    buildOptions |= BuildOptions.Development;
                }

                if (!string.IsNullOrWhiteSpace(options))
                {
                    foreach (var rawOpt in options.Split(','))
                    {
                        var opt = rawOpt.Trim().ToLowerInvariant();

                        buildOptions |= opt switch
                        {
                            "clean_build" => BuildOptions.CleanBuildCache,
                            "auto_run" => BuildOptions.AutoRunPlayer,
                            "deep_profiling" => BuildOptions.EnableDeepProfilingSupport,
                            "compress_lz4" => BuildOptions.CompressWithLz4,
                            "strict_mode" => BuildOptions.StrictMode,
                            "detailed_report" => BuildOptions.DetailedBuildReport,
                            _ => BuildOptions.None
                        };
                    }
                }

                var buildPlayerOptions = new BuildPlayerOptions
                {
                    scenes = scenePaths,
                    locationPathName = outputPath,
                    target = buildTarget,
                    options = buildOptions
                };

                var report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                return FormatBuildReport(report);
            });
        }

        #endregion

        #region HELPER METHODS

        /// <summary>
        /// Formats a <see cref="BuildReport"/> into a human-readable summary including result,
        /// platform, output path, size, duration, error and warning counts, and any error messages.
        /// </summary>
        /// <param name="report">The build report returned by <see cref="BuildPipeline.BuildPlayer"/>.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> text response on success, or an error response when the
        /// build result is not <see cref="BuildResult.Succeeded"/>, both containing the full summary.
        /// </returns>
        private static ToolResponse FormatBuildReport(BuildReport report)
        {
            var sb = new StringBuilder();
            var summary = report.summary;
            sb.AppendLine($"Build Result: {summary.result}");
            sb.AppendLine($"  Platform: {summary.platform}");
            sb.AppendLine($"  Output Path: {summary.outputPath}");
            sb.AppendLine($"  Total Size: {summary.totalSize / (1024 * 1024):F2} MB");
            sb.AppendLine($"  Total Time: {summary.totalTime}");
            sb.AppendLine($"  Total Errors: {summary.totalErrors}");
            sb.AppendLine($"  Total Warnings: {summary.totalWarnings}");

            if (summary.totalErrors > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Errors:");

                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            sb.AppendLine($"  [{step.name}] {msg.content}");
                        }
                    }
                }
            }

            return summary.result == BuildResult.Succeeded ? ToolResponse.Text(sb.ToString()) : ToolResponse.Error(sb.ToString());
        }

        #endregion
    }
}