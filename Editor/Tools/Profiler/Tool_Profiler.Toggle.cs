#nullable enable
using System.ComponentModel;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine.Profiling;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Profiler
    {
        #region TOOL METHODS

        /// <summary>
        /// Enables or disables the Unity Profiler. When enabling with a log file path,
        /// starts recording profiler data to a .raw file for later analysis.
        /// </summary>
        /// <param name="enable">Pass <c>true</c> to enable the profiler, <c>false</c> to disable it.</param>
        /// <param name="logFile">
        /// Optional path for a .raw log file. When provided and <paramref name="enable"/> is
        /// <c>true</c>, binary recording is started to this path.
        /// </param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the new profiler state and, when applicable,
        /// the log file path recording has started to.
        /// </returns>
        [McpTool("profiler-toggle", Title = "Profiler / Toggle")]
        [Description("Enables or disables the Unity Profiler. When enabling with a log file path, " + "starts recording profiler data to a .raw file for later analysis.")]
        public ToolResponse Toggle(
            [Description("true to enable profiler, false to disable.")] bool enable,
            [Description("Optional path for a .raw log file to record profiler data. Only used when enabling.")] string logFile = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (enable)
                {
                    if (!string.IsNullOrWhiteSpace(logFile))
                    {
                        Profiler.logFile = logFile;
                        Profiler.enableBinaryLog = true;
                    }

                    Profiler.enabled = true;
                    return ToolResponse.Text(string.IsNullOrWhiteSpace(logFile) ? "Profiler enabled." : $"Profiler enabled. Recording to '{logFile}'.");
                }
                else
                {
                    Profiler.enabled = false;
                    Profiler.enableBinaryLog = false;
                    Profiler.logFile = "";
                    return ToolResponse.Text("Profiler disabled. Recording stopped.");
                }
            });
        }

        #endregion
    }
}