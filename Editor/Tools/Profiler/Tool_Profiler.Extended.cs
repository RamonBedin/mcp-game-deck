#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tools for Unity Profiler management and performance analysis.
    /// Covers profiler start/stop, counter queries, memory snapshots, profiler area configuration,
    /// frame timing, Frame Debugger control, and profiler status reporting.
    /// </summary>
    [McpToolType]
    public partial class Tool_Profiler
    {
        #region TOOL METHODS

        /// <summary>Starts the profiler with optional log file.</summary>
        /// <param name="logFile">File path for the profiler log output. Empty to disable file logging.</param>
        /// <param name="deep">When true, enables deep profiling to capture all method calls. Default false.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the profiler started, with deep and log details if applicable.</returns>
        [McpTool("profiler-start", Title = "Profiler / Start")]
        [Description("Starts the Unity Profiler, optionally logging to a file.")]
        public ToolResponse Start(
            [Description("File path for profiler log. Empty = no file.")] string logFile = "",
            [Description("Enable deep profiling.")] bool deep = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!string.IsNullOrWhiteSpace(logFile))
                {
                    Profiler.logFile = logFile;
                }

                Profiler.enabled = true;
                ProfilerDriver.deepProfiling = deep;

                return ToolResponse.Text($"Profiler started." + (deep ? " (deep profiling)" : "") + (!string.IsNullOrWhiteSpace(logFile) ? $" Log: {logFile}" : ""));
            });
        }

        /// <summary>Stops the profiler.</summary>
        /// <returns>A <see cref="ToolResponse"/> confirming the profiler was stopped.</returns>
        [McpTool("profiler-stop", Title = "Profiler / Stop")]
        [Description("Stops the Unity Profiler.")]
        public ToolResponse Stop()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                Profiler.enabled = false;
                return ToolResponse.Text("Profiler stopped.");
            });
        }

        /// <summary>Gets profiler counters.</summary>
        /// <param name="category">Profiler category name: "Render", "Memory", "Physics", or "Scripts". Default "Render".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing each counter name and its last recorded value,
        /// with a note when a counter is unavailable.
        /// </returns>
        [McpTool("profiler-get-counters", Title = "Profiler / Get Counters", ReadOnlyHint = true)]
        [Description("Reads specific profiler counters by category.")]
        public ToolResponse GetCounters(
            [Description("Category name (e.g. 'Render', 'Scripts', 'Memory', 'Physics').")] string category = "Render"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Profiler Counters ({category}):");

                string[] counters = category switch
                {
                    "Render" => new[] { "Batches Count", "SetPass Calls Count", "Triangles Count", "Vertices Count" },
                    "Memory" => new[] { "Total Used Memory", "Total Reserved Memory", "GC Used Memory", "GC Reserved Memory" },
                    "Physics" => new[] { "Active Dynamic Bodies", "Active Kinematic Bodies", "Contacts Count" },
                    _ => new[] { "Main Thread", "Render Thread" },
                };

                Unity.Profiling.ProfilerCategory profilerCat = category switch
                {
                    "Memory" => Unity.Profiling.ProfilerCategory.Memory,
                    "Physics" => Unity.Profiling.ProfilerCategory.Physics,
                    "Scripts" => Unity.Profiling.ProfilerCategory.Scripts,
                    "Audio" => Unity.Profiling.ProfilerCategory.Audio,
                    "Ai" => Unity.Profiling.ProfilerCategory.Ai,
                    _ => Unity.Profiling.ProfilerCategory.Render,
                };

                for (int i = 0; i < counters.Length; i++)
                {
                    try
                    {
                        using var rec = Unity.Profiling.ProfilerRecorder.StartNew(profilerCat, counters[i]);
                        sb.AppendLine($"  {counters[i]}: {rec.LastValue}");
                    }
                    catch (System.Exception ex)
                    {
                        sb.AppendLine($"  {counters[i]}: (unavailable — {ex.Message})");
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Takes a memory snapshot.</summary>
        /// <param name="snapshotPath">Reserved path for future snapshot-to-file support. Default "Assets/MemorySnapshot".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with total allocated, reserved, unused, Mono heap/used,
        /// and temp allocator sizes in MB.
        /// </returns>
        [McpTool("profiler-memory-snapshot", Title = "Profiler / Memory Snapshot")]
        [Description("Takes a memory snapshot for analysis.")]
        public ToolResponse MemorySnapshot(
            [Description("Save path. Default 'Assets/MemorySnapshot'.")] string snapshotPath = "Assets/MemorySnapshot"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Memory Snapshot:");

                if (!string.IsNullOrWhiteSpace(snapshotPath))
                {
                    sb.AppendLine("Note: snapshotPath is reserved for future snapshot-to-file support.");
                }

                sb.AppendLine($"  Total Allocated: {Profiler.GetTotalAllocatedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Total Reserved: {Profiler.GetTotalReservedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Total Unused: {Profiler.GetTotalUnusedReservedMemoryLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Mono Heap: {Profiler.GetMonoHeapSizeLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Mono Used: {Profiler.GetMonoUsedSizeLong() / (1024.0 * 1024.0):F2} MB");
                sb.AppendLine($"  Temp Allocator: {Profiler.GetTempAllocatorSize() / (1024.0 * 1024.0):F2} MB");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Profiler ping with system info.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with enabled, supported, deep profiling, and log file status.
        /// </returns>
        [McpTool("profiler-ping", Title = "Profiler / Ping", ReadOnlyHint = true)]
        [Description("Returns profiler status and system performance info.")]
        public ToolResponse Ping()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var sb = new StringBuilder();
                sb.AppendLine("Profiler Status:");
                sb.AppendLine($"  Enabled: {Profiler.enabled}");
                sb.AppendLine($"  Supported: {Profiler.supported}");
                sb.AppendLine($"  Deep Profiling: {ProfilerDriver.deepProfiling}");
                sb.AppendLine($"  Log File: {Profiler.logFile}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Enables/disables profiler areas.</summary>
        /// <param name="enabledAreas">Comma-separated area names to enable (e.g. "CPU,Memory,Rendering"). All others are disabled.</param>
        /// <returns>A <see cref="ToolResponse"/> listing each profiler area and its resulting ON/OFF state.</returns>
        [McpTool("profiler-set-areas", Title = "Profiler / Set Areas")]
        [Description("Enables or disables profiler areas (CPU, GPU, Rendering, Memory, etc.).")]
        public ToolResponse SetAreas(
            [Description("Comma-separated areas to enable: 'CPU,Memory,Rendering'. Others disabled.")] string enabledAreas = "CPU,Memory"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                string[] areas = enabledAreas.Split(',');
                var sb = new StringBuilder();
                sb.AppendLine("Profiler areas updated:");

                var areaValues = System.Enum.GetValues(typeof(ProfilerArea));

                for (int i = 0; i < areaValues.Length; i++)
                {
                    var area = (ProfilerArea)areaValues.GetValue(i);
                    bool enable = false;

                    for (int j = 0; j < areas.Length; j++)
                    {
                        if (area.ToString().IndexOf(areas[j].Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            enable = true;
                            break;
                        }
                    }

                    Profiler.SetAreaEnabled(area, enable);
                    sb.AppendLine($"  {area}: {(enable ? "ON" : "OFF")}");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Lists memory snapshot files.</summary>
        /// <param name="searchPath">Directory to search recursively. Defaults to the project root when empty.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> listing up to 20 snapshot files with name, size, and last-modified date,
        /// plus a count of any additional files beyond the limit.
        /// </returns>
        [McpTool("profiler-memory-list-snapshots", Title = "Profiler / List Snapshots", ReadOnlyHint = true)]
        [Description("Lists memory profiler snapshot files (.snap) in the project.")]
        public ToolResponse ListSnapshots(
            [Description("Search path. Default project root.")] string searchPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(searchPath))
                {
                    searchPath = System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".";
                }

                var sb = new StringBuilder();
                string[] files = System.IO.Directory.GetFiles(searchPath, "*.snap", System.IO.SearchOption.AllDirectories);
                sb.AppendLine($"Found {files.Length} snapshot files:");
                int max = files.Length < 20 ? files.Length : 20;

                for (int i = 0; i < max; i++)
                {
                    var fi = new System.IO.FileInfo(files[i]);
                    sb.AppendLine($"  {fi.Name} ({fi.Length / 1024} KB, {fi.LastWriteTime:yyyy-MM-dd HH:mm})");
                }

                if (files.Length > 20)
                {
                    sb.AppendLine($"  ... and {files.Length - 20} more");
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Compares two memory snapshots (placeholder).</summary>
        /// <param name="snapshotA">File path to the first snapshot.</param>
        /// <param name="snapshotB">File path to the second snapshot.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the name, size, and size delta of both snapshots,
        /// or an error if either path is missing or not found on disk.
        /// </returns>
        [McpTool("profiler-memory-compare", Title = "Profiler / Compare Snapshots", ReadOnlyHint = true)]
        [Description("Compares two memory snapshots. Requires com.unity.memoryprofiler package.")]
        public ToolResponse CompareSnapshots(
            [Description("Path to snapshot A.")] string snapshotA = "",
            [Description("Path to snapshot B.")] string snapshotB = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(snapshotA) || string.IsNullOrWhiteSpace(snapshotB))
                {
                    return ToolResponse.Error("Both snapshotA and snapshotB paths are required.");
                }

                if (!System.IO.File.Exists(snapshotA))
                {
                    return ToolResponse.Error($"Snapshot not found: {snapshotA}");
                }

                if (!System.IO.File.Exists(snapshotB))
                {
                    return ToolResponse.Error($"Snapshot not found: {snapshotB}");
                }

                var fiA = new System.IO.FileInfo(snapshotA);
                var fiB = new System.IO.FileInfo(snapshotB);

                return ToolResponse.Text($"Snapshot comparison (basic):\n  A: {fiA.Name} ({fiA.Length / 1024} KB)\n  B: {fiB.Name} ({fiB.Length / 1024} KB)\n  Size diff: {(fiB.Length - fiA.Length) / 1024} KB\n\nFor detailed comparison, use the Memory Profiler window (Window > Analysis > Memory Profiler).");
            });
        }

        /// <summary>Enables Frame Debugger.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the Frame Debugger was disabled,
        /// or an error if the internal type or method is not accessible.
        /// </returns>
        [McpTool("profiler-frame-debugger-enable", Title = "Profiler / Frame Debugger Enable")]
        [Description("Enables the Frame Debugger for inspecting draw calls.")]
        public ToolResponse FrameDebuggerEnable()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var utilType = System.Type.GetType("UnityEditorInternal.FrameDebuggerUtility, UnityEditor");

                if (utilType == null)
                {
                    return ToolResponse.Error("FrameDebuggerUtility not available.");
                }

                var enableMethod = utilType.GetMethod("SetEnabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (enableMethod == null)
                {
                    return ToolResponse.Error("SetEnabled method not found.");
                }
                try
                {
                    enableMethod.Invoke(null, new object[] { true });
                }
                catch (System.Exception ex)
                {
                    return ToolResponse.Error($"Failed to enable Frame Debugger: {ex.Message}");
                }

                return ToolResponse.Text("Frame Debugger enabled.");
            });
        }

        /// <summary>Disables Frame Debugger.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the Frame Debugger was disabled,
        /// or an error if the internal type or method is not accessible.
        /// </returns>
        [McpTool("profiler-frame-debugger-disable", Title = "Profiler / Frame Debugger Disable")]
        [Description("Disables the Frame Debugger.")]
        public ToolResponse FrameDebuggerDisable()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var utilType = System.Type.GetType("UnityEditorInternal.FrameDebuggerUtility, UnityEditor");

                if (utilType == null)
                {
                    return ToolResponse.Error("FrameDebuggerUtility not available.");
                }

                var enableMethod = utilType.GetMethod("SetEnabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (enableMethod == null)
                {
                    return ToolResponse.Error("SetEnabled method not found.");
                }
                try
                {
                    enableMethod.Invoke(null, new object[] { false });
                }
                catch (System.Exception ex)
                {
                    return ToolResponse.Error($"Failed to disable Frame Debugger: {ex.Message}");
                }
                return ToolResponse.Text("Frame Debugger disabled.");
            });
        }

        /// <summary>Lists Frame Debugger draw call events.</summary>
        /// <param name="pageSize">Maximum number of events to describe. Default 50.</param>
        /// <param name="cursor">Starting event index for pagination. Default 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the total draw event count and a note to use the
        /// Frame Debugger window for detailed inspection, or an error if the utility type is unavailable.
        /// </returns>
        [McpTool("profiler-frame-debugger-events", Title = "Profiler / Frame Debugger Events", ReadOnlyHint = true)]
        [Description("Lists draw call events from the Frame Debugger (must be enabled first).")]
        public ToolResponse FrameDebuggerEvents(
            [Description("Max events to return.")] int pageSize = 50,
            [Description("Starting event index.")] int cursor = 0
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var utilType = System.Type.GetType("UnityEditorInternal.FrameDebuggerUtility, UnityEditor");

                if (utilType == null)
                {
                    return ToolResponse.Error("FrameDebuggerUtility not available.");
                }

                var countProp = utilType.GetProperty("count", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

                if (countProp == null)
                {
                    return ToolResponse.Error("count property not found.");
                }

                int total = (int)(countProp.GetValue(null) ?? 0);

                if (total == 0)
                {
                    return ToolResponse.Text("No frame debugger events. Enable Frame Debugger first.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Frame Debugger Events ({total} total, showing {cursor}-{cursor + pageSize}):");
                sb.AppendLine("(Detailed event inspection requires the Frame Debugger window.)");
                sb.AppendLine($"  Total draw events: {total}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}