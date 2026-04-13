#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Profiler
    {
        #region TOOL METHODS

        /// <summary>
        /// Gets frame timing data from <see cref="FrameTimingManager"/>. Returns CPU/GPU frame times,
        /// present wait time, and other timing metrics for the requested number of recent frames.
        /// </summary>
        /// <param name="frameCount">Number of recent frames to sample. Clamped to the range [1, 10].</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with per-frame CPU/GPU timing in milliseconds.
        /// When more than one frame is captured, averaged CPU and GPU times are appended.
        /// Returns an informational message when no timing data is available (e.g. outside Play Mode).
        /// </returns>
        [McpTool("profiler-frame-timing", Title = "Profiler / Frame Timing")]
        [Description("Gets frame timing data from FrameTimingManager. Returns CPU/GPU frame times, " + "present wait time and other timing metrics. Requires the application to be running (Play Mode).")]
        public ToolResponse FrameTiming(
            [Description("Number of frames to sample. Default 1, max 10.")] int frameCount = 1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (frameCount < 1)
                {
                    frameCount = 1;
                }

                if (frameCount > 10)
                {
                    frameCount = 10;
                }

                var timings = new FrameTiming[frameCount];
                FrameTimingManager.CaptureFrameTimings();
                uint captured = FrameTimingManager.GetLatestTimings((uint)frameCount, timings);

                if (captured == 0)
                {
                    return ToolResponse.Text("No frame timing data available. Ensure Play Mode is active.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Frame Timing ({captured} frame(s) captured):");

                for (int i = 0; i < (int)captured; i++)
                {
                    var t = timings[i];
                    sb.AppendLine($"  Frame {i}:");
                    sb.AppendLine($"    CPU Frame Time: {t.cpuFrameTime:F3} ms");
                    sb.AppendLine($"    CPU Main Thread Frame Time: {t.cpuMainThreadFrameTime:F3} ms");
                    sb.AppendLine($"    CPU Main Thread Present Wait Time: {t.cpuMainThreadPresentWaitTime:F3} ms");
                    sb.AppendLine($"    CPU Render Thread Frame Time: {t.cpuRenderThreadFrameTime:F3} ms");
                    sb.AppendLine($"    GPU Frame Time: {t.gpuFrameTime:F3} ms");
                }

                if (captured > 1)
                {
                    double avgCpu = 0, avgGpu = 0;

                    for (int i = 0; i < (int)captured; i++)
                    {
                        avgCpu += timings[i].cpuFrameTime;
                        avgGpu += timings[i].gpuFrameTime;
                    }

                    avgCpu /= captured;
                    avgGpu /= captured;
                    sb.AppendLine();
                    sb.AppendLine($"  Averages: CPU {avgCpu:F3} ms, GPU {avgGpu:F3} ms");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}